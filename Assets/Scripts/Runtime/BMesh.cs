using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
public class BMesh
{
    // Except for members marked as "attribute", all these classes only store references to objects allocated by BMesh itself.
    // attributes has no impact on the mesh, they are only set by calling code, including id
    public class Vertex
    {
        public int id; // [attribute]
        public Vector3 point; // [attribute]
        public Dictionary<string,AttributeValue> attributes; // [attribute] (extra attributes)
        public Edge edge; // one of the edges using this vertex as origin, navogates other using edge.next1/next2

        public Vertex(Vector3 _point)
        {
            point = _point;
        }
    }

    public class Edge
    {
        public int id; // [attribute]
        public Vertex vert1;
        public Vertex vert2;
        public Edge next1; // next edge around vert1. If you don't know whether your vertex is vert1 or vert2, use Next(v)
        public Edge next2; // next edge around vert1
        public Edge prev1;
        public Edge prev2;
        public Loop loop; // navigate list using radial_next

        public bool ContainsVertex(Vertex v)
        {
            return v == vert1 || v == vert2;
        }

        public Vertex OtherVertex(Vertex v)
        {
            return v == vert1 ? vert2 : vert1;
        }

        public Edge Next(Vertex v)
        {
            Debug.Assert(ContainsVertex(v));
            return v == vert1 ? next1 : next2;
        }

        public void SetNext(Vertex v, Edge other)
        {
            Debug.Assert(ContainsVertex(v));
            if (v == vert1) next1 = other;
            else next2 = other;
        }

        public Edge Prev(Vertex v)
        {
            Debug.Assert(ContainsVertex(v));
            return v == vert1 ? prev1 : prev2;
        }

        public void SetPrev(Vertex v, Edge other)
        {
            Debug.Assert(ContainsVertex(v));
            if (v == vert1) prev1 = other;
            else prev2 = other;
        }

        public List<Face> NeighborFaces()
        {
            var faces = new List<Face>();
            if (this.loop != null)
            {
                var it = this.loop;
                do
                {
                    faces.Add(it.face);
                    it = it.radial_next;
                } while (it != this.loop);
            }
            return faces;
        }

        public Vector3 Center()
        {
            return (vert1.point + vert2.point) * 0.5f;
        }
    }

    public class Loop
    {
        public Vertex vert;
        public Edge edge;
        public Face face; // there is exactly one face using a loop

        public Loop radial_prev; // around edge
        public Loop radial_next;
        public Loop prev; // around face
        public Loop next;

        public Loop(Vertex v, Edge e, Face f)
        {
            vert = v;
            SetEdge(e);
            SetFace(f);
        }

        public void SetFace(Face f)
        {
            Debug.Assert(this.face == null);
            if (f.loop == null)
            {
                f.loop = this;
                this.next = this.prev = this;
            }
            else
            {
                this.prev = f.loop;
                this.next = f.loop.next;

                f.loop.next.prev = this;
                f.loop.next = this;

                f.loop = this;
            }
            this.face = f;
        }

        public void SetEdge(Edge e)
        {
            Debug.Assert(this.edge == null);
            if (e.loop == null)
            {
                e.loop = this;
                this.radial_next = this.radial_prev = this;
            }
            else
            {
                this.radial_prev = e.loop;
                this.radial_next = e.loop.radial_next;

                e.loop.radial_next.radial_prev = this;
                e.loop.radial_next = this;

                e.loop = this;
            }
            this.edge = e;
        }
    }

    public class Face
    {
        public int id; // [attribute]
        public int vertcount;
        public Loop loop; // navigate list using next

        public List<Vertex> NeighborVertices()
        {
            var verts = new List<Vertex>();
            if (this.loop != null)
            {
                Loop it = this.loop;
                do
                {
                    verts.Add(it.vert);
                    it = it.next;
                } while (it != this.loop);
            }
            return verts;
        }

        public Vector3 Center()
        {
            Vector3 p = Vector3.zero;
            float sum = 0;
            foreach (var v in NeighborVertices())
            {
                p += v.point;
                sum += 1;
            }
            return p / sum;
        }
    }

    public List<Vertex> vertices;
    public List<Edge> edges;
    public List<Loop> loops;
    public List<Face> faces;

    public BMesh()
    {
        vertices = new List<Vertex>();
        loops = new List<Loop>();
        edges = new List<Edge>();
        faces = new List<Face>();

        vertexAttributes = new List<AttributeDefinition>();
        edgeAttributes = new List<AttributeDefinition>();
        loopAttributes = new List<AttributeDefinition>();
        faceAttributes = new List<AttributeDefinition>();
    }

    public Edge FindEdge(Vertex vert1, Vertex vert2)
    {
        Debug.Assert(vert1 != vert2);
        if (vert1.edge == null || vert2.edge == null) return null;

        Edge e1 = vert1.edge;
        Edge e2 = vert2.edge;
        do
        {
            if (e1.ContainsVertex(vert2)) return e1;
            if (e2.ContainsVertex(vert1)) return e2;
            e1 = e1.Next(vert1);
            e2 = e2.Next(vert2);
        } while (e1 != vert1.edge && e2 != vert2.edge);
        return null;
    }

    // removing an edge also removes all associated loops
    public void RemoveEdge(Edge e)
    {
        while (e.loop != null)
        {
            RemoveLoop(e.loop);
        }

        // Remove reference in vertices
        if (e == e.vert1.edge) e.vert1.edge = e.next1 != e ? e.next1 : null;
        if (e == e.vert2.edge) e.vert2.edge = e.next2 != e ? e.next2 : null;

        // Remove from linked lists
        e.prev1.SetNext(e.vert1, e.next1);
        e.next1.SetPrev(e.vert1, e.prev1);

        e.prev2.SetNext(e.vert2, e.next2);
        e.next2.SetPrev(e.vert2, e.prev2);

        edges.Remove(e);
    }

    // removing a loop also removes associated face
    public void RemoveLoop(Loop l)
    {
        if (l.face != null) // null iff loop is called from RemoveFace
        {
            // Trigger removing other loops, and this one again with l.face == null
            RemoveFace(l.face);
            return;
        }

        // remove from radial linked list
        if (l.radial_next == l)
        {
            l.edge.loop = null;
        }
        else
        {
            l.radial_prev.radial_next = l.radial_next;
            l.radial_next.radial_prev = l.radial_prev;
            if (l.edge.loop == l)
            {
                l.edge.loop = l.radial_next;
            }
        }

        // forget other loops of the same face so thet they get released from memory
        l.next = null;
        l.prev = null;

        loops.Remove(l);
    }

    public void RemoveFace(Face f)
    {
        Loop l = f.loop;
        Loop nextL = null;
        while (nextL != f.loop)
        {
            nextL = l.next;
            l.face = null; // prevent infinite recursion, because otherwise RemoveLoop calls RemoveFace
            RemoveLoop(l);
            l = nextL;
        }
        faces.Remove(f);
    }

    void EnsureVertexAttributes(Vertex v)
    {
        if (v.attributes == null) v.attributes = new Dictionary<string, AttributeValue>();
        foreach (var attr in vertexAttributes)
        {
            if (!v.attributes.ContainsKey(attr.name))
            {
                v.attributes[attr.name] = attr.defaultValue;
            }
            else if (!attr.type.CheckValue(v.attributes[attr.name]))
            {
                Debug.LogWarning("Vertex attribute '" + attr.name + "' is not compatible with mesh attribute definition, ignoring.");
                // different type, overriding value with default
                v.attributes[attr.name] = attr.defaultValue;
            }
        }
    }

    public Vertex AddVertex(Vertex vert)
    {
        EnsureVertexAttributes(vert);
        vertices.Add(vert);
        return vert;
    }
    public Vertex AddVertex(Vector3 point)
    {
        return AddVertex(new Vertex(point));
    }

    public Edge AddEdge(Vertex vert1, Vertex vert2)
    {
        Debug.Assert(vert1 != vert2);
        
        var edge = FindEdge(vert1, vert2);
        if (edge != null) return edge;

        edge = new Edge
        {
            vert1 = vert1,
            vert2 = vert2
        };
        edges.Add(edge);

        // Insert in vert1's edge list
        if (vert1.edge == null)
        {
            vert1.edge = edge;
            edge.next1 = edge.prev1 = edge;
        }
        else
        {
            edge.next1 = vert1.edge.Next(vert1);
            edge.prev1 = vert1.edge;
            edge.next1.SetPrev(vert1, edge);
            edge.prev1.SetNext(vert1, edge);
        }

        // Same for vert2 -- TODO avoid code duplication
        if (vert2.edge == null)
        {
            vert2.edge = edge;
            edge.next2 = edge.prev2 = edge;
        }
        else
        {
            edge.next2 = vert2.edge.Next(vert2);
            edge.prev2 = vert2.edge;
            edge.next2.SetPrev(vert2, edge);
            edge.prev2.SetNext(vert2, edge);
        }

        return edge;
    }

    public Face AddFace(Vertex[] fVerts)
    {
        if (fVerts.Length == 0) return null;
        foreach (var v in fVerts) Debug.Assert(v != null);

        var fEdges = new Edge[fVerts.Length];

        int i, i_prev = fVerts.Length - 1;
        for (i = 0; i < fVerts.Length; ++i)
        {
            fEdges[i_prev] = AddEdge(fVerts[i_prev], fVerts[i]);
            i_prev = i;
        }

        var f = new Face();
        faces.Add(f);

        for (i = 0; i < fVerts.Length; ++i)
        {
            Loop loop = new Loop(fVerts[i], fEdges[i], f);
            loops.Add(loop);
        }

        f.vertcount = fVerts.Length;
        return f;
    }

    public Face AddFace(Vertex v0, Vertex v1, Vertex v2)
    {
        return AddFace(new Vertex[] { v0, v1, v2 });
    }

    public Face AddFace(Vertex v0, Vertex v1, Vertex v2, Vertex v3)
    {
        return AddFace(new Vertex[] { v0, v1, v2, v3 });
    }

    public Face AddFace(int i0, int i1, int i2)
    {
        return AddFace(new Vertex[] { vertices[i0], vertices[i1], vertices[i2] });
    }

    public Face AddFace(int i0, int i1, int i2, int i3)
    {
        return AddFace(new Vertex[] { vertices[i0], vertices[i1], vertices[i2], vertices[i3] });
    }

    // Only works with tri or quad meshes!
    public void SetInMeshFilter(MeshFilter mf)
    {
        // Points
        Vector3[] points = new Vector3[vertices.Count];
        int i = 0;
        foreach (var vert in vertices)
        {
            vert.id = i;
            points[i] = vert.point;
            ++i;
        }

        // Triangles
        int tricount = 0;
        foreach (var f in faces)
        {
            Debug.Assert(f.vertcount == 3 || f.vertcount == 4);
            tricount += f.vertcount - 2;
        }
        int[] triangles = new int[3 * tricount];
        i = 0;
        foreach (var f in faces)
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
        Mesh mesh = new Mesh();
        mf.mesh = mesh;
        mesh.vertices = points;
        mesh.triangles = triangles;
    }

    ///////////////////////////////////////////////////////////////////////////
    #region [Attributes]

    public enum AttributeBaseType
    {
        Int,
        Float,
    }
    public class AttributeType
    {
        public AttributeBaseType baseType;
        public int dimensions;

        public bool CheckValue(AttributeValue value)
        {
            Debug.Assert(dimensions > 0);
            switch (baseType)
            {
                case AttributeBaseType.Int:
                {
                    var valueAsInt = value as IntAttributeValue;
                    return valueAsInt != null && valueAsInt.data.Length == dimensions;
                }
                case AttributeBaseType.Float:
                {
                    var valueAsFloat = value as FloatAttributeValue;
                    return valueAsFloat != null && valueAsFloat.data.Length == dimensions;
                }
                default:
                    Debug.Assert(false);
                    return false;
            }
        }
    }

    public class AttributeValue
    {}
    public class IntAttributeValue : AttributeValue
    {
        public int[] data;

        public IntAttributeValue() { }
        public IntAttributeValue(int i)
        {
            data = new int[] { i };
        }
        public IntAttributeValue(int i0, int i1)
        {
            data = new int[] { i0, i1 };
        }
    }
    public class FloatAttributeValue : AttributeValue
    {
        public float[] data;

        public FloatAttributeValue() { }
        public FloatAttributeValue(int f)
        {
            data = new float[] { f };
        }
        public FloatAttributeValue(int f0, int f1)
        {
            data = new float[] { f0, f1 };
        }
        public FloatAttributeValue(Vector3 v)
        {
            data = new float[] { v.x, v.y, v.z };
        }
        public Vector3 AsVector3()
        {
            return new Vector3(
                data.Length >= 0 ? data[0] : 0,
                data.Length >= 1 ? data[1] : 0,
                data.Length >= 2 ? data[2] : 0
            );
        }
    }

    public class AttributeDefinition
    {
        public string name;
        public AttributeType type;
        public AttributeValue defaultValue;

        public AttributeDefinition(string name, AttributeBaseType baseType, int dimensions)
        {
            this.name = name;
            type = new AttributeType { baseType = baseType, dimensions = dimensions };
            defaultValue = NullValue();
        }

        public AttributeValue NullValue()
        {
            Debug.Assert(type.dimensions > 0);
            switch (type.baseType)
            {
                case AttributeBaseType.Int:
                    return new IntAttributeValue { data = new int[type.dimensions] };
                case AttributeBaseType.Float:
                    return new FloatAttributeValue { data = new float[type.dimensions] };
                default:
                    Debug.Assert(false);
                    return new AttributeValue();
            }
        }
    }

    public List<AttributeDefinition> vertexAttributes;
    public List<AttributeDefinition> edgeAttributes;
    public List<AttributeDefinition> loopAttributes;
    public List<AttributeDefinition> faceAttributes;

    public bool HasVertexAttribute(string attribName)
    {
        foreach (var a in vertexAttributes)
        {
            if (a.name == attribName)
            {
                return true;
            }
        }
        return false;
    }

    public bool HasVertexAttribute(AttributeDefinition attrib)
    {
        return HasVertexAttribute(attrib.name);
    }

    public void AddVertexAttribute(AttributeDefinition attrib)
    {
        if (HasVertexAttribute(attrib)) return;
        vertexAttributes.Add(attrib);
        foreach (Vertex v in vertices)
        {
            if (v.attributes == null) v.attributes = new Dictionary<string, AttributeValue>(); // move in Vertex ctor?
            v.attributes[attrib.name] = attrib.defaultValue;
        }
    }

    #endregion
}
