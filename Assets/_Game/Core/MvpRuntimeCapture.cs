#if UNITY_EDITOR
using System.Collections;
using System;
using System.IO;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>Opt-in runtime capture for diagnostics; regular play and automated tests do not capture.</summary>
    public sealed class MvpRuntimeCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCapture()
        {
            if (Environment.GetEnvironmentVariable("FARMGAME_CAPTURE") != "1"
                || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Prototype") return;
            new GameObject("MVP Verification Capture").AddComponent<MvpRuntimeCapture>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1f);
            var grid = FindAnyObjectByType<GridSystem>();
            var player = FindAnyObjectByType<PlayerController>();
            var canvas = FindAnyObjectByType<Canvas>();
            var stats = GameObject.Find("Stats")?.GetComponent<UnityEngine.UI.Text>();
            var capsule = player == null ? null : player.GetComponent<CharacterController>();
            var visual = player == null ? null : player.GetComponentInChildren<Animator>();
            var path = Path.Combine(Path.GetTempPath(), "FarmGame-MVP-runtime.raw");
            var diagnostics = Path.Combine(Path.GetTempPath(), "FarmGame-MVP-runtime.txt");
            File.WriteAllText(diagnostics,
                $"chunks={grid?.LoadedChunkCount}\n" +
                $"editableWest={grid?.CanTill(new Vector2Int(-5, -5))}\n" +
                $"editableStart={grid?.CanTill(new Vector2Int(3, 3))}\n" +
                $"lockedEast={grid?.GetCellType(new Vector2Int(10, 0))}\n" +
                $"playerY={player?.transform.position.y}\n" +
                $"colliderBottom={capsule?.bounds.min.y}\n" +
                $"npcAnimator={visual?.runtimeAnimatorController?.name}\n" +
                $"gameSize={Screen.width}x{Screen.height}\n" +
                $"canvasSize={canvas?.GetComponent<RectTransform>().rect.size}\n" +
                $"statsHeight={stats?.rectTransform.rect.height}\n" +
                $"statsPreferredHeight={stats?.preferredHeight}\n" +
                $"statsText={stats?.text}\n");
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.GetRawTextureData());
            Destroy(image);
            Debug.Log("My Little Farm: runtime screenshot captured at " + path);
            yield return null;
            yield return null;
            Destroy(gameObject);
        }
    }
}
#endif
