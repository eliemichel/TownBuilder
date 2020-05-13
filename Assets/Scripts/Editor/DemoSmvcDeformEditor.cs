using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DemoSmvcDeform))]
[CanEditMultipleObjects]
public class DemoSmvcDeformEditor : Editor
{
    DemoSmvcDeform cont;

    void OnEnable()
    {
        cont = target as DemoSmvcDeform;
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
