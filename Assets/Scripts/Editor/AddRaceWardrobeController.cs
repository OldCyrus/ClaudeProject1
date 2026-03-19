using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA.CharacterSystem;

/// <summary>
/// Tools > Add Race Wardrobe Controller
/// Finds the DCA in the scene and adds RaceWardrobeController to its GameObject.
/// </summary>
public static class AddRaceWardrobeController
{
    [MenuItem("Tools/Add Race Wardrobe Controller")]
    public static void Run()
    {
        var dca = Object.FindAnyObjectByType<DynamicCharacterAvatar>();
        if (dca == null)
        {
            EditorUtility.DisplayDialog("Race Wardrobe Controller",
                "No DynamicCharacterAvatar found in the current scene.\n" +
                "Open the character creation scene and try again.", "OK");
            return;
        }

        var go = dca.gameObject;

        var existing = go.GetComponent<RaceWardrobeController>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Race Wardrobe Controller",
                $"RaceWardrobeController is already on '{go.name}'.\n\n" +
                "No changes made.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(go, "Add RaceWardrobeController");

        var ctrl = go.AddComponent<RaceWardrobeController>();
        ctrl.avatar = dca;   // wire up the reference immediately

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[AddRaceWardrobeController] Added RaceWardrobeController to '{go.name}' " +
                  "and wired DCA reference. Save the scene (Ctrl+S).");

        EditorUtility.DisplayDialog("Race Wardrobe Controller — Done",
            $"✓ RaceWardrobeController added to '{go.name}'\n\n" +
            "It will automatically apply:\n" +
            "  • Male outfit  when race = HumanMale\n" +
            "  • Female outfit when race = HumanFemale\n\n" +
            "Save the scene (Ctrl+S) then Play.\n\n" +
            "You can edit the outfit lists in the Inspector.",
            "OK — Save Scene");
    }
}
