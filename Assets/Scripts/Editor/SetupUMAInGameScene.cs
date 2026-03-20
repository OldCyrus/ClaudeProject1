using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Setup UMA in Game Scene
///
/// Run this with ClaudeMainLevel open.  It:
///   1. Adds UMA_GLIB to the scene (required for UMA to resolve assets at runtime)
///   2. Creates an UMAAvatar child on the Player with DynamicCharacterAvatar
///   3. Adds CharacterLoader (reads the saved recipe from character creation)
///   4. Adds RaceWardrobeController (fallback outfit if no saved recipe)
///   5. Wires UMAPlayerLink
/// </summary>
public static class SetupUMAInGameScene
{
    const float AvatarYOffset  = -0.9f;
    const float CameraTargetY  = 0.7f;

    static readonly string[] MaleOutfit = {
        "MaleDefaultUnderwear", "MaleShirt2", "MaleJeans", "TallShoes_Recipe", "MaleHair1",
    };
    static readonly string[] FemaleOutfit = {
        "FemaleDefaultUnderwear", "FemaleShirt1", "FemalePants1", "FemaleHair1",
    };

    [MenuItem("Tools/Setup UMA in Game Scene")]
    public static void Run()
    {
        var results = new List<string>();
        var warnings = new List<string>();

        // ── 1. UMA_GLIB ────────────────────────────────────────────────────────
        EnsureUMAGLIB(results, warnings);

        // ── 2. Find Player ──────────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup UMA in Game Scene",
                "No GameObject tagged 'Player' found in this scene.\n\n" +
                "Make sure the Player is tagged 'Player' and try again.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup UMA in Game Scene");

        // ── 3. Remove capsule mesh from Player root (keeps scripts) ────────────
        RemoveCapsuleMesh(player, results);

        // ── 4. Create UMAAvatar child with DCA ─────────────────────────────────
        var dca = EnsureAvatarChild(player, results);

        // ── 5. Preload both male and female default wardrobes ──────────────────
        if (dca != null)
            LoadBothGenderOutfits(dca, results, warnings);

        // ── 6. CharacterLoader ─────────────────────────────────────────────────
        var loader = player.GetComponent<CharacterLoader>();
        if (loader == null)
        {
            loader = player.AddComponent<CharacterLoader>();
            results.Add("✓ CharacterLoader added to Player");
        }
        else
        {
            results.Add("ℹ CharacterLoader already present");
        }
        if (dca != null) loader.avatar = dca;

        // ── 7. RaceWardrobeController ──────────────────────────────────────────
        if (dca != null)
        {
            var raceCtrl = dca.GetComponent<RaceWardrobeController>();
            if (raceCtrl == null)
            {
                raceCtrl = dca.gameObject.AddComponent<RaceWardrobeController>();
                raceCtrl.avatar = dca;
                results.Add("✓ RaceWardrobeController added to UMAAvatar");
            }
            else
            {
                results.Add("ℹ RaceWardrobeController already present");
            }
        }

        // ── 8. UMAPlayerLink ───────────────────────────────────────────────────
        var link = player.GetComponent<UMAPlayerLink>();
        if (link == null) link = player.AddComponent<UMAPlayerLink>();
        if (dca != null) link.avatar = dca;
        var ct = player.transform.Find("CameraTarget");
        if (ct != null)
        {
            link.cameraTarget = ct;
            var pos = ct.localPosition;
            pos.y = CameraTargetY;
            ct.localPosition = pos;
            results.Add($"✓ CameraTarget moved to Y={CameraTargetY}");
        }
        results.Add("✓ UMAPlayerLink wired");

        // ── Finalise ────────────────────────────────────────────────────────────
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string summary = string.Join("\n", results);
        if (warnings.Count > 0)
            summary += "\n\nWARNINGS:\n" + string.Join("\n", warnings);

        Debug.Log("[SetupUMAInGameScene]\n" + summary);
        EditorUtility.DisplayDialog("Setup UMA in Game Scene — Done",
            summary + "\n\nSave the scene (Ctrl+S) then Play.",
            "OK — Save Scene");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    static void EnsureUMAGLIB(List<string> results, List<string> warnings)
    {
        // Check if any UMAContext already exists.
        var existing = Object.FindAnyObjectByType<UMAContext>();
        if (existing != null)
        {
            results.Add($"ℹ UMA context already in scene ('{existing.gameObject.name}')");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("UMA_GLIB t:Prefab");
        if (guids.Length == 0)
        {
            warnings.Add("⚠ UMA_GLIB prefab not found — add it manually from UMA/Getting Started/");
            return;
        }

        string path = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == "UMA_GLIB")
            {
                path = p;
                break;
            }
        }
        if (path == null) path = AssetDatabase.GUIDToAssetPath(guids[0]);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            warnings.Add($"⚠ Could not load UMA_GLIB prefab at '{path}'");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Instantiate UMA_GLIB");
        results.Add($"✓ UMA_GLIB instantiated from '{path}'");
    }

    static void RemoveCapsuleMesh(GameObject player, List<string> results)
    {
        var mr = player.GetComponent<MeshRenderer>();
        var mf = player.GetComponent<MeshFilter>();
        if (mr != null) { Object.DestroyImmediate(mr); results.Add("✓ MeshRenderer removed from Player"); }
        if (mf != null) { Object.DestroyImmediate(mf); results.Add("✓ MeshFilter removed from Player"); }
        if (mr == null && mf == null) results.Add("ℹ No capsule mesh on Player — nothing to remove");
    }

    static DynamicCharacterAvatar EnsureAvatarChild(GameObject player, List<string> results)
    {
        var existing = player.transform.Find("UMAAvatar");
        GameObject avatarGO;

        if (existing != null)
        {
            avatarGO = existing.gameObject;
            results.Add("ℹ UMAAvatar child already exists");
        }
        else
        {
            avatarGO = new GameObject("UMAAvatar");
            avatarGO.transform.SetParent(player.transform, false);
            avatarGO.transform.localPosition = new Vector3(0f, AvatarYOffset, 0f);
            avatarGO.transform.localRotation = Quaternion.identity;
            avatarGO.transform.localScale    = Vector3.one;
            results.Add($"✓ UMAAvatar child created at local Y={AvatarYOffset}");
        }

        var dca = avatarGO.GetComponent<DynamicCharacterAvatar>();
        if (dca == null)
        {
            dca = avatarGO.AddComponent<DynamicCharacterAvatar>();
            results.Add("✓ DynamicCharacterAvatar added");
        }

        // Default race — CharacterLoader will override with saved race from recipe.
        dca.activeRace.name = "HumanMale";
        dca.preloadWardrobeRecipes.loadDefaultRecipes = true;
        dca.loadFileOnStart = false;
        dca.loadFilename    = "";
        dca.loadString      = "";

        // Assign animator via raceAnimationControllers.
        AssignAnimator(dca, results);

        EditorUtility.SetDirty(avatarGO);
        return dca;
    }

    static void AssignAnimator(DynamicCharacterAvatar dca, List<string> results)
    {
        // Prefer StarterAssetsThirdPerson.controller if available (has the full
        // Speed/MotionSpeed/Grounded/Jump/FreeFall parameter set).  Fall back to
        // the UMA Locomotion.controller for projects without Starter Assets.
        RuntimeAnimatorController controller = FindController("StarterAssetsThirdPerson", "StarterAssets")
                                            ?? FindController("Locomotion", "UMA");

        if (controller == null)
        {
            results.Add("⚠ No suitable animator controller found (tried StarterAssetsThirdPerson, Locomotion)");
            return;
        }

        dca.raceAnimationControllers.defaultAnimationController = controller;
        var existing = dca.raceAnimationControllers.animators;
        bool hasMale   = false, hasFemale = false;
        foreach (var e in existing)
        {
            if (e.raceName == "HumanMale")   hasMale   = true;
            if (e.raceName == "HumanFemale") hasFemale = true;
        }
        if (!hasMale)
            existing.Add(new DynamicCharacterAvatar.RaceAnimator { raceName = "HumanMale",   animatorController = controller, animatorControllerName = controller.name });
        if (!hasFemale)
            existing.Add(new DynamicCharacterAvatar.RaceAnimator { raceName = "HumanFemale", animatorController = controller, animatorControllerName = controller.name });

        results.Add($"✓ '{controller.name}' animator assigned for HumanMale + HumanFemale");
    }

    /// <summary>Searches for an AnimatorController by name, optionally restricting to paths
    /// that contain <paramref name="pathHint"/>.</summary>
    static RuntimeAnimatorController FindController(string assetName, string pathHint = null)
    {
        string[] guids = AssetDatabase.FindAssets(assetName + " t:AnimatorController");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (pathHint != null && !p.Contains(pathHint)) continue;
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
            if (ctrl != null) return ctrl;
        }
        // Second pass — no path restriction.
        if (pathHint != null)
        {
            foreach (var g in guids)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    AssetDatabase.GUIDToAssetPath(g));
                if (ctrl != null) return ctrl;
            }
        }
        return null;
    }

    static void LoadBothGenderOutfits(DynamicCharacterAvatar dca,
                                      List<string> results, List<string> warnings)
    {
        dca.preloadWardrobeRecipes.recipes.Clear();
        int added = 0;

        foreach (var name in MaleOutfit)
            if (AddRecipe(dca, name)) added++;
            else warnings.Add($"⚠ Male recipe not found: {name}");

        foreach (var name in FemaleOutfit)
            if (AddRecipe(dca, name)) added++;
            else warnings.Add($"⚠ Female recipe not found: {name}");

        results.Add($"✓ Preloaded {added} wardrobe recipes (male + female)");
        EditorUtility.SetDirty(dca);
    }

    static bool AddRecipe(DynamicCharacterAvatar dca, string recipeName)
    {
        foreach (var suffix in new[] { "t:UMAWardrobeRecipe", "t:UMATextRecipe" })
        {
            string[] guids = AssetDatabase.FindAssets(recipeName + " " + suffix);
            if (guids.Length == 0) continue;
            string chosen = guids[0];
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == recipeName)
                { chosen = g; break; }
            }
            var recipe = AssetDatabase.LoadAssetAtPath<UMATextRecipe>(
                AssetDatabase.GUIDToAssetPath(chosen));
            if (recipe == null) continue;
            var item = new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe);
            item._enabledInDefaultWardrobe = true;
            dca.preloadWardrobeRecipes.recipes.Add(item);
            return true;
        }
        return false;
    }
}
