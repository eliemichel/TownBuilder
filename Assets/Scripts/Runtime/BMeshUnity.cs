using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

public class BMeshUnity
{
    // Only works with tri or quad meshes!
    public static void SetInMeshFilter(BMesh mesh, MeshFilter mf)
    {
        // Points
        Vector2[] uvs = null;
        Vector3[] points = new Vector3[mesh.vertices.Count];
        if (mesh.HasVertexAttribute("uv"))
        {
            uvs = new Vector2[mesh.vertices.Count];
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
        unityMesh.triangles = triangles;
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
            BMesh.Vertex vert = l.vert;
            BMesh.Vertex other = l.edge.OtherVertex(vert);
            Gizmos.DrawRay(vert.point, (other.point - vert.point) * 0.1f);

            BMesh.Loop nl = l.next;
            BMesh.Vertex nother = nl.edge.ContainsVertex(vert) ? nl.edge.OtherVertex(vert) : nl.edge.OtherVertex(other);
            Vector3 no = vert.point + (other.point - vert.point) * 0.1f;
            Gizmos.DrawRay(no, (nother.point - no) * 0.1f);
        }

        foreach (var v in mesh.vertices)
        {
            float weight = (v.attributes["weight"] as BMesh.FloatAttributeValue).data[0];
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(v.point, weight * 0.1f);

            var glued = v.attributes["glued"] as BMesh.FloatAttributeValue;
            Vector3 restpos = (v.attributes["restpos"] as BMesh.FloatAttributeValue).AsVector3();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(restpos, glued.data[0] * 0.15f);

#if UNITY_EDITOR
            //var uv = v.attributes["uv"] as BMesh.FloatAttributeValue;
            //Handles.Label(v.point, "(" + uv.data[0] + "," + uv.data[1] + ")");
#endif // UNITY_EDITOR
        }
    }
}
