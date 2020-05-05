using Packages.Rider.Editor.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WorldGenerator : MonoBehaviour
{
    public float size = 1;
    public int divisions = 5;
    public bool generate = true;

    void GenerateSimpleHex()
    {
        Vector3[] vertices = new Vector3[7];
        Vector2[] uvs = new Vector2[7];
        int[] triangles = new int[3*6];

        for (int i = 0; i < 6; ++i)
        {
            float th = i * 2 * Mathf.PI / 6;
            float c = Mathf.Cos(th);
            float s = Mathf.Sin(th);
            vertices[i] = new Vector3(size * c, 0, size * s);

            uvs[i] = new Vector2(c * 0.5f + 0.5f, s * 0.5f + 0.5f);

            triangles[3 * i + 0] = (i + 1) % 6;
            triangles[3 * i + 1] = i;
            triangles[3 * i + 2] = 6;
        }
        vertices[6] = new Vector3(0, 0, 0);
        uvs[6] = new Vector2(0.5f, 0.5f);

        // Create mesh
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
    }

    struct AxialCoordinate
    {
        public int q; // x
        public int r; // z

        public AxialCoordinate(int _q, int _r)
        {
            q = _q;
            r = _r;
        }

        public override string ToString()
        {
            return "AxialCoordinate(" + q + " " + r + ")";
        }

        // Returns n such that n(n-1)/2 <= i < n(+1)(n+2)/2
        static int TriFloor(int i)
        {
            return (int)Mathf.Round(Mathf.Sqrt(2 * i + 1)) - 1;
        }

        public static AxialCoordinate FromIndex(int index, int n)
        {
            int l = 3 * (n + 1) * n / 2 + n + 1;
            int s, i0;
            if (index < l)
            {
                i0 = n * (n + 1) / 2 + index;
                s = 1;
            }
            else
            {
                i0 = n * (2 * n + 1) - (index - l + 1);
                s = -1;
            }
            int q0 = TriFloor(i0);
            int r0 = i0 - q0 * (q0 + 1) / 2;
            int q = q0 - 2 * n;
            int r = r0 - n - q;
            return new AxialCoordinate(s * q, s * r);
        }

        public int ToIndex(int n)
        {
            int l = 3 * (n + 1) * n / 2 + n + 1;
            if (q <= 0)
            {
                return l - n - 1 + q * (q + 4 * n + 3) / 2 + r;
            }
            else
            {
                return l + n + (q - 1) * (2 * n + 1) - (q - 1) * q / 2 + r;
            }
        }

        public Vector2 Center(float size)
        {
            return new Vector2(q * size * 3 / 2, Mathf.Sqrt(3) * size  * (r + q / 2.0f));
        }

        public bool InRange(int n)
        {
            return -n <= q && q <= n && -n <= r && r <= n && -n <= q + r && q + r <= n;
        }
    }

    void Test()
    {
        int n = divisions;
        int i = 0;
        for (int q = -n; q <= n; ++q)
        {
            int start = q <= 0 ? -q - n : -n;
            int end = q <= 0 ? n : n - q;
            for (int r = start; r <= end; ++r)
            {
                var expected = new AxialCoordinate(q, r);
                var found = AxialCoordinate.FromIndex(i, n);
                if (!found.Equals(expected))
                {
                    Debug.LogError("Test [FromIndex] failed at index " + i.ToString() + ": expected " + expected.ToString() + " but found " + found.ToString());
                }
                int j = expected.ToIndex(n);
                if (j != i)
                {
                    Debug.LogError("Test [ToIndex] failed at index " + i.ToString() + ": found " + j.ToString());
                }
                ++i;
            }
        }
    }

    void GenerateSubdividedHex()
    {
        int n = divisions;
        int pointcount = (2 * n + 1) * (2 * n + 1) - n * (n + 1);
        int tricount = 6 * n * n;

        Test();

        Vector3[] vertices = new Vector3[pointcount];
        Vector2[] uvs = new Vector2[pointcount];
        int[] triangles = new int[3 * tricount];

        int t = 0;
        for (int i = 0; i < pointcount; ++i)
        {
            var co = AxialCoordinate.FromIndex(i, n);
            Vector2 c = co.Center(size);
            vertices[i] = new Vector3(c.x, 0, c.y);
            uvs[i] = c / (n * Mathf.Sqrt(3) * size) * 0.5f + new Vector2(0.5f, 0.5f);

            var co2 = new AxialCoordinate(co.q + 1, co.r - 1); // right up of co
            var co3 = new AxialCoordinate(co.q + 1, co.r); // beneath co2
            var co4 = new AxialCoordinate(co.q, co.r + 1); // beneath co
            int j = co2.ToIndex(n);

            if (co2.InRange(n) && co3.InRange(n))
            {
                triangles[3 * t + 0] = i;
                triangles[3 * t + 1] = co3.ToIndex(n);
                triangles[3 * t + 2] = co2.ToIndex(n);
                ++t;
            }

            if (co3.InRange(n) && co4.InRange(n))
            {
                triangles[3 * t + 0] = i;
                triangles[3 * t + 1] = co4.ToIndex(n);
                triangles[3 * t + 2] = co3.ToIndex(n);
                ++t;
            }
        }

        if (t != tricount)
        {
            Debug.LogError("Wrong final triangle count: expected " + tricount + " but found " + t);
        }

        // Create mesh
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
    }

    void Generate()
    {
        GenerateSubdividedHex();
    }

    void Update()
    {
        if (generate)
        {
            Generate();
            generate = false;
        }
    }
}
