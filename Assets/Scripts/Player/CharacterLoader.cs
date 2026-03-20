using UnityEngine;
using UMA.CharacterSystem;

/// <summary>
/// Attach to the Player in the game scene.
/// Reads the saved UMA recipe from PlayerPrefs and applies it after the DCA builds.
/// </summary>
[DefaultExecutionOrder(-200)]
public class CharacterLoader : MonoBehaviour
{
    [Tooltip("The DynamicCharacterAvatar on the player. Auto-found if left empty.")]
    public DynamicCharacterAvatar avatar;

    [Tooltip("Spawn point name prefix. Player is teleported to the first match on load.")]
    public string spawnPointPrefix = "SpawnPoint";

    string _savedRecipe;

    void Awake()
    {
        // Find DCA — check children first, then search the whole scene.
        if (avatar == null)
            avatar = GetComponentInChildren<DynamicCharacterAvatar>();
        if (avatar == null)
            avatar = FindAnyObjectByType<DynamicCharacterAvatar>();

        _savedRecipe = CharacterPersistence.LoadRecipe();

        Debug.Log($"[CharacterLoader] DCA: {(avatar != null ? avatar.gameObject.name : "NULL")} | " +
                  $"Recipe saved: {!string.IsNullOrEmpty(_savedRecipe)} ({_savedRecipe?.Length ?? 0} chars)");

        TeleportToSpawnPoint();
    }

    void Start()
    {
        if (avatar == null || string.IsNullOrEmpty(_savedRecipe)) return;

        // Subscribe BEFORE the first build fires so we catch it.
        avatar.CharacterUpdated.AddListener(OnFirstBuild);
    }

    void OnFirstBuild(UMA.UMAData umaData)
    {
        // Unsubscribe immediately — we only need the first build.
        avatar.CharacterUpdated.RemoveListener(OnFirstBuild);

        Debug.Log($"[CharacterLoader] OnFirstBuild — wardrobe slots: " +
                  $"{avatar.WardrobeRecipes?.Count ?? 0}, race: {avatar.activeRace?.name}");

        // Apply the saved recipe unconditionally, overriding whatever defaults built first.
        Debug.Log("[CharacterLoader] Applying saved recipe via LoadFromRecipeString.");
        avatar.LoadFromRecipeString(_savedRecipe, DynamicCharacterAvatar.LoadOptions.useDefaults, true);
    }

    void OnDestroy()
    {
        if (avatar != null)
            avatar.CharacterUpdated.RemoveListener(OnFirstBuild);
    }

    void TeleportToSpawnPoint()
    {
        GameObject spawnPoint = GameObject.Find(spawnPointPrefix + "_1");

        if (spawnPoint == null)
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.StartsWith(spawnPointPrefix))
                { spawnPoint = go; break; }
            }
        }

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;
            Debug.Log($"[CharacterLoader] Teleported to '{spawnPoint.name}'.");
        }
        else
        {
            Debug.LogWarning($"[CharacterLoader] No spawn point '{spawnPointPrefix}_1' found.");
        }
    }
}
