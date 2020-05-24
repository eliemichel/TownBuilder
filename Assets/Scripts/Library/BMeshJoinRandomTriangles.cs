using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

/**
 * This is a BMesh operator that joins neighbor triangles randomly while it can.
 */
public class BMeshJoinRandomTriangles
{
    public int maxIterations = -1; // Maximum number of fused edges, set to -1 to ignore

    // Static main function for quick use
    public static void Call(BMesh mesh, int maxIterations = -1)
    {
        var op = new BMeshJoinRandomTriangles {
            maxIterations  = maxIterations
        };
        op.Run(mesh);
    }

    /**
     * Tells whether the edge joins two triangles
     */
    bool CanFuse(Edge e)
    {
        var faces = e.NeighborFaces();
        return faces.Count == 2 && faces[0].vertcount == 3 && faces[1].vertcount == 3;
    }

    /**
     * Assuming the edge joins two triangles, fuse it to make a quad out of
     * these two triangles.
     */
    bool FuseEdge(BMesh mesh, Edge e)
    {
        var faces = e.NeighborFaces();
        Debug.Assert(CanFuse(e));

        var vertices = new Vertex[4];
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

        mesh.RemoveEdge(e);
        mesh.AddFace(vertices);
        return true;
    }

    /**
     * One step of the algorithm, returning true if it succeeded at finding an edge to fuse.
     */
    bool RemoveRandomEdge(BMesh mesh)
    {
        var candidates = new List<Edge>();
        foreach (var e in mesh.edges)
        {
            if (CanFuse(e))
            {
                candidates.Add(e);
            }
        }

        if (candidates.Count == 0) return false;

        int i = Random.Range(0, candidates.Count);
        FuseEdge(mesh, candidates[i]);
        return true;
    }

    /**
     * If maxIterations < 0, any iteration is valid, otherwise only those lower than maxIterations
     */
    bool IsValidIteration(int i)
    {
        return maxIterations < 0 || i < maxIterations;
    }

    void Run(BMesh mesh)
    {
        for (int i = 0; IsValidIteration(i) && RemoveRandomEdge(mesh); ++i) { }
    }
}
