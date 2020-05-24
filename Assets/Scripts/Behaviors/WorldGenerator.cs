using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/**
 * World generator and also world runtime controller
 */
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class WorldGenerator : MonoBehaviour
{
    #region [Public parameters]
    public float size = 1;
    public int divisions = 5;
    public bool run = true;
    public int limitStep = 10;
    public float squarifyQuadsRate = 1.0f;
    public bool squarifyQuadsUniform = false;
    public int squarifyQuadsIterations = 10;
    public float squarifyQuadsBorderWeight = 1.0f;
    public int maxHeight = 5; // must not change because encoding of DualNgon (for raycast mesh) relies on it
    public Transform generatorCursor;
    public MeshFilter raycastMeshFilter;
    public MeshFilter cursorMeshFilter;
    public MarchingModuleManager moduleManager;

    public int nextTileQ = 0;
    public int nextTileR = 0;
    #endregion

    #region [Private attributes]
    class Tile
    {
        public BMesh mesh; // control mesh
        public BMesh skin; // visible mesh
    }

    class Cursor
    {
        public int vertexId;
        public TileAxialCoordinate tileCo;
        public int edgeIndex; // -2: bellow, -1: above, 0+: side
        public int floor;
    }

    TileAxialCoordinate currentTileCo;
    Tile currentTile;
    Dictionary<AxialCoordinate, Tile> tileSet;
    Cursor cursor;

    #endregion

    #region [World Generator]
    public void Test()
    {
        TestBMesh.Run();
        TestAxialCoordinate.Run();
        TestBMeshOperators.Run();
    }

    public void GenerateQuad()
    {
        var bmesh = new BMesh();
        bmesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2);
        bmesh.AddVertexAttribute("restpos", BMesh.AttributeBaseType.Float, 3);
        bmesh.AddVertexAttribute("weight", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("glued", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("occupancy", BMesh.AttributeBaseType.Float, maxHeight); // core voxel data

        BMesh.Vertex v0 = bmesh.AddVertex(new Vector3(-1, 0, -1) * 8);
        BMesh.Vertex v1 = bmesh.AddVertex(new Vector3(1, 0, -1) * 8);
        BMesh.Vertex v2 = bmesh.AddVertex(new Vector3(1, 0, 1) * 8);
        BMesh.Vertex v3 = bmesh.AddVertex(new Vector3(-1, 0, 1) * 8);
        bmesh.AddFace(v0, v1, v2, v3);

        foreach (var v in bmesh.vertices)
        {
            v.attributes["restpos"] = new BMesh.FloatAttributeValue(v.point);
        }

        v0.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v1.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v2.attributes["weight"] = new BMesh.FloatAttributeValue(1);
        v3.attributes["weight"] = new BMesh.FloatAttributeValue(0);

        currentTile.mesh = bmesh;
        currentTileCo = new TileAxialCoordinate(0, 0, divisions);
        ShowMesh();
    }

    public void GenerateSubdividedHex(TileAxialCoordinate tileCo = null)
    {
        if (tileCo == null) tileCo = NextTileCoordinate();

        var mesh = BMeshGenerators.SubdividedHex(tileCo.Center(size), divisions, size);
        mesh.AddVertexAttribute("glued", BMesh.AttributeBaseType.Float, 1);
        mesh.AddVertexAttribute("occupancy", BMesh.AttributeBaseType.Float, maxHeight); // core voxel data

        // Try to glue edge points to tiles that are already present
        foreach (var v in mesh.vertices)
        {
            // retrieve axial coords from UV
            var qr = v.attributes["uv"].asFloat().data;
            var co = new AxialCoordinate((int)qr[0], (int)qr[1]);

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

        currentTile.mesh = mesh;
        currentTileCo = tileCo;
        ShowMesh();
    }

    public void GenerateTile(TileAxialCoordinate tileCo = null)
    {
        //Random.InitState(3615);
        if (tileCo == null) tileCo = NextTileCoordinate();
        GenerateSubdividedHex(tileCo);
        BMeshJoinRandomTriangles.Call(currentTile.mesh);
        BMeshOperators.Subdivide(currentTile.mesh);
        for (int i = 0; i < 3; ++i) SquarifyQuads();
    }

    public void GenerateTileAtCursor()
    {
        if (generatorCursor == null) return;
        Vector2 p = new Vector2(generatorCursor.position.x, generatorCursor.position.z);
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
        if (currentTileCo == null || currentTile.mesh == null) return;
        tileSet[currentTileCo] = currentTile;
        currentTile = new Tile();
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
        if (currentTile != null)
        {
            if (currentTile.mesh != null) BMeshOperators.Merge(acc, currentTile.mesh);
            if (currentTile.skin != null) BMeshOperators.Merge(acc, currentTile.skin);
        }
        BMeshUnity.SetInMeshFilter(acc, GetComponent<MeshFilter>());
    }

    public void Clear()
    {
        currentTile = new Tile();
        tileSet = new Dictionary<AxialCoordinate, Tile>();
        currentTileCo = null;
        ShowMesh();
    }

    public void SquarifyQuads()
    {
        if (currentTile.mesh == null) return;

        // pre process weight attribute
        foreach (var v in currentTile.mesh.vertices)
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
            BMeshOperators.SquarifyQuads(currentTile.mesh, squarifyQuadsRate, squarifyQuadsUniform);
        }
        ShowMesh();
    }

    void ComputeSkin(Tile tile)
    {
        if (tile == null) return;
        tile.skin = new BMesh();
        ModuleBasedMarchingCubes.Run(tile.skin, tile.mesh, "occupancy", moduleManager);
        ShowMesh();
    }
    #endregion

    #region [Raycast Mesh]
    public void ComputeRaycastMesh()
    {
        if (currentTile.mesh == null) return;
        var raycastMesh = new BMesh();
        var uvAttr = raycastMesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2); // for vertex index
        raycastMesh.AddVertexAttribute("uv2", BMesh.AttributeBaseType.Float, 2) // for tile index
        .defaultValue = new BMesh.FloatAttributeValue(currentTileCo.q, currentTileCo.r);

        { int i = 0; foreach (BMesh.Vertex v in currentTile.mesh.vertices) { v.id = i++; } } // reset vertex ids

        foreach (BMesh.Vertex v in currentTile.mesh.vertices)
        {
            BMeshDual.AddDualNgonColumn(raycastMesh, v, uvAttr, "occupancy", maxHeight);
        }
        if (raycastMeshFilter != null) BMeshUnity.SetInMeshFilter(raycastMesh, raycastMeshFilter);
    }
    #endregion

    #region [UI Actions]
    public void RemoveRandomEdge()
    {
        if (currentTile.mesh == null) return;
        BMeshJoinRandomTriangles.Call(currentTile.mesh, 1);
        ShowMesh();
    }

    public void RemoveEdges()
    {
        if (currentTile.mesh == null) return;
        BMeshJoinRandomTriangles.Call(currentTile.mesh);
        ShowMesh();
    }

    public void Subdivide()
    {
        if (currentTile.mesh == null) return;
        BMeshOperators.Subdivide(currentTile.mesh);
        ShowMesh();
    }

    public void ComputeSkin()
    {
        if (currentTile.mesh == null) return;
        ComputeSkin(currentTile);
    }
    #endregion

    #region [World Controller]
    Tile GetTile(TileAxialCoordinate tileCo)
    {
        if (tileCo == null) return null;
        if (tileCo.Equals(currentTileCo))
        {
            return currentTile;
        }
        else if (tileSet != null && tileSet.ContainsKey(tileCo))
        {
            return tileSet[tileCo];
        }
        else
        {
            return null;
        }
    }

    void BuildCursorMesh()
    {
        Tile tile = GetTile(cursor.tileCo);
        Debug.Assert(tile != null);
        BMesh.Vertex v = tile.mesh.vertices[cursor.vertexId];
        var cursormesh = new BMesh();
        if (cursor.edgeIndex == -2)
        {
            BMeshDual.AddDualNgon(cursormesh, v, cursor.floor, true /* flipped */);
        }
        else if (cursor.edgeIndex == -1)
        {
            BMeshDual.AddDualNgon(cursormesh, v, cursor.floor, false /* flipped */);
        }
        else
        {
            BMesh.Edge it = v.edge;
            for (int i = 0; i < cursor.edgeIndex; ++i)
            {
                it = it.Next(v);
            }
            BMeshDual.AddDualNgonWall(cursormesh, it, cursor.floor);
        }
        BMeshUnity.SetInMeshFilter(cursormesh, cursorMeshFilter);
    }

    public void SetCursorAtVertex(int vertexId, int dualNgonId, TileAxialCoordinate tileCo)
    {
        if (cursor.vertexId == vertexId && cursor.tileCo == tileCo) return;
        cursor.vertexId = vertexId;
        cursor.tileCo = tileCo;
        cursor.floor = dualNgonId % maxHeight;
        cursor.edgeIndex = dualNgonId / maxHeight - 3;
        BuildCursorMesh();
    }

    public void HideCursor()
    {
        if (cursor.vertexId == -1) return;
        cursor.vertexId = -1;
        BMeshUnity.SetInMeshFilter(new BMesh(), cursorMeshFilter);
    }

    public void AddVoxelAtCursor()
    {
        Tile tile = GetTile(cursor.tileCo);
        if (cursor.vertexId == -1 || tile == null) return;

        int floor = cursor.floor;
        BMesh.Vertex v = tile.mesh.vertices[cursor.vertexId];
        BMesh.Vertex nv = v;
        if (cursor.edgeIndex == -2)
        {
            --floor;
        }
        else if (cursor.edgeIndex == -1)
        {
        }
        else
        {
            BMesh.Edge it = v.edge;
            for (int i = 0; i < cursor.edgeIndex; ++i)
            {
                it = it.Next(v);
            }
            nv = it.OtherVertex(v);
        }

        var occupancy = nv.attributes["occupancy"].asFloat().data;

        if (floor < 0 || floor >= occupancy.Length) return;
        if (occupancy[floor] > 0)
        {
            // The cursor is supposed to always be an edge from occupied to unoccupied
            Debug.LogWarning("Invalid cursor: floor #" + floor + " is already occupied");
            return;
        }
        occupancy[floor] = 1;
        ComputeSkin(tile);
    }

    public void RemoveVoxelAtCursor()
    {
        Tile tile = GetTile(cursor.tileCo);
        if (cursor.vertexId == -1 || tile == null) return;
        int floor = cursor.floor;
        BMesh.Vertex v = tile.mesh.vertices[cursor.vertexId];
        var occupancy = v.attributes["occupancy"].asFloat().data;

        if (cursor.edgeIndex == -1)
        {
            --floor;
        }

        if (floor < 0 || floor >= occupancy.Length) return;
        if (occupancy[floor] == 0)
        {
            // The cursor is supposed to always be an edge from occupied to unoccupied
            Debug.LogWarning("Invalid cursor: floor #" + cursor.floor + " is not occupied");
            return;
        }

        occupancy[floor] = 0;
        ComputeSkin(tile);
    }
    #endregion

    #region [MonoBehavior]
    private void OnEnable()
    {
        currentTile = new Tile();
        tileSet = new Dictionary<AxialCoordinate, Tile>();
        cursor = new Cursor();
    }
    void Update()
    {
        if (run)
        {
            GenerateTileAtCursor();
            ComputeRaycastMesh();
        }
    }

    void OnDrawGizmos()
    {
        if (currentTile == null || currentTile.mesh == null) return;
        var bmesh = currentTile.mesh;
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
    #endregion
}
