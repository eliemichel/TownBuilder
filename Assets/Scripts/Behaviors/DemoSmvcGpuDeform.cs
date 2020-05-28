using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoSmvcGpuDeform : MonoBehaviour
{
    public MeshFilter deformedMesh;
    public Transform[] orginalHandles;
    public Transform[] deformedHandles;
    public Transform[] deformedHandles2; // for the second instance
    public Transform debug;
    public bool run = false;
    public float timeBudget = 8; // in ms

    public Mesh originalMesh;
    Vector3[] originalVertices;
    Vector3[] vertices;
    float[] weights;

    ComputeBuffer weightBuffer;
    ComputeBuffer controlPointBuffer;
    float[] controlPointData;
    Material deformedMaterial;

    ///////////////////////////////////////////////////////////////////////////
    #region [Deformation]
    IEnumerator PrecomputeWeightsCoroutine()
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

        weights = new float[vertices.Length * cageVertices.Length];

        float startTime = Time.realtimeSinceStartup;

        for (var vid = 0; vid < vertices.Length; vid++)
        {
            Vector3 pos = deformedMesh.transform.localToWorldMatrix * originalVertices[vid];
            SmvcDeform.ComputeCoordinates(pos, cageFaces, cageVertices, weights, vid * cageVertices.Length);

            for (int j = 0; j < cageVertices.Length; ++j)
            {
                //Debug.Assert(!float.IsNaN(weights[j]), "weight #" + j + " is NaN at vertex #" + vid);
                if (float.IsNaN(weights[vid * cageVertices.Length + j])) Debug.LogWarning("weight #" + j + " is NaN at vertex #" + vid + " " + pos);
            }

            if (Time.realtimeSinceStartup - startTime > timeBudget * 1e-3)
            {
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }

        WriteWeightsToComputeBuffer();
    }

    void WriteWeightsToComputeBuffer()
    {
        weightBuffer = new ComputeBuffer(weights.Length, sizeof(float), ComputeBufferType.Default, ComputeBufferMode.Immutable);
        weightBuffer.SetData(weights);

        controlPointBuffer = new ComputeBuffer(deformedHandles.Length * 2, 3 * sizeof(float), ComputeBufferType.Default);
        controlPointData = new float[deformedHandles.Length * 3 * 2];

        deformedMaterial = deformedMesh.GetComponent<MeshRenderer>().material;
        deformedMaterial.SetBuffer("_Weights", weightBuffer);
        deformedMaterial.SetBuffer("_ControlPoints", controlPointBuffer);
    }

    void DrawDeformedInstances()
    {
        if (deformedMaterial == null) return;

        Matrix4x4[] matrices = new Matrix4x4[2];
        for (int i = 0; i < matrices.Length; ++i)
        {
            matrices[i] = deformedMesh.transform.localToWorldMatrix;
        }
        Graphics.DrawMeshInstanced(deformedMesh.sharedMesh, 0, deformedMaterial, matrices);
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////
    #region [MonoBehavior]
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

        if (run) StartCoroutine(PrecomputeWeightsCoroutine());
    }

    private void Update()
    {
        UpdateControlPointBuffer();
        DrawDeformedInstances();
    }

    void UpdateControlPointBuffer()
    {
        if (controlPointBuffer == null) return;
        Debug.Assert(orginalHandles.Length == deformedHandles.Length);
        Debug.Assert(orginalHandles.Length == 12);

        for (int j = 0; j < deformedHandles.Length * 3; ++j)
        {
            controlPointData[j] = (deformedMesh.transform.worldToLocalMatrix * deformedHandles[j/3].position)[j%3];
        }
        for (int j = 0; j < deformedHandles.Length * 3; ++j)
        {
            controlPointData[deformedHandles.Length * 3 + j] = (deformedMesh.transform.worldToLocalMatrix * deformedHandles2[j / 3].position)[j % 3];
        }
        controlPointBuffer.SetData(controlPointData);
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
    #endregion
}
