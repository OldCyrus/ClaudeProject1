using UnityEngine;
using Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Bridges Unity's new Input System to Cinemachine's axis provider.
/// Replaces CinemachineCore.GetInputAxis so FreeLook cameras read
/// Mouse.current.delta instead of the disabled legacy Input class.
///
/// Attach to any persistent GameObject — PlayerSetup puts it on the Player.
/// </summary>
public class CinemachineInputBridge : MonoBehaviour
{
    [Header("Mouse Sensitivity")]
    [Range(0.01f, 5f)] public float sensitivityX = 1.0f;
    [Range(0.01f, 5f)] public float sensitivityY = 0.8f;
    [Tooltip("Flip vertical orbit direction.")]
    public bool invertY = false;

    // Scale converts raw pixel delta → Cinemachine axis units.
    // FreeLook multiplies: returnedValue * MaxSpeed * deltaTime → degrees/frame.
    // At 60 fps, 10 px/frame delta, k=0.05, MaxSpeed=300 → ~2.4°/frame  (fine default).
    const float k = 0.05f;

    // Preserve whatever was set before we take over, so OnDisable restores it cleanly.
    CinemachineCore.AxisInputDelegate _previousHandler;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _previousHandler              = CinemachineCore.GetInputAxis;
        CinemachineCore.GetInputAxis  = ReadAxis;
    }

    void OnDisable()
    {
        // Only restore if we are still the active handler (guard against double-disable).
        if (CinemachineCore.GetInputAxis == ReadAxis)
            CinemachineCore.GetInputAxis = _previousHandler;
    }

    // ── Axis reader ───────────────────────────────────────────────────────────

    float ReadAxis(string axisName)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return 0f;

        // No camera rotation while the cursor is unlocked (menu / debug mode).
        if (Cursor.lockState != CursorLockMode.Locked) return 0f;

        var delta = Mouse.current.delta.ReadValue();
        switch (axisName)
        {
            case "Mouse X": return  delta.x * sensitivityX * k;
            case "Mouse Y": return  delta.y * sensitivityY * k * (invertY ? 1f : -1f);
            default:        return  0f;
        }
#else
        // This branch is dead-code in "New Input System Only" projects.
        return 0f;
#endif
    }
}
