using LilyXwfc;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestExclusiveWaveFunctionCollapse
{
    static BMesh GenerateGridTopologyMesh(int width, int height)
    {
        var topology = new BMesh();
        topology.AddLoopAttribute("adjacency", BMesh.AttributeBaseType.Int, 1);
        for (int j = 0; j < height; ++j)
        {
            for (int i = 0; i < width; ++i)
            {
                BMesh.Vertex v = topology.AddVertex(i, 0, j); // vertex # i + j * w
                if (j > 0)
                {
                    BMesh.Vertex sv = topology.vertices[i + (j - 1) * width]; // south vertex
                    var f = topology.AddFace(new BMesh.Vertex[] { v, sv });
                    var loop = f.loop;
                    if (loop.vert != v) loop = loop.next;
                    Debug.Assert(loop.vert == v);

                    loop.attributes["adjacency"] = new BMesh.IntAttributeValue(1 /* south */);
                    loop.next.attributes["adjacency"] = new BMesh.IntAttributeValue(0 /* north */);
                }
                if (i > 0)
                {
                    BMesh.Vertex wv = topology.vertices[i - 1 + j * width]; // west vertex
                    var e = topology.AddEdge(v, wv);
                    e.attributes["adjacency"] = new BMesh.IntAttributeValue(3 /* west */);

                    var f = topology.AddFace(new BMesh.Vertex[] { v, wv });
                    var loop = f.loop;
                    if (loop.vert != v) loop = loop.next;
                    Debug.Assert(loop.vert == v);

                    loop.attributes["adjacency"] = new BMesh.IntAttributeValue(3 /* west */);
                    loop.next.attributes["adjacency"] = new BMesh.IntAttributeValue(2 /* east */);
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

        Debug.Log("TestExclusiveWaveFunctionCollapse: TestCoroutine finished.");
        return true;
    }

    public static bool Run()
    {
        if (!TestCoroutine()) return false;
        return true;
    }
}
