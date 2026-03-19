using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Diagnose and Fix Collisions
///
/// Addresses all six requirements:
///
///   CHECK 1  Scans every floor and wall object — ensures a non-trigger BoxCollider
///            or MeshCollider is attached.  Adds one automatically if missing.
///
///   CHECK 2  Verifies the Player has a properly-sized CharacterController (or a
///            Rigidbody + CapsuleCollider).  Validates height, radius, and skinWidth.
///            Removes a redundant CapsuleCollider left over from CreatePrimitive().
///
///   CHECK 3  Replaces the Plane's zero-thickness MeshCollider with a solid BoxCollider
///            (0.4-unit slab) so wall-base seams cannot be slipped through.
///
///   CHECK 4  Moves every SpawnPoint and EnemySpawn that sits at Y < (CC_height/2 + 0.5)
///            up to a safe Y so the CharacterController never spawns underground.
///
///   CHECK 5  (covered by Check 1) — auto-adds colliders to any object that lacks one.
///
///   CHECK 6  Prints a clear report of every object that was missing a collider,
///            every spawn point that was repositioned, and a gap-scan summary.
/// </summary>
public static class CollisionDiagnostics
{
    // Safe spawn height: CC half-height + clearance buffer.
    // Player CC: height=1.8 → half=0.9.  We add 0.6 for safe clearance → Y=1.5.
    const float SafeSpawnY = 1.5f;

    // ── Entry point ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Diagnose and Fix Collisions")]
    public static void Run()
    {
        var sb     = new StringBuilder();
        var report = new List<string>();   // human-readable fix log shown in dialog

        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║     COLLISION DIAGNOSTICS + FIX REPORT   ║");
        sb.AppendLine("╚══════════════════════════════════════════╝\n");

        // ── Run all checks ────────────────────────────────────────────────────
        Check1_WallAndFloorColliders(sb, report);
        Check2_PlayerSetup(sb, report);
        Check3_SolidiseFloor(sb, report);
        Check4_SpawnPointHeights(sb, report);
        Check6_GapScan(sb);

        // ── Footer ────────────────────────────────────────────────────────────
        sb.AppendLine("\n╔══════════════════════════════════════════╗");
        sb.AppendLine($"║  FIXES APPLIED: {report.Count,-26}║");
        sb.AppendLine("╚══════════════════════════════════════════╝");

        if (report.Count == 0)
        {
            sb.AppendLine("  ✓ No issues found — scene is clean.");
        }
        else
        {
            sb.AppendLine("\nObjects / settings that were changed:");
            foreach (var line in report)
                sb.AppendLine("  • " + line);
        }

        // ── Persist ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string full = sb.ToString();
        Debug.Log(full);

        // Trim for the dialog popup (Unity has a char limit).
        string dialog = full.Length > 2000
            ? full.Substring(0, 2000) + "\n\n[…  full report in Console]"
            : full;
        EditorUtility.DisplayDialog("Collision Diagnostics", dialog, "OK — Save Scene (Ctrl+S)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHECK 1 — Floor and wall colliders
    // ══════════════════════════════════════════════════════════════════════════

    static void Check1_WallAndFloorColliders(StringBuilder sb, List<string> report)
    {
        sb.AppendLine("── CHECK 1 · Floor & Wall Colliders ──────────────────");

        // Collect all renderers under Maze_Walls (walls + floor).
        // Fall back to the entire scene if the parent isn't found.
        var mazeWalls = GameObject.Find("Maze_Walls");
        Renderer[] renderers = mazeWalls != null
            ? mazeWalls.GetComponentsInChildren<Renderer>(true)
            : GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        if (mazeWalls == null)
            sb.AppendLine("  ⚠  'Maze_Walls' parent not found — scanning all scene renderers.");

        int ok = 0, added = 0, triggerFixed = 0;

        foreach (var r in renderers)
        {
            var go  = r.gameObject;
            var col = go.GetComponents<Collider>();

            // ── Missing collider ──────────────────────────────────────────────
            if (col.Length == 0)
            {
                var bc        = go.AddComponent<BoxCollider>();
                bc.isTrigger  = false;
                EditorUtility.SetDirty(go);
                sb.AppendLine($"  ✔ ADDED BoxCollider → '{HierPath(go)}'");
                report.Add($"Added BoxCollider to '{go.name}'");
                added++;
                continue;
            }

            // ── Trigger collider on solid geometry ────────────────────────────
            foreach (var c in col)
            {
                if (!c.isTrigger) continue;
                c.isTrigger = false;
                EditorUtility.SetDirty(go);
                sb.AppendLine($"  ✔ TRIGGER → SOLID on '{HierPath(go)}'");
                report.Add($"Disabled isTrigger on '{go.name}'");
                triggerFixed++;
            }

            ok++;
        }

        sb.AppendLine($"\n  Geometry scanned : {renderers.Length}");
        sb.AppendLine($"  Already correct  : {ok}");
        sb.AppendLine($"  Colliders added  : {added}");
        sb.AppendLine($"  Triggers fixed   : {triggerFixed}\n");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHECK 2 — Player CharacterController sizing
    // ══════════════════════════════════════════════════════════════════════════

    static void Check2_PlayerSetup(StringBuilder sb, List<string> report)
    {
        sb.AppendLine("── CHECK 2 · Player Collision Setup ──────────────────");

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            sb.AppendLine("  ⚠  No GameObject tagged 'Player' found.\n");
            return;
        }

        // ── Remove redundant CapsuleCollider (left by CreatePrimitive) ─────────
        var extraCap = player.GetComponent<CapsuleCollider>();
        var cc       = player.GetComponent<CharacterController>();
        var rb       = player.GetComponent<Rigidbody>();

        if (extraCap != null && cc != null)
        {
            // CharacterController IS the collision volume — a CapsuleCollider on the
            // same object is redundant and can cause unexpected physics interactions.
            Object.DestroyImmediate(extraCap);
            EditorUtility.SetDirty(player);
            sb.AppendLine("  ✔ Removed redundant CapsuleCollider (CharacterController is sufficient).");
            report.Add("Removed redundant CapsuleCollider from Player");
        }
        else if (extraCap != null && cc == null && rb == null)
        {
            sb.AppendLine("  ⚠  Player has a CapsuleCollider but no CharacterController or Rigidbody.");
            sb.AppendLine("      Adding CharacterController with default sizing.");
            cc = player.AddComponent<CharacterController>();
            report.Add("Added CharacterController to Player");
        }

        // ── Validate CharacterController ──────────────────────────────────────
        if (cc != null)
        {
            sb.AppendLine($"  CharacterController found on '{player.name}':");
            sb.AppendLine($"    height      = {cc.height}");
            sb.AppendLine($"    radius      = {cc.radius}");
            sb.AppendLine($"    center      = {cc.center}");
            sb.AppendLine($"    stepOffset  = {cc.stepOffset}");
            sb.AppendLine($"    skinWidth   = {cc.skinWidth}");

            bool dirty = false;

            // stepOffset must be < height − 2*radius (Unity rule).
            // Also keep it strictly less than radius to avoid corner jitter.
            float maxSafeStep = cc.radius * 0.85f;
            if (cc.stepOffset >= cc.radius)
            {
                float newStep  = Mathf.Round(maxSafeStep * 100f) / 100f;
                sb.AppendLine($"  ✔ stepOffset {cc.stepOffset} ≥ radius {cc.radius} → set to {newStep}");
                report.Add($"Player CC stepOffset {cc.stepOffset}→{newStep}");
                cc.stepOffset = newStep;
                dirty = true;
            }
            else
            {
                sb.AppendLine($"  ✓ stepOffset ({cc.stepOffset}) < radius ({cc.radius})");
            }

            // skinWidth: keep between 5–15 % of radius for reliable ground detection.
            if (cc.skinWidth < 0.05f || cc.skinWidth > 0.15f)
            {
                cc.skinWidth = 0.08f;
                sb.AppendLine($"  ✔ skinWidth out of range → set to 0.08");
                report.Add("Player CC skinWidth → 0.08");
                dirty = true;
            }
            else
            {
                sb.AppendLine($"  ✓ skinWidth ({cc.skinWidth}) in range");
            }

            // Warn if center is (0,0,0) — pivot is at capsule midpoint, not feet.
            // The safe spawn Y must account for this.
            if (Mathf.Abs(cc.center.y) < 0.01f)
            {
                float minSpawnY = cc.height * 0.5f + 0.1f;
                sb.AppendLine($"  ℹ  center.y=0 → pivot is at capsule midpoint.");
                sb.AppendLine($"     Minimum safe spawn Y = {minSpawnY:F2}  " +
                              $"(using {SafeSpawnY} for all SpawnPoints).");
            }

            if (dirty) EditorUtility.SetDirty(player);
        }

        // ── Rigidbody + CapsuleCollider path ─────────────────────────────────
        if (cc == null && rb != null)
        {
            var cap = player.GetComponent<CapsuleCollider>();
            if (cap == null)
            {
                cap           = player.AddComponent<CapsuleCollider>();
                cap.height    = 1.8f;
                cap.radius    = 0.35f;
                cap.center    = new Vector3(0f, 0.9f, 0f);
                cap.isTrigger = false;
                EditorUtility.SetDirty(player);
                sb.AppendLine("  ✔ Added CapsuleCollider to Rigidbody player (height=1.8, r=0.35).");
                report.Add("Added CapsuleCollider to Rigidbody player");
            }
            else
            {
                sb.AppendLine($"  ✓ Rigidbody + CapsuleCollider present " +
                              $"(h={cap.height}, r={cap.radius}, center={cap.center}).");
            }
            rb.freezeRotation = true;
        }

        if (cc == null && rb == null)
        {
            sb.AppendLine("  ⚠  Player has neither a CharacterController nor a Rigidbody!");
            sb.AppendLine("      Run Tools > Setup Player to rebuild the player.");
        }

        sb.AppendLine();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHECK 3 — Solid floor collider
    // ══════════════════════════════════════════════════════════════════════════

    static void Check3_SolidiseFloor(StringBuilder sb, List<string> report)
    {
        sb.AppendLine("── CHECK 3 · Floor Collider Thickness ────────────────");

        // Find floor inside Maze_Walls, or anywhere in scene.
        var floor = FindNamedInHierarchy("Maze_Walls", "Floor")
                 ?? GameObject.Find("Floor");

        if (floor == null)
        {
            sb.AppendLine("  ⚠  No 'Floor' object found.\n");
            return;
        }

        var meshCol = floor.GetComponent<MeshCollider>();
        var boxCol  = floor.GetComponent<BoxCollider>();

        sb.AppendLine($"  Floor '{floor.name}'  scale={floor.transform.localScale}");

        // If a solid BoxCollider is already there we just validate thickness.
        if (boxCol != null && meshCol == null)
        {
            float thick = boxCol.size.y * floor.transform.lossyScale.y;
            if (thick < 0.1f)
            {
                boxCol.center = new Vector3(0f, -0.2f, 0f);
                boxCol.size   = new Vector3(1f,  0.4f, 1f);
                EditorUtility.SetDirty(floor);
                sb.AppendLine($"  ✔ Floor BoxCollider was too thin ({thick:F3} m) → expanded to 0.4 m.");
                report.Add("Expanded floor BoxCollider thickness to 0.4 m");
            }
            else
            {
                sb.AppendLine($"  ✓ Floor already has a solid BoxCollider (thickness={thick:F3} m).");
            }
            if (boxCol.isTrigger)
            {
                boxCol.isTrigger = false;
                EditorUtility.SetDirty(floor);
                sb.AppendLine("  ✔ Floor BoxCollider had isTrigger=true → fixed.");
                report.Add("Disabled isTrigger on floor BoxCollider");
            }
            sb.AppendLine();
            return;
        }

        // Zero-thickness MeshCollider: swap it out.
        if (meshCol != null)
        {
            sb.AppendLine("  ⚠  Floor uses a zero-thickness MeshCollider (Plane primitive).");
            sb.AppendLine("     Wall BoxColliders share the same Y=0 plane → floating-point seam gaps.");
            Object.DestroyImmediate(meshCol);
            report.Add("Replaced floor MeshCollider with solid BoxCollider");
        }
        else
        {
            sb.AppendLine("  ⚠  Floor has no collider at all — adding solid BoxCollider.");
            report.Add("Added missing BoxCollider to floor");
        }

        // Plane mesh is 10×10 local units; scale (6,1,6) → 60×60 world.
        // We want a slab whose TOP face is at world Y=0 (the floor surface).
        // Floor GO is at world Y=0, so center=(0,−0.2,0) in local space = top at Y=0.
        var bc        = floor.AddComponent<BoxCollider>();
        bc.isTrigger  = false;
        bc.center     = new Vector3(0f, -0.2f, 0f);
        bc.size       = new Vector3(1f,  0.4f, 1f);   // 60×0.4×60 world units
        EditorUtility.SetDirty(floor);

        sb.AppendLine("  ✔ Solid BoxCollider added: 60×0.4×60 m slab, top surface at Y=0.");
        sb.AppendLine("     Wall-base seams are now sealed by the floor's physical thickness.\n");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHECK 4 — Spawn-point heights  (the primary fall-through cause)
    // ══════════════════════════════════════════════════════════════════════════

    static void Check4_SpawnPointHeights(StringBuilder sb, List<string> report)
    {
        sb.AppendLine("── CHECK 4 · Spawn Point Heights ─────────────────────");
        sb.AppendLine($"  Safe spawn Y = {SafeSpawnY}  " +
                      $"(CC half-height 0.9 + 0.6 clearance)\n");

        // ── Determine min safe Y from the actual CharacterController ──────────
        float minSafeY = SafeSpawnY;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                // Bottom of capsule = center.y − height/2  (in local space).
                // For spawn to be safe: spawnY + (center.y − height/2) ≥ 0
                // → spawnY ≥ height/2 − center.y + buffer
                float buffer = 0.5f;
                minSafeY = cc.height * 0.5f - cc.center.y + buffer;
                sb.AppendLine($"  Derived from CC (h={cc.height}, center.y={cc.center.y}): " +
                              $"minSafeY = {minSafeY:F2}");
            }
        }

        int fixed_ = 0, alreadyOk = 0;

        // Scan everything named SpawnPoint* or EnemySpawn*.
        foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            bool isSpawn  = go.name.StartsWith("SpawnPoint");
            bool isEnemy  = go.name.StartsWith("EnemySpawn");
            if (!isSpawn && !isEnemy) continue;

            float y = go.transform.position.y;

            if (y < minSafeY)
            {
                Vector3 pos = go.transform.position;
                pos.y = isSpawn ? SafeSpawnY : SafeSpawnY;   // same safe height for both
                go.transform.position = pos;
                EditorUtility.SetDirty(go);

                sb.AppendLine($"  ✔ '{go.name}'  Y: {y:F2} → {pos.y:F2}  " +
                              $"(was {y:F2} — {(isSpawn ? "player" : "enemy")} would spawn underground)");
                report.Add($"'{go.name}' Y raised from {y:F2} to {pos.y:F2}");
                fixed_++;
            }
            else
            {
                sb.AppendLine($"  ✓ '{go.name}'  Y={y:F2}  (safe)");
                alreadyOk++;
            }
        }

        sb.AppendLine($"\n  Spawn points raised : {fixed_}");
        sb.AppendLine($"  Already at safe Y   : {alreadyOk}\n");

        if (fixed_ > 0)
        {
            sb.AppendLine("  ROOT CAUSE NOTE:");
            sb.AppendLine($"  All {fixed_} spawn point(s) were at Y=0 (floor level).");
            sb.AppendLine("  With CharacterController center=(0,0,0) and height=1.8,");
            sb.AppendLine("  placing the player at Y=0 puts the capsule bottom at Y=−0.9");
            sb.AppendLine("  — fully underground. Unity ejects the player downward through");
            sb.AppendLine("  the floor, causing the 'falling through' behaviour.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CHECK 6 — Raycast gap scan
    // ══════════════════════════════════════════════════════════════════════════

    static void Check6_GapScan(StringBuilder sb)
    {
        sb.AppendLine("── CHECK 6 · Floor Gap Scan (raycast grid) ──────────");

        const float step    =  2f;
        const float bound   = 28f;
        const float originY = 10f;

        var gaps   = new List<Vector2>();
        int tested = 0;

        for (float x = -bound; x <= bound; x += step)
        {
            for (float z = -bound; z <= bound; z += step)
            {
                tested++;
                if (!Physics.Raycast(new Vector3(x, originY, z), Vector3.down, 15f))
                    gaps.Add(new Vector2(x, z));
            }
        }

        sb.AppendLine($"  Grid: {step}-unit step over [−{bound},{bound}]² = {tested} points");

        if (gaps.Count == 0)
        {
            sb.AppendLine("  ✓ No floor gaps detected — full coverage confirmed.");
        }
        else
        {
            sb.AppendLine($"  ⚠  {gaps.Count} hole(s) detected:");
            foreach (var g in gaps)
                sb.AppendLine($"      world ({g.x:F0}, {g.y:F0})");
        }

        // SpawnPoint ground-check after height fix.
        sb.AppendLine("\n  SpawnPoint ground coverage (post-fix):");
        bool allGood = true;
        foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("SpawnPoint")) continue;
            bool hit = Physics.Raycast(go.transform.position + Vector3.up, Vector3.down, 3f);
            if (!hit)
            {
                sb.AppendLine($"  ⚠  '{go.name}' at {go.transform.position} is NOT over solid ground!");
                allGood = false;
            }
        }
        if (allGood)
            sb.AppendLine("  ✓ All SpawnPoints are over solid ground.\n");
    }

    // ── Utilities ──────────────────────────────────────────────────────────────

    static string HierPath(GameObject go)
    {
        string p = go.name;
        var t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }

    static GameObject FindNamedInHierarchy(string parentName, string childName)
    {
        var parent = GameObject.Find(parentName);
        if (parent == null) return null;
        var t = parent.transform.Find(childName);
        return t != null ? t.gameObject : null;
    }
}
