using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatAxialCoordinate
{
    public float q;
    public float r;

    public FloatAxialCoordinate(float _q, float _r)
    {
        q = _q;
        r = _r;
    }

    public virtual Vector2 Center(float size)
    {
        return new Vector2(q * size * 3 / 2, Mathf.Sqrt(3) * size * (r + q / 2.0f));
    }

    public static FloatAxialCoordinate AtPosition(Vector2 p, float size)
    {
        float q = p.x * 2 / (size * 3);
        float r = p.y * Mathf.Sqrt(3) / (3 * size) - q / 2.0f;
        return new FloatAxialCoordinate(q, r);
    }
}
