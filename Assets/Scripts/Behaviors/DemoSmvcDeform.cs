using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoSmvcDeform : MonoBehaviour
{
    public MeshFilter deformedMesh;
    public Transform[] orginalHandles;
    public Transform[] deformedHandles;
    public Transform debug;
    public bool run = false;
    public float timeBudget = 8; // in ms

    public Mesh originalMesh;
    Vector3[] originalVertices;
    Vector3[] vertices;
    float[][] weights;

    
    static readonly int[][] cageFaces = new int[][] {
        new int[]{ 0, 3, 2, 1 },
        new int[]{ 0, 5, 8, 4 },
        new int[]{ 1, 6, 9, 5 },
        new int[]{ 2, 7, 10, 6 },
        new int[]{ 3, 4, 11, 7 },
        new int[]{ 8, 9, 10, 11 },

        new int[]{ 0, 12+2, 5 },
        new int[]{ 5, 12+2, 1 },
        new int[]{ 1, 12+2, 0 },

        new int[]{ 1, 12+3, 6 },
        new int[]{ 6, 12+3, 2 },
        new int[]{ 2, 12+3, 1 },

        new int[]{ 2, 12+0, 7 },
        new int[]{ 7, 12+0, 3 },
        new int[]{ 3, 12+0, 2 },

        new int[]{ 3, 12+1, 4 },
        new int[]{ 4, 12+1, 0 },
        new int[]{ 0, 12+1, 3 },

        new int[]{ 5, 12+6, 8 },
        new int[]{ 8, 12+6, 9 },
        new int[]{ 9, 12+6, 5 },

        new int[]{ 6, 12+7, 9 },
        new int[]{ 9, 12+7,10 },
        new int[]{10, 12+7, 6 },

        new int[]{ 7, 12+4,10 },
        new int[]{10, 12+4,11 },
        new int[]{11, 12+4, 7 },

        new int[]{ 4, 12+5,11 },
        new int[]{11, 12+5, 8 },
        new int[]{ 8, 12+5, 4 },
    };

    IEnumerator PrecomputeWeightsCoroutine()
    {
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Debug.Assert(orginalHandles.Length == 20);
        Vector3[] cageVertices = new Vector3[20];
        for (int i = 0; i < 20; ++i)
        {
            cageVertices[i] = orginalHandles[i].position;
        }
        int[][] cageFaces = new int[][] {
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

        weights = new float[vertices.Length][];

        float startTime = Time.realtimeSinceStartup;

        for (var vid = 0; vid < vertices.Length; vid++)
        {
            weights[vid] = new float[20];

            Vector3 pos = deformedMesh.transform.localToWorldMatrix * originalVertices[vid];
            SmvcDeform.ComputeCoordinates(pos, cageFaces, cageVertices, weights[vid]);
            Debug.Assert(weights[vid].Length == cageVertices.Length);

            for (int j = 0; j < weights[vid].Length; ++j)
            {
                //Debug.Assert(!float.IsNaN(weights[j]), "weight #" + j + " is NaN at vertex #" + vid);
                if (float.IsNaN(weights[vid][j])) Debug.LogWarning("weight #" + j + " is NaN at vertex #" + vid + " " + pos);
            }

            if (Time.realtimeSinceStartup - startTime > timeBudget * 1e-3)
            {
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }
    }

    private void Start()
    {
        originalMesh = deformedMesh.sharedMesh;
        var clonedMesh = new Mesh
        {
            name = "clone",
            vertices = originalMesh.vertices,
            triangles = originalMesh.triangles,
            normals = originalMesh.normals,
            uv = originalMesh.uv
        };
        deformedMesh.mesh = clonedMesh;  //3

        vertices = clonedMesh.vertices; //4
        originalVertices = originalMesh.vertices;

        if (run) StartCoroutine(ContinuousDeform());
    }

    IEnumerator ContinuousDeform()
    {
        for (var it = PrecomputeWeightsCoroutine(); it.MoveNext();)
        {
            yield return null;
        }
        for (; ;)
        {
            for (var it = DeformCoroutine(); it.MoveNext();) {
                yield return null;
            }
            yield return null;
        }
    }

    IEnumerator DeformCoroutine()
    {
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Debug.Assert(orginalHandles.Length == 20);
        
        float startTime = Time.realtimeSinceStartup;

        for (var vid = 0; vid < vertices.Length; vid++)
        {
            Vector3 newPos = Vector3.zero;
            for (int j = 0; j < weights[vid].Length; ++j)
            {
                newPos += weights[vid][j] * deformedHandles[j].position;
            }
            vertices[vid] = deformedMesh.transform.worldToLocalMatrix * newPos;

            if (Time.realtimeSinceStartup - startTime > timeBudget * 1e-3)
            {
                //Debug.Log("i = " + i + "/" + vertices.Length);
                deformedMesh.mesh.vertices = vertices;
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }
        deformedMesh.mesh.vertices = vertices;
        deformedMesh.mesh.RecalculateNormals();
    }

    public void Deform()
    {
        for (var it = DeformCoroutine(); it.MoveNext();) { }
    }

    public void DeformDebug()
    {
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Debug.Assert(orginalHandles.Length == 20);
        Vector3[] cageVertices = new Vector3[20];
        for (int i = 0; i < 20; ++i)
        {
            cageVertices[i] = orginalHandles[i].position;
        }

        float[] weights = new float[20];
        SmvcDeform.ComputeCoordinates(debug.position, cageFaces, cageVertices, weights);
        Debug.Assert(weights.Length == cageVertices.Length);

        Vector3 newPos = Vector3.zero;
        for (int j = 0; j < weights.Length; ++j)
        {
            newPos += weights[j] * deformedHandles[j].position;
        }
        debug.position = newPos;
    }

    public void ShowCage()
    {
        Vector3[] cageVertices = new Vector3[20];
        int i;
        for (i = 0; i < 20; ++i)
        {
            cageVertices[i] = transform.worldToLocalMatrix * orginalHandles[i].position;
        }

        int triCount = 0;
        foreach (var face in cageFaces)
        {
            triCount += face.Length - 2;
        }
        int[] cageFacesAsTriangles = new int[3*triCount];
        i = 0;
        foreach (var face in cageFaces)
        {
            cageFacesAsTriangles[3 * i + 0] = face[0];
            cageFacesAsTriangles[3 * i + 1] = face[2];
            cageFacesAsTriangles[3 * i + 2] = face[1];
            ++i;
            if (face.Length == 4)
            {
                cageFacesAsTriangles[3 * i + 0] = face[0];
                cageFacesAsTriangles[3 * i + 1] = face[3];
                cageFacesAsTriangles[3 * i + 2] = face[2];
                ++i;
            }
        }

        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = cageVertices;
        mesh.triangles = cageFacesAsTriangles;
        mesh.RecalculateNormals();
        GetComponent<MeshRenderer>().enabled = true;
    }

    private void OnDrawGizmos()
    {
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Gizmos.color = Color.yellow;
        for (int i = 0; i < orginalHandles.Length; ++i)
        {
            Gizmos.DrawLine(orginalHandles[i].position, deformedHandles[i].position);
        }
    }
}
