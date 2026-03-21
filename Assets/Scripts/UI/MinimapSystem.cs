using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained debug minimap.
/// Add to any persistent GameObject in the scene (or let SetupMinimap.cs do it).
///
/// At Start() this script creates:
///   • An orthographic top-down Camera that renders to a RenderTexture
///   • A Canvas with a RawImage (200×200, top-right) showing that texture
///   • A small dot Image that tracks the player in minimap space
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("How many world units the minimap shows from centre to edge.")]
    public float orthographicSize = 55f;

    [Tooltip("Height above Y=0 the minimap camera sits.")]
    public float cameraHeight = 120f;

    [Tooltip("Centre of the area to cover (world XZ).")]
    public Vector2 worldCentre = new Vector2(0f, -28f);

    [Header("Display")]
    public int   displaySize  = 200;   // pixels
    public int   textureSize  = 256;   // render texture resolution

    [Header("Player dot")]
    public float dotSize = 10f;        // pixels
    public Color dotColor = Color.red;

    // ── Runtime refs ──────────────────────────────────────────────────────────
    Camera       _miniCam;
    RenderTexture _rt;
    RawImage     _mapImage;
    RectTransform _dot;
    Transform    _playerTransform;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        BuildCamera();
        BuildUI();
        FindPlayer();
    }

    void LateUpdate()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
            return;
        }
        UpdateDot();
    }

    // ── Camera setup ──────────────────────────────────────────────────────────

    void BuildCamera()
    {
        var camGO = new GameObject("MinimapCamera");
        camGO.transform.SetParent(transform);
        camGO.transform.position = new Vector3(worldCentre.x, cameraHeight, worldCentre.y);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        _rt.name = "MinimapRT";
        _rt.Create();

        _miniCam = camGO.AddComponent<Camera>();
        _miniCam.orthographic     = true;
        _miniCam.orthographicSize = orthographicSize;
        _miniCam.targetTexture    = _rt;
        _miniCam.clearFlags       = CameraClearFlags.SolidColor;
        _miniCam.backgroundColor  = new Color(0.08f, 0.08f, 0.08f, 1f);
        _miniCam.depth            = -10;        // render before main camera
        _miniCam.cullingMask      = ~0;         // everything
        _miniCam.farClipPlane     = cameraHeight + 10f;
    }

    // ── UI setup ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("MinimapCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background panel (dark border)
        var bgGO = new GameObject("MinimapBackground");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.8f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        int border = 4;
        int totalSize = displaySize + border * 2;
        bgRT.sizeDelta        = new Vector2(totalSize, totalSize);
        bgRT.anchorMin        = new Vector2(1f, 1f);
        bgRT.anchorMax        = new Vector2(1f, 1f);
        bgRT.pivot            = new Vector2(1f, 1f);
        bgRT.anchoredPosition = new Vector2(-10f, -10f);

        // Map image
        var mapGO = new GameObject("MinimapImage");
        mapGO.transform.SetParent(bgGO.transform, false);
        _mapImage = mapGO.AddComponent<RawImage>();
        _mapImage.texture = _rt;
        var mapRect = mapGO.GetComponent<RectTransform>();
        mapRect.sizeDelta        = new Vector2(displaySize, displaySize);
        mapRect.anchorMin        = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax        = new Vector2(0.5f, 0.5f);
        mapRect.pivot            = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero;

        // Player dot
        var dotGO = new GameObject("MinimapDot");
        dotGO.transform.SetParent(mapGO.transform, false);
        var dotImg = dotGO.AddComponent<Image>();
        dotImg.color = dotColor;
        _dot = dotGO.GetComponent<RectTransform>();
        _dot.sizeDelta        = new Vector2(dotSize, dotSize);
        _dot.anchorMin        = new Vector2(0.5f, 0.5f);
        _dot.anchorMax        = new Vector2(0.5f, 0.5f);
        _dot.pivot            = new Vector2(0.5f, 0.5f);
        _dot.anchoredPosition = Vector2.zero;
    }

    // ── Player tracking ───────────────────────────────────────────────────────

    void FindPlayer()
    {
        // Try tag first, then common names
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
            playerGO = GameObject.Find("Player");
        if (playerGO != null)
            _playerTransform = playerGO.transform;
    }

    void UpdateDot()
    {
        // Convert world XZ → minimap UV (0-1)
        float worldSpan = orthographicSize * 2f;   // total world units shown

        float u = (_playerTransform.position.x - (worldCentre.x - orthographicSize)) / worldSpan;
        float v = (_playerTransform.position.z - (worldCentre.y - orthographicSize)) / worldSpan;

        // Clamp so the dot never leaves the texture
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        // RawImage UV origin is bottom-left; screen Y is up
        float px = (u - 0.5f) * displaySize;
        float py = (v - 0.5f) * displaySize;

        _dot.anchoredPosition = new Vector2(px, py);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
