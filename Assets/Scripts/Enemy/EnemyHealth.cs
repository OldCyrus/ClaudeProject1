using System.Collections;
using UnityEngine;

/// <summary>
/// Manages enemy hit-points, drives the WorldSpaceHealthBar, and handles
/// the death sink-and-destroy sequence.
/// </summary>
[RequireComponent(typeof(WorldSpaceHealthBar))]
public class EnemyHealth : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────
    const int MaxHealth = 50;

    // ── State ─────────────────────────────────────────────────────────────────
    int  _currentHealth;
    bool _isDead;
    WorldSpaceHealthBar _healthBar;

    // ── Public getters ────────────────────────────────────────────────────────
    public int  CurrentHealth => _currentHealth;
    public bool IsDead        => _isDead;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _currentHealth = MaxHealth;

        _healthBar = GetComponent<WorldSpaceHealthBar>();
        if (_healthBar == null)
            _healthBar = gameObject.AddComponent<WorldSpaceHealthBar>();

        _healthBar.Initialize(MaxHealth, Color.red, 2.4f);
        _healthBar.UpdateBar(_currentHealth, MaxHealth);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduce health by <paramref name="amount"/>. Triggers Die() at zero.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _healthBar?.UpdateBar(_currentHealth, MaxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Disable AI behaviour
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        // Sink into ground then destroy
        StartCoroutine(SinkAndDestroy());
    }

    IEnumerator SinkAndDestroy()
    {
        float elapsed  = 0f;
        float duration = 1.5f;
        Vector3 startPos = transform.position;
        // Sink by approximately 2 world units (enough to disappear below ground)
        Vector3 endPos = startPos + Vector3.down * 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
