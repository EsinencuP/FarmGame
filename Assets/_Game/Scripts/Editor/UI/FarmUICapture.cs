using System;
using System.IO;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Opt-in Edit Mode screenshots of the actual Canvas. No gameplay/profile initialization.</summary>
    public static class FarmUICapture
    {
        public static string CaptureMain(int width, int height, bool settings = false, int tab = 0)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode preview.");
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            var canvas = menu.GetComponentInParent<Canvas>(); var camera = Camera.main; var scaler = canvas.GetComponent<CanvasScaler>();
            var mode = canvas.renderMode; var worldCamera = canvas.worldCamera; var plane = canvas.planeDistance; var scale = canvas.scaleFactor; var scaling = scaler.enabled;
            var previous = camera.targetTexture; var previousActive = RenderTexture.active; var aspect = camera.aspect; var mask = camera.cullingMask;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                scaler.enabled = false; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .2f;
                camera.targetTexture = target; camera.aspect = (float)width / height; camera.cullingMask = ~0; canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                Canvas.ForceUpdateCanvases(); menu.Preview(settings, tab); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("Verification/SceneSplit"); var path = "Verification/SceneSplit/" + (settings ? "Settings" : "MainMenu") + "-" + width + "x" + height + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG()); return path;
            }
            finally
            {
                camera.targetTexture = previous; camera.aspect = aspect; camera.cullingMask = mask; RenderTexture.active = previousActive;
                canvas.renderMode = mode; canvas.worldCamera = worldCamera; canvas.planeDistance = plane; canvas.scaleFactor = scale; scaler.enabled = scaling;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); Canvas.ForceUpdateCanvases(); menu.Preview();
            }
        }
        public static string Capture(FarmMenuUI.Page page, int width, int height, int settingsTab = 0, bool slotsPreview = false, bool guideAnimals = false, float guideScroll = 1, int marketTab = 0, bool sampleCart = false)
        {
            if (page == FarmMenuUI.Page.Main && UnityEngine.Object.FindAnyObjectByType<MainMenuUI>() != null) return CaptureMain(width, height);
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode preview.");
            var menu = UnityEngine.Object.FindAnyObjectByType<FarmMenuUI>();
            var canvas = menu.GetComponentInParent<Canvas>(); var camera = Camera.main;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var mode = canvas.renderMode; var worldCamera = canvas.worldCamera; var plane = canvas.planeDistance; var scale = canvas.scaleFactor; var scaling = scaler.enabled;
            var previous = camera.targetTexture; var previousActive = RenderTexture.active; var aspect = camera.aspect;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                scaler.enabled = false; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .2f;
                camera.targetTexture = target; camera.aspect = (float)width / height; canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                // A ScreenSpaceCamera canvas updates its target dimensions on the first render.
                // Prime it before calculating responsive panel scale for this capture size.
                camera.Render(); Canvas.ForceUpdateCanvases(); menu.Preview(page);
                if (page == FarmMenuUI.Page.Settings) menu.GetComponentInChildren<FarmSettingsUI>(true).PreviewPage(settingsTab);
                if (page == FarmMenuUI.Page.Guide) menu.GetComponentInChildren<FarmGuideUI>(true).Preview(guideAnimals, guideScroll);
                if (page == FarmMenuUI.Page.Shop) menu.GetComponentInChildren<FarmMarketUI>(true).Preview(marketTab, sampleCart);
                if (slotsPreview)
                {
                    menu.Preview(FarmMenuUI.Page.Game);
                    menu.transform.Find("HUD navigation").gameObject.SetActive(false);
                    var slots = canvas.GetComponentInChildren<SaveSlotsUI>(true);
                    slots.Preview(new[] {
                        new MyLittleFarm.Core.SaveSlotInfo { Index = 0, Manual = new MyLittleFarm.Core.SavePointInfo { Exists = true, CanLoad = true, Coins = 120, Crops = 12, Buildings = 3, PlayTimeSeconds = 1240 }, Autosave = new MyLittleFarm.Core.SavePointInfo() },
                        new MyLittleFarm.Core.SaveSlotInfo { Index = 1, Manual = new MyLittleFarm.Core.SavePointInfo { Exists = true, CanLoad = true, Coins = 85 }, Autosave = new MyLittleFarm.Core.SavePointInfo() },
                        new MyLittleFarm.Core.SaveSlotInfo { Index = 2, Manual = new MyLittleFarm.Core.SavePointInfo(), Autosave = new MyLittleFarm.Core.SavePointInfo() }
                    });
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("Verification/UIIntegration"); var path = "Verification/UIIntegration/" + (slotsPreview ? "Slots" : page.ToString()) + (page == FarmMenuUI.Page.Settings ? "-" + settingsTab : page == FarmMenuUI.Page.Guide ? guideAnimals ? "-Animals" : guideScroll < 1 ? "-Plants-Scrolled" : "-Plants" : "") + "-" + width + "x" + height + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG()); return path;
            }
            finally
            {
                camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = previousActive;
                canvas.renderMode = mode; canvas.worldCamera = worldCamera; canvas.planeDistance = plane; canvas.scaleFactor = scale; scaler.enabled = scaling;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                if (slotsPreview) canvas.GetComponentInChildren<SaveSlotsUI>(true).Close();
                Canvas.ForceUpdateCanvases(); menu.Preview(FarmMenuUI.Page.Game);
            }
        }
    }
}
