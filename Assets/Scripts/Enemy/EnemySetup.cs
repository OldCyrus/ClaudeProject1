using UnityEngine;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Configures a UMA DynamicCharacterAvatar for the enemy.
///
/// Wardrobe applied (all recipe names verified to exist in project):
///   MaleDefaultUnderwear  (Underwear slot)
///   MalePants             (Legs slot  — "Wrapped Pants", more formal than jeans)
///   MaleShirt4            (Chest slot)
///   MaleHair1             (Hair slot  — "The Rebel", short cut)
///
/// After UMA builds the mesh, assigns the Starter Assets animator controller
/// and calls EnemyAI.OnCharacterReady() to begin patrol.
/// </summary>
public class EnemySetup : MonoBehaviour
{
    [Tooltip("The EnemyAI component on the enemy root GameObject.")]
    public EnemyAI aiComponent;

    [Tooltip("UMA race name to use for this enemy.")]
    public string raceName = "HumanMale";

    [Tooltip("Animator controller assigned by SpawnEnemies editor tool.")]
    public RuntimeAnimatorController animatorController;

    DynamicCharacterAvatar _dca;
    bool _built;

    void Start()
    {
        _dca = GetComponent<DynamicCharacterAvatar>();
        if (_dca == null)
        {
            Debug.LogWarning("[EnemySetup] No DynamicCharacterAvatar on " + gameObject.name);
            return;
        }

        _dca.activeRace.name = raceName;
        _dca.CharacterUpdated.AddListener(OnCharacterBuilt);

        // Set animator controller via UMA's built-in pipeline so it is applied
        // automatically when BuildCharacter() runs — this is the reliable path.
        if (animatorController != null)
            _dca.raceAnimationControllers.defaultAnimationController = animatorController;
        else
            Debug.LogWarning("[EnemySetup] animatorController is null in Start — enemy may T-pose.");

        // Apply wardrobe before BuildCharacter so it is included in the first mesh build.
        // Recipe names verified to exist in:
        //   Assets/UMA/Content/Example/HumanMale/Recipes/WardrobeRecipes/
        _dca.SetSlot("MaleDefaultUnderwear");
        _dca.SetSlot("MaleRobe");
        _dca.SetSlot("MaleHair1");     // The Rebel — short military-style cut

        _dca.BuildCharacter();
    }

    void OnCharacterBuilt(UMAData data)
    {
        if (_built) return;
        _built = true;

        Debug.Log($"[EnemySetup] OnCharacterBuilt fired on {gameObject.name}");

        var anim = GetComponentInChildren<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("[EnemySetup] No Animator found after UMA build on " + gameObject.name);
        }
        else
        {
            anim.applyRootMotion = false;

            // UMA should have already assigned the controller via raceAnimationControllers.
            // If it is still null, assign it manually as a fallback.
            if (anim.runtimeAnimatorController != null)
            {
                Debug.Log($"[EnemySetup] Animator controller already set by UMA: '{anim.runtimeAnimatorController.name}' on {gameObject.name}");
            }
            else if (animatorController != null)
            {
                anim.runtimeAnimatorController = animatorController;
                Debug.Log($"[EnemySetup] Assigned animator controller (fallback) '{animatorController.name}' to {gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[EnemySetup] animatorController is null — enemy will T-pose. " +
                                 "Re-run Tools > Spawn Enemies to reassign it.");
            }
        }

        if (aiComponent != null)
            aiComponent.OnCharacterReady();
        else
            Debug.LogWarning("[EnemySetup] aiComponent not assigned on " + gameObject.name);
    }
}
