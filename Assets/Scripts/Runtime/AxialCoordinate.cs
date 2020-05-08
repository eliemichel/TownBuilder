using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// see https://www.redblobgames.com/grids/hexagons/
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

    public bool InRange(int n)
    {
        return -n <= q && q <= n && -n <= r && r <= n && -n <= q + r && q + r <= n;
    }
}
