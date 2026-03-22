using System.Collections;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Knocks back an enemy when the player walks into them at speed.
///
/// Detection uses proximity polling in Update() rather than OnTriggerEnter.
///
/// Fix 1 — Distance: uses horizontal (XZ) distance only. The player CC has
/// center=(0,0,0) and the enemy CC has center=(0,0.9,0), so their Y pivots
/// differ; 3D distance inflated the reading significantly.
///
/// Fix 2 — Speed: CharacterController.velocity reports the *actual* displacement
/// divided by deltaTime. When the player pushes into the enemy's CC they get
/// blocked, displacement → 0, and velocity reads zero even while sprinting.
/// We instead read StarterAssetsInputs.move.magnitude which reflects the
/// player's *intent* to move — non-zero the moment they press a key, regardless
/// of whether the CC is blocked.
/// </summary>
public class EnemyPushable : MonoBehaviour
{
    [SerializeField] float knockbackForce  = 8f;
    [SerializeField] float recoverTime     = 7f;
    [SerializeField] float minInputToPush  = 0.1f;   // move input magnitude threshold (replaces speed check)
    [SerializeField] float proximityRadius = 1.1f;   // horizontal distance threshold

    bool _isPushed    = false;
    bool _canBePushed = true;

    Transform            _playerTransform;
    StarterAssetsInputs  _playerInputs;

    public bool CanBePushed => _canBePushed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        Debug.Log("EnemyPushable Start called on " + gameObject.name);

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
            _playerInputs    = playerGO.GetComponent<StarterAssetsInputs>();
            Debug.Log($"EnemyPushable {gameObject.name}: player found={_playerTransform != null}, inputs found={_playerInputs != null}");
        }
        else
        {
            Debug.LogWarning("EnemyPushable: Player tag not found on " + gameObject.name);
        }
    }

    int _debugFrame = 0;

    void Update()
    {
        if (++_debugFrame % 60 == 0 && _playerTransform != null)
        {
            Vector3 ep = transform.position;
            Vector3 pp = _playerTransform.position;
            Debug.Log($"{gameObject.name} enemyPos=({ep.x:F2},{ep.y:F2},{ep.z:F2}) playerPos=({pp.x:F2},{pp.y:F2},{pp.z:F2})");

            Vector3 toPlayer = pp - ep;
            toPlayer.y = 0f;
            float hDist      = toPlayer.magnitude;
            float inputMag   = _playerInputs != null ? _playerInputs.move.magnitude : -1f;
            Debug.Log($"[EnemyPushable] {gameObject.name} hDist={hDist:F2} (threshold={proximityRadius}), inputMag={inputMag:F2} (min={minInputToPush})");
        }

        if (!_canBePushed) return;
        if (_playerTransform == null || _playerInputs == null) return;

        // Horizontal (XZ) distance only — Y pivots differ between player and enemy
        Vector3 offset = _playerTransform.position - transform.position;
        offset.y = 0f;
        float dist = offset.magnitude;
        if (dist > proximityRadius) return;

        // Use input magnitude instead of CC velocity — velocity is zero when blocked
        if (_playerInputs.move.magnitude < minInputToPush) return;

        // Push direction = from enemy toward player (horizontal)
        Vector3 pushDir = offset.magnitude > 0.001f ? offset.normalized : transform.forward;
        Debug.Log($"[EnemyPushable] PUSH triggered on {gameObject.name} — hDist={dist:F2}, input={_playerInputs.move.magnitude:F2}");
        Push(pushDir);
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
