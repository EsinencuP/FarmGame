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
        // Values shared with LandscapeSurface.shader. Zero keeps the vertex palette for path pieces.
        private enum SurfaceMode { Palette, Ground, Tilled, Planted, Watered }

        [Header("Terrain Palette")]
        [SerializeField] private Color grassColor = new Color(0.24f, 0.45f, 0.18f, 0.76f);
        [SerializeField] private Color dirtColor = new Color(0.45f, 0.32f, 0.18f);
        [SerializeField] private Color soilColor = new Color(0.34f, 0.22f, 0.13f);
        [SerializeField] private Color waterColor = new Color(0.16f, 0.42f, 0.79f);
        [SerializeField] private Color rockColor = new Color(0.24f, 0.45f, 0.18f, 0.76f);
        [SerializeField] private Color sandColor = new Color(0.76f, 0.67f, 0.43f);
        [SerializeField] private Color roadColor = new Color(0.52f, 0.45f, 0.35f);
        [SerializeField] private Color lockedColor = new Color(0.18f, 0.25f, 0.16f);
        [SerializeField, Min(0f)] private float surfaceLift = 0.02f;
        [SerializeField] private Mesh bakedMesh;
        [SerializeField] private Material surfaceMaterial;

        // Буферы переиспользуются при изменении клеток, чтобы один чанк не создавал объект на клетку.
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector4> _surfaceData = new List<Vector4>();
        private readonly List<Vector4> _surfaceCorners = new List<Vector4>();
        private Vector4[] _cornerData = System.Array.Empty<Vector4>();
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
        private bool _checkBorders;

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
            if (_mesh != null && _mesh != bakedMesh)
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
            if (_dirty || _checkBorders) Rebuild();
        }

        /// <summary>Перестраивает только этот чанк, если его версия или объект данных изменились.</summary>
        public void Rebuild()
        {
            if (_grid == null || _source == null) return;
            var chunk = _chunks.GetChunk(_source.RegisteredChunkCoord);
            if (chunk == null) return;
            var origin = _grid.ChunkLocalToGrid(_source.RegisteredChunkCoord, Vector2Int.zero);
            var refreshBorders = _checkBorders;
            _checkBorders = false;
            if (!_dirty && _lastChunk == chunk && _lastVersion == chunk.Version
                && (!refreshBorders || !BorderDataChanged(origin, chunk))) return;
            EnsureResources();
            _vertices.Clear();
            _triangles.Clear();
            _colors.Clear();
            _surfaceData.Clear();
            _surfaceCorners.Clear();
            var size = _cellSize;
            var stride = chunk.chunkSizeX + 1;
            var cornerCount = stride * (chunk.chunkSizeZ + 1);
            if (_cornerData.Length != cornerCount) _cornerData = new Vector4[cornerCount];
            for (var z = 0; z <= chunk.chunkSizeZ; z++)
            for (var x = 0; x <= chunk.chunkSizeX; x++)
                _cornerData[z * stride + x] = GetCornerData(origin + new Vector2Int(x, z));
            for (var z = 0; z < chunk.chunkSizeZ; z++)
            for (var x = 0; x < chunk.chunkSizeX; x++)
            {
                var vertex = _vertices.Count;
                var left = x * size;
                var bottom = z * size;
                var coordinate = origin + new Vector2Int(x,z);
                _vertices.Add(new Vector3(left, _grid.GetVertexHeight(coordinate) + surfaceLift, bottom));
                _vertices.Add(new Vector3(left, _grid.GetVertexHeight(coordinate+Vector2Int.up) + surfaceLift, bottom + size));
                _vertices.Add(new Vector3(left + size, _grid.GetVertexHeight(coordinate+Vector2Int.one) + surfaceLift, bottom + size));
                _vertices.Add(new Vector3(left + size, _grid.GetVertexHeight(coordinate+Vector2Int.right) + surfaceLift, bottom));
                _triangles.Add(vertex);
                _triangles.Add(vertex + 1);
                _triangles.Add(vertex + 2);
                _triangles.Add(vertex);
                _triangles.Add(vertex + 2);
                _triangles.Add(vertex + 3);
                // Постройка не перекрашивает землю под footprint; грядка рисуется поверх terrain.
                var type = chunk.GetBuilding(x, z).type != 0
                    ? chunk.GetTerrain(x, z).type : chunk.GetCell(x, z).type;
                var terrain = chunk.GetTerrain(x,z);
                var baseColor = ColorFor(type);
                if (type==CellType.Grass && terrain.biome==2) baseColor = new Color(0.23f,0.39f,0.22f);
                if (type==CellType.Road) baseColor = terrain.pathSurface==PathSurface.Stone
                    ? new Color(0.31f,0.34f,0.31f) : terrain.pathSurface==PathSurface.Wood
                    ? new Color(0.29f,0.20f,0.12f) : roadColor;
                var color = (Color32)baseColor;
                for (var corner = 0; corner < 4; corner++) _colors.Add(color);
                AddSurfaceData(x, z, stride, type, terrain);
                if (type==CellType.Road && terrain.pathSurface!=PathSurface.Dirt)
                    AddPathDetail(left,bottom,terrain.pathSurface,origin);
            }
            var collider = _source.TerrainCollider as MeshCollider;
            if (collider != null) collider.sharedMesh = null;
            _mesh.Clear();
            _mesh.indexFormat = _vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _surfaceData);
            _mesh.SetUVs(3, _surfaceCorners);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _filter.sharedMesh = _mesh;
            if (collider != null) collider.sharedMesh = _mesh;
            if (_legacySurface == null)
                _legacySurface = _source.LegacySurfaceRenderer;
            if (_legacySurface != null) _legacySurface.enabled = false;
            _lastChunk = chunk;
            _lastVersion = chunk.Version;
            _dirty = false;
        }

        /// <summary>Saved terrain geometry is editable in the scene; runtime owns only its temporary copy.</summary>
        public void AssignBakedGeometry(Mesh mesh, Material material)
        {
            if (_mesh != null && _mesh != bakedMesh)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
            bakedMesh=mesh; surfaceMaterial=material; _mesh=null; _dirty=true;
            Rebuild();
        }

        private void AddSurfaceData(int x, int z, int stride, CellType type, TerrainCell terrain)
        {
            var mode = type switch
            {
                CellType.Road when terrain.pathSurface != PathSurface.Dirt => SurfaceMode.Palette,
                CellType.Locked => SurfaceMode.Palette,
                CellType.Tilled => SurfaceMode.Tilled,
                CellType.Planted => SurfaceMode.Planted,
                CellType.Watered => SurfaceMode.Watered,
                _ => SurfaceMode.Ground
            };
            var packed = IsPacked(terrain) ? 1f : 0f;
            _surfaceData.Add(new Vector4(0, 0, (float)mode, packed));
            _surfaceData.Add(new Vector4(0, 1, (float)mode, packed));
            _surfaceData.Add(new Vector4(1, 1, (float)mode, packed));
            _surfaceData.Add(new Vector4(1, 0, (float)mode, packed));
            var index = z * stride + x;
            _surfaceCorners.Add(_cornerData[index]);
            _surfaceCorners.Add(_cornerData[index + stride]);
            _surfaceCorners.Add(_cornerData[index + stride + 1]);
            _surfaceCorners.Add(_cornerData[index + 1]);
        }

        private static bool IsPacked(TerrainCell terrain) => terrain.type == CellType.Dirt
            || terrain.type == CellType.Sand || (terrain.type == CellType.Road && terrain.pathSurface == PathSurface.Dirt);

        private Vector4 GetCornerData(Vector2Int vertex)
        {
            // Adjacent chunks sample the same four terrain cells at a shared world-grid corner.
            var cells = 0;
            var packed = 0;
            var water = 0;
            for (var z = -1; z <= 0; z++)
            for (var x = -1; x <= 0; x++)
            {
                var terrain = _grid.GetTerrainCell(vertex + new Vector2Int(x, z));
                if (terrain.type == CellType.OutOfBounds) continue;
                cells++;
                if (IsPacked(terrain)) packed++;
                if (terrain.type == CellType.Water) water++;
            }
            // Dampness follows the authored pond and its low shelf, not the decorative sand elsewhere.
            var shore = water == 0 ? 0f : Mathf.Clamp01(water * 0.75f)
                * (1f - Mathf.InverseLerp(-0.2f, 0.25f, _grid.GetVertexHeight(vertex)));
            // Shared slopes give the shader a smooth lighting normal without editing mesh geometry.
            var slopeX = (_grid.GetVertexHeight(vertex + Vector2Int.right)
                - _grid.GetVertexHeight(vertex + Vector2Int.left)) / (2 * _cellSize);
            var slopeZ = (_grid.GetVertexHeight(vertex + Vector2Int.up)
                - _grid.GetVertexHeight(vertex + Vector2Int.down)) / (2 * _cellSize);
            return new Vector4(cells == 0 ? 0f : (float)packed / cells, shore, slopeX, slopeZ);
        }

        private bool BorderDataChanged(Vector2Int origin, ChunkData chunk)
        {
            var width = chunk.chunkSizeX;
            var depth = chunk.chunkSizeZ;
            var stride = width + 1;
            for (var z = 0; z <= depth; z++)
            {
                var step = z == 0 || z == depth ? 1 : width;
                for (var x = 0; x <= width; x += step)
                    if (_cornerData[z * stride + x] != GetCornerData(origin + new Vector2Int(x, z))) return true;
            }
            return false;
        }

        private void AddPathDetail(float left, float bottom, PathSurface surface, Vector2Int origin)
        {
            var count=surface==PathSurface.Wood?5:2;
            for(var i=0;i<count;i++)
            {
                var gap=surface==PathSurface.Wood?0.012f:0.035f;
                var x0=left+gap*_cellSize; var x1=left+(1f-gap)*_cellSize;
                var z0=bottom+(i/(float)count+gap)*_cellSize;
                var z1=bottom+((i+1f)/count-gap)*_cellSize;
                var c=surface==PathSurface.Wood?new Color(0.57f+i*0.015f,0.39f+i*0.01f,0.22f)
                    :new Color(0.59f+i*0.035f,0.60f+i*0.035f,0.53f+i*0.035f);
                var index=_vertices.Count;
                AddPathVertex(x0,z0,origin); AddPathVertex(x0,z1,origin);
                AddPathVertex(x1,z1,origin); AddPathVertex(x1,z0,origin);
                _triangles.Add(index); _triangles.Add(index+1); _triangles.Add(index+2);
                _triangles.Add(index); _triangles.Add(index+2); _triangles.Add(index+3);
                for(var j=0;j<4;j++)
                {
                    _colors.Add(c);
                    _surfaceData.Add(Vector4.zero);
                    _surfaceCorners.Add(Vector4.zero);
                }
            }
        }

        private void AddPathVertex(float x,float z,Vector2Int origin)
        {
            var height=_grid.GetSurfaceHeight(origin.x*_cellSize+x,origin.y*_cellSize+z);
            _vertices.Add(new Vector3(x,height+surfaceLift+0.008f,z));
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
            if (_source == null) return;
            var offset = coordinate - _source.RegisteredChunkCoord;
            if (offset == Vector2Int.zero) _dirty = true;
            else if (Mathf.Abs(offset.x) <= 1 && Mathf.Abs(offset.y) <= 1) _checkBorders = true;
        }

        /// <summary>Создаёт временные графические ресурсы лишь перед первым построением.</summary>
        private void EnsureResources()
        {
            if (_mesh == null) _mesh = !Application.isPlaying && bakedMesh != null ? bakedMesh
                : new Mesh { name = name + " Terrain Mesh", hideFlags = HideFlags.DontSave };
            if (surfaceMaterial != null) { _renderer.sharedMaterial=surfaceMaterial; return; }
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
