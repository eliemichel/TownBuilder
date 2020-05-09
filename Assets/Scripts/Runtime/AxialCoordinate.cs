using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NB: There are two tile axis systems:
//  - local hex coords, using directly AxialCoordinate.Center to translate into global XYZ pos
//  - tile hex coords, to label tiles, which are sets of hexagons with coords in range [-n,n]
// see also https://www.redblobgames.com/grids/hexagons/
public class AxialCoordinate
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

    public override bool Equals(object obj)
    {
        var other = obj as AxialCoordinate;
        return other != null && other.q == q && other.r == r;
    }

    public override int GetHashCode()
    {
        return 486187739 * q + r;
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
        if (q < -n || q > n || r < -n || r > n || q + r < -n || q + r > n)
        {
            return -1; // out of bounds
        }
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
        return new Vector2(q * size * 3 / 2, Mathf.Sqrt(3) * size * (r + q / 2.0f));
    }

    public static AxialCoordinate AtPosition(Vector2 p, float size)
    {
        int q = (int)Mathf.Round(p.x * 2 / (size * 3));
        int r = (int)Mathf.Round(p.y * Mathf.Sqrt(3) / (3 * size) - q / 2.0f);
        return new AxialCoordinate(q, r);
    }

    public Vector2 CenterTileCoord(float size, int divisions)
    {
        float sqrt3 = Mathf.Sqrt(3);
        var tileCoordToWorld = new Matrix4x4(
            new Vector2(3, -sqrt3) / 2 * divisions,
            new Vector2(sqrt3, 3) / 2 * divisions,
            Vector2.zero, Vector2.zero
        );
        return tileCoordToWorld * Center(size);
    }

    public bool InRange(int n)
    {
        return -n <= q && q <= n && -n <= r && r <= n && -n <= q + r && q + r <= n;
    }
}
