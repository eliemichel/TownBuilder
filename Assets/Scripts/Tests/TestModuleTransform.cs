using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace Tests
{
    public class TestModuleTransform
    {
        static readonly Matrix4x4 rx = Matrix4x4.Rotate(Quaternion.AngleAxis(90, new Vector3(1, 0, 0)));
        static readonly Matrix4x4 ry = Matrix4x4.Rotate(Quaternion.AngleAxis(90, new Vector3(0, 1, 0)));
        static readonly Matrix4x4 rz = Matrix4x4.Rotate(Quaternion.AngleAxis(90, new Vector3(0, 0, 1)));
        static readonly Matrix4x4 mx = Matrix4x4.Scale(new Vector3(-1, 1, 1));

        // In Unity's indirect Y-up base
        static readonly Vector3[] cornersUnity = new Vector3[]
        {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(1, -1, 1),
            new Vector3(-1, -1, 1),
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1),
        };

        // In Blender's direct Z-up base
        static readonly Vector3[] corners = new Vector3[]
        {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(1, 1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1),
        };

        // In Blender's direct Z-up base
        static readonly Vector3[] faces = new Vector3[]
        {
            new Vector3(0, -1, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1),
        };

        static int CornerIndex(Vector3 pos)
        {
            int index = -1;
            float minDistance = 0;
            for (int i = 0; i < corners.Length; ++i)
            {
                float d = Vector3.Distance(corners[i], pos);
                if (index == -1 || d < minDistance)
                {
                    minDistance = d;
                    index = i;
                }
            }
            return index;
        }

        static int FaceIndex(Vector3 pos)
        {
            int index = -1;
            float minDistance = 0;
            for (int i = 0; i < faces.Length; ++i)
            {
                float d = Vector3.Distance(faces[i], pos);
                if (index == -1 || d < minDistance)
                {
                    minDistance = d;
                    index = i;
                }
            }
            return index;
        }

        [Test]
        public void RunCornerIndex()
        {
            // (meta test, CornerIndex is used for testing only)
            for (int i = 0; i < corners.Length; ++i)
            {
                Vector3 shuffle = new Vector3(
                    Random.Range(-.25f, .25f),
                    Random.Range(-.25f, .25f),
                    Random.Range(-.25f, .25f)
                );
                Vector3 p = corners[i] + shuffle;
                int j = CornerIndex(p);
                Debug.Assert(j == i, "Position " + p + " is seen as corner " + j + " but " + i + " was expected.");
            }
        }

        [Test]
        public void RunFaceIndex()
        {
            // (meta test, CornerIndex is used for testing only)
            for (int i = 0; i < faces.Length; ++i)
            {
                Vector3 shuffle = new Vector3(
                    Random.Range(-.5f, .5f),
                    Random.Range(-.5f, .5f),
                    Random.Range(-.5f, .5f)
                );
                Debug.Assert(FaceIndex(faces[i] + shuffle) == i);
            }
        }

        public delegate void TestMatchDelegate(ModuleTransform transform, Matrix4x4 m);

        public static void TestCornerTransformMatch(ModuleTransform transform, Matrix4x4 m)
        {
            for (int i = 0; i < corners.Length; ++i)
            {
                int j = transform.FromCanonical(i);
                Vector3 p = corners[i];
                int j2 = CornerIndex(m * p);
                Debug.Assert(j == j2, "Mismatch between geometric and algebric transforms (resp. " + j2 + " vs " + j + ")");
            }
        }

        public static void TestFaceTransformMatch(ModuleTransform transform, Matrix4x4 m)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                int j = transform.FromCanonicalFace(i);
                Vector3 p = faces[i];
                int j2 = FaceIndex(m * p);
                Debug.Assert(j == j2, "Mismatch between geometric and algebric transforms (resp. " + j2 + " vs " + j + ")");
            }
        }

        public static void TestInvFaceTransformMatch(ModuleTransform transform, Matrix4x4 m)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                int j = transform.ToCanonicalFace(transform.FromCanonicalFace(i));
                Debug.Assert(j == i, "Not inversed at index " + i + ": found " + j);
            }
        }

        /**
         * Utility to run a test delegate over a few random transforms
         */
        public void TestRandomTransforms(TestMatchDelegate test, int testCount = 10, int transformLength = 10)
        {
            string encodedLut = "xyzs";
            var matrixLut = new Matrix4x4[] { rx, ry, rz, mx };
            for (int i = 0; i < testCount; ++i)
            {
                string encoded = "";
                Matrix4x4 m = Matrix4x4.identity;
                for (int k = 0; k < transformLength; ++k)
                {
                    int d = Random.Range(0, encodedLut.Length);
                    encoded += encodedLut[d];
                    m = m * matrixLut[d];
                }
                test(new ModuleTransform(encoded), m);
            }
        }

        [Test]
        public void TestGeometricTransform()
        {
            // Quaternion transforms right to left
            // While Module transform is left to right
            TestCornerTransformMatch(new ModuleTransform("x"), rx);
            TestCornerTransformMatch(new ModuleTransform("y"), ry);
            TestCornerTransformMatch(new ModuleTransform("z"), rz);
            TestCornerTransformMatch(new ModuleTransform("xy"), rx * ry);
            TestCornerTransformMatch(new ModuleTransform("xz"), rx * rz);
            TestCornerTransformMatch(new ModuleTransform("yz"), ry * rz);
            TestCornerTransformMatch(new ModuleTransform("xxyzx"), rx * rx * ry * rz * rx);

            TestCornerTransformMatch(new ModuleTransform("s"), mx);
            TestCornerTransformMatch(new ModuleTransform("xs"), rx * mx);
            TestCornerTransformMatch(new ModuleTransform("ys"), ry * mx);
            TestCornerTransformMatch(new ModuleTransform("zs"), rz * mx);

            TestRandomTransforms(TestCornerTransformMatch);
        }

        [Test]
        public void TestFaceTransform()
        {
            TestFaceTransformMatch(new ModuleTransform(0), Matrix4x4.identity);
            TestFaceTransformMatch(new ModuleTransform("x"), rx);
            TestFaceTransformMatch(new ModuleTransform("y"), ry);
            TestFaceTransformMatch(new ModuleTransform("z"), rz);
            TestFaceTransformMatch(new ModuleTransform("s"), mx);

            TestRandomTransforms(TestFaceTransformMatch);
        }

        [Test]
        public void TestInvFaceTransform()
        {
            TestInvFaceTransformMatch(new ModuleTransform(0), Matrix4x4.identity);
            TestInvFaceTransformMatch(new ModuleTransform("x"), rx);
            TestInvFaceTransformMatch(new ModuleTransform("y"), ry);
            TestInvFaceTransformMatch(new ModuleTransform("z"), rz);
            TestInvFaceTransformMatch(new ModuleTransform("s"), mx);

            TestInvFaceTransformMatch(new ModuleTransform("xy"), rx * ry);
            return;

            TestRandomTransforms(TestInvFaceTransformMatch);
        }

        [Test]
        public void TestEncoding()
        {
            string encodedLut = "xyzs";
            for (int i = 0; i < 10; ++i)
            {
                string encoded = "";
                for (int k = 0; k < 10; ++k)
                {
                    int d = Random.Range(0, encodedLut.Length);
                    encoded += encodedLut[d];
                }
                var transform = new ModuleTransform(encoded);
                Debug.Assert(transform.Encoded == encoded);
            }
        }

        static int GetOccupationHash(int[] occupation)
        {
            int hash = 0;
            for (int i = 0; i < 8; ++i)
            {
                Debug.Assert(occupation[i] == 0 || occupation[i] == 1);
                hash += occupation[i] << i;
            }
            return hash;
        }

        static int[] TransformOccupation(int[] occupation, Matrix4x4 m)
        {
            Debug.Assert(occupation.Length == 8);
            var transformed = new int[8];
            for (int i = 0; i < 8; ++i)
            {
                Vector3 c = corners[i];
                int j = CornerIndex(m * c);
                transformed[j] = occupation[i];
            }
            return transformed;
        }

        static void TestHashTransformMatch(ModuleTransform transform, Matrix4x4 m)
        {
            for (int hash0 = 0; hash0 < 256; ++hash0)
            {
                var occ = new int[8];
                for (int i = 0; i < 8; ++i)
                {
                    occ[i] = (hash0 & (1 << i)) != 0 ? 1 : 0;
                }

                int hash = GetOccupationHash(occ);
                Debug.Assert(hash == hash0);
                int transformedHashGeo = GetOccupationHash(TransformOccupation(occ, m));
                int transformedHashAlg = transform.TransformHash(hash);
                Debug.Assert(transformedHashGeo == transformedHashAlg, "With transform " + transform + ": " + transformedHashGeo + " != " + transformedHashAlg);

                if (transformedHashGeo != transformedHashAlg) break;
            }
        }

        [Test]
        public void TestHash()
        {
            TestHashTransformMatch(new ModuleTransform(0), Matrix4x4.identity);
            TestHashTransformMatch(new ModuleTransform("x"), rx);

            TestRandomTransforms(TestHashTransformMatch);
        }
    }
}
