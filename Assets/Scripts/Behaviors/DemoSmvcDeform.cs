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

    private void Start()
    {
        originalMesh = deformedMesh.sharedMesh;
        var clonedMesh = new Mesh(); //2

        clonedMesh.name = "clone";
        clonedMesh.vertices = originalMesh.vertices;
        clonedMesh.triangles = originalMesh.triangles;
        clonedMesh.normals = originalMesh.normals;
        clonedMesh.uv = originalMesh.uv;
        deformedMesh.mesh = clonedMesh;  //3

        vertices = clonedMesh.vertices; //4
        originalVertices = originalMesh.vertices;

        if (run) StartCoroutine(ContinuousDeform());
    }

    IEnumerator ContinuousDeform()
    {
        for (; ;)
        {
            for (var it = DeformCoroutine(); it.MoveNext();) {
                yield return null;
            }
        }
    }

    IEnumerator DeformCoroutine()
    {
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Debug.Assert(orginalHandles.Length == 12);
        Vector3[] cageVertices = new Vector3[12];
        for (int i = 0; i < 12; ++i)
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

        float[] weights = new float[12];

        float startTime = Time.realtimeSinceStartup;

        for (var i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = deformedMesh.transform.localToWorldMatrix * originalVertices[i];
            SmvcDeform.ComputeCoordinates(pos, cageFaces, cageVertices, weights);
            Debug.Assert(weights.Length == cageVertices.Length);

            Vector3 newPos = Vector3.zero;
            for (int j = 0; j < weights.Length; ++j)
            {
                newPos += weights[j] * deformedHandles[j].position;
            }
            vertices[i] = deformedMesh.transform.worldToLocalMatrix * newPos;

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
        Debug.Assert(orginalHandles.Length == 12);
        Vector3[] cageVertices = new Vector3[12];
        for (int i = 0; i < 12; ++i)
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

        float[] weights = new float[12];
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
        Vector3[] cageVertices = new Vector3[12];
        for (int i = 0; i < 12; ++i)
        {
            cageVertices[i] = transform.worldToLocalMatrix * orginalHandles[i].position;
        }
        int[] cageFacesAsTriangles = new int[] {
            0, 3, 2, 0, 2, 1,
            0, 5, 8, 0, 8, 4,
            1, 6, 9, 1, 9, 5,
            2, 7, 10, 2, 10, 6,
            3, 4, 11, 3, 11, 7,
            8, 9, 10, 8, 10, 11,

            0, 1, 5,
            1, 2, 6,
            2, 3, 7,
            3, 0, 4,

            5, 9, 8,
            6, 10, 9,
            7, 11, 10,
            4, 8, 11
        };

        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = cageVertices;
        mesh.triangles = cageFacesAsTriangles;
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
