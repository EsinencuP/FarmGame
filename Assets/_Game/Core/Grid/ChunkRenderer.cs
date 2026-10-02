using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Показывает один чанк единым мешем; все игровые правила читают GridSystem, а не геометрию.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(TilemapChunk))]
    public sealed class ChunkRenderer : MonoBehaviour
    {
        [Header("Terrain Palette")]
        [SerializeField] private Color grassColor = new Color(0.24f, 0.45f, 0.18f);
        [SerializeField] private Color dirtColor = new Color(0.45f, 0.32f, 0.18f);
        [SerializeField] private Color soilColor = new Color(0.34f, 0.22f, 0.13f);
        [SerializeField] private Color waterColor = new Color(0.16f, 0.42f, 0.79f);
        [SerializeField] private Color rockColor = new Color(0.42f, 0.44f, 0.44f);
        [SerializeField] private Color sandColor = new Color(0.76f, 0.67f, 0.43f);
        [SerializeField] private Color roadColor = new Color(0.52f, 0.45f, 0.35f);
        [SerializeField] private Color lockedColor = new Color(0.18f, 0.25f, 0.16f);
        [SerializeField, Min(0f)] private float surfaceLift = 0.02f;

        // Буферы переиспользуются при изменении клеток, чтобы один чанк не создавал объект на клетку.
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Color32> _colors = new List<Color32>();
        private GridSystem _grid;
        private IChunkProvider _chunks;
        private float _cellSize;
        private TilemapChunk _source;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Renderer _legacySurface;
        private Mesh _mesh;
        private Material _material;
        private ChunkData _lastChunk;
        private int _lastVersion = -1;
        private bool _dirty = true;

        /// <summary>Подписывает представление на изменения чанка и готовит ссылки на компоненты.</summary>
        private void OnEnable()
        {
            // Компоненты и подписки берутся один раз; LateUpdate проверяет только флаг изменения.
            _source = GetComponent<TilemapChunk>();
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _legacySurface = _source.LegacySurfaceRenderer;
            BindGrid();
            _dirty = true;
        }

        /// <summary>Убирает подписку при выключении компонента или смене сцены.</summary>
        private void OnDisable()
        {
            if (_grid != null) _grid.OnChunkChanged -= HandleChunkChanged;
            _grid = null;
        }

        /// <summary>Освобождает временный меш и материал, которыми владеет этот компонент.</summary>
        private void OnDestroy()
        {
            // Созданные представлением ресурсы освобождаются отдельно от сценовых данных.
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }

        /// <summary>Откладывает множество изменений одного кадра до единственной перестройки меша.</summary>
        private void LateUpdate()
        {
            if (_grid == null) BindGrid();
            if (_dirty) Rebuild();
        }

        /// <summary>Перестраивает только этот чанк, если его версия или объект данных изменились.</summary>
        public void Rebuild()
        {
            if (_grid == null || _source == null) return;
            var chunk = _chunks.GetChunk(_source.RegisteredChunkCoord);
            if (chunk == null) return;
            if (!_dirty && _lastChunk == chunk && _lastVersion == chunk.Version) return;
            EnsureResources();
            _vertices.Clear();
            _triangles.Clear();
            _colors.Clear();
            var size = _cellSize;
            for (var z = 0; z < chunk.chunkSizeZ; z++)
            for (var x = 0; x < chunk.chunkSizeX; x++)
            {
                var vertex = _vertices.Count;
                var left = x * size;
                var bottom = z * size;
                _vertices.Add(new Vector3(left, surfaceLift, bottom));
                _vertices.Add(new Vector3(left, surfaceLift, bottom + size));
                _vertices.Add(new Vector3(left + size, surfaceLift, bottom + size));
                _vertices.Add(new Vector3(left + size, surfaceLift, bottom));
                _triangles.Add(vertex);
                _triangles.Add(vertex + 1);
                _triangles.Add(vertex + 2);
                _triangles.Add(vertex);
                _triangles.Add(vertex + 2);
                _triangles.Add(vertex + 3);
                // Постройка не перекрашивает землю под footprint; грядка рисуется поверх terrain.
                var type = chunk.GetBuilding(x, z).type != 0
                    ? chunk.GetTerrain(x, z).type : chunk.GetCell(x, z).type;
                var color = (Color32)ColorFor(type);
                for (var corner = 0; corner < 4; corner++) _colors.Add(color);
            }
            _mesh.Clear();
            _mesh.indexFormat = _vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _filter.sharedMesh = _mesh;
            if (_legacySurface == null)
                _legacySurface = _source.LegacySurfaceRenderer;
            if (_legacySurface != null) _legacySurface.enabled = false;
            _lastChunk = chunk;
            _lastVersion = chunk.Version;
            _dirty = false;
        }

        /// <summary>Находит сетку общего корня и подписывается только на событие изменённого чанка.</summary>
        private void BindGrid()
        {
            // В сцене сетка находится в соседнем дочернем объекте общего корня.
            var root = GetComponentInParent<MyLittleFarm.Core.GameBootstrap>();
            var grid = root == null ? GridSystem.Instance : root.GetComponentInChildren<GridSystem>(true);
            if (grid == null || grid == _grid) return;
            if (_grid != null) _grid.OnChunkChanged -= HandleChunkChanged;
            _grid = grid;
            _chunks = grid;
            _cellSize = grid.CellSize;
            _grid.OnChunkChanged += HandleChunkChanged;
            _dirty = true;
        }

        /// <summary>Помечает только представление своего чанка как требующее обновления.</summary>
        private void HandleChunkChanged(Vector2Int coordinate)
        {
            if (_source != null && coordinate == _source.RegisteredChunkCoord) _dirty = true;
        }

        /// <summary>Создаёт временные графические ресурсы лишь перед первым построением.</summary>
        private void EnsureResources()
        {
            if (_mesh == null) _mesh = new Mesh { name = name + " Terrain Mesh", hideFlags = HideFlags.DontSave };
            if (_material != null) return;
            // Sprite shader учитывает vertex color; при отсутствии используется доступный unlit shader.
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            _material = new Material(shader) { name = "Chunk Vertex Colors", hideFlags = HideFlags.DontSave };
            _renderer.sharedMaterial = _material;
        }

        /// <summary>Выбирает временный цвет поверхности для текущего игрового типа клетки.</summary>
        private Color ColorFor(CellType type)
        {
            switch (type)
            {
                case CellType.Dirt: return dirtColor;
                case CellType.Tilled:
                case CellType.Planted:
                case CellType.Watered: return soilColor;
                case CellType.Water: return waterColor;
                case CellType.Rock: return rockColor;
                case CellType.Sand: return sandColor;
                case CellType.Road: return roadColor;
                case CellType.Locked: return lockedColor;
                case CellType.Tree: return new Color(0.16f, 0.42f, 0.12f);
                default: return grassColor;
            }
        }
    }
}
