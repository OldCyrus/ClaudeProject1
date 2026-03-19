using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Fix UMA Player Setup
///
/// Fixes:
///   1. Removes UMABoneVisualizer components (debug-only, causes MissingReferenceException
///      because rootNode is null until the avatar skeleton is built).
///   2. Validates / repairs the DynamicCharacterAvatar on UMADynamicCharacterAvatar or
///      any child of Player named UMAAvatar.
///   3. Ensures UMAPlayerLink on Player is wired to the correct DCA child.
///   4. Verifies PlayerMovement, PlayerCombat, PlayerInteraction are on the Player root.
///   5. Prints a full state report.
/// </summary>
public static class UMAPlayerFixup
{
    [MenuItem("Tools/Fix UMA Player Setup")]
    public static void Run()
    {
        var sb     = new StringBuilder();
        var fixes  = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║       UMA PLAYER FIXUP REPORT            ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        // ── 1. Remove all UMABoneVisualizer components ──────────────────────────
        Fix1_RemoveBoneVisualizer(sb, fixes);

        // ── 2. Find and validate the DynamicCharacterAvatar ─────────────────────
        var dca = Fix2_ValidateDCA(sb, fixes);

        // ── 3. Wire UMAPlayerLink on Player ──────────────────────────────────────
        Fix3_WirePlayerLink(sb, fixes, dca);

        // ── 4. Report player script status ──────────────────────────────────────
        Fix4_ReportPlayerScripts(sb);

        // ── Footer ───────────────────────────────────────────────────────────────
        sb.AppendLine("\n╔══════════════════════════════════════════╗");
        sb.AppendLine($"║  TOTAL FIXES APPLIED: {fixes.Count,-20}║");
        sb.AppendLine("╚══════════════════════════════════════════╝");

        if (fixes.Count > 0)
        {
            sb.AppendLine("\nChanges made:");
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

        string dialog = full.Length > 2500
            ? full.Substring(0, 2500) + "\n\n[… full report in Console]"
            : full;

        EditorUtility.DisplayDialog("UMA Player Fixup", dialog, "OK — Save Scene (Ctrl+S)");
    }

    // ── Fix 1: UMABoneVisualizer ─────────────────────────────────────────────────

    static void Fix1_RemoveBoneVisualizer(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── FIX 1 · UMABoneVisualizer ─────────────────────────");

        // Search by type name so we don't need a hard assembly reference.
        int removed = 0;
        var allObjects = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allObjects)
        {
            if (mb == null) continue;
            if (mb.GetType().Name == "UMABoneVisualizer")
            {
                var go    = mb.gameObject;
                string goName = go.name;
                Object.DestroyImmediate(mb);
                EditorUtility.SetDirty(go);
                sb.AppendLine($"  ✔ Removed UMABoneVisualizer from '{goName}'");
                fixes.Add($"Removed UMABoneVisualizer from '{goName}'");
                removed++;
            }
        }

        if (removed == 0)
            sb.AppendLine("  ✓ No UMABoneVisualizer found in scene.");

        sb.AppendLine();
    }

    // ── Fix 2: DynamicCharacterAvatar validation ──────────────────────────────────

    static DynamicCharacterAvatar Fix2_ValidateDCA(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── FIX 2 · DynamicCharacterAvatar ────────────────────");

        // Look in several places:
        //   a) A child named "UMAAvatar" under the Player
        //   b) A GameObject named "UMADynamicCharacterAvatar" anywhere
        //   c) Any DCA in the scene
        DynamicCharacterAvatar dca = null;
        GameObject dcaGO = null;

        var player = GameObject.FindGameObjectWithTag("Player");

        // a) Under Player
        if (player != null)
        {
            var child = player.transform.Find("UMAAvatar");
            if (child != null) dca = child.GetComponent<DynamicCharacterAvatar>();
        }

        // b) Named "UMADynamicCharacterAvatar"
        if (dca == null)
        {
            var namedGO = GameObject.Find("UMADynamicCharacterAvatar");
            if (namedGO != null) dca = namedGO.GetComponent<DynamicCharacterAvatar>();
        }

        // c) Any DCA anywhere
        if (dca == null)
            dca = GameObject.FindAnyObjectByType<DynamicCharacterAvatar>();

        if (dca == null)
        {
            sb.AppendLine("  ⚠  No DynamicCharacterAvatar found in scene.");
            sb.AppendLine("     → Run 'Tools > Setup UMA Player' first.");
            sb.AppendLine();
            return null;
        }

        dcaGO = dca.gameObject;
        sb.AppendLine($"  ✓ Found DCA on '{dcaGO.name}'  (path: {GetPath(dcaGO)})");

        // Validate race
        bool raceSet = !string.IsNullOrEmpty(dca.activeRace.name) &&
                       dca.activeRace.name != "None Set";
        if (raceSet)
        {
            sb.AppendLine($"  ✓ Race: '{dca.activeRace.name}'");
        }
        else
        {
            dca.activeRace.name = "HumanMale";
            EditorUtility.SetDirty(dcaGO);
            sb.AppendLine("  ✔ Race was empty → set to 'HumanMale'");
            fixes.Add("Set DCA race to HumanMale");
        }

        // Validate preload wardrobe
        int recipes = dca.preloadWardrobeRecipes.recipes.Count;
        if (recipes == 0)
        {
            sb.AppendLine("  ⚠  No preload wardrobe recipes — character may appear naked.");
            sb.AppendLine("     Adding default male outfit…");
            AddDefaultWardrobe(dca, sb, fixes);
        }
        else
        {
            sb.AppendLine($"  ✓ Preload wardrobe: {recipes} recipe(s)");
            foreach (var r in dca.preloadWardrobeRecipes.recipes)
                sb.AppendLine($"    - {r._recipeName}");
        }

        // Make sure it's not inside a CharacterHolder that's blocking builds
        // (CharacterHolder is sometimes used as a scene-management wrapper).
        var charHolder = dcaGO.transform.parent;
        if (charHolder != null && charHolder.name.Contains("CharacterHolder"))
        {
            sb.AppendLine($"  ℹ  DCA is a child of '{charHolder.name}'.");
            sb.AppendLine("     This is fine as long as UMA_GLIB is also in the scene.");
        }

        // Verify UMA context (UMA_GLIB / UMAContext) exists
        var context = UMAContextBase.Instance;
        if (context == null)
        {
            sb.AppendLine("  ⚠  No UMAContext found in scene (need UMA_GLIB prefab).");
            sb.AppendLine("     Character will NOT build at runtime without it.");
        }
        else
        {
            sb.AppendLine($"  ✓ UMAContext found: '{context.gameObject.name}'");
        }

        sb.AppendLine();
        return dca;
    }

    // ── Fix 3: UMAPlayerLink ───────────────────────────────────────────────────────

    static void Fix3_WirePlayerLink(StringBuilder sb, List<string> fixes,
                                     DynamicCharacterAvatar dca)
    {
        sb.AppendLine("── FIX 3 · UMAPlayerLink ─────────────────────────────");

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            sb.AppendLine("  ⚠  No GameObject tagged 'Player'.");
            sb.AppendLine();
            return;
        }

        // Ensure UMAPlayerLink is on the Player.
        var link = player.GetComponent<UMAPlayerLink>();
        if (link == null)
        {
            link = player.AddComponent<UMAPlayerLink>();
            EditorUtility.SetDirty(player);
            sb.AppendLine("  ✔ Added UMAPlayerLink to Player.");
            fixes.Add("Added UMAPlayerLink to Player");
        }
        else
        {
            sb.AppendLine("  ✓ UMAPlayerLink already present.");
        }

        // Wire DCA reference.
        if (dca != null && link.avatar != dca)
        {
            link.avatar = dca;
            EditorUtility.SetDirty(player);
            sb.AppendLine($"  ✔ Wired DCA '{dca.gameObject.name}' → UMAPlayerLink.avatar");
            fixes.Add("Wired DCA to UMAPlayerLink.avatar");
        }
        else if (dca != null)
        {
            sb.AppendLine($"  ✓ UMAPlayerLink.avatar already wired to '{dca.gameObject.name}'");
        }

        // Wire CameraTarget.
        var ct = player.transform.Find("CameraTarget");
        if (ct != null && link.cameraTarget != ct)
        {
            link.cameraTarget = ct;
            EditorUtility.SetDirty(player);
            sb.AppendLine("  ✔ Wired CameraTarget → UMAPlayerLink.cameraTarget");
            fixes.Add("Wired CameraTarget to UMAPlayerLink");
        }
        else if (ct != null)
        {
            sb.AppendLine("  ✓ UMAPlayerLink.cameraTarget already wired.");
        }
        else
        {
            sb.AppendLine("  ⚠  CameraTarget child not found on Player.");
        }

        // If the DCA is not a child of Player, check if it needs to be moved
        // or if the Player just needs a reference.
        if (dca != null && !IsChildOf(dca.transform, player.transform))
        {
            sb.AppendLine($"\n  ℹ  DCA '{dca.gameObject.name}' is NOT a child of Player.");
            sb.AppendLine("     The CharacterController on Player handles collision.");
            sb.AppendLine("     UMAPlayerLink will still sync the avatar via the event.");
        }

        sb.AppendLine();
    }

    // ── Fix 4: Player scripts report ─────────────────────────────────────────────

    static void Fix4_ReportPlayerScripts(StringBuilder sb)
    {
        sb.AppendLine("── FIX 4 · Player Script Status ──────────────────────");

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            sb.AppendLine("  ⚠  No Player found.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"  Player: '{player.name}'  world pos={player.transform.position}");

        Check<CharacterController>(player, sb, "CharacterController");
        Check<PlayerMovement>     (player, sb, "PlayerMovement");
        Check<PlayerCombat>       (player, sb, "PlayerCombat");
        Check<PlayerStats>        (player, sb, "PlayerStats");
        Check<PlayerInteraction>  (player, sb, "PlayerInteraction");
        Check<UMAPlayerLink>      (player, sb, "UMAPlayerLink");
        Check<MeshRenderer>       (player, sb, "MeshRenderer (should be REMOVED for UMA)",
                                   warnIfPresent: true);
        Check<MeshFilter>         (player, sb, "MeshFilter (should be REMOVED for UMA)",
                                   warnIfPresent: true);

        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            sb.AppendLine($"\n  CC details: height={cc.height} radius={cc.radius} " +
                          $"center={cc.center} stepOffset={cc.stepOffset}");
            float capsuleBottom = player.transform.position.y + cc.center.y - cc.height * 0.5f;
            sb.AppendLine($"  Capsule bottom at world Y={capsuleBottom:F2}");
        }

        // DCA status
        var dca = player.GetComponentInChildren<DynamicCharacterAvatar>();
        if (dca != null)
        {
            sb.AppendLine($"\n  DCA child '{dca.gameObject.name}':");
            sb.AppendLine($"    local pos  = {dca.transform.localPosition}");
            sb.AppendLine($"    race       = {dca.activeRace.name}");
            sb.AppendLine($"    wardrobe   = {dca.preloadWardrobeRecipes.recipes.Count} recipe(s)");
        }
        else
        {
            // DCA may be a sibling / different hierarchy branch.
            var anyDCA = GameObject.FindAnyObjectByType<DynamicCharacterAvatar>();
            if (anyDCA != null)
                sb.AppendLine($"\n  DCA found elsewhere in scene: '{GetPath(anyDCA.gameObject)}'");
            else
                sb.AppendLine("\n  ⚠  No DCA in scene — run 'Tools > Setup UMA Player'");
        }

        sb.AppendLine();
    }

    // ── Wardrobe helper ────────────────────────────────────────────────────────────

    static readonly string[] DefaultRecipes =
    {
        "MaleDefaultUnderwear", "MaleShirt1", "MaleJeans", "MaleHair1"
    };

    static void AddDefaultWardrobe(DynamicCharacterAvatar dca,
                                   StringBuilder sb, List<string> fixes)
    {
        dca.preloadWardrobeRecipes.loadDefaultRecipes = true;
        int loaded = 0;

        foreach (var name in DefaultRecipes)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:UMAWardrobeRecipe");
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets(name + " t:UMATextRecipe");

            if (guids.Length == 0) { sb.AppendLine($"  ⚠  Recipe not found: {name}"); continue; }

            // Pick exact name match.
            string chosen = guids[0];
            foreach (var g in guids)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GUIDToAssetPath(g)) == name)
                { chosen = g; break; }
            }

            var recipe = AssetDatabase.LoadAssetAtPath<UMATextRecipe>(
                             AssetDatabase.GUIDToAssetPath(chosen));
            if (recipe == null) continue;

            dca.preloadWardrobeRecipes.recipes.Add(
                new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe));
            sb.AppendLine($"  ✔ Added wardrobe: {recipe.name}");
            fixes.Add($"Added wardrobe recipe: {recipe.name}");
            loaded++;
        }

        EditorUtility.SetDirty(dca.gameObject);
        sb.AppendLine($"  Loaded {loaded}/{DefaultRecipes.Length} wardrobe items.");
    }

    // ── Utility ────────────────────────────────────────────────────────────────────

    static void Check<T>(GameObject go, StringBuilder sb, string label,
                         bool warnIfPresent = false) where T : Component
    {
        var c = go.GetComponent<T>();
        if (warnIfPresent)
        {
            if (c != null)
                sb.AppendLine($"  ⚠  {label}: PRESENT (remove for UMA avatar)");
            else
                sb.AppendLine($"  ✓ {label}: not present (correct)");
        }
        else
        {
            sb.AppendLine(c != null
                ? $"  ✓ {label}: present"
                : $"  ⚠  {label}: MISSING");
        }
    }

    static bool IsChildOf(Transform child, Transform parent)
    {
        var t = child.parent;
        while (t != null) { if (t == parent) return true; t = t.parent; }
        return false;
    }

    static string GetPath(GameObject go)
    {
        string p = go.name;
        var t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }
}
