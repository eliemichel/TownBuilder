using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NB: There are two tile axis systems:
//  - local hex coords, using directly AxialCoordinate.Center to translate into global XYZ pos
//  - tile hex coords, to label tiles, which are sets of hexagons with coords in range [-n,n]
// Tile coords are pointy top and local coords are flat tops,
// according to https://www.redblobgames.com/grids/hexagons/ 's terminology
public class TileAxialCoordinate : AxialCoordinate
{
    int n; // number of divisions of the tile (subtiles use AxialCoordinate)

    public TileAxialCoordinate(int _q, int _r, int _n) : base(_q, _r)
    {
        n = _n;
    }

    static Matrix4x4 TileCoordToWorld(int n)
    {
        float sqrt3 = Mathf.Sqrt(3);
        return new Matrix4x4(
            new Vector2(3, -sqrt3) / 2 * n,
            new Vector2(sqrt3, 3) / 2 * n,
            Vector2.zero, Vector2.zero
        );
    }

    static Matrix4x4 WordCoordToTile(int n)
    {
        float sqrt3 = Mathf.Sqrt(3);
        return (new Matrix4x4(
            new Vector2(1, sqrt3/3) / 2 / n,
            new Vector2(-sqrt3/3, 1) / 2 / n,
            Vector2.zero, Vector2.zero
        ));
    }

    public static TileAxialCoordinate AtPosition(Vector2 p, float size, int n)
    {
        var fco = FloatAxialCoordinate.AtPosition(WordCoordToTile(n) * p, size);
        int q = (int)Mathf.Round(fco.q);
        int r = (int)Mathf.Round(fco.r);
        return new TileAxialCoordinate(q, r, n);
    }

    public override Vector2 Center(float size)
    {
        return TileCoordToWorld(n) * base.Center(size);
    }

    // List of tiles next to TileCoordinate() (tile hex coords) that also contain co (local hex coord)
    public List<TileAxialCoordinate> NeighboringTiles(AxialCoordinate co)
    {
        var neighbors = new List<TileAxialCoordinate>();
        if (co.q == -n)
        {
            neighbors.Add(new TileAxialCoordinate(q - 1, r, n));
        }
        if (co.q == n)
        {
            neighbors.Add(new TileAxialCoordinate(q + 1, r, n));
        }
        if (co.r == -n)
        {
            neighbors.Add(new TileAxialCoordinate(q + 1, r - 1, n));
        }
        if (co.r == n)
        {
            neighbors.Add(new TileAxialCoordinate(q - 1, r + 1, n));
        }
        if (co.r + co.q == -n)
        {
            neighbors.Add(new TileAxialCoordinate(q, r - 1, n));
        }
        if (co.r + co.q == n)
        {
            neighbors.Add(new TileAxialCoordinate(q, r + 1, n));
        }
        return neighbors;
    }

    // float version
    public List<TileAxialCoordinate> NeighboringTiles(FloatAxialCoordinate fco, float epsilon = 1e-6f)
    {
        var neighbors = new List<TileAxialCoordinate>();
        if (Mathf.Abs(fco.q + n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q - 1, r, n));
        }
        if (Mathf.Abs(fco.q - n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q + 1, r, n));
        }
        if (Mathf.Abs(fco.r + n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q + 1, r - 1, n));
        }
        if (Mathf.Abs(fco.r - n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q - 1, r + 1, n));
        }
        if (Mathf.Abs(fco.q + fco.r + n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q, r - 1, n));
        }
        if (Mathf.Abs(fco.q + fco.r - n) < epsilon)
        {
            neighbors.Add(new TileAxialCoordinate(q, r + 1, n));
        }
        return neighbors;
    }

    // Convert coordinate co local to this tile into a coord local to tileCo's tile
    public FloatAxialCoordinate ConvertLocalCoordTo(FloatAxialCoordinate fco, TileAxialCoordinate tileCo)
    {
        float s = 1;
        return FloatAxialCoordinate.AtPosition(fco.Center(s) + Center(s) - tileCo.Center(s), s);
    }
}
