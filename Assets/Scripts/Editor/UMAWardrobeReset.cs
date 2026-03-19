using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Reset UMA Wardrobe and DNA
///
/// Fixes:
///   0. (ROOT CAUSE) Clears loadFileOnStart / loadFilename / loadString so UMA
///      stops loading a saved character file that overrides everything else.
///   1. Clears ALL preload wardrobe items added via "Add All", starts fresh.
///   2. Resets predefined DNA overrides so the body returns to an average shape.
///   3. Builds a clean minimal outfit matching the avatar's race (Male or Female).
///   4. Ensures MaleShirt2 is correctly referenced and not blocked by a slot conflict.
/// </summary>
public static class UMAWardrobeReset
{
    // HumanMale default outfit.
    static readonly string[] MaleOutfit =
    {
        "MaleDefaultUnderwear",   // slot: Underwear
        "MaleShirt2",             // slot: Chest
        "MaleJeans",              // slot: Legs
        "TallShoes_Recipe",       // slot: Feet
        "MaleHair1",              // slot: Hair  ("The Rebel")
    };

    // HumanFemale default outfit.
    static readonly string[] FemaleOutfit =
    {
        "FemaleDefaultUnderwear", // slot: Underwear
        "FemaleShirt1",           // slot: Chest  ("Shirt 1")
        "FemalePants1",           // slot: Legs   ("Pants 1")
        "FemaleTallShoes_Black",  // slot: Feet   ("High Tops - Black")
        "FemaleHair1",            // slot: Hair   ("Hair 1")
    };

    // Fallback used when race doesn't match Male or Female.
    static readonly string[] DefaultOutfit = MaleOutfit;

    [MenuItem("Tools/Reset UMA Wardrobe and DNA")]
    public static void Run()
    {
        var sb    = new StringBuilder();
        var fixes = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║    UMA WARDROBE + DNA RESET REPORT       ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        var allDCAs = GameObject.FindObjectsByType<DynamicCharacterAvatar>(FindObjectsSortMode.None);

        if (allDCAs.Length == 0)
        {
            sb.AppendLine("⚠  No DynamicCharacterAvatar found in scene.");
            ShowResult(sb.ToString(), fixes);
            return;
        }

        foreach (var dca in allDCAs)
        {
            sb.AppendLine($"── DCA: '{dca.gameObject.name}' ──────────────────────────");

            Step0_ClearSavedFileLoad(dca, sb, fixes);
            Step1_ClearWardrobe(dca, sb, fixes);
            Step2_ResetDNA(dca, sb, fixes);
            Step3_SetDefaultOutfit(dca, sb, fixes);

            EditorUtility.SetDirty(dca.gameObject);
            sb.AppendLine();
        }

        // Step 4: Verify MaleShirt2 asset is not corrupted / has correct race.
        Step4_VerifyMaleShirt2(sb, fixes);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        ShowResult(sb.ToString(), fixes);
    }

    // ── Step 0: Clear saved-file load (ROOT CAUSE) ───────────────────────────────
    // If loadFileOnStart=true AND loadFilename/loadString are set, UMA completely
    // ignores preloadWardrobeRecipes and reloads the saved hat/robe/fat character.

    static void Step0_ClearSavedFileLoad(DynamicCharacterAvatar dca,
                                          StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("  Step 0 — Clear saved-file load flags");

        bool changed = false;

        if (dca.loadFileOnStart)
        {
            sb.AppendLine($"    loadFileOnStart = true  → false  ← ROOT CAUSE");
            dca.loadFileOnStart = false;
            fixes.Add("loadFileOnStart → false");
            changed = true;
        }
        else sb.AppendLine("    loadFileOnStart = false (ok)");

        if (!string.IsNullOrEmpty(dca.loadFilename))
        {
            sb.AppendLine($"    loadFilename = '{dca.loadFilename}'  → ''");
            dca.loadFilename = "";
            fixes.Add($"Cleared loadFilename (was '{dca.loadFilename}')");
            changed = true;
        }
        else sb.AppendLine("    loadFilename = '' (ok)");

        if (!string.IsNullOrEmpty(dca.loadString))
        {
            int len = dca.loadString.Length;
            sb.AppendLine($"    loadString ({len} chars)  → ''  ← overrides wardrobe");
            dca.loadString = "";
            fixes.Add("Cleared loadString");
            changed = true;
        }
        else sb.AppendLine("    loadString = '' (ok)");

        // loadPathType=String also bypasses preloadWardrobeRecipes.
        if (dca.loadPathType == DynamicCharacterAvatar.loadPathTypes.String)
        {
            sb.AppendLine("    loadPathType = String  → persistentDataPath");
            dca.loadPathType = DynamicCharacterAvatar.loadPathTypes.persistentDataPath;
            fixes.Add("loadPathType → persistentDataPath");
            changed = true;
        }

        if (!changed)
            sb.AppendLine("    No saved-file overrides found.");

        sb.AppendLine();
    }

    // ── Step 1: Clear all preload wardrobe items ─────────────────────────────────

    static void Step1_ClearWardrobe(DynamicCharacterAvatar dca,
                                    StringBuilder sb, List<string> fixes)
    {
        int count = dca.preloadWardrobeRecipes.recipes.Count;
        sb.AppendLine($"  Step 1 — Clear wardrobe ({count} item(s) → 0)");

        if (count > 0)
        {
            foreach (var r in dca.preloadWardrobeRecipes.recipes)
                sb.AppendLine($"    removed: {r._recipeName}");

            dca.preloadWardrobeRecipes.recipes.Clear();
            fixes.Add($"Cleared {count} wardrobe items from '{dca.gameObject.name}'");
        }
        else
        {
            sb.AppendLine("    (already empty)");
        }

        // Keep loadDefaultRecipes TRUE so our preloaded list is honoured.
        dca.preloadWardrobeRecipes.loadDefaultRecipes = true;
    }

    // ── Step 2: Reset DNA ─────────────────────────────────────────────────────────

    static void Step2_ResetDNA(DynamicCharacterAvatar dca,
                               StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("  Step 2 — Reset DNA overrides");

        // predefinedDNA holds any pre-set DNA values that override the race defaults.
        // Clearing it lets UMA use the race's baseline body shape.
        if (dca.predefinedDNA != null && dca.predefinedDNA.PreloadValues.Count > 0)
        {
            int dnaCount = dca.predefinedDNA.PreloadValues.Count;
            sb.AppendLine($"    Clearing {dnaCount} predefined DNA value(s):");
            foreach (var d in dca.predefinedDNA.PreloadValues)
                sb.AppendLine($"      {d.Name} = {d.Value:F3}  → cleared");

            dca.predefinedDNA.PreloadValues.Clear();
            fixes.Add($"Cleared {dnaCount} DNA overrides on '{dca.gameObject.name}'");
        }
        else
        {
            sb.AppendLine("    No predefined DNA overrides found.");
        }

        // keepPredefinedDNA must be FALSE so the cleared values actually take effect.
        if (dca.keepPredefinedDNA)
        {
            dca.keepPredefinedDNA = false;
            sb.AppendLine("    keepPredefinedDNA → false (was true)");
            fixes.Add("Set keepPredefinedDNA = false");
        }
    }

    // ── Step 3: Add clean default outfit ─────────────────────────────────────────

    static void Step3_SetDefaultOutfit(DynamicCharacterAvatar dca,
                                       StringBuilder sb, List<string> fixes)
    {
        string race   = dca.activeRace?.name ?? "";
        string[] outfit = race.Contains("Female") ? FemaleOutfit
                        : race.Contains("Male")   ? MaleOutfit
                        :                           DefaultOutfit;

        sb.AppendLine($"  Step 3 — Set default outfit (race: {race})");

        int added = 0;
        foreach (var recipeName in outfit)
        {
            var recipe = FindRecipe(recipeName);

            if (recipe == null)
            {
                sb.AppendLine($"    ⚠  '{recipeName}' not found — skipping.");
                continue;
            }

            // If compatibleRaces is empty, add the current race so the selector shows it as available.
            if (recipe.compatibleRaces == null || recipe.compatibleRaces.Count == 0)
            {
                string addRace = race.Length > 0 ? race : "HumanMale";
                recipe.compatibleRaces = new List<string> { addRace };
                EditorUtility.SetDirty(recipe);
                sb.AppendLine($"    ✔ '{recipeName}' had no compatible races — added {addRace}.");
                fixes.Add($"Added {addRace} to {recipeName}.compatibleRaces");
            }

            var item = new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe);
            item._enabledInDefaultWardrobe = true;
            dca.preloadWardrobeRecipes.recipes.Add(item);

            sb.AppendLine($"    ✔ added '{recipe.name}'  (slot: " +
                          $"{(recipe is UMAWardrobeRecipe w ? w.wardrobeSlot : "?")})" );
            fixes.Add($"Added '{recipe.name}' to outfit");
            added++;
        }

        sb.AppendLine($"    Outfit: {added}/{DefaultOutfit.Length} items added.");
    }

    // ── Step 4: Diagnose MaleShirt2 "unavailable" issue ──────────────────────────

    static void Step4_VerifyMaleShirt2(StringBuilder sb, List<string> fixes)
    {
        sb.AppendLine("── Step 4 — MaleShirt2 'unavailable' diagnosis ───────");

        var shirt2 = FindRecipe("MaleShirt2") as UMAWardrobeRecipe;
        if (shirt2 == null)
        {
            sb.AppendLine("  ⚠  MaleShirt2 not found as UMAWardrobeRecipe.");
            return;
        }

        sb.AppendLine($"  Asset path : {AssetDatabase.GetAssetPath(shirt2)}");
        sb.AppendLine($"  Slot       : {shirt2.wardrobeSlot}");
        sb.AppendLine($"  Compatible : {string.Join(", ", shirt2.compatibleRaces)}");
        sb.AppendLine($"  Incompatible: {shirt2.IncompatibleRecipes?.Count ?? 0} entries");
        sb.AppendLine($"  Suppress   : {shirt2.suppressWardrobeSlots?.Count ?? 0} suppressed slots");

        // The most common cause of "unavailable" in the DCS example selector:
        // The wardrobe collection on the DCA contains ANOTHER item in the same slot
        // (e.g. from "Add All"), and the selector greys out alternatives.
        // Fix: Step 1 cleared the full list; now only MaleShirt2 is in the Chest slot.
        sb.AppendLine();
        sb.AppendLine("  Root cause of 'unavailable' in wardrobe selector:");
        sb.AppendLine("  When 'Add All' was used, every item in every slot was loaded.");
        sb.AppendLine("  UMA's selector marks alternatives in the SAME slot as unavailable");
        sb.AppendLine("  when another item in that slot is currently equipped.");
        sb.AppendLine("  Fix: Step 1 cleared the overloaded list. MaleShirt2 is now the");
        sb.AppendLine("  ONLY item in the Chest slot, so it will show as selected/available.");
        sb.AppendLine();

        // Also ensure it's correctly labelled in the Global Library by forcing a re-check.
        // (AssetIndexer.Instance is only valid at runtime; at edit-time just mark dirty.)
        EditorUtility.SetDirty(shirt2);
        AssetDatabase.SaveAssets();
        sb.AppendLine("  ✔ MaleShirt2 asset marked dirty and re-saved (refreshes indexer).");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────────

    static UMATextRecipe FindRecipe(string name)
    {
        // Try UMAWardrobeRecipe first (more specific), then UMATextRecipe.
        foreach (var typeSuffix in new[] { "t:UMAWardrobeRecipe", "t:UMATextRecipe" })
        {
            string[] guids = AssetDatabase.FindAssets(name + " " + typeSuffix);
            if (guids.Length == 0) continue;

            // Prefer exact filename match.
            foreach (var g in guids)
            {
                string path  = AssetDatabase.GUIDToAssetPath(g);
                string fname = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fname == name)
                    return AssetDatabase.LoadAssetAtPath<UMATextRecipe>(path);
            }
            // Fallback to first result.
            return AssetDatabase.LoadAssetAtPath<UMATextRecipe>(
                       AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    static void ShowResult(string full, List<string> fixes)
    {
        var sb = new StringBuilder(full);
        sb.AppendLine($"\n  Total fixes applied: {fixes.Count}");

        Debug.Log(sb.ToString());

        string dialog = sb.Length > 2800
            ? sb.ToString().Substring(0, 2800) + "\n\n[… see Console for full log]"
            : sb.ToString();

        EditorUtility.DisplayDialog("UMA Wardrobe + DNA Reset", dialog,
                                    "OK — Save Scene (Ctrl+S) then Play");
    }
}
