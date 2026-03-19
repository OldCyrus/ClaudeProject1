using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Fix URP Materials
///
/// 1. Verifies URP assets are assigned in Graphics + Quality settings (reports, fixes if needed).
/// 2. Scans every Renderer in the active scene for Built-in Standard / legacy shaders.
/// 3. Creates named URP Lit .mat assets in Assets/Materials/ — preserving each object's colour,
///    metallic, and smoothness values — and re-assigns them.
/// </summary>
public static class URPFixer
{
    const string MatFolder  = "Assets/Materials";
    const string URPLit     = "Universal Render Pipeline/Lit";
    const string URPSimple  = "Universal Render Pipeline/Simple Lit";

    // Shader GUIDs that are NOT URP-compatible (built-in Standard + legacy variants).
    static readonly HashSet<string> BadShaderNames = new HashSet<string>
    {
        "Standard",
        "Standard (Specular setup)",
        "Diffuse",
        "Specular",
        "Bumped Diffuse",
        "Bumped Specular",
        "Particles/Standard Unlit",
        "Particles/Standard Surface",
        "Mobile/Diffuse",
        "Mobile/Bumped Diffuse",
    };

    // ── Menu entry ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Fix URP Materials")]
    public static void Run()
    {
        int verified = VerifyURPAssignment();
        int fixed_   = FixSceneMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "URP Fix Complete",
            $"Graphics/Quality settings: {verified} assignment(s) confirmed or corrected.\n" +
            $"Materials replaced: {fixed_}.\n\n" +
            "Save the scene (Ctrl+S) and press Play.",
            "OK");
    }

    // ── Step 1: Verify URP assignment ─────────────────────────────────────────

    static int VerifyURPAssignment()
    {
        int changes = 0;

        // Find best URP asset: prefer PC, fall back to any.
        var urpAsset = LoadURPAsset("PC_RPAsset")
                    ?? LoadURPAsset("URP")
                    ?? FindAnyURPAsset();

        if (urpAsset == null)
        {
            urpAsset = CreateFallbackURPAsset();
            changes++;
        }

        // ── Graphics settings ──────────────────────────────────────────────────
        if (GraphicsSettings.defaultRenderPipeline != urpAsset)
        {
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            EditorUtility.SetDirty(GraphicsSettings.defaultRenderPipeline);
            Debug.Log($"[URPFixer] Assigned '{urpAsset.name}' to Graphics > Scriptable Render Pipeline.");
            changes++;
        }
        else
        {
            Debug.Log($"[URPFixer] Graphics settings already use '{urpAsset.name}'. ✓");
        }

        // ── Quality settings (all levels) ─────────────────────────────────────
        var qso    = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = qso.FindProperty("m_QualitySettings");

        bool qualityDirty = false;
        for (int i = 0; i < levels.arraySize; i++)
        {
            var levelProp    = levels.GetArrayElementAtIndex(i);
            var srpProp = levelProp.FindPropertyRelative("customRenderPipeline");

            if (srpProp.objectReferenceValue != urpAsset)
            {
                srpProp.objectReferenceValue = urpAsset;
                qualityDirty = true;
                changes++;
                Debug.Log($"[URPFixer] Quality level {i} ({GetQualityLevelName(levels, i)}): " +
                          $"assigned '{urpAsset.name}'.");
            }
            else
            {
                Debug.Log($"[URPFixer] Quality level {i} ({GetQualityLevelName(levels, i)}): " +
                          $"already correct. ✓");
            }
        }

        if (qualityDirty)
        {
            qso.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        return changes;
    }

    // ── Step 2: Fix scene materials ───────────────────────────────────────────

    static int FixSceneMaterials()
    {
        if (!Directory.Exists(MatFolder))
            Directory.CreateDirectory(MatFolder);

        // Catalogue existing saved URP materials so we can reuse them.
        var savedMats = new Dictionary<string, Material>(); // key = asset name

        var urpShader = Shader.Find(URPLit);
        if (urpShader == null)
        {
            Debug.LogError("[URPFixer] Could not find shader 'Universal Render Pipeline/Lit'. " +
                           "Make sure the URP package is installed.");
            return 0;
        }

        int replaced = 0;

        var renderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            // Process each material slot.
            var mats      = r.sharedMaterials;
            bool modified = false;

            for (int slot = 0; slot < mats.Length; slot++)
            {
                var mat = mats[slot];
                if (mat == null) continue;
                if (!NeedsReplacement(mat)) continue;

                string matName = BuildMaterialName(r, slot);
                Material urpMat;

                if (savedMats.TryGetValue(matName, out urpMat))
                {
                    // Already created this session.
                }
                else
                {
                    string assetPath = $"{MatFolder}/{matName}.mat";
                    urpMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                    if (urpMat == null)
                    {
                        urpMat = CreateURPMaterial(mat, urpShader, matName);
                        AssetDatabase.CreateAsset(urpMat, assetPath);
                        Debug.Log($"[URPFixer] Created '{assetPath}'.");
                    }
                    else
                    {
                        // Refresh an existing saved mat to match current colours.
                        CopyProperties(mat, urpMat);
                        EditorUtility.SetDirty(urpMat);
                        Debug.Log($"[URPFixer] Updated existing '{assetPath}'.");
                    }

                    savedMats[matName] = urpMat;
                }

                mats[slot] = urpMat;
                modified   = true;
                replaced++;
            }

            if (modified)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }

        Debug.Log($"[URPFixer] Replaced {replaced} material slot(s) across {renderers.Length} renderer(s).");
        return replaced;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static bool NeedsReplacement(Material mat)
    {
        if (mat.shader == null) return false;
        return BadShaderNames.Contains(mat.shader.name);
    }

    /// Build a stable, human-readable asset name from the GameObject hierarchy.
    static string BuildMaterialName(Renderer r, int slot)
    {
        string baseName = r.transform.parent != null
            ? $"{r.transform.parent.name}_{r.gameObject.name}"
            : r.gameObject.name;

        baseName = baseName.Replace(" ", "_").Replace("/", "_");
        if (r.sharedMaterials.Length > 1) baseName += $"_slot{slot}";
        return "Mat_" + baseName;
    }

    /// Create a new URP Lit material that mirrors the source Standard material.
    static Material CreateURPMaterial(Material src, Shader urpShader, string name)
    {
        var m = new Material(urpShader) { name = name };
        CopyProperties(src, m);
        return m;
    }

    /// Copy colour / metallic / smoothness from Standard → URP Lit.
    static void CopyProperties(Material src, Material dst)
    {
        // Main colour: Standard uses _Color, URP uses _BaseColor.
        Color col = src.HasProperty("_Color")     ? src.GetColor("_Color")
                  : src.HasProperty("_BaseColor")  ? src.GetColor("_BaseColor")
                  : Color.white;

        dst.SetColor("_BaseColor", col);

        // Metallic.
        float metallic = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
        dst.SetFloat("_Metallic", metallic);

        // Smoothness: Standard calls it _Glossiness, URP calls it _Smoothness.
        float smooth = src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness")
                     : src.HasProperty("_Smoothness")  ? src.GetFloat("_Smoothness")
                     : 0.3f;
        dst.SetFloat("_Smoothness", smooth);

        // Main texture, if any.
        if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null)
            dst.SetTexture("_BaseMap", src.GetTexture("_MainTex"));

        // Emission.
        if (src.HasProperty("_EmissionColor"))
        {
            Color em = src.GetColor("_EmissionColor");
            if (em != Color.black)
            {
                dst.EnableKeyword("_EMISSION");
                dst.SetColor("_EmissionColor", em);
            }
        }

        // Surface type: opaque by default.
        dst.SetFloat("_Surface", 0f);         // 0 = Opaque
        dst.SetFloat("_Blend",   0f);
        dst.renderQueue = -1;
    }

    // ── URP asset lookup ──────────────────────────────────────────────────────

    static UniversalRenderPipelineAsset LoadURPAsset(string namePart)
    {
        string[] guids = AssetDatabase.FindAssets($"{namePart} t:UniversalRenderPipelineAsset");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    static UniversalRenderPipelineAsset FindAnyURPAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    static UniversalRenderPipelineAsset CreateFallbackURPAsset()
    {
        Debug.LogWarning("[URPFixer] No URP asset found — creating a default one.");

        if (!Directory.Exists("Assets/Settings"))
            Directory.CreateDirectory("Assets/Settings");

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        rendererData.name = "URP_Renderer";
        AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");

        var asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        asset.name = "URP_PipelineAsset";

        // Wire up renderer via reflection (the public API requires internal factory in URP 17).
        var rendererListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rendererListField != null)
            rendererListField.SetValue(asset, new ScriptableRendererData[] { rendererData });

        AssetDatabase.CreateAsset(asset, "Assets/Settings/URP_PipelineAsset.asset");
        AssetDatabase.SaveAssets();

        return asset;
    }

    static string GetQualityLevelName(SerializedProperty levels, int index)
    {
        var lp = levels.GetArrayElementAtIndex(index);
        var np = lp.FindPropertyRelative("name");
        return np != null ? np.stringValue : index.ToString();
    }
}
