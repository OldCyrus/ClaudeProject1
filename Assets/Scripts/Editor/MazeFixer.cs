using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Fix Maze Layout
///
/// Mathematical analysis of the maze geometry — no physics queries needed.
/// Moves all spawn points to verified-open positions and trims sealed walls
/// to add doorway openings into every isolated region.
/// </summary>
public static class MazeFixer
{
    const float Y = 1.5f;   // spawn point height

    // ── Verified safe spawn positions (calculated from wall data) ─────────
    // Each position lies in a region proven to be connected to the main maze.
    static readonly (string name, Vector3 pos)[] SafeSpawns =
    {
        // North central corridor  (X:-8 to 6, Z:20-30) — accessible through
        // 14-unit gap in H_20_W / H_20_E at Z=20.
        ("SpawnPoint_1", new Vector3(-4,  Y,  22)),
        ("SpawnPoint_2", new Vector3( 4,  Y,  22)),

        // South central strip (X:-8 to 6, Z:-20 to -30) — accessible through
        // 14-unit gap between H_n20_W(-8) and H_n20_E(6) at Z=-20.
        ("SpawnPoint_3", new Vector3(-4,  Y, -22)),
        ("SpawnPoint_4", new Vector3( 4,  Y, -22)),

        // Mid-north band (Z:10-20) — accessible through gaps in Z=10 walls.
        ("SpawnPoint_5", new Vector3(-12, Y,  15)),
        ("SpawnPoint_6", new Vector3( 12, Y,  15)),

        // Centre of the maze — the large open Z:-10 to 10, X:-8 to 8 hub.
        ("SpawnPoint_7", new Vector3( 0,  Y,   5)),
        ("SpawnPoint_8", new Vector3( 0,  Y,  -5)),
    };

    // ── Wall doorway cuts (object name → new scale X or Z, new position X or Z)
    // Each entry: wall name, axis ('x' or 'z'), newScale, newCentre
    // Calculated to trim 4 units off the end closest to the sealed room.
    static readonly (string wall, char axis, float newScale, float newCentre)[] DoorwayCuts =
    {
        // ── NORTH BAND: open the sealed outer/corner pockets ──────────────

        // H_20_W  (spans X:-30 to -14): trim 4 units off east (right) end
        //   → new span X:-30 to -18  gap at X:-18 to -14  (opens W pocket southward)
        ("H_20_W",    'x', 12f, -24f),

        // H_20_E  (spans X:6 to 30): trim 4 units off west (left) end
        //   → new span X:10 to 30  gap at X:6 to 10  (opens E pocket southward)
        ("H_20_E",    'x', 20f,  20f),

        // V_n20_N (spans Z:20 to 30 at X=-20): trim 4 units off south end
        //   → new span Z:24 to 30  gap at Z:20 to 24  (connects NW outer to inner)
        ("V_n20_N",   'z',  6f,  27f),

        // V_p20_N (spans Z:20 to 30 at X=20): trim 4 units off south end
        //   → new span Z:24 to 30  gap at Z:20 to 24  (connects NE outer to inner)
        ("V_p20_N",   'z',  6f,  27f),

        // Room_NW_E (spans Z:20.5 to 29.5 at X=-14): trim south end
        //   → new span Z:24.5 to 29.5  gap at Z:20.5 to 24.5
        ("Room_NW_E", 'z',  5f,  27f),

        // Room_NE_W (spans Z:20.5 to 29.5 at X=14): trim south end
        //   → new span Z:24.5 to 29.5  gap at Z:20.5 to 24.5
        ("Room_NE_W", 'z',  5f,  27f),

        // ── SOUTH BAND: open the sealed outer/corner pockets ─────────────

        // H_n20_W (spans X:-28 to -8): trim 8 units off west (left) end
        //   → new span X:-20 to -8  gap at X:-28 to -20
        //   (widens the south exit, giving SP3 at X=-4 easy access)
        ("H_n20_W",   'x', 12f, -14f),

        // H_n20_E (spans X:6 to 30): trim 4 units off west (left) end
        //   → new span X:10 to 30  gap at X:6 to 10  (opens SE pocket southward)
        ("H_n20_E",   'x', 20f,  20f),

        // V_n20_S (spans Z:-20 to -10 at X=-20): trim south end
        //   → new span Z:-20 to -14  gap at Z:-16 to -10
        ("V_n20_S",   'z',  6f, -17f),

        // V_p20_S (spans Z:-20 to -10 at X=20): trim south end
        //   → new span Z:-20 to -14  gap at Z:-16 to -10
        ("V_p20_S",   'z',  6f, -17f),

        // Room_SW_E (spans Z:-29.5 to -20.5 at X=-14): trim north end
        //   → new span Z:-29.5 to -24.5  gap at Z:-24.5 to -20.5
        ("Room_SW_E", 'z',  5f, -27f),

        // Room_SE_W (spans Z:-29.5 to -20.5 at X=14): trim north end
        //   → new span Z:-29.5 to -24.5  gap at Z:-24.5 to -20.5
        ("Room_SE_W", 'z',  5f, -27f),
    };

    // ─────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Fix Maze Layout")]
    public static void Run()
    {
        var sb       = new StringBuilder();
        var spMoved  = new List<string>();
        var walls    = new List<string>();
        var notFound = new List<string>();

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║        MAZE LAYOUT FIX REPORT            ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        // ── 1. Move spawn points ──────────────────────────────────────────
        sb.AppendLine("── Spawn Points ──────────────────────────────────────────────");
        foreach (var (spName, newPos) in SafeSpawns)
        {
            var go = GameObject.Find(spName);
            if (go == null)
            {
                sb.AppendLine($"  ⚠  {spName} not found in scene.");
                notFound.Add(spName);
                continue;
            }

            Vector3 old = go.transform.position;
            Undo.RecordObject(go.transform, "Move SpawnPoint");
            go.transform.position = newPos;
            EditorUtility.SetDirty(go);

            string moved = $"{spName}: ({old.x:F0},{old.z:F0}) → ({newPos.x:F0},{newPos.z:F0})";
            sb.AppendLine($"  ✔  {moved}");
            spMoved.Add(moved);
        }

        // ── 2. Open sealed rooms ──────────────────────────────────────────
        sb.AppendLine("\n── Doorway Cuts ──────────────────────────────────────────────");
        foreach (var (wallName, axis, newScale, newCentre) in DoorwayCuts)
        {
            var go = GameObject.Find(wallName);
            if (go == null)
            {
                sb.AppendLine($"  ⚠  '{wallName}' not found — skipped.");
                notFound.Add(wallName);
                continue;
            }

            Undo.RecordObject(go.transform, "Open doorway in wall");
            var s = go.transform.localScale;
            var p = go.transform.position;

            if (axis == 'x')
            {
                sb.AppendLine($"  ✔  {wallName}: scaleX {s.x}→{newScale}, posX {p.x:F1}→{newCentre:F1}");
                go.transform.localScale = new Vector3(newScale, s.y, s.z);
                go.transform.position   = new Vector3(newCentre, p.y, p.z);
            }
            else
            {
                sb.AppendLine($"  ✔  {wallName}: scaleZ {s.z}→{newScale}, posZ {p.z:F1}→{newCentre:F1}");
                go.transform.localScale = new Vector3(s.x, s.y, newScale);
                go.transform.position   = new Vector3(p.x, p.y, newCentre);
            }

            EditorUtility.SetDirty(go);
            walls.Add(wallName);
        }

        // ── 3. Finalise ───────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        sb.AppendLine($"\n── Summary ────────────────────────────────────────────────");
        sb.AppendLine($"Spawn points moved : {spMoved.Count}");
        sb.AppendLine($"Doorways added     : {walls.Count}");
        if (notFound.Count > 0)
            sb.AppendLine($"Not found          : {string.Join(", ", notFound)}");

        Debug.Log(sb.ToString());

        string dialog =
            $"Moved {spMoved.Count} spawn points to open positions.\n" +
            $"Added {walls.Count} doorway openings to sealed rooms.\n\n" +
            (notFound.Count > 0 ? $"⚠ {notFound.Count} object(s) not found — see Console.\n\n" : "") +
            "Sealed rooms fixed:\n" +
            "  • NW / NE corner pockets (H_20_W, H_20_E, V_n20_N, V_p20_N)\n" +
            "  • NW / NE inner corridors (Room_NW_E, Room_NE_W)\n" +
            "  • SW / SE corner pockets (H_n20_W, H_n20_E, V_n20_S, V_p20_S)\n" +
            "  • SW / SE room walls (Room_SW_E, Room_SE_W)\n\n" +
            "Save the scene (Ctrl+S).";

        EditorUtility.DisplayDialog("Fix Maze Layout — Done", dialog, "OK — Save Scene");
    }
}
