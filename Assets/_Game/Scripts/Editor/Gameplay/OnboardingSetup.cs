using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit Edit Mode authoring; never starts the farm or touches a player save.</summary>
    public static class OnboardingSetup
    {
        public const string Folder = "Assets/_Game/Content/World/Onboarding";
        [MenuItem("Tools/My Little Farm/Apply P2-5 Onboarding")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var root = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (root == null || root.gameObject.scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype first.");
            var onboarding = root.GetComponentInChildren<OnboardingSystem>(true);
            var hud = root.GetComponentInChildren<HUDController>(true);
            if (onboarding == null || hud == null) throw new MissingReferenceException("Baked onboarding/HUD is missing.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Content/World", "Onboarding");
            var prefab = MakeMarker();
            var marker = root.GetComponentInChildren<OnboardingMarker>(true);
            if (marker == null) marker = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform)).GetComponent<OnboardingMarker>();
            marker.name = "Onboarding World Marker";
            marker.transform.position = new Vector3(2.5f, 1.8f, 3.5f);
            marker.gameObject.SetActive(false);
            var overlay = hud.GetComponent<OnboardingOverlay>();
            if (overlay == null) overlay = hud.gameObject.AddComponent<OnboardingOverlay>();
            var card = hud.transform.Find("Tutorial Card") as RectTransform;
            if (card == null)
            {
                card = new GameObject("Tutorial Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                card.SetParent(hud.transform, false);
            }
            var background = card.GetComponent<Image>();
            // Modal catalogue/sign/house windows must render above the tutorial card.
            card.SetAsFirstSibling();
            background.color = new Color(.065f, .12f, .085f, .94f);
            background.raycastTarget = false;
            Text hint = null;
            foreach (var text in hud.GetComponentsInChildren<Text>(true))
                if (text.name == "Onboarding Hint") { hint = text; break; }
            if (hint == null) hint = MakeText(card, "Onboarding Hint", 22);
            hint.transform.SetParent(card, false);
            hint.fontSize = 22;
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 16; hint.resizeTextMaxSize = 22;
            hint.alignment = TextAnchor.UpperLeft;
            hint.color = new Color(1f, .96f, .77f);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
            hint.supportRichText = false;
            hint.raycastTarget = false;
            hint.enabled = true;
            Stretch(hint.rectTransform, new Vector2(18f, 48f), new Vector2(-18f, -12f));
            var skipTransform = card.Find("Skip Tutorial");
            var skip = skipTransform == null ? new GameObject("Skip Tutorial", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>() : skipTransform.GetComponent<Button>();
            skip.transform.SetParent(card, false);
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-16f, 10f); skipRect.sizeDelta = new Vector2(232f, 30f);
            skip.GetComponent<Image>().color = new Color(.25f, .34f, .25f);
            skip.targetGraphic = skip.GetComponent<Image>();
            var label = skip.GetComponentInChildren<Text>(true);
            if (label == null) label = MakeText(skip.transform, "Label", 18);
            label.text = "Пропустить обучение"; label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            Set(overlay, "card", card); Set(overlay, "hint", hint); Set(overlay, "skip", skip);
            Set(onboarding, "marker", marker); Set(onboarding, "overlay", overlay);
            Set(root.GetComponent<InputReader>(), "onboardingPointerBlock", card);
            var layout = hud.GetComponent<ResponsiveHudLayout>();
            if (layout != null)
            {
                Set(layout, "onboarding", null); Set(layout, "onboardingOverlay", overlay);
                layout.ApplyEditorPreview();
            }
            overlay.Preview(0, false);
            EditorUtility.SetDirty(onboarding); EditorUtility.SetDirty(overlay);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("P2-5: five-goal card and collider-free world marker authored. No gameplay initialization.");
        }

        private static GameObject MakeMarker()
        {
            var path = Folder + "/GoalMarker.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, .77f, .18f) };
            AssetDatabase.CreateAsset(material, Folder + "/GoalGold.mat");
            var mesh = new Mesh { name = "Goal Diamond" };
            mesh.vertices = new[] { new Vector3(0,.48f,0), new Vector3(.28f,0,0), new Vector3(0,0,.28f), new Vector3(-.28f,0,0), new Vector3(0,0,-.28f), new Vector3(0,-.48f,0) };
            mesh.triangles = new[] { 0,2,1, 0,3,2, 0,4,3, 0,1,4, 5,1,2, 5,2,3, 5,3,4, 5,4,1 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/GoalDiamond.mesh.asset");
            var ring = new Mesh { name = "Goal Ring" };
            var vertices = new Vector3[64]; var triangles = new int[192];
            for (var i = 0; i < 32; i++)
            {
                var angle = i * Mathf.PI * 2f / 32f;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * .42f, -.62f, Mathf.Sin(angle) * .42f);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * .32f, -.62f, Mathf.Sin(angle) * .32f);
                var a = i * 2; var b = ((i + 1) % 32) * 2; var t = i * 6;
                triangles[t] = a; triangles[t+1] = a+1; triangles[t+2] = b;
                triangles[t+3] = b; triangles[t+4] = a+1; triangles[t+5] = b+1;
            }
            ring.vertices = vertices; ring.triangles = triangles; ring.RecalculateNormals(); ring.RecalculateBounds();
            AssetDatabase.CreateAsset(ring, Folder + "/GoalRing.mesh.asset");
            var go = new GameObject("Goal Marker") { layer = 2 };
            try
            {
                var diamond = Part(go.transform, "Diamond", mesh, material);
                Part(go.transform, "Ring", ring, material);
                var component = go.AddComponent<OnboardingMarker>(); Set(component, "diamond", diamond);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Transform Part(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 2 };
            go.transform.SetParent(parent, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        private static Text MakeText(Transform parent, string name, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
            return text;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
    }
}
