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
            if (!v1.attributes.ContainsKey(attr.name) || !v2.attributes.ContainsKey(attr.name)) continue;
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
            AttributeLerp(mesh, newVerts[i], v, v, 1); // copy all attributes
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

    ///////////////////////////////////////////////////////////////////////////
    // Nearpoint
    // Find the point for which attribute 'attr' is closest to 'value'
    public static Vertex Nearpoint(BMesh mesh, AttributeValue value, string attrName)
    {
        if (!mesh.HasVertexAttribute(attrName)) return null;
        Vertex argmin = null;
        float min = 0;
        foreach (Vertex v in mesh.vertices)
        {
            float d = AttributeValue.Distance(v.attributes[attrName], value);
            if (argmin == null || d < min)
            {
                argmin = v;
                min = d;
            }
        }
        return argmin;
    }

    ///////////////////////////////////////////////////////////////////////////
    // Marching Cubes
    // read attribute 'occupancyAttr' from 'grid' vertices to get voxel occupancy
    // and use faces as cells to build 'mesh'. Requires grid to contain only quads
    class MarchingCubesOperator // namespace for helper types
    {
        // Permutation of points to put them in canonial form
        class Transform
        {
            public int[] permutation; // permutation from canonical to config

            public Transform(int offset)
            {
                permutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
                for (int i = 0; i < (4 - offset); ++i) RotateZ();
            }

            public Transform(string encoded)
            {
                permutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
                foreach (char c in encoded)
                {
                    switch (c)
                    {
                        case 'x':
                            RotateX();
                            break;
                        case 'y':
                            RotateY();
                            break;
                        case 'z':
                            RotateZ();
                            break;
                    }
                }
            }

            void RotateX()
            {
                permutation = new int[]
                {
                    permutation[3],
                    permutation[2],
                    permutation[6],
                    permutation[7],

                    permutation[0],
                    permutation[1],
                    permutation[5],
                    permutation[4]
                };
            }

            void RotateY()
            {
                permutation = new int[]
                {
                    permutation[4],
                    permutation[0],
                    permutation[3],
                    permutation[7],

                    permutation[5],
                    permutation[1],
                    permutation[2],
                    permutation[6]
                };
            }

            void RotateZ()
            {
                permutation = new int[]
                {
                    permutation[3],
                    permutation[0],
                    permutation[1],
                    permutation[2],

                    permutation[7],
                    permutation[4],
                    permutation[5],
                    permutation[6]
                };
            }

            public int FromCanonical(int index)
            {
                return permutation[index];
            }

            public Vector3 EdgeCenter(int i, int j, Vertex[] verts, Edge[] edges)
            {
                i = FromCanonical(i);
                j = FromCanonical(j);
                if (i / 4 == j / 4)
                {
                    if ((i + 1) % 4 == j % 4)
                    {
                        return edges[i % 4].Center() + Vector3.up * (i / 4);
                    }
                    if ((j + 1) % 4 == i % 4)
                    {
                        return edges[j % 4].Center() + Vector3.up * (j / 4);
                    }
                }
                else if (j == i + 4)
                {
                    return verts[i].point + Vector3.up * 0.5f;
                }
                else if (i == j + 4)
                {
                    return verts[j].point + Vector3.up * 0.5f;
                }
                Debug.Assert(false);
                return Vector3.zero;
            }
        }

        enum Pattern
        {
            None,
            Wall,
            Corner,
            DoubleCorner,
            InnerCorner,
            WallTop,
            CornerTop,
            DoubleCornerTop,
            InnerCornerTop,
        }

        class Configuration
        {
            public Transform transform;
            public Pattern pattern;

            public Configuration(Transform _transform, Pattern _pattern)
            {
                transform = _transform;
                pattern = _pattern;
            }
        }

        static readonly Configuration[] LUT = new Configuration[]
        {
            #region
            new Configuration(new Transform(""), Pattern.None),
            new Configuration(new Transform(""), Pattern.CornerTop),
            new Configuration(new Transform("zzz"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.WallTop),

            new Configuration(new Transform("zz"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzz"), Pattern.WallTop),
            new Configuration(new Transform("z"), Pattern.InnerCornerTop),

            new Configuration(new Transform("z"), Pattern.CornerTop),
            new Configuration(new Transform("z"), Pattern.WallTop),
            new Configuration(new Transform("zzz"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zz"), Pattern.InnerCornerTop),

            new Configuration(new Transform("zz"), Pattern.WallTop),
            new Configuration(new Transform("zzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform(""), Pattern.InnerCornerTop),
            new Configuration(new Transform(""), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(""), Pattern.None),
            new Configuration(new Transform(""), Pattern.Corner),
            new Configuration(new Transform("zzz"), Pattern.Corner),
            new Configuration(new Transform(""), Pattern.Wall),

            new Configuration(new Transform("zz"), Pattern.Corner),
            new Configuration(new Transform(""), Pattern.DoubleCorner),
            new Configuration(new Transform("zzz"), Pattern.Wall),
            new Configuration(new Transform("z"), Pattern.InnerCorner),

            new Configuration(new Transform("z"), Pattern.Corner),
            new Configuration(new Transform("z"), Pattern.Wall),
            new Configuration(new Transform("zzz"), Pattern.DoubleCorner),
            new Configuration(new Transform("zz"), Pattern.InnerCorner),

            new Configuration(new Transform("zz"), Pattern.Wall),
            new Configuration(new Transform("zzz"), Pattern.InnerCorner),
            new Configuration(new Transform(""), Pattern.InnerCorner),
            new Configuration(new Transform(""), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None),
            #endregion
            // --------------------------------------------------------- //
            #region
            new Configuration(new Transform(0), Pattern.None),
            new Configuration(new Transform(0), Pattern.Corner),
            new Configuration(new Transform(1), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.Wall),

            new Configuration(new Transform(2), Pattern.Corner),
            new Configuration(new Transform(0), Pattern.DoubleCorner),
            new Configuration(new Transform(1), Pattern.Wall),
            new Configuration(new Transform(3), Pattern.InnerCorner),

            new Configuration(new Transform(3), Pattern.Corner),
            new Configuration(new Transform(3), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.DoubleCorner),
            new Configuration(new Transform(2), Pattern.InnerCorner),

            new Configuration(new Transform(2), Pattern.Wall),
            new Configuration(new Transform(1), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.InnerCorner),
            new Configuration(new Transform(0), Pattern.None)
            #endregion
            // --------------------------------------------------------- //
        };

        static void JoinEdgeCenters(BMesh mesh, int[] indices, Vertex[] verts, Edge[] edges, Transform transform)
        {
            var newVerts = new Vertex[indices.Length/2];
            for (int i = 0; i < indices.Length/2; ++i)
            {
                newVerts[i] = mesh.AddVertex(transform.EdgeCenter(indices[2 * i + 0], indices[2 * i + 1], verts, edges));
            }
            mesh.AddFace(newVerts);
        }

        public static void Run(BMesh mesh, BMesh grid, string occupancyAttr)
        {
            foreach (var f in grid.faces)
            {
                Debug.Assert(f.vertcount == 4);
                var vertList = f.NeighborVertices();
                var verts = vertList.ToArray();
                var edges = f.NeighborEdges().ToArray();
                var occupancies = vertList.ConvertAll(v => (v.attributes[occupancyAttr] as FloatAttributeValue).data);

                int hash = 0;
                for (int k = 0; k < 4; ++k)
                {
                    float[] o = occupancies[k % 4];
                    int b = o.Length > k / 4 && o[k / 4] > 0 ? 1 : 0;
                    hash += b << k;
                }

                var config = LUT[hash % 16];

                switch (config.pattern)
                {
                    case Pattern.None:
                        break;
                    case Pattern.Wall:
                        {
                            Debug.Log("Adding Wall face...");
                            var indices = new int[] { 3,0,  7,4,  5,6,  1,2 };
                            JoinEdgeCenters(mesh, indices, verts, edges, config.transform);
                            break;
                        }
                    case Pattern.Corner:
                        {
                            Debug.Log("Adding Corner face...");
                            var indices = new int[] { 3,0,  7,4,  4,5,  0,1 };
                            JoinEdgeCenters(mesh, indices, verts, edges, config.transform);
                            break;
                        }
                    case Pattern.DoubleCorner:
                        {
                            int i = config.transform.FromCanonical(0);
                            int j = config.transform.FromCanonical(3);
                            var v0 = mesh.AddVertex(edges[j].Center());
                            var v1 = mesh.AddVertex(edges[i].Center());
                            var v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            var v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            Debug.Log("Adding DoubleCorner faces...");
                            mesh.AddFace(v0, v0p, v1p, v1);

                            i = config.transform.FromCanonical(1);
                            j = config.transform.FromCanonical(2);
                            v0 = mesh.AddVertex(edges[j].Center());
                            v1 = mesh.AddVertex(edges[i].Center());
                            v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            mesh.AddFace(v0, v0p, v1p, v1);
                            break;
                        }
                    case Pattern.InnerCorner:
                        {
                            int i = config.transform.FromCanonical(0);
                            int j = config.transform.FromCanonical(3);
                            var v0 = mesh.AddVertex(edges[j].Center());
                            var v1 = mesh.AddVertex(edges[i].Center());
                            var v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            var v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            Debug.Log("Adding InnerCorner face...");
                            mesh.AddFace(v0, v1, v1p, v0p);
                            break;
                        }
                    case Pattern.WallTop:
                        {
                            Debug.Log("Adding WallTop face...");
                            var indices = new int[] { 3,0,  0,4,  1,5,  1,2 };
                            JoinEdgeCenters(mesh, indices, verts, edges, config.transform);
                            break;
                        }
                    case Pattern.CornerTop:
                        {
                            Debug.Log("Adding CornerTop face...");
                            var indices = new int[] { 3,0,  0,4,  0,1 };
                            JoinEdgeCenters(mesh, indices, verts, edges, config.transform);
                            break;
                        }
                    case Pattern.DoubleCornerTop:
                        {
                            int i = config.transform.FromCanonical(0);
                            int j = config.transform.FromCanonical(3);
                            var v0 = mesh.AddVertex(edges[j].Center());
                            var v1 = mesh.AddVertex(edges[i].Center());
                            var v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            var v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            Debug.Log("Adding DoubleCorner faces...");
                            mesh.AddFace(v0, v0p, v1p, v1);

                            i = config.transform.FromCanonical(1);
                            j = config.transform.FromCanonical(2);
                            v0 = mesh.AddVertex(edges[j].Center());
                            v1 = mesh.AddVertex(edges[i].Center());
                            v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            mesh.AddFace(v0, v0p, v1p, v1);
                            break;
                        }
                    case Pattern.InnerCornerTop:
                        {
                            int i = config.transform.FromCanonical(0);
                            int j = config.transform.FromCanonical(3);
                            var v0 = mesh.AddVertex(edges[j].Center());
                            var v1 = mesh.AddVertex(edges[i].Center());
                            var v0p = mesh.AddVertex(edges[j].Center() + Vector3.up);
                            var v1p = mesh.AddVertex(edges[i].Center() + Vector3.up);
                            Debug.Log("Adding InnerCorner face...");
                            mesh.AddFace(v0, v1, v1p, v0p);
                            break;
                        }
                }
            }
        }
    }

    public static void MarchingCubes(BMesh mesh, BMesh grid, string occupancyAttr)
    {
        MarchingCubesOperator.Run(mesh, grid, occupancyAttr);
    }
}
