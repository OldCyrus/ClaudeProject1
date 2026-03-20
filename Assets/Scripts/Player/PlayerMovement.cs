using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Third-person character movement: walk, sprint, jump, and dodge/roll.
/// Uses the new Input System when available, otherwise falls back to legacy Input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed   = 5f;
    public float sprintSpeed = 9f;

    [Header("Jump")]
    public float jumpHeight = 2.2f;
    public float gravity    = -22f;

    [Header("Dodge / Roll")]
    public float dodgeSpeed      = 20f;
    public float dodgeDuration   = 0.28f;
    public float dodgeCooldown   = 1.0f;
    [Tooltip("Double-tap window in seconds.")]
    public float doubleTapWindow = 0.28f;

    [Header("Rotation")]
    public float turnSmoothTime = 0.08f;

    // ── References ──────────────────────────────────────────────────────────
    CharacterController _cc;
    PlayerStats         _stats;
    PlayerAnimator      _playerAnimator;
    Transform           _cam;

    // ── Vertical velocity ───────────────────────────────────────────────────
    float _verticalVelocity;

    // ── Dodge state ─────────────────────────────────────────────────────────
    bool    _isDodging;
    float   _dodgeTimer;
    float   _dodgeCooldownTimer;
    Vector3 _dodgeDir;

    // ── Double-tap detection (W=0, A=1, S=2, D=3) ──────────────────────────
    readonly float[] _lastTap  = new float[4];
#if !ENABLE_INPUT_SYSTEM
    readonly KeyCode[] _moveKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
#endif

    float _turnVelocity;

    // ── New Input System bindings ───────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
    Vector2 _moveInput;
    bool    _jumpPressed;
    bool    _sprintHeld;
    bool    _dodgePressed;

    // Called by PlayerInput component events (Send Messages mode).
    public void OnMove(InputValue v)   => _moveInput    = v.Get<Vector2>();
    public void OnJump(InputValue v)   => _jumpPressed  = v.isPressed;
    public void OnSprint(InputValue v) => _sprintHeld   = v.isPressed;
    public void OnDodge(InputValue v)  { if (v.isPressed) TryDodge(GetCameraRelativeDir(_moveInput)); }
#endif

    // ── Lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        _cc             = GetComponent<CharacterController>();
        _stats          = GetComponent<PlayerStats>();
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    void Update()
    {
        // Cache camera reference (set by ThirdPersonCamera on start).
        if (_cam == null) _cam = Camera.main?.transform;

        if (_dodgeCooldownTimer > 0f)
            _dodgeCooldownTimer -= Time.deltaTime;

        if (_isDodging)
        {
            TickDodge();
            return;
        }

        Move();
        HandleJump();
        HandleLegacyDodge();
        ApplyGravity();
    }

    // ── Movement ────────────────────────────────────────────────────────────

    void Move()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 raw = _moveInput;
#else
        Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        Vector3 worldDir = GetCameraRelativeDir(raw);

        bool sprinting =
#if ENABLE_INPUT_SYSTEM
            _sprintHeld;
#else
            Input.GetKey(KeyCode.LeftShift);
#endif

        float speed = sprinting ? sprintSpeed : walkSpeed;

        if (worldDir.sqrMagnitude > 0.01f)
        {
            float targetAngle  = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
            float smoothAngle  = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
                                                       ref _turnVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
        }

        _cc.Move(worldDir * speed * Time.deltaTime);

        // Legacy double-tap dodge detection.
#if !ENABLE_INPUT_SYSTEM
        DetectDoubleTap(raw);
#endif
    }

    // ── Jump ────────────────────────────────────────────────────────────────

    void HandleJump()
    {
        if (!_cc.isGrounded) return;
#if ENABLE_INPUT_SYSTEM
        bool jump = _jumpPressed;
        _jumpPressed = false;
#else
        bool jump = Input.GetKeyDown(KeyCode.Space);
#endif
        if (jump)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // ── Gravity ─────────────────────────────────────────────────────────────

    void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
    }

    // ── Dodge ────────────────────────────────────────────────────────────────

    /// Legacy input dodge: Left Ctrl instant, or double-tap a direction key.
    void HandleLegacyDodge()
    {
#if !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 dir = GetCameraRelativeDir(raw);
            TryDodge(dir.sqrMagnitude > 0.01f ? dir : transform.forward);
        }
#endif
    }

#if !ENABLE_INPUT_SYSTEM
    void DetectDoubleTap(Vector2 raw)
    {
        // W=0, A=1, S=2, D=3
        KeyCode[] keys = _moveKeys;
        Vector3[] dirs =
        {
            GetCameraRelativeDir(Vector2.up),
            GetCameraRelativeDir(Vector2.left),
            GetCameraRelativeDir(Vector2.down),
            GetCameraRelativeDir(Vector2.right)
        };
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i]))
            {
                if (Time.time - _lastTap[i] <= doubleTapWindow)
                {
                    TryDodge(dirs[i]);
                    _lastTap[i] = 0f;
                    return;
                }
                _lastTap[i] = Time.time;
            }
        }
    }
#endif

    void TryDodge(Vector3 direction)
    {
        if (_dodgeCooldownTimer > 0f || _isDodging) return;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;

        _isDodging          = true;
        _dodgeTimer         = dodgeDuration;
        _dodgeCooldownTimer = dodgeCooldown;
        _dodgeDir           = direction.normalized;
        _stats.IsInvincible = true;

        _playerAnimator?.TriggerDodge();
    }

    void TickDodge()
    {
        _cc.Move(_dodgeDir * dodgeSpeed * Time.deltaTime);
        _dodgeTimer -= Time.deltaTime;

        // Keep gravity during dodge so we don't float.
        _verticalVelocity += gravity * Time.deltaTime;
        _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);

        if (_dodgeTimer <= 0f)
        {
            _isDodging          = false;
            _stats.IsInvincible = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    Vector3 GetCameraRelativeDir(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return Vector3.zero;

        if (_cam != null)
        {
            Vector3 fwd   = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_cam.right,   Vector3.up).normalized;
            return (fwd * input.y + right * input.x).normalized;
        }
        return new Vector3(input.x, 0f, input.y).normalized;
    }

    // ── Gizmos ──────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
