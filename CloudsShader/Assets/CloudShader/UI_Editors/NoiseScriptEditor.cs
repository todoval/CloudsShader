using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

[CustomEditor(typeof(NoiseGenerator))]
[CanEditMultipleObjects]
public class NoiseScriptEditor : Editor
{
    // compute shaders needed for the noise script to work
    private SerializedProperty slicer;
    private SerializedProperty NoiseTextureGenerator;
    private SerializedProperty randomNumberGenerator;

    // perlin settings
    private SerializedProperty perlinOctaves; // 8 by default
    private SerializedProperty perlinFrequency; // 1 by default
    private SerializedProperty perlinPersistence; // 0.6 by default
    private SerializedProperty perlinLacunarity; // 2 by default
    private SerializedProperty perlinTextureResolution; // 16 by default

    // worley settings for other three channels
    private SerializedProperty greenChannelOctaves;
    private SerializedProperty blueChannelOctaves;
    private SerializedProperty alphaChannelOctaves;
    private SerializedProperty greenChannelCellSize;
    private SerializedProperty blueChannelCellSize;
    private SerializedProperty alphaChannelCellSize;

    private void OnEnable()
    {
        // the compute shaders
        NoiseTextureGenerator = serializedObject.FindProperty("NoiseTextureGenerator");
        randomNumberGenerator = serializedObject.FindProperty("randomNumberGenerator");
        slicer = serializedObject.FindProperty("slicer");
        // perlin settings (red channel)
        perlinTextureResolution = serializedObject.FindProperty("perlinTextureResolution");
        perlinOctaves = serializedObject.FindProperty("perlinOctaves");
        perlinFrequency = serializedObject.FindProperty("perlinFrequency");
        perlinPersistence = serializedObject.FindProperty("perlinPersistence");
        perlinLacunarity = serializedObject.FindProperty("perlinLacunarity");
        // worley settings
        greenChannelOctaves = serializedObject.FindProperty("greenChannelOctaves");
        blueChannelOctaves = serializedObject.FindProperty("blueChannelOctaves");
        alphaChannelOctaves = serializedObject.FindProperty("alphaChannelOctaves");
        greenChannelCellSize = serializedObject.FindProperty("greenChannelCellSize");
        blueChannelCellSize = serializedObject.FindProperty("blueChannelCellSize");
        alphaChannelCellSize = serializedObject.FindProperty("alphaChannelCellSize");
    }

    public override void OnInspectorGUI() 
    {
        serializedObject.Update();
        // the compute shaders needed for the script to work
        EditorGUILayout.PropertyField(NoiseTextureGenerator);
        EditorGUILayout.PropertyField(randomNumberGenerator);
        EditorGUILayout.PropertyField(slicer);

        // perlin noise options
        EditorGUILayout.LabelField("Perlin noise:");
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Red Channel:");
        EditorGUI.indentLevel++;
        string[] textureResolutionOptionNames = {"1","2","4","8","16"};
        int[] textureResolutionOptionValues = {1,2,4,8,16};
        perlinTextureResolution.intValue = EditorGUILayout.IntPopup("Resolution", perlinTextureResolution.intValue, textureResolutionOptionNames, textureResolutionOptionValues);
        EditorGUILayout.IntSlider(perlinOctaves, 1, 8, "Octaves");
        EditorGUILayout.PropertyField(perlinFrequency, new GUIContent("Frequency"));
        EditorGUILayout.PropertyField(perlinPersistence, new GUIContent("Persistence"));
        EditorGUILayout.PropertyField(perlinLacunarity, new GUIContent("Lacunarity"));
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;

        // worley noise options for channels
        EditorGUILayout.LabelField("Worley Noise:");
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Green Channel:");
        
        // add octaves, add cellSize
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(greenChannelOctaves, 1, 8,"Octaves");
        string[] cellOptionNames = {"1","2","4","8","16", "32"};
        int[] cellOptionValues = {1,2,4,8,16,32};
        greenChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", greenChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Blue Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(blueChannelOctaves, 1, 8,"Octaves");
        blueChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", blueChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Alpha Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(alphaChannelOctaves, 1, 8,"Octaves");
        alphaChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", alphaChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }

}
 