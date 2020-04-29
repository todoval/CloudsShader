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

    // perlin settings for the shape noise texture
    private SerializedProperty perlinOctaves; // 8 by default
    private SerializedProperty perlinFrequency; // 1 by default
    private SerializedProperty perlinPersistence; // 0.6 by default
    private SerializedProperty perlinLacunarity; // 2 by default
    private SerializedProperty perlinTextureResolution; // 16 by default

    // worley settings for other three channels of the shape noise texture
    private SerializedProperty shapeGreenChannelOctaves;
    private SerializedProperty shapeBlueChannelOctaves;
    private SerializedProperty shapeAlphaChannelOctaves;
    private SerializedProperty shapeGreenChannelCellSize;
    private SerializedProperty shapeBlueChannelCellSize;
    private SerializedProperty shapeAlphaChannelCellSize;
    
    // worley settings for the detail noise texture
    private SerializedProperty detailGreenChannelOctaves;
    private SerializedProperty detailBlueChannelOctaves;
    private SerializedProperty detailRedChannelOctaves;
    private SerializedProperty detailGreenChannelCellSize;
    private SerializedProperty detailBlueChannelCellSize;
    private SerializedProperty detailRedChannelCellSize;
    private void OnEnable()
    {
        // the compute shaders
        NoiseTextureGenerator = serializedObject.FindProperty("NoiseTextureGenerator");
        randomNumberGenerator = serializedObject.FindProperty("randomNumberGenerator");
        slicer = serializedObject.FindProperty("slicer");
        // perlin settings (red channel) of the shape texture
        perlinTextureResolution = serializedObject.FindProperty("perlinTextureResolution");
        perlinOctaves = serializedObject.FindProperty("perlinOctaves");
        perlinFrequency = serializedObject.FindProperty("perlinFrequency");
        perlinPersistence = serializedObject.FindProperty("perlinPersistence");
        perlinLacunarity = serializedObject.FindProperty("perlinLacunarity");
        // worley settings of the shape texture
        shapeGreenChannelOctaves = serializedObject.FindProperty("shapeGreenChannelOctaves");
        shapeBlueChannelOctaves = serializedObject.FindProperty("shapeBlueChannelOctaves");
        shapeAlphaChannelOctaves = serializedObject.FindProperty("shapeAlphaChannelOctaves");
        shapeGreenChannelCellSize = serializedObject.FindProperty("shapeGreenChannelCellSize");
        shapeBlueChannelCellSize = serializedObject.FindProperty("shapeBlueChannelCellSize");
        shapeAlphaChannelCellSize = serializedObject.FindProperty("shapeAlphaChannelCellSize");
        // worley settings of the detail texture
        detailGreenChannelOctaves = serializedObject.FindProperty("detailGreenChannelOctaves");
        detailBlueChannelOctaves = serializedObject.FindProperty("detailBlueChannelOctaves");
        detailRedChannelOctaves = serializedObject.FindProperty("detailRedChannelOctaves");
        detailGreenChannelCellSize = serializedObject.FindProperty("detailGreenChannelCellSize");
        detailBlueChannelCellSize = serializedObject.FindProperty("detailBlueChannelCellSize");
        detailRedChannelCellSize = serializedObject.FindProperty("detailRedChannelCellSize");
    }
    public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
        r.height = thickness;
        r.y+=padding/2;
        r.x-=30;
        r.width +=36;
        EditorGUI.DrawRect(r, color);
    }

    public override void OnInspectorGUI() 
    {
        serializedObject.Update();
        // the compute shaders needed for the script to work
        
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(NoiseTextureGenerator);
        EditorGUILayout.PropertyField(randomNumberGenerator);
        EditorGUILayout.PropertyField(slicer);

        // draw a line between compute shader settings and shape noise settings
        EditorGUILayout.Space();
        DrawUILine(new Color((float)0.5,(float)0.5,(float)0.5,1), 1, 10);
        
        // perlin noise options
        EditorGUILayout.LabelField("Shape noise:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
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
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Worley Noise:");
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Green Channel:");
        // add octaves, add cellSize
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(shapeGreenChannelOctaves, 1, 8,"Octaves");
        string[] cellOptionNames = {"1","2","4","8","16", "32"};
        int[] cellOptionValues = {1,2,4,8,16,32};
        shapeGreenChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", shapeGreenChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Blue Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(shapeBlueChannelOctaves, 1, 8,"Octaves");
        shapeBlueChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", shapeBlueChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Alpha Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(shapeAlphaChannelOctaves, 1, 8,"Octaves");
        shapeAlphaChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", shapeAlphaChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // add a button to create shape texture
        GUILayout.BeginHorizontal();
        GUILayout.Space(Screen.width/2 - 150/2);
        if (GUILayout.Button("Create Shape Texture",GUILayout.Width(150), GUILayout.Height(30)))
        {
            FindObjectOfType<NoiseGenerator>().createShapeNoise();
        }
        GUILayout.EndHorizontal();
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;

        // draw a line between detail and shape noise settings
        DrawUILine(new Color((float)0.5,(float)0.5,(float)0.5,1), 1, 10);
        
        // start of the detail noise
        EditorGUILayout.LabelField("Detail noise:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Worley noise:");
        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("Red Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(detailRedChannelOctaves, 1, 8,"Octaves");
        detailRedChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", detailRedChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Green Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(detailGreenChannelOctaves, 1, 8,"Octaves");
        detailGreenChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", detailGreenChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Blue Channel:");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(detailBlueChannelOctaves, 1, 8,"Octaves");
        detailBlueChannelCellSize.intValue = EditorGUILayout.IntPopup("Cell Size", detailBlueChannelCellSize.intValue,cellOptionNames, cellOptionValues);
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // add the add noise button
        GUILayout.BeginHorizontal();
        GUILayout.Space(Screen.width/2 - 150/2);
        if (GUILayout.Button("Create Detail Texture",GUILayout.Width(150), GUILayout.Height(30)))
        {
            FindObjectOfType<NoiseGenerator>().createDetailNoise();
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        serializedObject.ApplyModifiedProperties();
    }

}
 