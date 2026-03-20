using UnityEngine;
using UMA;
using UMA.CharacterSystem;

/// <summary>
/// Drives the player's Animator parameters from CharacterController state.
/// Attach to the Player root (same object as PlayerMovement).
///
/// Gets the Animator from the UMA-built avatar after CharacterUpdated fires,
/// then updates Speed / Direction / Grounded / Jump every frame.
/// </summary>
[DefaultExecutionOrder(10)]   // runs after PlayerMovement (-200) and default (0)
public class PlayerAnimator : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public DynamicCharacterAvatar avatar;
    public PlayerMovement movement;

    [Header("Smoothing")]
    [Tooltip("Damp time for Speed and Direction blend.")]
    public float dampTime = 0.1f;

    // ── private ──────────────────────────────────────────────────────────
    Animator           _anim;
    CharacterController _cc;

    // cached parameter hashes for performance
    static readonly int HashSpeed     = Animator.StringToHash("Speed");
    static readonly int HashDirection = Animator.StringToHash("Direction");
    static readonly int HashGrounded  = Animator.StringToHash("Grounded");
    static readonly int HashJump      = Animator.StringToHash("Jump");
    static readonly int HashDodge     = Animator.StringToHash("Dodge");

    bool _wasGrounded = true;

    // ── lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (avatar == null)
            avatar = GetComponentInChildren<DynamicCharacterAvatar>();
        if (avatar == null)
            avatar = FindAnyObjectByType<DynamicCharacterAvatar>();

        if (avatar != null)
            avatar.CharacterUpdated.AddListener(OnAvatarBuilt);
    }

    void OnDestroy()
    {
        if (avatar != null)
            avatar.CharacterUpdated.RemoveListener(OnAvatarBuilt);
    }

    void OnAvatarBuilt(UMAData umaData)
    {
        // UMA creates a new Animator on the skinned mesh after every build.
        _anim = avatar.GetComponentInChildren<Animator>();
        if (_anim == null) return;

        // Disable root motion — CharacterController handles all movement.
        _anim.applyRootMotion = false;

        // Ensure Grounded starts true so we don't fall-anim on spawn.
        if (HasParam(HashGrounded)) _anim.SetBool(HashGrounded, _cc.isGrounded);

        Debug.Log($"[PlayerAnimator] Animator found: '{_anim.gameObject.name}', " +
                  $"controller: '{_anim.runtimeAnimatorController?.name}', " +
                  $"rootMotion: {_anim.applyRootMotion}");
    }

    // ── per-frame update ──────────────────────────────────────────────────

    void Update()
    {
        if (_anim == null)
        {
            // Retry — UMA may have finished building after Awake.
            if (avatar != null)
            {
                _anim = avatar.GetComponentInChildren<Animator>();
                if (_anim != null) _anim.applyRootMotion = false;
            }
            return;
        }

        UpdateLocomotion();
        UpdateGrounded();
    }

    void UpdateLocomotion()
    {
        // Horizontal speed in world space.
        Vector3 hVel  = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        float   speed = hVel.magnitude;

        // Direction: project velocity into local space → left/right strafe.
        float direction = 0f;
        if (speed > 0.1f)
        {
            Vector3 localVel = transform.InverseTransformDirection(hVel);
            direction = Mathf.Clamp(localVel.x / speed, -1f, 1f);
        }

        if (HasParam(HashSpeed))
            _anim.SetFloat(HashSpeed, speed, dampTime, Time.deltaTime);

        if (HasParam(HashDirection))
            _anim.SetFloat(HashDirection, direction, dampTime, Time.deltaTime);
    }

    void UpdateGrounded()
    {
        bool grounded = _cc.isGrounded;

        if (HasParam(HashGrounded))
            _anim.SetBool(HashGrounded, grounded);

        // Fire Jump trigger on the frame we leave the ground moving upward.
        if (!_wasGrounded && grounded == _wasGrounded) { /* nothing */ }
        if (_wasGrounded && !grounded && _cc.velocity.y > 0.5f)
        {
            if (HasParam(HashJump))
                _anim.SetTrigger(HashJump);
        }

        _wasGrounded = grounded;
    }

    // ── public API (called by other scripts) ─────────────────────────────

    /// <summary>Call from PlayerMovement when a dodge starts.</summary>
    public void TriggerDodge()
    {
        if (_anim != null && HasParam(HashDodge))
            _anim.SetTrigger(HashDodge);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    bool HasParam(int hash)
    {
        if (_anim == null) return false;
        foreach (var p in _anim.parameters)
            if (p.nameHash == hash) return true;
        return false;
    }
}
