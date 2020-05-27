using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BMesh;

public class ModuleTransform
{
    // After transformation, #i becomes cornerPermutation[#i]
    int[] cornerPermutation; // permutation from canonical to transformed
    int[] facePermutation; // permutation from canonical to transformed
    int[] facePermutationInv; // permutation from transformed to canonical
    public bool flipped = false;
    public bool insideout = false;

    string _encoded; // for debug
    public string Encoded { get { return _encoded; } }
    public override string ToString()
    {
        return "ModuleTransform(" + Encoded + ")";
    }

    public ModuleTransform(int offset, bool mirrorX = false)
    {
        cornerPermutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        facePermutation = new int[] { 0, 1, 2, 3, 4, 5 };
        for (int i = 0; i < offset; ++i) RotateZ();
        if (mirrorX) MirrorX();
        PrecomputeInverse();
    }

    /**
     * Encoded is a string description of the transform using a few caracters.
     * It reads from right to left, so "xy" means "rotate around y then around x"
     * like matrix product does.
     * XYZ are given in Blender like direct Z-up basis
     */
    public ModuleTransform(string encoded)
    {
        cornerPermutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        facePermutation = new int[] { 0, 1, 2, 3, 4, 5 };
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
            case 's':
                MirrorX();
                break;
            case 'f':
                Flip();
                break;
            default:
                Debug.Assert(false);
                break;
            }
        }

        PrecomputeInverse();
    }

    void RotateX()
    {
        cornerPermutation = new int[]
        {
            cornerPermutation[3],
            cornerPermutation[2],
            cornerPermutation[6],
            cornerPermutation[7],

            cornerPermutation[0],
            cornerPermutation[1],
            cornerPermutation[5],
            cornerPermutation[4]
        };

        facePermutation = new int[]
        {
            facePermutation[5],
            facePermutation[1],//
            facePermutation[4],
            facePermutation[3],//
            facePermutation[0],
            facePermutation[2],
        };

        _encoded += "x";
    }

    void RotateY()
    {
        cornerPermutation = new int[]
        {
            cornerPermutation[4],
            cornerPermutation[0],
            cornerPermutation[3],
            cornerPermutation[7],

            cornerPermutation[5],
            cornerPermutation[1],
            cornerPermutation[2],
            cornerPermutation[6]
        };

        facePermutation = new int[]
        {
            facePermutation[0],//
            facePermutation[5],
            facePermutation[2],//
            facePermutation[4],
            facePermutation[1],
            facePermutation[3],
        };

        _encoded += "y";
    }

    void RotateZ()
    {
        cornerPermutation = new int[]
        {
            cornerPermutation[1],
            cornerPermutation[2],
            cornerPermutation[3],
            cornerPermutation[0],

            cornerPermutation[5],
            cornerPermutation[6],
            cornerPermutation[7],
            cornerPermutation[4]
        };

        facePermutation = new int[]
        {
            facePermutation[1],
            facePermutation[2],
            facePermutation[3],
            facePermutation[0],
            facePermutation[4],//
            facePermutation[5],//
        };

        _encoded += "z";
    }

    void MirrorX()
    {
        cornerPermutation = new int[]
        {
            cornerPermutation[1],
            cornerPermutation[0],
            cornerPermutation[3],
            cornerPermutation[2],

            cornerPermutation[5],
            cornerPermutation[4],
            cornerPermutation[7],
            cornerPermutation[6]
        };

        facePermutation = new int[]
        {
            facePermutation[0],//
            facePermutation[3],
            facePermutation[2],//
            facePermutation[1],
            facePermutation[4],//
            facePermutation[5],//
        };

        flipped = !flipped;

        _encoded += "s";
    }

    void Flip()
    {
        flipped = !flipped;
        insideout = !insideout;
        _encoded += "f";
    }

    void PrecomputeInverse()
    {
        facePermutationInv = new int[6];
        for (int i = 0; i < 6; ++i)
        {
            facePermutationInv[facePermutation[i]] = i;
        }
    }

    public int FromCanonical(int index)
    {
        return cornerPermutation[index];
    }

    public int FromCanonicalFace(int index)
    {
        return facePermutation[index];
    }

    public int ToCanonicalFace(int index)
    {
        return facePermutationInv[index];
    }

    // Transform a LUT index such that
    public int TransformHash(int hash)
    {
        int newHash = 0;
        for (int pow = 0; pow < cornerPermutation.Length; ++pow)
        {
            int mask = 1 << pow;
            int newMask = 1 << FromCanonical(pow);
            bool b = (hash & mask) != 0;
            if(b) newHash += newMask;
        }
        return newHash;
    }

    public int InverseTransformHash(int hash)
    {
        int newHash = 0;
        for (int pow = 0; pow < cornerPermutation.Length; ++pow)
        {
            int mask = 1 << FromCanonical(pow);
            int newMask = 1 << pow;
            bool b = (hash & mask) != 0;
            if (b) newHash += newMask;
        }
        return newHash;
    }

    public static Vector3 DefaultEdgeCenter(int i, int j, Vertex[] verts, Edge[] edges)
    {
        int floor_i = i / 4;
        int floor_j = j / 4;
        int sub_i = i % 4;
        int sub_j = j % 4;
        if (floor_i == floor_j)
        {
            if ((sub_i + 1) % 4 == sub_j)
            {
                return edges[sub_i].Center() + Vector3.up * floor_i;
            }
            if ((sub_j + 1) % 4 == sub_i)
            {
                return edges[sub_j].Center() + Vector3.up * floor_j;
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

    public Vector3 EdgeCenter(int i, int j, Vertex[] verts, Edge[] edges)
    {
        i = FromCanonical(i);
        j = FromCanonical(j);
        return DefaultEdgeCenter(i, j, verts, edges);
    }
}
