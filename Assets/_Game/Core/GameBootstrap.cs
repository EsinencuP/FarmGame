using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Building;
using UnityEngine.SceneManagement;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Точка подключения прототипа: в Play Mode связывает объекты, уже сохранённые в сцене.
    /// Создание и выгрузка иерархии выполняются заранее редакторским PrototypeSceneBaker.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        // Геометрия стартового чанка должна совпадать с размером tilemap-блока сцены.
        private const int ChunkSizeX = 10;
        private const int ChunkSizeZ = 10;
        private const float CellSize = 1f;

        // Защищает от повторного создания объектов при повторном вызове BuildPrototype.
        private bool _isBuilt;

        private void Awake()
        {
            // В рабочей сцене Play Mode разрешено только подключение уже выгруженной иерархии.
            if (!Application.isPlaying)
            {
                return;
            }

            if (GetComponentInChildren<GridSystem>(true) != null)
            {
                InitializeBakedScene();
            }
            else if (SceneManager.GetActiveScene().name == "Prototype")
            {
                Debug.LogError(
                    "Prototype scene is not baked. Use Tools/My Little Farm/Bake Prototype Scene in Edit Mode.",
                    this);
            }
        }

        /// <summary>
        /// Создаёт иерархию только для редакторской выгрузки и изолированных тестов.
        /// Рабочая сцена не вызывает этот метод при обычном запуске.
        /// </summary>
        public void BuildPrototype()
        {
            // Порядок важен: сначала источники данных, потом объекты, которые на них ссылаются.
            if (_isBuilt)
            {
                return;
            }

            // Если сцена уже выгружена, метод становится безопасным повторным подключением без дубликатов.
            if (GetComponentInChildren<GridSystem>(true) != null)
            {
                InitializeBakedScene();
                return;
            }

            _isBuilt = true;
            gameObject.AddComponent<RuntimeMaterials>();
            CreateLighting(transform);

            var input = gameObject.AddComponent<InputReader>();
            var grid = CreateSystem<GridSystem>("Grid System");
            grid.Configure(CellSize, ChunkSizeX, ChunkSizeZ, true);
            CreateStartChunk(transform);
            grid.RegisterSceneChunks(GetComponentsInChildren<TilemapChunk>(true));

            var inventory = CreateSystem<InventorySystem>("Inventory System");
            inventory.ConfigurePrototypeInventory();
            var wallet = CreateSystem<WalletSystem>("Wallet System");
            wallet.Configure(25);
            var selling = CreateSystem<SellingSystem>("Selling System");
            selling.Configure(inventory, wallet);

            var soil = CreateSystem<SoilSystem>("Soil System");
            soil.Configure(grid);
            var crops = CreateSystem<CropSystem>("Crop System");
            crops.Configure(grid);

            var player = CreatePlayer(grid.CellToWorld(new Vector2Int(3, 0)) + Vector3.up * 1.1f, input);
            player.transform.SetParent(transform, true);
            var cameraController = CreateCamera(input, player.transform);
            cameraController.transform.SetParent(transform, true);
            player.SetCamera(cameraController.transform);

            var buildings = CreateSystem<BuildSystem>("Building System");
            buildings.Configure(input, grid, crops, wallet, cameraController.GetComponent<Camera>(), true);

            var selector = CreateSystem<CellSelector>("Cell Selector");
            selector.Configure(grid, player.transform, input, cameraController.GetComponent<Camera>());
            var saleCrate = CreateSaleCrate(new Vector3(8.5f, 0.55f, 1.5f));
            saleCrate.SetParent(transform, true);

            var interaction = CreateSystem<InteractionSystem>("Interaction System");
            interaction.Configure(
                input,
                player.transform,
                saleCrate,
                selector,
                grid,
                soil,
                crops,
                inventory,
                selling);

            var saveSystem = CreateSystem<SaveSystem>("Save System");
            saveSystem.Configure(input, player, grid, crops, inventory, wallet, buildings);

            CreateHud(inventory, wallet, interaction, buildings);
            GameEvents.RaiseStatusChanged("Прототип готов: обработайте клетку перед персонажем");
            if (Application.isPlaying && SceneManager.GetActiveScene().name == "Prototype")
            {
                saveSystem.StartPersistence();
            }
        }

        private T CreateSystem<T>(string objectName) where T : Component
        {
            // Создаёт именованный дочерний объект-контейнер и добавляет требуемый компонент системы.
            var systemObject = new GameObject(objectName);
            systemObject.transform.SetParent(transform, false);
            return systemObject.AddComponent<T>();
        }

        private PlayerController CreatePlayer(Vector3 position, InputReader input)
        {
            // Капсула служит временной моделью; CharacterController обеспечивает движение и столкновения.
            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player";
            playerObject.transform.SetParent(transform, false);
            playerObject.transform.position = position;
            RuntimeMaterials.RemoveCollider(playerObject);
            RuntimeMaterials.Paint(playerObject.GetComponent<Renderer>(), new Color(0.20f, 0.46f, 0.82f));

            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.3f;

            var player = playerObject.AddComponent<PlayerController>();
            player.Configure(input);
            return player;
        }

        private static IsometricCameraController CreateCamera(InputReader input, Transform player)
        {
            // Создаёт основную камеру и настраивает слежение за Transform игрока.
            var cameraObject = new GameObject("Isometric Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.backgroundColor = new Color(0.58f, 0.77f, 0.92f);
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            var controller = cameraObject.AddComponent<IsometricCameraController>();
            controller.Configure(input, player);
            return controller;
        }

        private Transform CreateSaleCrate(Vector3 position)
        {
            // Временный куб обозначает стационарную точку продажи урожая.
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Sale Crate";
            crate.transform.SetParent(transform, false);
            crate.transform.position = position;
            crate.transform.localScale = new Vector3(1.7f, 1.1f, 1.4f);
            RuntimeMaterials.Paint(crate.GetComponent<Renderer>(), new Color(0.88f, 0.52f, 0.12f));
            return crate.transform;
        }

        /// <summary>
        /// Создаёт один стартовый ассет-блок для editor baker и тестовой сцены. В Play Mode
        /// рабочая сцена использует уже сохранённый объект и этот метод не вызывается.
        /// </summary>
        private static void CreateStartChunk(Transform parent)
        {
            var chunkObject = new GameObject("TileBlock_Start");
            chunkObject.transform.SetParent(parent, false);
            chunkObject.transform.position = Vector3.zero;
            chunkObject.AddComponent<TilemapChunk>();

            // Дочерний куб имитирует tilemap-ассет: pivot родителя остаётся в нижнем левом углу чанка.
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Terrain Surface";
            surface.transform.SetParent(chunkObject.transform, false);
            surface.transform.localPosition = new Vector3(ChunkSizeX * CellSize * 0.5f, -0.25f, ChunkSizeZ * CellSize * 0.5f);
            surface.transform.localScale = new Vector3(ChunkSizeX * CellSize, 0.5f, ChunkSizeZ * CellSize);
            RuntimeMaterials.Paint(surface.GetComponent<Renderer>(), new Color(0.24f, 0.45f, 0.18f));
        }

        private static void CreateLighting(Transform parent)
        {
            // Не создаёт второе солнце, если сцена уже содержит направленный или иной источник света.
            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var sun = new GameObject("Sun");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.93f, 0.78f);
            light.shadows = LightShadows.Soft;
        }

        private void CreateHud(InventorySystem inventory, WalletSystem wallet, InteractionSystem interaction, BuildSystem buildings)
        {
            // HUD собирается программно и сразу получает ссылки на данные и подсказки действий.
            var canvasObject = new GameObject("Prototype HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvasObject.transform);
            var stats = CreateText(panel, "Stats", 32, TextAnchor.UpperLeft, new Color(0.12f, 0.15f, 0.10f));
            SetRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -34f), new Vector2(300f, 150f));

            var help = CreateText(canvasObject.transform, "Help", 21, TextAnchor.UpperRight, Color.white);
            help.text = "WASD — движение   Shift — бег   Space — прыжок   Q / Shift+E — камера\nE / ЛКМ — действие   B — строительство   F5 — сохранить   F9 — загрузить";
            SetRect(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(800f, 70f));

            var prompt = CreateText(canvasObject.transform, "Interaction Prompt", 24, TextAnchor.MiddleCenter, Color.white);
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1700f, 130f));

            var status = CreateText(canvasObject.transform, "Status", 24, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.56f));
            SetRect(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 54f));

            var hud = canvasObject.AddComponent<HUDController>();
            hud.Configure(inventory, wallet, stats, status);
            var promptController = canvasObject.AddComponent<InteractionPromptUI>();
            promptController.Configure(interaction, prompt, buildings);
        }

        private static Transform CreatePanel(Transform parent)
        {
            // Создаёт полупрозрачный фон для показателей экономики.
            var panelObject = new GameObject("Stats Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.GetComponent<Image>();
            image.color = new Color(0.94f, 0.90f, 0.73f, 0.92f);
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(350f, 180f);
            return panelObject.transform;
        }

        private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
        {
            // Общая фабрика гарантирует одинаковую настройку всех текстовых элементов HUD.
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            // Применяет якоря, позицию и размер RectTransform в одном месте.
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        /// <summary>Находит сохранённые компоненты сцены и заново передаёт все runtime-зависимости.</summary>
        private void InitializeBakedScene()
        {
            if (_isBuilt)
            {
                return;
            }

            _isBuilt = true;
            RequireComponent<RuntimeMaterials>(gameObject);
            var input = RequireComponent<InputReader>(gameObject);
            var grid = RequireChild<GridSystem>();
            grid.Configure(CellSize, ChunkSizeX, ChunkSizeZ);
            grid.RegisterSceneChunks(GetComponentsInChildren<TilemapChunk>(true));

            var inventory = RequireChild<InventorySystem>();
            inventory.ConfigurePrototypeInventory();
            var wallet = RequireChild<WalletSystem>();
            wallet.Configure(25);
            var selling = RequireChild<SellingSystem>();
            selling.Configure(inventory, wallet);

            var soil = RequireChild<SoilSystem>();
            soil.Configure(grid);
            var crops = RequireChild<CropSystem>();
            crops.Configure(grid);
            var player = RequireChild<PlayerController>();
            player.Configure(input);
            RuntimeMaterials.Paint(player.GetComponent<Renderer>(), new Color(0.20f, 0.46f, 0.82f));
            var cameraController = RequireChild<IsometricCameraController>();
            cameraController.Configure(input, player.transform);
            player.SetCamera(cameraController.transform);

            var buildings = RequireChild<BuildSystem>();
            buildings.Configure(input, grid, crops, wallet, cameraController.GetComponent<Camera>());
            var selector = RequireChild<CellSelector>();
            selector.Configure(grid, player.transform, input, cameraController.GetComponent<Camera>());
            var saleCrate = FindNamedTransform("Sale Crate");
            RuntimeMaterials.Paint(saleCrate.GetComponent<Renderer>(), new Color(0.88f, 0.52f, 0.12f));

            var interaction = RequireChild<InteractionSystem>();
            interaction.Configure(input, player.transform, saleCrate, selector, grid, soil, crops, inventory, selling);
            var saveSystem = RequireChild<SaveSystem>();
            saveSystem.Configure(input, player, grid, crops, inventory, wallet, buildings);

            var hud = RequireChild<HUDController>();
            hud.Configure(inventory, wallet, FindNamedComponent<Text>("Stats"), FindNamedComponent<Text>("Status"));
            RequireChild<InteractionPromptUI>().Configure(
                interaction,
                FindNamedComponent<Text>("Interaction Prompt"),
                buildings);

            GameEvents.RaiseStatusChanged("Прототип готов: наведите курсор на клетку");
            if (Application.isPlaying && SceneManager.GetActiveScene().name == "Prototype")
            {
                saveSystem.StartPersistence();
            }
        }

        /// <summary>Возвращает обязательный компонент на корне или сообщает о повреждённой выгрузке.</summary>
        private static T RequireComponent<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            if (component == null)
            {
                throw new MissingComponentException($"Baked scene is missing {typeof(T).Name} on {owner.name}.");
            }

            return component;
        }

        /// <summary>Возвращает обязательный компонент из дочерней иерархии, включая выключенные объекты.</summary>
        private T RequireChild<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null)
            {
                throw new MissingComponentException($"Baked scene is missing {typeof(T).Name}.");
            }

            return component;
        }

        /// <summary>Находит дочерний Transform по сохранённому имени объекта.</summary>
        private Transform FindNamedTransform(string objectName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            throw new MissingReferenceException($"Baked scene is missing object '{objectName}'.");
        }

        /// <summary>Находит компонент нужного типа на дочернем объекте с заданным именем.</summary>
        private T FindNamedComponent<T>(string objectName) where T : Component
        {
            var target = FindNamedTransform(objectName);
            var component = target.GetComponent<T>();
            if (component == null)
            {
                throw new MissingComponentException($"Object '{objectName}' is missing {typeof(T).Name}.");
            }

            return component;
        }
    }
}
