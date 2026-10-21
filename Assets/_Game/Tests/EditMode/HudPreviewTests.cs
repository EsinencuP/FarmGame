using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Checks the saved HUD at release resolutions and proves Editor previews do not initialize gameplay.</summary>
    public sealed class HudPreviewTests
    {
        [TestCase(1280,720)]
        [TestCase(1920,1080)]
        [TestCase(2560,1440)]
        [TestCase(1280,800)]
        public void ConstructionCardFitsAllRowsAndPreviewLeavesFarmUninitialized(int width,int height)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Prototype.unity");
            try
            {
                var farm=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
                var build=farm.GetComponentInChildren<BuildSystem>(true);
                var field=typeof(BuildSystem).GetField("_layout",BindingFlags.Instance|BindingFlags.NonPublic);
                Assert.That(field.GetValue(build),Is.Null);
                var canvas=farm.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.WorldSpace;
                canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(width,height);
                var layout=canvas.GetComponent<ResponsiveHudLayout>();
                var router=canvas.GetComponent<InteractionPromptUI>();
                var card=canvas.GetComponentInChildren<BuildPromptUI>(true);
                var snapshot=UnityEditor.EditorJsonUtility.ToJson(farm.GetComponentInChildren<SaveSystem>());
                foreach(var mode in new[]{ResponsiveHudLayout.PreviewMode.BuildAllowed,ResponsiveHudLayout.PreviewMode.BuildBlocked})
                {
                    layout.SetEditorPreview(mode);Canvas.ForceUpdateCanvases();
                    Assert.That(card.gameObject.activeSelf,Is.True);
                    Assert.That(card.GetComponent<RectTransform>().rect.height,Is.EqualTo(180f));
                    Assert.That(card.PromptText.text,Does.Contain("E / ЛКМ — подтвердить").And.Contain("Q / R — повернуть").And.Contain("Esc / ПКМ — отмена").And.Contain("Delete — удалить"));
                    Assert.That(card.PromptText.preferredHeight,Is.LessThanOrEqualTo(card.PromptText.rectTransform.rect.height));
                    var corners=new Vector3[4];card.GetComponent<RectTransform>().GetWorldCorners(corners);
                    var canvasCorners=new Vector3[4];canvas.GetComponent<RectTransform>().GetWorldCorners(canvasCorners);
                    Assert.That(corners[0].x,Is.GreaterThanOrEqualTo(canvasCorners[0].x));
                    Assert.That(corners[2].x,Is.LessThanOrEqualTo(canvasCorners[2].x));
                    Assert.That(corners[0].y,Is.GreaterThan(canvasCorners[0].y));
                    Assert.That(corners[2].y,Is.LessThan(canvasCorners[2].y));
                    Assert.That(canvas.transform.Find("Interaction Prompt").GetComponent<Text>().enabled,Is.False);
                }
                layout.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
                Assert.That(card.gameObject.activeSelf,Is.False);
                Assert.That(canvas.transform.Find("Interaction Prompt").GetComponent<Text>().enabled,Is.True);
                Assert.That(field.GetValue(build),Is.Null,"Preview must not call BuildPrototype/Configure.");
                Assert.That(typeof(InteractionPromptUI).GetField("_buildings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router),Is.Null);
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(farm.GetComponentInChildren<SaveSystem>()),Is.EqualTo(snapshot));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
