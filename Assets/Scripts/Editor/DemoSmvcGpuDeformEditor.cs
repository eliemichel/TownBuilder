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
        if (GUILayout.Button("Deform")) cont.Deform();
        if (GUILayout.Button("Deform Debug")) cont.DeformDebug();
        if (GUILayout.Button("Show Cage")) cont.ShowCage();
        serializedObject.ApplyModifiedProperties();
    }
}
