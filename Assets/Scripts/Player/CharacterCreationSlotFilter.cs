using System.Collections.Generic;
using UnityEngine;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Add this component to the same GameObject as NewUMAGUI to hide specific
/// wardrobe slots from the character creation UI.
///
/// Hidden slots are stripped from every item list (FaceItems, HairItems,
/// LegsItems, BodyItems) before the UI builds, so their headers never appear.
/// The underlying DCA wardrobe system is completely untouched — default outfits
/// set via preloadWardrobeRecipes still apply normally at runtime.
///
/// To restore a slot later, simply remove it from the Hidden Wardrobe Slots list.
/// </summary>
[RequireComponent(typeof(NewUMAGUI))]
public class CharacterCreationSlotFilter : MonoBehaviour
{
    [Header("Hidden Wardrobe Slots")]
    [Tooltip("Wardrobe slot names to hide from the character creation UI.\n" +
             "Exact slot names: Hair, Chest, Legs, Feet, Hands, Face, Helmet, etc.\n" +
             "The underlying wardrobe system and default outfit are not affected.")]
    public List<string> hiddenWardrobeSlots = new List<string> { "Chest", "Legs" };

    // Awake runs before Start, so the lists are filtered before NewUMAGUI.Start()
    // populates the UI.
    void Awake()
    {
        var gui = GetComponent<NewUMAGUI>();
        if (gui == null) return;

        if (hiddenWardrobeSlots == null || hiddenWardrobeSlots.Count == 0) return;

        int removed = 0;

        removed += FilterList(gui.FaceItems,  "FaceItems");
        removed += FilterList(gui.HairItems,  "HairItems");
        removed += FilterList(gui.LegsItems,  "LegsItems");
        removed += FilterList(gui.BodyItems,  "BodyItems");

        // Hide navigation buttons whose entire item list is now empty,
        // so players don't tap a button and see a blank panel.
        HideButtonIfEmpty(gui.LegsButton, gui.LegsItems, "Legs");
        HideButtonIfEmpty(gui.BodyButton, gui.BodyItems, "Body");
        HideButtonIfEmpty(gui.FaceButton, gui.FaceItems, "Face");
        HideButtonIfEmpty(gui.HairButton, gui.HairItems, "Hair");

        if (removed > 0)
            Debug.Log($"[CharacterCreationSlotFilter] Removed {removed} item(s) from UI " +
                      $"for hidden slots: [{string.Join(", ", hiddenWardrobeSlots)}]");
    }

    int FilterList(List<UMAWardrobeRecipe> items, string listName)
    {
        if (items == null) return 0;

        int before = items.Count;
        items.RemoveAll(r => r != null && hiddenWardrobeSlots.Contains(r.wardrobeSlot));
        int removed = before - items.Count;

        if (removed > 0)
            Debug.Log($"[CharacterCreationSlotFilter] {listName}: removed {removed} item(s)");

        return removed;
    }

    void HideButtonIfEmpty(GameObject button, List<UMAWardrobeRecipe> items, string label)
    {
        if (button == null) return;

        // A button section is "empty" when it has no items after filtering AND
        // no DNA/colour options — check items only (DNA is unaffected by slot filter).
        if (items != null && items.Count == 0)
        {
            button.SetActive(false);
            Debug.Log($"[CharacterCreationSlotFilter] Hid '{label}' navigation button (no visible items).");
        }
    }
}
