///////////////////////////////////////////////////////////////////////////
// Marching Cubes
// read attribute 'occupancyAttr' from 'grid' vertices to get voxel occupancy
// and use faces as cells to build 'mesh'. Requires grid to contain only quads
using UnityEngine;
using static BMesh;

public class ModuleBasedMarchingCubes
{
    // Permutation of points to put them in canonial form
    public class Transform
    {
        public int[] permutation; // permutation from canonical to config
        public bool flipped = false;
        public bool insideout = false;

        public Transform(int offset)
        {
            permutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            for (int i = 0; i < (4 - offset); ++i) RotateZ();
        }

        public Transform(string encoded)
        {
            permutation = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
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
        }

        void RotateX()
        {
            permutation = new int[]
            {
                    permutation[3],
                    permutation[2],
                    permutation[6],
                    permutation[7],

                    permutation[0],
                    permutation[1],
                    permutation[5],
                    permutation[4]
            };
        }

        void RotateY()
        {
            permutation = new int[]
            {
                    permutation[4],
                    permutation[0],
                    permutation[3],
                    permutation[7],

                    permutation[5],
                    permutation[1],
                    permutation[2],
                    permutation[6]
            };
        }

        void RotateZ()
        {
            permutation = new int[]
            {
                    permutation[3],
                    permutation[0],
                    permutation[1],
                    permutation[2],

                    permutation[7],
                    permutation[4],
                    permutation[5],
                    permutation[6]
            };
        }

        void MirrorX()
        {
            permutation = new int[]
            {
                    permutation[1],
                    permutation[0],
                    permutation[3],
                    permutation[2],

                    permutation[5],
                    permutation[4],
                    permutation[7],
                    permutation[6]
            };
            flipped = !flipped;
        }

        void Flip()
        {
            flipped = !flipped;
            insideout = !insideout;
        }

        public int FromCanonical(int index)
        {
            return permutation[index];
        }

        // Transform a LUT index such that
        public int TransformHash(int hash)
        {
            int newHash = 0;
            for (int pow = 0; pow < permutation.Length; ++pow)
            {
                int mask = 1 << pow;
                int newMask = 1 << permutation[pow];
                bool b = (hash & mask) != 0;
                newHash += b ? newMask : 0;
            }
            return newHash;
        }

        public static Vector3 DefaultEdgeCenter(int i, int j, Vertex[] verts, Edge[] edges)
        {
            if (i / 4 == j / 4)
            {
                if ((i + 1) % 4 == j % 4)
                {
                    return edges[i % 4].Center() + Vector3.up * (i / 4);
                }
                if ((j + 1) % 4 == i % 4)
                {
                    return edges[j % 4].Center() + Vector3.up * (j / 4);
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

    enum Pattern
    {
        None,
        Wall,
        Corner,
        DoubleCorner,
        InnerCorner,
        WallTop,
        CornerTop,
        DoubleCornerTop,
        InnerCornerTop,
        Roof,
        InnerCornerTopVar,
        CrossedCorner,
        TowerCorner,
        WingCorner,
        OppositeCorner,
        WallTopVar,
        TripleCorner,
        TowerRoof
    }

    class Configuration
    {
        public Transform transform;
        public Pattern pattern;

        public Configuration(Transform _transform, Pattern _pattern)
        {
            transform = _transform;
            pattern = _pattern;
        }
    }

    static readonly Configuration[] LUT = new Configuration[]
    {
            #region top = [0 0 0 0] OK
            new Configuration(new Transform(""), Pattern.None),
            new Configuration(new Transform(""), Pattern.CornerTop),
            new Configuration(new Transform("zzz"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.WallTop),

            new Configuration(new Transform("zz"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzz"), Pattern.WallTop),
            new Configuration(new Transform("z"), Pattern.InnerCornerTop),

            new Configuration(new Transform("z"), Pattern.CornerTop),
            new Configuration(new Transform("z"), Pattern.WallTop),
            new Configuration(new Transform("zzz"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zz"), Pattern.InnerCornerTop),

            new Configuration(new Transform("zz"), Pattern.WallTop),
            new Configuration(new Transform("zzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform(""), Pattern.InnerCornerTop),
            new Configuration(new Transform(""), Pattern.Roof),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 0 0 0] OK
            new Configuration(new Transform("xxx"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.Corner),
            new Configuration(new Transform("xxx"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("xxxzzz"), Pattern.InnerCornerTop),

            new Configuration(new Transform("zz"), Pattern.OppositeCorner),
            new Configuration(new Transform("y"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzs"), Pattern.WallTopVar),
            new Configuration(new Transform("szz"), Pattern.WingCorner),

            new Configuration(new Transform("y"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("yz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("z"), Pattern.TripleCorner),
            new Configuration(new Transform("zz"), Pattern.TowerCorner),

            new Configuration(new Transform("zz"), Pattern.WallTopVar),
            new Configuration(new Transform("zzz"), Pattern.WingCorner),
            new Configuration(new Transform(""), Pattern.InnerCornerTopVar),
            new Configuration(new Transform(""), Pattern.TowerRoof),
            #endregion
            // --------------------------------------------------------- //
            #region top = [0 1 0 0] OK
            new Configuration(new Transform("zzzxxx"), Pattern.CornerTop),
            new Configuration(new Transform("zzzy"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzz"), Pattern.Corner),
            new Configuration(new Transform("zzzyz"), Pattern.InnerCornerTop),

            new Configuration(new Transform("zzzxxx"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzzz"), Pattern.TripleCorner),
            new Configuration(new Transform("zzzxxxzzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzzz"), Pattern.TowerCorner),

            new Configuration(new Transform("zzzzz"), Pattern.OppositeCorner),
            new Configuration(new Transform("zzzzz"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzy"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzzzz"), Pattern.WingCorner),

            new Configuration(new Transform("zzzzzzs"), Pattern.WallTopVar),
            new Configuration(new Transform("zzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzszz"), Pattern.WingCorner),
            new Configuration(new Transform("zzz"), Pattern.TowerRoof),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 1 0 0] OK
            new Configuration(new Transform("yy"), Pattern.WallTop),
            new Configuration(new Transform("xxxzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("xxxz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("xxx"), Pattern.Roof),

            new Configuration(new Transform("xxx"), Pattern.WallTopVar),
            new Configuration(new Transform("xxxzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("xxxz"), Pattern.WingCorner),
            new Configuration(new Transform("xxxzz"), Pattern.TowerRoof),

            new Configuration(new Transform("xxxs"), Pattern.WallTopVar),
            new Configuration(new Transform("yzzs"), Pattern.WingCorner),
            new Configuration(new Transform("xxxz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("xxxz"), Pattern.TowerRoof),

            new Configuration(new Transform("y"), Pattern.DoubleCorner),
            new Configuration(new Transform("xxf"), Pattern.WallTopVar),
            new Configuration(new Transform("xxsf"), Pattern.WallTopVar),
            new Configuration(new Transform("xxf"), Pattern.WallTop),
            #endregion

            // --------------------------------------------------------- //
            #region top = [0 0 1 0] OK
            new Configuration(new Transform("zzxxx"), Pattern.CornerTop),
            new Configuration(new Transform("zzzz"), Pattern.OppositeCorner),
            new Configuration(new Transform("zzy"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzzz"), Pattern.WallTopVar),

            new Configuration(new Transform("zz"), Pattern.Corner),
            new Configuration(new Transform("zzy"), Pattern.WallTopVar),
            new Configuration(new Transform("zzyz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzzz"), Pattern.WingCorner),

            new Configuration(new Transform("zzxxx"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzzzzs"), Pattern.WallTopVar),
            new Configuration(new Transform("zzz"), Pattern.TripleCorner),
            new Configuration(new Transform("zz"), Pattern.InnerCornerTopVar),

            new Configuration(new Transform("zzxxxzzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzszz"), Pattern.WingCorner),
            new Configuration(new Transform("zzzz"), Pattern.TowerCorner),
            new Configuration(new Transform("zz"), Pattern.TowerRoof),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 0 1 0] OK
            new Configuration(new Transform("xxz"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("ys"), Pattern.WallTopVar),
            new Configuration(new Transform("yyz"), Pattern.TripleCorner),
            new Configuration(new Transform("xxxzzz"), Pattern.InnerCornerTopVar),

            new Configuration(new Transform("xzzz"), Pattern.WallTopVar),
            new Configuration(new Transform(""), Pattern.DoubleCorner),
            new Configuration(new Transform("yyyzzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("xzsf"), Pattern.WallTopVar),

            new Configuration(new Transform("xzz"), Pattern.TripleCorner),
            new Configuration(new Transform("yz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("z"), Pattern.CrossedCorner),
            new Configuration(new Transform("xzzzf"), Pattern.TripleCorner),

            new Configuration(new Transform("xz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("yyyf"), Pattern.WallTopVar),
            new Configuration(new Transform("xxzzf"), Pattern.TripleCorner),
            new Configuration(new Transform("xxf"), Pattern.DoubleCornerTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [0 1 1 0] OK
            new Configuration(new Transform("zzzyy"), Pattern.WallTop),
            new Configuration(new Transform("zzzxxxs"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzxxxzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzyzzs"), Pattern.WingCorner),

            new Configuration(new Transform("zzzxxxz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzxxxz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzxxx"), Pattern.Roof),
            new Configuration(new Transform("zzzxxxz"), Pattern.TowerRoof),

            new Configuration(new Transform("zzzxxx"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzy"), Pattern.DoubleCorner),
            new Configuration(new Transform("zzzxxxzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzxxf"), Pattern.WallTopVar),

            new Configuration(new Transform("zzzxxxz"), Pattern.WingCorner),
            new Configuration(new Transform("zzzxxsf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzxxxzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzzxxf"), Pattern.WallTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 1 1 0] OK
            new Configuration(new Transform("xx"), Pattern.InnerCornerTop),
            new Configuration(new Transform("xx"), Pattern.WingCorner),
            new Configuration(new Transform("xx"), Pattern.TowerCorner),
            new Configuration(new Transform("xxxzzz"), Pattern.TowerRoof),

            new Configuration(new Transform("xxzs"), Pattern.WingCorner),
            new Configuration(new Transform("xzf"), Pattern.WallTopVar),
            new Configuration(new Transform("yyyzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zf"), Pattern.Corner),

            new Configuration(new Transform("xx"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzf"), Pattern.TripleCorner),
            new Configuration(new Transform("xzf"), Pattern.DoubleCornerTop),

            new Configuration(new Transform("sf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzf"), Pattern.OppositeCorner),
            new Configuration(new Transform("yzf"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("yzf"), Pattern.CornerTop),
            #endregion

            // --------------------------------------------------------- //
            #region top = [0 0 0 1] OK
            new Configuration(new Transform("zxxx"), Pattern.CornerTop),
            new Configuration(new Transform("zxxx"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzz"), Pattern.OppositeCorner),
            new Configuration(new Transform("zzzzs"), Pattern.WallTopVar),

            new Configuration(new Transform("zy"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zz"), Pattern.TripleCorner),
            new Configuration(new Transform("zzz"), Pattern.WallTopVar),
            new Configuration(new Transform("z"), Pattern.InnerCornerTopVar),

            new Configuration(new Transform("z"), Pattern.Corner),
            new Configuration(new Transform("zxxxzzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zy"), Pattern.WallTopVar),
            new Configuration(new Transform("zszz"), Pattern.WingCorner),

            new Configuration(new Transform("zyz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzz"), Pattern.TowerCorner),
            new Configuration(new Transform("zzzz"), Pattern.WingCorner),
            new Configuration(new Transform("z"), Pattern.TowerRoof),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 0 0 1] OK
            new Configuration(new Transform("zyy"), Pattern.WallTop),
            new Configuration(new Transform("zxxxz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zxxx"), Pattern.WallTopVar),
            new Configuration(new Transform("zxxxz"), Pattern.WingCorner),

            new Configuration(new Transform("zxxxs"), Pattern.WallTopVar),
            new Configuration(new Transform("zxxxz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zy"), Pattern.DoubleCorner),
            new Configuration(new Transform("zxxsf"), Pattern.WallTopVar),

            new Configuration(new Transform("zxxxzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zxxx"), Pattern.Roof),
            new Configuration(new Transform("zxxxzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zxxxzz"), Pattern.TowerRoof),

            new Configuration(new Transform("zyzzs"), Pattern.WingCorner),
            new Configuration(new Transform("zxxxz"), Pattern.TowerRoof),
            new Configuration(new Transform("zxxf"), Pattern.WallTopVar),
            new Configuration(new Transform("zxxf"), Pattern.WallTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [0 1 0 1] OK
            new Configuration(new Transform("zxxz"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zyyz"), Pattern.TripleCorner),
            new Configuration(new Transform("zxzzz"), Pattern.WallTopVar),
            new Configuration(new Transform("zyyyzzz"), Pattern.InnerCornerTopVar),

            new Configuration(new Transform("zxzz"), Pattern.TripleCorner),
            new Configuration(new Transform("zz"), Pattern.CrossedCorner),
            new Configuration(new Transform("zxz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zxxzzf"), Pattern.TripleCorner),

            new Configuration(new Transform("zys"), Pattern.WallTopVar),
            new Configuration(new Transform("zxxxzzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("z"), Pattern.DoubleCorner),
            new Configuration(new Transform("zxzsf"), Pattern.WallTopVar),

            new Configuration(new Transform("zyz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zxzzzf"), Pattern.TripleCorner),
            new Configuration(new Transform("zyyyf"), Pattern.WallTopVar),
            new Configuration(new Transform("zxxf"), Pattern.DoubleCornerTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 1 0 1] OK
            new Configuration(new Transform("zxx"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zxx"), Pattern.TowerCorner),
            new Configuration(new Transform("zxxzs"), Pattern.WingCorner),
            new Configuration(new Transform("zyyyzzz"), Pattern.TowerRoof),

            new Configuration(new Transform("zxx"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzf"), Pattern.TripleCorner),
            new Configuration(new Transform("zsf"), Pattern.WallTopVar),
            new Configuration(new Transform("zyzf"), Pattern.DoubleCornerTop),

            new Configuration(new Transform("zxx"), Pattern.WingCorner),
            new Configuration(new Transform("zxxxzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zxzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzf"), Pattern.Corner),

            new Configuration(new Transform("zzzzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zxzf"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzzzf"), Pattern.OppositeCorner),
            new Configuration(new Transform("zyzf"), Pattern.CornerTop),
            #endregion

            // --------------------------------------------------------- //
            #region top = [0 0 1 1] OK
            new Configuration(new Transform("zzyy"), Pattern.WallTop),
            new Configuration(new Transform("zzxxx"), Pattern.WallTopVar),
            new Configuration(new Transform("zzxxxs"), Pattern.WallTopVar),
            new Configuration(new Transform("zzy"), Pattern.DoubleCorner),

            new Configuration(new Transform("zzxxxzz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzxxxzz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzyzzs"), Pattern.WingCorner),
            new Configuration(new Transform("zzxxf"), Pattern.WallTopVar),

            new Configuration(new Transform("zzxxxz"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzxxxz"), Pattern.WingCorner),
            new Configuration(new Transform("zzxxxz"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzxxsf"), Pattern.WallTopVar),

            new Configuration(new Transform("zzxxx"), Pattern.Roof),
            new Configuration(new Transform("zzxxxzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzxxxz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzxxf"), Pattern.WallTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 0 1 1] OK
            new Configuration(new Transform("zzxx"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzxxzs"), Pattern.WingCorner),
            new Configuration(new Transform("zzxx"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzsf"), Pattern.WallTopVar),

            new Configuration(new Transform("zzxx"), Pattern.WingCorner),
            new Configuration(new Transform("zzxzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzzzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzzzf"), Pattern.OppositeCorner),

            new Configuration(new Transform("zzxx"), Pattern.TowerCorner),
            new Configuration(new Transform("zzyyyzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzzzf"), Pattern.TripleCorner),
            new Configuration(new Transform("zzyzf"), Pattern.DoubleCornerTop),

            new Configuration(new Transform("zzxxxzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzzf"), Pattern.Corner),
            new Configuration(new Transform("zzxzf"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzyzf"), Pattern.CornerTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [0 1 1 1] OK
            new Configuration(new Transform("zzzxx"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzxx"), Pattern.InnerCornerTopVar),
            new Configuration(new Transform("zzzxx"), Pattern.WingCorner),
            new Configuration(new Transform("zzzzzzf"), Pattern.WallTopVar),

            new Configuration(new Transform("zzzxx"), Pattern.TowerCorner),
            new Configuration(new Transform("zzzzzf"), Pattern.TripleCorner),
            new Configuration(new Transform("zzzxxxzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzzxzf"), Pattern.DoubleCornerTop),

            new Configuration(new Transform("zzzxxzs"), Pattern.WingCorner),
            new Configuration(new Transform("zzzsf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzxzf"), Pattern.WallTopVar),
            new Configuration(new Transform("zzzzzzf"), Pattern.OppositeCorner),

            new Configuration(new Transform("zzzyyyzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzzyzf"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzzzf"), Pattern.Corner),
            new Configuration(new Transform("zzzyzf"), Pattern.CornerTop),
            #endregion
            // --------------------------------------------------------- //
            #region top = [1 1 1 1] OK
            new Configuration(new Transform("xx"), Pattern.Roof),
            new Configuration(new Transform("xxz"), Pattern.TowerRoof),
            new Configuration(new Transform("xxzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zzf"), Pattern.WallTop),

            new Configuration(new Transform("xxzzz"), Pattern.TowerRoof),
            new Configuration(new Transform("zf"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zf"), Pattern.WallTop),
            new Configuration(new Transform("zf"), Pattern.CornerTop),

            new Configuration(new Transform("zf"), Pattern.InnerCornerTop),
            new Configuration(new Transform("zzzf"), Pattern.WallTop),
            new Configuration(new Transform("f"), Pattern.DoubleCornerTop),
            new Configuration(new Transform("zzf"), Pattern.CornerTop),

            new Configuration(new Transform("f"), Pattern.WallTop),
            new Configuration(new Transform("zzzf"), Pattern.CornerTop),
            new Configuration(new Transform("f"), Pattern.CornerTop),
            new Configuration(new Transform(""), Pattern.None)
        #endregion
        // --------------------------------------------------------- //
    };

    static int[] PatternOccupancy(Pattern pattern)
    {
        switch (pattern)
        {
            case Pattern.None:
                return new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            case Pattern.Wall:
                return new int[] { 1, 1, 0, 0, 1, 1, 0, 0 };
            case Pattern.Corner:
                return new int[] { 1, 0, 0, 0, 1, 0, 0, 0 };
            case Pattern.DoubleCorner:
                return new int[] { 1, 0, 1, 0, 1, 0, 1, 0 };
            case Pattern.InnerCorner:
                return new int[] { 0, 1, 1, 1, 0, 1, 1, 1 };
            case Pattern.WallTop:
                return new int[] { 1, 1, 0, 0, 0, 0, 0, 0 };
            case Pattern.CornerTop:
                return new int[] { 1, 0, 0, 0, 0, 0, 0, 0 };
            case Pattern.DoubleCornerTop:
                return new int[] { 1, 0, 1, 0, 0, 0, 0, 0 };
            case Pattern.InnerCornerTop:
                return new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            case Pattern.Roof:
                return new int[] { 1, 1, 1, 1, 0, 0, 0, 0 };
            case Pattern.InnerCornerTopVar:
                return new int[] { 0, 1, 1, 1, 1, 0, 0, 0 };
            case Pattern.CrossedCorner:
                return new int[] { 1, 0, 1, 0, 0, 1, 0, 1 };
            case Pattern.TowerCorner:
                return new int[] { 0, 1, 1, 1, 0, 0, 1, 0 };
            case Pattern.WingCorner:
                return new int[] { 0, 1, 1, 1, 0, 0, 0, 1 };
            case Pattern.OppositeCorner:
                return new int[] { 1, 0, 0, 0, 0, 0, 1, 0 };
            case Pattern.WallTopVar:
                return new int[] { 1, 1, 0, 0, 0, 0, 1, 0 };
            case Pattern.TripleCorner:
                return new int[] { 1, 0, 1, 0, 0, 1, 0, 0 };
            case Pattern.TowerRoof:
                return new int[] { 1, 1, 1, 1, 1, 0, 0, 0 };
        }
        Debug.Assert(false);
        return null;
    }

    public static bool Test()
    {
        Debug.Assert(LUT.Length == 256, "lut size");
        for (int i = 0; i < 256; ++i)
        {
            var config = LUT[i];
            var occupancy = PatternOccupancy(config.pattern);
            int checksum = 0;
            for (int k = 0; k < 8; ++k)
            {
                int o = occupancy[k];
                if (config.transform.insideout) o = 1 - o;
                checksum += o << config.transform.FromCanonical(k);
            }
            Debug.Assert(checksum == i % 255, "occupancy does no match for LUT entry #" + i + " (found " + checksum + ")");
        }

        return true;
    }

    static void JoinEdgeCenters(BMesh mesh, int[] indices, Vertex[] verts, Edge[] edges, Transform transform, int floor)
    {
        Vector3 floorOffset = floor * Vector3.up;
        var newVerts = new Vertex[indices.Length / 2];
        for (int i = 0; i < indices.Length / 2; ++i)
        {
            newVerts[i] = mesh.AddVertex(transform.EdgeCenter(indices[2 * i + 0], indices[2 * i + 1], verts, edges) + floorOffset);
        }
        if (transform.flipped)
        {
            System.Array.Reverse(newVerts);
        }
        mesh.AddFace(newVerts);
    }

    static void AddLegacyBlock(int hash, BMesh mesh, Vertex[] verts, Edge[] edges, int floor)
    {
        var config = LUT[hash];

        switch (config.pattern)
        {
            case Pattern.None:
                break;
            case Pattern.Wall:
                {
                    //Debug.Log("Adding Wall face...");
                    var indices = new int[] { 3, 0, 7, 4, 5, 6, 1, 2 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.Corner:
                {
                    //Debug.Log("Adding Corner face...");
                    var indices = new int[] { 3, 0, 7, 4, 4, 5, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.DoubleCorner:
                {
                    //Debug.Log("Adding DoubleCorner faces...");
                    var indices = new int[] { 3, 0, 7, 4, 4, 5, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);

                    indices = new int[] { 1, 2, 5, 6, 6, 7, 2, 3 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.InnerCorner:
                {
                    //Debug.Log("Adding InnerCorner face...");
                    var indices = new int[] { 0, 1, 4, 5, 7, 4, 3, 0 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.WallTop:
                {
                    //Debug.Log("Adding WallTop face...");
                    var indices = new int[] { 3, 0, 0, 4, 1, 5, 1, 2 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.CornerTop:
                {
                    //Debug.Log("Adding CornerTop face...");
                    var indices = new int[] { 3, 0, 0, 4, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.DoubleCornerTop:
                {
                    //Debug.Log("Adding DoubleCornerTop faces...");
                    var indices = new int[] { 3, 0, 0, 4, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 1, 2, 2, 6, 2, 3 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.InnerCornerTop:
                {
                    //Debug.Log("Adding InnerCornerTop faces...");
                    var indices = new int[] { 3, 0, 0, 1, 2, 6 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 0, 1, 1, 5, 2, 6 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 3, 0, 2, 6, 3, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.Roof:
                {
                    //Debug.Log("Adding Roof face...");
                    var indices = new int[] { 0, 4, 1, 5, 2, 6, 3, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.InnerCornerTopVar:
                {
                    //Debug.Log("Adding InnerCornerTopVar face...");
                    var indices = new int[] { 3, 0, 0, 1, 2, 6 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 0, 1, 1, 5, 2, 6 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 3, 0, 2, 6, 3, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 4, 5, 0, 4, 7, 4 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.CrossedCorner:
                {
                    //Debug.Log("Adding CorssedCorner face...");
                    var indices = new int[] { 3, 0, 0, 4, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 1, 2, 2, 6, 2, 3 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 4, 5, 5, 6, 1, 5 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 6, 7, 7, 4, 3, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.TowerCorner:
                {
                    //Debug.Log("Adding TowerCorner face...");
                    var indices = new int[] { 0, 1, 1, 5, 3, 7, 3, 0 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 3, 7, 1, 5, 5, 6, 6, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.WingCorner:
                {
                    //Debug.Log("Adding WingCorner face...");
                    var indices = new int[] { 1, 5, 2, 6, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 0, 1, 2, 6, 3, 0 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 3, 0, 2, 6, 6, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 3, 0, 6, 7, 7, 4 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.OppositeCorner:
                {
                    //Debug.Log("Adding OppositeCorner face...");
                    var indices = new int[] { 3, 0, 0, 4, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 5, 6, 6, 7, 2, 6 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.WallTopVar:
                {
                    //Debug.Log("Adding WallTopVar face...");
                    var indices = new int[] { 3, 0, 0, 4, 1, 5, 1, 2 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 5, 6, 6, 7, 2, 6 };
                    //indices = new int[] { 6,7,  7,4,  3,7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.TripleCorner:
                {
                    //Debug.Log("Adding TripleCorner face...");
                    var indices = new int[] { 3, 0, 0, 4, 0, 1 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 1, 2, 2, 6, 2, 3 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 4, 5, 5, 6, 1, 5 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
            case Pattern.TowerRoof: // flipped InnerCornerTop
                {
                    //Debug.Log("Adding TowerRoof face...");
                    var indices = new int[] { 1, 5, 2, 6, 4, 5 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 4, 5, 2, 6, 7, 4 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    indices = new int[] { 7, 4, 2, 6, 3, 7 };
                    JoinEdgeCenters(mesh, indices, verts, edges, config.transform, floor);
                    break;
                }
        }
    }

    static bool AddModule(MarchingModuleManager moduleManager, int hash, BMesh mesh, Vertex[] verts, Edge[] edges, int floor)
    {
        if (moduleManager == null) return false;
        var m = moduleManager.SampleModule(hash);
        if (m == null) return false;

        Debug.Log("Using module " + m.baseModule.meshFilter);
        var mf = m.baseModule.meshFilter;
        Vector3 floorOffset = floor * Vector3.up;
        var controlPoints = new Vector3[] {
            m.transform.EdgeCenter(0, 1, verts, edges) + floorOffset,
            m.transform.EdgeCenter(1, 2, verts, edges) + floorOffset,
            m.transform.EdgeCenter(2, 3, verts, edges) + floorOffset,
            m.transform.EdgeCenter(3, 0, verts, edges) + floorOffset,
            m.transform.EdgeCenter(0, 4, verts, edges) + floorOffset,
            m.transform.EdgeCenter(1, 5, verts, edges) + floorOffset,
            m.transform.EdgeCenter(2, 6, verts, edges) + floorOffset,
            m.transform.EdgeCenter(3, 7, verts, edges) + floorOffset,
            m.transform.EdgeCenter(4, 5, verts, edges) + floorOffset,
            m.transform.EdgeCenter(5, 6, verts, edges) + floorOffset,
            m.transform.EdgeCenter(6, 7, verts, edges) + floorOffset,
            m.transform.EdgeCenter(7, 4, verts, edges) + floorOffset
        };
        m.baseModule.deformer.controlPoints = controlPoints;
        BMeshUnity.Merge(mesh, mf.sharedMesh, m.baseModule.deformer);

        return true;
    }

    public static void Run(BMesh mesh, BMesh grid, string occupancyAttr, MarchingModuleManager moduleManager)
    {
        foreach (var f in grid.faces)
        {
            Debug.Assert(f.vertcount == 4);
            var vertList = f.NeighborVertices();
            var verts = vertList.ToArray();
            var edges = f.NeighborEdges().ToArray();
            var occupancies = vertList.ConvertAll(v => (v.attributes[occupancyAttr] as FloatAttributeValue).data);

            bool reachedTop = false;
            for (int floor = 0; !reachedTop && floor < 99; ++floor)
            {
                reachedTop = true;
                int hash = 0;
                for (int k = 0; k < 8; ++k)
                {
                    float[] o = occupancies[k % 4];
                    int fl = floor + (k / 4);
                    if (o.Length > fl) reachedTop = false;
                    int b = o.Length > fl && o[fl] > 0 ? 1 : 0;
                    hash += b << k;
                }

                if (!AddModule(moduleManager, hash, mesh, verts, edges, floor))
                {
                    AddLegacyBlock(hash, mesh, verts, edges, floor);
                }
            }
        }
    }
}