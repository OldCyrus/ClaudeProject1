using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Add Character Loader to Player
/// Finds the Player in the current scene and attaches CharacterLoader to it.
/// Run this with the game scene (ClaudeMainLevel) open.
/// </summary>
public static class AddCharacterLoader
{
    [MenuItem("Tools/Add Character Loader to Player")]
    public static void Run()
    {
        // Find the player — looks for GameObject named "Player" that has CharacterController.
        CharacterController cc = Object.FindAnyObjectByType<CharacterController>();
        if (cc == null)
        {
            EditorUtility.DisplayDialog("Character Loader",
                "No CharacterController found.\n\n" +
                "Open ClaudeMainLevel (the game scene) first.", "OK");
            return;
        }

        var player = cc.gameObject;

        // Remove any existing CharacterLoader to avoid duplicates.
        var existing = player.GetComponent<CharacterLoader>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Character Loader",
                $"CharacterLoader is already on '{player.name}'.\n\nNo changes made.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Add CharacterLoader");

        var loader = player.AddComponent<CharacterLoader>();

        // Auto-wire DCA if it exists in children.
        var dca = player.GetComponentInChildren<DynamicCharacterAvatar>();
        if (dca != null)
        {
            loader.avatar = dca;
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string dcaStatus = dca != null
            ? $"DCA wired: '{dca.gameObject.name}'"
            : "⚠ No DCA found in children — assign manually in Inspector.";

        Debug.Log($"[AddCharacterLoader] Added CharacterLoader to '{player.name}'. {dcaStatus}");

        EditorUtility.DisplayDialog("Character Loader — Done",
            $"✓ CharacterLoader added to '{player.name}'\n" +
            $"{dcaStatus}\n\n" +
            "Remaining steps:\n" +
            "1. Save this scene (Ctrl+S)\n" +
            "2. Add BOTH scenes to File > Build Settings\n" +
            "3. Play the character creation scene and click 'Start Game'",
            "OK — Save Scene");
    }
}
