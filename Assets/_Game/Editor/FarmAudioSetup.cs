using System;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Audio;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit editor authoring. Never starts a farm or writes a player profile.</summary>
    public static class FarmAudioSetup
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        [MenuItem("Tools/My Little Farm/Audio/Prepare Audio System")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            EnsureFolder("Assets/_Game/Content/Audio"); EnsureFolder("Assets/_Game/Resources/Audio");
            var path = "Assets/_Game/Content/Audio/FarmAudio.mixer";
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            // Unity's editor-only mixer factory is internal. Keep reflection confined to this installer.
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Audio.AudioMixerController")).First(t => t != null);
            if (mixer == null) mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { path });
            var master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
            Expose(mixer, master, "MasterDb");
            var groups = new AudioMixerGroup[4]; var names = new[] { "Music", "SFX", "Ambience", "UI" };
            var parameters = new[] { "MusicDb", "SfxDb", "AmbienceDb", "UiDb" };
            for (var i = 0; i < groups.Length; i++)
            {
                var group = mixer.FindMatchingGroups("").FirstOrDefault(g => g.name == names[i]);
                if (group == null)
                {
                    group = (AudioMixerGroup)type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { names[i], false });
                    var add = type.GetMethod("AddChildToParent", Flags); add.Invoke(add.IsStatic ? null : mixer, new[] { (object)group, master });
                }
                groups[i] = group; Expose(mixer, group, parameters[i]); EditorUtility.SetDirty(group);
            }
            var viewProperty = type.GetProperty("views", Flags);
            var views = (Array)viewProperty.GetValue(mixer);
            if (views.Length == 0)
            {
                var viewType = viewProperty.PropertyType.GetElementType(); var view = Activator.CreateInstance(viewType);
                var all = new[] { master }.Concat(groups.Cast<object>()).ToArray();
                var guidType = master.GetType().GetProperty("groupID", Flags).PropertyType;
                var guids = Array.CreateInstance(guidType, all.Length);
                for (var i = 0; i < all.Length; i++) guids.SetValue(all[i].GetType().GetProperty("groupID", Flags).GetValue(all[i]), i);
                viewType.GetField("name", Flags).SetValue(view, "All channels"); viewType.GetField("guids", Flags).SetValue(view, guids);
                views = Array.CreateInstance(viewType, 1); views.SetValue(view, 0); viewProperty.SetValue(mixer, views);
            }
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            var catalogPath = "Assets/_Game/Resources/Audio/FarmAudioCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<FarmAudioCatalog>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<FarmAudioCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.mixer = mixer; catalog.music = groups[0]; catalog.sfx = groups[1]; catalog.ambience = groups[2]; catalog.ui = groups[3];
            catalog.AddMissingSlots();
            var rain = catalog.Find(SoundId.AmbienceRain);
            if (rain.clips == null || rain.clips.Length == 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Content/World/Weather/LightRainLoop.wav");
                if (clip != null) { rain.clips = new[] { clip }; rain.gainDb = 0; }
            }
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(mixer); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            var original = SceneManager.GetActiveScene();
            foreach (var scenePath in new[] { FarmSceneFlow.MenuScene, FarmSceneFlow.GameScene })
            {
                var scene = SceneManager.GetSceneByPath(scenePath); var opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                ApplyToScene(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            AssetDatabase.SaveAssets(); Selection.activeObject = catalog;
            Debug.Log("Audio ready: Master / Music / SFX / Ambience / UI, cue catalog, pooled playback and settings in both scenes. Player files untouched.");
        }
        public static void ApplyToScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var settings in root.GetComponentsInChildren<FarmSettingsUI>(true))
                { settings.EnsureAudioControls(); EditorUtility.SetDirty(settings); }
                foreach (var button in root.GetComponentsInChildren<Button>(true)) UIAudioFeedback.Install(button);
                foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
                    if (player.GetComponent<PlayerAudioEmitter>() == null) player.gameObject.AddComponent<PlayerAudioEmitter>();
            }
        }
        private static void Expose(AudioMixer mixer, object group, string name)
        {
            var serialized = new SerializedObject(mixer); var parameters = serialized.FindProperty("m_ExposedParameters");
            if (parameters == null) throw new InvalidOperationException("Unsupported mixer serialization in this Unity editor.");
            var guid = group.GetType().GetMethod("GetGUIDForVolume", Flags).Invoke(group, null);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                serialized.Update(); parameters = serialized.FindProperty("m_ExposedParameters");
                for (var i = 0; i < parameters.arraySize; i++)
                {
                    var entry = parameters.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("guid").boxedValue.ToString() != guid.ToString()) continue;
                    entry.FindPropertyRelative("name").stringValue = name; serialized.ApplyModifiedPropertiesWithoutUndo(); return;
                }
                // Unity orders this array by GUID, so a newly exposed parameter need not be last.
                if (attempt == 0)
                {
                    var type = mixer.GetType().Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");
                    var path = Activator.CreateInstance(type, Flags, null, new[] { group, guid }, null);
                    mixer.GetType().GetMethod("AddExposedParameter", Flags).Invoke(mixer, new[] { path });
                }
            }
            throw new InvalidOperationException("Mixer volume could not be exposed: " + name);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/'); var parent = path.Substring(0, slash); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
