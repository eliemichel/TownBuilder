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
        Vector2[] unityUvs = null;
        if (mesh.HasVertexAttribute("uv")) unityUvs = unityMesh.uv;
        int[] unityTriangles = unityMesh.triangles;
        var verts = new Vertex[unityVertices.Length];
        
        for (int i = 0; i < unityVertices.Length; ++i)
        {
            Vector3 p = deformer != null ? deformer.GetVertex(i) : unityVertices[i];
            verts[i] = mesh.AddVertex(p);
            if (unityUvs != null)
            {
                var uv = verts[i].attributes["uv"].asFloat().data;
                uv[0] = unityUvs[i].x;
                uv[1] = unityUvs[i].y;
            }
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
