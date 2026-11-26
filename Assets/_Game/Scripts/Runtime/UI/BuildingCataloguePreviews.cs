using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MyLittleFarm.UI
{
    /// <summary>Renders each in-game model once, on demand, using a single isolated camera.</summary>
    internal sealed class BuildingCataloguePreviews : IDisposable
    {
        private const int PreviewLayer = 31;
        private const int Width = 512;
        private const int Height = 344;
        private static readonly Quaternion ViewAngle = Quaternion.Euler(26, 36, 0);
        private readonly Dictionary<string, Texture2D> _textures = new();
        private readonly BuildSystem _buildings;
        private readonly GameObject _stage;
        private readonly Camera _camera;
        private readonly RenderTexture _target;

        public BuildingCataloguePreviews(BuildSystem buildings)
        {
            _buildings = buildings;
            _stage = new GameObject("Building catalogue preview stage", typeof(RuntimeMaterials))
                { hideFlags = HideFlags.HideAndDontSave };
            _stage.transform.position = Vector3.down * 2048;
            var camera = new GameObject("Preview camera", typeof(Camera));
            camera.transform.SetParent(_stage.transform, false);
            _camera = camera.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.aspect = (float)Width / Height;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.useOcclusionCulling = false;
            _camera.allowHDR = false;
            _camera.farClipPlane = 100;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            _camera.targetTexture = _target;

            var lamp = new GameObject("Preview light", typeof(Light));
            lamp.transform.SetParent(_stage.transform, false);
            lamp.transform.localPosition = new Vector3(-8, 12, -10);
            var light = lamp.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 40;
            light.intensity = 2.5f;
            light.cullingMask = 1 << PreviewLayer;
        }

        public Texture2D Get(BuildingDefinition definition)
            => _textures.TryGetValue(definition.Id, out var texture) ? texture : null;

        public Texture2D Render(BuildingDefinition definition)
        {
            if (_textures.TryGetValue(definition.Id, out var cached)) return cached;
            var model = new GameObject("Preview model");
            model.transform.SetParent(_stage.transform, false);
            var previous = RenderTexture.active;
            Texture2D output = null;
            try
            {
                _buildings.CreatePreviewArt(model.transform, definition);
                foreach (var part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = PreviewLayer;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                if (VisualPrefab.TryGetBounds(model, out var bounds))
                {
                    var extents = bounds.extents;
                    _camera.transform.SetPositionAndRotation(bounds.center - ViewAngle * Vector3.forward * (extents.magnitude + 4), ViewAngle);
                    _camera.orthographicSize = 1.12f * Mathf.Max(
                        ProjectedExtent(ViewAngle * Vector3.up, extents),
                        ProjectedExtent(ViewAngle * Vector3.right, extents) / _camera.aspect);
                    _camera.Render();
                    RenderTexture.active = _target;
                    output = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                    output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    output.Apply(false, true);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                model.SetActive(false);
                Object.Destroy(model);
            }
            _textures.Add(definition.Id, output);
            return output;
        }

        private static float ProjectedExtent(Vector3 axis, Vector3 extents)
            => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

        public void Dispose()
        {
            foreach (var texture in _textures.Values)
                if (texture != null) Object.Destroy(texture);
            if (_camera != null) _camera.targetTexture = null;
            _target.Release();
            Object.Destroy(_target);
            Object.Destroy(_stage);
        }
    }
}
