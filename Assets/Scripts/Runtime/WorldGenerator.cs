using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
    public int maxHeight = 5;
    public Transform cursor;
    public MeshFilter raycastMeshFilter;

    public int nextTileQ = 0;
    public int nextTileR = 0;

    class Tile
    {
        public BMesh mesh; // control mesh
        public BMesh skin; // visible mesh
    }

    BMesh bmesh;
    BMesh skinmesh;
    TileAxialCoordinate currentTileCo;
    Dictionary<AxialCoordinate, Tile> tileSet;

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
        TestAxialCoordinate.Run();
        TestBMeshOperators.Run();
    }

    public void PlayTestScenario()
    {
        Clear();
        GenerateTile(new TileAxialCoordinate(0, 0, divisions));
        ValidateTile();
        GenerateTile(new TileAxialCoordinate(1, 0, divisions));
        ValidateTile();
        nextTileQ = 0;
        nextTileR = 1;
        GenerateTile();
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

        ShowMesh();
    }

    public void GenerateSubdividedHex(TileAxialCoordinate tileCo = null)
    {
        if (tileCo == null) tileCo = NextTileCoordinate();
        int n = divisions;
        int pointcount = (2 * n + 1) * (2 * n + 1) - n * (n + 1);
        Vector2 offset = tileCo.Center(size);

        bmesh = new BMesh();
        bmesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2);
        bmesh.AddVertexAttribute("restpos", BMesh.AttributeBaseType.Float, 3);
        bmesh.AddVertexAttribute("weight", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("glued", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("occupancy", BMesh.AttributeBaseType.Float, maxHeight); // core voxel data

        for (int i = 0; i < pointcount; ++i)
        {
            var co = AxialCoordinate.FromIndex(i, n);
            Vector2 c = co.Center(size) + offset;
            var v = bmesh.AddVertex(new Vector3(c.x, 0, c.y));
            v.id = i;
            v.attributes["restpos"] = new BMesh.FloatAttributeValue(v.point);
            v.attributes["weight"] = new BMesh.FloatAttributeValue(co.OnRangeEdge(n) ? 1 : 0);
            v.attributes["uv"] = new BMesh.FloatAttributeValue(co.q, co.r);

            // Try to glue edge points to tiles that are already present
            var glued = v.attributes["glued"] as BMesh.FloatAttributeValue;
            if (tileSet != null)
            {
                foreach (var neighborTileCo in tileCo.NeighboringTiles(co))
                {
                    if (tileSet.ContainsKey(neighborTileCo))
                    {
                        glued.data[0] = 1;
                        break;
                    }
                }
            }
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
        currentTileCo = tileCo;
        ShowMesh();

        // DEBUG
        var occupancy = bmesh.vertices[pointcount / 2].attributes["occupancy"] as BMesh.FloatAttributeValue;
        occupancy.data[0] = 1;
        occupancy.data[1] = 1;

        occupancy = bmesh.vertices[pointcount / 2 + 2].attributes["occupancy"] as BMesh.FloatAttributeValue;
        occupancy.data[0] = 1;
    }

    public void GenerateTile(TileAxialCoordinate tileCo = null)
    {
        Random.InitState(3615);
        if (tileCo == null) tileCo = NextTileCoordinate();
        GenerateSubdividedHex(tileCo);
        RemoveEdges();
        Subdivide();
        for (int i = 0; i < 3; ++i) SquarifyQuads();

        // DEBUG
        var occupancy = bmesh.vertices[13].attributes["occupancy"] as BMesh.FloatAttributeValue;
        occupancy.data[0] = 1;
        occupancy = bmesh.vertices[26].attributes["occupancy"] as BMesh.FloatAttributeValue;
        occupancy.data[0] = 1;
        occupancy.data[1] = 1;
        (bmesh.vertices[126].attributes["occupancy"] as BMesh.FloatAttributeValue).data[0] = 0;
        (bmesh.vertices[121].attributes["occupancy"] as BMesh.FloatAttributeValue).data[0] = 0;
        (bmesh.vertices[18].attributes["occupancy"] as BMesh.FloatAttributeValue).data[0] = 0;
        (bmesh.vertices[66].attributes["occupancy"] as BMesh.FloatAttributeValue).data[1] = 0;
        (bmesh.vertices[18].attributes["occupancy"] as BMesh.FloatAttributeValue).data[1] = 0;
    }

    public void GenerateTileAtCursor()
    {
        if (cursor == null) return;
        Vector2 p = new Vector2(cursor.position.x, cursor.position.z);
        var tileCo = TileAxialCoordinate.AtPosition(p, size, divisions);
        if (tileSet != null && tileSet.ContainsKey(tileCo)) return;
        if (currentTileCo != null)
        {
            if (currentTileCo.Equals(tileCo)) return;
            else ValidateTile();
        }
        Debug.Log("Generating tile at " + tileCo + "...");
        GenerateTile(tileCo);
    }

    // Coord of the large tile
    TileAxialCoordinate NextTileCoordinate()
    {
        return new TileAxialCoordinate(nextTileQ, nextTileR, divisions);
    }

    public void ValidateTile()
    {
        if (currentTileCo == null || bmesh == null) return;
        if (tileSet == null) tileSet = new Dictionary<AxialCoordinate, Tile>();
        tileSet[currentTileCo] = new Tile { mesh = bmesh, skin = skinmesh };
        bmesh = null;
        skinmesh = null;
        currentTileCo = null;
    }

    public void ShowMesh()
    {
        var acc = new BMesh();
        acc.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2);
        if (tileSet != null)
        {
            foreach (var pair in tileSet)
            {
                BMeshOperators.Merge(acc, pair.Value.mesh);
                if (pair.Value.skin != null)
                {
                    BMeshOperators.Merge(acc, pair.Value.skin);
                }
            }
        }
        if (bmesh != null) BMeshOperators.Merge(acc, bmesh);
        if (skinmesh != null) BMeshOperators.Merge(acc, skinmesh);
        BMeshUnity.SetInMeshFilter(acc, GetComponent<MeshFilter>());
    }

    public void Clear()
    {
        bmesh = null;
        skinmesh = null;
        tileSet = null;
        currentTileCo = null;
        ShowMesh();
    }

    bool CanFuse(BMesh.Edge e) // iff it joins two triangles
    {
        var faces = e.NeighborFaces();
        return faces.Count == 2 && faces[0].vertcount == 3 && faces[1].vertcount == 3;
    }

    public bool FuseEdge(BMesh.Edge e) // iff it joins two triangles
    {
        var faces = e.NeighborFaces();
        if (!CanFuse(e)) return false;

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

        bmesh.RemoveEdge(e);
        bmesh.AddFace(vertices);
        return true;
    }

    public bool RemoveRandomEdge()
    {
        if (bmesh == null) return false;

        var candidates = new List<BMesh.Edge>();
        foreach (var e in bmesh.edges)
        {
            if (CanFuse(e))
            {
                candidates.Add(e);
            }
        }

        if (candidates.Count == 0) return false;

        int i = Random.Range(0, candidates.Count);
        FuseEdge(candidates[i]);
        return true;
    }

    public void RemoveEdges()
    {
        if (bmesh == null) return;
        while (RemoveRandomEdge()) { }
        ShowMesh();
    }

    public void Subdivide()
    {
        if (bmesh == null) return;
        BMeshOperators.Subdivide(bmesh);
        ShowMesh();
    }

    public void SquarifyQuads()
    {
        if (bmesh == null) return;

        // pre process weight attribute
        foreach (var v in bmesh.vertices)
        {
            var weight = v.attributes["weight"] as BMesh.FloatAttributeValue;
            var restpos = v.attributes["restpos"] as BMesh.FloatAttributeValue;
            var uv = v.attributes["uv"] as BMesh.FloatAttributeValue;
            var glued = v.attributes["glued"] as BMesh.FloatAttributeValue;

            weight.data[0] = weight.data[0] < 1.0f ? 0.0f : squarifyQuadsBorderWeight;

            if (glued.data[0] >= 1 && tileSet != null)
            {
                var fco = new FloatAxialCoordinate(uv.data[0], uv.data[1]);
                foreach (var neighborTileCo in currentTileCo.NeighboringTiles(fco))
                {
                    if (tileSet.ContainsKey(neighborTileCo))
                    {
                        var gluedCo = currentTileCo.ConvertLocalCoordTo(fco, neighborTileCo);
                        var gluedUv = new BMesh.FloatAttributeValue(gluedCo.q, gluedCo.r);
                        BMesh.Vertex target = BMeshOperators.Nearpoint(tileSet[neighborTileCo].mesh, gluedUv, "uv");
                        Debug.Assert(target != null);
                        float dist = BMesh.AttributeValue.Distance(target.attributes["uv"], gluedUv);
                        Debug.Assert(dist < 1e-5, "Distance in UVs is too large: " + dist);
                        v.attributes["restpos"] = new BMesh.FloatAttributeValue(target.point);
                        weight.data[0] = 9999;
                    }
                }
            }
        }

        for (int i = 0; i < squarifyQuadsIterations; ++i)
        {
            BMeshOperators.SquarifyQuads(bmesh, squarifyQuadsRate, squarifyQuadsUniform);
        }
        ShowMesh();
    }

    public void ComputeSkin()
    {
        if (bmesh == null) return;
        skinmesh = new BMesh();
        BMeshOperators.MarchingCubes(skinmesh, bmesh, "occupancy");
        ShowMesh();
    }

    public void Generate()
    {
        GenerateSubdividedHex(NextTileCoordinate());
    }

    public void ComputeRaycastMesh()
    {
        if (bmesh == null) return;
        var raycastMesh = new BMesh();
        raycastMesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2); // for vertex index
        raycastMesh.AddVertexAttribute("uv2", BMesh.AttributeBaseType.Float, 2) // for tile index
        .defaultValue = new BMesh.FloatAttributeValue(currentTileCo.q, currentTileCo.r);

        { int i = 0; foreach (BMesh.Vertex v in bmesh.vertices) { v.id = i++; } } // reset vertex ids

        foreach (BMesh.Vertex v in bmesh.vertices)
        {
            BMesh.Vertex nv = raycastMesh.AddVertex(v.point);
            (nv.attributes["uv"] as BMesh.FloatAttributeValue).data[0] = v.id;
            var faces = v.NeighborFaces();
            var verts = new List<BMesh.Vertex>();
            foreach (BMesh.Face f in faces)
            {
                BMesh.Vertex u = raycastMesh.AddVertex(f.Center());
                (u.attributes["uv"] as BMesh.FloatAttributeValue).data[0] = v.id;
                verts.Add(u);
            }
            int prev_i = verts.Count - 1;
            for (int i = 0; i < verts.Count; ++i)
            {
                raycastMesh.AddFace(verts[prev_i], nv, verts[i]);
                prev_i = i;
            }
        }
        if (raycastMeshFilter != null) BMeshUnity.SetInMeshFilter(raycastMesh, raycastMeshFilter);
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
            GenerateTileAtCursor();
        }
    }

    void OnDrawGizmos()
    {
        if (bmesh == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        BMeshUnity.DrawGizmos(bmesh);
        //if (skinmesh != null) BMeshUnity.DrawGizmos(skinmesh);

        foreach (var v in bmesh.vertices)
        {
            float weight = (v.attributes["weight"] as BMesh.FloatAttributeValue).data[0];
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(v.point, weight * 0.1f);

            var glued = v.attributes["glued"] as BMesh.FloatAttributeValue;
            Vector3 restpos = (v.attributes["restpos"] as BMesh.FloatAttributeValue).AsVector3();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(restpos, glued.data[0] * 0.15f);
        }

        if (bmesh.HasVertexAttribute("occupancy"))
        {
            foreach (var v in bmesh.vertices)
            {
                var occupancy = v.attributes["occupancy"] as BMesh.FloatAttributeValue;
                for (int i = 0; i < occupancy.data.Length; ++i)
                {
                    if (occupancy.data[i] > 0) Gizmos.color = Color.blue;
                    else Gizmos.color = Color.gray;
                    Gizmos.DrawCube(v.point + Vector3.up * i, Vector3.one * 0.1f);
                }
            }
        }
    }
}
