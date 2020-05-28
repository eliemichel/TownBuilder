using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * Render a module many times (instanced), deformed using per-instance control
 * point buffer.
 * Usage:
 *     ResetInstances();
 *     int offset = AddInstance();
 *     ControlPointData[offset + 3 * i + k] = k-th component of i-th control point of the instance
 */
public class MarchingModuleRenderer : MonoBehaviour
{
    public Material material;
    public MeshFilter moduleMesh;
    public float timeBudget = 20; // in ms, for precomputation coroutine

    public float[] ControlPointData { get; private set; }

    bool ready = false;
    float[] weights;
    ComputeBuffer weightBuffer;
    ComputeBuffer controlPointBuffer;
    Mesh mesh;
    int instanceCount = 0;
    int previousInstanceCount = 0;

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

    void CloneMesh()
    {
        Mesh originalMesh = moduleMesh.sharedMesh;
        mesh = new Mesh
        {
            name = "clone",
            vertices = originalMesh.vertices,
            triangles = originalMesh.triangles,
            normals = originalMesh.normals,
            uv = originalMesh.uv
        };
    }

    IEnumerator PrecomputeWeightsCoroutine()
    {
        Debug.Assert(cageVertices.Length == 12);

        weights = new float[mesh.vertices.Length * cageVertices.Length];
        Matrix4x4 localToWorldMatrix = moduleMesh.transform.localToWorldMatrix;

        float startTime = Time.realtimeSinceStartup;

        for (var vid = 0; vid < mesh.vertices.Length; vid++)
        {
            Vector3 pos = localToWorldMatrix * mesh.vertices[vid];
            SmvcDeform.ComputeCoordinates(pos, cageFaces, cageVertices, weights, vid * cageVertices.Length);

            if (Time.realtimeSinceStartup - startTime > timeBudget * 1e-3)
            {
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }

        WriteWeightsToComputeBuffer();
        ready = true;
    }

    void WriteWeightsToComputeBuffer()
    {
        weightBuffer = new ComputeBuffer(weights.Length, sizeof(float), ComputeBufferType.Default, ComputeBufferMode.Immutable);
        weightBuffer.SetData(weights);
        weights = null; // free memory

        material.SetBuffer("_Weights", weightBuffer);
    }

    /**
     * Before calling AddInstance(), instances must be reset
     */
    public void ResetInstances()
    {
        previousInstanceCount = instanceCount;
        instanceCount = 0;
    }

    /**
     * Return the index in ControlPointData where to write the positions for
     * the control points of this instance.
     */
    public int AddInstance()
    {
        ++instanceCount;
        int n = 3 * cageVertices.Length * instanceCount;
        if (ControlPointData == null)
        {
            ControlPointData = new float[3 * cageVertices.Length];
        }
        while (ControlPointData.Length < n)
        {
            ControlPointData = new float[ControlPointData.Length * 2];
        }
        return n - 3 * cageVertices.Length;
    }

    void UpdateControlPointBuffer()
    {
        if (!ready) return;
        
        if (instanceCount > previousInstanceCount || controlPointBuffer == null)
        {
            controlPointBuffer = new ComputeBuffer(cageVertices.Length * instanceCount, 3 * sizeof(float), ComputeBufferType.Default);
            material.SetBuffer("_ControlPoints", controlPointBuffer);
        }

        controlPointBuffer.SetData(ControlPointData, 0, 0, cageVertices.Length * 3 * instanceCount);
    }

    void DrawDeformedInstances()
    {
        if (material == null) return;

        Matrix4x4[] matrices = new Matrix4x4[instanceCount];
        for (int i = 0; i < matrices.Length; ++i)
        {
            matrices[i] = moduleMesh.transform.localToWorldMatrix;
        }
        Graphics.DrawMeshInstanced(moduleMesh.sharedMesh, 0, material, matrices);
    }

    void Start()
    {
        CloneMesh();
        StartCoroutine(PrecomputeWeightsCoroutine());
    }

    void Update()
    {
        UpdateControlPointBuffer();
        DrawDeformedInstances();
    }
}
