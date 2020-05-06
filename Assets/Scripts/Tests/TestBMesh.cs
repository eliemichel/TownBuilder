using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class TestBMesh
{
    static bool Test1()
    {
        var mesh = new BMesh();

        BMesh.Vertex v0 = mesh.AddVertex(new Vector3(-0.5f, 0.0f, -Mathf.Sqrt(3) / 6));
        BMesh.Vertex v1 = mesh.AddVertex(new Vector3(0.5f, 0.0f, -Mathf.Sqrt(3) / 6));
        BMesh.Vertex v2 = mesh.AddVertex(new Vector3(0, 0.0f, Mathf.Sqrt(3) / 3));
        BMesh.Face f = mesh.AddFace(v0, v1, v2);

        Debug.Assert(mesh.vertices.Count == 3, "vert count");
        Debug.Assert(mesh.loops.Count == 3, "loop count");
        Debug.Assert(mesh.edges.Count == 3, "edge count");
        Debug.Assert(mesh.faces.Count == 1, "face count");

        BMesh.Loop l = mesh.loops[0];
        for (int i = 0; i < 3; ++i)
        {
            var v = mesh.vertices[i];
            Debug.Assert(mesh.loops[i].face == f, "loop has face");
            Debug.Assert(mesh.loops[i].edge != null, "loop has edge");
            Debug.Assert(mesh.edges[i].loop != null, "edge has loop");
            Debug.Assert(v.edge != null, "vertex has edge");
            Debug.Assert(v.edge.vert1 == v || v.edge.vert2 == v, "vertex is in vertex edge");
            Debug.Assert(l.next != l, "loop has next");
            Debug.Assert(l.next.prev == l, "loop has consistent next");
            Debug.Assert(l.radial_next.radial_prev == l, "loop has consistent radial next");
            l = l.next;
        }
        Debug.Assert(l == mesh.loops[0], "loop loops");

        Debug.Assert(mesh.FindEdge(v0, v1) != null, "edge between v0 and v1");
        Debug.Assert(mesh.FindEdge(v0, v2) != null, "edge between v0 and v2");
        Debug.Assert(mesh.FindEdge(v2, v1) != null, "edge between v2 and v1");

        Debug.Log("TestBMesh #1 passed.");

        return true;
    }

    static bool Test2()
    {
        var mesh = new BMesh();

        BMesh.Vertex v0 = mesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = mesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = mesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = mesh.AddVertex(new Vector3(1, 0, -1));
        BMesh.Face f = mesh.AddFace(v0, v1, v2, v3);

        Debug.Assert(mesh.vertices.Count == 4, "vert count");
        Debug.Assert(mesh.loops.Count == 4, "loop count");
        Debug.Assert(mesh.edges.Count == 4, "edge count");
        Debug.Assert(mesh.faces.Count == 1, "face count");

        for (int i = 0; i < 4; ++i)
        {
            var v = mesh.vertices[i];
            Debug.Assert(mesh.loops[i].face == f);
            Debug.Assert(v.edge != null);
            Debug.Assert(v.edge.vert1 == v || v.edge.vert2 == v);
        }

        Debug.Assert(mesh.FindEdge(v0, v1) != null, "edge between v0 and v1");

        mesh.RemoveEdge(mesh.edges[0]);
        Debug.Assert(mesh.vertices.Count == 4, "vert count after removing edge");
        Debug.Assert(mesh.loops.Count == 0, "loop count after removing edge");
        Debug.Assert(mesh.edges.Count == 3, "edge count after removing edge");
        Debug.Assert(mesh.faces.Count == 0, "face count after removing edge");

        Debug.Log("TestBMesh #2 passed.");

        return true;
    }

    static bool Test3()
    {
        var mesh = new BMesh();

        BMesh.Vertex v0 = mesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = mesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = mesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = mesh.AddVertex(new Vector3(1, 0, -1));
        BMesh.Face f0 = mesh.AddFace(v0, v1, v2);
        BMesh.Face f1 = mesh.AddFace(v2, v1, v3);

        Debug.Assert(mesh.vertices.Count == 4, "vert count");
        Debug.Assert(mesh.loops.Count == 6, "loop count");
        Debug.Assert(mesh.edges.Count == 5, "edge count");
        Debug.Assert(mesh.faces.Count == 2, "face count");

        BMesh.Edge e0 = null;
        foreach (BMesh.Edge e in mesh.edges)
        {
            if ((e.vert1 == v1 && e.vert2 == v2) || (e.vert1 == v2 && e.vert2 == v1))
            {
                e0 = e;
                break;
            }
        }
        Debug.Assert(e0 != null, "found edge between v1 and v2");
        mesh.RemoveEdge(e0);
        Debug.Assert(mesh.vertices.Count == 4, "vert count after removing edge");
        Debug.Assert(mesh.loops.Count == 0, "loop count after removing edge");
        Debug.Assert(mesh.edges.Count == 4, "edge count after removing edge");
        Debug.Assert(mesh.faces.Count == 0, "face count after removing edge");

        Debug.Log("TestBMesh #3 passed.");

        return true;
    }

    public static bool Run()
    {
        if (!Test1()) return false;
        if (!Test2()) return false;
        if (!Test3()) return false;
        Debug.Log("All TestBMesh passed.");
        return true;
    }
}
