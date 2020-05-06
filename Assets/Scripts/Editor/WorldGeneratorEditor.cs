using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldGenerator))]
[CanEditMultipleObjects]
public class WorldGeneratorEditor : Editor
{
    WorldGenerator cont;

    //SerializedProperty divisions;

    void OnEnable()
    {
        cont = target as WorldGenerator;
        //divisions = serializedObject.FindProperty("divisions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        //EditorGUILayout.PropertyField(divisions);
        if (GUILayout.Button("Generate Hex Grid"))
        {
            cont.GenerateSubdividedHex();
        }
        if (GUILayout.Button("Remove Edge"))
        {
            cont.RemoveRandomEdge();
        }
        if (GUILayout.Button("Remove Target Edge"))
        {
            cont.RemoveTargetEdge();
        }
        if (GUILayout.Button("Run Tests"))
        {
            cont.Test();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
