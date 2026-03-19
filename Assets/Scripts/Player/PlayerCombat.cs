using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles shooting (LMB raycast) and melee (RMB sphere hitbox).
/// Wires into PlayerStats.TakeDamage on hit targets.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Shoot")]
    public float shootRange  = 60f;
    public int   shootDamage = 25;
    public float fireRate    = 0.2f;   // seconds between shots

    [Header("Melee")]
    public float meleeRange    = 2.2f;
    public int   meleeDamage   = 40;
    public float meleeRate     = 0.6f;
    [Tooltip("How long the hitbox stays active after swinging.")]
    public float meleeHitboxDuration = 0.18f;

    [Header("VFX (optional)")]
    [Tooltip("Assign a muzzle flash particle prefab — left empty is fine for now.")]
    public GameObject muzzleFlashPrefab;

    // ── State ────────────────────────────────────────────────────────────────
    float     _nextFireTime;
    float     _nextMeleeTime;
    bool      _meleeSwingActive;
    float     _meleeTimer;
    Transform _cam;
    LineRenderer _shootLine;

    // ── New Input System ─────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
    bool _fireHeld;
    bool _meleePressed;

    public void OnFire(InputValue v)  => _fireHeld    = v.isPressed;
    public void OnMelee(InputValue v) { if (v.isPressed) TryMelee(); }
#endif

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam = Camera.main?.transform;
        BuildShootLine();
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main?.transform;

        // ── Shoot ─────────────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
        bool firing = _fireHeld;
#else
        bool firing = Input.GetMouseButton(0);
#endif
        if (firing && Time.time >= _nextFireTime)
            Shoot();

        // ── Melee ─────────────────────────────────────────────────────────────
#if !ENABLE_INPUT_SYSTEM
        if (Input.GetMouseButtonDown(1) && Time.time >= _nextMeleeTime)
            TryMelee();
#endif

        if (_meleeSwingActive)
        {
            _meleeTimer -= Time.deltaTime;
            if (_meleeTimer <= 0f) EndMeleeSwing();
        }
    }

    // ── Shoot ────────────────────────────────────────────────────────────────

    void Shoot()
    {
        _nextFireTime = Time.time + fireRate;

        Vector3 origin    = _cam != null ? _cam.position    : transform.position + Vector3.up * 1.5f;
        Vector3 direction = _cam != null ? _cam.forward : transform.forward;

        Vector3 endPoint = origin + direction * shootRange;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, shootRange))
        {
            endPoint = hit.point;
            ApplyDamage(hit.collider.gameObject, shootDamage);
        }

        if (muzzleFlashPrefab != null)
            Destroy(Instantiate(muzzleFlashPrefab, origin, Quaternion.identity), 0.1f);

        StartCoroutine(FlashLine(origin, endPoint));
    }

    IEnumerator FlashLine(Vector3 from, Vector3 to)
    {
        _shootLine.enabled = true;
        _shootLine.SetPosition(0, from);
        _shootLine.SetPosition(1, to);
        yield return new WaitForSeconds(0.04f);
        _shootLine.enabled = false;
    }

    // ── Melee ────────────────────────────────────────────────────────────────

    void TryMelee()
    {
        if (Time.time < _nextMeleeTime) return;
        _nextMeleeTime   = Time.time + meleeRate;
        _meleeSwingActive = true;
        _meleeTimer      = meleeHitboxDuration;
        // Animation trigger goes here once an Animator is added.
    }

    void EndMeleeSwing()
    {
        _meleeSwingActive = false;

        // Hitbox is a sphere slightly in front of the player.
        Vector3 hitCenter = transform.position
                          + transform.forward * (meleeRange * 0.6f)
                          + Vector3.up * 0.8f;

        Collider[] cols = Physics.OverlapSphere(hitCenter, meleeRange * 0.5f);
        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;
            ApplyDamage(col.gameObject, meleeDamage);
        }
    }

    // ── Shared damage dispatch ────────────────────────────────────────────────

    static void ApplyDamage(GameObject target, int amount)
    {
        target.GetComponent<PlayerStats>()?.TakeDamage(amount);
        // Expand: target.GetComponent<EnemyHealth>()?.TakeDamage(amount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void BuildShootLine()
    {
        GameObject lg = new GameObject("ShootLine");
        lg.transform.SetParent(transform);
        _shootLine = lg.AddComponent<LineRenderer>();
        _shootLine.positionCount = 2;
        _shootLine.startWidth    = 0.025f;
        _shootLine.endWidth      = 0.006f;
        _shootLine.useWorldSpace = true;

        Material m = new Material(Shader.Find("Sprites/Default"));
        m.color              = Color.yellow;
        _shootLine.material  = m;
        _shootLine.startColor = new Color(1f, 0.95f, 0.3f, 1f);
        _shootLine.endColor   = new Color(1f, 0.95f, 0.3f, 0f);
        _shootLine.enabled   = false;
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Melee hitbox preview.
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Vector3 c = transform.position + transform.forward * (meleeRange * 0.6f) + Vector3.up * 0.8f;
        Gizmos.DrawWireSphere(c, meleeRange * 0.5f);
    }
}
