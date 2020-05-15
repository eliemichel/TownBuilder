using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BMesh;

public class BMeshUnity
{
    // Only works with tri or quad meshes!
    public static void SetInMeshFilter(BMesh mesh, MeshFilter mf)
    {
        // Points
        Vector2[] uvs = null;
        Vector2[] uvs2 = null;
        Vector3[] points = new Vector3[mesh.vertices.Count];
        if (mesh.HasVertexAttribute("uv"))
        {
            uvs = new Vector2[mesh.vertices.Count];
        }
        if (mesh.HasVertexAttribute("uv2"))
        {
            uvs2 = new Vector2[mesh.vertices.Count];
        }
        int i = 0;
        foreach (var vert in mesh.vertices)
        {
            vert.id = i;
            points[i] = vert.point;
            if (uvs != null)
            {
                var uv = vert.attributes["uv"] as FloatAttributeValue;
                uvs[i] = new Vector2(uv.data[0], uv.data[1]);
            }
            if (uvs2 != null)
            {
                var uv2 = vert.attributes["uv2"] as FloatAttributeValue;
                uvs2[i] = new Vector2(uv2.data[0], uv2.data[1]);
            }
            ++i;
        }

        // Triangles
        int tricount = 0;
        foreach (var f in mesh.faces)
        {
            Debug.Assert(f.vertcount == 3 || f.vertcount == 4);
            tricount += f.vertcount - 2;
        }
        int[] triangles = new int[3 * tricount];
        i = 0;
        foreach (var f in mesh.faces)
        {
            Debug.Assert(f.vertcount == 3 || f.vertcount == 4);
            {
                var l = f.loop;
                triangles[3 * i + 0] = l.vert.id; l = l.next;
                triangles[3 * i + 1] = l.vert.id; l = l.next;
                triangles[3 * i + 2] = l.vert.id; l = l.next;
                ++i;
            }
            if (f.vertcount == 4)
            {
                var l = f.loop.next.next;
                triangles[3 * i + 0] = l.vert.id; l = l.next;
                triangles[3 * i + 1] = l.vert.id; l = l.next;
                triangles[3 * i + 2] = l.vert.id; l = l.next;
                ++i;
            }
        }

        // Apply mesh
        Mesh unityMesh = new Mesh();
        mf.mesh = unityMesh;
        unityMesh.vertices = points;
        if (uvs != null) unityMesh.uv = uvs;
        if (uvs2 != null) unityMesh.uv2 = uvs2;
        unityMesh.triangles = triangles;
        unityMesh.RecalculateNormals();
    }

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

    public static void DrawGizmos(BMesh mesh)
    {
        Gizmos.color = Color.yellow;
        foreach (var e in mesh.edges)
        {
            Gizmos.DrawLine(e.vert1.point, e.vert2.point);
        }
        Gizmos.color = Color.red;
        foreach (var l in mesh.loops)
        {
            Vertex vert = l.vert;
            Vertex other = l.edge.OtherVertex(vert);
            Gizmos.DrawRay(vert.point, (other.point - vert.point) * 0.1f);

            Loop nl = l.next;
            Vertex nother = nl.edge.ContainsVertex(vert) ? nl.edge.OtherVertex(vert) : nl.edge.OtherVertex(other);
            Vector3 no = vert.point + (other.point - vert.point) * 0.1f;
            Gizmos.DrawRay(no, (nother.point - no) * 0.1f);
        }
        Gizmos.color = Color.green;
        int i = 0;
        foreach (var f in mesh.faces)
        {
            Vector3 c = f.Center();
            Gizmos.DrawLine(c, f.loop.vert.point);
            Gizmos.DrawRay(c, (f.loop.next.vert.point - c) * 0.2f);
#if UNITY_EDITOR
            //Handles.Label(c, "f" + i);
            ++i;
#endif // UNITY_EDITOR
        }

        i = 0;
        foreach (Vertex v in mesh.vertices)
        {
#if UNITY_EDITOR
            //var uv = v.attributes["uv"] as BMesh.FloatAttributeValue;
            //Handles.Label(v.point, "" + i);
            ++i;
#endif // UNITY_EDITOR
        }
    }
}
