using System.Collections;
using UnityEngine;

/// <summary>
/// Knocks back an enemy when the player walks into them at speed.
///
/// Detection uses proximity polling in Update() rather than OnTriggerEnter.
/// OnTriggerEnter was unreliable because the enemy has a CharacterController
/// and a Rigidbody on the same root — an unsupported Unity combination that
/// silently breaks trigger callbacks.
///
/// Detects the player by PlayerStats component (confirmed present on the
/// Player GameObject). Requires horizontal speed >= minSpeedToPush.
/// </summary>
public class EnemyPushable : MonoBehaviour
{
    [SerializeField] float knockbackForce  = 8f;
    [SerializeField] float recoverTime     = 7f;
    [SerializeField] float minSpeedToPush  = 0.5f;
    [SerializeField] float proximityRadius = 0.9f;   // distance at which player contact is detected

    bool _isPushed    = false;
    bool _canBePushed = true;

    CharacterController _playerCC;

    public bool CanBePushed => _canBePushed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        Debug.Log("EnemyPushable Start called on " + gameObject.name);

        // Cache the player's CharacterController once at startup
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _playerCC = playerGO.GetComponent<CharacterController>();
        else
            Debug.LogWarning("EnemyPushable: Player tag not found on " + gameObject.name);
    }

    int _debugFrame = 0;

    void Update()
    {
        if (++_debugFrame % 60 == 0)
            Debug.Log("EnemyPushable Update running on " + gameObject.name);

        if (!_canBePushed) return;
        if (_playerCC == null) return;

        float dist = Vector3.Distance(transform.position, _playerCC.transform.position);
        if (dist > proximityRadius) return;

        Vector3 vel = _playerCC.velocity;
        vel.y = 0f;
        float speed = vel.magnitude;

        if (speed < minSpeedToPush) return;

        Debug.Log($"[EnemyPushable] PUSH triggered on {gameObject.name} — player speed: {speed:F2}, dist: {dist:F2}");
        Push(vel.normalized);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Push(Vector3 direction)
    {
        if (!_canBePushed) return;

        _isPushed    = true;
        _canBePushed = false;

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.isReady = false;
            ai.SetPushedState();
        }

        GetComponentInChildren<BaseballBat>()?.Drop(direction);

        StartCoroutine(ApplyKnockback(direction));
        StartCoroutine(Recover());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator ApplyKnockback(Vector3 direction)
    {
        float elapsed  = 0f;
        float duration = 0.4f;
        var   cc       = GetComponent<CharacterController>();
        Vector3 knockDir = direction.normalized;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            if (cc != null && cc.enabled)
                cc.Move((knockDir * knockbackForce + Physics.gravity) * dt);
            yield return null;
        }
    }

    IEnumerator Recover()
    {
        yield return new WaitForSeconds(0.4f);

        _isPushed = false;

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.isReady = true;
            ai.SetPatrolState();
        }

        yield return new WaitForSeconds(recoverTime);

        _canBePushed = true;
    }
}
