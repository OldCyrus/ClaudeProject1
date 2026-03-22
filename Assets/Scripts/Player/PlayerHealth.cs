using UnityEngine;

/// <summary>
/// Manages player hit-points, drives the WorldSpaceHealthBar, and handles
/// respawn on death. Does not depend on UMA.
/// </summary>
[RequireComponent(typeof(WorldSpaceHealthBar))]
public class PlayerHealth : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────
    const int MaxHealth = 100;

    // ── State ─────────────────────────────────────────────────────────────────
    int _currentHealth;
    WorldSpaceHealthBar _healthBar;

    // ── Public getter ─────────────────────────────────────────────────────────
    public int CurrentHealth => _currentHealth;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _currentHealth = MaxHealth;

        // GetComponent first (allows pre-existing bar); fall back to AddComponent.
        _healthBar = GetComponent<WorldSpaceHealthBar>();
        if (_healthBar == null)
            _healthBar = gameObject.AddComponent<WorldSpaceHealthBar>();

        _healthBar.Initialize(MaxHealth, Color.green, 2.4f);
        _healthBar.UpdateBar(_currentHealth, MaxHealth);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduce health by <paramref name="amount"/>. Triggers respawn at zero.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _healthBar?.UpdateBar(_currentHealth, MaxHealth);

        if (_currentHealth <= 0)
            Respawn();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Respawn()
    {
        // Collect all spawn points tagged "SpawnPoint"
        GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnObjects != null && spawnObjects.Length > 0)
        {
            int idx = Random.Range(0, spawnObjects.Length);
            Transform spawnPoint = spawnObjects[idx].transform;

            // Disable CharacterController so we can teleport cleanly
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            if (cc != null) cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] No GameObjects tagged 'SpawnPoint' found in scene.");
        }

        // Restore full health
        _currentHealth = MaxHealth;
        _healthBar?.UpdateBar(_currentHealth, MaxHealth);
    }
}
