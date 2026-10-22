using System;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    public static class WeatherSetup
    {
        private const string Folder="Assets/_Game/Content/World/Weather";

        [MenuItem("Tools/My Little Farm/Apply P2-3 Weather")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var cycle=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
            if(cycle==null)throw new MissingReferenceException("Install day/night lighting first.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Content/World","Weather");
            var sky=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/World/Lighting/FarmDayNightSky.mat");
            var clouds=AssetDatabase.LoadAssetAtPath<Material>("Assets/SkySeries Freebie/SunlessCirruscover.mat");
            if(sky==null||clouds==null)throw new MissingReferenceException("Install Sky Series skyboxes first.");
            sky.SetTexture("_CloudTex",clouds.GetTexture("_Tex"));EditorUtility.SetDirty(sky);
            var holder=root.transform.Find("World Weather");
            if(holder==null){var go=new GameObject("World Weather");go.transform.SetParent(root.transform,false);Undo.RegisterCreatedObjectUndo(go,"Add world weather");holder=go.transform;}
            var weather=holder.GetComponent<WeatherController>();if(weather==null)weather=holder.gameObject.AddComponent<WeatherController>();
            var rain=holder.GetComponentInChildren<ParticleSystem>(true);
            if(rain==null){var go=(GameObject)PrefabUtility.InstantiatePrefab(EnsureRainPrefab(),holder);rain=go.GetComponent<ParticleSystem>();}
            var settings=new SerializedObject(weather);
            settings.FindProperty("lighting").objectReferenceValue=cycle;
            settings.FindProperty("focus").objectReferenceValue=root.GetComponentInChildren<PlayerController>(true).transform;
            settings.FindProperty("rainParticles").objectReferenceValue=rain;
            settings.FindProperty("rainAudio").objectReferenceValue=rain.GetComponent<AudioSource>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            cycle.SetHour(cycle.StartingHour);weather.RestorePreview();
            EditorUtility.SetDirty(weather);EditorUtility.SetDirty(sky);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("P2-3 weather saved: Sunny / Cloudy / LightRain, 45-second fades, animated clouds, rain particles/audio and +20% rain growth.");
        }

        private static GameObject EnsureRainPrefab()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/LightRain.prefab");if(existing!=null)return existing;
            var clip=EnsureRainClip();
            var material=new Material(Shader.Find("MyLittleFarm/Weather/Light Rain Drop")){name="Light Rain Drops"};
            AssetDatabase.CreateAsset(material,Folder+"/LightRainDrops.mat");
            var go=new GameObject("Light Rain");go.transform.rotation=Quaternion.Euler(90,0,0);
            var rain=go.AddComponent<ParticleSystem>();rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rain.main;main.loop=true;main.playOnAwake=false;main.prewarm=false;main.duration=2;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
            main.startLifetime=new ParticleSystem.MinMaxCurve(1f,1.3f);main.startSpeed=new ParticleSystem.MinMaxCurve(12f,15f);
            main.startSize=new ParticleSystem.MinMaxCurve(.012f,.022f);main.startColor=Color.white;main.maxParticles=2400;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var shape=rain.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(26f,26f,.1f);
            var emission=rain.emission;emission.enabled=true;emission.rateOverTime=0;
            var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=.4f;velocity.y=0;velocity.z=.15f;
            var collision=rain.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;
            collision.mode=ParticleSystemCollisionMode.Collision3D;collision.quality=ParticleSystemCollisionQuality.Low;
            collision.collidesWith=~0;collision.enableDynamicColliders=false;collision.lifetimeLoss=1f;collision.radiusScale=.2f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2f;renderer.velocityScale=.012f;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var audio=go.AddComponent<AudioSource>();audio.clip=clip;audio.loop=true;audio.playOnAwake=false;
            audio.spatialBlend=0;audio.volume=0;audio.priority=160;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/LightRain.prefab");UnityEngine.Object.DestroyImmediate(go);return prefab;
        }

        private static AudioClip EnsureRainClip()
        {
            var path=Folder+"/LightRainLoop.wav";
            if(!File.Exists(path))
            {
                // Original synthesized ambience: filtered noise + small, damped droplet taps.
                // An overlap at the wrap joins continuous source samples instead of silence.
                const int rate=22050, length=rate*16, overlap=rate/2;
                var samples=new float[length+overlap];var random=new System.Random(876136);
                var fast=0f;var slow=0f;
                for(var i=0;i<samples.Length;i++)
                {
                    var white=(float)(random.NextDouble()*2-1);fast+=.22f*(white-fast);slow+=.008f*(white-slow);
                    samples[i]=(fast-slow)*.35f+white*.025f;
                }
                for(var onset=0;onset<samples.Length;onset+=random.Next(100,400))
                {
                    var hz=1800+random.NextDouble()*3800;var gain=.015+random.NextDouble()*.025;
                    for(var j=0;j<350&&onset+j<samples.Length;j++)
                        samples[onset+j]+=(float)(Math.Sin(j*2*Math.PI*hz/rate)*Math.Exp(-j/55.0)*gain);
                }
                for(var i=0;i<overlap;i++)
                {var t=(float)i/overlap;t=t*t*(3-2*t);samples[i]=Mathf.Lerp(samples[length+i],samples[i],t);}
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+length*2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                    writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(length*2);
                    for(var i=0;i<length;i++)writer.Write((short)(Mathf.Clamp(samples[i]*2.5f,-.95f,.95f)*short.MaxValue));
                    writer.Flush();File.WriteAllBytes(path,stream.ToArray());
                }
                AssetDatabase.ImportAsset(path);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.75f;importer.defaultSampleSettings=settings;
                importer.forceToMono=true;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
