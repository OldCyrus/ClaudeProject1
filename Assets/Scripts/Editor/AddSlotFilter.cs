using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;

/// <summary>
/// Tools > Add Character Creation Slot Filter
/// Finds the GameObject with NewUMAGUI in the current scene and
/// attaches CharacterCreationSlotFilter to it, hiding Chest and Legs.
/// </summary>
public static class AddSlotFilter
{
    [MenuItem("Tools/Add Character Creation Slot Filter")]
    public static void Run()
    {
        var gui = Object.FindAnyObjectByType<NewUMAGUI>();

        if (gui == null)
        {
            EditorUtility.DisplayDialog("Slot Filter",
                "No NewUMAGUI component found in the current scene.\n\n" +
                "Make sure the character creation scene is open and loaded.",
                "OK");
            return;
        }

        var go = gui.gameObject;

        // Already there?
        var existing = go.GetComponent<CharacterCreationSlotFilter>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Slot Filter",
                $"CharacterCreationSlotFilter is already on '{go.name}'.\n\n" +
                $"Hidden slots: {string.Join(", ", existing.hiddenWardrobeSlots)}",
                "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(go, "Add CharacterCreationSlotFilter");

        var filter = go.AddComponent<CharacterCreationSlotFilter>();
        // defaults are already Chest and Legs — nothing else to set

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[AddSlotFilter] Added CharacterCreationSlotFilter to '{go.name}'. " +
                  "Hidden slots: Chest, Legs. Save the scene (Ctrl+S).");

        EditorUtility.DisplayDialog("Slot Filter — Done",
            $"✓ CharacterCreationSlotFilter added to '{go.name}'\n\n" +
            "Hidden slots: Chest, Legs\n\n" +
            "Save the scene (Ctrl+S) then Play.\n" +
            "To change hidden slots, select the object and edit the list in the Inspector.",
            "OK — Save Scene");
    }
}
