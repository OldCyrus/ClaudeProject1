using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UMA.CharacterSystem;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Attach to the "Start Game" button in the character creation scene.
/// Saves the current UMA recipe to PlayerPrefs then loads the game scene.
/// Waits one frame before loading so UMA's delayed build queue can flush,
/// preventing MissingReferenceException on scene unload.
/// </summary>
public class ConfirmCharacterButton : MonoBehaviour
{
    [Tooltip("The DynamicCharacterAvatar being customised. Auto-found if left empty.")]
    public DynamicCharacterAvatar avatar;

    [Tooltip("Exact name of the game scene as shown in Build Settings.")]
    public string gameSceneName = "ClaudeMainLevel";

    [Tooltip("Optional: button to wire up automatically in Awake.")]
    public Button confirmButton;

    void Awake()
    {
        if (avatar == null)
            avatar = FindAnyObjectByType<DynamicCharacterAvatar>();

        if (confirmButton == null)
            confirmButton = GetComponent<Button>();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
    }

    public void OnConfirmClicked()
    {
        if (avatar == null)
        {
            Debug.LogError("[ConfirmCharacterButton] No DynamicCharacterAvatar found.");
            return;
        }

        // Capture the full character state: race, DNA, wardrobe, colors.
        string recipe = avatar.GetCurrentRecipe();

        if (string.IsNullOrEmpty(recipe))
        {
            Debug.LogWarning("[ConfirmCharacterButton] GetCurrentRecipe returned empty — " +
                             "avatar may not be fully built yet. Loading scene anyway.");
        }
        else
        {
            CharacterPersistence.SaveRecipe(recipe);
            Debug.Log($"[ConfirmCharacterButton] Saved recipe ({recipe.Length} chars). " +
                      $"Loading scene '{gameSceneName}'.");
        }

        // Disable the button immediately to prevent double-clicks.
        if (confirmButton != null) confirmButton.interactable = false;

        // Wait one frame before loading — lets UMA flush any pending delayed
        // build calls so the DCA isn't accessed after scene unload.
        StartCoroutine(LoadSceneNextFrame());
    }

    IEnumerator LoadSceneNextFrame()
    {
        yield return null; // one frame gap

        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            yield break;
        }

#if UNITY_EDITOR
        // Editor fallback: load by asset path so Build Profiles aren't required.
        string[] guids = UnityEditor.AssetDatabase.FindAssets(gameSceneName + " t:Scene");
        string scenePath = null;
        foreach (var g in guids)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == gameSceneName)
            {
                scenePath = p;
                break;
            }
        }

        if (scenePath != null)
        {
            EditorSceneManager.LoadSceneInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
        }
        else
        {
            Debug.LogError($"[ConfirmCharacterButton] Scene '{gameSceneName}' not found. " +
                           "Add it to File > Build Profiles.");
            if (confirmButton != null) confirmButton.interactable = true;
        }
#else
        Debug.LogError($"[ConfirmCharacterButton] Scene '{gameSceneName}' not in Build Profiles.");
        if (confirmButton != null) confirmButton.interactable = true;
#endif
    }
}
