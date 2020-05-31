using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

/**
 * This manager registers all modules so that they get used by the module based
 * marching cubes algorithm. Look for modules in its children.
 */
public class MarchingModuleManager : MonoBehaviour
{
    public Material sharedMaterial; // material used to render modules (supporting deformation and instancing)

    /**
     * Maximum number of modules in the same moduleset
     */
    public int MaxModuleCount { get { return maxModuleCount; } }

    public class TransformedModule
    {
        public MarchingModule baseModule;
        public ModuleTransform transform = new ModuleTransform(0);
    }

    int maxModuleCount;
    HashSet<TransformedModule>[] moduleSets; // at build time
    TransformedModule[][] moduleLut; // at sample time

    void RegisterModule(MarchingModule module)
    {
        module.AddRenderer(sharedMaterial);
        moduleSets[module.hash].Add(new TransformedModule { baseModule = module });

        int rotations = module.allowRotationAroundVerticalAxis ? 4 : 1;
        for (int i = 0; i < rotations; ++i)
        {
            {
                var transform = new ModuleTransform(i);
                moduleSets[transform.TransformHash(module.hash)].Add(new TransformedModule {
                    baseModule = module,
                    transform = transform
                });
            }
            if (module.allowFlipAlongX)
            {
                var transform = new ModuleTransform(i, true);
                Debug.Assert(transform.flipped);
                moduleSets[transform.TransformHash(module.hash)].Add(new TransformedModule {
                    baseModule = module,
                    transform = transform
                });
            }
        }
    }

    public void Prepare()
    {
        maxModuleCount = 0;
        moduleLut = new TransformedModule[256][];
        for (int hash = 0; hash < 256; ++hash)
        {
            if (moduleSets[hash] == null || moduleSets[hash].Count == 0) continue;
            int n = moduleSets[hash].Count;
            moduleLut[hash] = new TransformedModule[n];
            maxModuleCount = Mathf.Max(maxModuleCount, n);
            int j = 0;
            foreach (var m in moduleSets[hash])
            {
                moduleLut[hash][j++] = m;
            }
        }
        moduleSets = null;
    }

    /**
     * Picks randomly a module valid for a given hash.
     * Requires Prepare() to be called before
     */
    public TransformedModule SampleModule(int hash)
    {
        Debug.Assert(hash >= 0 && hash < 256);
        if (moduleLut[hash] == null || moduleLut[hash].Length == 0) return null;
        int i = Random.Range(0, moduleLut[hash].Length);
        return moduleLut[hash][i];
    }

    /**
     * Get a precise module from the pool
     */
    public TransformedModule GetModule(int hash, int subindex)
    {
        Debug.Assert(hash >= 0 && hash < 256);
        if (moduleLut[hash] != null && subindex >= 0 && subindex < moduleLut[hash].Length)
        {
            return moduleLut[hash][subindex];
        }
        else
        {
            return null;
        }
    }

    public int ModuleCount(int hash)
    {
        if (moduleLut[hash] != null) return moduleLut[hash].Length;
        return 0;
    }

    public void ResetRenderers()
    {
        foreach (var arr in moduleLut)
        {
            if (arr != null)
            {
                foreach (var m in arr)
                {
                    m.baseModule.Renderer?.ResetInstances();
                }
            }
        }
    }

    public bool AreAllRenderersReady()
    {
        foreach (var arr in moduleLut)
        {
            if (arr != null)
            {
                foreach (var m in arr)
                {
                    if (m.baseModule.Renderer && !m.baseModule.Renderer.IsReady)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    void Start()
    {
        moduleSets = new HashSet<TransformedModule>[256];
        for (int i = 0; i < 256; ++i)
        {
            moduleSets[i] = new HashSet<TransformedModule>();
        }
        foreach (var module in GetComponentsInChildren<MarchingModule>())
        {
            RegisterModule(module);
        }
        Prepare();
    }
}
