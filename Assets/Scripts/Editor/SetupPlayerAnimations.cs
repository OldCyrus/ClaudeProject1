using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Setup Player Animations
///
/// Creates Assets/Animations/PlayerLocomotion.controller with:
///   • Locomotion blend tree  (Idle → Walk → Sprint driven by Speed)
///   • Fall state             (entered when Grounded == false)
///   • Jump state             (Jump trigger)
///   • Dodge state            (Dodge trigger)
///   • Attack state           (Attack trigger — placeholder)
///   • Shoot state            (Shoot trigger — placeholder)
///
/// Then assigns the controller to the DCA on the Player (HumanMale + HumanFemale)
/// and adds PlayerAnimator to the Player if missing.
/// </summary>
public static class SetupPlayerAnimations
{
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";

    // ── FBX source paths ─────────────────────────────────────────────────────
    const string IdlesFbxPath = "Assets/UMA/Content/Example/Animators/Idles.fbx";
    const string RunsFbxPath  = "Assets/UMA/Content/Example/Animators/Runs.fbx";

    // ── Speed thresholds ─────────────────────────────────────────────────────
    const float SpeedIdle   = 0f;
    const float SpeedWalk   = 5f;
    const float SpeedSprint = 9f;

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Setup Player Animations")]
    public static void Run()
    {
        // ── 1. Load animation clips ───────────────────────────────────────────
        AnimationClip idleClip = LoadFirstClip(IdlesFbxPath, "Idle");
        AnimationClip runClip  = LoadFirstClip(RunsFbxPath,  "Run");

        if (idleClip == null || runClip == null)
        {
            EditorUtility.DisplayDialog("Setup Player Animations",
                $"Could not load animation clips.\n\n" +
                $"Idle: {(idleClip == null ? "NOT FOUND" : "OK")} ({IdlesFbxPath})\n" +
                $"Run:  {(runClip  == null ? "NOT FOUND" : "OK")} ({RunsFbxPath})\n\n" +
                "Ensure those FBX files exist and are imported.",
                "OK");
            return;
        }

        Debug.Log($"[SetupPlayerAnimations] Idle clip: '{idleClip.name}', Run clip: '{runClip.name}'");

        // ── 2. Create (or re-create) the AnimatorController ───────────────────
        EnsureFolder("Assets/Animations");

        AnimatorController controller;
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            // Overwrite existing so re-running is safe.
            AssetDatabase.DeleteAsset(ControllerPath);
        }
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ── 3. Parameters ─────────────────────────────────────────────────────
        controller.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        controller.AddParameter("Direction", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",      AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dodge",     AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Shoot",     AnimatorControllerParameterType.Trigger);

        var root = controller.layers[0].stateMachine;

        // ── 4. Locomotion blend tree (Speed: 0 → idle, 5 → walk, 9 → sprint) ─
        AnimatorState locomotionState = CreateLocomotionBlendTree(controller, root,
                                                                   idleClip, runClip);
        root.defaultState = locomotionState;

        // ── 5. Additional states ──────────────────────────────────────────────
        AnimatorState fallState   = CreateState(root, "Fall",   idleClip, new Vector3(300, 120));
        AnimatorState jumpState   = CreateState(root, "Jump",   idleClip, new Vector3(300, 200));
        AnimatorState dodgeState  = CreateState(root, "Dodge",  idleClip, new Vector3(300, 280));
        AnimatorState attackState = CreateState(root, "Attack", idleClip, new Vector3(300, 360));
        AnimatorState shootState  = CreateState(root, "Shoot",  idleClip, new Vector3(300, 440));

        // ── 6. Transitions ────────────────────────────────────────────────────

        // Locomotion ↔ Fall (Grounded)
        AddTransition(locomotionState, fallState, immediate: true)
            .AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

        AddTransition(fallState, locomotionState, immediate: true)
            .AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        // Locomotion → Jump (trigger)
        AddTransition(locomotionState, jumpState, immediate: true)
            .AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // Jump → Fall (lose ground) after 0.3 s
        var jToF = AddTransition(jumpState, fallState, immediate: false);
        jToF.hasExitTime = true;
        jToF.exitTime    = 0.5f;
        jToF.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

        // Jump → Locomotion (land early)
        AddTransition(jumpState, locomotionState, immediate: true)
            .AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        // AnyState → Dodge
        var anyToDodge = root.AddAnyStateTransition(dodgeState);
        anyToDodge.hasExitTime    = false;
        anyToDodge.duration       = 0.05f;
        anyToDodge.canTransitionToSelf = false;
        anyToDodge.AddCondition(AnimatorConditionMode.If, 0, "Dodge");

        // Dodge → Locomotion on exit
        var dodgeExit = AddTransition(dodgeState, locomotionState, immediate: false);
        dodgeExit.hasExitTime = true;
        dodgeExit.exitTime    = 0.9f;
        dodgeExit.duration    = 0.1f;

        // AnyState → Attack
        var anyToAttack = root.AddAnyStateTransition(attackState);
        anyToAttack.hasExitTime    = false;
        anyToAttack.duration       = 0.05f;
        anyToAttack.canTransitionToSelf = false;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        var attackExit = AddTransition(attackState, locomotionState, immediate: false);
        attackExit.hasExitTime = true;
        attackExit.exitTime    = 0.9f;
        attackExit.duration    = 0.1f;

        // AnyState → Shoot
        var anyToShoot = root.AddAnyStateTransition(shootState);
        anyToShoot.hasExitTime    = false;
        anyToShoot.duration       = 0.05f;
        anyToShoot.canTransitionToSelf = false;
        anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");

        var shootExit = AddTransition(shootState, locomotionState, immediate: false);
        shootExit.hasExitTime = true;
        shootExit.exitTime    = 0.9f;
        shootExit.duration    = 0.1f;

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupPlayerAnimations] Controller created at '{ControllerPath}'.");

        // ── 7. Assign to Player DCA ───────────────────────────────────────────
        int dcaCount = AssignToDCA(controller);

        // ── 8. Add PlayerAnimator to Player ───────────────────────────────────
        bool addedAnimator = AddPlayerAnimatorComponent();

        // ── 9. Report ─────────────────────────────────────────────────────────
        string msg =
            $"PlayerLocomotion.controller created at:\n{ControllerPath}\n\n" +
            $"States: Locomotion (blend tree), Fall, Jump, Dodge, Attack, Shoot\n" +
            $"Parameters: Speed, Direction, Grounded, Jump, Dodge, Attack, Shoot\n\n" +
            $"DCA race controllers updated : {dcaCount}\n" +
            $"PlayerAnimator added to Player: {(addedAnimator ? "yes" : "already present")}\n\n" +
            "NOTE: Jump, Dodge, Attack, Shoot states use the idle clip as a\n" +
            "placeholder — replace with real clips when available.\n\n" +
            "Save the scene (Ctrl+S).";

        EditorUtility.DisplayDialog("Setup Player Animations — Done", msg, "OK");
    }

    // ── Blend tree builder ────────────────────────────────────────────────────

    static AnimatorState CreateLocomotionBlendTree(AnimatorController ctrl,
                                                    AnimatorStateMachine root,
                                                    AnimationClip idleClip,
                                                    AnimationClip runClip)
    {
        var blendTree = new BlendTree
        {
            name           = "LocomotionTree",
            blendType      = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };

        blendTree.AddChild(idleClip, SpeedIdle);
        blendTree.AddChild(runClip,  SpeedWalk);
        blendTree.AddChild(runClip,  SpeedSprint);

        AssetDatabase.AddObjectToAsset(blendTree, ctrl);

        var state = root.AddState("Locomotion", new Vector3(100, 200));
        state.motion = blendTree;
        state.writeDefaultValues = true;
        return state;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static AnimatorState CreateState(AnimatorStateMachine root, string name,
                                     AnimationClip clip, Vector3 pos)
    {
        var state = root.AddState(name, pos);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    /// <summary>Adds a transition with common defaults pre-set.</summary>
    static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to,
                                                 bool immediate)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = immediate ? 0.05f : 0.15f;
        return t;
    }

    /// <summary>Loads the first AnimationClip sub-asset from an FBX, preferring
    /// one whose name contains <paramref name="preferHint"/>.</summary>
    static AnimationClip LoadFirstClip(string fbxPath, string preferHint)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip first    = null;
        AnimationClip preferred = null;

        foreach (var a in all)
        {
            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                if (first == null) first = clip;
                if (clip.name.IndexOf(preferHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    preferred = clip;
            }
        }
        return preferred ?? first;
    }

    /// <summary>Assigns the controller to the DCA on the Player in the active scene.</summary>
    static int AssignToDCA(AnimatorController controller)
    {
        int count = 0;
        var dcas = Object.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsSortMode.None);

        foreach (var dca in dcas)
        {
            Undo.RecordObject(dca, "Assign PlayerLocomotion controller");

            dca.raceAnimationControllers.defaultAnimationController = controller;

            var list    = dca.raceAnimationControllers.animators;
            bool hasMale   = false;
            bool hasFemale = false;

            foreach (var e in list)
            {
                if (e.raceName == "HumanMale")   { hasMale   = true; e.animatorController = controller; e.animatorControllerName = controller.name; }
                if (e.raceName == "HumanFemale")  { hasFemale = true; e.animatorController = controller; e.animatorControllerName = controller.name; }
            }

            if (!hasMale)
                list.Add(new DynamicCharacterAvatar.RaceAnimator
                {
                    raceName               = "HumanMale",
                    animatorController     = controller,
                    animatorControllerName = controller.name
                });

            if (!hasFemale)
                list.Add(new DynamicCharacterAvatar.RaceAnimator
                {
                    raceName               = "HumanFemale",
                    animatorController     = controller,
                    animatorControllerName = controller.name
                });

            EditorUtility.SetDirty(dca);
            count++;

            Debug.Log($"[SetupPlayerAnimations] Assigned '{controller.name}' to DCA on '{dca.gameObject.name}'.");
        }

        if (count == 0)
            Debug.LogWarning("[SetupPlayerAnimations] No DynamicCharacterAvatar found in scene — open ClaudeMainLevel and run again.");

        return count;
    }

    /// <summary>Ensures PlayerAnimator is on every GameObject that has a PlayerMovement.</summary>
    static bool AddPlayerAnimatorComponent()
    {
        bool added = false;
        var players = Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (var pm in players)
        {
            if (pm.GetComponent<PlayerAnimator>() != null) continue;

            Undo.AddComponent<PlayerAnimator>(pm.gameObject);
            EditorUtility.SetDirty(pm.gameObject);
            Debug.Log($"[SetupPlayerAnimations] Added PlayerAnimator to '{pm.gameObject.name}'.");
            added = true;
        }
        return added;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf   = path.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
