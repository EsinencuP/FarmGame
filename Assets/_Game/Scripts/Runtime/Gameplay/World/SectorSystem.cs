using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Управляет покупкой стартового сектора и участков GameScene.</summary>
    [DefaultExecutionOrder(-85)]
    public sealed class SectorSystem : MonoBehaviour
    {
        public readonly struct Parcel
        {
            public readonly string Id, Name, Description;
            public readonly Vector2Int Chunk;
            public readonly int Cost;

            public Parcel(string id, string name, string description, Vector2Int chunk, int cost)
            {
                Id = id;
                Name = name;
                Description = description;
                Chunk = chunk;
                Cost = cost;
            }
        }

        public static IReadOnlyList<Parcel> GameLocations { get; } = Array.AsReadOnly(new[]
        {
            new Parcel("parcel:fishing", "Озёрная долина", "Открывает западную часть карты и рыбалку у пруда.",
                new Vector2Int(-2, 0), 55),
            new Parcel("parcel:forest", "Дальний лес", "Открывает северные и восточные луга. В лесу спрятаны редкие семена.",
                new Vector2Int(0, 3), 90)
        });

        [SerializeField, Min(0)] private int purchasePrice = 30;
        [SerializeField] private Color unlockedTerrainColor = new Color(0.29f, 0.55f, 0.21f);
        [SerializeField] private Color lockedTerrainColor = new Color(0.18f, 0.25f, 0.16f);

        // Сектор хранится в сцене заранее. Право доступа не зависит от росписи грунта.
        private InputReader_AI _input;
        private GridSystem _grid;
        private WalletSystem _wallet;
        private TilemapChunk _sectorChunk;
        internal bool UsesLegacySector => _parcels.Count == 0;
        internal bool AllLocationsOpen => UsesLegacySector ? IsUnlocked : OwnsParcel(_parcels.Count - 1);
        private FishPond _fishing;
        private RareSeedCache[] _caches;
        private IReadOnlyList<Parcel> _parcels = Array.Empty<Parcel>();
        public bool IsUnlocked { get; private set; }
        internal int OwnedParcels { get; private set; }
        internal IReadOnlyList<Parcel> Locations => _parcels;
        public int PurchasePrice => TransactionMath.IncreasedPurchasePrice(purchasePrice);

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
            _parcels = gameObject.scene.path == FarmSceneFlow.GameScene ? GameLocations : Array.Empty<Parcel>();
            RestoreParcels(0);
            UpdateAccess();
            RefreshAppearance();
        }

        public bool CanPurchase
        {
            get
            {
                if (!UsesLegacySector) return !AllLocationsOpen;
                if (IsUnlocked || _grid == null || _sectorChunk?.IsLockedSector != true) return false;
                var origin = _grid.WorldToGrid(_sectorChunk.transform.position);
                return _grid.IsChunkLoaded(_grid.GridToChunk(origin));
            }
        }

        /// <summary>Списывает стоимость и открывает клетки только при загруженном закрытом секторе.</summary>
        public bool TryPurchase()
        {
            if (!CanPurchase) return false;
            if (!UsesLegacySector)
            {
                var next = _parcels[OwnsParcel(0) ? 1 : 0];
                if (!_wallet.TrySpend(next.Cost)) return false;
                CompleteParcelPurchase(next.Id);
                return true;
            }
            if (!_wallet.TrySpend(PurchasePrice))
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

        internal bool OwnsParcel(int index) => (OwnedParcels & (1 << index)) != 0;

        internal bool HasValidParcels(int ownership) =>
            UsesLegacySector ? ownership == 0 : ownership is 0 or 1 or 3;

        internal static int LocationIndex(Vector2Int chunk)
        {
            if (Mathf.Abs(chunk.x) <= 1 && Mathf.Abs(chunk.y) <= 1) return -1;
            return chunk.x <= -2 ? 0 : 1;
        }

        internal bool IsUnownedParcel(Vector2Int chunk, int ownership)
        {
            if (UsesLegacySector) return false;
            var index = LocationIndex(chunk);
            return index >= 0 && (ownership & (1 << index)) == 0;
        }

        internal string LockedParcelHint(Vector2Int chunk)
        {
            var index = LocationIndex(chunk);
            if (UsesLegacySector || index < 0 || OwnsParcel(index)) return null;
            var parcel = _parcels[index];
            if (index > 0 && !OwnsParcel(0))
                return parcel.Name + " закрыт. Сначала откройте Озёрную долину через «Улучшения» на рынке.";
            return parcel.Name + " · откройте вход через «Улучшения» на рынке за " + UI.FarmText.Coins(parcel.Cost) + ".";
        }

        internal string LocationUnavailable(int index) =>
            OwnsParcel(index) ? "Уже открыто" : index > 0 && !OwnsParcel(index - 1) ? "Сначала откройте Озёрную долину" : null;

        internal void BindLocationContent(FishPond fishing, RareSeedCache[] caches)
        {
            _fishing = fishing;
            _caches = caches;
            RefreshLocationContent();
        }

        private void RefreshLocationContent()
        {
            if (UsesLegacySector) return;
            if (_fishing != null) _fishing.gameObject.SetActive(OwnsParcel(0));
            if (_caches == null) return;
            foreach (var cache in _caches)
                if (cache != null) cache.gameObject.SetActive(OwnsParcel(1));
        }

        internal void CompleteParcelPurchase(string id)
        {
            for (var index = 0; index < _parcels.Count; index++)
            {
                var parcel = _parcels[index];
                if (parcel.Id != id || LocationUnavailable(index) != null) continue;
                RestoreParcels(OwnedParcels | (1 << index));
                var center = _grid.ChunkLocalToGrid(parcel.Chunk, new Vector2Int(_grid.ChunkSizeX / 2, _grid.ChunkSizeZ / 2));
                GameEvents.Record("sector");
                GameEvents.RaiseActionFeedback("sector-unlock", _grid.GridToWorld(center));
                return;
            }
        }

        internal void RestoreParcels(int ownership)
        {
            OwnedParcels = ownership;
            _grid.SetLocationAccess(this, ownership);
            RefreshLocationContent();
        }

        /// <summary>После загрузки применяет право собственности к клеткам и внешнему виду.</summary>
        public void Restore(bool unlocked)
        {
            IsUnlocked = unlocked;
            UpdateAccess();
            if (UsesLegacySector && unlocked && _grid != null && _sectorChunk != null)
                _grid.UnlockArea(_sectorChunk.transform.position,
                    _grid.ChunkSizeX * _grid.CellSize, _grid.ChunkSizeZ * _grid.CellSize);
            RefreshAppearance();
            GameEvents.RaiseInventoryChanged();
        }

        private void UpdateAccess()
        {
            if (_grid == null || _sectorChunk == null) return;
            var origin = _grid.WorldToGrid(_sectorChunk.transform.position);
            _grid.SetSectorLock(_grid.GridToChunk(origin), UsesLegacySector && _sectorChunk.IsLockedSector && !IsUnlocked);
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
