using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Checks the authored ground against the gameplay plane without running the farm or loading saves.</summary>
    [InitializeOnLoad]
    public static class GroundAlignmentValidator
    {
        public const float GroundY = 0f;
        public const float Tolerance = 0.001f;
        private const string MenuRoot = "Tools/My Little Farm/Ground/";

        static GroundAlignmentValidator() => EditorSceneManager.sceneSaved += ValidateSavedFarm;

        private static void ValidateSavedFarm(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<GameBootstrap>(true) == null) continue;
                foreach (var issue in ValidateScene(scene)) Debug.LogWarning("Ground alignment: " + issue);
                return;
            }
        }

        /// <summary>Checks every chunk in a scene, including inactive sectors; does not modify transforms.</summary>
        public static List<string> ValidateScene(Scene scene)
        {
            var issues = new List<string>();
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
                {
                    count++;
                    ValidateChunk(chunk, issues);
                }
                foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
                {
                    var visual = player.VisualRoot;
                    var controller = player.GetComponent<CharacterController>();
                    if (visual == null || controller == null || !TryGetPlayerFeetLocalY(player.transform, visual, out var feet))
                        continue;
                    var contact = controller.center.y - controller.height * 0.5f - controller.skinWidth;
                    if (Mathf.Abs(feet - contact) > Tolerance)
                        issues.Add(player.name + $": visual feet offset from capsule contact is {feet - contact:F4}.");
                }
            }
            if (count == 0) issues.Add("The scene contains no TilemapChunk surfaces to validate.");
            return issues;
        }

        /// <summary>Uses posed mesh vertices: animation culling bounds can extend below the actual soles.</summary>
        public static bool TryGetPlayerFeetLocalY(Transform player, Transform visual, out float feet)
        {
            feet = float.PositiveInfinity;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                var mesh = new Mesh();
                try
                {
                    // Imported FBX children have scale 100. Compensate for it before TransformPoint.
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                        feet = Mathf.Min(feet, player.InverseTransformPoint(skin.transform.TransformPoint(vertex)).y);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if (!float.IsPositiveInfinity(feet)) return true;
            if (!VisualPrefab.TryGetBounds(visual.gameObject, out var bounds)) return false;
            feet = player.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
            return true;
        }

        /// <summary>Reports missing art, physical offsets, sloped surfaces and stray imported colliders.</summary>
        public static void ValidateChunk(TilemapChunk chunk, List<string> issues)
        {
            var prefix = chunk.name + ": ";
            // Phase 2 uses a saved heightfield mesh as both visual and physical terrain.
            if (chunk.WorldMap != null && chunk.WorldMap.HasRelief && chunk.TerrainCollider is MeshCollider meshSurface)
            {
                var filter=chunk.GetComponent<MeshFilter>();
                if (!meshSurface.enabled || meshSurface.isTrigger || meshSurface.sharedMesh==null
                    || filter==null || filter.sharedMesh!=meshSurface.sharedMesh)
                    issues.Add(prefix+"Authored relief must share its visible mesh with an enabled non-trigger MeshCollider.");
                return;
            }
            var visual = chunk.transform.Find("Ground Visual");
            if (visual == null || !VisualPrefab.TryGetBounds(visual.gameObject, out var bounds))
                issues.Add(prefix + "Ground Visual or its renderer is missing.");
            else
            {
                if (Mathf.Abs(bounds.max.y - GroundY) > Tolerance)
                    issues.Add(prefix + $"Ground Visual top is {bounds.max.y:F4}; expected Y = 0.");
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                    if (collider.enabled)
                        issues.Add(prefix + "Ground Visual contains an enabled collider: " + collider.name);
            }

            // Read the authored box, not Collider.bounds: inactive sectors have empty physics bounds.
            var surface = chunk.TerrainCollider as BoxCollider;
            if (surface == null)
            {
                issues.Add(prefix + "Terrain Surface must have a BoxCollider.");
                return;
            }
            if (!surface.enabled || surface.isTrigger)
                issues.Add(prefix + "Terrain Surface collider must be enabled and non-trigger.");
            var half = surface.size * 0.5f;
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = surface.transform.TransformPoint(surface.center + new Vector3(x * half.x, half.y, z * half.z));
                if (Mathf.Abs(corner.y - GroundY) <= Tolerance) continue;
                issues.Add(prefix + $"Terrain Surface top corner is {corner.y:F4}; expected a flat plane at Y = 0.");
                return;
            }
        }

        /// <summary>Moves only the visual along world Y; preserves horizontal placement, scale and physics.</summary>
        public static bool AlignGroundVisual(GameObject visual)
        {
            if (visual == null || !VisualPrefab.TryGetBounds(visual, out var bounds))
                throw new InvalidOperationException("Ground Visual must contain a renderer before alignment.");
            var offset = GroundY - bounds.max.y;
            if (Mathf.Abs(offset) < 0.000001f) return false;
            Undo.RecordObject(visual.transform, "Align Ground Visual to Y = 0");
            visual.transform.position += Vector3.up * offset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            return true;
        }

        [MenuItem(MenuRoot + "Validate Active Scene")]
        public static void ValidateActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var issues = ValidateScene(scene);
            foreach (var issue in issues) Debug.LogWarning("Ground alignment: " + issue);
            if (issues.Count == 0) Debug.Log("Ground alignment: authored visual and physical surfaces match; legacy flat surfaces remain at Y = 0.");
        }

        [MenuItem(MenuRoot + "Align Visuals To Y=0")]
        public static void AlignActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Align the authored farm in Edit Mode.");
            var scene = SceneManager.GetActiveScene();
            var changed = 0;
            var playersAdjusted = 0;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Align farm ground visuals");
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
                {
                    var visual = chunk.transform.Find("Ground Visual");
                    if (visual != null && AlignGroundVisual(visual.gameObject)) changed++;
                }
                foreach (var root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
                    if (GameplayReviewRepair.AlignPlayerVisual(player)) playersAdjusted++;
            }
            finally { Undo.CollapseUndoOperations(group); }
            if (changed > 0 || playersAdjusted > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Ground alignment: adjusted {changed} ground visual(s), {playersAdjusted} player visual(s). Terrain colliders were preserved.");
            ValidateActiveScene();
        }

        [MenuItem(MenuRoot + "Align Visuals To Y=0", true)]
        private static bool CanAlign() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
