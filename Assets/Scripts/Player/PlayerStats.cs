using System;
using UnityEngine;

/// <summary>
/// Tracks player health and handles death/respawn logic.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Respawn")]
    [Tooltip("Leave empty — spawn points are found automatically by name prefix 'SpawnPoint'.")]
    public Transform[] spawnPoints;

    // Invincibility flag — set true by PlayerMovement during a dodge roll.
    public bool IsInvincible { get; set; }

    // Broadcast to UI, game manager, etc.
    public event Action<int, int> OnHealthChanged;   // (current, max)
    public event Action            OnDeath;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (IsInvincible || amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0) Die();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ── Internal ────────────────────────────────────────────────────────────

    void Die()
    {
        OnDeath?.Invoke();
        // Small delay so death effects can play before teleport (expand later).
        Respawn();
    }

    void Respawn()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Transform target = PickRandomSpawn();
        if (target == null) return;

        // Move the CharacterController without fighting physics.
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(target.position, target.rotation);
            cc.enabled = true;
        }
        else
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }

    Transform PickRandomSpawn()
    {
        // Use manually assigned array first.
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        // Auto-discover by name prefix.
        var found = new System.Collections.Generic.List<Transform>();
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.StartsWith("SpawnPoint"))
                found.Add(go.transform);

        return found.Count > 0
            ? found[UnityEngine.Random.Range(0, found.Count)]
            : null;
    }
}
