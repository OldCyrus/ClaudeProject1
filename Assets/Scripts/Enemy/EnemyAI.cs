using UnityEngine;

/// <summary>
/// CharacterController-based enemy AI — no NavMesh required.
///
/// States:
///   Patrol  — walks back and forth along its corridor, flipping when a wall is detected
///   Chase   — moves directly toward the player (CharacterController slides around walls)
///   Attack  — stops moving, faces player, swings bat every attackCooldown seconds
///   Pushed  — controlled externally by EnemyPushable
///   Dead    — nothing runs
///
/// Detection uses a line-of-sight raycast only (no proximity trigger).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    // ── State ─────────────────────────────────────────────────────────────────
    public enum State { Patrol, Chase, Attack, Pushed, Dead }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Detection")]
    [SerializeField] float detectionRange = 15f;
    [SerializeField] float fieldOfView    = 110f;

    [Header("Movement")]
    [SerializeField] float patrolSpeed   = 1.8f;
    [SerializeField] float chaseSpeed    = 4f;
    [SerializeField] float wallCheckDist = 1.2f;   // how far ahead to detect walls

    [Header("Combat")]
    [SerializeField] float attackRange    = 1.2f;
    [SerializeField] float attackCooldown = 2f;

    [Header("Chase")]
    [SerializeField] float loseSightDelay = 10f;

    // ── Component refs ────────────────────────────────────────────────────────
    CharacterController _cc;
    Animator            _animator;
    Transform           _player;
    EnemyHealth         _health;
    BaseballBat         _bat;

    // ── State ─────────────────────────────────────────────────────────────────
    State _state = State.Patrol;

    // Patrol
    Vector3 _patrolDir;          // current patrol direction (world space, Y=0)

    // Chase / sight loss timer
    float _loseSightTimer;

    // Attack
    float _lastAttackTime;

    // Physics
    float _verticalVelocity;

    // UMA ready gate — set true by EnemySetup.OnCharacterBuilt
    [System.NonSerialized] public bool isReady;

    // ── Animator hashes ───────────────────────────────────────────────────────
    static readonly int _hashSpeed       = Animator.StringToHash("Speed");
    static readonly int _hashMotionSpeed = Animator.StringToHash("MotionSpeed");
    static readonly int _hashGrounded    = Animator.StringToHash("Grounded");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by EnemySetup once the UMA avatar has finished building.</summary>
    public void OnCharacterReady()
    {
        isReady   = true;
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    public State CurrentState     => _state;
    public void  SetPushedState() => _state = State.Pushed;
    public void  SetPatrolState() => EnterPatrol();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        _health = GetComponent<EnemyHealth>();
        _bat    = GetComponentInChildren<BaseballBat>();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _player = playerGO.transform;

        // Start patrolling in the direction the spawn point faces
        _patrolDir = transform.forward;
        _patrolDir.y = 0f;
        if (_patrolDir == Vector3.zero) _patrolDir = Vector3.forward;
    }

    void Update()
    {
        if (!isReady) return;
        if (_health != null && _health.IsDead) { _state = State.Dead; return; }

        ApplyGravity();

        switch (_state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
            case State.Pushed: /* EnemyPushable handles movement */ ApplyGravity(); break;
            case State.Dead:   break;
        }
    }

    // ── Gravity ───────────────────────────────────────────────────────────────

    void ApplyGravity()
    {
        if (_cc == null || !_cc.enabled) return;
        if (_cc.isGrounded)
            _verticalVelocity = -0.5f;
        else
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;
    }

    // ── Move helper — applies horizontal + vertical via CharacterController ───

    void MoveHorizontal(Vector3 horizontalDir, float speed)
    {
        if (_cc == null || !_cc.enabled) return;
        Vector3 move = horizontalDir.normalized * speed + Vector3.up * _verticalVelocity;
        _cc.Move(move * Time.deltaTime);
    }

    // ── State: Patrol ─────────────────────────────────────────────────────────

    void UpdatePatrol()
    {
        if (_player != null && CanSeePlayer()) { EnterChase(); return; }

        // Wall / obstacle check — raycast from chest height ahead
        Vector3 chest = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(chest, _patrolDir, wallCheckDist))
        {
            // Reverse direction
            _patrolDir = -_patrolDir;
            transform.rotation = Quaternion.LookRotation(_patrolDir);
        }

        // Face patrol direction smoothly
        if (_patrolDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(_patrolDir),
                Time.deltaTime * 6f);

        MoveHorizontal(_patrolDir, patrolSpeed);
        SetAnimatorLocomotion(patrolSpeed, 1f, true);
    }

    // ── State: Chase ──────────────────────────────────────────────────────────

    void UpdateChase()
    {
        if (_player == null) { EnterPatrol(); return; }

        bool sees = CanSeePlayer();

        if (sees)
            _loseSightTimer = loseSightDelay;
        else
        {
            _loseSightTimer -= Time.deltaTime;
            if (_loseSightTimer <= 0f) { EnterPatrol(); return; }
        }

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist <= attackRange) { EnterAttack(); return; }

        // Move toward player
        Vector3 dir = (_player.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 10f);
            MoveHorizontal(dir, chaseSpeed);
        }

        SetAnimatorLocomotion(chaseSpeed, 1f, true);
    }

    // ── State: Attack ─────────────────────────────────────────────────────────

    void UpdateAttack()
    {
        if (_player == null) { EnterPatrol(); return; }

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > attackRange * 1.5f) { EnterChase(); return; }

        // Face player
        Vector3 dir = (_player.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 8f);

        SetAnimatorLocomotion(0f, 1f, true);

        // Apply gravity — UpdateAttack does not call MoveHorizontal so we must
        // move the CC explicitly, otherwise enemies float while in attack stance.
        if (_cc != null && _cc.enabled)
            _cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

        if (Time.time - _lastAttackTime >= attackCooldown)
        {
            _lastAttackTime = Time.time;
            _bat?.EnemySwing();
        }
    }

    // ── State transitions ─────────────────────────────────────────────────────

    void EnterPatrol()
    {
        _state = State.Patrol;
        // Keep current patrol dir (may have been set during chase)
        if (_patrolDir == Vector3.zero) _patrolDir = transform.forward;
        _patrolDir.y = 0f;
    }

    void EnterChase()
    {
        _state          = State.Chase;
        _loseSightTimer = loseSightDelay;
    }

    void EnterAttack()
    {
        _state          = State.Attack;
        _lastAttackTime = Time.time - attackCooldown;   // allow immediate first swing
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    bool CanSeePlayer()
    {
        if (_player == null) return false;

        Vector3 eyePos   = transform.position + Vector3.up * 1.6f;
        Vector3 toPlayer = (_player.position + Vector3.up * 1f) - eyePos;
        float   distance = toPlayer.magnitude;

        if (distance > detectionRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > fieldOfView * 0.5f) return false;

        // Raycast — first object hit must be the player (not a wall)
        if (Physics.Raycast(eyePos, toPlayer.normalized, out RaycastHit hit, detectionRange))
        {
            return hit.transform == _player || hit.transform.IsChildOf(_player);
        }

        return false;
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    void SetAnimatorLocomotion(float speed, float motionSpeed, bool grounded)
    {
        if (_animator == null) return;
        _animator.SetFloat(_hashSpeed,       speed);
        _animator.SetFloat(_hashMotionSpeed, motionSpeed);
        _animator.SetBool (_hashGrounded,    grounded);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up, _patrolDir * wallCheckDist);
    }
}
