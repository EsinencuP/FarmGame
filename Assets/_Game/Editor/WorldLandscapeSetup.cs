using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit, one-time authoring of the Phase 2 level. No runtime generation or random seed.</summary>
    public static class WorldLandscapeSetup
    {
        private const string Folder = "Assets/_Game/Content/World/Landscape";
        private const string Farm = "Assets/LowPolyFarmLite/Prefabs/";
        // Hand-authored heights at ten-unit intervals: x/z = -30,-20,-10,0,10,20,30,40.
        // The former 30x30 farm and its seams remain exactly flat and keep their original terrain data.
        private static readonly float[,] Heights =
        {
            { .2f,.3f,.6f,.8f,.6f,.5f,1.2f,1.6f },
            { .2f,.6f,.2f,.4f,.3f,.4f,1.7f,2f },
            { .5f,.2f,0,0,0,0,.6f,1f },
            { .2f,0,0,0,0,0,.5f,.7f },
            { .3f,0,0,0,0,0,1.1f,1.5f },
            { .7f,.4f,0,0,0,0,1.8f,2f },
            { 1.2f,2f,.7f,1.1f,1.4f,1.5f,2.4f,2f },
            { 1.7f,2.1f,1.4f,1.7f,2.2f,2.4f,2.6f,2.2f }
        };
        private static readonly Vector2[] Pond =
        {
            new Vector2(-25,6), new Vector2(-21,3), new Vector2(-16,4), new Vector2(-13,8),
            new Vector2(-14,13), new Vector2(-18,16), new Vector2(-23,14), new Vector2(-26,10)
        };
        private static readonly Vector2[] Trees =
        {
            new Vector2(-28,-27),new Vector2(-22,-28),new Vector2(-15,-27),new Vector2(-7,-28),
            new Vector2(1,-27),new Vector2(9,-28),new Vector2(18,-27),new Vector2(27,-28),new Vector2(37,-27),
            new Vector2(-28,-20),new Vector2(-27,-11),new Vector2(-28,-2),new Vector2(-28,7),
            new Vector2(-28,18),new Vector2(-27,27),new Vector2(-28,37),
            new Vector2(37,-19),new Vector2(38,-10),new Vector2(37,-1),new Vector2(38,8),
            new Vector2(37,17),new Vector2(38,26),new Vector2(37,37),
            new Vector2(-21,37),new Vector2(-13,38),new Vector2(-5,37),new Vector2(3,38),
            new Vector2(11,37),new Vector2(19,38),new Vector2(28,37),
            new Vector2(-23,29),new Vector2(-20,33),new Vector2(-16,28),new Vector2(-12,32),
            new Vector2(-8,28),new Vector2(-4,33),new Vector2(0,29),new Vector2(5,33),
            new Vector2(9,29),new Vector2(14,33),new Vector2(18,29),new Vector2(22,34),
            new Vector2(28,30),new Vector2(32,34),new Vector2(33,25),
            new Vector2(-21,21),new Vector2(-17,19),new Vector2(-25,-4),new Vector2(-21,-8),
            new Vector2(31,-21),new Vector2(33,-6),new Vector2(30,7),new Vector2(32,16)
        };
        private static readonly Vector2[] Rocks =
        {
            new Vector2(-25,3),new Vector2(-23,1),new Vector2(-17,1),new Vector2(-12,14),
            new Vector2(-21,17),new Vector2(-26,15),new Vector2(-27,-15),new Vector2(-19,-24),
            new Vector2(-5,-24),new Vector2(14,-25),new Vector2(32,-25),new Vector2(34,-14),
            new Vector2(34,4),new Vector2(33,12),new Vector2(31,21),new Vector2(29,34),
            new Vector2(21,36),new Vector2(16,31),new Vector2(2,35),new Vector2(-10,35),
            new Vector2(-24,34),new Vector2(-25,23)
        };
        private static readonly Vector2[] Shrubs =
        {
            new Vector2(-24,18),new Vector2(-18,18),new Vector2(-15,2),new Vector2(-24,-7),
            new Vector2(-20,-22),new Vector2(-11,-24),new Vector2(6,-23),new Vector2(21,-23),
            new Vector2(32,-12),new Vector2(33,1),new Vector2(31,13),new Vector2(30,24),
            new Vector2(-18,26),new Vector2(-7,31),new Vector2(5,28),new Vector2(16,35)
        };

        [MenuItem("Tools/My Little Farm/Apply Phase 2 Authored Landscape")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if (scene.path!="Assets/_Game/Scenes/Prototype.unity") throw new InvalidOperationException("Open Prototype first.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (root==null) throw new InvalidOperationException("Farm bootstrap is missing.");
            if (root.transform.Find("Authored Landscape")!=null)
                throw new InvalidOperationException("Landscape is already authored. Edit its maps, meshes and objects directly; automatic replacement is intentionally disabled.");
            var grid=root.GetComponentInChildren<GridSystem>(true);
            if(grid.CellSize!=1f || grid.ChunkSizeX!=10 || grid.ChunkSizeZ!=10)
                throw new InvalidOperationException("This authored layout uses one-unit cells and 10x10 chunks.");
            EnsureFolder(Folder);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Content/Art/CropStages/CropVertexColor.shader");
            var groundMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LandscapeGround.mat");
            if(groundMaterial==null){groundMaterial=new Material(shader){name="Landscape Vertex Palette"};AssetDatabase.CreateAsset(groundMaterial,Folder+"/LandscapeGround.mat");}
            var chunks=new List<TilemapChunk>();
            for(var x=-3;x<=3;x++) for(var z=-3;z<=3;z++)
            {
                var central=x>=-1 && x<=1 && z>=-1 && z<=1;
                var name=x==0&&z==0?"TileBlock_Start":x==1&&z==0?"TileBlock_Extra":"TileBlock_"+x+"_"+z;
                var existing=root.transform.Find(name);
                var block=existing==null?new GameObject(name):existing.gameObject;
                if(existing==null){Undo.RegisterCreatedObjectUndo(block,"Expand farm landscape");block.transform.SetParent(root.transform,false);block.transform.localPosition=new Vector3(x*10,0,z*10);}
                var chunk=block.GetComponent<TilemapChunk>();if(chunk==null)chunk=block.AddComponent<TilemapChunk>();
                var map=chunk.WorldMap;
                if(map==null)map=AssetDatabase.LoadAssetAtPath<WorldMapAsset>(Folder+"/"+name+".asset");
                if(map==null){map=ScriptableObject.CreateInstance<WorldMapAsset>();AssetDatabase.CreateAsset(map,Folder+"/"+name+".asset");}
                chunk.AssignWorldMap(map);
                for(var vx=x*10;vx<=x*10+10;vx++) for(var vz=z*10;vz<=z*10+10;vz++)
                    map.SetVertexHeight(new Vector2Int(vx,vz),AuthoredHeight(vx,vz));
                if(!central) for(var cx=x*10;cx<x*10+10;cx++) for(var cz=z*10;cz<z*10+10;cz++)
                {
                    var p=new Vector2(cx+0.5f,cz+0.5f);
                    var water=InsidePond(p);
                    var terrain=new TerrainCell(water?CellType.Water:CellType.Grass)
                    {biome=(byte)(cz>=26?2:0),permissions=water?CellPermissions.Custom:CellPermissions.Custom|CellPermissions.Walk};
                    if(!water && AuthoredHeight(cx,cz)==0f && AuthoredHeight(cx+1,cz+1)==0f)
                        terrain.permissions=CellPermissions.Custom|CellPermissions.Walk|CellPermissions.Farm|CellPermissions.Build|CellPermissions.Orchard;
                    map.SetTerrain(new Vector2Int(cx,cz),terrain);
                }
                var oldSurface=chunk.TerrainCollider;
                if(oldSurface!=null)oldSurface.enabled=false;
                var old=block.transform.Find("Ground Visual"); if(old!=null)old.gameObject.SetActive(false);
                var collider=block.GetComponent<MeshCollider>();if(collider==null)collider=block.AddComponent<MeshCollider>();collider.enabled=true;chunk.AssignTerrainCollider(collider);
                var view=block.GetComponent<ChunkRenderer>();if(view==null)view=block.AddComponent<ChunkRenderer>();
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".mesh.asset");
                if(mesh==null){mesh=new Mesh{name=name+" Authored Terrain"};AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".mesh.asset");}
                // Register all maps before rebuilding seam vertices.
                grid.AssignSurfaceMap(new Vector2Int(x,z),map);
                chunks.Add(chunk);
                EditorUtility.SetDirty(map); EditorUtility.SetDirty(chunk);
                var settings=new SerializedObject(view);
                settings.FindProperty("bakedMesh").objectReferenceValue=mesh;
                settings.FindProperty("surfaceMaterial").objectReferenceValue=groundMaterial;
                settings.FindProperty("grassColor").colorValue=new Color(.34f,.52f,.23f,1);
                settings.FindProperty("rockColor").colorValue=new Color(.34f,.52f,.23f,1);
                settings.FindProperty("roadColor").colorValue=new Color(.56f,.43f,.29f,1);
                settings.FindProperty("waterColor").colorValue=new Color(.32f,.39f,.24f,1);
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            grid.RegisterSceneChunks(chunks);
            foreach(var c in chunks)c.ApplyTo(grid);
            // Fixed trails use integer cell rectangles. Original nine base maps are preserved for save compatibility.
            PaintPath(grid,-27,-17,60,2,PathSurface.Dirt);
            PaintPath(grid,3,-15,2,5,PathSurface.Dirt);
            PaintPath(grid,24,-15,2,42,PathSurface.Stone);
            PaintPath(grid,-12,24,36,2,PathSurface.Stone);
            PaintPath(grid,-12,-15,2,39,PathSurface.Wood);
            var landscape=new GameObject("Authored Landscape");Undo.RegisterCreatedObjectUndo(landscape,"Dress authored landscape");landscape.transform.SetParent(root.transform,false);
            CreatePond(landscape.transform);
            CreateEarthSides(landscape.transform);
            var trees=new GameObject("LowPolyFarmLite Trees");trees.transform.SetParent(landscape.transform,false);
            var rocks=new GameObject("LowPolyFarmLite Rocks");rocks.transform.SetParent(landscape.transform,false);
            var shrubs=new GameObject("LowPolyFarmLite Crown Shrubs");shrubs.transform.SetParent(landscape.transform,false);
            for(var i=0;i<Trees.Length;i++)Place(grid,trees.transform,Farm+"Tree_04.prefab",Trees[i],0.82f+(i%4)*.10f,i*47f,true,false);
            for(var i=0;i<Rocks.Length;i++)Place(grid,rocks.transform,Farm+"Rock_04.prefab",Rocks[i],.7f+(i%3)*.28f,i*61f,false,false);
            var shrubPrefab=CreateShrub();
            for(var i=0;i<Shrubs.Length;i++)Place(grid,shrubs.transform,AssetDatabase.GetAssetPath(shrubPrefab),Shrubs[i],.75f+(i%3)*.12f,i*39f,false,true);
            CreateLimits(landscape.transform);
            foreach(var c in chunks)
            {
                c.ApplyTo(grid);
                var view=c.GetComponent<ChunkRenderer>();
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+c.name+".mesh.asset");
                view.AssignBakedGeometry(mesh,groundMaterial);
                EditorUtility.SetDirty(mesh);
                EditorUtility.SetDirty(c.WorldMap);
            }
            grid.Select(null);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("Phase 2 authored landscape saved: 70x70 cells (5.44x area), 49 chunks, pond, northern forest, LowPolyFarmLite decor, dirt/stone/wood trails.");
        }

        private static float AuthoredHeight(int x,int z)
        {
            var u=Mathf.Clamp((x+30)/10f,0,7); var v=Mathf.Clamp((z+30)/10f,0,7);
            var ix=Mathf.Min(6,Mathf.FloorToInt(u));var iz=Mathf.Min(6,Mathf.FloorToInt(v));
            var tx=Mathf.SmoothStep(0,1,u-ix);var tz=Mathf.SmoothStep(0,1,v-iz);
            var height=Mathf.Lerp(Mathf.Lerp(Heights[iz,ix],Heights[iz,ix+1],tx),Mathf.Lerp(Heights[iz+1,ix],Heights[iz+1,ix+1],tx),tz);
            var point=new Vector2(x,z);var distance=float.MaxValue;
            for(var i=0;i<Pond.Length;i++)
            {
                var a=Pond[i];var edge=Pond[(i+1)%Pond.Length]-a;
                var closest=a+edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/edge.sqrMagnitude);
                distance=Mathf.Min(distance,Vector2.Distance(point,closest));
            }
            // Fixed shoreline contour, with a shallow shelf before the deeper basin.
            if(InsidePond(point))return Mathf.Lerp(-.24f,-.85f,Mathf.SmoothStep(0,1,distance/1.6f));
            return distance<1.5f?Mathf.Lerp(-.24f,height,Mathf.SmoothStep(0,1,distance/1.5f)):height;
        }

        private static bool InsidePond(Vector2 p)
        {
            var inside=false;
            for(int i=0,j=Pond.Length-1;i<Pond.Length;j=i++)
            {
                var a=Pond[i];var b=Pond[j];
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }

        private static bool OriginalCell(Vector2Int cell)=>cell.x>=-10&&cell.x<20&&cell.y>=-10&&cell.y<20;

        private static void PaintPath(GridSystem grid,int x,int z,int width,int depth,PathSurface surface)
        {
            for(var cx=x;cx<x+width;cx++)for(var cz=z;cz<z+depth;cz++)
            {
                var p=new Vector2Int(cx,cz);if(OriginalCell(p))continue;
                var terrain=grid.GetTerrainCell(p);if(terrain.type==CellType.Water)continue;
                terrain.type=CellType.Road;terrain.pathSurface=surface;
                terrain.permissions=CellPermissions.Custom|CellPermissions.Walk;
                SetBaseTerrain(grid,p,terrain);
            }
        }

        private static void SetBaseTerrain(GridSystem grid,Vector2Int p,TerrainCell terrain)
        {
            var coord=grid.GridToChunk(p);
            var name=coord==Vector2Int.zero?"TileBlock_Start":coord==new Vector2Int(1,0)?"TileBlock_Extra":"TileBlock_"+coord.x+"_"+coord.y;
            var chunk=grid.transform.parent.Find(name).GetComponent<TilemapChunk>();
            chunk.WorldMap.SetTerrain(p,terrain);grid.SetTerrainCell(p,terrain);
        }

        private static void Place(GridSystem grid,Transform parent,string path,Vector2 p,float scale,float angle,bool tree,bool shrub)
        {
            var cell=new Vector2Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.y));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.name=(tree?"Boundary Tree":shrub?"Crown Shrub":"Landscape Rock")+" "+cell.x+","+cell.y;
            go.transform.localScale=Vector3.one*scale;go.transform.rotation=Quaternion.Euler(0,angle,0);
            go.transform.position=grid.GridToWorld(cell);
            foreach(var c in go.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            var bounds=new Bounds();var first=true;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true)){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
            go.transform.position+=Vector3.up*(grid.GridToWorld(cell).y-bounds.min.y-.025f);
            go.isStatic=true;
            if(!shrub)
            {
                // Trunks/rocks block matching cells; foliage does not create a giant canopy collider.
                var obstacle=new GameObject("Solid footprint");obstacle.transform.SetParent(parent,false);obstacle.transform.position=grid.GridToWorld(cell);
                var box=obstacle.AddComponent<BoxCollider>();box.center=new Vector3(0,tree?1f:.35f,0);box.size=new Vector3(.92f,tree?2f:.7f,.92f);
                var terrain=grid.GetTerrainCell(cell);terrain.type=CellType.Rock;terrain.permissions=CellPermissions.Custom;SetBaseTerrain(grid,cell,terrain);
            }
            else
            {
                var terrain=grid.GetTerrainCell(cell);terrain.permissions=CellPermissions.Custom|CellPermissions.Walk;SetBaseTerrain(grid,cell,terrain);
            }
        }

        private static GameObject CreateShrub()
        {
            // Reuse actual LowPolyFarmLite crown triangles and UVs, rather than a grass placeholder.
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Farm+"Tree_04.prefab");
            var filter=source.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
            var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;var kept=new List<int>();
            for(var i=0;i<triangles.Length;i+=3)
                if(vertices[triangles[i]].y>1.8f&&vertices[triangles[i+1]].y>1.8f&&vertices[triangles[i+2]].y>1.8f)
                {kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}
            if(kept.Count==0)throw new InvalidOperationException("Tree crown triangles were not found.");
            var remap=new Dictionary<int,int>();var compact=new List<Vector3>();var compactUV=new List<Vector2>();
            for(var i=0;i<kept.Count;i++)
            {
                var old=kept[i];
                if(!remap.TryGetValue(old,out var index))
                {
                    index=compact.Count;remap.Add(old,index);
                    compact.Add(new Vector3(vertices[old].x*.42f,(vertices[old].y-1.8f)*.29f,vertices[old].z*.42f));
                    compactUV.Add(uv.Length>old?uv[old]:Vector2.zero);
                }
                kept[i]=index;
            }
            var crown=new Mesh{name="LowPolyFarmLite Tree Crown Shrub"};crown.SetVertices(compact);crown.SetUVs(0,compactUV);crown.SetTriangles(kept,0);crown.RecalculateNormals();crown.RecalculateBounds();
            AssetDatabase.CreateAsset(crown,Folder+"/CrownShrub.mesh.asset");
            var go=new GameObject("LowPolyFarmLite Crown Shrub");go.AddComponent<MeshFilter>().sharedMesh=crown;
            go.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var result=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/CrownShrub.prefab");UnityEngine.Object.DestroyImmediate(go);return result;
        }

        private static void CreatePond(Transform parent)
        {
            var points=new List<Vector3>();var triangles=new List<int>();
            for(var x=-28;x<-11;x++)for(var z=1;z<19;z++)
            {
                var a=new Vector3(x,AuthoredHeight(x,z)+.02f,z);var b=new Vector3(x,AuthoredHeight(x,z+1)+.02f,z+1);
                var c=new Vector3(x+1,AuthoredHeight(x+1,z+1)+.02f,z+1);var d=new Vector3(x+1,AuthoredHeight(x+1,z)+.02f,z);
                AddClippedWater(a,b,c,points,triangles);AddClippedWater(a,c,d,points,triangles);
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/PondWater.mesh.asset");
            if(mesh==null){mesh=new Mesh{name="Authored Pond Water"};AssetDatabase.CreateAsset(mesh,Folder+"/PondWater.mesh.asset");}
            mesh.Clear();mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/PondWater.mat");
            if(material==null){material=new Material(Shader.Find("Standard")){name="Pond Water",color=new Color(.17f,.48f,.54f,1)};material.SetFloat("_Glossiness",.72f);AssetDatabase.CreateAsset(material,Folder+"/PondWater.mat");}
            var go=new GameObject("Pond Water — fixed shoreline");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;go.isStatic=true;
        }

        private static void AddClippedWater(Vector3 a,Vector3 b,Vector3 c,List<Vector3> vertices,List<int> triangles)
        {
            const float waterY=-.22f;
            var input=new[]{a,b,c};var clipped=new List<Vector3>();
            for(var i=0;i<3;i++)
            {
                var from=input[i];var to=input[(i+1)%3];
                var inside=from.y<waterY;var nextInside=to.y<waterY;
                if(inside)clipped.Add(from);
                if(inside!=nextInside)clipped.Add(Vector3.Lerp(from,to,(waterY-from.y)/(to.y-from.y)));
            }
            var start=vertices.Count;
            foreach(var point in clipped)vertices.Add(new Vector3(point.x,waterY,point.z));
            for(var i=1;i<clipped.Count-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }

        private static void CreateEarthSides(Transform parent)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            var corners=new[]{new Vector2Int(-30,-30),new Vector2Int(-30,40),new Vector2Int(40,40),new Vector2Int(40,-30)};
            for(var side=0;side<4;side++)for(var step=0;step<70;step++)
            {
                var start=corners[side];var delta=(corners[(side+1)%4]-start)/70;var a=start+delta*step;var b=a+delta;
                var index=vertices.Count;
                vertices.Add(new Vector3(a.x,AuthoredHeight(a.x,a.y)+.02f,a.y));vertices.Add(new Vector3(b.x,AuthoredHeight(b.x,b.y)+.02f,b.y));
                vertices.Add(new Vector3(b.x,-3,b.y));vertices.Add(new Vector3(a.x,-3,a.y));
                for(var i=0;i<4;i++)colors.Add(i<2?new Color(.29f,.30f,.15f):new Color(.25f,.19f,.12f));
                triangles.Add(index);triangles.Add(index+2);triangles.Add(index+1);triangles.Add(index);triangles.Add(index+3);triangles.Add(index+2);
            }
            var mesh=new Mesh{name="Authored Landscape Earth Sides"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/EarthSides.mesh.asset");
            var go=new GameObject("Landscape Earth Sides");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LandscapeGround.mat");go.isStatic=true;
        }

        /// <summary>Explicit authoring command for the supplied pond contour; never called by runtime or reload.</summary>
        public static void RefreshPondBanks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();var grid=root.GetComponentInChildren<GridSystem>();
            var chunks=root.GetComponentsInChildren<TilemapChunk>(true);
            foreach(var chunk in chunks)
            {
                var start=grid.WorldToGrid(chunk.transform.position);
                for(var x=start.x;x<=start.x+10;x++)for(var z=start.y;z<=start.y+10;z++)
                    if(x>=-28&&x<=-11&&z>=1&&z<=19)chunk.WorldMap.SetVertexHeight(new Vector2Int(x,z),AuthoredHeight(x,z));
                grid.AssignSurfaceMap(grid.GridToChunk(start),chunk.WorldMap);
            }
            // Finish every shared corner before any mesh reads a neighboring map.
            foreach(var chunk in chunks)chunk.ApplyTo(grid);
            foreach(var chunk in chunks)
            {
                var settings=new SerializedObject(chunk.GetComponent<ChunkRenderer>());settings.FindProperty("waterColor").colorValue=new Color(.32f,.39f,.24f);settings.ApplyModifiedPropertiesWithoutUndo();
                var view=chunk.GetComponent<ChunkRenderer>();view.AssignBakedGeometry(AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+chunk.name+".mesh.asset"),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LandscapeGround.mat"));
                EditorUtility.SetDirty(chunk.WorldMap);EditorUtility.SetDirty(chunk.GetComponent<MeshFilter>().sharedMesh);
            }
            var landscape=root.transform.Find("Authored Landscape");
            var water=landscape.Find("Pond Water — fixed shoreline");if(water!=null)UnityEngine.Object.DestroyImmediate(water.gameObject);
            CreatePond(landscape);
            if(landscape.Find("Landscape Earth Sides")==null)CreateEarthSides(landscape);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.gameObject.scene);
        }

        private static void CreateLimits(Transform parent)
        {
            var boundary=new GameObject("World limits");boundary.transform.SetParent(parent,false);
            for(var i=0;i<4;i++)
            {
                var go=new GameObject("Edge "+i);go.transform.SetParent(boundary.transform,false);
                go.transform.localPosition=i<2?new Vector3(i==0?-30.1f:40.1f,2,5):new Vector3(5,2,i==2?-30.1f:40.1f);
                go.AddComponent<BoxCollider>().size=i<2?new Vector3(.2f,10,70.4f):new Vector3(70.4f,10,.2f);
            }
        }

        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var slash=path.LastIndexOf('/');var parent=path.Substring(0,slash);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
        }
    }
}
