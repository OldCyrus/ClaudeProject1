using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools > Fix Hallway North Walls
///
/// Closes the two openings at the north side of the hallway (the cell-door side, Z=0).
/// The hallway extends 4 units past the cell block at each end:
///   Left gap:  X = -18 to -14  (west of Cell_1)
///   Right gap: X = +14 to +18  (east of Cell_8)
/// These get solid walls added, leaving the maze-side (south) walls untouched.
/// </summary>
public static class FixHallwayNorthWalls
{
    const float WallH = 4.0f;
    const float WallT = 0.5f;

    // Hallway extents
    const float HallX0 = -18f;   // west end
    const float HallX1 =  18f;   // east end
    const float CellX0 = -14f;   // west edge of cell block (Cell_1)
    const float CellX1 =  14f;   // east edge of cell block (Cell_8)
    const float NorthZ  =   0f;  // north wall of hallway (cell-door side)

    [MenuItem("Tools/Fix Hallway North Walls")]
    public static void Run()
    {
        // Find (or create) the Hallway parent
        var hallGO = GameObject.Find("Hallway");
        var parent = hallGO != null ? hallGO.transform : null;

        // Remove any existing versions of these walls to avoid doubles
        foreach (var nm in new[]{ "Hall_NorthL", "Hall_NorthR" })
        {
            var old = GameObject.Find(nm);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        // Left gap wall: X = -18 to -14
        float leftW  = CellX0 - HallX0;   // 4
        float leftCX = (HallX0 + CellX0) * 0.5f;   // -16
        AddWall("Hall_NorthL", parent, leftCX,  WallH * 0.5f, NorthZ + WallT * 0.5f, leftW,  WallH, WallT);

        // Right gap wall: X = +14 to +18
        float rightW  = HallX1 - CellX1;  // 4
        float rightCX = (CellX1 + HallX1) * 0.5f;  // +16
        AddWall("Hall_NorthR", parent, rightCX, WallH * 0.5f, NorthZ + WallT * 0.5f, rightW, WallH, WallT);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[FixHallwayNorthWalls] Added Hall_NorthL and Hall_NorthR.");
        EditorUtility.DisplayDialog("Fix Hallway North Walls",
            "Done.\n\n" +
            "Hall_NorthL: X = -18 to -14 (west gap closed)\n" +
            "Hall_NorthR: X = +14 to +18 (east gap closed)\n\n" +
            "Maze-side walls are untouched.\n\nSave (Ctrl+S).", "OK");
    }

    static void AddWall(string nm, Transform parent, float cx, float cy, float cz,
                        float sx, float sy, float sz)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nm;
        go.transform.position   = new Vector3(cx, cy, cz);
        go.transform.localScale = new Vector3(sx, sy, sz);
        go.isStatic = true;
        if (parent != null) go.transform.SetParent(parent, true);

        // Apply wall material if available, else grey URP mat
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prison/Wall_Material.mat");
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (wallMat != null)
            {
                mr.sharedMaterial = wallMat;
            }
            else
            {
                var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.22f));
                else mat.color = new Color(0.22f, 0.22f, 0.22f);
                mr.sharedMaterial = mat;
            }
        }
        Undo.RegisterCreatedObjectUndo(go, "Fix Hallway North Walls");
    }
}
