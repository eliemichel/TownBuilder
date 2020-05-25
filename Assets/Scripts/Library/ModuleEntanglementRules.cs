﻿using System.Collections;
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

    MarchingModule GetModule(PureState s)
    {
        int hash = s.index / Dimension;
        int subindex = s.index % Dimension;
        return moduleManager.GetModule(hash, subindex)?.baseModule;
    }

    //public override SuperposedState AllowedStates(SuperposedState x, int connectionType);
    public override bool Allows(PureState x, int connectionType, PureState y)
    {
        if (connectionType == (int)ConnectionType.Horizontal)
        {
            Debug.Log("Allows(_, Horizontal, _) = true");
            return true;
        }

        MarchingModule mx = GetModule(x);
        MarchingModule my = GetModule(y);

        if (mx == null || my == null)
        {
            Debug.Log("Allows(" + x + ", _, " + y + ") = true (no module found)");
            return true;
        }

        if (connectionType == (int)ConnectionType.Above)
        {
            Debug.Log("Allows(" + x + ", Above, " + y + ") = " + (mx.hasPillarBellow == my.hasPillarAbove));
            return mx.hasPillarBellow == my.hasPillarAbove;
        }
        else
        {
            Debug.Log("Allows(" + x + ", Bellow, " + y + ") = " + (mx.hasPillarAbove == my.hasPillarBellow));
            return mx.hasPillarAbove == my.hasPillarBellow;
        }
    }

    public override int DualConnection(int c)
    {
        switch ((ConnectionType)c)
        {
        case ConnectionType.Horizontal:
            return (int)ConnectionType.Horizontal;
        case ConnectionType.Above:
            return (int)ConnectionType.Bellow;
        case ConnectionType.Bellow:
            return (int)ConnectionType.Above;
        }
        return c;
    }

    public override int DimensionInExclusionClass(int exclusionClass)
    {
        return Mathf.Max(1, moduleManager.ModuleCount(exclusionClass));
    }
}
