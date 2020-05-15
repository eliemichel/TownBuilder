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
    public bool allowRotationAroundVerticalAxis = true;
    public MeshDeformer deformer;

    public void Init()
    {
        deformer = new MeshDeformer(meshFilter.transform);
        deformer.Precompute(meshFilter.sharedMesh);
    }
}
