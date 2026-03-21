using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Remove Ceilings
/// Deletes every GameObject whose name contains "ceiling" (case-insensitive).
/// Walls, floors, doors, and all other objects are untouched.
/// </summary>
public static class RemoveCeilings
{
    [MenuItem("Tools/Remove Ceilings")]
    public static void Run()
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var go in all)
        {
            if (go.name.IndexOf("ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RemoveCeilings] Removed {count} ceiling object(s).");
        EditorUtility.DisplayDialog("Remove Ceilings",
            $"Removed {count} ceiling object(s).\nSave the scene (Ctrl+S).", "OK");
    }
}
