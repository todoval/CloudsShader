using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

[CustomEditor(typeof(NoiseGenerator))]
[CanEditMultipleObjects]
public class NoiseScriptEditor : Editor
{
    //int textureResolutionValue;

    // perlin settings
    private SerializedProperty perlinOctaves; // 8 by default
    private SerializedProperty perlinFrequency; // 1 by default
    private SerializedProperty perlinPersistence; // 0.6 by default
    private SerializedProperty perlinLacunarity; // 2 by default
    private SerializedProperty perlinTextureResolution;

    private void OnEnable()
    {
        perlinTextureResolution = serializedObject.FindProperty("perlinTextureResolution");
        perlinOctaves = serializedObject.FindProperty("perlinOctaves");
        perlinFrequency = serializedObject.FindProperty("perlinFrequency");
        perlinPersistence = serializedObject.FindProperty("perlinPersistence");
        perlinLacunarity = serializedObject.FindProperty("perlinLacunarity");
    }

    public override void OnInspectorGUI() 
    {
        serializedObject.Update();

        // perlin noise options
        EditorGUILayout.LabelField("Perlin noise:");
        EditorGUI.indentLevel++;
        string[] textureResolutionOptionNames = {"1","2","4","8","16"};
        int[] textureResolutionOptionValues = {1,2,4,8,16};
        perlinTextureResolution.intValue = EditorGUILayout.IntPopup("Texture Resolution", perlinTextureResolution.intValue, textureResolutionOptionNames, textureResolutionOptionValues);
        EditorGUILayout.IntSlider(perlinOctaves, 1, 8);
        EditorGUILayout.PropertyField(perlinFrequency);
        EditorGUILayout.PropertyField(perlinPersistence);
        EditorGUILayout.PropertyField(perlinLacunarity);
        EditorGUI.indentLevel--;

        // worley noise options for channels
        EditorGUILayout.LabelField("Worley Noise:");
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Red Channel:");
        EditorGUI.indentLevel++;
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Green Channel:");
        EditorGUI.indentLevel++;
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Blue Channel:");
        EditorGUI.indentLevel++;
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }

}
 