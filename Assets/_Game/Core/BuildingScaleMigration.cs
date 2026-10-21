using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>Expands legacy building footprints on a detached save, relocating only when necessary and never deleting content.</summary>
    internal static class BuildingScaleMigration
    {
        public static void Apply(SaveData data, GridSystem grid, Func<Vector2Int, bool> lockedSector)
        {
            Physics.SyncTransforms();
            var overlaps = new Collider[32];
            var entries = new Dictionary<Vector2Int, CellSaveEntry>();
            var chunks = new Dictionary<Vector2Int, ChunkSaveEntry>();
            foreach (var chunk in data.grid.chunks)
            {
                var coord = new Vector2Int(chunk.chunkX, chunk.chunkZ); chunks.Add(coord, chunk);
                foreach (var entry in chunk.modifiedCells)
                {
                    var position = grid.ChunkLocalToGrid(coord, new Vector2Int(entry.localX, entry.localZ));
                    entries.Add(position, entry);
                    if (!entry.hasTerrainData)
                    {
                        var terrain = grid.GetBaseTerrainCell(position);
                        var oldType = (CellType)entry.cellType;
                        if (oldType == CellType.Grass || oldType == CellType.Dirt || oldType == CellType.Road
                            || oldType == CellType.Water || oldType == CellType.Rock || oldType == CellType.Sand || oldType == CellType.Locked)
                            terrain.type = oldType;
                        entry.hasTerrainData = true; entry.terrainType = (byte)terrain.type;
                        entry.biome = terrain.biome; entry.moisture = terrain.moisture; entry.permissions = (byte)terrain.permissions;
                        entry.pathSurface = (byte)terrain.pathSurface;
                    }
                    if ((CellType)entry.cellType != CellType.Building && (CellType)entry.cellType != CellType.BuildingEdge) continue;
                    entry.cellType = entry.hasTerrainData ? entry.terrainType : (byte)grid.GetBaseTerrainCell(position).type;
                    entry.occupantId = null;
                }
            }
            var animals = new HashSet<Vector2Int>();
            foreach (var animal in data.animals) animals.Add(new Vector2Int(animal.x, animal.z));
            var positions = new List<Vector2Int>();
            foreach (var chunk in grid.GetLoadedChunkCoords())
            for (var z = 0; z < grid.ChunkSizeZ; z++)
            for (var x = 0; x < grid.ChunkSizeX; x++) positions.Add(grid.ChunkLocalToGrid(chunk, new Vector2Int(x, z)));
            var layout = new BuildingLayout(BuildingDefinition.Catalog);
            var buildings = new List<BuildingRuntimeState>(data.buildings);
            buildings.Sort((a,b) => string.CompareOrdinal(a.id,b.id));
            foreach (var state in buildings)
            {
                var origin = new Vector2Int(state.x,state.z);
                positions.Sort((a,b) =>
                {
                    var da = Math.Abs((long)a.x-origin.x)+Math.Abs((long)a.y-origin.y);
                    var db = Math.Abs((long)b.x-origin.x)+Math.Abs((long)b.y-origin.y);
                    var result = da.CompareTo(db); if (result != 0) return result;
                    result = a.x.CompareTo(b.x); return result != 0 ? result : a.y.CompareTo(b.y);
                });
                var placed = false;
                foreach (var position in positions)
                {
                    var candidate = state.Copy(); candidate.x = position.x; candidate.z = position.y;
                    var definition = layout.Definition(state.definitionId);
                    var center = BuildingView.Center(candidate.x,candidate.z,candidate.quarterTurns,definition,grid);
                    var dx = Mathf.Max(0f, Mathf.Abs(data.player.x-center.x)-definition.RotatedWidth(candidate.quarterTurns)*0.5f);
                    var dz = Mathf.Max(0f, Mathf.Abs(data.player.z-center.z)-definition.RotatedDepth(candidate.quarterTurns)*0.5f);
                    if (dx*dx+dz*dz < 0.4f*0.4f) continue;
                    var count = Physics.OverlapBoxNonAlloc(center + Vector3.up * (definition.Height * 0.5f + 0.05f),
                        new Vector3(definition.RotatedWidth(candidate.quarterTurns)*0.5f-0.07f,definition.Height*0.5f,
                            definition.RotatedDepth(candidate.quarterTurns)*0.5f-0.07f), overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    var physicalBlock = count == overlaps.Length;
                    for (var i=0; i<count && !physicalBlock; i++)
                        if (overlaps[i].GetComponentInParent<BuildingView>() == null && overlaps[i].GetComponent<CharacterController>() == null)
                            physicalBlock = true;
                    if (physicalBlock) continue;
                    if (!layout.TryPlace(candidate, (x,z) =>
                    {
                        var p = new Vector2Int(x,z);
                        if (!grid.IsChunkLoaded(grid.GridToChunk(p)) || lockedSector(p) || animals.Contains(p)) return true;
                        var terrain = grid.GetBaseTerrainCell(p);
                        if (entries.TryGetValue(p,out var entry))
                        {
                            var type = (CellType)entry.cellType;
                            if (type != CellType.Grass && type != CellType.Dirt && type != CellType.Road) return true;
                            if (entry.hasTerrainData) terrain = new TerrainCell((CellType)entry.terrainType) { permissions = (CellPermissions)entry.permissions };
                        }
                        var natural = terrain.type == CellType.Grass || terrain.type == CellType.Dirt || terrain.type == CellType.Road;
                        return !natural || !terrain.Allows(CellPermissions.Build, natural);
                    },out _)) continue;
                    placed = true; break;
                }
                if (!placed) throw new InvalidDataException("Недостаточно свободной территории для нового масштаба построек. Исходное сохранение не изменено.");
            }
            data.buildings = layout.Capture();
            foreach (var state in data.buildings)
            {
                var definition = layout.Definition(state.definitionId);
                for (var z = 0; z < definition.RotatedDepth(state.quarterTurns); z++)
                for (var x = 0; x < definition.RotatedWidth(state.quarterTurns); x++)
                {
                    var position = new Vector2Int(state.x+x,state.z+z);
                    if (!entries.TryGetValue(position,out var entry))
                    {
                        var coord = grid.GridToChunk(position);
                        if (!chunks.TryGetValue(coord,out var chunk))
                        {
                            chunk = new ChunkSaveEntry { chunkX=coord.x,chunkZ=coord.y };
                            chunks.Add(coord,chunk); data.grid.chunks.Add(chunk);
                        }
                        var local = grid.GridToLocalCell(position); var terrain = grid.GetBaseTerrainCell(position);
                        entry = new CellSaveEntry { localX=local.x,localZ=local.y,hasTerrainData=true,
                            terrainType=(byte)terrain.type,permissions=(byte)terrain.permissions,biome=terrain.biome,moisture=terrain.moisture,pathSurface=(byte)terrain.pathSurface };
                        chunk.modifiedCells.Add(entry); entries.Add(position,entry);
                    }
                    entry.cellType = (byte)(x == 0 && z == 0 ? CellType.Building : CellType.BuildingEdge);
                    entry.occupantId = state.id;
                }
            }
            data.saveVersion = SaveSystem.CurrentSaveVersion;
            data.worldBaseHash = grid.ComputeBaseMapHash();
        }
    }
}
