using UnityEngine;

/// <summary>
/// Static helper for passing UMA character recipes between scenes via PlayerPrefs.
/// </summary>
public static class CharacterPersistence
{
    const string RecipeKey = "UMACharacterRecipe";

    public static void SaveRecipe(string recipe)
    {
        PlayerPrefs.SetString(RecipeKey, recipe);
        PlayerPrefs.Save();
    }

    public static string LoadRecipe()
    {
        return PlayerPrefs.GetString(RecipeKey, "");
    }

    public static bool HasRecipe()
    {
        return PlayerPrefs.HasKey(RecipeKey) &&
               !string.IsNullOrEmpty(PlayerPrefs.GetString(RecipeKey, ""));
    }

    public static void ClearRecipe()
    {
        PlayerPrefs.DeleteKey(RecipeKey);
    }
}
