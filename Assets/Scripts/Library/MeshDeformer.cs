using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * The public API is rather simple -- just Vector3 Deform(Vector3) -- but
 * it is specialized to use SmvcDeform on cube's edge centers
 */
public class MeshDeformer
{
    public Vector3[] controlPoints = new Vector3[12];

    readonly Matrix4x4 premultiplyMatrix;
    float[][] weights;

    static readonly Vector3[] cageVertices = new Vector3[] {
        new Vector3(0.5f, -0.5f, 0) * 2 + Vector3.up,
        new Vector3(0, -0.5f, 0.5f) * 2 + Vector3.up,
        new Vector3(-0.5f, -0.5f, 0) * 2 + Vector3.up,
        new Vector3(0, -0.5f, -0.5f) * 2 + Vector3.up,

        new Vector3(0.5f, 0, -0.5f) * 2 + Vector3.up,
        new Vector3(0.5f, 0, 0.5f) * 2 + Vector3.up,
        new Vector3(-0.5f, 0, 0.5f) * 2 + Vector3.up,
        new Vector3(-0.5f, 0, -0.5f) * 2 + Vector3.up,

        new Vector3(0.5f, 0.5f, 0) * 2 + Vector3.up,
        new Vector3(0, 0.5f, 0.5f) * 2 + Vector3.up,
        new Vector3(-0.5f, 0.5f, 0) * 2 + Vector3.up,
        new Vector3(0, 0.5f, -0.5f) * 2 + Vector3.up
    };

    static readonly int[][] cageFaces = new int[][] {
        new int[]{ 0, 3, 2, 1 },
        new int[]{ 0, 5, 8, 4 },
        new int[]{ 1, 6, 9, 5 },
        new int[]{ 2, 7, 10, 6 },
        new int[]{ 3, 4, 11, 7 },
        new int[]{ 8, 9, 10, 11 },

        new int[]{ 0, 1, 5 },
        new int[]{ 1, 2, 6 },
        new int[]{ 2, 3, 7 },
        new int[]{ 3, 0, 4 },

        new int[]{ 5, 9, 8 },
        new int[]{ 6, 10, 9 },
        new int[]{ 7, 11, 10 },
        new int[]{ 4, 8, 11 }
    };

    public MeshDeformer(Transform transform)
    {
        premultiplyMatrix = transform.localToWorldMatrix;
    }

    public void Precompute(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Debug.Assert(cageVertices.Length == 12);

        weights = new float[vertices.Length][];

        float startTime = Time.realtimeSinceStartup;

        for (var vid = 0; vid < vertices.Length; vid++)
        {
            weights[vid] = new float[12];

            Vector3 p = vertices[vid];
            if (premultiplyMatrix != null) p = premultiplyMatrix * p;
            float tmp = p.x;
            p.x = p.z;
            p.z = tmp;

            SmvcDeform.ComputeCoordinates(p, cageFaces, cageVertices, weights[vid]);
            Debug.Assert(weights[vid].Length == cageVertices.Length);

            for (int j = 0; j < weights[vid].Length; ++j)
            {
                Debug.Assert(!float.IsNaN(weights[vid][j]), "weight #" + j + " is NaN at vertex #" + vid);
                //if (float.IsNaN(weights[vid][j])) Debug.LogWarning("weight #" + j + " is NaN at vertex #" + vid + " " + pos);
            }
        }
    }

    public Vector3 GetVertex(int vid)
    {
        Debug.Assert(vid < weights.Length);
        Debug.Assert(weights[0].Length == 12);

        Vector3 p = Vector3.zero;
        for (int j = 0; j < 12; ++j)
        {
            p += weights[vid][j] * controlPoints[j];
        }
        return p;
    }
}
