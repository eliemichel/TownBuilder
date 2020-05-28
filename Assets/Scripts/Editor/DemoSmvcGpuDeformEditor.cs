using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DemoSmvcGpuDeform))]
[CanEditMultipleObjects]
public class DemoSmvcGpuDeformEditor : Editor
{
    DemoSmvcGpuDeform cont;

    void OnEnable()
    {
        cont = target as DemoSmvcGpuDeform;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}
