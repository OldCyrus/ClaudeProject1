using System.Collections.Generic;
using UnityEngine;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Attach to the same GameObject as DynamicCharacterAvatar (or any parent).
///
/// Listens for CharacterUpdated and applies the appropriate default wardrobe
/// whenever the character is wearing nothing for the current race.  This
/// ensures HumanFemale gets clothes after a ChangeRace() call at runtime,
/// without interfering with player-chosen wardrobe customisations.
/// </summary>
public class RaceWardrobeController : MonoBehaviour
{
    [Header("Avatar")]
    public DynamicCharacterAvatar avatar;

    [Header("Male Default Outfit")]
    public List<string> maleOutfit = new List<string>
    {
        "MaleDefaultUnderwear",
        "MaleShirt2",
        "MaleJeans",
        "TallShoes_Recipe",
        "MaleHair1",
    };

    [Header("Female Default Outfit")]
    public List<string> femaleOutfit = new List<string>
    {
        "FemaleDefaultUnderwear",
        "FemaleShirt1",
        "FemalePants1",
        "FemaleHair1",
    };

    // Tracks the race for which we last applied defaults so we only apply
    // once per race-switch and don't loop endlessly.
    string _lastDefaultsAppliedForRace = null;

    void Start()
    {
        if (avatar == null)
            avatar = GetComponentInChildren<DynamicCharacterAvatar>();

        if (avatar == null)
        {
            Debug.LogWarning("[RaceWardrobeController] No DynamicCharacterAvatar found.");
            return;
        }

        avatar.CharacterUpdated.AddListener(OnCharacterUpdated);
    }

    void OnDestroy()
    {
        if (avatar != null)
            avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
    }

    void OnCharacterUpdated(UMAData umaData)
    {
        if (avatar == null) return;

        string race = avatar.activeRace?.name ?? "";
        if (string.IsNullOrEmpty(race)) return;

        // Already applied defaults for this race in this session — don't repeat.
        if (race == _lastDefaultsAppliedForRace) return;

        // Check if the character currently has any wardrobe items equipped.
        var equipped = avatar.WardrobeRecipes;
        if (equipped != null && equipped.Count > 0)
        {
            // Character already has wardrobe — player may have customised it.
            // Record the race so we know defaults were handled.
            _lastDefaultsAppliedForRace = race;
            return;
        }

        // Wardrobe is empty for this race — apply defaults.
        List<string> outfit = race.Contains("Female") ? femaleOutfit
                            : race.Contains("Male")   ? maleOutfit
                            : null;

        if (outfit == null || outfit.Count == 0)
        {
            Debug.LogWarning($"[RaceWardrobeController] No default outfit defined for race '{race}'.");
            return;
        }

        Debug.Log($"[RaceWardrobeController] Applying default outfit for race '{race}'.");

        foreach (var recipeName in outfit)
        {
            avatar.SetSlot(recipeName);
        }

        _lastDefaultsAppliedForRace = race;

        // Rebuild to display the new wardrobe.
        avatar.BuildCharacter(false);
    }
}
