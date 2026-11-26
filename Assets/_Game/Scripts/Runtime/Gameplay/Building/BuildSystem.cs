using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.UI;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>
    /// Управляет пользовательским режимом строительства: выбирает клетку курсором,
    /// показывает предпросмотр, проверяет место и выполняет покупку, перенос или удаление.
    /// </summary>
    [DefaultExecutionOrder(-175)]
    public sealed partial class BuildSystem : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingVisual
        {
            public string definitionId;
            public GameObject prefab;
        }

        [Header("Imported Building Art")]
        [SerializeField] private BuildingVisual[] visualPrefabs = Array.Empty<BuildingVisual>();
        [SerializeField] private GameObject[] houseLevelPrefabs=Array.Empty<GameObject>();
        [SerializeField] private MyLittleFarm.UI.BuildCustomizationUI customizationUI;
        public event Action<bool> ActiveChanged;

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
        private CropSystem_AI _crops;
        private WalletSystem _wallet;
        private InputReader_AI _input;
        private Camera _camera;
        private MyLittleFarm.Gameplay.Animals.AnimalSystem_AI _animals;
        // Preview — временная геометрия, а два материала обозначают допустимое и запрещённое место.
        private GameObject _preview;
        private GameObject _previewModel;
        private string _previewDefinition;
        private Renderer[] _previewModelRenderers;
        private Material _validGhostMaterial;
        private Material _invalidGhostMaterial;
        private Material[][] _validModelMaterials;
        private Material[][] _invalidModelMaterials;
        private bool? _previewValid;
        private GameObject _footprint;
        private Material _footprintMaterial;
        private readonly List<LineRenderer> _footprintLines = new List<LineRenderer>();
        private readonly HashSet<Vector2Int> _previewBlockedCells = new HashSet<Vector2Int>();
        private readonly List<Bounds> _previewObstacleBounds = new List<Bounds>();
        // Состояние текущего сеанса строительства и выбранного объекта.
        private int _selected;
        private int _turns;
        private enum Management { Place, Move, Remove }
        private Management _management;
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
        internal bool HasWorkerHousing => _layout?.HasWorkerHousing() == true;
        internal int AvailableCoins => _wallet?.Coins ?? 0;
        internal ApplePresses Presses { get; private set; }
        internal ProductionBatches Production { get; private set; }
        public FeederStore Feeders {get;private set;}
        public WarehouseStore Warehouses { get; private set; }
        public BuildingDefinition SelectedDefinition => BuildingDefinition.Catalog[_selected];
        public void BindAnimals(MyLittleFarm.Gameplay.Animals.AnimalSystem_AI animals) => _animals = animals;

        /// <summary>Подключает зависимости, создаёт пустой layout и объект предпросмотра.</summary>
        public void Configure(
            InputReader_AI input,
            GridSystem grid,
            CropSystem_AI crops,
            WalletSystem wallet,
            Camera camera,
            bool allowSceneCreation = false,
            InventorySystem_AI inventory = null)
        {
            _input = input; _grid = grid; _crops = crops; _wallet = wallet; _camera = camera;
            Feeders=inventory==null?null:new FeederStore(this,inventory);
            Warehouses = inventory == null ? null : new WarehouseStore(inventory,this);
            Production = inventory == null ? null : new ProductionBatches(inventory);
            Presses = Production == null ? null : new ApplePresses(Production);
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
            // The old solid slab hides the individual outlines. Keep its transform for the front marker.
            if (_preview.TryGetComponent<Renderer>(out var slab)) slab.enabled = false;
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

        private GameObject VisualFor(string definitionId,int level=0)
        {
            if(definitionId=="feeder")return Resources.Load<GameObject>("FarmContent/Feeder");
            if (definitionId == "mill") return Resources.Load<GameObject>("FarmContent/Mill");
            if (definitionId == "cheese_dairy") return Resources.Load<GameObject>("FarmContent/Dairy");
            if (definitionId == BarnHabitat.BuildingId) return Resources.Load<GameObject>(BarnHabitat.PrefabResource);
            if(definitionId=="house"&&level>=0&&level<houseLevelPrefabs.Length&&houseLevelPrefabs[level]!=null)
                return houseLevelPrefabs[level];
            if (visualPrefabs == null) return null;
            foreach (var entry in visualPrefabs)
                if (entry != null && entry.definitionId == definitionId) return entry.prefab;
            return null;
        }

        /// <summary>Сообщает layout, запрещает ли GridSystem строительство на клетке.</summary>
        private bool IsTerrainBlocked(int x, int z, string ignoreId = null)
            => PlacementBlockReason(new Vector2Int(x, z), ignoreId) != null;

        private string PlacementBlockReason(Vector2Int position, string ignoreId)
        {
            var cell = _grid.GetCell(position);
            // При переносе собственный footprint разрешён, но клетки других владельцев остаются занятыми.
            if (ignoreId != null && cell.occupantId == ignoreId
                && (cell.type == CellType.Building || cell.type == CellType.BuildingEdge)) return null;
            return _grid.BuildingBlockReason(position) ?? (_crops.Contains(position) ? "Здесь растёт культура" : null);
        }

        /// <summary>Возвращает признак занятости клетки постройкой.</summary>
        public bool IsOccupied(Vector2Int cell) => _layout != null && _layout.IsOccupied(cell.x, cell.y);
        /// <summary>Возвращает копию постройки под указанной клеткой либо null.</summary>
        public BuildingRuntimeState GetAt(Vector2Int cell) => _layout?.Get(_layout.BuildingAt(cell.x, cell.y));
        /// <summary>Создаёт отделённый снимок всех построек для сохранения.</summary>
        public List<BuildingRuntimeState> Capture() => _layout.Capture();
        public List<BuildingRuntimeState> CaptureReady() => _layout.Capture(readyOnly: true);
        public BuildingRuntimeState GetBuilding(string id)
        {
            var state = _layout?.Get(id);
            return state?.IsUnderConstruction == false ? state : null;
        }
        public ProductionRecipe RecipeFor(string id) => Production == null ? null : ProductionRecipe.ForBuilding(GetBuilding(id)?.definitionId);
        internal bool IsApplePress(string id) => Presses != null && GetBuilding(id)?.definitionId == ApplePresses.BuildingId;
        public bool TryStartProduction(string id,long now,out string message)
            => TryStartProduction(id, RecipeFor(id)?.Id, now, out message);
        public bool TryStartProduction(string id, string recipeId, long now, out string message)
        {
            var building = RecipeFor(id);
            var recipe = ProductionRecipe.Find(recipeId);
            message = building == null ? "Постройка не найдена." : "Рецепт недоступен в этой постройке.";
            if (recipe == null || recipe.BuildingId != building?.BuildingId || !Production.TryStart(id, recipe, now, out message))
                return false;
            if (_views.TryGetValue(id, out var view))
                MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.ProductionStart, view.transform.position);
            return true;
        }
        public bool TryCollectProduction(string id,long now,out string message)
        {
            message="Постройка не найдена.";
            if (RecipeFor(id) == null || !Production.TryCollect(id, now, out message)) return false;
            if (_views.TryGetValue(id, out var view))
                MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.ProductionCollect, view.transform.position);
            return true;
        }
        public bool ProductionInReach(string id, Vector3 player, float reach=2.4f)
        {
            if(RecipeFor(id)==null) return false;
            return BuildingInReach(id,player,reach);
        }
        public bool IsFeeder(string id)=>Feeders!=null && GetBuilding(id)?.definitionId=="feeder";
        public bool FeederInReach(string id,Vector3 player)=>IsFeeder(id) && BuildingInReach(id,player,2.4f);
        public bool FeederCovers(string id,Vector3 position)=>IsFeeder(id) && BuildingInReach(id,position,FeederStore.Radius);
        public bool IsWarehouse(string id)=>Warehouses!=null && GetBuilding(id)?.definitionId=="storage";
        public BarnDoor DoorFor(string id) => id != null && _views.TryGetValue(id, out var view) ? view.Door : null;
        public bool WarehouseInReach(string id,Vector3 player)=>IsWarehouse(id) && BuildingInReach(id,player,2.4f);
        private bool BuildingInReach(string id,Vector3 player,float reach)
        {
            if(id==null || !_views.TryGetValue(id,out var view))return false;
            var collider=view.GetComponent<Collider>();
            var nearest=collider==null ? view.transform.position : collider.ClosestPoint(player);
            var delta=nearest-player;delta.y=0;return delta.sqrMagnitude<=reach*reach;
        }
        public BuildingView FindFirstView(string definitionId)
        {
            foreach (var view in _views.Values)
                if (view != null && view.DefinitionId == definitionId) return view;
            return null;
        }
        /// <summary>Pure bounds projection for save validation; never instantiates art into the live farm.</summary>
        public Bounds SavedCollisionBounds(BuildingRuntimeState state, BuildingDefinition definition)
        {
            var center = BuildingView.Center(state.x, state.z, state.quarterTurns, definition, _grid);
            var prefab = VisualFor(definition.Id,state.appearanceLevel);
            if (prefab != null && (prefab.GetComponent<BuildingArtMetrics>() != null
                || prefab.GetComponent<DecorationArtMetrics>() != null)
                && VisualPrefab.TryGetLocalBounds(definition.Id == BarnHabitat.BuildingId ? BarnHabitat.SolidModel(prefab) : prefab,
                    prefab.transform,out var local))
            {
                var matrix = Matrix4x4.TRS(center,Quaternion.Euler(0,state.quarterTurns*90f,0),Vector3.one)
                    * Matrix4x4.TRS(prefab.transform.localPosition,prefab.transform.localRotation,prefab.transform.localScale);
                var result = new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
                for (var x=-1;x<=1;x+=2) for(var y=-1;y<=1;y+=2) for(var z=-1;z<=1;z+=2)
                    result.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,new Vector3(x,y,z))));
                return result;
            }
            return new Bounds(center+Vector3.up*definition.Height*.5f,
                new Vector3(definition.RotatedWidth(state.quarterTurns)*_grid.CellSize-.12f,
                    definition.Height,definition.RotatedDepth(state.quarterTurns)*_grid.CellSize-.12f));
        }
        public void SelectDefinition(int index)
        {
            if(index<0||index>=BuildingDefinition.Catalog.Count)return;
            _selected=index;_movingId=null;_deleteId=null;_turns=0;_nextPromptAt=0;
        }

        public void BeginPlacement(int index)
        {
            SelectDefinition(index);
            SetActive(true);
        }

        public void BeginManagement(bool remove)
        {
            SetActive(true);
            _management = remove ? Management.Remove : Management.Move;
            CurrentPrompt = ManagementPrompt();
        }

        private string ManagementPrompt()
        {
            var action = _management == Management.Move ? "переноса" : "сноса";
            return $"Выберите готовую постройку для {action} · {InputReader_AI.ActionHint}\n"
                + $"Настроить · {InputReader_AI.KeyLabel(InputReader_AI.Command.Customize)}   |   Отмена · Esc";
        }

        /// <summary>Включает или выключает режим, сбрасывая незавершённый перенос и удаление.</summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            _management = Management.Place;
            _movingId = null; _deleteId = null;
            _input.BuildModeActive = active;
            _input.SuppressGameplayThisFrame = true;
            _preview.SetActive(false);
            if (_footprint != null) _footprint.SetActive(false);
            if (_previewModel != null) _previewModel.SetActive(false);
            _grid.Select(null);
            _nextPromptAt = 0;
            CurrentPrompt = active ? PlacementPrompt(SelectedDefinition, _turns, false, false, "Наведите курсор на участок") : string.Empty;
            ActiveChanged?.Invoke(active);
        }

        private void Update()
        {
            // Обработка организована как конечный автомат: обычное размещение, перенос или подтверждение удаления.
            if (_input == null || _layout == null) return;
            if (_input.BuildTogglePressed && customizationUI != null)
            {
                customizationUI.ToggleCatalogue();
                return;
            }
            if (_input.ModalInputActive || !IsActive) return;
            _input.SuppressGameplayThisFrame = true;
            if (_input.CancelPressed)
            {
                if (_management != Management.Place || (_movingId == null && _deleteId == null)) SetActive(false);
                else { _movingId = null; _deleteId = null; }
                return;
            }
            if (_input.BuildSelection >= 0) SelectDefinition(_input.BuildSelection);
            // Луч из позиции курсора пересекает горизонтальную плоскость фермы и переводится в клетку.
            _hasTarget = false;
            if (_input.HasPointer && !_input.WorldPointerBlocked && _camera != null
                &&(customizationUI==null||!customizationUI.BlocksWorldPointer(_input.PointerPosition)))
            {
                var ray = _camera.ScreenPointToRay(_input.PointerPosition);
                if (_grid.TryRaycastSurface(ray, out var surfaceHit))
                    _hasTarget = _grid.TryWorldToCell(surfaceHit.point, out _target);
            }
            if (_deleteId != null)
            {
                // В состоянии подтверждения остальные строительные действия временно недоступны.
                _preview.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                if (_previewModel != null) _previewModel.SetActive(false);
                var state = _layout.Get(_deleteId);
                if (state == null) { _deleteId = null; return; }
                CurrentPrompt = FarmText.Get("construction.deletePrompt", _layout.Definition(state.definitionId).Name,
                    FarmText.Coins(state.Refund), InputReader_AI.KeyLabel(InputReader_AI.Command.Interact));
                if (_input.ConfirmPressed || (_input.InteractPressed && !_input.PointerInteractPressed))
                {
                    TryRemove(_deleteId, out var reason);
                    GameEvents.RaiseStatusChanged(reason);
                    _deleteId = null;
                    if (_management == Management.Remove) SetActive(false);
                }
                return;
            }
            if (_hasTarget && _movingId == null)
            {
                var move = _input.MoveBuildingPressed || (_management == Management.Move && _input.InteractPressed);
                var remove = _input.DeleteBuildingPressed || (_management == Management.Remove && _input.InteractPressed);
                if (move || remove || _input.CustomizeBuildingPressed)
                {
                    var hovered = GetAt(_target);
                    if (hovered?.IsUnderConstruction == true)
                    {
                        GameEvents.RaiseStatusChanged("Постройка ещё строится.");
                        return;
                    }
                    if (hovered == null)
                    {
                        GameEvents.RaiseStatusChanged("Наведите курсор на готовую постройку.");
                        return;
                    }
                    if (_input.CustomizeBuildingPressed) customizationUI?.ShowDetails(hovered);
                    else if (move)
                    {
                        _movingId = hovered.id;
                        _turns = hovered.quarterTurns;
                        for (var i = 0; i < BuildingDefinition.Catalog.Count; i++)
                            if (BuildingDefinition.Catalog[i].Id == hovered.definitionId) _selected = i;
                    }
                    else if (Production?.IsOccupied(hovered.id) == true)
                        GameEvents.RaiseStatusChanged(Production.RemovalHint);
                    else if (hovered.definitionId == "storage" && Warehouses?.HasContents(hovered.id) == true)
                        GameEvents.RaiseStatusChanged("Сначала освободите склад. Внутри остаются предметы.");
                    else _deleteId = hovered.id;
                    _preview.SetActive(false);
                    if (_footprint != null) _footprint.SetActive(false);
                    if (_previewModel != null) _previewModel.SetActive(false);
                    return;
                }
            }
            if (_management != Management.Place && _movingId == null)
            {
                CurrentPrompt = ManagementPrompt();
                return;
            }
            if (_input.RotateBuildingPressed)
            { _turns = (_turns + 1) % 4; MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.BuildRotate); }
            var reasonText = "Наведите курсор на участок";
            _previewBlockedCells.Clear();
            _previewObstacleBounds.Clear();
            // Валидность влияет одновременно на цвет предпросмотра и возможность подтвердить действие.
            var valid = _hasTarget && CanPlace(SelectedDefinition.Id, _target, _turns, _movingId, out reasonText,
                collectPreviewBlockers: true);
            if (valid && _movingId == null && _wallet.Coins < SelectedDefinition.Price)
            { valid = false; reasonText = FarmText.MissingCoins(SelectedDefinition.Price - _wallet.Coins); }
            _preview.SetActive(_hasTarget);
            UpdateFootprint();
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
            }
            if (Time.unscaledTime >= _nextPromptAt || _input.BuildSelection >= 0 || _input.RotateBuildingPressed)
            {
                _nextPromptAt = Time.unscaledTime + promptRefreshInterval;
                CurrentPrompt = PlacementPrompt(SelectedDefinition, _turns, _movingId != null, valid, reasonText);
            }
            if (_input.InteractPressed && _hasTarget)
            {
                var success = _movingId == null
                    ? TryStartConstruction(SelectedDefinition.Id, _target, _turns, out reasonText)
                    : TryMove(_movingId, _target, _turns, out reasonText);
                if (success)
                {
                    var editSign = _movingId == null && FarmDecorationCatalog.SupportsText(SelectedDefinition.Id);
                    SetActive(false);
                    if (editSign) customizationUI?.ShowDetails(GetAt(_target));
                }
                GameEvents.RaiseStatusChanged(reasonText);
            }
        }

        /// <summary>Shared text for runtime and data-only Editor preview; does not touch game state.</summary>
        public static string PlacementPrompt(BuildingDefinition definition, int turns, bool moving, bool valid, string reason)
        {
            return FarmText.Get("construction.placement", definition.Name,
                moving ? FarmText.Get("construction.freeMove") : FarmText.Coins(definition.Price),
                definition.RotatedWidth(turns), definition.RotatedDepth(turns),
                valid ? FarmText.Get("construction.valid") : reason, InputReader_AI.ActionHint, InputReader_AI.BuildRotateHint,
                InputReader_AI.KeyLabel(InputReader_AI.Command.Build));
        }

        private Material GhostMaterial(Shader shader, Color tint)
        {
            tint.a = ghostOpacity;
            return new Material(shader) { color = tint, hideFlags = HideFlags.DontSave };
        }

        private void ReleaseGhostMaterials()
        {
            foreach (var material in new[] { _validGhostMaterial, _invalidGhostMaterial, _constructionGhostMaterial })
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            _validGhostMaterial = _invalidGhostMaterial = _constructionGhostMaterial = null;
            _previewValid = null;
        }

        private void OnDestroy()
        {
            ReleaseGhostMaterials();
            if (_footprintMaterial != null) Destroy(_footprintMaterial);
        }

        private void UpdateFootprint()
        {
            if (!_hasTarget)
            {
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }
            if (_footprint == null)
            {
                _footprint = new GameObject("Building footprint");
                _footprint.transform.SetParent(transform, false);
                _footprintMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    hideFlags = HideFlags.DontSave,
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10
                };
            }
            var width = SelectedDefinition.RotatedWidth(_turns);
            var depth = SelectedDefinition.RotatedDepth(_turns);
            var count = width * depth;
            while (_footprintLines.Count < count)
            {
                var line = new GameObject("Footprint cell").AddComponent<LineRenderer>();
                line.transform.SetParent(_footprint.transform, false);
                line.gameObject.layer = 2;
                line.sharedMaterial = _footprintMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.widthMultiplier = 0.027f;
                _footprintLines.Add(line);
            }
            var half = _grid.CellSize * 0.45f;
            for (var i = 0; i < _footprintLines.Count; i++)
            {
                var line = _footprintLines[i];
                line.enabled = i < count;
                if (!line.enabled) continue;
                var position = _target + new Vector2Int(i % width, i / width);
                var center = _grid.CellToWorld(position);
                var blocked = _previewBlockedCells.Contains(position) || PlacementBlockReason(position, _movingId) != null
                    || PreviewOverlapsCell(center);
                line.positionCount = blocked ? 7 : 5;
                line.startColor = line.endColor = blocked ? invalidPreviewColor : validPreviewColor;
                for (var corner = 0; corner < line.positionCount; corner++)
                {
                    var index = corner < 5 ? corner % 4 : corner == 5 ? 2 : 0;
                    var x = center.x + (index == 0 || index == 3 ? -half : half);
                    var z = center.z + (index < 2 ? -half : half);
                    line.SetPosition(corner, new Vector3(x, _grid.GetSurfaceHeight(x, z) + 0.065f, z));
                }
            }
            _footprint.SetActive(true);
        }

        private bool PreviewOverlapsCell(Vector3 center)
        {
            var halfCell = _grid.CellSize * 0.5f;
            foreach (var bounds in _previewObstacleBounds)
            {
                var offset = bounds.center - center;
                if (Mathf.Abs(offset.x) < halfCell + bounds.extents.x
                    && Mathf.Abs(offset.z) < halfCell + bounds.extents.z) return true;
            }
            return false;
        }

        private void UpdateModelPreview(bool valid)
        {
            var level=_movingId==null?0:(_layout.Get(_movingId)?.appearanceLevel??0);
            var previewKey=SelectedDefinition.Id+":"+level;
            if (_previewDefinition != previewKey)
            {
                if (_previewModel != null) { _previewModel.SetActive(false); Destroy(_previewModel); }
                _previewDefinition = previewKey;
                _previewModel = new GameObject("Building model preview");
                _previewModel.transform.SetParent(transform, false);
                var model = CreatePreviewArt(_previewModel.transform, SelectedDefinition, level);
                var direction = model.localRotation * Vector3.back;
                var front = _preview.transform.Find("Preview front");
                front.localPosition = new Vector3(direction.x * 0.46f, 0.55f, direction.z * 0.46f);
                front.localRotation = Quaternion.LookRotation(direction, Vector3.up);
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
            => CanPlace(definitionId, cell, turns, ignoreId, out reason, collectPreviewBlockers: false);

        private bool CanPlace(string definitionId, Vector2Int cell, int turns, string ignoreId, out string reason,
            bool collectPreviewBlockers)
        {
            if (_layout == null || _grid == null) { reason = "Строительная система ещё не готова"; return false; }
            // Предпросмотр собирает остальные препятствия, даже если первая причина уже известна.
            string cellReason = null;
            var layoutValid = _layout.CanPlace(definitionId, cell.x, cell.y, turns,
                (x, z) => (cellReason = PlacementBlockReason(new Vector2Int(x, z), ignoreId)) != null, ignoreId, out var layoutReason);
            var firstBlock = layoutValid ? null : cellReason ?? layoutReason;
            if (!layoutValid && !collectPreviewBlockers) { reason = firstBlock; return false; }

            var definition = _layout.Definition(definitionId);
            var width = definition.RotatedWidth(turns);
            var depth = definition.RotatedDepth(turns);
            var blockedCells = collectPreviewBlockers ? _previewBlockedCells : null;
            var animalBlocks = _animals?.BlocksFootprint(cell, width, depth, ignoreId, blockedCells) == true;
            if (animalBlocks || _animals?.IsChanging == true) firstBlock ??= "В области постройки находятся животные";
            if (definitionId == BarnHabitat.BuildingId && (firstBlock == null || collectPreviewBlockers))
            {
                var barnBlock = BarnBlockReason(cell, turns, ignoreId, width, depth, blockedCells);
                firstBlock ??= barnBlock;
            }
            if (firstBlock == null || collectPreviewBlockers)
            {
                var obstacleReason = PhysicalBlockReason(definition, cell, turns, ignoreId, collectPreviewBlockers);
                firstBlock ??= obstacleReason;
            }
            reason = firstBlock ?? string.Empty;
            return firstBlock == null;
        }

        private string BarnBlockReason(Vector2Int cell, int turns, string ignoreId, int width, int depth,
            HashSet<Vector2Int> blockedCells)
        {
            var candidate = new BuildingRuntimeState { id = ignoreId, definitionId = BarnHabitat.BuildingId,
                x = cell.x, z = cell.y, quarterTurns = turns };
            string firstBlock = null;
            if (_animals != null && !_animals.CanMoveBarn(candidate, out var animalReason))
            {
                if (blockedCells == null) return animalReason;
                firstBlock = animalReason;
            }
            var baseY = _grid.GridToWorld(cell).y;
            for (var z = 0; z < depth; z++)
                for (var x = 0; x < width; x++)
                {
                    var position = cell + new Vector2Int(x, z);
                    if (Mathf.Abs(_grid.GridToWorld(position).y - baseY) <= .12f) continue;
                    firstBlock ??= "Для коровника и загона нужен ровный участок";
                    if (blockedCells == null) return firstBlock;
                    blockedCells.Add(position);
                }
            return firstBlock;
        }

        private string PhysicalBlockReason(BuildingDefinition definition, Vector2Int cell, int turns, string ignoreId,
            bool collectPreviewBlockers)
        {
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
            var firstBlock = count == _overlaps.Length ? "Слишком много препятствий" : null;
            if (firstBlock != null && !collectPreviewBlockers) return firstBlock;
            for (var i = 0; i < count; i++)
            {
                var obstacle = _overlaps[i];
                var view = obstacle.GetComponentInParent<BuildingView>();
                if (ignoreId != null && view != null && view.BuildingId == ignoreId) continue;
                var animal = obstacle.GetComponentInParent<AnimalView>();
                if (ignoreId != null && animal != null && _animals?.IsResident(animal.AnimalId, ignoreId) == true) continue;
                firstBlock ??= obstacle is CharacterController ? "Игрок стоит в области постройки"
                    : animal != null ? "В области постройки находится животное"
                    : view == null ? "Мешает объект на участке" : "Мешает соседняя постройка";
                if (!collectPreviewBlockers) return firstBlock;
                _previewObstacleBounds.Add(obstacle.bounds);
            }
            return firstBlock;
        }

        // Explicit ready-building placement used by the existing fixture and authoring entry point.
        public bool TryPlace(string definitionId, Vector2Int cell, int turns, out string reason)
            => Place(definitionId, cell, turns, timed: false, out reason);

        public bool TryStartConstruction(string definitionId, Vector2Int cell, int turns, out string reason)
            => Place(definitionId, cell, turns, timed: true, out reason);

        private bool Place(string definitionId, Vector2Int cell, int turns, bool timed, out string reason)
        {
            if (!CanPlace(definitionId, cell, turns, null, out reason)) return false;
            var definition = _layout.Definition(definitionId);
            if (_wallet.Coins < definition.Price) { reason = FarmText.MissingCoins(definition.Price - _wallet.Coins); return false; }
            var duration = timed ? definition.ConstructionSeconds : 0;
            var state = new BuildingRuntimeState { id = Guid.NewGuid().ToString("N"), definitionId = definitionId,
                x = cell.x, z = cell.y, quarterTurns = turns, paidCost = definition.Price,
                constructionDurationSeconds = duration, constructionSecondsRemaining = duration };
            if (!_layout.TryPlace(state, IsPlacementCellBlocked, out reason)) return false;

            var size = new Vector2Int(definition.RotatedWidth(turns), definition.RotatedDepth(turns));
            try
            {
                _grid.OccupyWithBuilding(cell, size, state.id);
                AddBuildingView(state, definition);
            }
            catch
            {
                Rollback();
                throw;
            }
            if (!_wallet.TrySpend(definition.Price))
            {
                Rollback();
                reason = FarmText.MissingCoins(definition.Price - _wallet.Coins);
                return false;
            }

            if (state.IsUnderConstruction)
                reason = $"Строительство началось · {definition.Name} · {FarmText.Duration(duration)}";
            else
            {
                AnnounceCompletion(definitionId);
                reason = "Постройка размещена";
            }
            return true;

            void Rollback()
            {
                _layout.TryRemove(state.id, out _);
                _grid.FreeBuilding(cell, size, state.id);
                RemoveBuildingView(state.id);
            }
        }

        private bool IsPlacementCellBlocked(int x, int z) => IsTerrainBlocked(x, z);

        private static void AnnounceCompletion(string definitionId)
        {
            GameEvents.Record("build", definitionId);
            GameEvents.RaiseProgressAction(definitionId == "house" ? "house" : definitionId == "storage" ? "chest" : "build");
        }

        public bool TryMove(string id, Vector2Int cell, int turns, out string reason)
        {
            // Перенос бесплатный: меняются занятость layout и Transform существующего view.
            var state = _layout.Get(id);
            if (state == null) { reason = "Постройка не найдена"; return false; }
            if (state.IsUnderConstruction) { reason = "Перенос доступен после завершения строительства"; return false; }
            if (!CanPlace(state.definitionId, cell, turns, id, out reason)
                || !_layout.TryMove(id, cell.x, cell.y, turns,
                    (x, z) => IsTerrainBlocked(x, z, id), out reason)) return false;
            var definition = _layout.Definition(state.definitionId);
            _grid.FreeBuilding(new Vector2Int(state.x, state.z),
                new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), id);
            _grid.OccupyWithBuilding(cell,
                new Vector2Int(definition.RotatedWidth(turns), definition.RotatedDepth(turns)), id);
            _views[id].Apply(_layout.Get(id), _layout.Definition(state.definitionId), _grid);
            if (state.definitionId == BarnHabitat.BuildingId) _animals?.ApplyBarnMove(_layout.Get(id));
            reason = "Постройка перенесена";
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.BuildMove, _grid.CellToWorld(cell));
            return true;
        }

        /// <summary>Удаляет подтверждённую постройку и возвращает половину уплаченной цены.</summary>
        public bool TryRemove(string id, out string reason)
        {
            var state = _layout.Get(id);
            if (state == null) { reason = "Постройка не найдена"; return false; }
            if (state.IsUnderConstruction) { reason = "Снос доступен после завершения строительства"; return false; }
            if (state.definitionId == BarnHabitat.BuildingId && (_animals?.IsChanging == true || _animals?.HasResidents(id) == true))
            { reason = "В коровнике живут коровы — удаление запрещено. Перенесите коровник вместе с животными"; return false; }
            if(state.definitionId=="feeder" && (Feeders?.IsChanging==true || Feeders?.Amount(id)>0)){reason="Сначала заберите пшеницу из кормушки.";return false;}
            if(state.definitionId=="storage" && (Warehouses?.IsChanging==true || Warehouses?.HasContents(id)==true))
            {reason="Сначала освободите склад. Внутри остаются предметы.";return false;}
            if (Production?.IsOccupied(id) == true) { reason = Production.RemovalHint; return false; }
            var refund = state.Refund;
            if ((long)_wallet.Coins + refund > int.MaxValue) { reason = "Кошелёк заполнен"; return false; }
            if(state.definitionId=="storage") Warehouses?.ForgetEmpty(id);
            if(state.definitionId=="feeder")Feeders?.ForgetEmpty(id);
            _layout.TryRemove(id, out _);
            var definition = _layout.Definition(state.definitionId);
            _grid.FreeBuilding(new Vector2Int(state.x, state.z),
                new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), id);
            RemoveBuildingView(id);
            _wallet.AddCoins(refund);
            GameEvents.Record("refund", state.definitionId, value: refund);
            reason = $"Постройка удалена. Возврат: {refund} монет";
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.BuildRemove, _grid.CellToWorld(new Vector2Int(state.x, state.z)));
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
            foreach (var view in _views.Values) DestroyView(view.gameObject);
            _views.Clear();
            foreach (var site in _constructionSites.Values) DestroyView(site.Root);
            _constructionSites.Clear();
            _layout = candidate;
            foreach (var state in candidate.Capture())
            {
                var definition = candidate.Definition(state.definitionId);
                _grid.OccupyWithBuilding(new Vector2Int(state.x, state.z),
                    new Vector2Int(definition.RotatedWidth(state.quarterTurns), definition.RotatedDepth(state.quarterTurns)), state.id);
                AddBuildingView(state, definition);
            }
        }

        public bool TrySetSignText(string id,string text,out string reason)
        {
            var state=GetBuilding(id);
            if(state==null||!FarmDecorationCatalog.SupportsText(state.definitionId)){reason="Это не вывеска";return false;}
            if(!_layout.TryCustomize(id,0,FarmDecorationCatalog.CleanText(text),out reason))return false;
            _views[id].Apply(_layout.Get(id),_layout.Definition(state.definitionId),_grid);
            reason="Текст вывески сохранён";return true;
        }

        public bool CanUpgradeHouse(string id,out string reason)
        {
            var state=GetBuilding(id);
            if(state==null||state.definitionId!="house"){reason="Дом не найден";return false;}
            if(state.appearanceLevel>=2){reason="Усадьба уже полностью улучшена";return false;}
            if(!CanPlace("house",new Vector2Int(state.x,state.z),state.quarterTurns,id,out reason))
            {reason="Освободите территорию дома перед улучшением";return false;}
            reason=""; return true;
        }
        public bool TryUpgradeHouse(string id,out string reason)
        {
            if (!CanUpgradeHouse(id,out reason)) return false;
            var market = GetComponentInParent<MyLittleFarm.Core.GameBootstrap>()?.GetComponentInChildren<MyLittleFarm.Gameplay.Economy.SeedShopSystem>(true);
            if (market == null || !market.CanTrade) { reason="Улучшения доступны на рынке"; return false; }
            if (!_wallet.TrySpend(FarmDecorationCatalog.HouseUpgradePrice(_layout.Get(id).appearanceLevel))) { reason="Недостаточно монет"; return false; }
            CompleteHouseUpgrade(id); reason="Дом улучшен"; return true;
        }
        internal void CompleteHouseUpgrade(string id)
        {
            if (!_layout.TryUpgradeHouse(id)) return;
            var updated=_layout.Get(id);var old=_views[id];
            _views[id]=BuildingView.Create(transform,_layout.Definition("house"),updated,_grid,VisualFor("house",updated.appearanceLevel));
            old.gameObject.SetActive(false);Destroy(old.gameObject);
            GameEvents.Record("house_upgrade",amount:updated.appearanceLevel);
            GameEvents.RaiseActionFeedback("upgrade", _views[id].transform.position);
            _previewDefinition=null;
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
            if (_footprint != null) _footprint.SetActive(false);
            if (_previewModel != null) _previewModel.SetActive(false);
            ActiveChanged?.Invoke(false);
        }
    }
}
