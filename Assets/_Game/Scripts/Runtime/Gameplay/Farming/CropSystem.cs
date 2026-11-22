using System;
using System.Collections.Generic;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Управляет посевами, их ростом, сбором, визуальными объектами и снимками сохранения.</summary>
    public sealed class CropSystem : MonoBehaviour
    {
        // ID является частью сохранения и не меняется через Inspector.
        public const string PrototypeCropId = "carrot";
        public const float DefaultGrowDurationSeconds = 8f;
        public const int DefaultYield = 2;

        [Header("Legacy Crop Fallback")]
        [Tooltip("Время роста моркови только для тестовой сцены без FarmCatalog; в игре настройка находится в каталоге.")]
        [SerializeField, Min(0.1f)] private float growDurationSeconds = DefaultGrowDurationSeconds;
        [Tooltip("Урожай моркови только для сцены без FarmCatalog.")]
        [SerializeField, Min(1)] private int yieldAmount = DefaultYield;
        [Tooltip("Количество визуальных стадий роста культуры.")]
        [SerializeField, Min(2)] private int stageCount = 3;
        [Tooltip("Интервал обновления внешнего вида растущих культур.")]
        [SerializeField, Min(0.02f)] private float viewRefreshInterval = 0.25f;

        // Словарь содержит только представления; игровое состояние каждого посева живёт в FarmingCell чанка.
        private readonly Dictionary<Vector2Int, CropView> _views = new Dictionary<Vector2Int, CropView>();
        private readonly Dictionary<Material, Material> _swayMaterials = new();
        private IWorldGridWriter _grid;
        // Каталог определяет параметры каждого вида; null сохраняет совместимость старых тестовых сцен.
        private FarmCatalog _catalog;
        private WeatherController _weather;
        private DayNightCycle _dayNight;
        // Each planting/load starts its own baseline: earlier rain never benefits a new crop.
        private readonly Dictionary<Vector2Int,double> _rainBaselines=new Dictionary<Vector2Int,double>();
        private readonly Dictionary<Vector2Int, RainSample> _qualityRain = new Dictionary<Vector2Int, RainSample>();
        private readonly struct RainSample
        {
            internal readonly long At;
            internal readonly float GrowthBonus, RainySeconds;
            internal RainSample(long at, float growthBonus, float rainySeconds)
            { At = at; GrowthBonus = growthBonus; RainySeconds = rainySeconds; }
        }
        // Время следующего визуального обновления ограничивает работу четырьмя проверками в секунду.
        private float _nextRefresh;

        /// <summary>Текущая урожайность одной клетки, установленная в Inspector.</summary>
        public int YieldAmount => yieldAmount;
        /// <summary>Число визуальных стадий, используемое новыми культурами.</summary>
        public int StageCount => stageCount;
        public bool IsConfigured => _grid != null;

        /// <summary>Подключает сетку, по которой проверяются клетки и вычисляются мировые позиции.</summary>
        public void Configure(IWorldGridWriter grid, FarmCatalog catalog = null)
        {
            _grid = grid;
            _catalog = catalog;
            var root=GetComponentInParent<GameBootstrap>();
            _weather=root==null?null:root.GetComponentInChildren<WeatherController>(true);
            _dayNight=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
        }

        private void Update()
        {
            // Рост основан на UTC, а unscaledTime только регулирует частоту обновления представлений.
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + viewRefreshInterval;
            RefreshViews(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private void OnDestroy()
        {
            foreach (var material in _swayMaterials.Values) Destroy(material);
        }

        public bool Contains(Vector2Int position)
        {
            // Быстрая проверка используется строительством для запрета размещения поверх растения.
            return _grid != null && _grid.GetFarmingCell(position).soilType == CellType.Planted;
        }

        public bool TryGet(Vector2Int position, out CropRuntimeState crop)
        {
            // Возвращает состояние для подсказки прогресса и попытки сбора.
            crop = null;
            if (_grid == null) return false;
            var farming = _grid.GetFarmingCell(position);
            if (farming.soilType != CellType.Planted || string.IsNullOrEmpty(farming.cropId)) return false;
            crop = ToRuntimeState(position, farming);
            crop.weatherGrowthBonusSeconds=GetWeatherBonus(position,farming);
            crop.rainySeconds = SampleRain(position, farming, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                crop.weatherGrowthBonusSeconds).RainySeconds;
            return true;
        }

        private static int Moisture(CropRuntimeState crop) => HarvestQuality.Moisture(
            crop.manuallyWatered ? HarvestQuality.ManualMoisture : 0,
            crop.rainySeconds, crop.growDurationSeconds);

        private static HarvestSize ResolveSize(CropRuntimeState crop) => crop.sizeReady ? crop.harvestSize :
            HarvestQuality.Select(crop.cropId, crop.x, crop.z, crop.plantedAtUnixMs, Moisture(crop));

        public string QualityHint(Vector2Int position, long nowUnixMs)
        {
            if (!TryGet(position, out var crop)) return "";
            if (crop.IsMature(nowUnixMs)) return "Размер " + ResolveSize(crop);
            return HarvestQuality.GrowthHint(Moisture(crop), crop.manuallyWatered);
        }

        public bool TryWater(Vector2Int position, long nowUnixMs)
        {
            if (!TryGet(position, out var crop) || crop.IsMature(nowUnixMs) || crop.manuallyWatered) return false;
            var farming = _grid.GetFarmingCell(position);
            farming.manuallyWatered = true;
            _grid.SetFarmingCell(position, farming, refreshSurface:false);
            return true;
        }

        private RainSample SampleRain(Vector2Int position, FarmingCell farming, long now, float newGrowthBonus)
        {
            if (farming.sizeReady || !_qualityRain.TryGetValue(position, out var sample))
                return new RainSample(now, newGrowthBonus, farming.rainySeconds);
            var elapsed = (now - sample.At) / 1000d;
            if (elapsed <= 0) return sample;
            var ageAtSample = Math.Max(0, (sample.At - farming.plantedAtUnixMs) / 1000d) + sample.GrowthBonus;
            var remaining = Math.Max(0, farming.growDurationSeconds - ageAtSample);
            var rain = Math.Max(0, newGrowthBonus - sample.GrowthBonus) * 5;
            return new RainSample(now, newGrowthBonus, Math.Min(farming.growDurationSeconds,
                sample.RainySeconds + (float)Math.Min(rain, Math.Min(elapsed, remaining))));
        }

        private float GetWeatherBonus(Vector2Int position,FarmingCell farming)
        {
            if(_weather==null)return farming.weatherGrowthBonusSeconds;
            if(!_rainBaselines.TryGetValue(position,out var baseline))
                _rainBaselines[position]=baseline=_weather.GrowthBonusSeconds;
            return (float)Math.Min(farming.growDurationSeconds,farming.weatherGrowthBonusSeconds
                +Math.Max(0,_weather.GrowthBonusSeconds-baseline));
        }

        public bool Plant(Vector2Int position, long plantedAtUnixMs)
            => Plant(position, PrototypeCropId, plantedAtUnixMs);

        /// <summary>Сажает выбранный вид культуры по его определению из каталога.</summary>
        public bool Plant(Vector2Int position, string cropId, long plantedAtUnixMs)
        {
            // Посадка допустима на свободной обработанной клетке без существующего посева.
            if (_grid == null || Contains(position) || !_grid.CanPlant(position))
            {
                return false;
            }

            CropDefinition definition = null;
            if (_catalog != null && !_catalog.TryGetCrop(cropId, out definition)) return false;
            if (_catalog == null && cropId != PrototypeCropId) return false;

            var crop = new CropRuntimeState
            {
                x = position.x,
                z = position.y,
                cropId = cropId,
                plantedAtUnixMs = plantedAtUnixMs,
                growDurationSeconds = definition == null ? growDurationSeconds : definition.GrowDurationSeconds,
                stageCount = definition == null ? stageCount : definition.StageCount
            };
            _grid.SetFarmingCell(position, ToFarmingCell(crop));
            _rainBaselines[position]=_weather==null?0:_weather.GrowthBonusSeconds;
            _qualityRain[position] = new RainSample(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0, 0);
            AddView(crop);
            GameEvents.Record("plant",cropId);
            return true;
        }

        public bool TryHarvest(Vector2Int position, long nowUnixMs, out int yield)
        {
            return TryHarvest(position, nowUnixMs, out _, out yield);
        }

        /// <summary>Собирает зрелый урожай и возвращает ID предмета и количество.</summary>
        public bool TryHarvest(Vector2Int position, long nowUnixMs, out string itemId, out int yield)
            => TryHarvest(position, nowUnixMs, out itemId, out yield, out _);

        public bool TryHarvest(Vector2Int position, long nowUnixMs, out string itemId, out int yield, out HarvestSize size)
        {
            // Незрелое растение остаётся неизменным; зрелое удаляется из данных и сцены.
            itemId = null;
            yield = 0;
            size = HarvestSize.M;
            if (!TryGet(position, out var crop) || !crop.IsMature(nowUnixMs))
            {
                return false;
            }

            CropDefinition definition = null;
            if (_catalog != null && !_catalog.TryGetCrop(crop.cropId, out definition)) return false;
            itemId = definition == null ? crop.cropId : definition.HarvestItemId;
            yield = HarvestYield(crop.cropId);
            if (HarvestQuality.IsSized(itemId)) size = ResolveSize(crop);
            if(definition!=null && definition.RegrowSeconds>0)
            {
                var recovering=_grid.GetFarmingCell(position);
                recovering.plantedAtUnixMs=nowUnixMs;recovering.growDurationSeconds=definition.RegrowSeconds;
                recovering.weatherGrowthBonusSeconds=0;recovering.isRegrowing=true;
                recovering.manuallyWatered = recovering.sizeReady = false;
                recovering.rainySeconds = 0;
                recovering.harvestSize = HarvestSize.M;
                _grid.SetFarmingCell(position,recovering);
                _rainBaselines[position]=_weather==null?0:_weather.GrowthBonusSeconds;
                _qualityRain[position] = new RainSample(nowUnixMs, 0, 0);
                if (_views.TryGetValue(position, out var retained))
                    retained.SetStage(CropRuntimeState.StageFor(0f, crop.stageCount, true), crop.stageCount);
                return true;
            }
            if (_views.TryGetValue(position, out var view))
            {
                _views.Remove(position);
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }

            _grid.SetFarmingCell(position, new FarmingCell { soilType = CellType.Tilled });
            _rainBaselines.Remove(position);
            _qualityRain.Remove(position);
            return true;
        }
        public float HarvestHour => _dayNight==null?-1:_dayNight.Hour;
        public int HarvestYield(string cropId) => _catalog!=null&&_catalog.TryGetCrop(cropId,out var definition)
            ? definition.HarvestYield(HarvestHour) : yieldAmount;

        public List<CropRuntimeState> Capture()
        {
            // Каждое состояние копируется, чтобы снимок не ссылался на рабочий словарь.
            var result = new List<CropRuntimeState>();
            foreach (var position in _grid.GetPlantedCells())
            {
                if (!TryGet(position, out var crop)) continue;
                if (!crop.sizeReady && crop.IsMature(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                {
                    crop.harvestSize = ResolveSize(crop);
                    crop.sizeReady = true;
                }
                result.Add(crop);
            }

            return result;
        }

        public void Restore(List<CropRuntimeState> crops)
        {
            // Старые данные и views очищаются перед воссозданием проверенного снимка.
            ClearAll();
            if (crops == null)
            {
                return;
            }

            foreach (var crop in crops)
            {
                if (_grid.GetCellType(crop.Position) == CellType.Planted
                    && _grid.GetFarmingCell(crop.Position).occupantId == CropOccupantId(crop.Position))
                {
                    var copy = new CropRuntimeState { x = crop.x, z = crop.z, cropId = crop.cropId,
                        plantedAtUnixMs = crop.plantedAtUnixMs, growDurationSeconds = crop.growDurationSeconds,
                        weatherGrowthBonusSeconds = crop.weatherGrowthBonusSeconds, stageCount = crop.stageCount, isRegrowing=crop.isRegrowing,
                        manuallyWatered = crop.manuallyWatered, rainySeconds = crop.rainySeconds, sizeReady = crop.sizeReady, harvestSize = crop.harvestSize };
                    if (!copy.sizeReady && copy.IsMature(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                    {
                        copy.harvestSize = ResolveSize(copy);
                        copy.sizeReady = true;
                    }
                    var existing = _grid.GetFarmingCell(crop.Position);
                    var restored = ToFarmingCell(copy);
                    restored.moisture = existing.moisture;
                    restored.fertilizer = existing.fertilizer;
                    restored.lastWateredDay = existing.lastWateredDay;
                    restored.flags = existing.flags;
                    _grid.SetFarmingCell(crop.Position, restored);
                    _rainBaselines[crop.Position]=_weather==null?0:_weather.GrowthBonusSeconds;
                    if (!copy.sizeReady)
                        _qualityRain[crop.Position] = new RainSample(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            copy.weatherGrowthBonusSeconds, copy.rainySeconds);
                    AddView(copy);
                }
            }
        }

        public void ClearAll()
        {
            // Уничтожает только визуальные объекты; GridSystem сбрасывается отдельно при загрузке.
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
                }
            }

            _views.Clear();
            _rainBaselines.Clear();
            _qualityRain.Clear();
        }

        /// <summary>Создаёт представление для состояния, уже записанного в чанке.</summary>
        private void AddView(CropRuntimeState crop)
        {
            // Только видимые объекты имеют GameObject; данные растения не дублируются в MonoBehaviour.
            CropDefinition definition = null;
            _catalog?.TryGetCrop(crop.cropId, out definition);
            var stage = crop.GetStage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var view = CropView.Create(_grid.GridToWorld(crop.Position), definition, transform,
                stage, crop.stageCount, _swayMaterials);
            _views.Add(crop.Position, view);
        }

        /// <summary>Строит стабильный ID владельца клетки для связи разреженного грида с CropSystem.</summary>
        public static string CropOccupantId(Vector2Int position) => $"crop:{position.x}:{position.y}";

        private void RefreshViews(long nowUnixMs)
        {
            // Синхронизирует визуальную стадию каждой культуры с рассчитанным прогрессом.
            foreach (var pair in _views)
            {
                var farming=_grid.GetFarmingCell(pair.Key);
                if(farming.soilType!=CellType.Planted)continue;
                var weatherBonus = GetWeatherBonus(pair.Key, farming);
                var ratio = CropRuntimeState.GrowthRatioAt(nowUnixMs, farming.plantedAtUnixMs, farming.growDurationSeconds, weatherBonus);
                if (!farming.sizeReady)
                {
                    var sample = SampleRain(pair.Key, farming, nowUnixMs, weatherBonus);
                    if (ratio < 1f) _qualityRain[pair.Key] = sample;
                    else
                    {
                        farming.rainySeconds = sample.RainySeconds;
                        farming.harvestSize = ResolveSize(ToRuntimeState(pair.Key, farming));
                        farming.sizeReady = true;
                        _grid.SetFarmingCell(pair.Key, farming, refreshSurface:false);
                        _qualityRain.Remove(pair.Key);
                    }
                }
                var stage = CropRuntimeState.StageFor(ratio, farming.stageCount, farming.isRegrowing);
                if (ratio >= 1f && pair.Value.CurrentStage != stage && Time.timeScale > 0)
                    GameEvents.RaiseActionFeedback("crop-ready", pair.Value.transform.position);
                pair.Value.SetStage(stage,farming.stageCount);
            }
        }

        internal int RipenForDemo(long now)
        {
            var count = 0;
            foreach (var position in _views.Keys)
            {
                if (!TryGet(position, out var crop) || crop.IsMature(now)) continue;
                var farming = _grid.GetFarmingCell(position);
                farming.harvestSize = ResolveSize(crop);
                farming.sizeReady = true;
                farming.rainySeconds = crop.rainySeconds;
                farming.plantedAtUnixMs = now - (long)Math.Ceiling(farming.growDurationSeconds * 1000d);
                _grid.SetFarmingCell(position, farming, refreshSurface: false);
                _qualityRain.Remove(position);
                count++;
            }
            RefreshViews(now);
            return count;
        }

        /// <summary>Переводит сохраняемый посев в отдельный слой чанка.</summary>
        private static FarmingCell ToFarmingCell(CropRuntimeState crop) => new FarmingCell
        {
            soilType = CellType.Planted,
            occupantId = CropOccupantId(crop.Position),
            cropId = crop.cropId,
            plantedAtUnixMs = crop.plantedAtUnixMs,
            growDurationSeconds = crop.growDurationSeconds,
            weatherGrowthBonusSeconds = crop.weatherGrowthBonusSeconds,
            stageCount = crop.stageCount, isRegrowing=crop.isRegrowing,
            manuallyWatered = crop.manuallyWatered, rainySeconds = crop.rainySeconds,
            sizeReady = crop.sizeReady, harvestSize = crop.harvestSize
        };

        /// <summary>Возвращает совместимое DTO из источника истины для HUD и сохранения.</summary>
        private static CropRuntimeState ToRuntimeState(Vector2Int position, FarmingCell farming) =>
            new CropRuntimeState
            {
                x = position.x,
                z = position.y,
                cropId = farming.cropId,
                plantedAtUnixMs = farming.plantedAtUnixMs,
                growDurationSeconds = farming.growDurationSeconds,
                weatherGrowthBonusSeconds = farming.weatherGrowthBonusSeconds,
                stageCount = farming.stageCount, isRegrowing=farming.isRegrowing,
                manuallyWatered = farming.manuallyWatered, rainySeconds = farming.rainySeconds,
                sizeReady = farming.sizeReady, harvestSize = farming.harvestSize
            };
    }
}
