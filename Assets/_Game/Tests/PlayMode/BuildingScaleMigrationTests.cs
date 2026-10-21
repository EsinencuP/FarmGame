using System.Collections;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Proves version-six buildings expand without losing crops, money or IDs, and fail atomically without room.</summary>
    public sealed class BuildingScaleMigrationTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator LegacyHouseRelocatesAroundCropAndPreservesCallerAndEconomy()
        {
            var grid=Farm.GetComponentInChildren<GridSystem>();
            var north=new GameObject("Migration north chunk");north.transform.SetParent(Farm.transform,false);north.transform.position=new Vector3(0,0,10);
            var chunk=north.AddComponent<TilemapChunk>();grid.RegisterSceneChunk(chunk);chunk.ApplyTo(grid);
            var cropPosition=new Vector2Int(4,7);grid.SetCellType(cropPosition,CellType.Tilled);
            Assert.That(Farm.GetComponentInChildren<CropSystem>().Plant(cropPosition,100000),Is.True);
            var snapshot=LegacyHouse(grid);
            var original=JsonUtility.ToJson(snapshot);
            Farm.GetComponentInChildren<SaveSystem>().Restore(snapshot);
            Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(original));
            var states=Farm.GetComponentInChildren<BuildSystem>().Capture();
            Assert.That(states.Count,Is.EqualTo(1));Assert.That(states[0].id,Is.EqualTo("legacy-house"));
            Assert.That(states[0].x!=1||states[0].z!=3,Is.True,"The expanded footprint must avoid the crop.");
            Assert.That(Farm.GetComponentInChildren<CropSystem>().Contains(cropPosition),Is.True);
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(25));
            Assert.That(Farm.GetComponentInChildren<SaveSystem>().Capture().saveVersion,Is.EqualTo(7));
            var definition=BuildingDefinition.Catalog[0];var count=0;
            for(var z=0;z<definition.Depth;z++)for(var x=0;x<definition.Width;x++)if(grid.GetCell(new Vector2Int(states[0].x+x,states[0].z+z)).occupantId==states[0].id)count++;
            Assert.That(count,Is.EqualTo(definition.Width*definition.Depth));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoRoomRejectsMigrationWithoutChangingLiveFarmOrSnapshot()
        {
            var grid=Farm.GetComponentInChildren<GridSystem>();
            for(var x=0;x<10;x++)for(var z=0;z<10;z++)
                if(!(x>=1&&x<=2&&z>=3&&z<=4))grid.SetTerrainCell(new Vector2Int(x,z),new TerrainCell(CellType.Rock));
            var snapshot=LegacyHouse(grid);var original=JsonUtility.ToJson(snapshot);var before=JsonUtility.ToJson(grid.Serialize());
            var error=Assert.Throws<InvalidDataException>(()=>Farm.GetComponentInChildren<SaveSystem>().Restore(snapshot));
            Assert.That(error.Message,Does.Contain("Недостаточно"));
            Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(grid.Serialize()),Is.EqualTo(before));
            Assert.That(Farm.GetComponentInChildren<BuildSystem>().Count,Is.Zero);
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(25));
            yield return null;
        }

        private SaveData LegacyHouse(GridSystem grid)
        {
            grid.OccupyWithBuilding(new Vector2Int(1,3),new Vector2Int(2,2),"legacy-house");
            var snapshot=Farm.GetComponentInChildren<SaveSystem>().Capture();snapshot.saveVersion=6;
            snapshot.buildings.Add(new BuildingRuntimeState{id="legacy-house",definitionId="house",x=1,z=3,paidCost=20});
            return snapshot;
        }

        [UnityTest]
        public IEnumerator PreTerrainMetadataSaveLoadsFromDiskAndCanBeValidatedTwice()
        {
            var grid=Farm.GetComponentInChildren<GridSystem>();
            var north=new GameObject("Old save north chunk");north.transform.SetParent(Farm.transform,false);north.transform.position=new Vector3(0,0,10);
            var chunk=north.AddComponent<TilemapChunk>();grid.RegisterSceneChunk(chunk);chunk.ApplyTo(grid);
            grid.SetCellType(new Vector2Int(9,0),CellType.Dirt);
            var snapshot=LegacyHouse(grid);snapshot.saveVersion=4;snapshot.worldBaseHash=0;
            foreach(var block in snapshot.grid.chunks)foreach(var entry in block.modifiedCells)entry.hasTerrainData=false;
            var path=Path.Combine(Application.temporaryCachePath,"metric-old-save-"+System.Guid.NewGuid().ToString("N")+".json");
            try
            {
                File.WriteAllText(path,JsonUtility.ToJson(snapshot));
                Assert.That(Farm.GetComponentInChildren<SaveSystem>().LoadFromPath(path),Is.True);
                Assert.That(Farm.GetComponentInChildren<BuildSystem>().Count,Is.EqualTo(1));
                Assert.That(grid.GetTerrainCell(new Vector2Int(9,0)).type,Is.EqualTo(CellType.Dirt));
                Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(25));
            }
            finally{if(File.Exists(path))File.Delete(path);}
            yield return null;
        }
    }
}
