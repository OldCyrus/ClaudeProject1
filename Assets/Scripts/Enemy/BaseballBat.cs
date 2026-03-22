using UnityEngine;

/// <summary>
/// Baseball bat that can be held by an enemy, dropped on the ground, or
/// picked up and wielded by the player.
/// </summary>
public class BaseballBat : MonoBehaviour
{
    // ── Enums ─────────────────────────────────────────────────────────────────
    public enum BatState { EnemyHeld, Dropped, PlayerHeld }

    // ── Fields ────────────────────────────────────────────────────────────────
    [Header("Combat")]
    [SerializeField] int   damage        = 10;
    [SerializeField] float swingCooldown = 0.5f;

    [Header("Swing geometry")]
    [SerializeField] float swingReach  = 1.5f;
    [SerializeField] float swingRadius = 0.4f;

    // State
    BatState  _state = BatState.EnemyHeld;

    // Holders
    Transform _enemyHolder;   // set by EnemySetup
    Transform _playerHolder;  // set when picked up by player

    // Components
    Rigidbody _rb;
    Collider  _pickupCollider;

    // Timing
    float _lastSwing;

    // ── Public getters ────────────────────────────────────────────────────────
    public BatState State => _state;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Ensure Rigidbody exists (kinematic by default until dropped)
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;

        // Create sphere trigger collider for pickup detection
        // Only active when state == Dropped
        var sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius    = 0.8f;
        sphere.enabled   = false;
        _pickupCollider  = sphere;
    }

    void LateUpdate()
    {
        switch (_state)
        {
            case BatState.EnemyHeld:
                if (_enemyHolder != null)
                {
                    transform.position = _enemyHolder.position;
                    transform.rotation = _enemyHolder.rotation;
                }
                break;

            case BatState.PlayerHeld:
                if (_playerHolder != null)
                {
                    // Position slightly in front and to the right of the player's shoulder
                    transform.position = _playerHolder.position
                        + _playerHolder.right   * 0.35f
                        + _playerHolder.up      * 1.1f
                        + _playerHolder.forward * 0.3f;
                    transform.rotation = _playerHolder.rotation;
                }
                break;
        }
    }

    void Update()
    {
        if (_state == BatState.PlayerHeld)
        {
            if (Input.GetMouseButtonDown(0))
                PlayerSwing();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Assign the enemy bone/anchor that holds the bat.</summary>
    public void SetEnemyHolder(Transform holder)
    {
        _enemyHolder = holder;
    }

    /// <summary>Called by EnemyAI to swing at the player.</summary>
    public void EnemySwing()
    {
        if (Time.time - _lastSwing < swingCooldown) return;
        _lastSwing = Time.time;

        Vector3 origin    = transform.position;
        Vector3 direction = transform.forward;

        if (Physics.SphereCast(origin, swingRadius, direction, out RaycastHit hit, swingReach))
        {
            var ph = hit.collider.GetComponentInParent<PlayerHealth>();
            ph?.TakeDamage(damage);
        }
    }

    /// <summary>Drop the bat in a direction (e.g. when enemy is knocked back).</summary>
    public void Drop(Vector3 knockDirection)
    {
        _state = BatState.Dropped;

        transform.SetParent(null);

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.AddForce(knockDirection.normalized * 4f + Vector3.up * 3f, ForceMode.Impulse);
        }

        if (_pickupCollider != null)
            _pickupCollider.enabled = true;
    }

    /// <summary>Pick up the bat (called when player walks over it).</summary>
    public void PickUp(Transform playerTransform)
    {
        _state         = BatState.PlayerHeld;
        _playerHolder  = playerTransform;

        transform.SetParent(null); // keep in world space; LateUpdate will follow player

        if (_rb != null)
            _rb.isKinematic = true;

        if (_pickupCollider != null)
            _pickupCollider.enabled = false;
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_state != BatState.Dropped) return;

        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph == null) return;

        // Don't pick up if the player is already holding a bat
        var existingBat = other.GetComponentInParent<BaseballBat>();
        if (existingBat != null && existingBat != this) return;

        PickUp(other.transform);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    void PlayerSwing()
    {
        if (Time.time - _lastSwing < swingCooldown) return;
        _lastSwing = Time.time;

        Vector3 origin    = transform.position + Vector3.up * 0.5f;
        Vector3 direction = _playerHolder != null ? _playerHolder.forward : transform.forward;

        RaycastHit[] hits = Physics.SphereCastAll(origin, swingRadius, direction, swingReach);
        foreach (var hit in hits)
        {
            // Hit enemy
            var eh = hit.collider.GetComponentInParent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(damage);
                continue;
            }

            // Hit another player (not self)
            var ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null && ph.gameObject != (_playerHolder != null ? _playerHolder.gameObject : gameObject))
            {
                ph.TakeDamage(damage);
            }
        }
    }
}
