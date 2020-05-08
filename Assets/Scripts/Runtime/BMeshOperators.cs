using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

// BMesh Operators are free to override some attributes, provided that their docstring says it.
// It is the responsibility of calling code to save previous values if they are needed.
// All operators assume that the provided mesh is not null
public class BMeshOperators
{
    ///////////////////////////////////////////////////////////////////////////
    // AttributeLerp

    // Set all attributes in destination vertex to attr[v1] * (1 - t) + attr[v2] * t
    // Overriding attributes: all in vertex 'destination', none in others
    public static void AttributeLerp(BMesh mesh, Vertex destination, Vertex v1, Vertex v2, float t)
    {
        foreach (var attr in mesh.vertexAttributes)
        {
            switch (attr.type.baseType)
            {
                case AttributeBaseType.Float:
                {
                    var val1 = v1.attributes[attr.name] as FloatAttributeValue;
                    var val2 = v2.attributes[attr.name] as FloatAttributeValue;
                    int n = val1.data.Length;
                    Debug.Assert(val2.data.Length == n);
                    var val = new FloatAttributeValue { data = new float[n] };
                    for (int i = 0; i < n; ++i)
                    {
                        val.data[i] = Mathf.Lerp(val1.data[i], val2.data[i], t);
                    }
                    destination.attributes[attr.name] = val;
                    break;
                }
                case AttributeBaseType.Int:
                {
                    var val1 = v1.attributes[attr.name] as IntAttributeValue;
                    var val2 = v2.attributes[attr.name] as IntAttributeValue;
                    int n = val1.data.Length;
                    Debug.Assert(val2.data.Length == n);
                    var val = new IntAttributeValue { data = new int[n] };
                    for (int i = 0; i < n; ++i)
                    {
                        val.data[i] = (int)Mathf.Round(Mathf.Lerp(val1.data[i], val2.data[i], t));
                    }
                    destination.attributes[attr.name] = val;
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    // Subdivide

    // Overriding attributes: edge's id
    public static void Subdivide(BMesh mesh)
    {
        int i = 0;
        var edgeCenters = new Vertex[mesh.edges.Count];
        var originalEdges = new Edge[mesh.edges.Count];
        foreach (Edge e in mesh.edges)
        {
            edgeCenters[i] = mesh.AddVertex(e.Center());
            AttributeLerp(mesh, edgeCenters[i], e.vert1, e.vert2, 0.5f);
            originalEdges[i] = e;
            e.id = i++;
        }
        
        var originalFaces = new List<Face>(mesh.faces); // copy because mesh.faces changes during iterations
        foreach (Face f in originalFaces)
        {
            Vertex faceCenter = mesh.AddVertex(f.Center());
            float w = 0;

            // Create one quad per loop in the original face
            Loop it = f.loop;
            do {
                w += 1;
                AttributeLerp(mesh, faceCenter, faceCenter, it.vert, 1 / w);

                var quad = new Vertex[] {
                    it.vert,
                    edgeCenters[it.edge.id],
                    faceCenter,
                    edgeCenters[it.prev.edge.id]
                };
                mesh.AddFace(quad);
                it = it.next;
            } while (it != f.loop);

            // then get rid of the original face
            mesh.RemoveFace(f);
        }

        // Remove old edges
        foreach (Edge e in originalEdges)
        {
            mesh.RemoveEdge(e);
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    // SquarifyQuads

    static Matrix4x4 ComputeLocalAxis(Vector3 r0, Vector3 r1, Vector3 r2, Vector3 r3)
    {
        Vector3 Z = (
                  Vector3.Cross(r0, r1).normalized
                + Vector3.Cross(r1, r2).normalized
                + Vector3.Cross(r2, r3).normalized
                + Vector3.Cross(r3, r0).normalized
            ).normalized;
        Vector3 X = r0.normalized;
        Vector3 Y = Vector3.Cross(Z, X);
        var localToGlobal = new Matrix4x4(X, Y, Z, Vector4.zero);
        return localToGlobal;
    }

    static float AverageRadiusLength(BMesh mesh)
    {
        float lengthsum = 0;
        float weightsum = 0;
        foreach (Face f in mesh.faces)
        {
            Vector3 c = f.Center();
            List<Vertex> verts = f.NeighborVertices();
            if (verts.Count != 4) continue;
            // (r for "radius")
            Vector3 r0 = verts[0].point - c;
            Vector3 r1 = verts[1].point - c;
            Vector3 r2 = verts[2].point - c;
            Vector3 r3 = verts[3].point - c;

            var localToGlobal = ComputeLocalAxis(r0, r1, r2, r3);
            var globalToLocal = localToGlobal.transpose;

            // in local coordinates (l for "local")
            Vector3 l0 = globalToLocal * r0;
            Vector3 l1 = globalToLocal * r1;
            Vector3 l2 = globalToLocal * r2;
            Vector3 l3 = globalToLocal * r3;

            // Rotate vectors (rl for "rotated local")
            Vector3 rl0 = l0;
            Vector3 rl1 = new Vector3(l1.y, -l1.x, l1.z);
            Vector3 rl2 = new Vector3(-l2.x, -l2.y, l2.z);
            Vector3 rl3 = new Vector3(-l3.y, l3.x, l3.z);

            Vector3 average = (rl0 + rl1 + rl2 + rl3) / 4;

            lengthsum += average.magnitude;
            weightsum += 1;
        }
        return lengthsum / weightsum;
    }

    // Try to make quads as square as possible (may be called iteratively)
    // Overriding attributes: vertex's id
    // Optionnaly read attributes: restpos, weight
    public static void SquarifyQuads(BMesh mesh, float rate = 1.0f, bool uniformLength = false)
    {
        float avg = 0;
        if (uniformLength)
        {
            avg = AverageRadiusLength(mesh);
        }

        var pointUpdates = new Vector3[mesh.vertices.Count];
        var weights = new float[mesh.vertices.Count];

        int i = 0;
        foreach (Vertex v in mesh.vertices)
        {
            if (mesh.HasVertexAttribute("restpos"))
            {
                if (mesh.HasVertexAttribute("weight"))
                {
                    weights[i] = (v.attributes["weight"] as FloatAttributeValue).data[0];
                }
                else
                {
                    weights[i] = 1;
                }
                var restpos = (v.attributes["restpos"] as FloatAttributeValue).AsVector3();
                pointUpdates[i] = (restpos - v.point) * weights[i];
                
            } else
            {
                pointUpdates[i] = Vector3.zero;
                weights[i] = 0;
            }
            v.id = i++;
        }

        // Accumulate updates
        foreach (Face f in mesh.faces)
        {
            Vector3 c = f.Center();
            List<Vertex> verts = f.NeighborVertices();
            if (verts.Count != 4) continue;
            // (r for "radius")
            Vector3 r0 = verts[0].point - c;
            Vector3 r1 = verts[1].point - c;
            Vector3 r2 = verts[2].point - c;
            Vector3 r3 = verts[3].point - c;

            var localToGlobal = ComputeLocalAxis(r0, r1, r2, r3);
            var globalToLocal = localToGlobal.transpose;

            // in local coordinates (l for "local")
            Vector3 l0 = globalToLocal * r0;
            Vector3 l1 = globalToLocal * r1;
            Vector3 l2 = globalToLocal * r2;
            Vector3 l3 = globalToLocal * r3;

            bool switch03 = false; 
            if (l1.normalized.y < l3.normalized.y)
            {
                switch03 = true;
                var tmp = l3;
                l3 = l1;
                l1 = tmp;
            }
            // now 0->1->2->3 is in direct trigonometric order

            // Rotate vectors (rl for "rotated local")
            Vector3 rl0 = l0;
            Vector3 rl1 = new Vector3(l1.y, -l1.x, l1.z);
            Vector3 rl2 = new Vector3(-l2.x, -l2.y, l2.z);
            Vector3 rl3 = new Vector3(-l3.y, l3.x, l3.z);

            Vector3 average = (rl0 + rl1 + rl2 + rl3) / 4;
            if (uniformLength)
            {
                average = average.normalized * avg;
            }

            // Rotate back (lt for "local target")
            Vector3 lt0 = average;
            Vector3 lt1 = new Vector3(-average.y, average.x, average.z);
            Vector3 lt2 = new Vector3(-average.x, -average.y, average.z);
            Vector3 lt3 = new Vector3(average.y, -average.x, average.z);

            // Switch back
            if (switch03)
            {
                var tmp = lt3;
                lt3 = lt1;
                lt1 = tmp;
            }

            // Back to global (t for "target")
            Vector3 t0 = localToGlobal * lt0;
            Vector3 t1 = localToGlobal * lt1;
            Vector3 t2 = localToGlobal * lt2;
            Vector3 t3 = localToGlobal * lt3;

            // Accumulate
            pointUpdates[verts[0].id] += t0 - r0;
            pointUpdates[verts[1].id] += t1 - r1;
            pointUpdates[verts[2].id] += t2 - r2;
            pointUpdates[verts[3].id] += t3 - r3;
            weights[verts[0].id] += 1;
            weights[verts[1].id] += 1;
            weights[verts[2].id] += 1;
            weights[verts[3].id] += 1;
        }

        // Apply updates
        i = 0;
        foreach (Vertex v in mesh.vertices)
        {
            if (weights[i] > 0)
            {
                v.point += pointUpdates[i] * (rate / weights[i]);
            }
            ++i;
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    // Merge

    // Overriding attributes: vertex's id
    public static void Merge(BMesh mesh, BMesh other)
    {
        var newVerts = new Vertex[other.vertices.Count];
        int i = 0;
        foreach (Vertex v in other.vertices)
        {
            newVerts[i] = mesh.AddVertex(v.point);
            v.id = i;
            ++i;
        }
        foreach (Edge e in other.edges)
        {
            mesh.AddEdge(newVerts[e.vert1.id], newVerts[e.vert2.id]);
        }
        foreach (Face f in other.faces)
        {
            var neighbors = f.NeighborVertices();
            var newNeighbors = new Vertex[neighbors.Count];
            int j = 0;
            foreach (var v in neighbors)
            {
                newNeighbors[j] = newVerts[v.id];
                ++j;
            }
            mesh.AddFace(newNeighbors);
        }
    }
}
