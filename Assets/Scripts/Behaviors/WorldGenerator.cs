using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
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

    #region [Wave Function Collapse Grid]
    BMesh wfcGrid;

    /**
     * A dual voxel of the virtual grid has a base face and a floor
     */
    class VFace
    {
        public BMesh.Face face;
        public int floor;

        public List<VFace> NeighborVFaces()
        {
            List<VFace> vfaces = new List<VFace>();
            foreach (var ne in face.NeighborEdges())
            {
                foreach (var nf in ne.NeighborFaces())
                {
                    if (nf != face) vfaces.Add(new VFace { face = nf, floor = floor });
                }
            }
            vfaces.Add(new VFace { face = face, floor = floor + 1 });
            if (floor > 0) vfaces.Add(new VFace { face = face, floor = floor - 1 });
            return vfaces;
        }

        /**
         * A VFace is empty iff its 8 corners are empty
         */
        public bool IsEmpty()
        {
            //if (floor == 0) return false; // for debug
            foreach (var corner in face.NeighborVertices())
            {
                var occ = corner.attributes["occupancy"].asFloat().data;
                if (floor < occ.Length && occ[floor] > 0) return false;
                if (floor + 1 < occ.Length && occ[floor + 1] > 0) return false;
            }
            return true;
        }
    }

    // Low level operation ensuring that e2 is right after e1 in the chained
    // list of edges that are around vertex v.
    // TODO: move the SwapEdge part to BMesh and test it
    void EnsureAfter(BMesh.Edge e1, BMesh.Edge e2, BMesh.Vertex v)
    {
        int c0 = v.NeighborEdges().Count; // for testing

        Debug.Assert(e1 != e2);

        BMesh.Edge pivot = e1.Next(v);
        if (pivot != e2)
        {
            // SwapEdge(pivot, e2, v)

            var before_e2 = e2.Prev(v);
            var after_e2 = e2.Next(v);
            var before_pivot = pivot.Prev(v); // == e1
            var after_pivot = pivot.Next(v);

            if (after_e2 == pivot && after_pivot == e2)
            {
                // nothing to do
            }
            else
            {
                after_e2.SetPrev(v, pivot);
                pivot.SetNext(v, after_e2);

                before_pivot.SetNext(v, e2);
                e2.SetPrev(v, before_pivot);
                
                if (after_pivot == e2) // and so after_e2 != pivot
                {
                    e2.SetNext(v, pivot);
                    pivot.SetPrev(v, e2);
                }
                else if (after_e2 == pivot) // and so after_pivot != e2
                {
                    pivot.SetNext(v, e2);
                    e2.SetPrev(v, pivot);
                }
                else
                {
                    before_e2.SetNext(v, pivot);
                    pivot.SetPrev(v, before_e2);

                    after_pivot.SetPrev(v, e2);
                    e2.SetNext(v, after_pivot);
                }
            }

            Debug.Assert(after_e2.Next(v) != after_e2);
            Debug.Assert(after_e2.Prev(v) != after_e2);
            Debug.Assert(before_e2.Next(v) != before_e2);
            Debug.Assert(before_e2.Prev(v) != before_e2);
            Debug.Assert(after_pivot.Next(v) != after_pivot);
            Debug.Assert(after_pivot.Prev(v) != after_pivot);
            Debug.Assert(before_pivot.Next(v) != before_pivot);
            Debug.Assert(before_pivot.Prev(v) != before_pivot);
        }

        Debug.Assert(e1.Next(v) == e2);
        Debug.Assert(e2.Prev(v) == e1);
        Debug.Assert(v.NeighborEdges().Count == c0);
    }

    BMesh.Vertex ComputeWfcGrid_Aux(BMesh baseGrid, VFace vface)
    {
        int[] visited = vface.face.attributes["visited"].asInt().data;
        int[] vertex = vface.face.attributes["vertex"].asInt().data;
        Debug.Assert(visited.Length == vertex.Length);

        if (visited.Length > vface.floor && visited[vface.floor] > 0)
        {
            return wfcGrid.vertices[vertex[vface.floor]];
        }

        // If needed resize attribute vectors
        if (visited.Length <= vface.floor)
        {
            var newVisited = new int[vface.floor + 1];
            var newVertex = new int[vface.floor + 1];
            for (int i = 0; i < visited.Length; ++i)
            {
                newVisited[i] = visited[i];
                newVertex[i] = vertex[i];
            }
            vface.face.attributes["visited"].asInt().data = newVisited;
            vface.face.attributes["vertex"].asInt().data = newVertex;
            visited = newVisited;
            vertex = newVertex;
        }
        visited[vface.floor] = 1;
        vertex[vface.floor] = wfcGrid.vertices.Count; // index of the next vertex

        var v = wfcGrid.AddVertex(vface.face.Center() + Vector3.up * (vface.floor + 0.5f));

        Vector3 d0 = Vector3.zero;
        foreach (VFace nf in vface.NeighborVFaces())
        {
            if (nf.floor == vface.floor)
            {
                Vector3 nvp = nf.face.Center() + Vector3.up * (nf.floor + 0.5f);
                d0 = (nvp - v.point).normalized;
                break;
            }
        }
        float prevAngle = 361;

        Debug.Log("---");
        int c = 0;
        foreach (VFace nf in vface.NeighborVFaces())
        {
            if (nf.floor == vface.floor)
            {
                Vector3 nvp = nf.face.Center() + Vector3.up * (nf.floor + 0.5f);
                Vector3 di = (nvp - v.point).normalized;
                float angle = Vector3.SignedAngle(d0, di, Vector3.up);
                if (angle <= 0) angle += 360;
                Debug.Log("#" + c + ": angle = " + angle + ", di = " + di + " and d0 = " + d0);
                Debug.Assert(angle < prevAngle, "error at vface #" + vface.face.id + ":" + vface.floor);
                prevAngle = angle;
                ++c;
            }
        }

        prevAngle = 361;

        BMesh.Edge prevHorizontalEdge = null;
        foreach (VFace nf in vface.NeighborVFaces())
        {
            if (nf.IsEmpty()) continue;
            var nv = ComputeWfcGrid_Aux(baseGrid, nf);
            if (nv != null)
            {
                var e = wfcGrid.AddEdge(v, nv);
                int type = nf.floor == vface.floor ? 0 : 1;
                e.attributes["type"].asInt().data[0] = type;

                Debug.Assert(Vector3.Distance(nf.face.Center() + Vector3.up * (nf.floor + 0.5f), nv.point) < 1e-5);

                if (type == 0)
                {
                    Vector3 di = (nv.point - v.point).normalized;
                    float angle = Vector3.SignedAngle(d0, di, Vector3.up);
                    if (angle <= 0) angle += 360;
                    Debug.Log("angle = " + angle);
                    Debug.Assert(angle < prevAngle, "error at vface #" + vface.face.id + ":" + vface.floor);
                    prevAngle = angle;

                    if (prevHorizontalEdge != null)
                    {
                        EnsureAfter(prevHorizontalEdge, e, v);
                    }
                    prevHorizontalEdge = e;
                }
            }
        }

        return v;
    }

    /**
     * The WFC grid has one vertex per module slot and one edge per module
     * connection. It propagates from a given face of the baseGrid.
     * WFC grid is the volumetric dual of the original virtual grid of base
     * baseGrid, meaning that a vertex in WFC grid corresponds to a (cubic)
     * element of volume in the virtual grid.
     */
    void ComputeWfcGrid(BMesh baseGrid, BMesh.Face face = null)
    {
        wfcGrid = new BMesh();
        wfcGrid.AddEdgeAttribute("type", BMesh.AttributeBaseType.Int, 1); // 0: horizontal, 1: vertical
        if (!baseGrid.HasFaceAttribute("visited"))
        {
            // These attributes are vectors because they are per vface
            baseGrid.AddFaceAttribute("visited", BMesh.AttributeBaseType.Int, 1);
            // store the index of the vertex corresponding to this vface
            baseGrid.AddFaceAttribute("vertex", BMesh.AttributeBaseType.Int, 1);
        }
        else
        {
            // Reset attributes
            foreach (var f in baseGrid.faces)
            {
                f.attributes["visited"] = new BMesh.IntAttributeValue(0);
                f.attributes["vertex"] = new BMesh.IntAttributeValue(0);
            }
        }
        int floor = 0;
        if (face == null)
        {
            // Find a non empty vface
            foreach (var v in baseGrid.vertices)
            {
                var occ = v.attributes["occupancy"].asFloat().data;
                for (int i = 0; i < occ.Length; ++i)
                {
                    if (occ[i] > 0)
                    {
                        floor = i;
                        face = v.NeighborFaces()[0];
                        break;
                    }
                }
                if (face != null) break;
            }
        }
        ComputeWfcGrid_Aux(baseGrid, new VFace { face = face, floor = floor });
    }
    #endregion

    #region [Wave Function Collapse System]
    void ComputeExclusionClasses(BMesh wfcTopology)
    {
        // exclusion class for XWFC
        wfcTopology.AddVertexAttribute("class", BMesh.AttributeBaseType.Int, 1);

        { int i = 0; foreach (var v in wfcTopology.vertices) v.id = i++; }

        // Check that edge ordering is consistent
        // TODO: move to a proper test file
        foreach (var v in wfcTopology.vertices)
        {
            var edges = v.NeighborEdges();
            if (edges.Count == 0) continue;
            Vector3 d0 = (edges[0].OtherVertex(v).point - v.point).normalized;
            float prevAngle = 361;
            int i = 0;
            foreach (var e in edges)
            {
                int type = e.attributes["type"].asInt().data[0];
                if (type != 0) continue; // only look at horizontal edges

                Vector3 di = (e.OtherVertex(v).point - v.point).normalized;
                float angle = Vector3.SignedAngle(d0, di, Vector3.up);
                if (angle <= 0) angle += 360;
                if (edges.Count == 4)
                {
                    Debug.Log("Angle edge #" + i + " out of vertex #" + v.id + ": " + angle);
                    Debug.Log("   (di = " + di + ")");
                }
                Debug.Assert(edges.Count != 4 || angle < prevAngle);
                prevAngle = angle;
                ++i;
            }
        }
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

    public void ComputeWfcGrid()
    {
        if (currentTile.mesh == null) return;
        ComputeWfcGrid(currentTile.mesh);
    }

    public void ComputeExclusionClasses()
    {
        if (wfcGrid == null) return;
        ComputeExclusionClasses(wfcGrid);
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
        Gizmos.matrix = transform.localToWorldMatrix;

        if (wfcGrid != null)
        {
            BMeshUnity.DrawGizmos(wfcGrid);
#if UNITY_EDITOR
            foreach (var e in wfcGrid.edges)
            {
                var type = e.attributes["type"].asInt().data[0];
                Handles.Label(e.Center(), "" + type);
            }
#endif // UNITY_EDITOR
        }

        return;

        if (currentTile == null || currentTile.mesh == null) return;
        var bmesh = currentTile.mesh;
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
