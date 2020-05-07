using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

public class TestBMeshOperators
{
    static bool TestSubdivideQuad()
    {
        var mesh = new BMesh();

        Vertex v0 = mesh.AddVertex(new Vector3(-1, 0, -1));
        Vertex v1 = mesh.AddVertex(new Vector3(-1, 0, 1));
        Vertex v2 = mesh.AddVertex(new Vector3(1, 0, 1));
        Vertex v3 = mesh.AddVertex(new Vector3(1, 0, -1));
        mesh.AddFace(v0, v1, v2, v3);

        BMeshOperators.Subdivide(mesh);

        Debug.Assert(mesh.vertices.Count == 9, "vertex count");
        Debug.Assert(mesh.edges.Count == 12, "edge count");
        Debug.Assert(mesh.loops.Count == 16, "loop count");
        Debug.Assert(mesh.faces.Count == 4, "face count");

        foreach (Face f in mesh.faces)
        {
            Debug.Assert(f.vertcount == 4, "faces are quads");
        }

        Debug.Log("TestBMeshOperators TestSubdivideQuad passed.");
        return true;
    }

    static bool TestSubdivideTris()
    {
        var mesh = new BMesh();

        BMesh.Vertex v0 = mesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = mesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = mesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = mesh.AddVertex(new Vector3(1, 0, -1));
        BMesh.Face f0 = mesh.AddFace(v0, v1, v2);
        BMesh.Face f1 = mesh.AddFace(v2, v1, v3);

        BMeshOperators.Subdivide(mesh);

        Debug.Assert(mesh.vertices.Count == 11, "vertex count");
        Debug.Assert(mesh.edges.Count == 16, "edge count");
        Debug.Assert(mesh.loops.Count == 24, "loop count");
        Debug.Assert(mesh.faces.Count == 6, "face count");

        foreach (Face f in mesh.faces)
        {
            Debug.Assert(f.vertcount == 4, "faces are quads");
        }

        Debug.Log("TestBMeshOperators TestSubdivideQuad passed.");
        return true;
    }

    public static bool Run()
    {
        if (!TestSubdivideQuad()) return false;
        if (!TestSubdivideTris()) return false;
        Debug.Log("All TestBMeshOperators passed.");
        return true;
    }
}
