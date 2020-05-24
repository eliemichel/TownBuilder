using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

public class TestBMeshMarchingCubes
{
    static float epsilon = 1e-6f;
    
    static bool TestMarchingCubes()
    {
        var grid = new BMesh();
        for (int i = 0; i < 9; ++i)
        {
            grid.AddVertex(new Vector3(i % 3 - 1, 0, i / 3 - 1));
        }
        grid.AddFace(0, 1, 4, 3);
        grid.AddFace(1, 2, 5, 4);
        grid.AddFace(3, 4, 7, 6);
        grid.AddFace(4, 5, 8, 7);

        Debug.Assert(Vector3.Distance(grid.vertices[4].point, Vector3.zero) < epsilon, "grid is centered");

        grid.AddVertexAttribute("occupancy", AttributeBaseType.Float, 5);
        (grid.vertices[4].attributes["occupancy"] as FloatAttributeValue).data[0] = 1;

        var mesh = new BMesh();
        BMeshMarchingCubes.Run(mesh, grid, "occupancy");

        Debug.Assert(mesh.faces.Count == 4, "face count (expected 4, found " + mesh.faces.Count + ")");
        Debug.Assert(mesh.loops.Count == 12, "loop count (expected 12, found " + mesh.loops.Count + ")");
        // Not relevant here because for now we don't remove doubled vertices
        //Debug.Assert(mesh.vertices.Count == 5, "vertex count (expected 5, found " + mesh.vertices.Count + ")");
        //Debug.Assert(mesh.edges.Count == 8, "edge count (expected 8, found " + mesh.edges.Count + ")");

        if (!BMeshMarchingCubes.Test()) return false;

        Debug.Log("TestBMeshOperators TestMarchingCubes passed.");
        return true;
    }

    public static bool Run()
    {
        if (!TestMarchingCubes()) return false;
        Debug.Log("All TestBMeshMarchingCubes passed.");
        return true;
    }
}
