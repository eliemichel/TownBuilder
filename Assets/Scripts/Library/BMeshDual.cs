using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

/**
 * Some operators related to dual meshes, in the special case of our irregular
 * grid. This grid is represented as a mesh for its ground and then assumed to
 * be infinitely repeated vertically, hence the 'floor' argument to some
 * methods.
 */
public class BMeshDual
{
    /**
     * Vertical position of a floor
     */
    static Vector3 FloorOffset(int floor)
    {
        return Vector3.up * Mathf.Max(floor - 0.5f, 0);
    }

    /**
     * Create the face dual to a given vertex.
     * The corners of this face are the centers of the faces using the dual vertex.
     */
    public static void AddDualNgon(BMesh mesh, Vertex v, int floor, bool flipped = false) // @flipped anyway face orientation is messy (todo)
    {
        Vertex nv = mesh.AddVertex(v.point + FloorOffset(floor));
        var faces = v.NeighborFaces();
        var verts = new List<Vertex>();
        foreach (Face f in faces)
        {
            Vertex u = mesh.AddVertex(f.Center() + FloorOffset(floor));
            verts.Add(u);
        }
        int prev_i = verts.Count - 1;
        for (int i = 0; i < verts.Count; ++i)
        {
            mesh.AddFace(verts[flipped ? i : prev_i], nv, verts[flipped ? prev_i : i]);
            prev_i = i;
        }
    }

    /**
     * Create a face dual to an edge of the grid mesh.
     * The corners of this face are the centers of the faces that share the
     * edge (two faces) plus the same at the next floor.
     */
    public static void AddDualNgonWall(BMesh mesh, Edge e, int floor)
    {
        var faces = e.NeighborFaces();
        Debug.Assert(faces.Count >= 2);
        var verts = new List<Vertex>();
        foreach (Face f in faces)
        {
            Vertex u0 = mesh.AddVertex(f.Center() + FloorOffset(floor));
            Vertex u1 = mesh.AddVertex(f.Center() + FloorOffset(floor + 1));
            verts.Add(u0);
            verts.Add(u1);
        }
        int prev_i = verts.Count / 2 - 1;
        for (int i = 0; i < verts.Count / 2; ++i)
        {
            mesh.AddFace(verts[2 * prev_i + 0], verts[2 * i + 0], verts[2 * i + 1], verts[2 * prev_i + 1]);
            prev_i = i;
        }
    }

    /**
     * This is a bit more dedicated to the TownBuilder scenario. Read the
     * occupancy attribute to add dual faces for all grid cells above a given
     * vertex of the grid base mesh.
     * 
     * Also write in uv attribute an identifier to be able to retrieve the
     * virtual grid index later on. This identifier requires a maximum height
     * to be specified.
     * 
     * Assumes that vertices have consistent id from 0 to vertCount - 1
     */
    public static void AddDualNgonColumn(BMesh mesh, Vertex v, AttributeDefinition uvAttr, string occupancyAttr, int maxEdgeCount = 4)
    {
        Debug.Assert(v.edge != null);
        var occupancy = v.attributes[occupancyAttr].asFloat().data;

        bool occ, prev_occ = true;
        for (int floor = 0; floor < occupancy.Length; ++floor, prev_occ = occ)
        {
            occ = occupancy[floor] > 0;

            // Add face above the cell
            if (occ && !prev_occ)
            {
                int dualNgonId = 1 + floor * (maxEdgeCount + 2) + 0;
                uvAttr.defaultValue = new FloatAttributeValue(v.id, dualNgonId);
                AddDualNgon(mesh, v, floor, true /* flipped */);
            }

            // Add face bellow the cell
            if (!occ && prev_occ)
            {
                int dualNgonId = 1 + floor * (maxEdgeCount + 2) + 1;
                uvAttr.defaultValue = new FloatAttributeValue(v.id, dualNgonId);
                AddDualNgon(mesh, v, floor, false /* flipped */);
            }

            // If cell is occupied, add wall arounds at interfaces with empty cells
            if (occ)
            {
                int edgeIndex = 0; // will be written in UVs to find it back at mouse ray casting
                Edge it = v.edge;
                do
                {
                    Vertex neighbor = it.OtherVertex(v);
                    float nocc = neighbor.attributes[occupancyAttr].asFloat().data[floor];
                    if (nocc == 0)
                    {
                        int dualNgonId = 1 + floor * (maxEdgeCount + 2) + (edgeIndex + 2);
                        uvAttr.defaultValue = new FloatAttributeValue(v.id, dualNgonId);
                        AddDualNgonWall(mesh, it, floor);
                    }
                    it = it.Next(v);
                    ++edgeIndex;
                } while (it != v.edge);
            }
        }
    }

    // Low level operation ensuring that e2 is right after e1 in the chained
    // list of edges that are around vertex v.
    // TODO: move the SwapEdge part to BMesh and test it
    public static void SwapEdge(Edge e1, Edge e2, Vertex v)
    {
        var before_e2 = e2.Prev(v);
        var after_e2 = e2.Next(v);
        var before_pivot = e1.Prev(v);
        var after_pivot = e1.Next(v);

        if (after_e2 == e1 && after_pivot == e2)
        {
            // nothing to do
        }
        else
        {
            after_e2.SetPrev(v, e1);
            e1.SetNext(v, after_e2);

            before_pivot.SetNext(v, e2);
            e2.SetPrev(v, before_pivot);

            if (after_pivot == e2) // and so after_e2 != pivot
            {
                e2.SetNext(v, e1);
                e1.SetPrev(v, e2);
            }
            else if (after_e2 == e1) // and so after_pivot != e2
            {
                e1.SetNext(v, e2);
                e2.SetPrev(v, e1);
            }
            else
            {
                before_e2.SetNext(v, e1);
                e1.SetPrev(v, before_e2);

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
}
