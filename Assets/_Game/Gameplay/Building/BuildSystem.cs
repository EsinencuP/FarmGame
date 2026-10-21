using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>
    /// Управляет пользовательским режимом строительства: выбирает клетку курсором,
    /// показывает предпросмотр, проверяет место и выполняет покупку, перенос или удаление.
    /// </summary>
    [DefaultExecutionOrder(-175)]
    public sealed class BuildSystem : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingVisual
        {
            public string definitionId;
            public GameObject prefab;
        }

        [Header("Imported Building Art")]
        [SerializeField] private BuildingVisual[] visualPrefabs = Array.Empty<BuildingVisual>();

        [Header("Build Preview")]
        [SerializeField] private Shader ghostShader;
        [SerializeField, Range(0.1f, 0.8f)] private float ghostOpacity = 0.35f;
        [Tooltip("Цвет предпросмотра, когда размещение разрешено.")]
        [SerializeField] private Color validPreviewColor = new Color(0.25f, 0.9f, 0.35f);
        [Tooltip("Цвет предпросмотра, когда размещение запрещено.")]
        [SerializeField] private Color invalidPreviewColor = new Color(0.95f, 0.20f, 0.16f);
        [Tooltip("Высота плоского объёма предпросмотра.")]
        [SerializeField, Min(0.01f)] private float previewHeight = 0.18f;
        [Tooltip("Зазор между краем клетки и предпросмотром.")]
        [SerializeField, Min(0f)] private float previewCellGap = 0.08f;
        [Tooltip("Высота предпросмотра над поверхностью мира.")]
        [SerializeField] private float previewLift = 0.12f;
        [Tooltip("Интервал обновления текста строительной подсказки.")]
        [SerializeField, Min(0.02f)] private float promptRefreshInterval = 0.1f;
        [Tooltip("Отступ физической проверки от края footprint.")]
        [SerializeField, Min(0.001f)] private float overlapInset = 0.07f;
        [Tooltip("Вертикальный зазор физической проверки над землёй.")]
        [SerializeField, Min(0f)] private float overlapGroundClearance = 0.05f;

        // Связывает стабильный id постройки с её текущим объектом в сцене.
        private readonly Dictionary<string, BuildingView> _views = new Dictionary<string, BuildingView>();
        // Переиспользуемый буфер физической проверки исключает выделение массива каждый кадр.
        private readonly Collider[] _overlaps = new Collider[32];
        // Layout является источником истины для размещения и занятости клеток.
        private BuildingLayout _layout;
        // Зависимости дают доступ к сетке, посевам, деньгам, вводу и лучу камеры.
        private GridSystem _grid;
        private CropSystem _crops;
        private WalletSystem _wallet;
        private InputReader _input;
        private Camera _camera;
        // Preview — временная геометрия, а два материала обозначают допустимое и запрещённое место.
        private GameObject _preview;
        private GameObject _previewModel;
        private string _previewDefinition;
        private Renderer[] _previewModelRenderers;
        private Renderer _previewRenderer;
        private Material _validPreviewMaterial;
        private Material _invalidPreviewMaterial;
        private Material _validGhostMaterial;
        private Material _invalidGhostMaterial;
        private Material[][] _validModelMaterials;
        private Material[][] _invalidModelMaterials;
        private bool? _previewValid;
        // Состояние текущего сеанса строительства и выбранного объекта.
        private int _selected;
        private int _turns;
        private string _movingId;
        private string _deleteId;
        private Vector2Int _target;
        private bool _hasTarget;
        // Ограничивает обновление текста подсказки десятью разами в секунду.
        private float _nextPromptAt;

        // Публичное состояние читают контроллер игрока, селектор клетки и UI.
        public bool IsActive { get; private set; }
        public string CurrentPrompt { get; private set; } = string.Empty;
        public int Count => _layout?.Count ?? 0;
        public BuildingDefinition SelectedDefinition => BuildingDefinition.Catalog[_selected];

        /// <summary>Подключает зависимости, создаёт пустой layout и объект предпросмотра.</summary>
        public void Configure(
            InputReader input,
            GridSystem grid,
            CropSystem crops,
            WalletSystem wallet,
            Camera camera,
            bool allowSceneCreation = false)
        {
            _input = input; _grid = grid; _crops = crops; _wallet = wallet; _camera = camera;
            if (_previewModel != null)
            {
                _previewModel.SetActive(false);
                if (Application.isPlaying) Destroy(_previewModel); else DestroyImmediate(_previewModel);
            }
            _previewModel = null;
            _previewDefinition = null;
            _layout = NewLayout();
            grid.Buildings = this;
            // При запуске выгруженной сцены переиспользуем сохранённый preview; создаём его только при выгрузке/в тесте.
            var existingPreview = transform.Find("Building preview");
            if (existingPreview == null && !allowSceneCreation)
            {
                throw new InvalidOperationException("Baked building preview is missing. Rebuild Prototype scene in Edit Mode.");
            }
            _preview = existingPreview == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : existingPreview.gameObject;
            _preview.name = "Building preview";
            _preview.transform.SetParent(transform, false);
            RuntimeMaterials.RemoveCollider(_preview);
            _previewRenderer = _preview.GetComponent<Renderer>();
            // Материалы предпросмотра создаются один раз и затем переключаются без аллокаций.
            var materials = GetComponentInParent<RuntimeMaterials>();
            if (materials == null)
            {
                throw new MissingComponentException("Building System has no RuntimeMaterials owner.");
            }
            _validPreviewMaterial = materials.Get(validPreviewColor);
            _invalidPreviewMaterial = materials.Get(invalidPreviewColor);
            ReleaseGhostMaterials();
            var shader = ghostShader != null ? ghostShader : Shader.Find("MyLittleFarm/BuildingGhost");
            if (shader == null) throw new MissingReferenceException("Building ghost shader is missing.");
            _validGhostMaterial = GhostMaterial(shader, validPreviewColor);
            _invalidGhostMaterial = GhostMaterial(shader, invalidPreviewColor);
            var existingFront = _preview.transform.Find("Preview front");
            if (existingFront == null && !allowSceneCreation)
            {
                throw new InvalidOperationException("Baked preview direction marker is missing.");
            }
            var front = existingFront == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : existingFront.gameObject;
            front.name = "Preview front";
            front.transform.SetParent(_preview.transform, false);
            front.transform.localPosition = new Vector3(0, 0.55f, -0.46f);
            front.transform.localScale = new Vector3(0.35f, 0.12f, 0.06f);
            RuntimeMaterials.RemoveCollider(front);
            RuntimeMaterials.Paint(front.GetComponent<Renderer>(), Color.white);
            _preview.SetActive(false);
            if (_previewModel != null) _previewModel.SetActive(false);
        }

        /// <summary>Создаёт пустую модель размещения без искусственных границ мира.</summary>
        private static BuildingLayout NewLayout() => new BuildingLayout(BuildingDefinition.Catalog);

        private GameObject VisualFor(string definitionId)
        {
            if (visualPrefabs == null) return null;
            foreach (var entry in visualPrefabs)
                if (entry != null && entry.definitionId == definitionId) return entry.prefab;
            return null;
        }

        /// <summary>Сообщает layout, запрещает ли GridSystem строительство на клетке.</summary>
        private bool IsTerrainBlocked(int x, int z, string ignoreId = null)
        {
            var position = new Vector2Int(x, z);
            var cell = _grid.GetCell(position);
            // При переносе собственный footprint разрешён, но клетки других владельцев остаются занятыми.
            if (ignoreId != null && cell.occupantId == ignoreId
                && (cell.type == CellType.Building || cell.type == CellType.BuildingEdge)) return false;
            return !_grid.CanBuild(position) || _crops.Contains(position);
        }

        /// <summary>Возвращает признак занятости клетки постройкой.</summary>
        public bool IsOccupied(Vector2Int cell) => _layout != null && _layout.IsOccupied(cell.x, cell.y);
        /// <summary>Возвращает копию постройки под указанной клеткой либо null.</summary>
        public BuildingRuntimeState GetAt(Vector2Int cell) => _layout.Get(_layout.BuildingAt(cell.x, cell.y));
        /// <summary>Создаёт отделённый снимок всех построек для сохранения.</summary>
        public List<BuildingRuntimeState> Capture() => _layout.Capture();

        /// <summary>Включает или выключает режим, сбрасывая незавершённый перенос и удаление.</summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            _movingId = null; _deleteId = null;
            _input.BuildModeActive = active;
            _input.SuppressGameplayThisFrame = true;
            _preview.SetActive(false);
            if (_previewModel != null) _previewModel.SetActive(false);
            _grid.Select(null);
            _nextPromptAt = 0;
            CurrentPrompt = active ? PlacementPrompt(SelectedDefinition, _turns, false, false, "Наведите курсор на участок") : string.Empty;
        }

        private void Update()
        {
            // Обработка организована как конечный автомат: обычное размещение, перенос или подтверждение удаления.
            if (_input == null || _layout == null) return;
            if (_input.BuildTogglePressed) SetActive(!IsActive);
            if (!IsActive) return;
            _input.SuppressGameplayThisFrame = true;
            if (_input.CancelPressed)
            {
                if (_movingId != null || _deleteId != null) { _movingId = null; _deleteId = null; }
                else SetActive(false);
                return;
            }
            if (_input.BuildSelection >= 0)
            {
                _selected = _input.BuildSelection;
                _movingId = null; _deleteId = null; _turns = 0;
            }
            // Луч из позиции курсора пересекает горизонтальную плоскость фермы и переводится в клетку.
            _hasTarget = false;
            if (_input.HasPointer && _camera != null)
            {
                var ray = _camera.ScreenPointToRay(_input.PointerPosition);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance))
                    _hasTarget = _grid.TryWorldToCell(ray.GetPoint(distance), out _target);
            }
            if (_deleteId != null)
            {
                // В состоянии подтверждения остальные строительные действия временно недоступны.
                _preview.SetActive(false);
                if (_previewModel != null) _previewModel.SetActive(false);
                var state = _layout.Get(_deleteId);
                if (state == null) { _deleteId = null; return; }
                CurrentPrompt = $"Удалить {_layout.Definition(state.definitionId).Name}? Возврат: {state.paidCost / 2} монет\nE / Enter — подтвердить\nEsc / ПКМ — отменить";
                if (_input.ConfirmPressed || (_input.InteractPressed && !_input.PointerInteractPressed))
                {
                    TryRemove(_deleteId, out var reason);
                    GameEvents.RaiseStatusChanged(reason);
                    _deleteId = null;
                }
                return;
            }
            if (_hasTarget && _movingId == null && (_input.MoveBuildingPressed || _input.DeleteBuildingPressed))
            {
                // Перенос и удаление начинаются только если под курсором действительно есть постройка.
                var hovered = GetAt(_target);
                if (hovered != null && _input.MoveBuildingPressed)
                {
                    _movingId = hovered.id; _turns = hovered.quarterTurns;
                    for (var i = 0; i < BuildingDefinition.Catalog.Count; i++)
                        if (BuildingDefinition.Catalog[i].Id == hovered.definitionId) _selected = i;
                }
                else if (hovered != null && _input.DeleteBuildingPressed)
                { _deleteId = hovered.id; _preview.SetActive(false); if (_previewModel != null) _previewModel.SetActive(false); return; }
            }
            if (_input.RotateBuildingPressed) _turns = (_turns + 1) % 4;
            var reasonText = "Наведите курсор на участок";
            // Валидность влияет одновременно на цвет предпросмотра и возможность подтвердить действие.
            var valid = _hasTarget && CanPlace(SelectedDefinition.Id, _target, _turns, _movingId, out reasonText);
            if (valid && _movingId == null && _wallet.Coins < SelectedDefinition.Price)
            { valid = false; reasonText = "Недостаточно монет"; }
            _preview.SetActive(_hasTarget);
            UpdateModelPreview(valid);
            if (_hasTarget)
            {
                _preview.transform.position = BuildingView.Center(_target.x, _target.y, _turns, SelectedDefinition, _grid)
                    + Vector3.up * previewLift;
                _preview.transform.rotation = Quaternion.Euler(0, _turns * 90f, 0);
                _preview.transform.localScale = new Vector3(
                    Mathf.Max(0.01f, SelectedDefinition.Width * _grid.CellSize - previewCellGap),
                    previewHeight,
                    Mathf.Max(0.01f, SelectedDefinition.Depth * _grid.CellSize - previewCellGap));
                var material = valid ? _validPreviewMaterial : _invalidPreviewMaterial;
                if (_previewRenderer.sharedMaterial != material) _previewRenderer.sharedMaterial = material;
            }
            if (Time.unscaledTime >= _nextPromptAt || _input.BuildSelection >= 0 || _input.RotateBuildingPressed)
            {
                _nextPromptAt = Time.unscaledTime + promptRefreshInterval;
                CurrentPrompt = PlacementPrompt(SelectedDefinition, _turns, _movingId != null, valid, reasonText);
            }
            if (_input.InteractPressed && _hasTarget)
            {
                var success = _movingId == null
                    ? TryPlace(SelectedDefinition.Id, _target, _turns, out reasonText)
                    : TryMove(_movingId, _target, _turns, out reasonText);
                if (success) _movingId = null;
                GameEvents.RaiseStatusChanged(reasonText);
            }
        }

        /// <summary>Shared text for runtime and data-only Editor preview; does not touch game state.</summary>
        public static string PlacementPrompt(BuildingDefinition definition, int turns, bool moving, bool valid, string reason)
        {
            return $"СТРОИТЕЛЬСТВО · {definition.Name} · {(moving ? "бесплатный перенос" : definition.Price + " монет")} · {definition.RotatedWidth(turns)}×{definition.RotatedDepth(turns)}\n"
                + "1 Дом · 2 Склад · 3 Стойка · 4 Клумба · 5 Курятник\n"
                + (valid ? "Размещение доступно" : reason)
                + "\nE / ЛКМ — подтвердить   Q / R — повернуть   Esc / ПКМ — отмена\nM — перенос   Delete — удалить   B — выйти из строительства";
        }

        private Material GhostMaterial(Shader shader, Color tint)
        {
            tint.a = ghostOpacity;
            return new Material(shader) { color = tint, hideFlags = HideFlags.DontSave };
        }

        private void ReleaseGhostMaterials()
        {
            foreach (var material in new[] { _validGhostMaterial, _invalidGhostMaterial })
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            _validGhostMaterial = _invalidGhostMaterial = null;
            _previewValid = null;
        }

        private void OnDestroy() => ReleaseGhostMaterials();

        private void UpdateModelPreview(bool valid)
        {
            if (_previewDefinition != SelectedDefinition.Id)
            {
                if (_previewModel != null) { _previewModel.SetActive(false); Destroy(_previewModel); }
                _previewDefinition = SelectedDefinition.Id;
                _previewModel = new GameObject("Building model preview");
                _previewModel.transform.SetParent(transform, false);
                var prefab = VisualFor(SelectedDefinition.Id);
                if (prefab != null)
                {
                    var model = VisualPrefab.CreateBuilding(prefab, _previewModel.transform, new Vector3(
                        SelectedDefinition.Width * _grid.CellSize - 0.12f, SelectedDefinition.Height,
                        SelectedDefinition.Depth * _grid.CellSize - 0.12f));
                    var direction = model.transform.localRotation * Vector3.back;
                    var front = _preview.transform.Find("Preview front");
                    front.localPosition = new Vector3(direction.x * 0.46f, 0.55f, direction.z * 0.46f);
                    front.localRotation = Quaternion.LookRotation(direction, Vector3.up);
                }
                else
                {
                    var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.transform.SetParent(_previewModel.transform, false);
                    body.transform.localScale = new Vector3(SelectedDefinition.Width * _grid.CellSize - 0.12f,
                        SelectedDefinition.Height, SelectedDefinition.Depth * _grid.CellSize - 0.12f);
                    body.transform.localPosition = Vector3.up * SelectedDefinition.Height * 0.5f;
                    RuntimeMaterials.RemoveCollider(body);
                }
                _previewModelRenderers = _previewModel.GetComponentsInChildren<Renderer>();
                _validModelMaterials = new Material[_previewModelRenderers.Length][];
                _invalidModelMaterials = new Material[_previewModelRenderers.Length][];
                for (var i = 0; i < _previewModelRenderers.Length; i++)
                {
                    var renderer = _previewModelRenderers[i];
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    _validModelMaterials[i] = new Material[count];
                    _invalidModelMaterials[i] = new Material[count];
                    for (var slot = 0; slot < count; slot++)
                    {
                        _validModelMaterials[i][slot] = _validGhostMaterial;
                        _invalidModelMaterials[i][slot] = _invalidGhostMaterial;
                    }
                }
                foreach (var collider in _previewModel.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var child in _previewModel.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
                _previewValid = null;
            }
            _previewModel.SetActive(_hasTarget);
            if (!_hasTarget) return;
            _previewModel.transform.position = BuildingView.Center(_target.x, _target.y, _turns, SelectedDefinition, _grid);
            _previewModel.transform.rotation = Quaternion.Euler(0f, _turns * 90f, 0f);
            if (_previewValid != valid)
            {
                for (var i = 0; i < _previewModelRenderers.Length; i++)
                    _previewModelRenderers[i].sharedMaterials = valid ? _validModelMaterials[i] : _invalidModelMaterials[i];
                _previewValid = valid;
            }
        }

        public bool CanPlace(string definitionId, Vector2Int cell, int turns, string ignoreId, out string reason)
        {
            // Сначала выполняется дешёвая клеточная проверка, затем физическая проверка объёмом.
            if (!_layout.CanPlace(definitionId, cell.x, cell.y, turns,
                    (x, z) => IsTerrainBlocked(x, z, ignoreId), ignoreId, out reason)) return false;
            var definition = _layout.Definition(definitionId);
            var center = BuildingView.Center(cell.x, cell.y, turns, definition, _grid);
            // Учитываем игрока и объекты сцены, но игнорируем переносимую постройку.
            Physics.SyncTransforms();
            var count = Physics.OverlapBoxNonAlloc(
                center + Vector3.up * (definition.Height * 0.5f + overlapGroundClearance),
                new Vector3(
                    Mathf.Max(0.001f, definition.RotatedWidth(turns) * _grid.CellSize * 0.5f - overlapInset),
                    definition.Height * 0.5f,
                    Mathf.Max(0.001f, definition.RotatedDepth(turns) * _grid.CellSize * 0.5f - overlapInset)),
                _overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) { reason = "Слишком много препятствий"; return false; }
            for (var i = 0; i < count; i++)
            {
                var view = _overlaps[i].GetComponentInParent<BuildingView>();
                if (ignoreId != null && view != null && view.BuildingId == ignoreId) continue;
                reason = "Мешает персонаж или объект";
                return false;
            }
            return true;
        }

        public bool TryPlace(string definitionId, Vector2Int cell, int turns, out string reason)
        {
            // Деньги списываются только после всех проверок и успешного добавления в layout.
            if (!CanPlace(definitionId, cell, turns, null, out reason)) return false;
            var definition = _layout.Definition(definitionId);
            if (_wallet.Coins < definition.Price) { reason = "Недостаточно монет"; return false; }
            var state = new BuildingRuntimeState { id = Guid.NewGuid().ToString("N"), definitionId = definitionId,
                x = cell.x, z = cell.y, quarterTurns = turns, paidCost = definition.Price };
            if (!_layout.TryPlace(state, (x, z) => IsTerrainBlocked(x, z), out reason)) return false;
            _views.Add(state.id, BuildingView.Create(transform, definition, state, _grid,
                VisualFor(definition.Id)));
            _grid.OccupyWithBuilding(cell,
                new Vector2Int(definition.RotatedWidth(turns), definition.RotatedDepth(turns)), state.id);
            _wallet.TrySpend(definition.Price);
            reason = "Постройка размещена";
            GameEvents.RaiseProgressAction(definitionId == "house" ? "house"
                : definitionId == "storage" ? "chest" : "build");
            return true;
        }

        public bool TryMove(string id, Vector2Int cell, int turns, out string reason)
        {
            // Перенос бесплатный: меняются занятость layout и Transform существующего view.
            var state = _layout.Get(id);
            if (state == null) { reason = "Постройка не найдена"; return false; }
            if (!CanPlace(state.definitionId, cell, turns, id, out reason)
                || !_layout.TryMove(id, cell.x, cell.y, turns,
                    (x, z) => IsTerrainBlocked(x, z, id), out reason)) return false;
            var definition = _layout.Definition(state.definitionId);
            _grid.FreeBuilding(new Vector2Int(state.x, state.z),
                new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), id);
            _grid.OccupyWithBuilding(cell,
                new Vector2Int(definition.RotatedWidth(turns), definition.RotatedDepth(turns)), id);
            _views[id].Apply(_layout.Get(id), _layout.Definition(state.definitionId), _grid);
            reason = "Постройка перенесена";
            return true;
        }

        /// <summary>Удаляет подтверждённую постройку и возвращает половину уплаченной цены.</summary>
        public bool TryRemove(string id, out string reason)
        {
            var state = _layout.Get(id);
            if (state == null) { reason = "Постройка не найдена"; return false; }
            var refund = state.paidCost / 2;
            if ((long)_wallet.Coins + refund > int.MaxValue) { reason = "Кошелёк заполнен"; return false; }
            _layout.TryRemove(id, out _);
            var definition = _layout.Definition(state.definitionId);
            _grid.FreeBuilding(new Vector2Int(state.x, state.z),
                new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), id);
            _views[id].gameObject.SetActive(false);
            Destroy(_views[id].gameObject);
            _views.Remove(id);
            _wallet.AddCoins(refund);
            reason = $"Постройка удалена. Возврат: {refund} монет";
            return true;
        }

        public void ValidateSnapshot(List<BuildingRuntimeState> states, Func<int, int, bool> blocked)
        {
            // Проверка строит отдельный layout, поэтому не затрагивает текущий мир.
            var candidate = NewLayout();
            foreach (var state in states)
                if (!candidate.TryPlace(state, blocked, out var reason)) throw new InvalidDataException(reason);
        }

        public void Restore(List<BuildingRuntimeState> states)
        {
            // Сначала целиком собирается кандидат; старые виды удаляются только после его успеха.
            var candidate = NewLayout();
            foreach (var state in states)
                if (!candidate.TryPlace(state, (x, z) => IsTerrainBlocked(x, z, state.id), out var reason))
                    throw new InvalidDataException(reason);
            SetActive(false);
            foreach (var view in _views.Values) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            _views.Clear();
            _layout = candidate;
            foreach (var state in candidate.Capture())
            {
                var definition = candidate.Definition(state.definitionId);
                _grid.OccupyWithBuilding(new Vector2Int(state.x, state.z),
                    new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), state.id);
                _views.Add(state.id, BuildingView.Create(transform, candidate.Definition(state.definitionId), state,
                    _grid, VisualFor(state.definitionId)));
            }
        }

        public bool IsNearMarket(Vector3 position, float distance)
        {
            // Позволяет торговой стойке работать как дополнительная точка продажи.
            foreach (var view in _views.Values)
            {
                if (view.DefinitionId != "market") continue;
                var offset = position - view.transform.position;
                offset.y = 0;
                if (offset.sqrMagnitude <= distance * distance) return true;
            }
            return false;
        }

        private void OnDisable()
        {
            // Гарантированно снимает блокировку обычного управления при отключении компонента.
            if (_input != null) _input.BuildModeActive = false;
            IsActive = false;
            if (_preview != null) _preview.SetActive(false);
            if (_previewModel != null) _previewModel.SetActive(false);
        }
    }
}
