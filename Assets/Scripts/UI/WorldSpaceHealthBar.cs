using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a world-space health bar that floats above a character and always
/// faces the main camera (billboard). Call Initialize() from Start() of the
/// owning health component.
/// </summary>
public class WorldSpaceHealthBar : MonoBehaviour
{
    // ── Runtime refs ──────────────────────────────────────────────────────────
    Canvas    _canvas;
    Image     _fillImage;
    Transform _canvasTransform;
    float     _heightOffset;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the health-bar canvas. Must be called once before any UpdateBar calls.
    /// </summary>
    /// <param name="maxHp">Maximum HP value (used for initial full-bar display).</param>
    /// <param name="fillColor">Color of the filled portion.</param>
    /// <param name="heightOffset">World-space Y offset above this transform's origin.</param>
    public void Initialize(float maxHp, Color fillColor, float heightOffset)
    {
        _heightOffset = heightOffset;

        // ── Root canvas GO ────────────────────────────────────────────────────
        var canvasGO = new GameObject("HealthBarCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);

        _canvasTransform = canvasGO.transform;

        // World-space canvas
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        // Canvas size in world units: 1.2 wide × 0.15 tall at scale 0.01
        // RectTransform pixel size = 120 × 15  →  at scale 0.01 = 1.2 × 0.15 world units
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(120f, 15f);
        canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // ── Background (black) ────────────────────────────────────────────────
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.black;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin        = Vector2.zero;
        bgRT.anchorMax        = Vector2.one;
        bgRT.offsetMin        = Vector2.zero;
        bgRT.offsetMax        = Vector2.zero;

        // ── Fill image ────────────────────────────────────────────────────────
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        _fillImage = fillGO.AddComponent<Image>();
        _fillImage.color = fillColor;
        _fillImage.type  = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillAmount = 1f;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin        = Vector2.zero;
        fillRT.anchorMax        = Vector2.one;
        fillRT.offsetMin        = new Vector2(1f, 1f);   // 1-pixel inset
        fillRT.offsetMax        = new Vector2(-1f, -1f);
    }

    /// <summary>
    /// Update the displayed fill fraction.
    /// </summary>
    public void UpdateBar(float current, float max)
    {
        if (_fillImage == null) return;
        _fillImage.fillAmount = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_canvasTransform == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Billboard: face the camera
        _canvasTransform.rotation = Quaternion.LookRotation(
            _canvasTransform.position - cam.transform.position);
    }
}
