using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Shows committed actions using a shared pool of ground rings and item markers.</summary>
    public sealed class ActionFeedbackSystem : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float duration = .55f;
        [SerializeField, Min(0f)] private float riseHeight = .7f;
        [SerializeField] private Color plantColor = new(.76f, .61f, .36f);
        [SerializeField] private Color harvestColor = new(1f, .82f, .28f);
        [SerializeField] private Color saleColor = new(1f, .92f, .12f);
        [SerializeField] private Color collectColor = new(.96f, .96f, .86f);
        [SerializeField, Range(1, 64)] private int maxVisibleMarkers = 24;

        private readonly List<Marker> _active = new(24);
        private readonly Stack<Marker> _pool = new(24);
        private Camera _camera;
        private Material _ringMaterial;

        private sealed class Marker
        {
            public GameObject Root;
            public LineRenderer Ring;
            public CanvasGroup Card;
            public Image Icon;
            public Text Quantity;
            public Vector3 Origin;
            public Color Color;
            public float StartedAt;
            public bool Ground;
        }

        private void OnEnable() => GameEvents.ActionVisualFeedback += Show;
        private void OnDisable()
        {
            GameEvents.ActionVisualFeedback -= Show;
            foreach (var marker in _active) Recycle(marker);
            _active.Clear();
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
        }

        private void Show(string action, Vector3 position, string item, int amount)
        {
            var ground = action == "plant";
            if (!Application.isPlaying || _active.Count >= maxVisibleMarkers || (!ground && (item == null || amount == 0))) return;
            var marker = _pool.Count > 0 ? _pool.Pop() : CreateMarker();
            marker.Ground = ground;
            marker.Origin = position + Vector3.up * (ground ? .045f : .45f);
            marker.StartedAt = Time.unscaledTime;
            marker.Color = ground ? Color.Lerp(plantColor, new Color(.88f, .64f, .36f), .78f)
                : action == "harvest" ? harvestColor : action == "collect" ? collectColor : saleColor;
            marker.Ring.gameObject.SetActive(ground);
            marker.Card.gameObject.SetActive(!ground);
            if (!ground)
            {
                marker.Icon.sprite = FarmUITheme.Sprite("Icons/" + (item == "coin" ? "coin" : FarmUITheme.ItemIcon(item)));
                marker.Icon.enabled = marker.Icon.sprite != null;
                marker.Quantity.text = amount.ToString("+#,0;-#,0;0");
                marker.Quantity.color = amount < 0 ? new Color(1f, .76f, .58f) : marker.Color;
            }
            marker.Root.SetActive(true);
            Paint(marker, 0);
            _active.Add(marker);
        }

        private Marker CreateMarker()
        {
            var root = new GameObject("Action feedback");
            root.layer = 2;
            root.transform.SetParent(transform, false);
            var ring = new GameObject("Soil ring").AddComponent<LineRenderer>();
            ring.gameObject.layer = 2;
            ring.transform.SetParent(root.transform, false);
            ring.useWorldSpace = false; ring.loop = true;
            ring.alignment = LineAlignment.TransformZ;
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ring.positionCount = 24;
            ring.widthMultiplier = .055f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (_ringMaterial == null) _ringMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            ring.sharedMaterial = _ringMaterial;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }

            var card = FarmUITheme.Rect(root.transform, "Item and quantity");
            card.sizeDelta = new Vector2(164, 54);
            card.localScale = Vector3.one * .008f;
            card.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var icon = FarmUITheme.Icon(card, "coin");
            FarmUITheme.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(5, 0), new Vector2(48, 48));
            var quantity = FarmUITheme.Label(card, "", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 40);
            quantity.alignment = TextAnchor.MiddleLeft;
            quantity.rectTransform.offsetMin = new Vector2(56, 0);
            var outline = quantity.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.17f, .12f, .08f, .95f);
            outline.effectDistance = new Vector2(2, -2);
            return new Marker { Root = root, Ring = ring, Card = group, Icon = icon, Quantity = quantity };
        }

        private void LateUpdate()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var marker = _active[i];
                var lifetime = marker.Ground ? duration * .58f : Mathf.Max(.8f, duration);
                var progress = (Time.unscaledTime - marker.StartedAt) / lifetime;
                if (progress >= 1f) { Recycle(marker); _active.RemoveAt(i); }
                else Paint(marker, progress);
            }
        }

        private void Paint(Marker marker, float progress)
        {
            var t = 1 - (1 - progress) * (1 - progress);
            if (marker.Ground)
            {
                marker.Root.transform.position = marker.Origin;
                marker.Ring.transform.localScale = Vector3.one * (FarmSettingsUI.ReduceMotion ? .35f : Mathf.Lerp(.14f, .44f, t));
                var color = marker.Color; color.a = (1 - progress) * .85f;
                marker.Ring.startColor = marker.Ring.endColor = color;
                return;
            }
            marker.Root.transform.position = marker.Origin + Vector3.up * (FarmSettingsUI.ReduceMotion ? 0 : riseHeight * t);
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) marker.Card.transform.rotation = _camera.transform.rotation;
            marker.Card.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1f, progress));
        }

        private void Recycle(Marker marker)
        {
            marker.Root.SetActive(false);
            _pool.Push(marker);
        }
    }
}
