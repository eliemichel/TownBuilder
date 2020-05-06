using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAxialCoordinate
{
    // Start is called before the first frame update
    public static bool Run(int n)
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

        return true;
    }
}
