using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MarchingModule))]
[CanEditMultipleObjects]
public class MarchingModuleEditor : Editor
{
    MarchingModule cont;
    int hash;
    Rect hashBitRect;

    void OnEnable()
    {
        cont = target as MarchingModule;
    }

    void HashBit(int pow, float x, float y)
    {
        int mask = 1 << pow;
        bool b = (cont.hash & mask) != 0;

        //b = EditorGUILayout.Toggle(b);
        b = EditorGUI.Toggle(new Rect(hashBitRect.x + x * 16, hashBitRect.y + y * 16, 12, 12), b);

        hash = (hash & (~mask)) + (b ? mask : 0);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        hash = cont.hash;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Space();
        hashBitRect = GUILayoutUtility.GetRect(4 * 16, 6 * 16);
        HashBit(0, 2, 2+3);
        HashBit(1, 3, 1+3);
        HashBit(2, 1, 0+3);
        HashBit(3, 0, 1+3);
        HashBit(4, 2, 2);
        HashBit(5, 3, 1);
        HashBit(6, 1, 0);
        HashBit(7, 0, 1);
        cont.hash = hash;
        EditorGUILayout.Space();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}
