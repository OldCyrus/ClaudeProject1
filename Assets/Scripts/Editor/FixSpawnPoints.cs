using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Fix Spawn Points
///
/// Raises all SpawnPoint_* objects to Y = 1.0 above the floor so the player's
/// CharacterController capsule (bottom = pivot.y - 0.9) starts above the floor
/// surface instead of spawning inside it and falling through.
/// </summary>
public static class FixSpawnPoints
{
    // The CharacterController capsule bottom sits 0.9 m below the pivot when
    // center=(0,0,0) and height=1.8.  Spawning at Y=1.0 puts the capsule
    // bottom at Y=0.1, safely above any floor slab whose top is at Y=0.
    const float SpawnY = 1.0f;

    [MenuItem("Tools/Fix Spawn Points")]
    public static void Run()
    {
        int fixed_ = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("SpawnPoint")) continue;

            Undo.RecordObject(go.transform, "Fix Spawn Points");
            var p = go.transform.position;
            p.y = SpawnY;
            go.transform.position = p;
            EditorUtility.SetDirty(go);
            fixed_++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FixSpawnPoints] Raised {fixed_} spawn point(s) to Y={SpawnY}.");
        EditorUtility.DisplayDialog("Fix Spawn Points",
            $"Raised {fixed_} SpawnPoint object(s) to Y={SpawnY}.\n\nSave the scene (Ctrl+S).", "OK");
    }
}
