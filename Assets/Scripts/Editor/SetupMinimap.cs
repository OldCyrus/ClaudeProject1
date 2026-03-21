using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Setup Minimap
///
/// Creates a "Minimap" GameObject in the active scene and adds MinimapSystem to it.
/// Safe to run multiple times — removes existing Minimap first.
/// </summary>
public static class SetupMinimap
{
    [MenuItem("Tools/Setup Minimap")]
    public static void Run()
    {
        // Remove any existing minimap
        var existing = GameObject.Find("Minimap");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var go = new GameObject("Minimap");
        Undo.RegisterCreatedObjectUndo(go, "Setup Minimap");

        var mm = go.AddComponent<MinimapSystem>();

        // Default settings suited to the prison level layout:
        //   Cells at X:-14..+14, Z:0..+4
        //   Maze extends to Z:-70 approx
        //   Centre at X:0, Z:-32 covers the whole layout
        mm.worldCentre     = new Vector2(0f, -32f);
        mm.orthographicSize = 60f;
        mm.cameraHeight    = 120f;
        mm.displaySize     = 200;
        mm.textureSize     = 256;
        mm.dotSize         = 10f;
        mm.dotColor        = Color.red;

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupMinimap] Minimap added to scene. Save (Ctrl+S) and Play.");
        EditorUtility.DisplayDialog("Setup Minimap",
            "Minimap added.\n\n" +
            "A 200×200 overlay will appear in the top-right corner during Play.\n" +
            "A red dot tracks the player.\n\n" +
            "To adjust the area shown, select the 'Minimap' GameObject and\n" +
            "change 'World Centre' and 'Orthographic Size' in the Inspector.\n\n" +
            "Save the scene (Ctrl+S) before pressing Play.", "OK");
    }
}
