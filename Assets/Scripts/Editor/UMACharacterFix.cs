using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Fix UMA Character (T-pose / Floor / Duplicates)
///
/// Fixes three issues in one pass:
///   1. T-pose  — assigns Locomotion.controller to the DCA so UMA uses a
///                proper idle/locomotion animation set instead of the bind pose.
///   2. Floor clip — positions the CharacterHolder / DCA so the avatar stands
///                on top of the floor rather than clipping through it.
///   3. Duplicates — removes the old capsule MeshRenderer/MeshFilter from the
///                Player root (which creates a second visible "character") and
///                destroys any stray duplicate UMAAvatar or CharacterHolder objects.
/// </summary>
public static class UMACharacterFix
{
    // Path to the UMA example locomotion controller (works for HumanMale/Female).
    const string LocomotionControllerPath =
        "Assets/UMA/Content/Example/Animators/Locomotion.controller";

    // The CharacterController on the Player has height=1.8, center=(0,0,0).
    // The avatar child needs its feet at the capsule bottom: -(height/2) = -0.9.
    const float AvatarFeetOffset = -0.9f;

    // Safe spawn height for the Player root (capsule midpoint, Y=0 floor).
    // When standing on the floor (Y=0), Player.y = height/2 = 0.9.
    const float SafePlayerY = 0.9f;

    [MenuItem("Tools/Fix UMA Character (T-pose, Floor, Duplicates)")]
    public static void Run()
    {
        var sb    = new StringBuilder();
        var fixes = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║   UMA CHARACTER FIX — T-POSE / FLOOR     ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        Fix1_AssignAnimator(sb, fixes);
        Fix2_FixFloorClipping(sb, fixes);
        Fix3_RemoveDuplicates(sb, fixes);

        sb.AppendLine("\n╔══════════════════════════════════════════╗");
        sb.AppendLine($"║  FIXES APPLIED: {fixes.Count,-26}║");
        sb.AppendLine("╚══════════════════════════════════════════╝");

        if (fixes.Count > 0)
        {
            sb.AppendLine("\nChanges:");
            foreach (var f in fixes) sb.AppendLine("  • " + f);
        }
        else
        {
            sb.AppendLine("\n  ✓ Nothing needed fixing.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string full = sb.ToString();
        Debug.Log(full);

        string dialog = full.Length > 2800
            ? full.Substring(0, 2800) + "\n\n[… full report in Console]"
            : full;

        EditorUtility.DisplayDialog("UMA Character Fix", dialog, "OK — Save Scene (Ctrl+S)");
    }

    // ── Fix 1: T-pose → assign Locomotion animator ──────────────────────────────

    static void Fix1_AssignAnimator(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── FIX 1 · Animator Controller (T-pose) ──────────────");

        // Load the Locomotion controller asset.
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                             LocomotionControllerPath);

        if (controller == null)
        {
            // Fallback: search by name.
            string[] guids = AssetDatabase.FindAssets("Locomotion t:AnimatorController");
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                // Prefer the one without "Head" to keep it simple.
                if (System.IO.Path.GetFileNameWithoutExtension(p) == "Locomotion")
                {
                    controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
                    break;
                }
            }
        }

        if (controller == null)
        {
            sb.AppendLine("  ⚠  Could not find Locomotion.controller in UMA/Content/Example/Animators/");
            sb.AppendLine("     Searching all project animators…");
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
            if (guids.Length > 0)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
                sb.AppendLine($"  ℹ  Using fallback: {p}");
            }
        }

        if (controller == null)
        {
            sb.AppendLine("  ✗ No animator controller found anywhere — T-pose cannot be fixed.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"  ✓ Animator controller: '{controller.name}'");

        // Find all DCAs in the scene and assign the controller.
        var allDCAs = GameObject.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsSortMode.None);
        if (allDCAs.Length == 0)
        {
            sb.AppendLine("  ⚠  No DynamicCharacterAvatar found in scene.");
            sb.AppendLine();
            return;
        }

        foreach (var dca in allDCAs)
        {
            bool changed = false;

            // Set default animation controller (used when no race-specific one is set).
            if (dca.raceAnimationControllers.defaultAnimationController != controller)
            {
                dca.raceAnimationControllers.defaultAnimationController = controller;
                changed = true;
            }

            // Also add/update a race-specific entry for HumanMale and HumanFemale.
            foreach (var race in new[] { "HumanMale", "HumanFemale" })
            {
                bool found = false;
                foreach (var ra in dca.raceAnimationControllers.animators)
                {
                    if (ra.raceName == race)
                    {
                        if (ra.animatorController != controller)
                        {
                            ra.animatorController = controller;
                            ra.animatorControllerName = controller.name;
                            changed = true;
                        }
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    dca.raceAnimationControllers.animators.Add(
                        new DynamicCharacterAvatar.RaceAnimator
                        {
                            raceName = race,
                            animatorController = controller,
                            animatorControllerName = controller.name
                        });
                    changed = true;
                }
            }

            // If the avatar already has an Animator built (in-editor preview),
            // assign the controller directly so the change is immediate.
            var animator = dca.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
                sb.AppendLine($"  ✔ Assigned controller directly to existing Animator on '{dca.name}'");
            }

            if (changed)
            {
                EditorUtility.SetDirty(dca.gameObject);
                sb.AppendLine($"  ✔ Assigned '{controller.name}' to DCA '{dca.gameObject.name}'");
                fixes.Add($"Assigned '{controller.name}' animator to '{dca.gameObject.name}'");
            }
            else
            {
                sb.AppendLine($"  ✓ DCA '{dca.gameObject.name}' already had correct animator.");
            }
        }

        sb.AppendLine();
    }

    // ── Fix 2: Floor clipping ────────────────────────────────────────────────────

    static void Fix2_FixFloorClipping(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── FIX 2 · Floor Clipping ────────────────────────────");

        // ── 2a: Fix Player root Y ────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            float minY = cc != null
                ? (cc.height * 0.5f - cc.center.y)   // capsule half-height above pivot
                : SafePlayerY;

            Vector3 pos = player.transform.position;
            if (pos.y < minY - 0.01f)
            {
                float oldY = pos.y;
                pos.y = minY;
                player.transform.position = pos;
                EditorUtility.SetDirty(player);
                sb.AppendLine($"  ✔ Player Y: {oldY:F3} → {pos.y:F3} " +
                              $"(capsule bottom now at floor level)");
                fixes.Add($"Moved Player from Y={oldY:F2} to Y={pos.y:F2}");
            }
            else
            {
                sb.AppendLine($"  ✓ Player Y={pos.y:F3} — capsule bottom at " +
                              $"Y={(pos.y - minY):F3} (OK)");
            }
        }

        // ── 2b: Fix CharacterHolder / DCA position ──────────────────────────────
        // If CharacterHolder is separate from Player, its Y must also be >= 0.
        var holder = GameObject.Find("CharacterHolder");
        if (holder != null)
        {
            Vector3 hp = holder.transform.position;
            sb.AppendLine($"  CharacterHolder world Y = {hp.y:F3}");

            if (hp.y < 0f)
            {
                float old = hp.y;
                hp.y = 0f;
                holder.transform.position = hp;
                EditorUtility.SetDirty(holder);
                sb.AppendLine($"  ✔ CharacterHolder Y: {old:F3} → 0");
                fixes.Add($"Moved CharacterHolder from Y={old:F2} to Y=0");
            }
        }

        // ── 2c: Fix UMAAvatar / DCA child offset ────────────────────────────────
        // If the DCA is a direct child of Player, its local Y should be AvatarFeetOffset.
        // If it's standalone (CharacterHolder branch), its world Y should be >= 0.
        var allDCAs = GameObject.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsSortMode.None);
        foreach (var dca in allDCAs)
        {
            var dcaGO = dca.gameObject;
            bool isChildOfPlayer = player != null &&
                                   IsChildOf(dcaGO.transform, player.transform);

            if (isChildOfPlayer)
            {
                // Should sit at feet-offset relative to Player.
                Vector3 lp = dcaGO.transform.localPosition;
                if (Mathf.Abs(lp.y - AvatarFeetOffset) > 0.01f)
                {
                    float old = lp.y;
                    lp.y = AvatarFeetOffset;
                    dcaGO.transform.localPosition = lp;
                    EditorUtility.SetDirty(dcaGO);
                    sb.AppendLine($"  ✔ '{dcaGO.name}' local Y: {old:F3} → {AvatarFeetOffset} (feet at capsule bottom)");
                    fixes.Add($"'{dcaGO.name}' local Y → {AvatarFeetOffset}");
                }
                else
                {
                    sb.AppendLine($"  ✓ '{dcaGO.name}' local Y = {lp.y:F3} (correct)");
                }
            }
            else
            {
                // Standalone DCA — world Y should be >= 0 (feet on floor).
                float wy = dcaGO.transform.position.y;
                if (wy < 0f)
                {
                    Vector3 wp = dcaGO.transform.position;
                    float old = wp.y;
                    wp.y = 0f;
                    dcaGO.transform.position = wp;
                    EditorUtility.SetDirty(dcaGO);
                    sb.AppendLine($"  ✔ '{dcaGO.name}' world Y: {old:F3} → 0");
                    fixes.Add($"'{dcaGO.name}' world Y → 0");
                }
                else
                {
                    sb.AppendLine($"  ✓ '{dcaGO.name}' world Y = {wy:F3} (OK)");
                }
            }
        }

        sb.AppendLine();
    }

    // ── Fix 3: Remove duplicates ─────────────────────────────────────────────────

    static void Fix3_RemoveDuplicates(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── FIX 3 · Duplicate Characters ──────────────────────");

        // ── 3a: Remove capsule mesh from Player (it's a duplicate visual) ────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var mr = player.GetComponent<MeshRenderer>();
            var mf = player.GetComponent<MeshFilter>();

            if (mr != null)
            {
                // Check if the mesh is a capsule primitive.
                bool isCapsule = mf != null && mf.sharedMesh != null &&
                                 mf.sharedMesh.name.ToLower().Contains("capsule");
                string reason = isCapsule ? "capsule primitive" : "MeshRenderer";

                Object.DestroyImmediate(mr);
                EditorUtility.SetDirty(player);
                sb.AppendLine($"  ✔ Removed MeshRenderer ({reason}) from Player");
                fixes.Add($"Removed MeshRenderer from Player ({reason})");
            }
            if (mf != null)
            {
                Object.DestroyImmediate(mf);
                EditorUtility.SetDirty(player);
                sb.AppendLine("  ✔ Removed MeshFilter from Player");
                fixes.Add("Removed MeshFilter from Player");
            }
            if (mr == null && mf == null)
                sb.AppendLine("  ✓ Player has no capsule mesh (already clean)");
        }

        // ── 3b: Check for multiple CharacterHolder objects ────────────────────────
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var holders = new List<GameObject>();
        var umaAvatars = new List<GameObject>();

        foreach (var go in allObjects)
        {
            if (go.name == "CharacterHolder") holders.Add(go);
            if (go.name == "UMAAvatar" || go.name == "UMADynamicCharacterAvatar") umaAvatars.Add(go);
        }

        sb.AppendLine($"\n  CharacterHolder objects found : {holders.Count}");
        sb.AppendLine($"  UMAAvatar-named objects found : {umaAvatars.Count}");

        if (holders.Count > 1)
        {
            // Keep the first, destroy the rest.
            sb.AppendLine($"  ⚠  {holders.Count} CharacterHolder objects — removing duplicates.");
            for (int i = 1; i < holders.Count; i++)
            {
                string p = GetPath(holders[i]);
                Object.DestroyImmediate(holders[i]);
                sb.AppendLine($"  ✔ Destroyed duplicate CharacterHolder at '{p}'");
                fixes.Add($"Destroyed duplicate CharacterHolder '{p}'");
            }
        }

        // ── 3c: Check for multiple DCAs ───────────────────────────────────────────
        var allDCAs = GameObject.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsSortMode.None);
        sb.AppendLine($"  DynamicCharacterAvatar count  : {allDCAs.Length}");

        if (allDCAs.Length > 1)
        {
            sb.AppendLine($"  ⚠  {allDCAs.Length} DCAs found — investigating…");

            // If one is a child of Player and another is standalone, keep the standalone
            // (it's the intended UMADynamicCharacterAvatar from CharacterHolder).
            // If two are both standalone, keep the first and destroy the rest.
            var toDestroy = new List<DynamicCharacterAvatar>();

            // Find the "main" DCA: prefer one named "UMADynamicCharacterAvatar" or child of CharacterHolder.
            DynamicCharacterAvatar main = null;
            foreach (var dca in allDCAs)
            {
                if (dca.gameObject.name == "UMADynamicCharacterAvatar") { main = dca; break; }
            }
            if (main == null) main = allDCAs[0];

            foreach (var dca in allDCAs)
            {
                if (dca == main) continue;
                toDestroy.Add(dca);
            }

            foreach (var dca in toDestroy)
            {
                string p = GetPath(dca.gameObject);
                sb.AppendLine($"  ✔ Destroyed duplicate DCA at '{p}'");
                fixes.Add($"Destroyed duplicate DCA '{p}'");
                Object.DestroyImmediate(dca.gameObject);
            }
        }
        else if (allDCAs.Length == 1)
        {
            sb.AppendLine($"  ✓ Single DCA — no duplicates.");
        }
        else
        {
            sb.AppendLine("  ⚠  No DCA found in scene — run 'Tools > Setup UMA Player' first.");
        }

        sb.AppendLine();
    }

    // ── Utilities ────────────────────────────────────────────────────────────────

    static bool IsChildOf(Transform child, Transform parent)
    {
        var t = child.parent;
        while (t != null) { if (t == parent) return true; t = t.parent; }
        return false;
    }

    static string GetPath(GameObject go)
    {
        if (go == null) return "(null)";
        string p = go.name;
        var t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }
}
