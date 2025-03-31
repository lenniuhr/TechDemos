using LenniUhr.Grass;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassGenerator))]
public class GrassGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GrassGenerator grassGenerator = (GrassGenerator)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Generate Grass"))
        {
            grassGenerator.Initialize();
        }
    }
}
