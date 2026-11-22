using System;
using System.IO;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Captures an editor preview only when explicitly requested from the Tools menu.</summary>
    public static class MvpPreviewCapture
    {
        [MenuItem("Tools/My Little Farm/Capture MVP Preview")]
        private static void Capture()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var controller = UnityEngine.Object.FindAnyObjectByType<IsometricCameraController>(FindObjectsInactive.Include);
            var camera = controller == null ? null : controller.GetComponent<Camera>();
            if (camera == null) return;
            const int width = 1024;
            const int height = 768;
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var path = Path.Combine(Path.GetTempPath(), "FarmGame-MVP-preview.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("My Little Farm: preview captured to " + path);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
