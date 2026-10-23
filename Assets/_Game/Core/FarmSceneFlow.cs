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
        public const string GameScene = "Assets/_Game/Scenes/Prototype.unity";
        private static FarmSceneFlow _active;
        public static bool IsLoading => _active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { _active = null; FarmUITheme.ClearCache(); }
        public static bool LoadGame() => Begin(GameScene, "Загрузка фермы…");
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
            var shade = FarmUITheme.Image(canvas.transform, "Loading"); FarmUITheme.Stretch(shade.rectTransform); shade.color = new Color32(35, 53, 28, 255);
            var label = FarmUITheme.Label(shade.transform, message, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 36); label.color = FarmUITheme.Cream;
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
            }
            else Time.timeScale = previousScale;
            Destroy(gameObject);
        }
        private void OnDestroy() { if (_active == this) _active = null; }
    }
}
