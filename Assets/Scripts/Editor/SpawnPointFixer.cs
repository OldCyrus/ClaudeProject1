using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Fix Spawn Points
///
/// Validates every SpawnPoint against the maze geometry using Physics.CheckCapsule,
/// then moves any invalid ones to a nearby open position found by grid search.
/// Also adds doorway openings where sealed rooms were detected.
/// </summary>
public static class SpawnPointFixer
{
    // Player capsule dimensions (matches CharacterController settings).
    const float CapsuleRadius = 0.4f;
    const float CapsuleHeight = 1.8f;
    const float SpawnY        = 1.5f;   // all spawn points sit at this Y

    // Grid search: step size and max radius when looking for a free spot.
    const float SearchStep   = 1.0f;
    const float SearchRadius = 15.0f;

    [MenuItem("Tools/Fix Spawn Points")]
    public static void Run()
    {
        var sb      = new StringBuilder();
        var moved   = new List<string>();
        var openings = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║       SPAWN POINT FIX REPORT             ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        // ── 1. Find all spawn points ───────────────────────────────────────
        var spawnPoints = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.StartsWith("SpawnPoint"))
                spawnPoints.Add(go);

        if (spawnPoints.Count == 0)
        {
            EditorUtility.DisplayDialog("Fix Spawn Points",
                "No GameObjects with name starting 'SpawnPoint' found.\n" +
                "Open the game scene (ClaudeMainLevel) first.", "OK");
            return;
        }

        spawnPoints.Sort((a, b) => string.Compare(a.name, b.name));
        sb.AppendLine($"Found {spawnPoints.Count} spawn point(s).\n");

        // ── 2. Validate & fix each spawn point ────────────────────────────
        foreach (var sp in spawnPoints)
        {
            Vector3 pos = sp.transform.position;
            pos.y = SpawnY;

            bool blocked = IsCapsuleBlocked(pos);

            if (!blocked)
            {
                sb.AppendLine($"  ✔  {sp.name} at {Vec(pos)} — OK");
                continue;
            }

            sb.AppendLine($"  ✖  {sp.name} at {Vec(pos)} — BLOCKED, searching for free spot…");

            Vector3? free = FindFreePosition(pos);
            if (free.HasValue)
            {
                Undo.RecordObject(sp.transform, "Move SpawnPoint");
                sp.transform.position = free.Value;
                EditorUtility.SetDirty(sp);

                string msg = $"{sp.name}: {Vec(pos)} → {Vec(free.Value)}";
                sb.AppendLine($"     ✔  Moved to {Vec(free.Value)}");
                moved.Add(msg);
            }
            else
            {
                sb.AppendLine($"     ⚠  Could not find a free position within {SearchRadius} units.");
            }
        }

        // ── 3. Detect & open sealed corner rooms ──────────────────────────
        sb.AppendLine("\n── Sealed Room Check ─────────────────────────────────────────");
        CheckAndOpenSealedCorners(sb, openings);

        // ── 4. Save & report ──────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        sb.AppendLine($"\n── Summary ───────────────────────────────────────────────────");
        sb.AppendLine($"Spawn points moved   : {moved.Count}");
        foreach (var m in moved)   sb.AppendLine($"  • {m}");
        sb.AppendLine($"Openings added       : {openings.Count}");
        foreach (var o in openings) sb.AppendLine($"  • {o}");

        Debug.Log(sb.ToString());

        string dialog = moved.Count == 0 && openings.Count == 0
            ? "All spawn points are in valid positions.\nNo changes were needed."
            : $"Fixed {moved.Count} spawn point(s), added {openings.Count} doorway opening(s).\n\n" +
              "See Console for full report.\n\nSave the scene (Ctrl+S).";

        EditorUtility.DisplayDialog("Fix Spawn Points — Done", dialog, "OK — Save Scene");
    }

    // ── Physics helpers ───────────────────────────────────────────────────

    static bool IsCapsuleBlocked(Vector3 pos)
    {
        // Capsule: bottom sphere centre and top sphere centre.
        float halfH = CapsuleHeight * 0.5f - CapsuleRadius;
        Vector3 p1 = pos + Vector3.up * (CapsuleRadius);
        Vector3 p2 = pos + Vector3.up * (CapsuleRadius + halfH * 2f);
        return Physics.CheckCapsule(p1, p2, CapsuleRadius - 0.05f);
    }

    /// <summary>Spiral-grid search outward from <paramref name="origin"/> for a clear capsule position.</summary>
    static Vector3? FindFreePosition(Vector3 origin)
    {
        for (float r = SearchStep; r <= SearchRadius; r += SearchStep)
        {
            int steps = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * r / SearchStep));
            for (int i = 0; i < steps; i++)
            {
                float angle = i * 360f / steps;
                float dx = Mathf.Cos(angle * Mathf.Deg2Rad) * r;
                float dz = Mathf.Sin(angle * Mathf.Deg2Rad) * r;
                var candidate = new Vector3(origin.x + dx, SpawnY, origin.z + dz);
                if (!IsCapsuleBlocked(candidate))
                    return candidate;
            }
        }
        return null;
    }

    // ── Sealed-corner detection ───────────────────────────────────────────

    static void CheckAndOpenSealedCorners(StringBuilder sb, List<string> openings)
    {
        // Test probe positions in the four corner areas that manual analysis
        // identified as potentially sealed.
        var corners = new[]
        {
            ("NW corner",  new Vector3(-25, SpawnY, 25),  "H_20_W"),
            ("NE corner",  new Vector3( 25, SpawnY, 25),  "H_20_E"),
            ("SW corner",  new Vector3(-25, SpawnY,-25),  "H_n20_W"),
            ("SE corner",  new Vector3( 25, SpawnY,-25),  "H_n20_E"),
        };

        foreach (var (label, probe, wallName) in corners)
        {
            if (!IsCapsuleBlocked(probe))
            {
                sb.AppendLine($"  ✔  {label} — accessible");
                continue;
            }

            // Probe is blocked.  Check whether the blocking wall is the
            // named horizontal barrier that seals this corner.
            var wall = GameObject.Find(wallName);
            if (wall == null)
            {
                sb.AppendLine($"  ⚠  {label} — blocked but '{wallName}' not found; skipped.");
                continue;
            }

            // Trim the wall to create a doorway opening.
            // Strategy: shrink the wall's X scale and shift its centre so
            // a 4-unit gap opens closest to the corner.
            bool opened = TrimWallForDoorway(wall, probe, label, sb);
            if (opened)
                openings.Add($"Doorway added in '{wallName}' (opening to {label})");
        }
    }

    /// <summary>
    /// Shrinks a horizontal wall (extends along X) to create a doorway opening
    /// on the side closest to <paramref name="corner"/>.
    /// </summary>
    static bool TrimWallForDoorway(GameObject wall, Vector3 corner,
                                   string label, StringBuilder sb)
    {
        const float DoorWidth  = 4f;   // gap width in world units
        const float MinWallLen = 4f;   // don't trim if wall would become too short

        var t      = wall.transform;
        float posX = t.position.x;
        float scaleX = t.localScale.x;

        // Wall world extents along X.
        float wallLeft  = posX - scaleX * 0.5f;
        float wallRight = posX + scaleX * 0.5f;

        bool trimRight = corner.x > posX;   // corner is to the right → open right end
        float newScaleX = scaleX - DoorWidth;

        if (newScaleX < MinWallLen)
        {
            sb.AppendLine($"  ⚠  {label}: '{wall.name}' too short to trim safely (scale={scaleX}). Skipped.");
            return false;
        }

        Undo.RecordObject(t, "Trim wall for doorway");

        float newPosX;
        if (trimRight)
            newPosX = wallLeft + newScaleX * 0.5f;   // keep left end, shrink from right
        else
            newPosX = wallRight - newScaleX * 0.5f;  // keep right end, shrink from left

        t.localScale = new Vector3(newScaleX, t.localScale.y, t.localScale.z);
        t.position   = new Vector3(newPosX,   t.position.y,   t.position.z);

        EditorUtility.SetDirty(wall);

        sb.AppendLine($"  ✔  {label}: '{wall.name}' trimmed — " +
                      $"scale X {scaleX}→{newScaleX}, pos X {posX:F1}→{newPosX:F1} " +
                      $"(4-unit doorway on {(trimRight ? "right" : "left")} end)");
        return true;
    }

    // ── Utility ───────────────────────────────────────────────────────────

    static string Vec(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
}
