using UnityEngine;

/// <summary>
/// Silently receives animation events fired by the Starter Assets animator
/// controller (OnFootstep, OnLand).  Must live on the same GameObject as the
/// Animator — Unity only dispatches animation events to scripts on the exact
/// same object, not to parent GameObjects.
///
/// Attach to the UMAAvatar child on both the Player and every enemy.
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    void OnFootstep(AnimationEvent animationEvent) { }
    void OnLand(AnimationEvent animationEvent)     { }
}
