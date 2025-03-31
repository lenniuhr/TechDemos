using UnityEditor;
using UnityEngine;

namespace LenniUhr.Grass
{
    [CustomEditor(typeof(GrassMap))]
    public class GrassMapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            GrassMap grassMap = (GrassMap)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Generate Mesh Buffer"))
            {
                grassMap.GenerateMeshBuffer();
            }
            if (GUILayout.Button("Generate Mask Texture"))
            {
                grassMap.GenerateMaskTexture();
            }
        }
    }
}
