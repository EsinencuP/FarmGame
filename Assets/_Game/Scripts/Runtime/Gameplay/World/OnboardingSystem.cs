using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.UI;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Five optional goals; guidance never locks movement or farm actions.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed class OnboardingSystem : MonoBehaviour
    {
        public const int StepCount = 5;
        [SerializeField] private bool enabledInNewGame = true;
        [SerializeField] private OnboardingMarker marker;
        [SerializeField] private OnboardingOverlay overlay;
        private GridSystem _grid;
        private InputReader_AI _input;
        private QuickSlotSystem _slots;
        private BuildSystem _buildings;
        private CropSystem_AI _crops;
        private InventorySystem_AI _inventory;
        private WalletSystem _wallet;
        private Transform _player, _market;
        private int _step;
        private bool _ready, _suppliesGranted, _gardenPlanted;
        private bool _saleCompleted;
        private Vector2Int _gardenCell;
        private Vector3 _target;
        private bool _hasTarget;
        private Vector2Int _buildingCell;
        private float _nextTargetAt;
        public int Step => _step;
        public bool IsActive => enabledInNewGame && _step < StepCount;
        public bool CanSkip => _ready;
        public bool UsesOverlay => overlay != null;
        public bool SuppliesGranted => _suppliesGranted;
        public bool GardenPlanted => _gardenPlanted;
        public bool SaleCompleted => _saleCompleted;
        public Vector2Int GardenCell => _gardenCell;
        public string CurrentHint => IsActive
            ? HintFor(_step, _gardenPlanted, gameObject.scene.path == FarmSceneFlow.GameScene ? 2 : 0,
                _slots?.SlotFor(InventorySystem_AI.CarrotSeedId) ?? 0)
            : string.Empty;

        public static string HintFor(int step, bool planted = false, int firstStep = 0, int carrotSlot = 0)
        {
            var toolKey = InputReader_AI.KeyLabel(InputReader_AI.Command.ToggleTool);
            var hint = step switch
            {
                0 => FarmText.Get("onboarding.house", InputReader_AI.KeyLabel(InputReader_AI.Command.Build), InputReader_AI.ActionHint),
                1 => FarmText.Get("onboarding.storage", InputReader_AI.KeyLabel(InputReader_AI.Command.Build)),
                2 => planted ? FarmText.Get("onboarding.harvest", InputReader_AI.ActionHint)
                    : FarmText.Get("onboarding.plant",
                        carrotSlot >= 0 ? "Выберите семена моркови · " + InputReader_AI.KeyLabel(InputReader_AI.Command.Seed1 + carrotSlot)
                            : "Назначьте семена на быстрый слот через инвентарь · " + InputReader_AI.KeyLabel(InputReader_AI.Command.Inventory),
                        InputReader_AI.ActionHint, toolKey == "—" ? "Выберите «Мотыга» на панели" : "Достаньте мотыгу · " + toolKey),
                3 => FarmText.Get("onboarding.buy", InputReader_AI.MarketHint),
                4 => FarmText.Get("onboarding.sell", InputReader_AI.MarketHint),
                _ => string.Empty
            };
            return hint.Length == 0 ? hint : $"{step - Mathf.Min(step, firstStep) + 1} / {StepCount - firstStep} · {hint}";
        }

        public void Configure(GridSystem grid, BuildSystem buildings, CropSystem_AI crops,
            InventorySystem_AI inventory, WalletSystem wallet, Transform player, Transform market, QuickSlotSystem slots = null)
        {
            _grid = grid; _buildings = buildings; _crops = crops;
            _inventory = inventory; _wallet = wallet; _player = player; _market = market;
            _slots = slots;
            _input = GetComponentInParent<GameBootstrap>()?.GetComponent<InputReader_AI>();
            if (overlay != null) overlay.Configure(this);
        }
        private void OnEnable()
        {
            GameEvents.ProgressAction += HandleProgress;
            GameEvents.ActionFeedback += HandleFeedback;
        }
        private void Start()
        {
            if (!Application.isPlaying || _grid == null) return;
            _ready = true;
            GrantStarterSupplies();
            ReconcileBuildings();
            Publish();
        }
        // One grant per saved farm, including unfinished legacy tutorials. Never repeats on load.
        private void GrantStarterSupplies()
        {
            if (!IsActive || _suppliesGranted || _wallet == null || _inventory == null) return;
            _wallet.AddCoins(Mathf.Min(25, int.MaxValue - _wallet.Coins));
            var seeds = _inventory.GetAmount(InventorySystem_AI.CarrotSeedId);
            if (seeds < 4) _inventory.Add(InventorySystem_AI.CarrotSeedId, 4 - seeds);
            _suppliesGranted = true;
        }
        public int Capture() => _step;
        public void Restore(int step, bool suppliesGranted = false, bool gardenPlanted = false,
            Vector2Int gardenCell = default, bool saleCompleted = false)
        {
            _step = Mathf.Clamp(step, 0, StepCount);
            _suppliesGranted = suppliesGranted;
            _gardenPlanted = gardenPlanted;
            _gardenCell = gardenCell;
            _saleCompleted = saleCompleted;
            _hasTarget = false;
            if (_ready) GrantStarterSupplies();
            ReconcileBuildings();
            Publish();
        }
        public void Skip()
        {
            if (!CanSkip || !IsActive) return;
            _step = StepCount;
            Publish();
        }
        private void ReconcileBuildings()
        {
            if (_buildings == null || !enabledInNewGame) return;
            if (gameObject.scene.path == FarmSceneFlow.GameScene)
            {
                _step = Mathf.Max(_step, 2);
                return;
            }
            if (_step == 0 && _buildings.FindFirstView("house") != null) _step = 1;
            if (_step == 1 && _buildings.FindFirstView("storage") != null) _step = 2;
        }
        private void HandleFeedback(string action, Vector3 position)
        {
            if (!IsActive || _grid == null || _step != 2) return;
            var cell = _grid.WorldToGrid(position);
            if (action == "plant")
            {
                _gardenCell = cell;
                _gardenPlanted = true;
                Publish();
            }
            else if (action == "harvest" && _grid.GetCellType(cell) != CellType.Tree)
            {
                _step = 3;
                Publish();
            }
        }
        private void HandleProgress(string action)
        {
            if (!IsActive) return;
            // Selling early is valid free play. Remember it so the shop goal cannot strand the player.
            if (_step is 2 or 3 && action == "sale") { _saleCompleted = true; return; }
            var expected = _step == 0 ? "house" : _step == 1 ? "chest"
                : _step == 2 ? "harvest" : _step == 3 ? "purchase" : "sale";
            // Crop feedback advances the bed goal; orchard's generic harvest must not advance it.
            if (action != expected || _step == 2) return;
            _step++;
            if (_step == 4 && _saleCompleted) _step = StepCount;
            ReconcileBuildings();
            Publish();
        }
        private void Publish()
        {
            _hasTarget = false;
            _nextTargetAt = 0f;
            if (!IsActive && marker != null) marker.Hide();
            GameEvents.RaiseOnboardingChanged(_step);
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying || !_ready || marker == null || _grid == null) return;
            if (!IsActive || Time.timeScale <= 0 || _input?.ModalInputActive == true) { marker.Hide(); return; }
            if (Time.unscaledTime >= _nextTargetAt)
            {
                _nextTargetAt = Time.unscaledTime + .5f;
                ResolveTarget();
            }
            if (_hasTarget) marker.Show(_target); else marker.Hide();
        }
        private void ResolveTarget()
        {
            if (_step >= 3)
            {
                if (_market == null) { _hasTarget = false; return; }
                _target = _market.position + Vector3.up * 1.8f;
                _hasTarget = true;
                return;
            }
            if (_step == 2)
            {
                if (_gardenPlanted && _crops.TryGet(_gardenCell, out _))
                {
                    _target = _grid.CellToWorld(_gardenCell) + Vector3.up * 1.8f;
                    _hasTarget = true; return;
                }
                foreach (var cell in _grid.GetPlantedCells())
                {
                    _gardenCell = cell;
                    if (!_gardenPlanted) { _gardenPlanted = true; GameEvents.RaiseOnboardingChanged(_step); }
                    _target = _grid.CellToWorld(cell) + Vector3.up * 1.8f;
                    _hasTarget = true; return;
                }
                if (_gardenPlanted) { _gardenPlanted = false; GameEvents.RaiseOnboardingChanged(_step); }
                if (_hasTarget && (_grid.CanTill(_gardenCell) || _grid.CanPlant(_gardenCell))) return;
                var origin = _grid.WorldToGrid(_player.position);
                for (var radius = 1; radius <= 12; radius++)
                    for (var z = -radius; z <= radius; z++)
                        for (var x = -radius; x <= radius; x++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                            var cell = origin + new Vector2Int(x, z);
                            if (!_grid.CanTill(cell) && !_grid.CanPlant(cell)) continue;
                            _gardenCell = cell; _target = _grid.CellToWorld(cell) + Vector3.up * 1.8f;
                            _hasTarget = true; return;
                        }
                _hasTarget = false; return;
            }
            // A suggested location, not a reservation. Placement remains unrestricted.
            var id = _step == 0 ? "house" : "storage";
            if (_hasTarget && _buildings.CanPlace(id, _buildingCell, 0, null, out _)) return;
            _hasTarget = false;
            var center = _grid.WorldToGrid(_player.position);
            for (var radius = 3; radius <= 20; radius++)
                for (var z = -radius; z <= radius; z++)
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                        var cell = center + new Vector2Int(x, z);
                        if (!_buildings.CanPlace(id, cell, 0, null, out _)) continue;
                        _buildingCell = cell;
                        _target = _grid.CellToWorld(cell) + Vector3.up * 1.25f;
                        _hasTarget = true; return;
                    }
        }
        private void OnDisable()
        {
            GameEvents.ProgressAction -= HandleProgress;
            GameEvents.ActionFeedback -= HandleFeedback;
            if (marker != null) marker.Hide();
        }
    }
}
