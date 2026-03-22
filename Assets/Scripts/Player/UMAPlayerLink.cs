using UnityEngine;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Sits on the Player root.  Holds a reference to the DynamicCharacterAvatar child
/// and wires it into the rest of the player system once the UMA character is built.
///
/// Responsibilities:
///   • Kick off the UMA character build on Start.
///   • When UMA finishes building, sync the Animator reference to any scripts that
///     need it (currently none, but the hook is here for future use).
///   • Move the CameraTarget to the avatar's head bone so the Cinemachine camera
///     frames the character correctly.
/// </summary>
public class UMAPlayerLink : MonoBehaviour
{
    [Header("UMA Avatar")]
    [Tooltip("The child GameObject that carries the DynamicCharacterAvatar component.")]
    public DynamicCharacterAvatar avatar;

    [Header("Camera Target")]
    [Tooltip("The CameraTarget child that the Cinemachine FreeLook camera orbits around.")]
    public Transform cameraTarget;

    [Tooltip("Head-bone name on the UMA skeleton (default is correct for HumanMale/Female).")]
    public string headBoneName = "Head";

    [Tooltip("Vertical offset above the head bone for the camera look-at point.")]
    public float headOffset = 0.1f;

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    void Awake()
    {
        // Auto-find if not wired in Inspector.
        if (avatar == null)
            avatar = GetComponentInChildren<DynamicCharacterAvatar>();

        if (cameraTarget == null)
        {
            var ct = transform.Find("CameraTarget");
            if (ct != null) cameraTarget = ct;
        }
    }

    void Start()
    {
        if (avatar == null)
        {
            Debug.LogWarning("[UMAPlayerLink] No DynamicCharacterAvatar found on or under " + name);
            return;
        }

        // Subscribe before the avatar builds so we don't miss the event.
        avatar.CharacterUpdated.AddListener(OnCharacterBuilt);
    }

    void OnDestroy()
    {
        if (avatar != null)
            avatar.CharacterUpdated.RemoveListener(OnCharacterBuilt);
    }

    // ── Character-built callback ────────────────────────────────────────────────

    void OnCharacterBuilt(UMAData umaData)
    {
        // Ensure animation events (OnFootstep, OnLand) have a receiver on the
        // same GameObject as the Animator — parent scripts are not called.
        if (avatar.gameObject.GetComponent<AnimationEventReceiver>() == null)
            avatar.gameObject.AddComponent<AnimationEventReceiver>();

        // Try to place the CameraTarget at the head bone position.
        if (cameraTarget != null)
        {
            Transform headBone = FindBoneInChildren(avatar.transform, headBoneName);
            if (headBone != null)
            {
                // Parent the camera target to the head bone so it follows animations.
                cameraTarget.SetParent(headBone, false);
                cameraTarget.localPosition = new Vector3(0f, headOffset, 0f);
                cameraTarget.localRotation = Quaternion.identity;
            }
        }
    }

    // ── Utilities ───────────────────────────────────────────────────────────────

    static Transform FindBoneInChildren(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            var found = FindBoneInChildren(child, boneName);
            if (found != null) return found;
        }
        return null;
    }
}
