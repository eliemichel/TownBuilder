using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BMesh;

public class BMeshUnityExtra
{
    
    public static void Merge(BMesh mesh, Mesh unityMesh, MeshDeformer deformer = null, bool flipFaces = false)
    {
        Vector3[] unityVertices = unityMesh.vertices;
        int[] unityTriangles = unityMesh.triangles;
        var verts = new Vertex[unityVertices.Length];
        
        for (int i = 0; i < unityVertices.Length; ++i)
        {
            Vector3 p = deformer != null ? deformer.GetVertex(i) : unityVertices[i];
            verts[i] = mesh.AddVertex(p);
        }
        for (int i = 0; i < unityTriangles.Length / 3; ++i)
        {
            mesh.AddFace(
                verts[unityTriangles[3 * i + (flipFaces ? 1 : 0)]],
                verts[unityTriangles[3 * i + (flipFaces ? 0 : 1)]],
                verts[unityTriangles[3 * i + 2]]
            );
        }
    }

}
