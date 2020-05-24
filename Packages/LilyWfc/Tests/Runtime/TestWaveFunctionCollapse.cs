using LilyWfc;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestWaveFunctionCollapse
{
    static BMesh GenerateGridTopologyMesh(int width, int height)
    {
        var topology = new BMesh();
        topology.AddEdgeAttribute("type", BMesh.AttributeBaseType.Int, 1);
        for (int j = 0; j < height; ++j)
        {
            for (int i = 0; i < width; ++i)
            {
                topology.AddVertex(i, 0, j); // vertex # i + j * w
                if (j > 0)
                {
                    var e = topology.AddEdge(i + j * width, i + (j - 1) * width);
                    e.attributes["type"] = new BMesh.IntAttributeValue(1 /* south */);
                }
                if (i > 0)
                {
                    var e = topology.AddEdge(i + j * width, i - 1 + j * width);
                    e.attributes["type"] = new BMesh.IntAttributeValue(3 /* west */);
                }
            }
        }

        return topology;
    }

    static ModuleRegistry GenerateExampleModuleRegistry()
    {
        var modules = new Module[16];
        int i = 0;
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        0, 0, 0, // S
	        0, 0, 0, // E
	        0, 0, 0  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        1, 1, 1, // S
	        0, 0, 1, // E
	        0, 0, 1  // W
        });
        modules[i++] = new Module(new int[] {
            1, 1, 1, // N
	        1, 1, 1, // S
	        1, 1, 1, // E
	        1, 1, 1  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        2, 2, 2, // S
	        0, 2, 2, // E
	        0, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            2, 2, 2, // N
	        2, 2, 2, // S
	        2, 2, 2, // E
	        2, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            2, 2, 2, // N
	        1, 1, 1, // S
	        2, 2, 1, // E
	        2, 2, 1  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        0, 0, 2, // S
	        0, 2, 2, // E
	        0, 0, 0  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 2, // N
	        0, 0, 2, // S
	        2, 2, 2, // E
	        0, 0, 0  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 2, // N
	        1, 1, 1, // S
	        2, 2, 1, // E
	        0, 0, 1  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 2, // N
	        2, 2, 2, // S
	        2, 2, 2, // E
	        0, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        2, 0, 0, // S
	        0, 0, 0, // E
	        0, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            2, 0, 0, // N
	        2, 0, 0, // S
	        0, 0, 0, // E
	        2, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            2, 0, 0, // N
	        1, 1, 1, // S
	        0, 0, 1, // E
	        2, 2, 1  // W
        });
        modules[i++] = new Module(new int[] {
            2, 0, 0, // N
	        2, 2, 2, // S
	        0, 2, 2, // E
	        2, 2, 2  // W
        });
        modules[i++] = new Module(new int[] {
            0, 3, 0, // N
	        1, 1, 1, // S
	        0, 0, 1, // E
	        0, 0, 1  // W
        });
        modules[i++] = new Module(new int[] {
            0, 0, 0, // N
	        0, 3, 0, // S
	        0, 0, 0, // E
	        0, 0, 0  // W
        });

        return new ModuleRegistry { modules = modules };
    }

    /**
     * Ensure that coroutine and default implementation give the very same result
     */
    static bool TestCoroutine()
    {
        var topology = GenerateGridTopologyMesh(10, 6);
        var registry = GenerateExampleModuleRegistry();
        var rules = new ConnectionStateEntanglementRules(registry);
        var system = new WaveFunctionSystem(topology, rules, registry.modules.Length);
        var wfc = new WaveFunctionCollapse(system);

        Random.InitState(3623);
        wfc.Collapse(200);

        // Save state
        var savedState = new SuperposedState[topology.vertices.Count];
        int i = 0;
        foreach (WaveVariable x in system.Variables())
        {
            savedState[i++] = system.GetWave(x);
        }

        system.Reset();

        Random.InitState(3623);
        for (var it = wfc.CollapseCoroutine(200); it.MoveNext();) { }

        // Check
        i = 0;
        foreach (WaveVariable x in system.Variables())
        {
            Debug.Assert(savedState[i++].Equals(system.GetWave(x)), "Different state for variable " + x);
        }

        Debug.Log("TestWaveFunctionCollapse: TestCoroutine finished.");
        return true;
    }

    public static bool Run()
    {
        if (!TestCoroutine()) return false;
        return true;
    }
}
