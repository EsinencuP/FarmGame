using System;
using System.Collections;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyLittleFarm.Core
{
    /// <summary>One temporary loader survives a single scene replacement, then releases its resources.</summary>
    public sealed class FarmSceneFlow : MonoBehaviour
    {
        public const string MenuScene = "Assets/_Game/Scenes/MainMenu.unity";
        public const string SlotsScene = "Assets/_Game/Scenes/SaveSelection.unity";
        public const string GameScene = "Assets/_Game/Scenes/GameScene.unity";
        public const string PrototypeScene = "Assets/_Game/Scenes/Prototype.unity";

        public static bool IsFarmScene(Scene scene) => scene.path == GameScene || scene.path == PrototypeScene;
        private static FarmSceneFlow _active;
        internal readonly struct FarmLaunch
        {
            public readonly int Slot;
            public readonly bool Manual, NewFarm;
            public FarmLaunch(int slot, bool manual, bool newFarm) { Slot = slot; Manual = manual; NewFarm = newFarm; }
        }
        private static FarmLaunch? _launch;
        private static bool _launchFailed;
        public static bool IsLoading => _active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _active = null; _launch = null; _launchFailed = false;
            FarmUITheme.ClearCache();
        }
        public static bool LoadSlots() => Begin(SlotsScene, "Сохранения…");
        internal static bool LoadFarm(int slot, bool manual, bool newFarm)
        {
            if (!Begin(GameScene, "Загрузка фермы…")) return false;
            _launch = new FarmLaunch(slot, manual, newFarm);
            return true;
        }
        internal static FarmLaunch? TakeFarmLaunch()
        {
            var launch = _launch;
            _launch = null;
            return launch;
        }
        internal static void ReportLaunchFailure() => _launchFailed = true;
        internal static bool TakeLaunchFailure()
        {
            var result = _launchFailed;
            _launchFailed = false;
            return result;
        }
        public static bool LoadMenu() => Begin(MenuScene, "Главное меню…");
        private static bool Begin(string scene, string message)
        {
            if (!Application.isPlaying || IsLoading) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogError("Scene is absent from the build list: " + scene); return false; }
            var root = new GameObject("Scene transition");
            DontDestroyOnLoad(root); _active = root.AddComponent<FarmSceneFlow>();
            _active.CreateOverlay(message); _active.StartCoroutine(_active.Replace(scene)); return true;
        }
        private void CreateOverlay(string message)
        {
            var canvas = new GameObject("Loading overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            var surface = canvas.GetComponent<Canvas>(); surface.renderMode = RenderMode.ScreenSpaceOverlay; surface.sortingOrder = 32000;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var loading = FarmScreenArtwork.CreateLoading(canvas.transform, message, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            loading.SetActive(true);
        }
        private IEnumerator Replace(string scene)
        {
            // Scaled simulation must not inherit a pause from the scene being unloaded.
            var previousScale = Time.timeScale;
            Time.timeScale = 1;
            yield return null;
            FarmUITheme.ClearCache();
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single); }
            catch (Exception error) { Debug.LogException(error); }
            if (operation != null)
            {
                yield return operation;
                // Release the previous world's assets and static sprite references after its objects unload.
                yield return Resources.UnloadUnusedAssets();
                yield return null;
                if (scene == GameScene && (_launchFailed || _launch.HasValue))
                {
                    _launch = null;
                    _launchFailed = true;
                    yield return SceneManager.LoadSceneAsync(SlotsScene, LoadSceneMode.Single);
                }
            }
            else { _launch = null; Time.timeScale = previousScale; }
            Destroy(gameObject);
        }
        private void OnDestroy() { if (_active == this) _active = null; }
    }
}
