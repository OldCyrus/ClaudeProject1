using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Add Female Default Wardrobe
///
/// Adds HumanFemale-compatible wardrobe recipes to the DCA's preloadWardrobeRecipes
/// WITHOUT touching any existing HumanMale entries.
///
/// Because each female recipe has compatibleRaces: [HumanFemale], UMA will only
/// apply them when the active race is HumanFemale.  The male entries remain active
/// for HumanMale.  A single DCA can therefore serve both genders correctly.
/// </summary>
public static class AddFemaleWardrobe
{
    // The recipes to add for HumanFemale.  All of these are in
    // Assets/UMA/Content/Example/HumanFemale/Recipes/WardrobeRecipes/HumanFemale/
    // and already have compatibleRaces = [HumanFemale] baked in.
    static readonly string[] FemaleOutfit =
    {
        "FemaleDefaultUnderwear",   // slot: Underwear
        "FemaleShirt1",             // slot: Chest
        "FemalePants1",             // slot: Legs
        "FemaleHair1",              // slot: Hair
    };

    [MenuItem("Tools/Add Female Default Wardrobe")]
    public static void Run()
    {
        var sb    = new StringBuilder();
        var fixes = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║   ADD FEMALE DEFAULT WARDROBE REPORT     ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        var allDCAs = GameObject.FindObjectsByType<DynamicCharacterAvatar>(
                          FindObjectsSortMode.None);

        if (allDCAs.Length == 0)
        {
            sb.AppendLine("⚠  No DynamicCharacterAvatar found in the current scene.");
            ShowResult(sb.ToString(), fixes);
            return;
        }

        foreach (var dca in allDCAs)
        {
            sb.AppendLine($"── DCA: '{dca.gameObject.name}' ──────────────────────────");
            AddFemaleEntries(dca, sb, fixes);
            EditorUtility.SetDirty(dca.gameObject);
            sb.AppendLine();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        ShowResult(sb.ToString(), fixes);
    }

    static void AddFemaleEntries(DynamicCharacterAvatar dca,
                                 StringBuilder sb, List<string> fixes)
    {
        // Collect names already in the preload list so we don't add duplicates.
        var existingNames = new HashSet<string>();
        foreach (var item in dca.preloadWardrobeRecipes.recipes)
            if (item._recipe != null)
                existingNames.Add(item._recipe.name);

        sb.AppendLine($"  Existing preload entries: {existingNames.Count}");

        int added = 0;
        foreach (var recipeName in FemaleOutfit)
        {
            if (existingNames.Contains(recipeName))
            {
                sb.AppendLine($"  ⏭  '{recipeName}' already present — skipped.");
                continue;
            }

            var recipe = FindRecipe(recipeName);
            if (recipe == null)
            {
                sb.AppendLine($"  ⚠  '{recipeName}' not found in project — skipped.");
                continue;
            }

            // Verify compatibleRaces includes HumanFemale.
            bool femaleCompatible = recipe.compatibleRaces != null &&
                                    recipe.compatibleRaces.Contains("HumanFemale");
            if (!femaleCompatible)
            {
                // Safe to add — UMA will still apply it for HumanFemale when
                // compatibleRaces is empty (applies to all races).
                sb.AppendLine($"  ℹ  '{recipeName}' has no race restriction — will apply to all races.");
            }

            var item = new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe);
            item._enabledInDefaultWardrobe = true;
            dca.preloadWardrobeRecipes.recipes.Add(item);

            string slot = (recipe is UMAWardrobeRecipe wr) ? wr.wardrobeSlot : "?";
            sb.AppendLine($"  ✔  Added '{recipe.name}'  (slot: {slot}, races: " +
                          $"{string.Join(", ", recipe.compatibleRaces)})");
            fixes.Add($"Added '{recipe.name}' to '{dca.gameObject.name}'");
            added++;
        }

        // Ensure loadDefaultRecipes stays true so the list is honoured at startup.
        dca.preloadWardrobeRecipes.loadDefaultRecipes = true;

        sb.AppendLine($"\n  Female entries added: {added}/{FemaleOutfit.Length}");
        sb.AppendLine("  HumanMale entries are unchanged.");
    }

    static UMATextRecipe FindRecipe(string name)
    {
        foreach (var typeSuffix in new[] { "t:UMAWardrobeRecipe", "t:UMATextRecipe" })
        {
            string[] guids = AssetDatabase.FindAssets(name + " " + typeSuffix);
            if (guids.Length == 0) continue;

            foreach (var g in guids)
            {
                string path  = AssetDatabase.GUIDToAssetPath(g);
                string fname = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fname == name)
                    return AssetDatabase.LoadAssetAtPath<UMATextRecipe>(path);
            }
            return AssetDatabase.LoadAssetAtPath<UMATextRecipe>(
                       AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    static void ShowResult(string full, List<string> fixes)
    {
        var sb = new StringBuilder(full);
        sb.AppendLine($"\n  Total entries added: {fixes.Count}");
        Debug.Log(sb.ToString());

        string dialog = sb.Length > 2800
            ? sb.ToString().Substring(0, 2800) + "\n\n[… see Console for full log]"
            : sb.ToString();

        EditorUtility.DisplayDialog("Add Female Default Wardrobe", dialog,
                                    "OK — Save Scene (Ctrl+S) then Play");
    }
}
