using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAxialCoordinate
{
    // Start is called before the first frame update
    static bool TestIndex(int n)
    {
        int i = 0;
        for (int q = -n; q <= n; ++q)
        {
            int start = q <= 0 ? -q - n : -n;
            int end = q <= 0 ? n : n - q;
            for (int r = start; r <= end; ++r)
            {
                var expected = new AxialCoordinate(q, r);
                var found = AxialCoordinate.FromIndex(i, n);
                Debug.Assert(
                    found.Equals(expected),
                    "Test [FromIndex] failed at index " + i.ToString() + ": expected " + expected.ToString() + " but found " + found.ToString()
                );
                int j = expected.ToIndex(n);
                Debug.Assert(
                    j == i,
                    "Test [ToIndex] failed at index " + i.ToString() + ": found " + j.ToString()
                );
                ++i;
            }
        }

        Debug.Log("TestAxialCoordinate TestIndex passed.");
        return true;
    }

    static bool TestTile()
    {
        int n = 4;
        var tile0 = new TileAxialCoordinate(0, 0, n);
        var tile1 = new TileAxialCoordinate(1, 0, n);
        var co1 = new AxialCoordinate(-n, 0);
        var neighbors = tile1.NeighboringTiles(co1);
        Debug.Assert(neighbors.Count == 2, "two neighbours");
        Debug.Assert(neighbors.Contains(tile0), "tile0 is a neighbours");

        neighbors = tile1.NeighboringTiles(new AxialCoordinate(-n+1, 0));
        Debug.Assert(neighbors.Count == 0, "no neighbours for inner subtile");

        Debug.Log("TestAxialCoordinate TestTile passed.");
        return true;
    }

    public static bool Run()
    {
        if (!TestIndex(1)) return false;
        if (!TestIndex(2)) return false;
        if (!TestIndex(4)) return false;
        if (!TestIndex(9)) return false;
        if (!TestTile()) return false;
        Debug.Log("All TestAxialCoordinate passed.");
        return true;
    }
}
