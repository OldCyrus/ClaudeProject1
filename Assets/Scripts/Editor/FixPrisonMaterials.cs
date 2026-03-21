using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Tools > Fix Prison Materials
///
/// Creates URP/Lit materials and applies them to all prison level geometry
/// based on object name conventions set by BuildPrisonLevel.
/// </summary>
public static class FixPrisonMaterials
{
    const string MatFolder = "Assets/Materials/Prison";

    // Material definitions: (asset name, color, smoothness)
    static readonly (string name, Color color, float smooth)[] Defs =
    {
        ("Wall_Material",     new Color(0.22f, 0.22f, 0.22f), 0.1f),
        ("Floor_Material",    new Color(0.28f, 0.22f, 0.15f), 0.05f),
        ("Ceiling_Material",  new Color(0.20f, 0.20f, 0.20f), 0.05f),
        ("Cell_Bar_Material", new Color(0.18f, 0.18f, 0.20f), 0.55f),
        ("Exit_Material",     new Color(0.75f, 0.08f, 0.08f), 0.25f),
        ("Key_Material",      new Color(1.00f, 0.82f, 0.00f), 0.65f),
    };

    [MenuItem("Tools/Fix Prison Materials")]
    public static void Run()
    {
        // ── 1. Ensure material folder exists ──────────────────────────────────
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Prison");
        }

        // ── 2. Find or create each material ───────────────────────────────────
        var mats = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var (name, color, smooth) in Defs)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = CreateURPMaterial(color, smooth);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                // Update existing material in case colours changed
                SetURPColor(mat, color);
                mat.SetFloat("_Smoothness", smooth);
                EditorUtility.SetDirty(mat);
            }
            mats[name] = mat;
            Debug.Log($"[FixPrisonMaterials] Ready: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── 3. Apply materials to scene objects ───────────────────────────────
        int applied = 0;
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

        foreach (var mr in allRenderers)
        {
            string n = mr.gameObject.name.ToLower();
            Material mat = PickMaterial(n, mats);
            if (mat == null) continue;

            Undo.RecordObject(mr, "Fix Prison Materials");
            mr.sharedMaterial = mat;
            EditorUtility.SetDirty(mr);
            applied++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Fix Prison Materials",
            $"Done.\n\n" +
            $"• {mats.Count} URP/Lit materials created/updated in {MatFolder}\n" +
            $"• {applied} renderers updated\n\n" +
            "Save the scene (Ctrl+S).", "OK");

        Debug.Log($"[FixPrisonMaterials] Applied {applied} materials.");
    }

    // ── Material selection by object name ─────────────────────────────────────

    static Material PickMaterial(string n,
        System.Collections.Generic.Dictionary<string, Material> mats)
    {
        // Key / Exit first (most specific)
        if (n.Contains("keypickup") || n == "key")
            return mats["Key_Material"];

        if (n.Contains("exitdoor") || n == "exit")
            return mats["Exit_Material"];

        // Prison bar pieces
        if (n.Contains("_barleft")  || n.Contains("_barright") ||
            n.Contains("_barabove") || n.Contains("_bar"))
            return mats["Cell_Bar_Material"];

        // Floors
        if (n.Contains("_floor") || n.Contains("floor_") || n == "floor")
            return mats["Floor_Material"];

        // Ceilings
        if (n.Contains("_ceiling") || n.Contains("ceiling_") || n == "ceiling")
            return mats["Ceiling_Material"];

        // Walls and everything else structural
        if (n.Contains("wall") || n.Contains("_end")  ||
            n.Contains("backwall") || n.Contains("farwall") ||
            n.Contains("divider"))
            return mats["Wall_Material"];

        // Generic grey fallback for anything else that is part of the level
        // (ground plane, unnamed primitives, etc.)
        if (n.Contains("ground"))
            return mats["Floor_Material"];

        return null;   // Don't touch unrelated objects (player, UMA, etc.)
    }

    // ── URP material helpers ──────────────────────────────────────────────────

    static Material CreateURPMaterial(Color color, float smoothness)
    {
        // Try both common URP shader names
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");

        if (shader == null)
        {
            Debug.LogWarning("[FixPrisonMaterials] URP/Lit shader not found — " +
                             "falling back to Standard. Make sure URP is installed.");
            shader = Shader.Find("Standard");
        }

        var mat = new Material(shader);
        SetURPColor(mat, color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic",   0f);
        return mat;
    }

    static void SetURPColor(Material mat, Color color)
    {
        // URP/Lit uses _BaseColor; Standard uses _Color
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }
}
