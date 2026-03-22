using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA.CharacterSystem;

/// <summary>
/// Editor tool: Tools > Spawn Enemies
///
/// Finds all GameObjects whose name starts with "EnemySpawn", removes any
/// stale "Enemy_N" GameObjects, then creates a fully configured enemy at
/// each spawn point.
/// </summary>
public static class SpawnEnemies
{
    [MenuItem("Tools/Spawn Enemies")]
    static void Run()
    {
        // ── 1. Collect spawn point positions ──────────────────────────────────
        List<Vector3> spawnPositions = new List<Vector3>();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.StartsWith("EnemySpawn"))
                spawnPositions.Add(go.transform.position);
        }

        if (spawnPositions.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Spawn Enemies",
                "No GameObjects whose name starts with \"EnemySpawn\" were found in the scene.\n" +
                "Run Tools > Build Prison Level first to generate spawn points.",
                "OK");
            return;
        }

        // ── 2. Remove existing enemies (avoid doubles) ────────────────────────
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.StartsWith("Enemy_"))
                toDestroy.Add(go);
        }
        foreach (GameObject go in toDestroy)
            Object.DestroyImmediate(go);

        // ── 3. Create one enemy per spawn position ────────────────────────────
        for (int i = 0; i < spawnPositions.Count; i++)
            CreateEnemy(i, spawnPositions[i]);

        // ── 4. Finalise ───────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Spawn Enemies",
            $"Spawned {spawnPositions.Count} enemies.\n\n" +
            "IMPORTANT: Before pressing Play:\n" +
            "1. Run Tools > Fix Prison Materials to apply materials\n" +
            "2. Add PlayerHealth + WorldSpaceHealthBar to the Player GameObject.",
            "OK");
    }

    // ── Enemy factory ──────────────────────────────────────────────────────────

    static void CreateEnemy(int index, Vector3 position)
    {
        // ── Root GameObject ────────────────────────────────────────────────────
        GameObject root = new GameObject($"Enemy_{index}");
        root.transform.position = position;

        // ── CharacterController ────────────────────────────────────────────────
        // center=(0, 0.9, 0): capsule bottom = center.y - height/2 = 0.9 - 0.9 = 0
        // relative to the pivot.  Enemy pivots sit at floor level, so the CC
        // bottom is exactly at the floor surface — no overlap, no upward push.
        // (Using center=(0,0,0) embeds the bottom 0.9 m into the floor, causing
        // Unity to push the enemy upward when Play starts.)
        var cc        = root.AddComponent<CharacterController>();
        cc.height     = 1.8f;
        cc.center     = new Vector3(0f, 0.9f, 0f);
        cc.radius     = 0.35f;

        // ── Gameplay components ────────────────────────────────────────────────
        var ai         = root.AddComponent<EnemyAI>();
        var health     = root.AddComponent<EnemyHealth>();
        var pushable   = root.AddComponent<EnemyPushable>();
        var healthBar  = root.AddComponent<WorldSpaceHealthBar>();

        // Suppress unused-variable warnings — these are added for their presence
        _ = health;
        _ = pushable;
        _ = healthBar;

        // ── UMA Avatar child ───────────────────────────────────────────────────
        GameObject avatarGO = new GameObject("UMAAvatar");
        avatarGO.transform.SetParent(root.transform, false);
        avatarGO.transform.localPosition = Vector3.zero;
        avatarGO.transform.localRotation = Quaternion.identity;

        var dca = avatarGO.AddComponent<DynamicCharacterAvatar>();
        dca.activeRace.name   = "HumanMale";
        dca.loadFileOnStart   = false;

        // Receive OnFootstep / OnLand animation events — must be on the same
        // GameObject as the Animator (avatarGO), not the root.
        avatarGO.AddComponent<AnimationEventReceiver>();

        // Load the Starter Assets animator controller
        var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller");
        if (animCtrl == null)
            Debug.LogWarning("[SpawnEnemies] Could not find StarterAssetsThirdPerson.controller — enemies will T-pose.");

        var setup                  = avatarGO.AddComponent<EnemySetup>();
        setup.aiComponent          = ai;
        setup.raceName             = "HumanMale";
        setup.animatorController   = animCtrl;

        // ── Bat anchor ────────────────────────────────────────────────────────
        GameObject batAnchor = new GameObject("BatAnchor");
        batAnchor.transform.SetParent(root.transform, false);
        batAnchor.transform.localPosition = new Vector3(0.4f, 1.2f, 0.3f);
        batAnchor.transform.localRotation = Quaternion.identity;

        // ── Bat ───────────────────────────────────────────────────────────────
        GameObject batGO = CreateBatVisual();
        batGO.name = "Bat";
        batGO.transform.SetParent(batAnchor.transform, false);
        batGO.transform.localPosition = Vector3.zero;
        batGO.transform.localRotation = Quaternion.identity;

        var bat = batGO.AddComponent<BaseballBat>();
        bat.SetEnemyHolder(batAnchor.transform);

        Debug.Log($"[SpawnEnemies] Created Enemy_{index} at {position}");
    }

    // ── Bat visual helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a scaled capsule mesh to represent the baseball bat visually.
    /// Uses CreatePrimitive internally then extracts the mesh/renderer before
    /// destroying the temporary primitive.
    /// </summary>
    static GameObject CreateBatVisual()
    {
        // Temporary primitive to borrow the capsule mesh
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Mesh capsuleMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);

        GameObject batVisual = new GameObject("BatMesh");
        batVisual.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);

        var mf = batVisual.AddComponent<MeshFilter>();
        mf.sharedMesh = capsuleMesh;

        var mr = batVisual.AddComponent<MeshRenderer>();

        // Try to use URP/Lit; fall back to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.55f, 0.3f, 0.1f));  // wooden brown
            mr.sharedMaterial = mat;
        }

        return batVisual;
    }
}
