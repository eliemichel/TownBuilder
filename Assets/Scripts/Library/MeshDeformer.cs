using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * The public API is rather simple -- just Vector3 Deform(Vector3) -- but
 * it is specialized to use SmvcDeform on cube's edge centers
 */
public class MeshDeformer
{
    readonly Matrix4x4 premultiplyMatrix;
    readonly float[] weights = new float[12];
    readonly Vector3[] deformedVertices;

    static readonly Vector3[] cageVertices = new Vector3[] {
        new Vector3(0.5f, -0.5f, 0),
        new Vector3(0, -0.5f, 0.5f),
        new Vector3(-0.5f, -0.5f, 0),
        new Vector3(0, -0.5f, -0.5f),

        new Vector3(0.5f, 0, -0.5f),
        new Vector3(0.5f, 0, 0.5f),
        new Vector3(-0.5f, 0, 0.5f),
        new Vector3(-0.5f, 0, -0.5f),

        new Vector3(0.5f, 0.5f, 0),
        new Vector3(0, 0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, 0),
        new Vector3(0, 0.5f, -0.5f)
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

    public MeshDeformer(Transform transform, Vector3[] deformedVertices)
    {
        premultiplyMatrix = transform.localToWorldMatrix;
        this.deformedVertices = deformedVertices;
    }

    public Vector3 Deform(Vector3 p)
    {
        if (premultiplyMatrix != null)
        {
            p = premultiplyMatrix * p;
        }

        SmvcDeform.ComputeCoordinates(p, cageFaces, cageVertices, weights);

        p = Vector3.zero;
        for (int j = 0; j < weights.Length; ++j)
        {
            p += weights[j] * deformedVertices[j];
        }

        return p;
    }
}
