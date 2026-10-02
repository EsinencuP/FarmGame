using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Открывает заранее выгруженный дополнительный чанк фермы за монеты.</summary>
    [DefaultExecutionOrder(-85)]
    public sealed class SectorSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int purchasePrice = 30;
        [SerializeField] private Color unlockedTerrainColor = new Color(0.29f, 0.55f, 0.21f);
        [SerializeField] private Color lockedTerrainColor = new Color(0.18f, 0.25f, 0.16f);

        // Сектор хранится в сцене заранее; покупка меняет только типы клеток и его цвет.
        private InputReader _input;
        private GridSystem _grid;
        private WalletSystem _wallet;
        private TilemapChunk _sectorChunk;
        public bool IsUnlocked { get; private set; }
        public int PurchasePrice => purchasePrice;

        /// <summary>Определяет, входит ли клетка в заранее выгруженный дополнительный блок.</summary>
        public bool ContainsCell(Vector2Int position)
        {
            if (_grid == null || _sectorChunk == null) return false;
            var origin = _grid.WorldToGrid(_sectorChunk.transform.position);
            return position.x >= origin.x && position.x < origin.x + _grid.ChunkSizeX
                && position.y >= origin.y && position.y < origin.y + _grid.ChunkSizeZ;
        }

        /// <summary>Подключает выгруженный сектор без создания нового объекта в Play Mode.</summary>
        public void Configure(InputReader input, GridSystem grid, WalletSystem wallet, TilemapChunk sectorChunk)
        {
            _input = input;
            _grid = grid;
            _wallet = wallet;
            _sectorChunk = sectorChunk;
            IsUnlocked = false;
            RefreshAppearance();
        }

        private void Update()
        {
            // L покупает соседний сектор при достаточном балансе вне режима строительства.
            if (_input != null && _input.SectorPressed && !_input.BuildModeActive
                && !_input.SuppressGameplayThisFrame) TryPurchase();
        }

        /// <summary>Списывает стоимость и открывает клетки только при загруженном закрытом секторе.</summary>
        public bool TryPurchase()
        {
            var origin = _grid == null || _sectorChunk == null
                ? default : _grid.WorldToGrid(_sectorChunk.transform.position);
            if (IsUnlocked || _grid == null || _sectorChunk == null
                || !_grid.IsChunkLoaded(_grid.GridToChunk(origin))
                || _grid.GetCellType(origin) != CellType.Locked)
                return false;
            if (!_wallet.TrySpend(purchasePrice))
            {
                GameEvents.RaiseStatusChanged("Недостаточно монет для сектора");
                return false;
            }
            Restore(true);
            GameEvents.RaiseStatusChanged("Новый сектор фермы открыт");
            return true;
        }

        /// <summary>После загрузки применяет право собственности к клеткам и внешнему виду.</summary>
        public void Restore(bool unlocked)
        {
            IsUnlocked = unlocked;
            if (unlocked && _grid != null && _sectorChunk != null)
                _grid.UnlockArea(_sectorChunk.transform.position,
                    _grid.ChunkSizeX * _grid.CellSize, _grid.ChunkSizeZ * _grid.CellSize);
            RefreshAppearance();
            GameEvents.RaiseInventoryChanged();
        }

        private void RefreshAppearance()
        {
            // Цвет временной поверхности показывает, какой сектор уже доступен.
            if (_sectorChunk == null) return;
            var chunkRenderer = _sectorChunk.GetComponent<ChunkRenderer>();
            if (chunkRenderer != null)
            {
                chunkRenderer.Rebuild();
                return;
            }
            var surface = _sectorChunk.GetComponentInChildren<Renderer>();
            if (surface != null) RuntimeMaterials.Paint(surface,
                IsUnlocked ? unlockedTerrainColor : lockedTerrainColor);
        }
    }
}
