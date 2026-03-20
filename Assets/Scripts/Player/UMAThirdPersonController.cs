using UnityEngine;
using StarterAssets;
using UMA;
using UMA.CharacterSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// UMA-aware replacement for StarterAssets.ThirdPersonController.
///
/// The standard ThirdPersonController uses TryGetComponent<Animator>() which
/// only searches the root object.  UMA builds the Animator on a dynamically
/// created child mesh, so we must bind it via DCA.CharacterUpdated and use
/// GetComponentInChildren.
///
/// Everything else is identical to the original ThirdPersonController so the
/// StarterAssetsThirdPerson.controller (Speed, MotionSpeed, Grounded, Jump,
/// FreeFall parameters) works without modification.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(StarterAssetsInputs))]
public class UMAThirdPersonController : MonoBehaviour
{
    // ── Inspector: Player ─────────────────────────────────────────────────────
    [Header("Player")]
    [Tooltip("Walk speed in m/s")]
    public float MoveSpeed = 2.0f;

    [Tooltip("Sprint speed in m/s")]
    public float SprintSpeed = 5.335f;

    [Range(0f, 0.3f)]
    [Tooltip("How fast the character turns to face movement direction")]
    public float RotationSmoothTime = 0.12f;

    [Tooltip("Acceleration and deceleration")]
    public float SpeedChangeRate = 10.0f;

    [Space(10)]
    public float JumpHeight = 1.2f;
    public float Gravity    = -15.0f;

    [Space(10)]
    [Tooltip("Time required to pass before being able to jump again")]
    public float JumpTimeout = 0.50f;

    [Tooltip("Grace period before entering the fall state (helps on stairs)")]
    public float FallTimeout = 0.15f;

    // ── Inspector: Grounding ──────────────────────────────────────────────────
    [Header("Player Grounded")]
    public bool      Grounded       = true;
    public float     GroundedOffset = -0.14f;
    public float     GroundedRadius = 0.28f;
    public LayerMask GroundLayers;

    // ── Inspector: Cinemachine ────────────────────────────────────────────────
    [Header("Cinemachine")]
    [Tooltip("Child transform the Cinemachine virtual camera follows.")]
    public GameObject CinemachineCameraTarget;

    [Tooltip("Maximum camera pitch (look up)")]
    public float TopClamp    = 70.0f;

    [Tooltip("Maximum camera pitch (look down)")]
    public float BottomClamp = -30.0f;

    [Tooltip("Extra degrees added to camera pitch (fine-tune locked angle)")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("Lock camera position on all axes")]
    public bool LockCameraPosition = false;

    // ── Inspector: UMA ────────────────────────────────────────────────────────
    [Header("UMA")]
    [Tooltip("Auto-found in children if left empty.")]
    public DynamicCharacterAvatar avatar;

    // ── private: cinemachine ──────────────────────────────────────────────────
    float _cinemachineTargetYaw;
    float _cinemachineTargetPitch;

    // ── private: movement ─────────────────────────────────────────────────────
    float _speed;
    float _animationBlend;
    float _targetRotation  = 0f;
    float _rotationVelocity;
    float _verticalVelocity;
    const float _terminalVelocity = 53f;

    // ── private: timeouts ─────────────────────────────────────────────────────
    float _jumpTimeoutDelta;
    float _fallTimeoutDelta;

    // ── private: animation IDs ────────────────────────────────────────────────
    int _animIDSpeed;
    int _animIDGrounded;
    int _animIDJump;
    int _animIDFreeFall;
    int _animIDMotionSpeed;

    // ── private: components ───────────────────────────────────────────────────
    Animator            _animator;
    bool                _hasAnimator;
    CharacterController _controller;
    StarterAssetsInputs _input;
    GameObject          _mainCamera;

#if ENABLE_INPUT_SYSTEM
    PlayerInput _playerInput;   // optional — only used for device-type detection
#endif

    const float _threshold = 0.01f;

    bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return _playerInput != null &&
                   _playerInput.currentControlScheme == "KeyboardMouse";
#else
            return false;
#endif
        }
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
    }

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _input      = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();   // null-safe — optional
#endif
        // UMA — subscribe before the first build fires.
        if (avatar == null)
            avatar = GetComponentInChildren<DynamicCharacterAvatar>();
        if (avatar == null)
            avatar = FindAnyObjectByType<DynamicCharacterAvatar>();
        if (avatar != null)
            avatar.CharacterUpdated.AddListener(OnCharacterBuilt);

        // Try to grab the animator now; may be null until UMA builds.
        RefreshAnimator();
        AssignAnimationIDs();

        if (CinemachineCameraTarget != null)
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    void OnDestroy()
    {
        if (avatar != null)
            avatar.CharacterUpdated.RemoveListener(OnCharacterBuilt);
    }

    /// <summary>Called every time UMA finishes (re)building the character mesh.</summary>
    void OnCharacterBuilt(UMAData umaData)
    {
        RefreshAnimator();
    }

    /// <summary>
    /// Finds the Animator on the UMA-built child mesh and disables root motion.
    /// Safe to call repeatedly — idempotent.
    /// </summary>
    void RefreshAnimator()
    {
        _animator    = GetComponentInChildren<Animator>();
        _hasAnimator = _animator != null;
        if (_hasAnimator)
            _animator.applyRootMotion = false;
    }

    // ── per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        // Retry until UMA has finished its first build.
        if (!_hasAnimator) RefreshAnimator();

        JumpAndGravity();
        GroundedCheck();
        Move();
    }

    void LateUpdate()
    {
        CameraRotation();
    }

    // ── animation ID cache ────────────────────────────────────────────────────

    void AssignAnimationIDs()
    {
        _animIDSpeed       = Animator.StringToHash("Speed");
        _animIDGrounded    = Animator.StringToHash("Grounded");
        _animIDJump        = Animator.StringToHash("Jump");
        _animIDFreeFall    = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    // ── movement systems ──────────────────────────────────────────────────────

    void GroundedCheck()
    {
        var spherePos = new Vector3(transform.position.x,
                                    transform.position.y - GroundedOffset,
                                    transform.position.z);
        Grounded = Physics.CheckSphere(spherePos, GroundedRadius, GroundLayers,
                                       QueryTriggerInteraction.Ignore);
        if (_hasAnimator)
            _animator.SetBool(_animIDGrounded, Grounded);
    }

    void CameraRotation()
    {
        if (CinemachineCameraTarget == null) return;

        if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            // Mouse input: don't multiply by deltaTime (already per-frame delta).
            // Gamepad input: multiply by deltaTime (value is rate).
            float dtMult = IsCurrentDeviceMouse ? 1f : Time.deltaTime;
            _cinemachineTargetYaw   += _input.look.x * dtMult;
            _cinemachineTargetPitch += _input.look.y * dtMult;
        }

        _cinemachineTargetYaw   = ClampAngle(_cinemachineTargetYaw,   float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp,    TopClamp);

        CinemachineCameraTarget.transform.rotation =
            Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                             _cinemachineTargetYaw, 0f);
    }

    void Move()
    {
        float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
        if (_input.move == Vector2.zero) targetSpeed = 0f;

        float currentHSpeed =
            new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
        const float speedOffset = 0.1f;
        float inputMag = _input.analogMovement ? _input.move.magnitude : 1f;

        if (currentHSpeed < targetSpeed - speedOffset ||
            currentHSpeed > targetSpeed + speedOffset)
        {
            _speed = Mathf.Lerp(currentHSpeed, targetSpeed * inputMag,
                                Time.deltaTime * SpeedChangeRate);
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
        if (_animationBlend < 0.01f) _animationBlend = 0f;

        var inputDir = new Vector3(_input.move.x, 0f, _input.move.y).normalized;

        if (_input.move != Vector2.zero)
        {
            _targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg +
                              (_mainCamera != null ? _mainCamera.transform.eulerAngles.y : 0f);
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation,
                                                   ref _rotationVelocity, RotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        }

        var targetDir = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
        _controller.Move(targetDir.normalized * (_speed * Time.deltaTime) +
                         new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);

        if (_hasAnimator)
        {
            _animator.SetFloat(_animIDSpeed,       _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, inputMag);
        }
    }

    void JumpAndGravity()
    {
        if (Grounded)
        {
            _fallTimeoutDelta = FallTimeout;

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump,     false);
                _animator.SetBool(_animIDFreeFall, false);
            }

            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (_input.jump && _jumpTimeoutDelta <= 0f)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                if (_hasAnimator) _animator.SetBool(_animIDJump, true);
            }

            if (_jumpTimeoutDelta >= 0f)
                _jumpTimeoutDelta -= Time.deltaTime;
        }
        else
        {
            _jumpTimeoutDelta = JumpTimeout;

            if (_fallTimeoutDelta >= 0f)
                _fallTimeoutDelta -= Time.deltaTime;
            else if (_hasAnimator)
                _animator.SetBool(_animIDFreeFall, true);

            _input.jump = false;
        }

        if (_verticalVelocity < _terminalVelocity)
            _verticalVelocity += Gravity * Time.deltaTime;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle >  360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    void OnDrawGizmosSelected()
    {
        var c = Grounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Gizmos.color = c;
        Gizmos.DrawSphere(
            new Vector3(transform.position.x,
                        transform.position.y - GroundedOffset,
                        transform.position.z),
            GroundedRadius);
    }

    // ── animation event receivers (called by animation clips) ─────────────────
    // These are no-ops if no AudioSource is wired — prevents missing-method warnings.

    void OnFootstep(AnimationEvent animationEvent) { }
    void OnLand(AnimationEvent animationEvent)     { }
}
