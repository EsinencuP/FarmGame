using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.Gameplay.Building
{
    public sealed partial class BuildSystem
    {
        private sealed class ConstructionSite
        {
            public BuildingDefinition Definition;
            public Vector2Int Cell;
            public int Turns;
            public int Duration;
            public GameObject Root;
            public RectTransform Billboard;
            public RectTransform Progress;
            public Text Timer;
            public int DisplayedSeconds = -1;
        }

        private readonly Dictionary<string, ConstructionSite> _constructionSites = new();
        private readonly List<string> _constructionReady = new();
        private Material _constructionGhostMaterial;

        internal Transform CreatePreviewArt(Transform parent, BuildingDefinition definition, int level = 0)
        {
            var art = BuildingView.CreateArt(parent, definition, BuildingView.ModelSize(definition, _grid.CellSize),
                VisualFor(definition.Id, level));
            foreach (var collider in parent.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var behaviour in parent.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var light in parent.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var child in parent.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            return art;
        }

        private void AddBuildingView(BuildingRuntimeState state, BuildingDefinition definition)
        {
            if (!state.IsUnderConstruction)
            {
                _views.Add(state.id, BuildingView.Create(transform, definition, state, _grid,
                    VisualFor(definition.Id, state.appearanceLevel)));
                return;
            }

            var site = new ConstructionSite
            {
                Definition = definition,
                Cell = new Vector2Int(state.x, state.z),
                Turns = state.quarterTurns,
                Duration = state.constructionDurationSeconds,
                Root = new GameObject("Строительство · " + definition.Name)
            };
            _constructionSites.Add(state.id, site);
            site.Root.transform.SetParent(transform, false);
            site.Root.transform.SetPositionAndRotation(BuildingView.Center(state.x, state.z, state.quarterTurns, definition, _grid),
                Quaternion.Euler(0, state.quarterTurns * 90f, 0));
            CreatePreviewArt(site.Root.transform, definition);
            if (_constructionGhostMaterial == null)
                _constructionGhostMaterial = GhostMaterial(_validGhostMaterial.shader, new Color(.39f, .78f, 1f));
            foreach (var renderer in site.Root.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = _constructionGhostMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var billboard = FarmUITheme.Rect(site.Root.transform, "Construction progress");
            billboard.localPosition = Vector3.up * (definition.Height + .55f);
            billboard.localScale = Vector3.one * .013f;
            billboard.sizeDelta = new Vector2(234, 68);
            var canvas = billboard.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            site.Billboard = billboard;
            var backdrop = FarmUITheme.Image(billboard, "Progress backdrop");
            FarmUITheme.Stretch(backdrop.rectTransform);
            backdrop.color = new Color(.045f, .095f, .085f, .94f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = FarmUITheme.Label(billboard, definition.Name, font, 20);
            FarmUITheme.Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1),
                Vector2.down * 4, new Vector2(218, 28));
            title.color = FarmUITheme.Cream;
            site.Timer = FarmUITheme.Label(billboard, "", font, 18);
            FarmUITheme.Place(site.Timer.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                Vector2.up * 12, new Vector2(220, 24));
            site.Timer.color = new Color(.72f, .92f, 1f);
            var track = FarmUITheme.Image(billboard, "Progress track");
            FarmUITheme.Place(track.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                Vector2.up * 7, new Vector2(210, 5));
            track.color = new Color(.21f, .31f, .31f);
            var progress = FarmUITheme.Image(track.transform, "Progress fill");
            FarmUITheme.Stretch(progress.rectTransform);
            progress.color = new Color(.43f, .86f, .93f);
            site.Progress = progress.rectTransform;
            UpdateConstructionDisplay(site, state.constructionSecondsRemaining);
        }

        private void LateUpdate() => AdvanceConstruction(Time.deltaTime);

        internal int FinishConstructionForDemo() => AdvanceConstruction(float.MaxValue);

        private int AdvanceConstruction(float elapsed)
        {
            if (_constructionSites.Count == 0) return 0;
            _constructionReady.Clear();
            foreach (var pair in _constructionSites)
            {
                var site = pair.Value;
                var remaining = _layout.AdvanceConstruction(pair.Key, elapsed);
                if (_camera != null) site.Billboard.rotation = _camera.transform.rotation;
                if (elapsed > 0 && remaining <= 0 && CanPlace(site.Definition.Id, site.Cell, site.Turns, pair.Key, out _))
                    _constructionReady.Add(pair.Key);
                else UpdateConstructionDisplay(site, remaining);
            }
            foreach (var id in _constructionReady)
            {
                var site = _constructionSites[id];
                var state = _layout.Get(id);
                var view = BuildingView.Create(transform, site.Definition, state, _grid, VisualFor(state.definitionId));
                _views.Add(id, view);
                _layout.CompleteConstruction(id);
                _constructionSites.Remove(id);
                DestroyView(site.Root);
                AnnounceCompletion(state.definitionId);
                GameEvents.RaiseStatusChanged("Строительство завершено · " + site.Definition.Name);
            }
            return _constructionReady.Count;
        }

        private static void UpdateConstructionDisplay(ConstructionSite site, float remaining)
        {
            var seconds = Mathf.CeilToInt(remaining);
            if (site.DisplayedSeconds != seconds)
            {
                site.DisplayedSeconds = seconds;
                site.Timer.text = seconds > 0 ? $"{seconds / 60}:{seconds % 60:00}" : "Освободите площадку";
            }
            site.Progress.anchorMax = new Vector2(1f - remaining / site.Duration, 1);
        }

        private void RemoveBuildingView(string id)
        {
            if (_views.Remove(id, out var view)) DestroyView(view.gameObject);
            if (_constructionSites.Remove(id, out var site)) DestroyView(site.Root);
        }

        private static void DestroyView(GameObject root)
        {
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }
    }
}
