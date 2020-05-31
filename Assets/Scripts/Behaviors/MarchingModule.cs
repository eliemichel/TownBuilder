using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * A marching module is a cube-based element copied when doing module based
 * marching cubes. It declares which of its cube vertices are inside and
 * outside, and is registered in a global module list in the
 * MarchingModuleManager.
 */
public class MarchingModule : MonoBehaviour
{
    public int hash; // index in the LUT, edited bit by bit in the editor
    public MeshFilter meshFilter;
    public bool allowRotationAroundVerticalAxis = true; // there is almost no reason not to tick this
    public bool allowFlipAlongX = true;
    public int[] adjacency; // one int per direction which must be equal in neighbor's dual connection

    public MarchingModuleRenderer Renderer { get; private set; }

    // For entanglement rules (a bit ad hoc for now)
    public bool hasPillarAbove = false;
    public bool hasPillarBellow = false;

    public void AddRenderer(Material material)
    {
        Renderer = gameObject.AddComponent<MarchingModuleRenderer>();
        Renderer.material = material;
    }

    private void OnDrawGizmos()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Gizmos.color = Color.white;
            Matrix4x4 m = meshFilter.transform.localToWorldMatrix;
            Vector4 z = m.GetColumn(1);
            m.SetColumn(1, -m.GetColumn(0));
            m.SetColumn(0, -z);
            m.SetColumn(3, new Vector4(0, 0, 0, 1));
            Gizmos.matrix = transform.localToWorldMatrix * m;
            if (meshFilter.sharedMesh.vertexCount > 0) Gizmos.DrawWireMesh(meshFilter.sharedMesh);
        }

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3[] points = new Vector3[] {
            new Vector3(1, 0, -1),
            new Vector3(1, 0, 1),
            new Vector3(-1, 0, 1),
            new Vector3(-1, 0, -1),

            new Vector3(1, 2, -1),
            new Vector3(1, 2, 1),
            new Vector3(-1, 2, 1),
            new Vector3(-1, 2, -1)
        };

        for (int i = 0; i < points.Length; ++i)
        {
            if ((hash & (1 << i)) != 0)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(points[i], Vector3.one * 0.1f);
            }
            else
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(points[i], Vector3.one * 0.1f);
            }
        }
    }
}
