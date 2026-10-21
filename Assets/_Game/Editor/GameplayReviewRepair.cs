using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Applies the reviewed presentation fixes to the existing scene without regenerating its map.</summary>
    public static class GameplayReviewRepair
    {
        [MenuItem("Tools/My Little Farm/Apply Gameplay Review Fixes")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype in Edit Mode before applying review fixes.");
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (farm == null) throw new MissingReferenceException("GameBootstrap is missing.");
            foreach (var chunk in farm.GetComponentsInChildren<TilemapChunk>(true))
            {
                var visual = chunk.transform.Find("Ground Visual");
                if (visual == null) continue;
                MvpSceneUpgrade.AlignGround(chunk, visual.gameObject);
                for (var i = 0; i < chunk.transform.childCount; i++)
                {
                    var child = chunk.transform.GetChild(i);
                    if (!child.name.Contains("Tree") && !child.name.Contains("Rock")) continue;
                    if (child.GetComponent<InteractionTarget>() == null)
                        Undo.AddComponent<InteractionTarget>(child.gameObject).Configure(InteractionTargetKind.Obstacle, null, true);
                }
            }
            var crate = farm.transform.Find("Sale Crate");
            if (crate != null && crate.GetComponent<InteractionTarget>() == null)
                Undo.AddComponent<InteractionTarget>(crate.gameObject).Configure(InteractionTargetKind.Market);
            var player = farm.GetComponentInChildren<PlayerController>(true);
            if (player != null) AlignPlayerVisual(player);
            var grid = farm.GetComponentInChildren<GridSystem>(true);
            if (grid != null) grid.Configure(grid.CellSize, grid.ChunkSizeX, grid.ChunkSizeZ);
            var layout = farm.GetComponentInChildren<ResponsiveHudLayout>(true);
            if (layout != null)
            {
                layout.enabled = false;
                layout.enabled = true;
                layout.ApplyEditorPreview();
                foreach (var text in layout.GetComponentsInChildren<UnityEngine.UI.Text>(true)) EditorUtility.SetDirty(text);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Content/Animation/PlayerLocomotion.controller");
            if (controller != null) MvpSceneUpgrade.RepairLocomotion(controller);
            // Content IDs, economy, terrain, footprint and saves are preserved.
            ImportedArtSetup.ImportIntoPrototype();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save review fixes.");
        }

        public static bool AlignPlayerVisual(PlayerController player)
        {
            var visual = player.VisualRoot;
            var controller = player.GetComponent<CharacterController>();
            if (visual == null || controller == null ||
                !GroundAlignmentValidator.TryGetPlayerFeetLocalY(player.transform, visual, out var localBottom)) return false;
            var offset = controller.center.y - controller.height * 0.5f - controller.skinWidth - localBottom;
            if (Mathf.Abs(offset) < 0.000001f) return false;
            Undo.RecordObject(visual, "Align player feet");
            // CharacterController leaves skinWidth above the contact plane while grounded.
            visual.localPosition += Vector3.up * offset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            return true;
        }
    }
}
