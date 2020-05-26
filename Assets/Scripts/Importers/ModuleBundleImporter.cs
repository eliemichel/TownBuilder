using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using UnityEditor;

[System.Serializable()]
class ModuleInfo
{
    public string name;
    public int hash;
    public int[] adjacency; // one int per direction -- horizontal, above, bellow -- which must be equal in neighbor's dual connection
}
[System.Serializable()]
class ModuleBundleInfo
{
    public ModuleInfo[] modules;
    public string fbx_file;
}

/**
 * A Module Bundle is a set of metadata around an FBX that is exported from
 * Blender to Unity.
 */
[ScriptedImporter(1, "wfc")]
public class ModuleBundleImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var root = new GameObject();
        var bundleInfo = JsonUtility.FromJson<ModuleBundleInfo>(File.ReadAllText(ctx.assetPath));

        string assetDir = Path.GetDirectoryName(ctx.assetPath);
        string fbxPath = Path.Combine(assetDir, bundleInfo.fbx_file);

        // Load FBX first
        var fbx = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
        if (fbx == null) return;

        Dictionary<string, MeshFilter> meshFilters = new Dictionary<string, MeshFilter>();

        ctx.DependsOnSourceAsset(fbxPath);
        foreach (var asset in fbx)
        {
            var mf = (asset as GameObject)?.GetComponent<MeshFilter>();
            if (mf == null) continue;
            meshFilters[mf.name] = mf;
        }

        int i = 0;
        foreach (var moduleInfo in bundleInfo.modules)
        {
            Debug.Log("Module '" + moduleInfo.name + "', hash " + moduleInfo.hash);
            var module = new GameObject(moduleInfo.name);
            module.transform.SetParent(root.transform);
            module.transform.position = Vector3.forward * 3 * i;
            MarchingModule behavior = module.AddComponent<MarchingModule>();
            behavior.hash = moduleInfo.hash;
            behavior.allowRotationAroundVerticalAxis = true;
            behavior.hasPillarAbove = moduleInfo.adjacency[4] == 0;
            behavior.hasPillarBellow = moduleInfo.adjacency[5] == 0;
            if (meshFilters.ContainsKey(moduleInfo.name))
            {
                behavior.meshFilter = meshFilters[moduleInfo.name];
            }
            ctx.AddObjectToAsset(moduleInfo.name, module);
            ++i;
        }

        ctx.AddObjectToAsset("Module Bundle", root);
        ctx.SetMainObject(root);
    }
}
