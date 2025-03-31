using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TerrainGenerator terrainGenerator = (TerrainGenerator)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
        {
            terrainGenerator.GenerateWorldInEditor();
        }

        if (GUILayout.Button("Clear Mesh"))
        {
            terrainGenerator.DeleteWorldInEditor();
        }
    }
}
