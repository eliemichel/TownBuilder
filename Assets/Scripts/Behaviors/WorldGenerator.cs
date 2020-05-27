using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
        public BMesh.Vertex[] vertices; // keep a reference to the vertices of the tile for glueing
    }

    class Cursor
    {
        public int vertexId;
        public int edgeIndex; // -2: bellow, -1: above, 0+: side
        public int floor;
    }

    Dictionary<AxialCoordinate, Tile> tileSet;
    Cursor cursor;
    BMesh wfcGridForGizmos; // for debug only
    BMesh wfcOutputMesh;
    BMesh fullBaseGrid;

    int maxEdgeCount = 4; // there are only quads

    #endregion

    #region [World Generator]
    public void GenerateQuad()
    {
        var bmesh = new BMesh();
        bmesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2);
        bmesh.AddVertexAttribute("restpos", BMesh.AttributeBaseType.Float, 3);
        bmesh.AddVertexAttribute("weight", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("glued", BMesh.AttributeBaseType.Float, 1);
        bmesh.AddVertexAttribute("gluedId", BMesh.AttributeBaseType.Int, 1);
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
        ShowMesh();
    }

    public BMesh GenerateSubdividedHex(TileAxialCoordinate tileCo = null)
    {
        if (tileCo == null) tileCo = NextTileCoordinate();

        var mesh = BMeshGenerators.SubdividedHex(tileCo.Center(size), divisions, size);
        mesh.AddVertexAttribute("glued", BMesh.AttributeBaseType.Float, 1);
        mesh.AddVertexAttribute("gluedId", BMesh.AttributeBaseType.Int, 1);
        mesh.AddVertexAttribute("occupancy", BMesh.AttributeBaseType.Float, maxHeight); // core voxel data

        // Try to glue edge points to tiles that are already present
        foreach (var v in mesh.vertices)
        {
            // retrieve axial coords from UV
            var qr = v.attributes["uv"].asFloat().data;
            var co = new AxialCoordinate((int)qr[0], (int)qr[1]);

            var glued = v.attributes["glued"].asFloat();
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
        return mesh;
    }

    public void GenerateTile(TileAxialCoordinate tileCo = null)
    {
        //Random.InitState(3615);
        if (tileCo == null) tileCo = NextTileCoordinate();
        BMesh mesh = GenerateSubdividedHex(tileCo);
        BMeshJoinRandomTriangles.Call(mesh);
        BMeshOperators.Subdivide(mesh);
        for (int i = 0; i < 3; ++i) SquarifyQuads(mesh, tileCo);
        tileSet[tileCo] = new Tile { vertices = mesh.vertices.ToArray() };

        AddToBaseGrid(mesh);
    }

    // Custom Merge to remove doubles based on a priori knownledge
    // base copied from BMeshOperators.Merge
    void CustomMerge(BMesh mesh, BMesh other)
    {
        // For modifications A and B
        var firstNewVert = mesh.vertices.Count;
        var newIndices = new int[other.vertices.Count];
        int c = 0;

        var newVerts = new BMesh.Vertex[other.vertices.Count];
        int i = 0;
        foreach (BMesh.Vertex v in other.vertices)
        {
            v.id = i;
            // MODIFICATION A: do not copy glued points
            bool glued = v.attributes["glued"].asFloat().data[0] >= 1;
            if (glued)
            {
                newVerts[i] = mesh.vertices[v.attributes["gluedId"].asInt().data[0]];
                Debug.Log("Gluing new #" + v.id + " to old #" + newVerts[i].id);
                newIndices[i] = newVerts[i].id;
            }
            else
            {
                newVerts[i] = mesh.AddVertex(v.point);
                newIndices[i] = firstNewVert + c++;
            }
            BMeshOperators.AttributeLerp(mesh, newVerts[i], v, v, 1); // copy all attributes
            ++i;
        }
        foreach (BMesh.Edge e in other.edges)
        {
            BMesh.Vertex v1 = newVerts[e.vert1.id];
            BMesh.Vertex v2 = newVerts[e.vert2.id];
            mesh.AddEdge(v1, v2);
        }
        foreach (BMesh.Face f in other.faces)
        {
            var neighbors = f.NeighborVertices();
            var newNeighbors = new BMesh.Vertex[neighbors.Count];
            int j = 0;
            foreach (var v in neighbors)
            {
                newNeighbors[j] = newVerts[v.id];
                ++j;
            }
            mesh.AddFace(newNeighbors);
        }
        // MODIFICATION B: update index in other.vertices
        foreach (var v in other.vertices)
        {
            v.id = newIndices[v.id];
        }
    }

    void AddToBaseGrid(BMesh mesh)
    {
        // TODO: manage instanced modules
        if (fullBaseGrid == null)
        {
            fullBaseGrid = new BMesh();
            fullBaseGrid.AddVertexAttribute("occupancy", BMesh.AttributeBaseType.Float, maxHeight); // core voxel data
        }
        CustomMerge(fullBaseGrid, mesh);
        ShowMesh();
    }

    public void GenerateTileAtCursor()
    {
        if (generatorCursor == null) return;
        Vector2 p = new Vector2(generatorCursor.position.x, generatorCursor.position.z);
        var tileCo = TileAxialCoordinate.AtPosition(p, size, divisions);
        if (tileSet != null && tileSet.ContainsKey(tileCo)) return;
        Debug.Log("Generating tile at " + tileCo + "...");
        GenerateTile(tileCo);
    }

    // Coord of the large tile
    TileAxialCoordinate NextTileCoordinate()
    {
        return new TileAxialCoordinate(nextTileQ, nextTileR, divisions);
    }

    public void ShowMesh()
    {
        var acc = new BMesh();
        acc.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2);
        if (wfcOutputMesh != null) BMeshOperators.Merge(acc, wfcOutputMesh);
        if (fullBaseGrid != null) BMeshOperators.Merge(acc, fullBaseGrid);
        BMeshUnity.SetInMeshFilter(acc, GetComponent<MeshFilter>());
    }

    public void Clear()
    {
        tileSet = new Dictionary<AxialCoordinate, Tile>();
        ShowMesh();
    }

    public void SquarifyQuads(BMesh mesh, TileAxialCoordinate tileCo)
    {
        // pre process weight attribute
        foreach (var v in mesh.vertices)
        {
            var weight = v.attributes["weight"] as BMesh.FloatAttributeValue;
            var restpos = v.attributes["restpos"] as BMesh.FloatAttributeValue;
            var uv = v.attributes["uv"] as BMesh.FloatAttributeValue;
            var glued = v.attributes["glued"] as BMesh.FloatAttributeValue;

            weight.data[0] = weight.data[0] < 1.0f ? 0.0f : squarifyQuadsBorderWeight;

            if (glued.data[0] >= 1 && tileSet != null)
            {
                var fco = new FloatAxialCoordinate(uv.data[0], uv.data[1]);
                foreach (var neighborTileCo in tileCo.NeighboringTiles(fco))
                {
                    if (tileSet.ContainsKey(neighborTileCo))
                    {
                        var gluedCo = tileCo.ConvertLocalCoordTo(fco, neighborTileCo);
                        var gluedUv = new BMesh.FloatAttributeValue(gluedCo.q, gluedCo.r);

                        // This is a pretty dirty way of building the mesh but for efficiency we only keep vertices in Tile.
                        BMesh points = new BMesh {
                            vertices = tileSet[neighborTileCo].vertices.ToList(),
                            vertexAttributes = new List<BMesh.AttributeDefinition> { new BMesh.AttributeDefinition("uv", BMesh.AttributeBaseType.Float, 2) }
                        };

                        BMesh.Vertex target = BMeshOperators.Nearpoint(points, gluedUv, "uv");
                        Debug.Assert(target != null);
                        float dist = BMesh.AttributeValue.Distance(target.attributes["uv"], gluedUv);
                        Debug.Assert(dist < 1e-5, "Distance in UVs is too large: " + dist);
                        v.attributes["restpos"] = new BMesh.FloatAttributeValue(target.point);
                        v.attributes["gluedId"].asInt().data[0] = target.id;
                        weight.data[0] = 9999;
                    }
                }
            }
        }

        for (int i = 0; i < squarifyQuadsIterations; ++i)
        {
            BMeshOperators.SquarifyQuads(mesh, squarifyQuadsRate, squarifyQuadsUniform);
        }
        ShowMesh();
    }

    /**
     * @param voxel voxel of the change since last update
     */
    void UpdateSkin(Voxel voxel)
    {
        if (fullBaseGrid == null) return;

        UnityEngine.Profiling.Profiler.BeginSample("ComputeWfcGrid");
        BMesh wfcTopology = ComputeWfcTopology(fullBaseGrid, voxel);
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("ComputeExclusionClasses");
        ComputeExclusionClasses(wfcTopology, fullBaseGrid);
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("RunXwfc");
        RunXwfc(wfcTopology);
        UnityEngine.Profiling.Profiler.EndSample();

        wfcOutputMesh = UpdateWfcOutputMesh(wfcOutputMesh, fullBaseGrid, wfcTopology);
        ShowMesh();

        wfcGridForGizmos = wfcTopology; // for debug
    }
    #endregion

    #region [Raycast Mesh]
    /**
     * Build a mesh with voxel faces (abusivly called "dual ngons") that is
     * rendered to be used to know over which voxel the mouse is.
     * Vertical faces correspond to edges of the base grid.
     */
    public void ComputeRaycastMesh()
    {
        if (fullBaseGrid == null) return;
        var raycastMesh = new BMesh();
        var uvAttr = raycastMesh.AddVertexAttribute("uv", BMesh.AttributeBaseType.Float, 2); // for vertex index
        
        { int i = 0; foreach (BMesh.Vertex v in fullBaseGrid.vertices) { v.id = i++; } } // reset vertex ids

        foreach (BMesh.Vertex v in fullBaseGrid.vertices)
        {
            BMeshDual.AddDualNgonColumn(raycastMesh, v, uvAttr, "occupancy", maxEdgeCount);
        }
        if (raycastMeshFilter != null) BMeshUnity.SetInMeshFilter(raycastMesh, raycastMeshFilter);
    }
    #endregion

    #region [Wave Function Collapse Grid]

    /**
     * A voxel is a location that can be occupied or not.
     * Voxels are placed as regular columns on top of the vertices of an
     * arbitrary grid so they are identified by the vertex plus a floor.
     * Occupation flag is contained in vertex attribute "occupancy".
     */
    class Voxel
    {
        public BMesh.Vertex vert;
        public int floor;

        public bool IsOccupied()
        {
            var occ = vert.attributes["occupancy"].asFloat().data;
            return floor >= 0 && occ.Length > floor && occ[floor] > 0;
        }
    }

    /**
     * A dual voxel is somehow a corner of voxel. The same way voxels are
     * columns on top of vertices of the base grid, dual voxels are column on
     * top of faces (vertex' dual).
     * Dual voxels are slots where to put tiles (using wave function collapse)
     */
    class DualVoxel
    {
        public BMesh.Face face;
        public int floor;

        /**
         * Neighbor dual voxels are returned in the right order for adjacency,
         * i.e. the i-th neighbors is in adjacency relation #i with 'this'.
         * Does not return the neighbors bellow floor=0
         */
        public List<DualVoxel> NeighborDualVoxels()
        {
            List<DualVoxel> neighbors = new List<DualVoxel>();
            foreach (var ne in face.NeighborEdges())
            {
                foreach (var nf in ne.NeighborFaces())
                {
                    if (nf != face) neighbors.Add(new DualVoxel { face = nf, floor = floor });
                }
            }
            Debug.Assert(neighbors.Count == 4, "neighbors.Count = " + neighbors.Count);
            neighbors.Add(new DualVoxel { face = face, floor = floor + 1 });
            if (floor > 0) neighbors.Add(new DualVoxel { face = face, floor = floor - 1 });
            return neighbors;
        }

        /**
         * Corners of a dual voxels are voxels, and vice versa.
         * They are returned in the right order to measure occupation hash
         */
        public List<Voxel> Corners()
        {
            var corners = new List<Voxel>();
            var cornersTop = new List<Voxel>();
            foreach (var vert in face.NeighborVertices())
            {
                corners.Add(new Voxel { vert = vert, floor = floor });
                cornersTop.Add(new Voxel { vert = vert, floor = floor + 1 });
            }
            corners.AddRange(cornersTop);
            return corners;
        }

        /**
         * A dual voxel is empty iff its 8 corners are empty
         */
        public bool IsEmpty()
        {
            //if (floor == 0) return false; // for debug
            foreach (var corner in Corners())
            {
                if (corner.IsOccupied()) return false;
            }
            return true;
        }

        /**
         * The occupation hash summarizes in one single int the occupation of
         * the 8 corners. This value ranges from 0 (empty) to 255 (full) and
         * is used to query module registry.
         */
        public int OccupationHash()
        {
            int hash = 0;
            int k = 0;
            foreach (var corner in Corners())
            {
                if (corner.IsOccupied()) hash += (1 << k);
                ++k;
            }
            return hash;
        }

        /**
         * Save the dual voxel to a 2-dimensional int attribute
         */
        public void SaveToAttribute(BMesh.AttributeValue attr)
        {
            var data = attr.asInt().data;
            data[0] = face.id;
            data[1] = floor;
        }

        public static DualVoxel FromAttribute(BMesh.AttributeValue attr, BMesh mesh)
        {
            int[] data = attr.asInt().data;
            return new DualVoxel { face = mesh.faces[data[0]], floor = data[1] };
        }

        public Vector3 Center()
        {
            return face.Center() + Vector3.up * (floor + 0.5f);
        }
    }

    /**
     * Auxiliary function walking recursively in the connected component of a
     * given dualvoxel, creating the WFC topology and returning the vertex of
     * this topology that corresponds to teh dual voxel.
     */
    BMesh.Vertex ComputeWfcTopology_Walk(BMesh baseGrid, BMesh wfcTopology, DualVoxel dualvoxel)
    {
        int[] visited = dualvoxel.face.attributes["visited"].asInt().data;
        int[] vertex = dualvoxel.face.attributes["vertex"].asInt().data;
        Debug.Assert(visited.Length == vertex.Length);

        // If dual voxel has already been visited, return.
        if (visited.Length > dualvoxel.floor && visited[dualvoxel.floor] > 0)
        {
            return wfcTopology.vertices[vertex[dualvoxel.floor]];
        }

        // If needed resize attribute vectors
        if (visited.Length <= dualvoxel.floor)
        {
            int height = dualvoxel.floor + 1;
            foreach (BMesh.Vertex baseCorner in dualvoxel.face.NeighborVertices())
            {
                height = Mathf.Max(height, baseCorner.attributes["occupancy"].asFloat().data.Length);
            }
            var newVisited = new int[height];
            var newVertex = new int[height];
            for (int i = 0; i < visited.Length; ++i)
            {
                newVisited[i] = visited[i];
                newVertex[i] = vertex[i];
            }
            dualvoxel.face.attributes["visited"].asInt().data = newVisited;
            dualvoxel.face.attributes["vertex"].asInt().data = newVertex;
            visited = newVisited;
            vertex = newVertex;
        }
        
        visited[dualvoxel.floor] = 1;
        vertex[dualvoxel.floor] = wfcTopology.vertices.Count; // index of the next vertex

        var v = wfcTopology.AddVertex(dualvoxel.Center());
        dualvoxel.SaveToAttribute(v.attributes["dualvoxel"]);

        int adj = -1;
        foreach (DualVoxel nf in dualvoxel.NeighborDualVoxels())
        {
            ++adj;
            
            // If the whole column on top of it is empty, ignore
            {
                // a bit dirty
                bool empty = true;
                foreach (var corner in nf.face.NeighborVertices())
                {
                    var occ = corner.attributes["occupancy"].asFloat().data;
                    for (int i = nf.floor; i < occ.Length && empty; ++i)
                    {
                        if (occ[i] > 0) empty = false;
                    }
                    if (!empty) break;
                }
                if (empty) continue;
            }
            var nv = ComputeWfcTopology_Walk(baseGrid, wfcTopology, nf);
            if (nv != null)
            {
                // old neighboring mechanism
                if (wfcTopology.FindEdge(v, nv) == null)
                {
                    var edge = wfcTopology.AddEdge(v, nv);
                    int type = nf.floor == dualvoxel.floor ? 0 : (nf.floor == dualvoxel.floor + 1 ? 2/*bellow*/ : 1/*above*/); // see ModuleEntanglementRules.ConnectionType
                    //edge.attributes["type"].asInt().data[0] = type;

                    if (type == 2) Debug.Assert(adj == 4, "adj = " + adj);
                    if (type == 1) Debug.Assert(adj == 5, "adj = " + adj);
                }

                // New neighboring mechanism for WFC: use 2-vertex face,
                // i.e. half-edges with a different type on each loop.
                BMesh.Loop l;
                BMesh.Edge e = wfcTopology.FindEdge(v, nv);
                bool test = false;
                if (e != null && e.loop != null)
                {
                    l = e.loop;
                    test = true;
                }
                else
                {
                    BMesh.Face f = wfcTopology.AddFace(new BMesh.Vertex[] { v, nv });
                    l = f.loop;
                }
                if (l.vert != v) l = l.next;
                Debug.Assert(l.vert == v);

                if (test)
                {
                    int nadj = l.next.attributes["adjacency"].asInt().data[0];
                    if (adj < 4) Debug.Assert(nadj < 4, "adj = " + adj + ", and nadj = " + nadj);
                }

                l.attributes["adjacency"].asInt().data[0] = adj;

                Debug.Assert(Vector3.Distance(nf.Center(), nv.point) < 1e-5);
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
     * Reset face's id
     */
    BMesh ComputeWfcTopology(BMesh baseGrid, Voxel voxel)
    {
        BMesh wfcTopology = new BMesh();

        // 0: horizontal, 1: vertical
        wfcTopology.AddEdgeAttribute("type", BMesh.AttributeBaseType.Int, 1);

        // index of the corresponding face in gridMesh, and floor
        wfcTopology.AddVertexAttribute("dualvoxel", BMesh.AttributeBaseType.Int, 2);

        // new adjacency mechanism
        wfcTopology.AddLoopAttribute("adjacency", BMesh.AttributeBaseType.Int, 1)
            .defaultValue = new BMesh.IntAttributeValue(-1);

        // Initialize or reset attributes "visited" and "vertex" that label voxels (so columns on vertices)
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

        // Ensure face ids
        { int i = 0; foreach (var f in baseGrid.faces) f.id = i++; }

        foreach (var f in voxel.vert.NeighborFaces())
        {
            ComputeWfcTopology_Walk(baseGrid, wfcTopology, new DualVoxel { face = f, floor = voxel.floor });
        }

        { int i = 0; foreach (var v in wfcTopology.vertices) v.id = i++; }
        foreach (var l in wfcTopology.loops)
        {
            //Debug.Assert(l.attributes["adjacency"].asInt().data[0] != -1, "Loop " + l.vert.id + "->" + l.next.vert.id + " has not been initialized");
            if (l.attributes["adjacency"].asInt().data[0] == -1)
                Debug.LogWarning("Loop " + l.vert.id + "->" + l.next.vert.id + " has not been initialized");
        }

        return wfcTopology;
    }
    #endregion

    #region [Wave Function Collapse System]
    void ComputeExclusionClasses(BMesh wfcTopology, BMesh baseGrid)
    {
        // exclusion class for XWFC
        wfcTopology.AddVertexAttribute("class", BMesh.AttributeBaseType.Int, 1);

        { int i = 0; foreach (var v in wfcTopology.vertices) v.id = i++; }

        foreach (var v in wfcTopology.vertices)
        {
            int[] dualVFace = v.attributes["dualvoxel"].asInt().data;
            int dualFaceId = dualVFace[0];
            int floor = dualVFace[1];
            var dualFace = baseGrid.faces[dualFaceId];

            var corners = dualFace.NeighborVertices().ToArray();
            Debug.Assert(corners.Length == 4);
            int hash = 0;
            for (int k = 0; k < 2; ++k)
            {
                for (int i = 0; i < 4; ++i)
                {
                    var occ = corners[i].attributes["occupancy"].asFloat().data;
                    if (occ.Length > floor + k && occ[floor + k] > 0)
                        hash += 1 << (i + k * 4);
                }
            }

            v.attributes["class"] = new BMesh.IntAttributeValue(hash);
        }
    }

    ModuleEntanglementRules rules;
    LilyXwfc.WaveFunctionSystem system;

    public void RunXwfc(BMesh topology)
    {
        if (rules == null) rules = new ModuleEntanglementRules(moduleManager);
        system = new LilyXwfc.WaveFunctionSystem(topology, rules, moduleManager.MaxModuleCount, "class");
        var wfc = new LilyXwfc.WaveFunctionCollapse(system, true);
        //for (var it = wfc.CollapseCoroutine(200); it.MoveNext();) { }
        if (wfc.Collapse(200))
        {
            Debug.Log("wfc finished");
        }
        else
        {
            Debug.Log("wfc did NOT finish in 200 steps");
        }
    }

    void ClearUpdatedParts(BMesh skinMesh, BMesh baseGrid)
    {
        // Clear all modules that are associated to faces that are part of the
        // current wfcGrid but keep the other modules unchanged
        var oldVertices = skinMesh.vertices.ToArray();
        foreach (var v in oldVertices)
        {
            int[] dualVFace = v.attributes["dualvoxel"].asInt().data;
            int dualFaceId = dualVFace[0];
            int floor = dualVFace[1];
            var dualFace = baseGrid.faces[dualFaceId];

            var visited = dualFace.attributes["visited"].asInt().data;
            if (visited.Length > floor && visited[floor] > 0)
            {
                skinMesh.RemoveVertex(v);
            }
        }
    }

    BMesh.AttributeDefinition EnsureVFaceAttribute(BMesh skinMesh)
    {
        if (!skinMesh.HasVertexAttribute("dualvoxel"))
        {
            return skinMesh.AddVertexAttribute("dualvoxel", BMesh.AttributeBaseType.Int, 2);
        }
        else
        {
            // vfaceAttr = debugMesh.GetVertexAttribute("dualvoxel");
            foreach (var attr in skinMesh.vertexAttributes)
            {
                if (attr.name == "dualvoxel")
                {
                    return attr;
                }
            }
        }
        return null;
    }

    MarchingModuleManager.TransformedModule GetTransformedModule(BMesh.Vertex v)
    {
        if (system != null)
        {
            var state = system.GetWave(LilyXwfc.WaveVariable.FromRaw(v.id));
            var comp = state.Components();
            if (comp.Count > 0)
            {
                LilyXwfc.PureState ps = comp[0];
                int hash = ps.index / system.dimension;
                int subindex = ps.index % system.dimension;
                return moduleManager.GetModule(hash, subindex);
            }
            else
            {
                return null;
            }
        }
        else
        {
            int hash = v.attributes["class"].asInt().data[0];
            return moduleManager.SampleModule(hash);
        }
    }

    public BMesh UpdateWfcOutputMesh(BMesh skinMesh, BMesh baseGrid, BMesh wfcGrid)
    {
        //if (skinMesh == null) skinMesh = new BMesh();
        skinMesh = new BMesh();

        BMesh.AttributeDefinition vfaceAttr = EnsureVFaceAttribute(skinMesh);

        // Don't do that, it's too slow:
        //UnityEngine.Profiling.Profiler.BeginSample("UpdateWfcOutputMesh - ClearUpdatedParts");
        //ClearUpdatedParts(skinMesh, baseGrid);
        //UnityEngine.Profiling.Profiler.EndSample();

        foreach (var v in wfcGrid.vertices)
        {
            var m = GetTransformedModule(v);
            if (m == null) continue;

            var dualvoxel = DualVoxel.FromAttribute(v.attributes["dualvoxel"], baseGrid);
            var verts = dualvoxel.face.NeighborVertices().ToArray();
            var edges = dualvoxel.face.NeighborEdges().ToArray();

            var mf = m.baseModule.meshFilter;
            Vector3 floorOffset = dualvoxel.floor * Vector3.up;
            var controlPoints = m.baseModule.deformer.controlPoints;
            // Match occupation points with control points
            controlPoints[0] = m.transform.EdgeCenter(1, 2, verts, edges) + floorOffset;
            controlPoints[1] = m.transform.EdgeCenter(2, 3, verts, edges) + floorOffset;
            controlPoints[2] = m.transform.EdgeCenter(3, 0, verts, edges) + floorOffset;
            controlPoints[3] = m.transform.EdgeCenter(0, 1, verts, edges) + floorOffset;
            controlPoints[4] = m.transform.EdgeCenter(1, 5, verts, edges) + floorOffset;
            controlPoints[5] = m.transform.EdgeCenter(2, 6, verts, edges) + floorOffset;
            controlPoints[6] = m.transform.EdgeCenter(3, 7, verts, edges) + floorOffset;
            controlPoints[7] = m.transform.EdgeCenter(0, 4, verts, edges) + floorOffset;
            controlPoints[8] = m.transform.EdgeCenter(5, 6, verts, edges) + floorOffset;
            controlPoints[9] = m.transform.EdgeCenter(6, 7, verts, edges) + floorOffset;
            controlPoints[10] = m.transform.EdgeCenter(7, 4, verts, edges) + floorOffset;
            controlPoints[11] = m.transform.EdgeCenter(4, 5, verts, edges) + floorOffset;
            vfaceAttr.defaultValue = v.attributes["dualvoxel"];
            BMeshUnityExtra.Merge(skinMesh, mf.sharedMesh, m.baseModule.deformer, !m.transform.flipped);
        }

        return skinMesh;
    }
    #endregion

    #region [UI Actions]
    public void RemoveRandomEdge()
    {
        if (fullBaseGrid == null) return;
        BMeshJoinRandomTriangles.Call(fullBaseGrid, 1);
        ShowMesh();
    }

    public void RemoveEdges()
    {
        if (fullBaseGrid == null) return;
        BMeshJoinRandomTriangles.Call(fullBaseGrid);
        ShowMesh();
    }

    public void Subdivide()
    {
        if (fullBaseGrid == null) return;
        BMeshOperators.Subdivide(fullBaseGrid);
        ShowMesh();
    }
    #endregion

    #region [World Controller]

    void BuildCursorMesh()
    {
        BMesh.Vertex v = fullBaseGrid.vertices[cursor.vertexId];
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
        int floor = dualNgonId / (maxEdgeCount + 2);
        int edgeIndex = dualNgonId % (maxEdgeCount + 2) - 2;
        if (cursor.vertexId == vertexId && cursor.floor == floor && cursor.edgeIndex == edgeIndex) return;
        cursor.vertexId = vertexId;
        cursor.floor = floor;
        cursor.edgeIndex = edgeIndex;
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
        if (cursor.vertexId == -1 || fullBaseGrid == null) return;

        int floor = cursor.floor;
        BMesh.Vertex v = fullBaseGrid.vertices[cursor.vertexId];
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

        UpdateSkin(new Voxel { vert = v, floor = floor });
        //ComputeSkin(tile);
    }

    public void RemoveVoxelAtCursor()
    {
        if (cursor.vertexId == -1 || fullBaseGrid == null) return;
        int floor = cursor.floor;
        BMesh.Vertex v = fullBaseGrid.vertices[cursor.vertexId];
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

        UpdateSkin(new Voxel { vert = v, floor = floor });
    }
    #endregion

    #region [MonoBehavior]
    private void OnEnable()
    {
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

        if (wfcGridForGizmos != null && false)
        {
            BMeshUnity.DrawGizmos(wfcGridForGizmos);
#if UNITY_EDITOR
            Handles.color = Color.blue;
            Gizmos.color = Color.blue;
            foreach (var l in wfcGridForGizmos.loops)
            {
                var adj = l.attributes["adjacency"].asInt().data[0];
                Handles.Label(l.vert.point * 0.75f + l.edge.Center() * 0.25f, "" + adj);
            }
            foreach (var v in wfcGridForGizmos.vertices)
            {
                var xclass = v.attributes["class"].asInt().data[0];
                //Handles.Label(v.point, "#" + v.id + " (" + xclass + ")");
            }
#endif // UNITY_EDITOR
        }

        if (wfcGridForGizmos != null && fullBaseGrid != null && wfcGridForGizmos.vertices.Count > 0)
        {
            var v0 = wfcGridForGizmos.vertices[0];
            var dualvoxel = DualVoxel.FromAttribute(v0.attributes["dualvoxel"], fullBaseGrid);
            var verts = dualvoxel.face.NeighborVertices().ToArray();
            var edges = dualvoxel.face.NeighborEdges().ToArray();
            var m = GetTransformedModule(v0);
            if (m != null)
            {
                var mf = m.baseModule.meshFilter;
                Vector3 floorOffset = dualvoxel.floor * Vector3.up;
                var controlPoints = new Vector3[12]; ;
                ModuleTransform transform = m.transform;
                //ModuleTransform transform = new ModuleTransform("z");

#if UNITY_EDITOR
                Handles.color = Color.blue;
                Gizmos.color = Color.blue;
                for (int i = 0; i < 8; ++i)
                {
                    int j = transform.FromCanonical(i);
                    Handles.Label(verts[j%4].point + (dualvoxel.floor + (j/4)) * Vector3.up, "" + i);
                }
            
                // Match occupation points with control points
                controlPoints[0] = transform.EdgeCenter(1, 2, verts, edges) + floorOffset;
                controlPoints[1] = transform.EdgeCenter(2, 3, verts, edges) + floorOffset;
                controlPoints[2] = transform.EdgeCenter(3, 0, verts, edges) + floorOffset;
                controlPoints[3] = transform.EdgeCenter(0, 1, verts, edges) + floorOffset;
                controlPoints[4] = transform.EdgeCenter(1, 5, verts, edges) + floorOffset;
                controlPoints[5] = transform.EdgeCenter(2, 6, verts, edges) + floorOffset;
                controlPoints[6] = transform.EdgeCenter(3, 7, verts, edges) + floorOffset;
                controlPoints[7] = transform.EdgeCenter(0, 4, verts, edges) + floorOffset;
                controlPoints[8] = transform.EdgeCenter(5, 6, verts, edges) + floorOffset;
                controlPoints[9] = transform.EdgeCenter(6, 7, verts, edges) + floorOffset;
                controlPoints[10] = transform.EdgeCenter(7, 4, verts, edges) + floorOffset;
                controlPoints[11] = transform.EdgeCenter(4, 5, verts, edges) + floorOffset;

                for (int j = 0; j < 12; ++j)
                {
                    Handles.Label(controlPoints[j], "$" + j);
                }
            }
#endif // UNITY_EDITOR
        }


        /*
        BMeshUnity.DrawGizmos(fullBaseGrid);
#if UNITY_EDITOR
        foreach (var v in fullBaseGrid.vertices)
        {
            Handles.Label(v.point, "" + v.id);
        }
#endif // UNITY_EDITOR
        
        foreach (var v in fullBaseGrid.vertices)
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
            foreach (var v in fullBaseGrid.vertices)
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
        */
    }
    #endregion
}
