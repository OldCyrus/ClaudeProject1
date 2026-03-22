using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Place Enemy Spawns
/// Re-creates the EnemySpawn_c_r marker objects without rebuilding the whole level.
/// Uses the same grid constants and spawn cells as BuildPrisonLevel.
/// </summary>
public static class PlaceEnemySpawns
{
    // Must match BuildPrisonLevel constants exactly
    const float MCELL   = 4.0f;
    const float MGAP    = 2.5f;
    const float MSTRIDE = MCELL + MGAP;   // 6.5
    const float MX0     = -26f;
    const float MZ1     = -4f;

    static readonly (int c, int r)[] SpawnCells = {
        (7,7),(1,0),(7,5),(7,8),(3,7),(3,8),(0,6),(6,3),
    };

    [MenuItem("Tools/Place Enemy Spawns")]
    public static void Run()
    {
        // Remove any stale spawn markers
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.StartsWith("EnemySpawn_"))
                Undo.DestroyObjectImmediate(go);
        }

        // Find or create a parent object to keep the hierarchy tidy
        GameObject parent = GameObject.Find("EnemySpawnPoints");
        if (parent == null)
        {
            parent = new GameObject("EnemySpawnPoints");
            Undo.RegisterCreatedObjectUndo(parent, "Place Enemy Spawns");
        }

        foreach (var (c, r) in SpawnCells)
        {
            var sp = new GameObject($"EnemySpawn_{c}_{r}");
            sp.transform.position = new Vector3(CellCX(c), 1.0f, CellCZ(r));
            sp.transform.SetParent(parent.transform, true);
            Undo.RegisterCreatedObjectUndo(sp, "Place Enemy Spawns");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Place Enemy Spawns",
            $"Placed {SpawnCells.Length} spawn markers.\n\nNow run Tools > Spawn Enemies.",
            "OK");
    }

    static float CellCX(int c) => MX0 + c * MSTRIDE + MCELL * 0.5f;
    static float CellCZ(int r) => MZ1 - r * MSTRIDE - MCELL * 0.5f;
}
