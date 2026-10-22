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
        private const string ReturningPlayerKey = "MyLittleFarm.Onboarding.Seen";
        [SerializeField] private bool enabledInNewGame = true;
        [SerializeField] private OnboardingMarker marker;
        [SerializeField] private OnboardingOverlay overlay;
        private GridSystem _grid;
        private BuildSystem _buildings;
        private CropSystem _crops;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private Transform _player, _market;
        private int _step;
        private bool _ready, _suppliesGranted, _gardenPlanted;
        private bool _saleCompleted;
        private bool _restored;
        private Vector2Int _gardenCell;
        private Vector3 _target;
        private bool _hasTarget;
        private Vector2Int _buildingCell;
        private float _nextTargetAt;
        public int Step => _step;
        public bool IsActive => enabledInNewGame && _step < StepCount;
        public bool CanSkip { get; private set; }
        public bool UsesOverlay => overlay != null;
        public bool SuppliesGranted => _suppliesGranted;
        public bool GardenPlanted => _gardenPlanted;
        public bool SaleCompleted => _saleCompleted;
        public Vector2Int GardenCell => _gardenCell;
        public string CurrentHint => IsActive ? HintFor(_step, _gardenPlanted) : string.Empty;

        public static string HintFor(int step, bool planted = false)
        {
            switch (step)
            {
                case 0: return "1 / 5   ДОМ\nB — стройка, 1 — дом. Поставьте дом на свободной земле.\nE / ЛКМ — поставить, Esc — выйти из стройки.";
                case 1: return "2 / 5   СУНДУК\nВ стройке выберите 2 — склад-сундук и разместите его.\nB — открыть / закрыть стройку. Маркер предлагает место.";
                case 2: return planted
                    ? "3 / 5   ГРЯДКА\nДождитесь зрелого растения под маркером.\nПодойдите и нажмите E / ЛКМ, чтобы собрать урожай."
                    : "3 / 5   ГРЯДКА\nВыйдите из стройки (Esc). Выберите морковь (1).\nУ маркера: E / ЛКМ обработать землю, ещё раз — посадить.";
                case 3: return "4 / 5   МАГАЗИН\nПодойдите к торговому ящику под маркером.\nВыберите морковь (1), нажмите P — купить 4 семени.";
                case 4: return "5 / 5   ПРОДАЖА\nНаведите курсор на торговый ящик под маркером.\nПодойдите и нажмите E / ЛКМ — продать урожай.";
                default: return string.Empty;
            }
        }

        public void Configure(GridSystem grid, BuildSystem buildings, CropSystem crops,
            InventorySystem inventory, WalletSystem wallet, Transform player, Transform market)
        {
            _grid = grid; _buildings = buildings; _crops = crops;
            _inventory = inventory; _wallet = wallet; _player = player; _market = market;
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
            CanSkip = _restored || PlayerPrefs.GetInt(ReturningPlayerKey, 0) != 0;
            PlayerPrefs.SetInt(ReturningPlayerKey, 1);
            PlayerPrefs.Save();
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
            var seeds = _inventory.GetAmount(InventorySystem.CarrotSeedId);
            if (seeds < 4) _inventory.Add(InventorySystem.CarrotSeedId, 4 - seeds);
            _suppliesGranted = true;
        }
        public int Capture() => _step;
        public void Restore(int step, bool suppliesGranted = false, bool gardenPlanted = false,
            Vector2Int gardenCell = default, bool saleCompleted = false)
        {
            _step = Mathf.Clamp(step, 0, StepCount);
            _restored = true;
            if (_ready) CanSkip = true;
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
            if (_step == 3 && action == "sale") { _saleCompleted = true; return; }
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
            if (!_ready || marker == null || _grid == null) return;
            if (!IsActive) { marker.Hide(); return; }
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
            var definition = BuildingDefinition.Catalog[_step];
            var center = _grid.WorldToGrid(_player.position);
            for (var radius = 3; radius <= 20; radius++)
                for (var z = -radius; z <= radius; z++)
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                        var cell = center + new Vector2Int(x, z);
                        if (!_buildings.CanPlace(id, cell, 0, null, out _)) continue;
                        _buildingCell = cell;
                        _target = BuildingView.Center(cell.x, cell.y, 0, definition, _grid) + Vector3.up * 1.8f;
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
