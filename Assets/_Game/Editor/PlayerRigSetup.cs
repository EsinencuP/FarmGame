using System;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Builds a replaceable Humanoid presentation from Kevin Iglesias' installed in-place motion library.</summary>
    public static class PlayerRigSetup
    {
        public const string Folder = "Assets/_Game/Content/Character";
        public const string ControllerPath = Folder + "/KevinLocomotion.controller";
        public const string PrefabPath = Folder + "/HumanPlayerVisual.prefab";
        private const string Library = "Assets/Kevin Iglesias/Human Animations/";

        public static void Install(GameBootstrap farm)
        {
            EnsureFolder(Folder);
            var controller = EnsureController();
            var prefab = EnsureVisual(controller);
            var player = farm.GetComponentInChildren<PlayerController>(true);
            if (player == null) throw new MissingReferenceException("PlayerController is missing.");
            var old = player.transform.Find("Nolant Visual");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var visual = player.transform.Find("Character Visual");
            if (visual == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Install Kevin Humanoid");
                instance.name = "Character Visual";
                visual = instance.transform;
            }
            var capsule = player.GetComponent<CharacterController>();
            Undo.RecordObject(capsule, "Set player metric capsule");
            capsule.height = 1.8f; capsule.radius = 0.3f; capsule.center = Vector3.zero;
            capsule.skinWidth = 0.03f; capsule.stepOffset = 0.25f;
            var animator = visual.GetComponentInChildren<Animator>();
            Undo.RecordObject(animator, "Use Kevin locomotion");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0f);
            GameplayReviewRepair.AlignPlayerVisual(player);
            Undo.RecordObject(player.transform, "Set player ground contact");
            player.transform.position = new Vector3(player.transform.position.x, 0.93f, player.transform.position.z);
            var settings = new SerializedObject(player);
            settings.FindProperty("walkSpeed").floatValue = 2.2f;
            settings.FindProperty("runSpeed").floatValue = 4.5f;
            settings.FindProperty("sprintSpeed").floatValue = 6.5f;
            settings.ApplyModifiedProperties();
            var renderer = player.GetComponent<Renderer>();
            if (renderer != null) Undo.DestroyObjectImmediate(renderer);
            var mesh = player.GetComponent<MeshFilter>();
            if (mesh != null) Undo.DestroyObjectImmediate(mesh);
            EditorUtility.SetDirty(animator);
        }

        private static GameObject EnsureVisual(AnimatorController controller)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                // Upgrade the initial preview-only prefab without resetting later skin/presentation edits.
                if (existing.GetComponentInChildren<Animator>().cullingMode != AnimatorCullingMode.AlwaysAnimate)
                {
                    var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                    try { NormalizeVisual(contents); PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath); }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
                return existing;
            }
            var root = new GameObject("Character Visual");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Library + "Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_M.prefab");
                if (source == null) throw new MissingReferenceException("Human Basic Motions male model is missing.");
                var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                model.name = "Humanoid Model";
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                var animator = model.GetComponent<Animator>();
                if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("The character requires a valid Humanoid Avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                NormalizeVisual(root);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void NormalizeVisual(GameObject root)
        {
            var animator = root.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0f);
            var bounds = BakedBounds(root);
            animator.transform.localScale *= 1.8f / bounds.size.y;
            animator.Rebind(); animator.Update(0f);
            bounds = BakedBounds(root);
            animator.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        public static Bounds BakedBounds(GameObject root)
        {
            var bounds = new Bounds(); var found = false;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if (!found) throw new InvalidOperationException("The character has no skinned geometry.");
            return bounds;
        }

        private static AnimationClip Clip(string relative)
        {
            var path = Library + "Animations/" + relative + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"))
                ?? throw new MissingReferenceException("Missing animation: " + path);
        }

        public static AnimatorController EnsureController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                EnsureGestureTransition(existing);
                EnsureRunState(existing);
                return existing;
            }
            EnsureFolder(Folder);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var name in new[] { "Speed", "MoveX", "MoveZ", "Gait", "VerticalSpeed", "Turn", "Playback" })
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (var name in new[] { "Grounded", "Relaxed", "Sprinting" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Gesture", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HandPose", AnimatorControllerParameterType.Int);
            var parameters = controller.parameters;
            foreach (var parameter in parameters)
            {
                if (parameter.name == "Grounded") parameter.defaultBool = true;
                if (parameter.name == "Playback") parameter.defaultFloat = 1f;
            }
            controller.parameters = parameters;
            var machine = controller.layers[0].stateMachine;
            var idle = Clip("Male/Idles/HumanM@Idle01");
            var locomotion = new BlendTree { name = "Gaits", blendParameter = "Gait", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(locomotion, controller);
            locomotion.AddChild(idle, 0f);
            locomotion.AddChild(DirectionTree(controller, "Walk", 8), 1f);
            locomotion.AddChild(DirectionTree(controller, "Run", 8), 2f);
            locomotion.AddChild(DirectionTree(controller, "Sprint", 5), 3f);
            var moving = State(machine, "Locomotion", locomotion);
            moving.speedParameter = "Playback"; moving.speedParameterActive = true;
            machine.defaultState = moving;
            var toRelaxed = State(machine, "Idle to relaxed", Clip("Male/Idles/HumanM@Idle01-Idle02"));
            var relaxed = State(machine, "Relaxed idle", Clip("Male/Idles/HumanM@Idle02"));
            var toIdle = State(machine, "Relaxed to idle", Clip("Male/Idles/HumanM@Idle02-Idle01"));
            Transition(moving, toRelaxed).AddCondition(AnimatorConditionMode.If, 0, "Relaxed");
            Exit(toRelaxed, relaxed);
            Transition(relaxed, toIdle).AddCondition(AnimatorConditionMode.IfNot, 0, "Relaxed");
            Exit(toIdle, moving);
            foreach (var state in new[] { toRelaxed, relaxed, toIdle })
                Transition(state, moving).AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            foreach (var side in new[] { "Left", "Right" })
            {
                var turn = State(machine, "Turn " + side, Clip("Male/Movement/Turn/HumanM@Turn01_" + side));
                var enter = Transition(moving, turn);
                enter.AddCondition(side == "Left" ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater,
                    side == "Left" ? -0.3f : 0.3f, "Turn");
                enter.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
                Exit(turn, moving);
                Transition(turn, moving).AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            }
            var jump = State(machine, "Jump takeoff", Clip("Male/Movement/Jump/HumanM@Jump01 - Begin"));
            jump.speed = 1.8f;
            var rise = State(machine, "Jump rise", Clip("Male/Movement/Jump/HumanM@Jump01"));
            var fall = State(machine, "Fall", Clip("Male/Movement/Jump/HumanM@Fall01"));
            var land = State(machine, "Land", Clip("Male/Movement/Jump/HumanM@Jump01 - Land"));
            land.speed = 1.5f;
            var takeoff = machine.AddAnyStateTransition(jump);
            takeoff.hasExitTime = false; takeoff.duration = 0.04f; takeoff.canTransitionToSelf = false;
            takeoff.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            var flight = Exit(jump, rise, 0.45f); flight.offset = 0.25f;
            Transition(rise, fall).AddCondition(AnimatorConditionMode.Less, 0, "VerticalSpeed");
            var drop = machine.AddAnyStateTransition(fall);
            drop.hasExitTime = false; drop.duration = 0.08f; drop.canTransitionToSelf = false;
            drop.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            drop.AddCondition(AnimatorConditionMode.Less, -0.1f, "VerticalSpeed");
            foreach (var state in new[] { jump, rise, fall })
                Transition(state, land).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Exit(land, moving, 0.8f);
            var resume = Exit(land, moving, 0.2f); resume.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            var talk = State(machine, "Talk gesture", Clip("Male/Social/Conversation/HumanM@Talk01"));
            Exit(talk, moving);
            Transition(talk, moving).AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            EnsureGestureTransition(controller);
            AddHands(controller);
            EnsureRunState(controller);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureRunState(AnimatorController controller)
        {
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == "Run")) return;
            var moving = machine.states.Select(s => s.state).Single(s => s.name == "Locomotion");
            var gaits = (BlendTree)moving.motion;
            var runTree = gaits.children.Select(c => c.motion).OfType<BlendTree>().Single(t => t.name == "Run directions");
            var run = State(machine, "Run", runTree);
            run.speedParameter = "Playback"; run.speedParameterActive = true;
            var enter = Transition(moving, run);
            enter.AddCondition(AnimatorConditionMode.Greater, PlayerController.RunAnimationThreshold, "Speed");
            enter.AddCondition(AnimatorConditionMode.Less, 2.5f, "Gait");
            enter.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            // A small exit hysteresis prevents oscillation close to the running threshold.
            Transition(run, moving).AddCondition(AnimatorConditionMode.Less, PlayerController.RunAnimationThreshold - 0.2f, "Speed");
            Transition(run, moving).AddCondition(AnimatorConditionMode.Greater, 2.5f, "Gait");
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureGestureTransition(AnimatorController controller)
        {
            var machine = controller.layers[0].stateMachine;
            var talk = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Talk gesture");
            if (talk == null || machine.anyStateTransitions.Any(t => t.destinationState == talk)) return;
            // A gesture must also interrupt relaxed idle/its transitions, instead of leaving a trigger queued until normal idle returns.
            foreach (var child in machine.states)
                foreach (var old in child.state.transitions.Where(t => t.destinationState == talk).ToArray())
                    child.state.RemoveTransition(old);
            var gesture = machine.AddAnyStateTransition(talk);
            gesture.hasExitTime = false; gesture.hasFixedDuration = true; gesture.duration = 0.1f; gesture.canTransitionToSelf = false;
            gesture.AddCondition(AnimatorConditionMode.If, 0, "Gesture");
            gesture.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            gesture.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
            EditorUtility.SetDirty(controller);
        }

        private static BlendTree DirectionTree(AnimatorController controller, string gait, int count)
        {
            var tree = new BlendTree { name = gait + " directions", blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX", blendParameterY = "MoveZ" };
            AssetDatabase.AddObjectToAsset(tree, controller);
            var directions = new[] { "Forward", "ForwardLeft", "ForwardRight", "Left", "Right", "Backward", "BackwardLeft", "BackwardRight" };
            var positions = new[] { Vector2.up, new Vector2(-1,1).normalized, new Vector2(1,1).normalized,
                Vector2.left, Vector2.right, Vector2.down, new Vector2(-1,-1).normalized, new Vector2(1,-1).normalized };
            for (var i = 0; i < count; i++)
                tree.AddChild(Clip("Male/Movement/" + gait + "/HumanM@" + gait + "01_" + directions[i]), positions[i]);
            return tree;
        }

        private static void AddHands(AnimatorController controller)
        {
            var mask = new AvatarMask { name = "Fingers only" };
            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.AddObjectToAsset(mask, controller);
            var machine = new AnimatorStateMachine { name = "Hand poses" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var grip = State(machine, "Object grip", Clip("Masked Poses/Human@ObjectGripHands01"));
            var fist = State(machine, "Closed hands", Clip("Masked Poses/Human@HandsClosed01"));
            machine.defaultState = grip;
            foreach (var pair in new[] { (state: grip, value: 1), (state: fist, value: 2) })
            {
                var transition = machine.AddAnyStateTransition(pair.state);
                transition.hasExitTime = false; transition.duration = 0.1f; transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, pair.value, "HandPose");
            }
            controller.AddLayer(new AnimatorControllerLayer { name = "Hand poses", stateMachine = machine,
                avatarMask = mask, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
        {
            var state = machine.AddState(name); state.motion = motion; state.writeDefaultValues = false; state.iKOnFeet = true;
            return state;
        }
        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to); transition.hasExitTime = false; transition.duration = 0.1f;
            transition.hasFixedDuration = true; return transition;
        }
        private static AnimatorStateTransition Exit(AnimatorState from, AnimatorState to, float time = 0.9f)
        {
            var transition = Transition(from, to); transition.hasExitTime = true; transition.exitTime = time; return transition;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
