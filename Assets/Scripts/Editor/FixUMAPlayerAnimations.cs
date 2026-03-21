using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cinemachine;
using StarterAssets;
using UMA.CharacterSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tools > Fix UMA Player Animations
///
/// Fixes the floating + no-animations issue by:
///
///   REMOVE (conflicts with Starter Assets):
///     • PlayerMovement       — replaced by UMAThirdPersonController
///     • PlayerAnimator       — replaced by UMAThirdPersonController
///     • CinemachineInputBridge — replaced by UMAThirdPersonController.CameraRotation()
///     • StarterAssets.ThirdPersonController — vanilla SA script, replaced by UMA version
///
///   ADD / CONFIGURE:
///     • StarterAssetsInputs  — required by UMAThirdPersonController
///     • PlayerInput          — switched to StarterAssets.inputactions (Send Messages)
///     • UMAThirdPersonController — UMA-aware movement + animator driver
///
///   ANIMATOR:
///     • Assigns StarterAssetsThirdPerson.controller to the DCA for both races.
///       This controller has Speed/MotionSpeed/Grounded/Jump/FreeFall which match
///       what UMAThirdPersonController drives every frame.
///
///   CAMERA:
///     • Creates "CinemachineCameraTarget" child on Player at eye height (Y=1.35).
///     • Disables any CinemachineFreeLook cameras (they fight with the SA camera system).
///     • Instantiates the Starter Assets PlayerFollowCamera prefab (VirtualCamera).
///     • Sets Follow + LookAt to the new CinemachineCameraTarget.
///     • Ensures a MainCamera tagged "MainCamera" is in the scene.
/// </summary>
public static class FixUMAPlayerAnimations
{
    const float EyeHeight = 1.35f;   // local Y of CinemachineCameraTarget on Player

    [MenuItem("Tools/Fix UMA Player Animations")]
    public static void Run()
    {
        var results  = new List<string>();
        var warnings = new List<string>();

        // ── Find Player ───────────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Fix UMA Player Animations",
                "No GameObject tagged 'Player' found in the scene.\n\n" +
                "Tag your Player root object 'Player' and try again.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Fix UMA Player Animations");

        // ── 1. Remove conflicting scripts ─────────────────────────────────────
        Remove<PlayerMovement>(player,  results, "PlayerMovement");
        Remove<PlayerAnimator>(player,  results, "PlayerAnimator");
        Remove<CinemachineInputBridge>(player, results, "CinemachineInputBridge");
        var vanillaTPC = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (vanillaTPC != null)
        {
            Undo.DestroyObjectImmediate(vanillaTPC);
            results.Add("✓ StarterAssets.ThirdPersonController removed");
        }

        // ── 2. Add StarterAssetsInputs ────────────────────────────────────────
        if (player.GetComponent<StarterAssetsInputs>() == null)
        {
            Undo.AddComponent<StarterAssetsInputs>(player);
            results.Add("✓ StarterAssetsInputs added");
        }
        else { results.Add("ℹ StarterAssetsInputs already present"); }

        // ── 3. PlayerInput → StarterAssets.inputactions ───────────────────────
#if ENABLE_INPUT_SYSTEM
        ConfigurePlayerInput(player, results, warnings);
#else
        warnings.Add("⚠ ENABLE_INPUT_SYSTEM not active — PlayerInput skipped.");
#endif

        // ── 4. UMAThirdPersonController ───────────────────────────────────────
        var ctrl = player.GetComponent<UMAThirdPersonController>();
        if (ctrl == null)
        {
            ctrl = Undo.AddComponent<UMAThirdPersonController>(player);
            results.Add("✓ UMAThirdPersonController added");
        }
        else { results.Add("ℹ UMAThirdPersonController already present"); }

        // ── 5. CinemachineCameraTarget child ──────────────────────────────────
        var camTarget = EnsureCameraTarget(player, results);

        // ── 6. Wire CinemachineCameraTarget to controller ─────────────────────
        if (ctrl != null && camTarget != null)
        {
            ctrl.CinemachineCameraTarget = camTarget.gameObject;
            ctrl.GroundLayers            = LayerMask.GetMask("Default");
            EditorUtility.SetDirty(ctrl);
            results.Add("✓ UMAThirdPersonController.CinemachineCameraTarget wired");
            results.Add("✓ GroundLayers set to Default");
        }

        // ── 7. Assign StarterAssetsThirdPerson.controller to DCA ──────────────
        AssignAnimator(player, results, warnings);

        // ── 8. Camera setup ───────────────────────────────────────────────────
        SetupCamera(camTarget, results, warnings);

        // ── Finalise ──────────────────────────────────────────────────────────
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        // Report
        string summary = string.Join("\n", results);
        if (warnings.Count > 0)
            summary += "\n\nWARNINGS:\n" + string.Join("\n", warnings);

        summary +=
            "\n\n── Notes ──\n" +
            "• Save the scene (Ctrl+S) then press Play.\n" +
            "• PlayerStats and PlayerCombat are untouched.\n" +
            "• Dodge system (was in PlayerMovement) is not in Starter Assets.\n" +
            "  Add a separate dodge script later if needed.";

        Debug.Log("[FixUMAPlayerAnimations]\n" + summary);
        EditorUtility.DisplayDialog("Fix UMA Player Animations — Done", summary, "OK — Save Scene");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void Remove<T>(GameObject go, List<string> log, string label) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) return;
        Undo.DestroyObjectImmediate(c);
        log.Add($"✓ {label} removed");
    }

#if ENABLE_INPUT_SYSTEM
    static void ConfigurePlayerInput(GameObject player,
                                     List<string> results, List<string> warnings)
    {
        // Find StarterAssets.inputactions
        InputActionAsset actions = null;
        foreach (var guid in AssetDatabase.FindAssets("StarterAssets t:InputActionAsset"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            // Exclude Mobile UI assets
            if (!p.Contains("Mobile"))
            {
                actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p);
                if (actions != null) break;
            }
        }

        var pi    = player.GetComponent<PlayerInput>();
        bool wasNew = pi == null;
        if (wasNew) pi = Undo.AddComponent<PlayerInput>(player);

        if (actions != null)
        {
            pi.actions = actions;
            results.Add($"✓ PlayerInput {(wasNew ? "added" : "updated")} with '{actions.name}'");
        }
        else
        {
            warnings.Add("⚠ StarterAssets.inputactions not found — assign it manually to PlayerInput.");
            if (wasNew) results.Add("✓ PlayerInput added (assign InputActionAsset manually)");
        }

        pi.notificationBehavior = PlayerNotifications.SendMessages;
        pi.defaultActionMap     = "Player";
        EditorUtility.SetDirty(pi);
    }
#endif

    static Transform EnsureCameraTarget(GameObject player, List<string> results)
    {
        var t = player.transform.Find("CinemachineCameraTarget");
        if (t != null)
        {
            results.Add("ℹ CinemachineCameraTarget child already exists");
            return t;
        }

        var go = new GameObject("CinemachineCameraTarget");
        Undo.RegisterCreatedObjectUndo(go, "Create CinemachineCameraTarget");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
        go.transform.localRotation = Quaternion.identity;
        results.Add($"✓ CinemachineCameraTarget created at local Y={EyeHeight}");
        return go.transform;
    }

    static void AssignAnimator(GameObject player,
                                List<string> results, List<string> warnings)
    {
        // Load StarterAssetsThirdPerson.controller
        RuntimeAnimatorController controller = null;
        foreach (var guid in AssetDatabase.FindAssets("StarterAssetsThirdPerson t:AnimatorController"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.Contains("StarterAssets"))
            {
                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
                if (controller != null) break;
            }
        }

        if (controller == null)
        {
            warnings.Add("⚠ StarterAssetsThirdPerson.controller not found in Assets/StarterAssets. " +
                         "Make sure the Starter Assets package is imported.");
            return;
        }

        // Find DCA
        var dca = player.GetComponentInChildren<DynamicCharacterAvatar>(true);
        if (dca == null) dca = Object.FindAnyObjectByType<DynamicCharacterAvatar>();

        if (dca == null)
        {
            warnings.Add("⚠ No DynamicCharacterAvatar found. " +
                         "Run 'Tools > Setup UMA in Game Scene' first, then re-run this tool.");
            return;
        }

        Undo.RecordObject(dca, "Assign StarterAssets animator");

        dca.raceAnimationControllers.defaultAnimationController = controller;

        var list = dca.raceAnimationControllers.animators;
        bool hasMale = false, hasFemale = false;
        foreach (var e in list)
        {
            if (e.raceName == "HumanMale")
            {
                hasMale = true;
                e.animatorController     = controller;
                e.animatorControllerName = controller.name;
            }
            if (e.raceName == "HumanFemale")
            {
                hasFemale = true;
                e.animatorController     = controller;
                e.animatorControllerName = controller.name;
            }
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
        results.Add($"✓ '{controller.name}' assigned to DCA (HumanMale + HumanFemale)");
        results.Add($"  Path: {AssetDatabase.GetAssetPath(controller)}");
    }

    static void SetupCamera(Transform camTarget,
                             List<string> results, List<string> warnings)
    {
        if (camTarget == null)
        {
            warnings.Add("⚠ CinemachineCameraTarget is null — camera not configured.");
            return;
        }

        // Disable any existing FreeLook cameras — they manage their own rotation
        // and conflict with UMAThirdPersonController.CameraRotation().
        int freelookDisabled = 0;
        foreach (var fl in Object.FindObjectsByType<CinemachineFreeLook>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(fl.gameObject, "Disable FreeLook");
            fl.gameObject.SetActive(false);
            freelookDisabled++;
        }
        if (freelookDisabled > 0)
            results.Add($"✓ {freelookDisabled} CinemachineFreeLook camera(s) disabled");

        // If there's already a VirtualCamera, reconfigure it.
        var existingVcam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (existingVcam != null)
        {
            Undo.RecordObject(existingVcam, "Reconfigure VCam");
            existingVcam.Follow = camTarget;
            existingVcam.LookAt = camTarget;
            EditorUtility.SetDirty(existingVcam);
            results.Add($"✓ Existing VirtualCamera '{existingVcam.name}' " +
                        $"Follow + LookAt → '{camTarget.name}'");
        }
        else
        {
            // Instantiate Starter Assets PlayerFollowCamera prefab.
            GameObject prefab = null;
            foreach (var guid in AssetDatabase.FindAssets("PlayerFollowCamera t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("StarterAssets"))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (prefab != null) break;
                }
            }

            if (prefab == null)
            {
                warnings.Add("⚠ PlayerFollowCamera prefab not found. " +
                             "Add a CinemachineVirtualCamera to the scene manually and set " +
                             "Follow + LookAt to 'CinemachineCameraTarget'.");
            }
            else
            {
                var inst  = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate PlayerFollowCamera");
                var vcam = inst.GetComponent<CinemachineVirtualCamera>();
                if (vcam != null)
                {
                    vcam.Follow = camTarget;
                    vcam.LookAt = camTarget;
                    EditorUtility.SetDirty(vcam);
                }
                results.Add($"✓ PlayerFollowCamera instantiated — Follow + LookAt → '{camTarget.name}'");
            }
        }

        // Ensure a camera tagged "MainCamera" exists (required by UMAThirdPersonController).
        EnsureMainCamera(results, warnings);
    }

    static void EnsureMainCamera(List<string> results, List<string> warnings)
    {
        var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCam != null)
        {
            results.Add($"ℹ MainCamera '{mainCam.name}' already in scene");
            return;
        }

        // Try Starter Assets MainCamera prefab.
        foreach (var guid in AssetDatabase.FindAssets("MainCamera t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.Contains("StarterAssets"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(inst, "Instantiate MainCamera");
                    results.Add("✓ Starter Assets MainCamera instantiated");
                    return;
                }
            }
        }

        warnings.Add("⚠ No camera tagged 'MainCamera' found. " +
                     "Tag your main camera 'MainCamera' so UMAThirdPersonController " +
                     "can read the camera orientation for movement direction.");
    }
}
