using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Detects nearby interactable objects (tagged "Door" or "Interactable") and
/// lets the player interact with them via the E key.
/// Implements IDoor / IInteractable interfaces for extensible door/item logic.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public float   interactRange = 2.5f;
    public LayerMask interactMask = ~0;

    // ── State ─────────────────────────────────────────────────────────────────
    string    _promptText = "";
    GUIStyle  _promptStyle;
    bool      _hasKey; // rudimentary key-hold flag (replace with inventory later)

    // ── New Input System ───────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
    bool _interactPressed;
    public void OnInteract(InputValue v) { if (v.isPressed) _interactPressed = true; }
#endif

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Update()
    {
        _promptText = ScanForInteractable();

#if ENABLE_INPUT_SYSTEM
        if (_interactPressed)
        {
            _interactPressed = false;
            TryInteract();
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
            TryInteract();
#endif
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    string ScanForInteractable()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, interactRange, interactMask);
        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;
            if (col.CompareTag("Door"))        return "[E]  Open Door";
            if (col.CompareTag("Interactable")) return "[E]  Pick Up";
        }
        return "";
    }

    // ── Interact ──────────────────────────────────────────────────────────────

    void TryInteract()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, interactRange, interactMask);
        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;

            if (col.CompareTag("Door"))
            {
                HandleDoor(col.gameObject);
                return;
            }
            if (col.CompareTag("Interactable"))
            {
                HandleInteractable(col.gameObject);
                return;
            }
        }
    }

    void HandleDoor(GameObject door)
    {
        // Interface first — door script can override behaviour.
        var iDoor = door.GetComponent<IDoor>();
        if (iDoor != null) { iDoor.Open(); return; }

        // Fallback: disable collider + hide object (placeholder until door script exists).
        var col = door.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        door.SetActive(false);
        Debug.Log($"[Interaction] Door '{door.name}' opened (fallback).");
    }

    void HandleInteractable(GameObject item)
    {
        // Interface first.
        var iItem = item.GetComponent<IInteractable>();
        if (iItem != null) { iItem.Interact(gameObject); return; }

        // Key pickup fallback.
        if (item.name.Contains("Key"))
        {
            _hasKey = true;
            item.SetActive(false);
            Debug.Log($"[Interaction] {gameObject.name} picked up the key!");
            // Expand: GameManager.Instance?.NotifyKeyPickup(gameObject);
            return;
        }

        Debug.Log($"[Interaction] Interacted with '{item.name}'.");
    }

    // ── HUD Prompt ────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (string.IsNullOrEmpty(_promptText)) return;

        _promptStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };

        float w = 220f, h = 30f;
        GUI.Label(
            new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h),
            _promptText,
            _promptStyle);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }

    // ── Public accessor ───────────────────────────────────────────────────────

    public bool HasKey => _hasKey;
}

// ── Interfaces ── implement these on door/item GameObjects ──────────────────

public interface IDoor
{
    void Open();
    void Close();
}

public interface IInteractable
{
    void Interact(GameObject interactor);
}
