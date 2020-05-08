using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;


[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class WorldGenerator : MonoBehaviour
{
    public float size = 1;
    public int divisions = 5;
    public bool generate = true;
    public bool run = true;
    public int limitStep = 10;
    public float squarifyQuadsRate = 1.0f;
    public bool squarifyQuadsUniform = false;
    public int squarifyQuadsIterations = 10;
    public float squarifyQuadsBorderWeight = 1.0f;

    public int nextTileQ = 0;
    public int nextTileR = 0;

    BMesh bmesh;

    Dictionary<AxialCoordinate, BMesh> tileSet;

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
        bmesh.AddVertexAttribute(new BMesh.AttributeDefinition("restpos", BMesh.AttributeBaseType.Float, 3));
        bmesh.AddVertexAttribute(new BMesh.AttributeDefinition("weight", BMesh.AttributeBaseType.Float, 1));

        BMesh.Vertex v0 = bmesh.AddVertex(new Vector3(-1, 0, -1));
        BMesh.Vertex v1 = bmesh.AddVertex(new Vector3(-1, 0, 1));
        BMesh.Vertex v2 = bmesh.AddVertex(new Vector3(1, 0, 1));
        BMesh.Vertex v3 = bmesh.AddVertex(new Vector3(1, 0, -1));
        bmesh.AddFace(v0, v1, v2, v3);

        v0.attributes["restpos"] = new BMesh.FloatAttributeValue(v0.point);
        v0.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v1.attributes["restpos"] = new BMesh.FloatAttributeValue(v1.point);
        v1.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v2.attributes["restpos"] = new BMesh.FloatAttributeValue(v2.point);
        v2.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v3.attributes["restpos"] = new BMesh.FloatAttributeValue(v3.point);
        v3.attributes["weight"] = new BMesh.FloatAttributeValue(0);

        ShowArchived();
    }

    public void GenerateSubdividedHex()
    {
        int n = divisions;
        int pointcount = (2 * n + 1) * (2 * n + 1) - n * (n + 1);

        // on dual grid
        var offset = new AxialCoordinate(nextTileQ, nextTileR).Center(size * divisions * Mathf.Sqrt(3));
        float tmp = offset.x;
        offset.x = offset.y;
        offset.y = tmp;

        bmesh = new BMesh();
        bmesh.AddVertexAttribute(new BMesh.AttributeDefinition("restpos", BMesh.AttributeBaseType.Float, 3));
        bmesh.AddVertexAttribute(new BMesh.AttributeDefinition("weight", BMesh.AttributeBaseType.Float, 1));

        for (int i = 0; i < pointcount; ++i)
        {
            var co = AxialCoordinate.FromIndex(i, n);
            var prevCo = AxialCoordinate.FromIndex(i-1, n);
            var nextCo = AxialCoordinate.FromIndex(i+1, n);
            Vector2 c = co.Center(size) + offset;
            var v = bmesh.AddVertex(new Vector3(c.x, 0, c.y));
            v.id = i;
            v.attributes["restpos"] = new BMesh.FloatAttributeValue(v.point);
            v.attributes["weight"] = new BMesh.FloatAttributeValue(prevCo.q != co.q || co.q != nextCo.q || co.q == -n || co.q == n ? 1 : 0);
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
        ShowArchived();
        return;
    }

    public void GenerateTile()
    {
        GenerateSubdividedHex();
        RemoveEdges();
        Subdivide();
        for (int i = 0; i < 3; ++i) SquarifyQuads();
    }

    public void ValidateTile()
    {
        if (tileSet == null) tileSet = new Dictionary<AxialCoordinate, BMesh>();
        tileSet[new AxialCoordinate(nextTileQ, nextTileR)] = bmesh;
        bmesh = null;
    }

    public void ShowArchived()
    {
        var acc = new BMesh();
        if (tileSet != null)
        {
            foreach (var pair in tileSet)
            {
                BMeshOperators.Merge(acc, pair.Value);
            }
        }
        BMeshOperators.Merge(acc, bmesh);
        acc.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void ClearArchived()
    {
        tileSet = null;
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
        //ShowArchived();
        bmesh.SetInMeshFilter(GetComponent<MeshFilter>());
    }

    public void Subdivide()
    {
        if (bmesh == null) return;
        BMeshOperators.Subdivide(bmesh);

        // post process weight attribute
        foreach (var v in bmesh.vertices)
        {
            var weight = v.attributes["weight"] as BMesh.FloatAttributeValue;
            weight.data[0] = weight.data[0] < 1.0f ? 0.0f : 1.0f;
        }

        ShowArchived();
    }

    public void SquarifyQuads()
    {
        if (bmesh == null) return;

        // pre process weight attribute
        foreach (var v in bmesh.vertices)
        {
            var weight = v.attributes["weight"] as BMesh.FloatAttributeValue;
            weight.data[0] = weight.data[0] == 0.0f ? 0.0f : squarifyQuadsBorderWeight;
        }

        for (int i = 0; i < squarifyQuadsIterations; ++i)
        {
            BMeshOperators.SquarifyQuads(bmesh, squarifyQuadsRate, squarifyQuadsUniform);
        }
        ShowArchived();
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

        if (run)
        {
            SquarifyQuads();
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

        Gizmos.color = Color.blue;
        foreach (var v in bmesh.vertices)
        {
            float weight = (v.attributes["weight"] as BMesh.FloatAttributeValue).data[0];
            Gizmos.DrawSphere(v.point, weight * 0.1f);
        }
    }
}
