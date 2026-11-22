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

        // Сектор хранится в сцене заранее. Право доступа не зависит от росписи грунта.
        private InputReader_AI _input;
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
        public void Configure(InputReader_AI input, GridSystem grid, WalletSystem wallet, TilemapChunk sectorChunk)
        {
            _input = input;
            _grid = grid;
            _wallet = wallet;
            _sectorChunk = sectorChunk;
            IsUnlocked = false;
            UpdateAccess();
            RefreshAppearance();
        }

        public bool CanPurchase
        {
            get
            {
                if (IsUnlocked || _grid == null || _sectorChunk?.IsLockedSector != true) return false;
                var origin = _grid.WorldToGrid(_sectorChunk.transform.position);
                return _grid.IsChunkLoaded(_grid.GridToChunk(origin));
            }
        }

        /// <summary>Списывает стоимость и открывает клетки только при загруженном закрытом секторе.</summary>
        public bool TryPurchase()
        {
            if (!CanPurchase) return false;
            if (!_wallet.TrySpend(purchasePrice))
            {
                GameEvents.RaiseStatusChanged("Недостаточно монет для сектора");
                return false;
            }
            CompletePurchase();
            GameEvents.RaiseStatusChanged("Новый сектор фермы открыт");
            return true;
        }

        internal void CompletePurchase()
        {
            Restore(true);
            GameEvents.Record("sector");
            var center = _sectorChunk.transform.position + new Vector3(_grid.ChunkSizeX, 0, _grid.ChunkSizeZ) * (_grid.CellSize * .5f);
            GameEvents.RaiseActionFeedback("sector-unlock", _grid.CellToWorld(_grid.WorldToGrid(center)));
        }

        /// <summary>После загрузки применяет право собственности к клеткам и внешнему виду.</summary>
        public void Restore(bool unlocked)
        {
            IsUnlocked = unlocked;
            UpdateAccess();
            if (unlocked && _grid != null && _sectorChunk != null)
                _grid.UnlockArea(_sectorChunk.transform.position,
                    _grid.ChunkSizeX * _grid.CellSize, _grid.ChunkSizeZ * _grid.CellSize);
            RefreshAppearance();
            GameEvents.RaiseInventoryChanged();
        }

        private void UpdateAccess()
        {
            if (_grid == null || _sectorChunk == null) return;
            var origin = _grid.WorldToGrid(_sectorChunk.transform.position);
            _grid.SetSectorLock(_grid.GridToChunk(origin), _sectorChunk.IsLockedSector && !IsUnlocked);
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
