using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lightweight fallback orbit camera — used when Cinemachine is not present.
/// When Cinemachine is active (CinemachineBrain on Main Camera) this script
/// is NOT attached to the camera, so it costs nothing at runtime.
///
/// Supports both the new Input System and the legacy Input class via #if guards.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Tooltip("World-space offset applied to the look-at point (raise to shoulder height).")]
    public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Distance")]
    public float distance    = 6f;
    public float minDistance = 1.5f;
    public float maxDistance = 12f;
    public float zoomSpeed   = 4f;

    [Header("Orbit")]
    public float sensitivityX = 180f;
    public float sensitivityY = 120f;
    public float minPitch     = -15f;
    public float maxPitch     =  55f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.07f;
    public bool  lockCursor         = true;

    // ── State ────────────────────────────────────────────────────────────────
    float   _yaw;
    float   _pitch = 12f;
    Vector3 _smoothVel;

    // ── Init ─────────────────────────────────────────────────────────────────

    void Start()
    {
        SetCursorLock(lockCursor);
        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    // ── LateUpdate ────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (target == null) return;
        HandleCursorToggle();
        UpdateOrbit();
        UpdatePosition();
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    void HandleCursorToggle()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLock(Cursor.lockState != CursorLockMode.Locked);
#else
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLock(Cursor.lockState != CursorLockMode.Locked);
#endif
    }

    void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    // ── Orbit ─────────────────────────────────────────────────────────────────

    void UpdateOrbit()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            var delta = Mouse.current.delta.ReadValue();
            _yaw   += delta.x * sensitivityX * 0.01f;
            _pitch -= delta.y * sensitivityY * 0.01f;

            // Scroll to zoom.
            if (Mouse.current.scroll.IsActuated())
                distance -= Mouse.current.scroll.ReadValue().y * zoomSpeed * 0.01f;
        }
#else
        _yaw   += Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;
        _pitch -= Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime;
        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
#endif

        _pitch   = Mathf.Clamp(_pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    // ── Position ──────────────────────────────────────────────────────────────

    void UpdatePosition()
    {
        Vector3    pivot      = target.position + pivotOffset;
        Quaternion rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    desiredPos = pivot - rotation * Vector3.forward * distance;

        // Wall push-in.
        if (Physics.Linecast(pivot, desiredPos, out RaycastHit hit,
            ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            desiredPos = hit.point + hit.normal * 0.15f;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref _smoothVel, positionSmoothTime);

        transform.LookAt(pivot);
    }
}
