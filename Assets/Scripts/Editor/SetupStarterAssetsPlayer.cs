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
/// Tools > Setup Starter Assets Player
///
/// Run this with ClaudeMainLevel open (after "Setup UMA in Game Scene" has been
/// run at least once so UMA_GLIB and the UMAAvatar child already exist).
///
/// What it does:
///   1. Removes the old movement scripts (PlayerMovement, PlayerAnimator,
///      ThirdPersonCamera, CinemachineInputBridge) from the Player.
///   2. Adds StarterAssetsInputs + PlayerInput (StarterAssets.inputactions).
///   3. Adds UMAThirdPersonController (UMA-aware movement + animation driver).
///   4. Creates a "CinemachineCameraTarget" child on the Player at eye height.
///   5. Wires UMAThirdPersonController.CinemachineCameraTarget.
///   6. Instantiates the Starter Assets PlayerFollowCamera prefab (or
///      reconfigures an existing CinemachineVirtualCamera in the scene).
///   7. Assigns StarterAssetsThirdPerson.controller to the UMA DCA for both
///      HumanMale and HumanFemale races.
///   8. Sets GroundLayers to the Default layer.
/// </summary>
public static class SetupStarterAssetsPlayer
{
    // Height of the CinemachineCameraTarget child above the Player pivot.
    // Player capsule is 1.8 m; eye height is ≈1.35 m above the base.
    const float CameraTargetHeight = 1.35f;

    [MenuItem("Tools/Setup Starter Assets Player")]
    public static void Run()
    {
        var results  = new List<string>();
        var warnings = new List<string>();

        // ── 1. Find Player ────────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Starter Assets Player",
                "No GameObject tagged 'Player' found.\n\n" +
                "Tag your Player object 'Player' and try again.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup Starter Assets Player");

        // ── 2. Remove old movement / animation / camera scripts ───────────────
        RemoveComponent<PlayerMovement>(player, results, "PlayerMovement");
        RemoveComponent<PlayerAnimator>(player, results, "PlayerAnimator");
        RemoveComponent<ThirdPersonCamera>(player, results, "ThirdPersonCamera");
        RemoveComponent<CinemachineInputBridge>(player, results, "CinemachineInputBridge");

        // Remove the vanilla ThirdPersonController if somehow present (user ran SA setup before).
        var tpc = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null)
        {
            Undo.DestroyObjectImmediate(tpc);
            results.Add("✓ StarterAssets.ThirdPersonController removed (replaced by UMAThirdPersonController)");
        }

        // ── 3. StarterAssetsInputs ────────────────────────────────────────────
        if (player.GetComponent<StarterAssetsInputs>() == null)
        {
            Undo.AddComponent<StarterAssetsInputs>(player);
            results.Add("✓ StarterAssetsInputs added");
        }
        else
        {
            results.Add("ℹ StarterAssetsInputs already present");
        }

        // ── 4. PlayerInput ────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
        SetupPlayerInput(player, results, warnings);
#else
        warnings.Add("⚠ ENABLE_INPUT_SYSTEM not defined — PlayerInput not added. Starter Assets requires the New Input System.");
#endif

        // ── 5. UMAThirdPersonController ───────────────────────────────────────
        var ctrl = player.GetComponent<UMAThirdPersonController>();
        if (ctrl == null)
        {
            ctrl = Undo.AddComponent<UMAThirdPersonController>(player);
            results.Add("✓ UMAThirdPersonController added");
        }
        else
        {
            results.Add("ℹ UMAThirdPersonController already present");
        }

        // ── 6. CinemachineCameraTarget child ──────────────────────────────────
        var camTarget = EnsureCameraTarget(player, results);

        // ── 7. Wire CinemachineCameraTarget to controller ─────────────────────
        if (camTarget != null && ctrl != null)
        {
            ctrl.CinemachineCameraTarget = camTarget.gameObject;
            EditorUtility.SetDirty(ctrl);
            results.Add("✓ UMAThirdPersonController.CinemachineCameraTarget wired");
        }

        // ── 8. Assign StarterAssetsThirdPerson.controller to DCA ──────────────
        AssignAnimatorToDCA(player, results, warnings);

        // ── 9. Set GroundLayers = Default ─────────────────────────────────────
        if (ctrl != null)
        {
            ctrl.GroundLayers = LayerMask.GetMask("Default");
            EditorUtility.SetDirty(ctrl);
            results.Add("✓ GroundLayers set to 'Default'");
        }

        // ── 10. Cinemachine virtual camera ────────────────────────────────────
        SetupCinemachineCamera(camTarget, results, warnings);

        // ── Finalise ──────────────────────────────────────────────────────────
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string summary = string.Join("\n", results);
        if (warnings.Count > 0)
            summary += "\n\nWARNINGS:\n" + string.Join("\n", warnings);

        summary +=
            "\n\n── Notes ──\n" +
            "• PlayerMovement (dodge/combat) was removed. Add a separate dodge\n" +
            "  script if needed — it no longer conflicts with ThirdPersonController.\n" +
            "• PlayerStats and PlayerCombat are untouched.\n" +
            "• Run 'Setup UMA in Game Scene' first if UMAAvatar child is missing.";

        Debug.Log("[SetupStarterAssetsPlayer]\n" + summary);
        EditorUtility.DisplayDialog("Setup Starter Assets Player — Done",
            summary + "\n\nSave the scene (Ctrl+S) then Play.",
            "OK — Save Scene");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void RemoveComponent<T>(GameObject go, List<string> results, string label)
        where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
        {
            Undo.DestroyObjectImmediate(c);
            results.Add($"✓ {label} removed");
        }
    }

#if ENABLE_INPUT_SYSTEM
    static void SetupPlayerInput(GameObject player, List<string> results, List<string> warnings)
    {
        // Find the StarterAssets input action asset.
        string[] guids = AssetDatabase.FindAssets("StarterAssets t:InputActionAsset");
        InputActionAsset inputActions = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("StarterAssets") && !p.Contains("Mobile"))
            {
                inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p);
                if (inputActions != null) break;
            }
        }

        if (inputActions == null)
        {
            warnings.Add("⚠ StarterAssets.inputactions not found — PlayerInput not configured.\n" +
                          "  Assign the StarterAssets InputActionAsset manually.");
        }

        var pi = player.GetComponent<PlayerInput>();
        bool wasNew = pi == null;
        if (wasNew) pi = Undo.AddComponent<PlayerInput>(player);

        if (inputActions != null)
        {
            pi.actions = inputActions;
            results.Add($"✓ PlayerInput {(wasNew ? "added" : "updated")} with '{inputActions.name}'");
        }
        else if (wasNew)
        {
            results.Add("✓ PlayerInput added (actions not assigned — assign manually)");
        }

        pi.notificationBehavior = PlayerNotifications.SendMessages;
        pi.defaultActionMap     = "Player";
        EditorUtility.SetDirty(pi);
    }
#endif

    static Transform EnsureCameraTarget(GameObject player, List<string> results)
    {
        // Prefer a child specifically named for StarterAssets.
        var existing = player.transform.Find("CinemachineCameraTarget");
        if (existing != null)
        {
            results.Add("ℹ CinemachineCameraTarget child already exists");
            return existing;
        }

        var go = new GameObject("CinemachineCameraTarget");
        Undo.RegisterCreatedObjectUndo(go, "Create CinemachineCameraTarget");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = new Vector3(0f, CameraTargetHeight, 0f);
        go.transform.localRotation = Quaternion.identity;
        results.Add($"✓ CinemachineCameraTarget child created at local Y={CameraTargetHeight}");
        return go.transform;
    }

    static void AssignAnimatorToDCA(GameObject player, List<string> results, List<string> warnings)
    {
        // Find the StarterAssetsThirdPerson controller.
        string[] guids = AssetDatabase.FindAssets("StarterAssetsThirdPerson t:AnimatorController");
        if (guids.Length == 0)
        {
            warnings.Add("⚠ StarterAssetsThirdPerson.controller not found in project.");
            return;
        }

        RuntimeAnimatorController controller = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("StarterAssets"))
            {
                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
                if (controller != null) break;
            }
        }

        if (controller == null)
        {
            warnings.Add("⚠ Could not load StarterAssetsThirdPerson.controller.");
            return;
        }

        // Find DCA anywhere under (or on) the player.
        var dca = player.GetComponentInChildren<DynamicCharacterAvatar>();
        if (dca == null)
            dca = Object.FindAnyObjectByType<DynamicCharacterAvatar>();

        if (dca == null)
        {
            warnings.Add("⚠ No DynamicCharacterAvatar found. Run 'Setup UMA in Game Scene' first.");
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
        results.Add($"✓ '{controller.name}' assigned to DCA for HumanMale + HumanFemale");
    }

    static void SetupCinemachineCamera(Transform camTarget, List<string> results, List<string> warnings)
    {
        if (camTarget == null)
        {
            warnings.Add("⚠ CinemachineCameraTarget is null — Cinemachine camera not configured.");
            return;
        }

        // Check if a CinemachineVirtualCamera already exists in the scene.
        var existing = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (existing != null)
        {
            // Reconfigure the existing one.
            Undo.RecordObject(existing, "Reconfigure CinemachineVirtualCamera");
            existing.Follow = camTarget;
            existing.LookAt = camTarget;
            EditorUtility.SetDirty(existing);
            results.Add($"✓ Existing CinemachineVirtualCamera '{existing.name}' " +
                        $"Follow + LookAt set to '{camTarget.name}'");
            return;
        }

        // No virtual camera — try to instantiate the Starter Assets PlayerFollowCamera prefab.
        string[] prefabGuids = AssetDatabase.FindAssets("PlayerFollowCamera t:Prefab");
        GameObject prefab = null;
        foreach (var g in prefabGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("StarterAssets"))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null) break;
            }
        }

        if (prefab == null)
        {
            warnings.Add("⚠ PlayerFollowCamera prefab not found. Add a Cinemachine " +
                         "VirtualCamera to the scene manually and set Follow + LookAt to " +
                         "'CinemachineCameraTarget'.");
            return;
        }

        var camInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(camInstance, "Instantiate PlayerFollowCamera");

        var vcam = camInstance.GetComponent<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = camTarget;
            vcam.LookAt = camTarget;
            EditorUtility.SetDirty(vcam);
            results.Add($"✓ PlayerFollowCamera instantiated, Follow + LookAt → '{camTarget.name}'");
        }
        else
        {
            results.Add("✓ PlayerFollowCamera instantiated (set Follow + LookAt manually)");
        }

        // Ensure a camera tagged MainCamera exists (needed by UMAThirdPersonController).
        var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCam == null)
        {
            // Instantiate the Starter Assets MainCamera prefab.
            string[] camGuids = AssetDatabase.FindAssets("MainCamera t:Prefab");
            foreach (var g in camGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("StarterAssets"))
                {
                    var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (camPrefab != null)
                    {
                        var camInst = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab);
                        Undo.RegisterCreatedObjectUndo(camInst, "Instantiate MainCamera");
                        results.Add($"✓ Starter Assets MainCamera instantiated (tagged 'MainCamera')");
                        break;
                    }
                }
            }
            if (GameObject.FindGameObjectWithTag("MainCamera") == null)
                warnings.Add("⚠ No camera tagged 'MainCamera' in scene — add one or tag your existing camera.");
        }
        else
        {
            results.Add($"ℹ MainCamera '{mainCam.name}' already in scene");
        }
    }
}
