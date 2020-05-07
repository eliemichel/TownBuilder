using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;


[RequireComponent(typeof(MeshFilter))]
public class WorldGenerator : MonoBehaviour
{
    public float size = 1;
    public int divisions = 5;
    public bool generate = true;
    public int limitStep = 10;
    public float squarifyQuadsRate = 1.0f;
    public int squarifyQuadsIterations = 10;

    BMesh bmesh;

    void GenerateSimpleHex()
    {
        Vector3[] vertices = new Vector3[7];
        Vector2[] uvs = new Vector2[7];
        int[] triangles = new int[3*6];

        for (int i = 0; i < 6; ++i)
        {
            float th = i * 2 * Mathf.PI / 6;
            float c = Mathf.Cos(th);
            float s = Mathf.Sin(th);
            vertices[i] = new Vector3(size * c, 0, size * s);

            uvs[i] = new Vector2(c * 0.5f + 0.5f, s * 0.5f + 0.5f);

            triangles[3 * i + 0] = (i + 1) % 6;
            triangles[3 * i + 1] = i;
            triangles[3 * i + 2] = 6;
        }
        vertices[6] = new Vector3(0, 0, 0);
        uvs[6] = new Vector2(0.5f, 0.5f);

        // Create mesh
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
    }

    public void Test()
    {
        TestBMesh.Run();
        TestAxialCoordinate.Run(divisions);
        TestBMeshOperators.Run();
    }

    public void GenerateQuad()
    {
        bmesh = new BMesh();

        BMesh.Vertex v0 = bmesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = bmesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = bmesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = bmesh.AddVertex(new Vector3(1, 0, -1));
        bmesh.AddFace(v0, v1, v2, v3);

        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void GenerateSubdividedHex()
    {
        int n = divisions;
        int pointcount = (2 * n + 1) * (2 * n + 1) - n * (n + 1);

        bmesh = new BMesh();
        for (int i = 0; i < pointcount; ++i)
        {
            var co = AxialCoordinate.FromIndex(i, n);
            Vector2 c = co.Center(size);
            bmesh.AddVertex(new Vector3(c.x, 0, c.y)).id = i;
        }

        int step = 0;
        for (int i = 0; i < pointcount && step < limitStep; ++i)
        {
            var co = AxialCoordinate.FromIndex(i, n);
            var co2 = new AxialCoordinate(co.q + 1, co.r - 1); // right up of co
            var co3 = new AxialCoordinate(co.q + 1, co.r); // beneath co2
            var co4 = new AxialCoordinate(co.q, co.r + 1); // beneath co

            if (co2.InRange(n) && co3.InRange(n))
            {
                bmesh.AddFace(i, co3.ToIndex(n), co2.ToIndex(n));
                ++step;
                if (step >= limitStep) break;
            }

            if (co3.InRange(n) && co4.InRange(n))
            {
                bmesh.AddFace(i, co4.ToIndex(n), co3.ToIndex(n));
                ++step;
            }
        }
        Debug.Assert(bmesh.faces.Count == 6 * n * n);
        Debug.Assert(bmesh.loops.Count == 3 * 6 * n * n);
        Debug.Assert(bmesh.vertices.Count == pointcount);
        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
        return;
    }

    public bool FuseEdge(int i) // iff it joins two triangles
    {
        var e = bmesh.edges[i];
        var faces = e.NeighborFaces();
        bool isValidEdge = faces.Count == 2 && faces[0].vertcount == 3 && faces[1].vertcount == 3;
        if (!isValidEdge) return false;

        var vertices = new BMesh.Vertex[4];
        vertices[0] = e.vert1;
        vertices[1] = null;
        vertices[2] = e.vert2;
        vertices[3] = null;
        foreach (var face in faces)
        {
            foreach (var v in face.NeighborVertices())
            {
                if (!e.ContainsVertex(v))
                {
                    if (vertices[1] == null) vertices[1] = v;
                    else vertices[3] = v;
                }
            }
        }
        Debug.Assert(vertices[0] != null && vertices[1] != null && vertices[2] != null && vertices[3] != null);

        bmesh.RemoveEdge(bmesh.edges[i]);
        bmesh.AddFace(vertices);
        return true;
    }

    public bool RemoveRandomEdge()
    {
        if (bmesh == null) return false;
        if (bmesh.edges.Count == 0) return false;
        int i = Random.Range(0, bmesh.edges.Count);
        var e = bmesh.edges[i];
        int i0 = i;
        while (!FuseEdge(i))
        {
            i = (i + 1) % bmesh.edges.Count;
            if (i == i0)
            {
                return false;
            }
        }
        return true;
    }

    public void RemoveEdges()
    {
        if (bmesh == null) return;
        while (RemoveRandomEdge()) { }
        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void Subdivide()
    {
        if (bmesh == null) return;
        BMeshOperators.Subdivide(bmesh);
        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void SquarifyQuads()
    {
        if (bmesh == null) return;
        for (int i = 0; i < squarifyQuadsIterations; ++i)
        {
            BMeshOperators.SquarifyQuads(bmesh, squarifyQuadsRate);
        }
        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void Generate()
    {
        GenerateSubdividedHex();
    }

    void Update()
    {
        if (generate)
        {
            Generate();
            generate = false;
        }
    }

    void OnDrawGizmos()
    {
        if (bmesh == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        foreach (var e in bmesh.edges)
        {
            Gizmos.DrawLine(e.vert1.point, e.vert2.point);
        }
        Gizmos.color = Color.red;
        foreach (var l in bmesh.loops)
        {
            BMesh.Vertex vert = l.vert;
            BMesh.Vertex other = l.edge.OtherVertex(vert);
            Gizmos.DrawRay(vert.point, (other.point - vert.point) * 0.1f);

            BMesh.Loop nl = l.next;
            BMesh.Vertex nother = nl.edge.ContainsVertex(vert) ? nl.edge.OtherVertex(vert) : nl.edge.OtherVertex(other);
            Vector3 no = vert.point + (other.point - vert.point) * 0.1f;
            Gizmos.DrawRay(no, (nother.point - no) * 0.1f);
        }
    }
}
