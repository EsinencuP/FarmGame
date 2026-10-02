using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Building;
using UnityEngine.SceneManagement;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
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
        [Header("Prototype World Bake")]
        [Tooltip("Размер клетки, который будет записан в GridSystem при следующей выгрузке сцены.")]
        [SerializeField, Min(0.01f)] private float bakedCellSize = 1f;
        [Tooltip("Ширина стартового чанка в клетках.")]
        [SerializeField, Min(1)] private int bakedChunkSizeX = 10;
        [Tooltip("Глубина стартового чанка в клетках.")]
        [SerializeField, Min(1)] private int bakedChunkSizeZ = 10;
        [Tooltip("Толщина временной поверхности стартового чанка.")]
        [SerializeField, Min(0.01f)] private float terrainThickness = 0.5f;
        [SerializeField] private Color terrainColor = new Color(0.24f, 0.45f, 0.18f);

        [Header("New Game")]
        [Tooltip("Стартовый баланс до загрузки существующего сохранения.")]
        [SerializeField, Min(0)] private int startingCoins = 25;
        [Tooltip("Стартовая клетка персонажа в новой игре.")]
        [SerializeField] private Vector2Int playerStartCell = new Vector2Int(3, 0);
        [Tooltip("Высота центра персонажа над плоскостью грида при создании.")]
        [SerializeField, Min(0f)] private float playerStartHeight = 1.1f;
        [SerializeField] private Color playerColor = new Color(0.20f, 0.46f, 0.82f);
        [Tooltip("Мировая позиция стартового ящика продажи.")]
        [SerializeField] private Vector3 saleCratePosition = new Vector3(8.5f, 0.55f, 1.5f);
        [SerializeField] private Vector3 saleCrateScale = new Vector3(1.7f, 1.1f, 1.4f);
        [SerializeField] private Color saleCrateColor = new Color(0.88f, 0.52f, 0.12f);

        [Header("Starter Zone Mockup")]
        [Tooltip("Цвет блоков условного загона. Его границы берутся из Animal System.")]
        [SerializeField] private Color penFenceColor = new Color(0.58f, 0.35f, 0.16f);
        [Tooltip("Высота ограждения в клеточных размерах при следующей выгрузке сцены.")]
        [SerializeField, Min(0.05f)] private float penFenceHeightInCells = 0.55f;
        [Tooltip("Ширина бруса ограждения в клеточных размерах.")]
        [SerializeField, Range(0.03f, 0.3f)] private float penFenceWidthInCells = 0.09f;

        [Header("Baked Camera")]
        [SerializeField, Range(10f, 120f)] private float cameraFieldOfView = 46f;
        [SerializeField, Min(0.01f)] private float cameraNearClip = 0.1f;
        [SerializeField, Min(1f)] private float cameraFarClip = 250f;
        [SerializeField] private Color cameraBackgroundColor = new Color(0.58f, 0.77f, 0.92f);

        [Header("Baked Lighting")]
        [SerializeField] private Vector3 sunEulerAngles = new Vector3(48f, -32f, 0f);
        [SerializeField, Min(0f)] private float sunIntensity = 1.25f;
        [SerializeField] private Color sunColor = new Color(1f, 0.93f, 0.78f);

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
            grid.Configure(bakedCellSize, bakedChunkSizeX, bakedChunkSizeZ, true);
            CreateStartChunk(transform);
            var extraChunk = CreateAdditionalChunk(transform);
            grid.RegisterSceneChunks(GetComponentsInChildren<TilemapChunk>(true));

            var catalog = CreateSystem<FarmCatalog>("Farm Catalog");
            catalog.Configure();
            var inventory = CreateSystem<InventorySystem>("Inventory System");
            inventory.ConfigurePrototypeInventory();
            var wallet = CreateSystem<WalletSystem>("Wallet System");
            wallet.Configure(startingCoins);
            var selling = CreateSystem<SellingSystem>("Selling System");
            selling.Configure(inventory, wallet, catalog);
            var slots = CreateSystem<QuickSlotSystem>("Quick Slots");
            slots.Configure(input, catalog);
            var shop = CreateSystem<SeedShopSystem>("Seed Shop");
            var sector = CreateSystem<SectorSystem>("Sector System");
            sector.Configure(input, grid, wallet, extraChunk);
            var upgrades = CreateSystem<ToolUpgradeSystem>("Tool Upgrades");
            upgrades.Configure(input, wallet);

            var soil = CreateSystem<SoilSystem>("Soil System");
            soil.Configure(grid);
            var crops = CreateSystem<CropSystem>("Crop System");
            crops.Configure(grid, catalog);
            var orchard = CreateSystem<OrchardSystem>("Orchard System");
            orchard.Configure(grid, inventory, catalog);
            var animals = CreateSystem<AnimalSystem>("Animal System");
            animals.Configure(grid, inventory, catalog, wallet);
            CreatePenMockup(grid, animals);
            var onboarding = CreateSystem<OnboardingSystem>("Onboarding System");

            var player = CreatePlayer(grid.CellToWorld(playerStartCell) + Vector3.up * playerStartHeight, input, grid);
            player.transform.SetParent(transform, true);
            var cameraController = CreateCamera(input, player.transform);
            cameraController.transform.SetParent(transform, true);
            player.SetCamera(cameraController.transform);

            var buildings = CreateSystem<BuildSystem>("Building System");
            buildings.Configure(input, grid, crops, wallet, cameraController.GetComponent<Camera>(), true);

            var selector = CreateSystem<CellSelector>("Cell Selector");
            selector.Configure(grid, player.transform, input, cameraController.GetComponent<Camera>());
            var saleCrate = CreateSaleCrate(saleCratePosition);
            saleCrate.SetParent(transform, true);
            shop.Configure(input, catalog, slots, inventory, wallet, player.transform, saleCrate, grid);

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
                selling,
                catalog,
                slots,
                upgrades,
                orchard,
                animals);

            var saveSystem = CreateSystem<SaveSystem>("Save System");
            saveSystem.Configure(input, player, grid, crops, inventory, wallet, buildings, catalog,
                slots, sector, upgrades, orchard, animals, onboarding);
            CreateSystem<ActionFeedbackSystem>("Action Feedback");

            CreateHud(inventory, wallet, interaction, buildings, catalog, slots, sector, upgrades,
                shop, onboarding);
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

        private PlayerController CreatePlayer(Vector3 position, InputReader input, GridSystem grid)
        {
            // Капсула служит временной моделью; CharacterController обеспечивает движение и столкновения.
            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player";
            playerObject.transform.SetParent(transform, false);
            playerObject.transform.position = position;
            RuntimeMaterials.RemoveCollider(playerObject);
            RuntimeMaterials.Paint(playerObject.GetComponent<Renderer>(), playerColor);

            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.3f;

            var player = playerObject.AddComponent<PlayerController>();
            player.Configure(input, grid);
            return player;
        }

        private IsometricCameraController CreateCamera(InputReader input, Transform player)
        {
            // Создаёт основную камеру и настраивает слежение за Transform игрока.
            var cameraObject = new GameObject("Isometric Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = cameraFieldOfView;
            camera.nearClipPlane = cameraNearClip;
            camera.farClipPlane = Mathf.Max(cameraNearClip + 0.01f, cameraFarClip);
            camera.backgroundColor = cameraBackgroundColor;
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
            crate.transform.localScale = saleCrateScale;
            RuntimeMaterials.Paint(crate.GetComponent<Renderer>(), saleCrateColor);
            return crate.transform;
        }

        /// <summary>
        /// Выгружает ограду загона как редактируемые кубы без коллайдеров. Она показывает
        /// область работы куриц, но не занимает клетки и не мешает строительству или лучу выбора.
        /// </summary>
        private void CreatePenMockup(GridSystem grid, AnimalSystem animals)
        {
            var minimum = animals.PenMinCell;
            var maximum = animals.PenMaxCell;
            var pen = new GameObject("Chicken Pen Mockup");
            pen.transform.SetParent(transform, false);
            var cell = grid.CellSize;
            var height = Mathf.Max(0.05f, penFenceHeightInCells) * cell;
            var width = penFenceWidthInCells * cell;
            var south = (minimum.y) * cell;
            var north = (maximum.y + 1) * cell;
            var west = (minimum.x) * cell;
            var east = (maximum.x + 1) * cell;
            var gateX = minimum.x + (maximum.x - minimum.x + 1) / 2;

            for (var x = minimum.x; x <= maximum.x; x++)
            {
                var centerX = (x + 0.5f) * cell;
                // В южной стороне один проход: игрок может подойти к курам.
                if (x != gateX)
                    CreatePenRail(pen.transform, $"South Fence {x}",
                        new Vector3(centerX, height * 0.5f, south),
                        new Vector3(cell, height, width));
                CreatePenRail(pen.transform, $"North Fence {x}",
                    new Vector3(centerX, height * 0.5f, north),
                    new Vector3(cell, height, width));
            }
            for (var z = minimum.y; z <= maximum.y; z++)
            {
                var centerZ = (z + 0.5f) * cell;
                CreatePenRail(pen.transform, $"West Fence {z}",
                    new Vector3(west, height * 0.5f, centerZ),
                    new Vector3(width, height, cell));
                CreatePenRail(pen.transform, $"East Fence {z}",
                    new Vector3(east, height * 0.5f, centerZ),
                    new Vector3(width, height, cell));
            }
        }

        /// <summary>Создаёт одну цветную секцию визуальной ограды без игрового коллайдера.</summary>
        private void CreatePenRail(Transform parent, string railName, Vector3 position, Vector3 scale)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = railName;
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = position;
            rail.transform.localScale = scale;
            RuntimeMaterials.RemoveCollider(rail);
            RuntimeMaterials.Paint(rail.GetComponent<Renderer>(), penFenceColor);
        }

        /// <summary>
        /// Создаёт один стартовый ассет-блок для editor baker и тестовой сцены. В Play Mode
        /// рабочая сцена использует уже сохранённый объект и этот метод не вызывается.
        /// </summary>
        private void CreateStartChunk(Transform parent)
        {
            var chunkObject = new GameObject("TileBlock_Start");
            chunkObject.transform.SetParent(parent, false);
            chunkObject.transform.position = Vector3.zero;
            chunkObject.AddComponent<TilemapChunk>();
            chunkObject.AddComponent<ChunkRenderer>();

            // Дочерний куб имитирует tilemap-ассет: pivot родителя остаётся в нижнем левом углу чанка.
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Terrain Surface";
            surface.transform.SetParent(chunkObject.transform, false);
            surface.transform.localPosition = new Vector3(
                bakedChunkSizeX * bakedCellSize * 0.5f,
                -terrainThickness * 0.5f,
                bakedChunkSizeZ * bakedCellSize * 0.5f);
            surface.transform.localScale = new Vector3(
                bakedChunkSizeX * bakedCellSize,
                terrainThickness,
                bakedChunkSizeZ * bakedCellSize);
            RuntimeMaterials.Paint(surface.GetComponent<Renderer>(), terrainColor);
        }

        /// <summary>Готовит соседний закрытый блок во время bake, чтобы покупка не создавала сцену в Play Mode.</summary>
        private TilemapChunk CreateAdditionalChunk(Transform parent)
        {
            var chunkObject = new GameObject("TileBlock_Extra");
            chunkObject.transform.SetParent(parent, false);
            chunkObject.transform.position = new Vector3(bakedChunkSizeX * bakedCellSize, 0f, 0f);
            var chunk = chunkObject.AddComponent<TilemapChunk>();
            chunkObject.AddComponent<ChunkRenderer>();
            chunk.ConfigureLockedSector(true);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Terrain Surface Extra";
            surface.transform.SetParent(chunkObject.transform, false);
            surface.transform.localPosition = new Vector3(bakedChunkSizeX * bakedCellSize * 0.5f,
                -terrainThickness * 0.5f, bakedChunkSizeZ * bakedCellSize * 0.5f);
            surface.transform.localScale = new Vector3(bakedChunkSizeX * bakedCellSize,
                terrainThickness, bakedChunkSizeZ * bakedCellSize);
            RuntimeMaterials.Paint(surface.GetComponent<Renderer>(), new Color(0.18f, 0.25f, 0.16f));
            return chunk;
        }

        private void CreateLighting(Transform parent)
        {
            // Не создаёт второе солнце, если сцена уже содержит направленный или иной источник света.
            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var sun = new GameObject("Sun");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(sunEulerAngles);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = sunIntensity;
            light.color = sunColor;
            light.shadows = LightShadows.Soft;
        }

        private void CreateHud(InventorySystem inventory, WalletSystem wallet, InteractionSystem interaction,
            BuildSystem buildings, FarmCatalog catalog, QuickSlotSystem slots,
            SectorSystem sector, ToolUpgradeSystem upgrades, SeedShopSystem shop,
            OnboardingSystem onboarding)
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
            SetRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -34f), new Vector2(420f, 410f));

            // Временные значки сохраняются в сцене при bake и позже принимают настоящие Sprite из каталога.
            var seedIcons = new Image[catalog.Crops.Count];
            var harvestIcons = new Image[catalog.Crops.Count];
            for (var index = 0; index < catalog.Crops.Count; index++)
            {
                var crop = catalog.Crops[index];
                seedIcons[index] = CreateIcon(canvasObject.transform, $"Seed Icon {index + 1}",
                    new Vector2(58f + index * 76f, -465f), crop.MatureColor);
                harvestIcons[index] = CreateIcon(canvasObject.transform, $"Harvest Icon {index + 1}",
                    new Vector2(58f + index * 76f, -555f), crop.MatureColor);
            }
            var coinIcon = CreateIcon(canvasObject.transform, "Coin Icon", new Vector2(390f, -465f),
                new Color(1f, 0.78f, 0.16f));
            var toolIcon = CreateIcon(canvasObject.transform, "Tool Icon", new Vector2(390f, -555f),
                new Color(0.55f, 0.66f, 0.72f));
            var seedLabel = CreateText(canvasObject.transform, "Seed Icons Label", 18, TextAnchor.UpperLeft, Color.white);
            seedLabel.text = "Семена  1–6";
            SetRect(seedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(25f, -420f), new Vector2(450f, 28f));
            var harvestLabel = CreateText(canvasObject.transform, "Harvest Icons Label", 18, TextAnchor.UpperLeft, Color.white);
            harvestLabel.text = "Урожай";
            SetRect(harvestLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(25f, -510f), new Vector2(450f, 28f));

            var help = CreateText(canvasObject.transform, "Help", 21, TextAnchor.UpperRight, Color.white);
            help.text = "WASD — движение   Shift — бег   Space — прыжок   Q / Shift+E — камера\n1–6 — семена   T — яблоня   C — курица в загоне   P — магазин\nE / ЛКМ — действие   B — стройка (1–5)   L — сектор   U — инструмент   F5 / F9 — save / load";
            SetRect(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(900f, 110f));

            var prompt = CreateText(canvasObject.transform, "Interaction Prompt", 24, TextAnchor.MiddleCenter, Color.white);
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1700f, 130f));

            var status = CreateText(canvasObject.transform, "Status", 24, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.56f));
            SetRect(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 54f));
            var onboardingText = CreateText(canvasObject.transform, "Onboarding Hint", 24,
                TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.76f));
            SetRect(onboardingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -165f), new Vector2(900f, 54f));

            var hud = canvasObject.AddComponent<HUDController>();
            hud.Configure(inventory, wallet, stats, status, catalog, slots, sector, upgrades, shop,
                seedIcons, harvestIcons, coinIcon, toolIcon,
                GetComponentInChildren<AnimalSystem>(true),
                onboarding, onboardingText);
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
            rect.sizeDelta = new Vector2(470f, 620f);
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

        /// <summary>Создаёт цветной UI-плейсхолдер, который можно заменить Sprite через Inspector.</summary>
        private static Image CreateIcon(Transform parent, string name, Vector2 anchoredPosition, Color color)
        {
            var iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            var icon = iconObject.GetComponent<Image>();
            icon.color = color;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPosition, new Vector2(56f, 56f));
            return icon;
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
            // Runtime использует значения самого GridSystem, которые дизайнер меняет в его Inspector.
            grid.ConfigureFromInspector();
            grid.RegisterSceneChunks(GetComponentsInChildren<TilemapChunk>(true));

            var catalog = RequireChild<FarmCatalog>();
            catalog.Configure();
            var inventory = RequireChild<InventorySystem>();
            inventory.ConfigurePrototypeInventory();
            var wallet = RequireChild<WalletSystem>();
            wallet.Configure(startingCoins);
            var selling = RequireChild<SellingSystem>();
            selling.Configure(inventory, wallet, catalog);
            var slots = RequireChild<QuickSlotSystem>();
            slots.Configure(input, catalog);
            var shop = RequireChild<SeedShopSystem>();
            var sector = RequireChild<SectorSystem>();
            sector.Configure(input, grid, wallet,
                RequireComponent<TilemapChunk>(FindNamedTransform("TileBlock_Extra").gameObject));
            var upgrades = RequireChild<ToolUpgradeSystem>();
            upgrades.Configure(input, wallet);

            var soil = RequireChild<SoilSystem>();
            soil.Configure(grid);
            var crops = RequireChild<CropSystem>();
            crops.Configure(grid, catalog);
            var orchard = RequireChild<OrchardSystem>();
            orchard.Configure(grid, inventory, catalog);
            var animals = RequireChild<AnimalSystem>();
            animals.Configure(grid, inventory, catalog, wallet);
            var onboarding = RequireChild<OnboardingSystem>();
            var player = RequireChild<PlayerController>();
            player.Configure(input, grid);
            RuntimeMaterials.Paint(player.GetComponent<Renderer>(), playerColor);
            var cameraController = RequireChild<IsometricCameraController>();
            cameraController.Configure(input, player.transform);
            player.SetCamera(cameraController.transform);

            var buildings = RequireChild<BuildSystem>();
            buildings.Configure(input, grid, crops, wallet, cameraController.GetComponent<Camera>());
            var selector = RequireChild<CellSelector>();
            selector.Configure(grid, player.transform, input, cameraController.GetComponent<Camera>());
            var saleCrate = FindNamedTransform("Sale Crate");
            RuntimeMaterials.Paint(saleCrate.GetComponent<Renderer>(), saleCrateColor);
            shop.Configure(input, catalog, slots, inventory, wallet, player.transform, saleCrate, grid);

            var interaction = RequireChild<InteractionSystem>();
            interaction.Configure(input, player.transform, saleCrate, selector, grid, soil, crops,
                inventory, selling, catalog, slots, upgrades, orchard, animals);
            var saveSystem = RequireChild<SaveSystem>();
            saveSystem.Configure(input, player, grid, crops, inventory, wallet, buildings, catalog,
                slots, sector, upgrades, orchard, animals, onboarding);
            RequireChild<ActionFeedbackSystem>();

            var hud = RequireChild<HUDController>();
            var seedIcons = new Image[catalog.Crops.Count];
            var harvestIcons = new Image[catalog.Crops.Count];
            for (var index = 0; index < catalog.Crops.Count; index++)
            {
                seedIcons[index] = FindNamedComponent<Image>($"Seed Icon {index + 1}");
                harvestIcons[index] = FindNamedComponent<Image>($"Harvest Icon {index + 1}");
            }
            hud.Configure(inventory, wallet, FindNamedComponent<Text>("Stats"), FindNamedComponent<Text>("Status"),
                catalog, slots, sector, upgrades, shop, seedIcons, harvestIcons,
                FindNamedComponent<Image>("Coin Icon"), FindNamedComponent<Image>("Tool Icon"), animals,
                onboarding, FindNamedComponent<Text>("Onboarding Hint"));
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
