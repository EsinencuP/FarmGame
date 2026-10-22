using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>Fits imported art inside a gameplay footprint without letting its colliders change the rules.</summary>
    public static class VisualPrefab
    {
        public static GameObject Create(GameObject prefab, Transform parent, Vector3 maximumSize)
        {
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, parent, false);
            FitExisting(instance, maximumSize);
            return instance;
        }

        public static void FitExisting(GameObject instance, Vector3 maximumSize)
        {
            if (instance == null) return;
            var parent = instance.transform.parent;
            if (parent == null) return;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            if (!TryGetBounds(instance, out var bounds)) return;
            var size = bounds.size;
            var scale = Mathf.Min(
                maximumSize.x / Mathf.Max(size.x, 0.001f),
                maximumSize.y / Mathf.Max(size.y, 0.001f),
                maximumSize.z / Mathf.Max(size.z, 0.001f));
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;
            instance.transform.localScale *= scale;

            if (TryGetBounds(instance, out bounds))
            {
                var localCenter = parent.InverseTransformPoint(bounds.center);
                var localBottom = parent.InverseTransformPoint(
                    new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                instance.transform.localPosition -= new Vector3(localCenter.x, localBottom.y, localCenter.z);
            }
        }

        /// <summary>Building height is authored independently of the reserved horizontal footprint.</summary>
        public static GameObject CreateBuilding(GameObject prefab, Transform parent, Vector3 size)
        {
            if (prefab == null) return null;
            var instance = Object.Instantiate(prefab, parent, false);
            if (instance.GetComponent<MyLittleFarm.Gameplay.Building.BuildingArtMetrics>() != null
                ||instance.GetComponent<MyLittleFarm.Gameplay.Building.DecorationArtMetrics>()!=null)
            {
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                // Metric art is already calibrated. Fitting it again would shrink doors or distort proportions.
                return instance;
            }
            // Prefer the orientation that uses more of the reserved footprint before fitting.
            if (TryGetBounds(instance, out var original))
            {
                var normal = Mathf.Min(size.x / Mathf.Max(0.001f, original.size.x), size.z / Mathf.Max(0.001f, original.size.z));
                var rotated = Mathf.Min(size.x / Mathf.Max(0.001f, original.size.z), size.z / Mathf.Max(0.001f, original.size.x));
                if (rotated > normal * 1.01f)
                    instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * instance.transform.localRotation;
            }
            FitExisting(instance, new Vector3(size.x, 10000f, size.z));
            if (instance != null && TryGetBounds(instance, out var bounds) && bounds.size.y > 0.001f)
            {
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale,
                    new Vector3(1f, size.y / bounds.size.y, 1f));
                if (TryGetBounds(instance, out bounds))
                    instance.transform.localPosition -= Vector3.up * parent.InverseTransformPoint(
                        new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
            }
            return instance;
        }

        public static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        /// <summary>Aggregates renderer bounds in a chosen local frame, including rotated/scaled parents.</summary>
        public static bool TryGetLocalBounds(GameObject instance, Transform relativeTo, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer) continue;
                var local = renderer.localBounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                    var point = relativeTo.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found;
        }
    }
}
