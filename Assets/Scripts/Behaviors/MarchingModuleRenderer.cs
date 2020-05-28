﻿using System.Collections;
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
    public Transform moduleTransform;
    public float timeBudget = 20; // in ms, for precomputation coroutine

    public float[] ControlPointData { get; private set; }
    public int[] FlagData { get; private set; } // one int per instance, used for flipping normals depending on module transform

    bool ready = false;
    float[] weights;
    ComputeBuffer weightBuffer;
    ComputeBuffer controlPointBuffer;
    ComputeBuffer flagBuffer;
    Mesh mesh;
    int instanceCount = 0;
    int previousInstanceCount = 0;
    bool controlPointBufferUpToDate = false;

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
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999f); // deactivate culling
    }

    IEnumerator PrecomputeWeightsCoroutine()
    {
        Debug.Assert(cageVertices.Length == 12);

        weights = new float[mesh.vertices.Length * cageVertices.Length];

        Matrix4x4 localToWorldMatrix = moduleMesh.transform.localToWorldMatrix;
        //Matrix4x4 localToWorldMatrix = moduleTransform.localToWorldMatrix;
        localToWorldMatrix.m03 = 0;
        localToWorldMatrix.m13 = 0;
        localToWorldMatrix.m23 = 0;
        // dirty fix (because of FBX axes?)
        localToWorldMatrix = new Matrix4x4(
            new Vector4(-1, 0, 0, 0),
            new Vector4(0, 1, 0, 0),
            new Vector4(0, 0, -1, 0),
            new Vector4(0, 0, 0, 1)
        ) * localToWorldMatrix;

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
        material.SetInt("_CageVertCount", cageVertices.Length);
    }

    /**
     * Before calling AddInstance(), instances must be reset
     */
    public void ResetInstances()
    {
        previousInstanceCount = instanceCount;
        instanceCount = 0;
        controlPointBufferUpToDate = false;
    }

    /**
     * Return the index in ControlPointData where to write the positions for
     * the control points of this instance.
     */
    public int AddInstance(int flags = 0)
    {
        ++instanceCount;
        int n = 3 * cageVertices.Length * instanceCount;
        if (ControlPointData == null)
        {
            Debug.Assert(FlagData == null);
            ControlPointData = new float[3 * cageVertices.Length];
            FlagData = new int[1];
        }
        while (ControlPointData.Length < n)
        {
            float[] newControlPointData = new float[ControlPointData.Length * 2];
            for (int i = 0; i < ControlPointData.Length; ++i)
            {
                newControlPointData[i] = ControlPointData[i];
            }
            ControlPointData = newControlPointData;

            int[] newFlagData = new int[FlagData.Length * 2];
            for (int i = 0; i < FlagData.Length; ++i)
            {
                newFlagData[i] = FlagData[i];
            }
            FlagData = newFlagData;
        }
        Debug.Assert(FlagData.Length * 3 * cageVertices.Length == ControlPointData.Length);
        FlagData[instanceCount - 1] = flags;
        return n - 3 * cageVertices.Length;
    }

    void UpdateControlPointBuffer()
    {
        if (controlPointBufferUpToDate || !ready || instanceCount == 0) return;
        
        if (controlPointBuffer == null || cageVertices.Length * instanceCount > controlPointBuffer.count)
        {
            if (controlPointBuffer != null)
                Debug.Log("Reallocating cp buffer because we need " + (cageVertices.Length * instanceCount) + " > " + (controlPointBuffer.count) + " points");
            if (controlPointBuffer != null) controlPointBuffer.Dispose();
            controlPointBuffer = new ComputeBuffer(cageVertices.Length * instanceCount, 3 * sizeof(float), ComputeBufferType.Default);
            material.SetBuffer("_ControlPoints", controlPointBuffer);

            if (flagBuffer != null) flagBuffer.Dispose();
            flagBuffer = new ComputeBuffer(instanceCount, sizeof(int), ComputeBufferType.Default);
            material.SetBuffer("_Flags", flagBuffer);
        }

        Debug.Log("UpdateControlPointBuffer: " + instanceCount + " instances");
        controlPointBuffer.SetData(ControlPointData, 0, 0, cageVertices.Length * 3 * instanceCount);
        flagBuffer.SetData(FlagData, 0, 0, instanceCount);
        controlPointBufferUpToDate = true;
    }

    void DrawDeformedInstances()
    {
        if (material == null) return;

        Matrix4x4[] matrices = new Matrix4x4[instanceCount];
        for (int i = 0; i < matrices.Length; ++i)
        {
            matrices[i] = Matrix4x4.identity; // moduleMesh.transform.localToWorldMatrix;
        }
        Graphics.DrawMeshInstanced(moduleMesh.sharedMesh, 0, material, matrices);
    }

    void Start()
    {
        if (moduleMesh == null)
        {
            var module = GetComponent<MarchingModule>();
            if (module != null)
            {
                moduleMesh = module.meshFilter;
            }
        }
        CloneMesh();
        StartCoroutine(PrecomputeWeightsCoroutine());
    }

    void Update()
    {
        UpdateControlPointBuffer();
        DrawDeformedInstances();
    }

    private void OnDrawGizmos()
    {
        return;
        for (int i = 0; i < instanceCount; ++i)
        {
            Vector3 prev = Vector3.zero;
            for (int j = 0; j < cageVertices.Length; ++j)
            {
                Gizmos.color = FlagData[i] != 0 ? Color.green : Color.blue;
                int offset = (i * cageVertices.Length + j) * 3;
                Vector3 p = new Vector3(
                    ControlPointData[offset + 0],
                    ControlPointData[offset + 1],
                    ControlPointData[offset + 2]
                );
                Gizmos.DrawSphere(p, j == 0 ? 0.03f : 0.025f);
                if (prev != Vector3.zero) Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
