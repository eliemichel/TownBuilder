using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BMesh
{
    // Except for members marked as "attribute", all these classes only store references to objects allocated by BMesh itself.
    // attributes has no impact on the mesh, they are only set by calling code, including id
    public class Vertex
    {
        public int id; // [attribute]
        public Vector3 point; // [attribute]
        public Edge edge; // one of the edges using this vertex as origin

        public Vertex(Vector3 _point)
        {
            point = _point;
        }
    }

    public class Edge
    {
        public Vertex vert1;
        public Vertex vert2;
        public Loop loop; // navigate list using radial_next
    }

    public class Loop
    {
        public Vertex vert;
        public Edge edge;
        public Face face;

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
        public int vertcount;
        public Loop loop; // navigate list using next
    }

    public List<Vertex> vertices;
    public List<Loop> loops;
    public List<Edge> edges;
    public List<Face> faces;

    public BMesh()
    {
        vertices = new List<Vertex>();
        loops = new List<Loop>();
        edges = new List<Edge>();
        faces = new List<Face>();
    }

    Edge FindOrientedEdge(Vertex vert1, Vertex vert2)
    {
        if (vert1.edge == null) return null;
        Edge e = vert1.edge;
        do
        {
            if ((e.vert1 == vert1 && e.vert2 == vert2) || (e.vert2 == vert1 && e.vert1 == vert2))
            {
                return e;
            }
            if (e.loop == null) break;
            e = e.loop.radial_next.edge;
        } while (e != vert1.edge);
        return null;
    }

    public Edge FindEdge(Vertex vert1, Vertex vert2)
    {
        Edge e = FindOrientedEdge(vert1, vert2);
        if (e != null) return e;
        return FindOrientedEdge(vert2, vert1);
    }

    public Vertex AddVertex(Vertex vert)
    {
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
        if (vert1.edge == null) vert1.edge = edge;
        return edge;
    }

    public Face AddFace(Vertex[] fVerts)
    {
        if (fVerts.Length == 0) return null;

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

    // Only works with triangular meshes!
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
        int[] triangles = new int[3 * faces.Count];
        i = 0;
        foreach (var f in faces)
        {
            Debug.Assert(f.vertcount == 3);
            var l = f.loop;
            triangles[3 * i + 0] = l.vert.id; l = l.next;
            triangles[3 * i + 1] = l.vert.id; l = l.next;
            triangles[3 * i + 2] = l.vert.id; l = l.next;
            ++i;
        }

        // Apply mesh
        Mesh mesh = new Mesh();
        mf.mesh = mesh;
        mesh.vertices = points;
        mesh.triangles = triangles;
    }
}
