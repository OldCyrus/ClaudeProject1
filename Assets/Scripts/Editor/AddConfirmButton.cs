using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Add Confirm Character Button
///
/// Creates a "Start Game" UI button in the current scene and attaches
/// ConfirmCharacterButton to it, wired to the DCA and the game scene name.
/// </summary>
public static class AddConfirmButton
{
    const string GameSceneName = "ClaudeMainLevel";

    [MenuItem("Tools/Add Confirm Character Button")]
    public static void Run()
    {
        // ── 1. Find the DCA ───────────────────────────────────────────────
        var dca = Object.FindAnyObjectByType<UMA.CharacterSystem.DynamicCharacterAvatar>();
        if (dca == null)
        {
            EditorUtility.DisplayDialog("Add Confirm Button",
                "No DynamicCharacterAvatar found in the current scene.\n\n" +
                "Open the character creation scene first, then run this tool.", "OK");
            return;
        }

        // ── 2. Find or create a Canvas ────────────────────────────────────
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // ── 3. Remove any existing confirm button to avoid duplicates ─────
        var existingBtn = canvas.GetComponentInChildren<ConfirmCharacterButton>();
        if (existingBtn != null)
        {
            Undo.DestroyObjectImmediate(existingBtn.gameObject);
        }

        // ── 4. Create the button GameObject ──────────────────────────────
        var btnGO = new GameObject("StartGameButton");
        Undo.RegisterCreatedObjectUndo(btnGO, "Create StartGameButton");
        btnGO.transform.SetParent(canvas.transform, false);

        // RectTransform — bottom-centre, 240×60
        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta        = new Vector2(240f, 60f);

        // Background image
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.18f, 0.55f, 0.22f); // green

        // Unity UI Button
        var button = btnGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.25f, 0.72f, 0.30f);
        colors.pressedColor     = new Color(0.12f, 0.38f, 0.15f);
        button.colors = colors;

        // ── 5. Label ─────────────────────────────────────────────────────
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin        = Vector2.zero;
        labelRT.anchorMax        = Vector2.one;
        labelRT.offsetMin        = Vector2.zero;
        labelRT.offsetMax        = Vector2.zero;

        // Use legacy Unity UI Text (no TMP dependency required).
        var legacyText = labelGO.AddComponent<Text>();
        legacyText.text      = "Start Game";
        legacyText.alignment = TextAnchor.MiddleCenter;
        legacyText.fontSize  = 24;
        legacyText.color     = Color.white;

        // ── 6. ConfirmCharacterButton component ───────────────────────────
        var confirm = btnGO.AddComponent<ConfirmCharacterButton>();
        confirm.avatar        = dca;
        confirm.gameSceneName = GameSceneName;
        confirm.confirmButton = button;

        // ── 7. Wire button click → ConfirmCharacterButton.OnConfirmClicked ─
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            button.onClick,
            confirm.OnConfirmClicked);

        EditorUtility.SetDirty(btnGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[AddConfirmButton] Created 'StartGameButton' on Canvas '{canvas.name}'. " +
                  $"DCA: '{dca.gameObject.name}'. Target scene: '{GameSceneName}'.");

        EditorUtility.DisplayDialog("Confirm Character Button — Done",
            "✓ 'Start Game' button added to the Canvas.\n\n" +
            "NEXT STEPS:\n" +
            "1. Save this scene (Ctrl+S)\n" +
            "2. Open ClaudeMainLevel scene\n" +
            "3. Select the Player GameObject\n" +
            "4. Add Component → CharacterLoader\n" +
            "   (or run Tools > Add Character Loader to Player)\n" +
            "5. Add BOTH scenes to File > Build Settings\n" +
            "6. Press Play in the character creation scene to test",
            "OK");
    }
}
