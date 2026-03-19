using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Setup UMA Player
///
/// One-click tool that:
///   1. Removes the old capsule MeshRenderer / MeshFilter from the Player
///      while preserving every script component.
///   2. Creates (or finds) a child "UMAAvatar" at feet-level offset and adds
///      DynamicCharacterAvatar to it.
///   3. Configures the DCA race (HumanMale) and pre-loads a default clothed
///      outfit so the character never appears naked in-game.
///   4. Wires up UMAPlayerLink and adjusts the CameraTarget height to sit
///      at the avatar's head level.
/// </summary>
public static class UMAPlayerSetup
{
    // Vertical offset of UMA avatar root relative to Player pivot.
    // Player CC: height=1.8, center=(0,0,0) → pivot is at capsule midpoint.
    // Offset = -(height/2) so avatar feet sit at the capsule bottom.
    const float AvatarYOffset = -0.9f;

    // CameraTarget local Y on the Player (head height in Player-local space).
    // Player pivot at capsule midpoint. Head ≈ +0.9 above midpoint → local 0.7
    // (a bit below top to give a nice over-shoulder view).
    const float CameraTargetY = 0.7f;

    // ── Default wardrobe ────────────────────────────────────────────────────────
    // Asset names (without extension).  Must exist in the UMA InternalDataStore
    // or the Global Library so UMA can find them at runtime.
    static readonly string[] DefaultWardrobeRecipes =
    {
        "MaleDefaultUnderwear",   // underwear (fallback layer)
        "MaleShirt1",             // shirt
        "MaleJeans",              // jeans
        "MaleHair1",              // hair
    };

    // ── Entry point ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Setup UMA Player")]
    public static void Run()
    {
        // ── 1. Find Player ──────────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("UMA Player Setup",
                "No GameObject tagged 'Player' found.\n\n" +
                "Tag your player object 'Player' first, then run this tool.",
                "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup UMA Player");

        // ── 2. Remove capsule visual (MeshRenderer + MeshFilter) ───────────────
        RemoveCapsuleMesh(player);

        // ── 3. Create / configure UMAAvatar child ──────────────────────────────
        var dca = SetupAvatarChild(player);

        // ── 4. Configure race and default wardrobe ─────────────────────────────
        if (dca != null)
            ConfigureWardrobe(dca);

        // ── 5. Wire up CameraTarget ────────────────────────────────────────────
        UpdateCameraTarget(player);

        // ── 6. Add UMAPlayerLink bridge ────────────────────────────────────────
        var link = player.GetComponent<UMAPlayerLink>();
        if (link == null)
            link = player.AddComponent<UMAPlayerLink>();

        link.avatar = dca;

        var ctTransform = player.transform.Find("CameraTarget");
        if (ctTransform != null) link.cameraTarget = ctTransform;

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[UMAPlayerSetup] Done. Save the scene (Ctrl+S) and press Play.\n" +
                  "Make sure UMA_GLIB is in the scene hierarchy before entering Play mode.");

        EditorUtility.DisplayDialog("UMA Player Setup — Done",
            "✓ Capsule mesh removed (scripts intact)\n" +
            "✓ UMAAvatar child configured with HumanMale race\n" +
            "✓ Default wardrobe preloaded (shirt, jeans, hair)\n" +
            "✓ CameraTarget moved to head height\n" +
            "✓ UMAPlayerLink added\n\n" +
            "Next steps:\n" +
            "1. Make sure UMA_GLIB is in the scene.\n" +
            "2. Save the scene (Ctrl+S).\n" +
            "3. Press Play — UMA builds the character at runtime.",
            "OK");
    }

    // ── Step 2: Remove visual-only components ───────────────────────────────────

    static void RemoveCapsuleMesh(GameObject player)
    {
        // A Capsule created via CreatePrimitive gets a MeshRenderer and MeshFilter.
        // The CharacterController / scripts are separate — safe to remove just these two.
        var mr = player.GetComponent<MeshRenderer>();
        var mf = player.GetComponent<MeshFilter>();

        if (mr != null)
        {
            Object.DestroyImmediate(mr);
            Debug.Log("[UMAPlayerSetup] Removed MeshRenderer from Player.");
        }
        if (mf != null)
        {
            Object.DestroyImmediate(mf);
            Debug.Log("[UMAPlayerSetup] Removed MeshFilter from Player.");
        }

        if (mr == null && mf == null)
            Debug.Log("[UMAPlayerSetup] Player had no MeshRenderer/MeshFilter — nothing to remove.");
    }

    // ── Step 3: Create or find UMAAvatar child ──────────────────────────────────

    static DynamicCharacterAvatar SetupAvatarChild(GameObject player)
    {
        // Re-use existing child if already set up.
        var existingChild = player.transform.Find("UMAAvatar");
        GameObject avatarGO;

        if (existingChild != null)
        {
            avatarGO = existingChild.gameObject;
            Debug.Log("[UMAPlayerSetup] Found existing UMAAvatar child.");
        }
        else
        {
            avatarGO = new GameObject("UMAAvatar");
            avatarGO.transform.SetParent(player.transform, false);
            avatarGO.transform.localPosition = new Vector3(0f, AvatarYOffset, 0f);
            avatarGO.transform.localRotation = Quaternion.identity;
            avatarGO.transform.localScale    = Vector3.one;
            Debug.Log("[UMAPlayerSetup] Created UMAAvatar child at local Y=" + AvatarYOffset);
        }

        // Add DynamicCharacterAvatar if not already present.
        var dca = avatarGO.GetComponent<DynamicCharacterAvatar>();
        if (dca == null)
        {
            dca = avatarGO.AddComponent<DynamicCharacterAvatar>();
            Debug.Log("[UMAPlayerSetup] Added DynamicCharacterAvatar.");
        }

        // Set race.
        dca.activeRace.name = "HumanMale";

        // Ensure preload is enabled.
        dca.preloadWardrobeRecipes.loadDefaultRecipes = true;

        EditorUtility.SetDirty(avatarGO);
        return dca;
    }

    // ── Step 4: Configure default wardrobe ─────────────────────────────────────

    static void ConfigureWardrobe(DynamicCharacterAvatar dca)
    {
        dca.preloadWardrobeRecipes.recipes.Clear();

        int loaded = 0;
        foreach (var recipeName in DefaultWardrobeRecipes)
        {
            // Search entire project for this wardrobe recipe asset.
            string[] guids = AssetDatabase.FindAssets(recipeName + " t:UMAWardrobeRecipe");
            if (guids.Length == 0)
            {
                // Try broader search (sometimes recipes are UMATextRecipe base type).
                guids = AssetDatabase.FindAssets(recipeName + " t:UMATextRecipe");
            }

            if (guids.Length == 0)
            {
                Debug.LogWarning("[UMAPlayerSetup] Wardrobe recipe not found: " + recipeName +
                                 " — skipping. You can add it manually in the DCA inspector.");
                continue;
            }

            // Prefer an exact name match if multiple assets share a similar name.
            string chosen = guids[0];
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string fname = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fname == recipeName) { chosen = g; break; }
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(chosen);
            var recipe = AssetDatabase.LoadAssetAtPath<UMA.UMATextRecipe>(assetPath);
            if (recipe == null)
            {
                Debug.LogWarning("[UMAPlayerSetup] Could not load recipe at: " + assetPath);
                continue;
            }

            var item = new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe);
            dca.preloadWardrobeRecipes.recipes.Add(item);
            Debug.Log("[UMAPlayerSetup] Preloaded wardrobe: " + recipe.name);
            loaded++;
        }

        EditorUtility.SetDirty(dca);
        Debug.Log($"[UMAPlayerSetup] {loaded}/{DefaultWardrobeRecipes.Length} wardrobe items loaded.");
    }

    // ── Step 5: Update CameraTarget height ─────────────────────────────────────

    static void UpdateCameraTarget(GameObject player)
    {
        var ct = player.transform.Find("CameraTarget");
        if (ct == null)
        {
            Debug.LogWarning("[UMAPlayerSetup] CameraTarget child not found — skipping height update.");
            return;
        }

        // Only update Y; preserve X and Z (usually 0,0).
        Vector3 p = ct.localPosition;
        float oldY = p.y;
        p.y = CameraTargetY;
        ct.localPosition = p;

        EditorUtility.SetDirty(ct.gameObject);
        Debug.Log($"[UMAPlayerSetup] CameraTarget Y: {oldY} → {CameraTargetY}");
    }
}
