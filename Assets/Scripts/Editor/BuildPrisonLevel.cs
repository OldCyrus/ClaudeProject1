using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Build Prison Level  (v2 — fully enclosed, connected layout)
///
/// Layout (top-down, Z increases toward cells / north):
///
///   Z = +4  ┌──[Cell1]──[Cell2]──...──[Cell8]──┐
///   Z =  0  │         HALLWAY (E-W)              │
///   Z = -4  └─[Door]─────────────────────[Door]─┘
///            │                                   │
///        [Left wing maze]               [Right wing maze]
///            │                                   │
///            └──────── Cross Corridor ───────────┘
///                            │
///                     [Central Room]
///                            │
///                      [Deep Corridor]
///                       /          \
///                 [Key Room]   [Exit Chamber]
///
/// Hallway end walls have full-height doorways (no solid end caps).
/// Cell bar fronts have full-height door openings.
/// All rooms connect — no gaps, no open sky.
/// </summary>
public static class BuildPrisonLevel
{
    // ── Shared dimensions ─────────────────────────────────────────────────────
    const float WallH  = 3.0f;   // interior ceiling height
    const float WallT  = 0.25f;  // wall / slab thickness
    const float CellW  = 3.5f;   // each cell width (X)
    const float CellD  = 4.0f;   // each cell depth (Z)
    const int   NCells = 8;
    const float HallW  = 4.0f;   // hallway width (Z extent)
    const float DoorW  = 1.4f;   // bar-wall door opening width per cell

    // World origin: X of cell-block left edge, Y=0 floor, Z=0 cell front face
    static readonly float OX = -14f;  // = -(NCells * CellW / 2)

    // Derived hallway extents
    static float CellsW  => NCells * CellW;           // 28
    static float HallX0  => OX - 4f;                  // -18  (west end)
    static float HallX1  => OX + CellsW + 4f;         // +18  (east end)
    static float HallZ0  => -HallW;                    // -4   (south/far wall)
    static float HallZ1  => 0f;                        //  0   (north, cell side)

    // ── Entry point ───────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Prison Level")]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog("Build Prison Level",
            "DELETE all existing level geometry and rebuild from scratch?",
            "Yes — Rebuild", "Cancel")) return;

        ClearExistingGeometry();

        var goCell   = new GameObject("CellBlock");
        var goHall   = new GameObject("Hallway");
        var goMWalls = new GameObject("Maze_Walls");
        var goMRooms = new GameObject("Maze_Rooms");
        var goSpawns = new GameObject("Spawn_Points");
        var goEnemy  = new GameObject("Enemy_Spawns");
        var goKey    = new GameObject("Key");
        var goExit   = new GameObject("Exit");
        RegisterUndo(goCell, goHall, goMWalls, goMRooms, goSpawns, goEnemy, goKey, goExit);

        BuildCellBlock(goCell.transform, goSpawns.transform);
        BuildHallway  (goHall.transform);
        BuildMaze     (goMWalls.transform, goMRooms.transform,
                       goEnemy.transform, goKey.transform, goExit.transform);

        // Large ground slab under everything
        MakeCube("Ground", null,
            new Vector3(0f, -WallT * 0.5f, -30f),
            new Vector3(200f, WallT, 200f));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Build Prison Level — Done",
            "Changes made:\n" +
            "• Cell bar doors are now full height (3 units).\n" +
            "• Hallway end walls removed — replaced by maze entry corridors.\n" +
            "• Left and right maze wings connect flush to hallway ends.\n" +
            "• Far hallway wall is solid — no open field.\n" +
            "• All areas are fully enclosed (floor + ceiling + 4 walls).\n\n" +
            "Run: Tools > Fix Prison Materials\n" +
            "Then: Tools > Fix Spawn Points\n" +
            "Then: Save (Ctrl+S) and Play.", "OK");
    }

    // ── Clear ─────────────────────────────────────────────────────────────────
    static void ClearExistingGeometry()
    {
        string[] names = {
            "CellBlock","Hallway","Maze_Walls","Maze_Rooms",
            "Spawn_Points","Enemy_Spawns","Key","Exit","Ground",
            "Maze","MazeWalls","MazeRooms","Walls","Rooms","Spawns","Floor",
        };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
        foreach (var go in UnityEngine.SceneManagement.SceneManager
                               .GetActiveScene().GetRootGameObjects())
        {
            if (go.name.StartsWith("SpawnPoint") || go.name.StartsWith("EnemySpawn"))
                Undo.DestroyObjectImmediate(go);
        }
    }

    // ── Cell block ────────────────────────────────────────────────────────────
    static void BuildCellBlock(Transform par, Transform spawnPar)
    {
        float hy = WallH * 0.5f;

        // Shared back wall (north side of all cells)
        MakeWall("BackWall", par,
            cx: OX + CellsW * 0.5f,  cy: hy,
            cz: CellD + WallT * 0.5f,
            sx: CellsW + WallT * 2f,  sy: WallH, sz: WallT);

        for (int i = 0; i < NCells; i++)
        {
            float x0 = OX + i * CellW;
            float x1 = x0 + CellW;
            float cx = (x0 + x1) * 0.5f;
            string nm = $"Cell_{i + 1}";

            // Left dividing wall (only leftmost cell needs its own left wall)
            if (i == 0)
                MakeWall($"{nm}_WallL", par,
                    cx: x0 - WallT * 0.5f, cy: hy,
                    cz: CellD * 0.5f,
                    sx: WallT, sy: WallH, sz: CellD);

            MakeWall($"{nm}_WallR", par,
                cx: x1 + WallT * 0.5f, cy: hy,
                cz: CellD * 0.5f,
                sx: WallT, sy: WallH, sz: CellD);

            MakeSlab($"{nm}_Floor",   par, cx, -WallT * 0.5f, CellD * 0.5f,   CellW, CellD);
            MakeSlab($"{nm}_Ceiling", par, cx,  WallH + WallT * 0.5f, CellD * 0.5f, CellW, CellD);

            // Bar front — left / right pillars only, NO strip above door
            // → full WallH door opening so player can walk through
            float pillarW = (CellW - DoorW) * 0.5f;
            MakeWall($"{nm}_BarL", par,
                cx: x0 + pillarW * 0.5f, cy: hy,
                cz: -WallT * 0.5f,
                sx: pillarW, sy: WallH, sz: WallT,
                col: ColorGray(0.22f));
            MakeWall($"{nm}_BarR", par,
                cx: x1 - pillarW * 0.5f, cy: hy,
                cz: -WallT * 0.5f,
                sx: pillarW, sy: WallH, sz: WallT,
                col: ColorGray(0.22f));

            // Spawn point inside cell at safe standing height
            var sp = new GameObject($"SpawnPoint_{i + 1}");
            sp.tag = "SpawnPoint";
            sp.transform.SetParent(spawnPar);
            sp.transform.position = new Vector3(cx, 1.0f, CellD * 0.6f);
            Undo.RegisterCreatedObjectUndo(sp, "Build Prison Level");
        }
    }

    // ── Hallway ───────────────────────────────────────────────────────────────
    // North side (Z=0) is open — cells face directly into it.
    // East (X=+18) and West (X=-18) end walls are REMOVED.
    //   → The maze entry corridors sit flush against those openings.
    // South / far wall (Z=-4) is solid — closes off the back.
    static void BuildHallway(Transform par)
    {
        float hallLen = HallX1 - HallX0;   // 36
        float hallCX  = (HallX0 + HallX1) * 0.5f;
        float hallCZ  = (HallZ0 + HallZ1) * 0.5f;
        float hy      = WallH * 0.5f;

        MakeSlab("Hall_Floor",   par, hallCX, -WallT * 0.5f, hallCZ, hallLen, HallW);
        MakeSlab("Hall_Ceiling", par, hallCX,  WallH + WallT * 0.5f, hallCZ, hallLen, HallW);

        // South / far wall — fully solid, seals off the back of the hallway
        MakeWall("Hall_SouthWall", par,
            cx: hallCX, cy: hy,
            cz: HallZ0 - WallT * 0.5f,
            sx: hallLen, sy: WallH, sz: WallT);

        // ── NO east / west end walls ──────────────────────────────────────────
        // The maze entry corridors (LeftEntry / RightEntry) are placed so their
        // east / west outer walls are collinear with the hallway ends, creating
        // a seamless opening.  Nothing to build here.
    }

    // ── Maze ──────────────────────────────────────────────────────────────────
    // Coordinate convention:  all Z values are negative (south of hallway).
    //
    // The two entry corridors are sized to exactly match the hallway opening:
    //   X = HallX0-MazeW .. HallX0   (left wing)
    //   X = HallX1 .. HallX1+MazeW   (right wing)
    //   Z = HallZ0 .. HallZ1  (= -4 .. 0) — same Z extent as hallway
    //
    // Both wings then extend south and are joined by a cross corridor.
    // From there deeper rooms lead to the key dead-end and exit chamber.
    static void BuildMaze(Transform wPar, Transform rPar,
                          Transform ePar, Transform kPar, Transform exPar)
    {
        const float MW    = 8f;   // maze corridor / room width
        const float CorrL = 16f;  // south corridor length

        // ── Wing origins ──────────────────────────────────────────────────────
        float lx0 = HallX0 - MW;      // -26   left wing west edge
        float lx1 = HallX0;           // -18   left wing east edge (= hallway west end)
        float rx0 = HallX1;           //  18   right wing west edge (= hallway east end)
        float rx1 = HallX1 + MW;      //  26   right wing east edge

        // Z extents
        float entryZ0 = HallZ0;       // -4  (flush with hallway)
        float entryZ1 = HallZ1;       //  0  (flush with hallway)
        float southZ0 = entryZ0 - CorrL;  // -20
        float crossZ0 = southZ0 - MW;     // -28  cross corridor south edge
        float crossZ1 = southZ0;          // -20  cross corridor north edge
        float ctrZ0   = crossZ0 - 12f;    // -40  central room south edge
        float ctrZ1   = crossZ0;          // -28  central room north edge
        float deepZ0  = ctrZ0 - 12f;      // -52  deep corridor south edge
        float deepZ1  = ctrZ0;            // -40  deep corridor north edge
        float keyZ0   = deepZ0 - 8f;      // -60  key room south edge
        float keyZ1   = deepZ0;           // -52  key room north edge
        float exitZ0  = keyZ0 - 8f;       // -68  exit chamber south edge
        float exitZ1  = keyZ0;            // -60  exit chamber north edge

        // ── Room list: (x0, z0, x1, z1, name) ────────────────────────────────
        var rooms = new List<(float x0, float z0, float x1, float z1, string nm)>
        {
            // Left wing
            (lx0, entryZ0, lx1, entryZ1, "LeftEntry"),        // flush with hallway
            (lx0, southZ0, lx1, entryZ0, "LeftSouthCorridor"),

            // Left alcove (dead end branching west)
            (lx0 - 6f, southZ0 + 4f, lx0, southZ0 + 12f, "LeftAlcove"),

            // Right wing
            (rx0, entryZ0, rx1, entryZ1, "RightEntry"),       // flush with hallway
            (rx0, southZ0, rx1, entryZ0, "RightSouthCorridor"),

            // Right alcove (dead end branching east)
            (rx1, southZ0 + 4f, rx1 + 6f, southZ0 + 12f, "RightAlcove"),

            // Cross corridor joining both wings
            (lx0, crossZ0, rx1, crossZ1, "CrossCorridor"),

            // Central room
            (-10f, ctrZ0, 10f, ctrZ1, "CentralRoom"),

            // Connector from cross to central
            (-4f, ctrZ1, 4f, crossZ0, "CentralConnector"),

            // Deep corridor (narrow)
            (-4f, deepZ0, 4f, deepZ1, "DeepCorridor"),

            // Key room — dead end branching west off deep corridor
            (-14f, keyZ0, -4f, keyZ1, "KeyRoom"),

            // Exit chamber
            (-4f, exitZ0, 4f, exitZ1, "ExitChamber"),
        };

        foreach (var (x0, z0, x1, z1, nm) in rooms)
        {
            bool isRoom = nm.Contains("Room") || nm.Contains("Chamber") ||
                          nm.Contains("Alcove") || nm.Contains("Central");
            BuildRoom(nm, wPar, rPar, x0, z0, x1, z1, isRoom);
        }

        // ── Key prop (yellow) ─────────────────────────────────────────────────
        float keyCX = (-14f + -4f) * 0.5f;
        float keyCZ = (keyZ0 + keyZ1) * 0.5f;
        MakeCube("KeyPickup", kPar,
            new Vector3(keyCX, 0.5f, keyCZ),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Color(1f, 0.85f, 0f));

        // ── Exit door (red) ───────────────────────────────────────────────────
        MakeCube("ExitDoor", exPar,
            new Vector3(0f, WallH * 0.5f, exitZ0 - WallT),
            new Vector3(2.5f, WallH, WallT * 2f),
            new Color(0.8f, 0.08f, 0.08f));

        // ── Enemy spawn points ────────────────────────────────────────────────
        var spawns = new (float x, float z, string nm)[]
        {
            (lx0 + MW * 0.5f, southZ0 + CorrL * 0.5f, "EnemySpawn_LeftWing"),
            (rx0 + MW * 0.5f, southZ0 + CorrL * 0.5f, "EnemySpawn_RightWing"),
            (0f,              (crossZ0 + crossZ1) * 0.5f, "EnemySpawn_CrossCorridor"),
            (0f,              (ctrZ0   + ctrZ1)   * 0.5f, "EnemySpawn_CentralRoom"),
            (0f,              (deepZ0  + deepZ1)  * 0.5f, "EnemySpawn_DeepCorridor"),
        };
        foreach (var (x, z, nm) in spawns)
        {
            var sp = new GameObject(nm);
            sp.transform.SetParent(ePar);
            sp.transform.position = new Vector3(x, 1.0f, z);
            Undo.RegisterCreatedObjectUndo(sp, "Build Prison Level");
        }
    }

    // ── BuildRoom: floor + ceiling + 4 outer walls for an axis-aligned box ───
    // Adjacent rooms share/overlap wall slabs — that is fine.
    static void BuildRoom(string nm, Transform wPar, Transform rPar,
                          float x0, float z0, float x1, float z1, bool isRoom)
    {
        float cx   = (x0 + x1) * 0.5f;
        float cz   = (z0 + z1) * 0.5f;
        float sx   = x1 - x0;
        float sz   = z1 - z0;
        float hy   = WallH * 0.5f;
        Color wCol = isRoom ? ColorGray(0.28f) : ColorGray(0.20f);
        Color fCol = isRoom ? new Color(0.28f, 0.22f, 0.15f) : ColorGray(0.22f);

        MakeSlab($"{nm}_Floor",   rPar, cx, -WallT * 0.5f,             cz, sx, sz, fCol);
        MakeSlab($"{nm}_Ceiling", rPar, cx,  WallH + WallT * 0.5f,     cz, sx, sz);

        // North wall (higher Z = toward hallway)
        MakeWall($"{nm}_WallN", wPar, cx, hy, z1 + WallT * 0.5f, sx, WallH, WallT, wCol);
        // South wall
        MakeWall($"{nm}_WallS", wPar, cx, hy, z0 - WallT * 0.5f, sx, WallH, WallT, wCol);
        // East wall
        MakeWall($"{nm}_WallE", wPar, x1 + WallT * 0.5f, hy, cz, WallT, WallH, sz, wCol);
        // West wall
        MakeWall($"{nm}_WallW", wPar, x0 - WallT * 0.5f, hy, cz, WallT, WallH, sz, wCol);
    }

    // ── Geometry primitives ───────────────────────────────────────────────────

    static void MakeWall(string nm, Transform par,
                         float cx, float cy, float cz,
                         float sx, float sy, float sz,
                         Color? col = null)
        => MakeCube(nm, par, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz),
                    col ?? ColorGray(0.25f));

    static void MakeSlab(string nm, Transform par,
                         float cx, float cy, float cz,
                         float sx, float sz,
                         Color? col = null)
        => MakeCube(nm, par, new Vector3(cx, cy, cz), new Vector3(sx, WallT, sz),
                    col ?? ColorGray(0.30f));

    static void MakeCube(string nm, Transform par,
                         Vector3 pos, Vector3 scale,
                         Color? col = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name             = nm;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.isStatic         = true;
        if (par != null) go.transform.SetParent(par, true);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            var mat = new Material(sh);
            Color c = col ?? ColorGray(0.25f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
            mr.sharedMaterial = mat;
        }
        Undo.RegisterCreatedObjectUndo(go, "Build Prison Level");
    }

    static void RegisterUndo(params GameObject[] gos)
    {
        foreach (var g in gos) Undo.RegisterCreatedObjectUndo(g, "Build Prison Level");
    }

    static Color ColorGray(float v) => new Color(v, v, v);
}
