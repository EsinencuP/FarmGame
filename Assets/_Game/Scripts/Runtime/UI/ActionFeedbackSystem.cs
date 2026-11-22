using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
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
        private ParticleSystem _dust;
        private GridSystem _grid;

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
            public bool Accent;
        }

        private void OnEnable() => GameEvents.ActionVisualFeedback += Show;
        private void OnDisable()
        {
            GameEvents.ActionVisualFeedback -= Show;
            foreach (var marker in _active) Recycle(marker);
            _active.Clear();
            if (_dust != null) _dust.Clear();
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
        }

        private void Show(string action, Vector3 position, string item, int amount)
        {
            if (!Application.isPlaying) return;
            var soil = action == "plant" || action == "till";
            if (action == "step")
            {
                if (_grid == null) _grid = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<GridSystem>();
                if (_grid != null)
                {
                    var cell = _grid.WorldToGrid(position);
                    var surface = _grid.GetCell(cell).type;
                    if (surface is CellType.Tilled or CellType.Planted or CellType.Sand) EmitDust(position, 3);
                }
                return;
            }
            if (soil) EmitDust(position, action == "plant" ? 7 : 5);
            var accent = action is "crop-ready" or "animal-ready" or "upgrade" or "sector-unlock";
            var ground = soil || accent;
            if (_active.Count >= maxVisibleMarkers || (!ground && (item == null || amount == 0))) return;
            var marker = _pool.Count > 0 ? _pool.Pop() : CreateMarker();
            marker.Ground = ground;
            marker.Accent = accent;
            marker.Origin = position + Vector3.up * (ground ? .045f : .45f);
            marker.StartedAt = Time.unscaledTime;
            marker.Color = accent ? (action == "sector-unlock" ? new Color(.76f, 1f, .52f) : harvestColor)
                : soil ? Color.Lerp(plantColor, new Color(.88f, .64f, .36f), .78f)
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

        private Material RingMaterial => _ringMaterial != null ? _ringMaterial :
            _ringMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };

        private void EmitDust(Vector3 position, int count)
        {
            if (FarmSettingsUI.ReduceMotion) return;
            if (_dust == null)
            {
                var node = new GameObject("Soil dust") { layer = 2 };
                node.SetActive(false);
                node.transform.SetParent(transform, false);
                _dust = node.AddComponent<ParticleSystem>();
                var main = _dust.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = .25f;
                main.maxParticles = 64;
                var emission = _dust.emission;
                emission.enabled = false;
                var shape = _dust.shape;
                shape.enabled = false;
                var colors = _dust.colorOverLifetime;
                colors.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                colors.color = fade;
                var renderer = node.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = RingMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                node.SetActive(true);
                _dust.Play();
            }
            for (var i = 0; i < count; i++)
            {
                var angle = i * 2.4f;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                _dust.Emit(new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * .06f + direction * .07f * (1 + i % 3),
                    velocity = direction * .34f + Vector3.up * (.36f + .13f * (i % 3)),
                    startColor = new Color32(191, 145, 91, 150),
                    startLifetime = .24f + .04f * (i % 3),
                    startSize = .035f + .009f * (i % 3)
                }, 1);
            }
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
            ring.sharedMaterial = RingMaterial;
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
                var lifetime = marker.Ground && !marker.Accent ? duration * .58f : Mathf.Max(.8f, duration);
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
                var radius = marker.Accent ? .88f : .44f;
                marker.Ring.transform.localScale = Vector3.one * (FarmSettingsUI.ReduceMotion ? radius : Mathf.Lerp(.14f, radius, t));
                var color = marker.Color; color.a = (1 - progress) * .85f;
                marker.Ring.startColor = marker.Ring.endColor = color;
                return;
            }
            marker.Root.transform.position = marker.Origin + Vector3.up * (FarmSettingsUI.ReduceMotion ? 0 : riseHeight * t);
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) marker.Card.transform.rotation = _camera.transform.rotation;
            marker.Card.transform.localScale = Vector3.one * (.008f * (FarmSettingsUI.ReduceMotion
                ? 1f : Mathf.Lerp(.78f, 1f, Mathf.Clamp01(progress * 6f))));
            marker.Card.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1f, progress));
        }

        private void Recycle(Marker marker)
        {
            marker.Root.SetActive(false);
            _pool.Push(marker);
        }
    }
}
