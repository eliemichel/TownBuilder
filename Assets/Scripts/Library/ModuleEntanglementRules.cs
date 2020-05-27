using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LilyXwfc;

/**
 * Entanglement rules based on the ModuleManager
 */
public class ModuleEntanglementRules : AEntanglementRules
{
    readonly MarchingModuleManager moduleManager;

    // More human readable mapping of connectionType integer value
    enum ConnectionType
    {
        Horizontal,
        Above,
        Bellow,
    }

    /**
     * Dimension of the system
     */
    int Dimension { get { return moduleManager.MaxModuleCount; } }

    public ModuleEntanglementRules(MarchingModuleManager moduleManager)
    {
        this.moduleManager = moduleManager;
    }

    MarchingModuleManager.TransformedModule GetModule(PureState s)
    {
        int hash = s.index / Dimension;
        int subindex = s.index % Dimension;
        return moduleManager.GetModule(hash, subindex);
    }

    static int GetTransformedAdjacency(MarchingModuleManager.TransformedModule m, int face)
    {
        int untransformedFace = m.transform.ToCanonicalFace(face);
        if (face < 4) Debug.Assert(untransformedFace < 4, "Face #" + face + " transformed from " + untransformedFace + " by transform " + m.transform); // we only use transforms around Z
        return m.baseModule.adjacency[untransformedFace];
    }

    public override bool Allows(PureState x, BMesh.Loop connection, PureState y)
    {
        int connectionType = connection.attributes["adjacency"].asInt().data[0];
        int dualConnectionType = connection.next.attributes["adjacency"].asInt().data[0];

        var mx = GetModule(x);
        var my = GetModule(y);

        if (mx == null || my == null)
        {
            //Debug.Log("Allows(" + x + ", _, " + y + ") = true (no module found)");
            return true;
        }

        if (connectionType < 4) Debug.Assert(dualConnectionType < 4);
        if (connectionType == -1 || dualConnectionType == -1) return false;

        bool allowed = GetTransformedAdjacency(mx, connectionType) == GetTransformedAdjacency(my, dualConnectionType);
        return allowed;
    }

    public override int DimensionInExclusionClass(int exclusionClass)
    {
        return Mathf.Max(1, moduleManager.ModuleCount(exclusionClass));
    }
}
