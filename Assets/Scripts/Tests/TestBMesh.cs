using UnityEngine;

public class TestBMesh
{
    static float epsilon = 1e-8f;
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

        // Edges
        BMesh.Edge e0 = mesh.FindEdge(v0, v1);
        BMesh.Edge e1 = mesh.FindEdge(v1, v2);
        BMesh.Edge e2 = mesh.FindEdge(v2, v3);
        BMesh.Edge e3 = mesh.FindEdge(v3, v0);
        Debug.Assert(e0 != null, "found edge v0->v1");
        Debug.Assert(e1 != null, "found edge v1->v2");
        Debug.Assert(e2 != null, "found edge v2->v3");
        Debug.Assert(e3 != null, "found edge v3->v0");

        Vector3 expected;
        expected = new Vector3(-1, 0, 0);
        Debug.Assert(Vector3.Distance(expected, e0.Center()) < epsilon, "edge 0 center");
        expected = new Vector3(0, 0, 1);
        Debug.Assert(Vector3.Distance(expected, e1.Center()) < epsilon, "edge 1 center");
        expected = new Vector3(1, 0, 0);
        Debug.Assert(Vector3.Distance(expected, e2.Center()) < epsilon, "edge 2 center");
        expected = new Vector3(0, 0, -1);
        Debug.Assert(Vector3.Distance(expected, e3.Center()) < epsilon, "edge 3 center");

        // face
        expected = new Vector3(0, 0, 0);
        Debug.Assert(Vector3.Distance(expected, f.Center()) < epsilon, "face center");

        // Loop consistency
        v0.id = 0; v1.id = 1; v2.id = 2; v3.id = 3;
        BMesh.Loop l = v0.edge.loop;
        BMesh.Loop it = l;
        int prevId = it.prev.vert.id;
        int forward = (prevId + 1) % 4 == it.vert.id ? 1 : 0;
        do
        {
            Debug.Assert((forward == 1 && (prevId + 1) % 4 == it.vert.id) || (it.vert.id + 1) % 4 == prevId, "valid quad loop order");
            prevId = it.vert.id;
            it = it.next;
        } while (it != l);

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

    static bool TestAttributes()
    {
        var mesh = new BMesh();

        BMesh.Vertex v0 = mesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = mesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = mesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = mesh.AddVertex(new Vector3(1, 0, -1));
        BMesh.Face f0 = mesh.AddFace(v0, v1, v2);
        BMesh.Face f1 = mesh.AddFace(v2, v1, v3);

        mesh.AddVertexAttribute(new BMesh.AttributeDefinition("test", BMesh.AttributeBaseType.Float, 3));
        var otherAttr = new BMesh.AttributeDefinition("other", BMesh.AttributeBaseType.Int, 1);
        var def = otherAttr.defaultValue as BMesh.IntAttributeValue;
        def.data[0] = 42;
        mesh.AddVertexAttribute(otherAttr);
        foreach (var v in mesh.vertices)
        {
            Debug.Assert(v.attributes.ContainsKey("test"), "vertex has test attribute");
            var test = v.attributes["test"] as BMesh.FloatAttributeValue;
            Debug.Assert(test != null, "vertex test attribute has float value");
            var testAsInt = v.attributes["test"] as BMesh.IntAttributeValue;
            Debug.Assert(testAsInt == null, "vertex test attribute has no int value");
            Debug.Assert(test.data.Length == 3, "vertex test attribute has 3 dimensions");
            Debug.Assert(test.data[0] == 0 && test.data[1] == 0 && test.data[2] == 0, "vertex test attribute has value (0, 0, 0)");

            Debug.Assert(v.attributes.ContainsKey("other"), "vertex has other attribute");
            var other = v.attributes["other"] as BMesh.IntAttributeValue;
            Debug.Assert(other.data.Length == 1, "vertex other attribute has 1 dimension");
            Debug.Assert(other.data[0] == 42, "vertex other attribute has value 42");
        }

        Debug.Log("TestBMesh TestAttributes passed.");

        return true;
    }

    public static bool Run()
    {
        if (!Test1()) return false;
        if (!Test2()) return false;
        if (!Test3()) return false;
        if (!TestAttributes()) return false;
        Debug.Log("All TestBMesh passed.");
        return true;
    }
}
