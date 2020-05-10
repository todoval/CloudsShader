using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

[CustomEditor(typeof(CloudGenerator))]
[CanEditMultipleObjects]
public class CloudScript : Editor
{
    private SerializedProperty speed;
    private SerializedProperty color;
    private SerializedProperty tileSize;
    private SerializedProperty container;
    private SerializedProperty lightingType;
    private SerializedProperty sceneLight;
    private SerializedProperty absorptionCoeff;
    private SerializedProperty phaseType;
    private SerializedProperty henyeyCoeff;
    private SerializedProperty henyeyRatio;
    private SerializedProperty cloudMaxHeight;
    private SerializedProperty cloudHeightModifier;
    private SerializedProperty detailAmount;
    private SerializedProperty detailModifier;
    private SerializedProperty densityConstant;
    private SerializedProperty cloudBottomModifier;

    private void OnEnable()
    {
        lightingType = serializedObject.FindProperty("lightingType");
        phaseType = serializedObject.FindProperty("phaseType");
        sceneLight = serializedObject.FindProperty("sceneLight");
        absorptionCoeff = serializedObject.FindProperty("absorptionCoeff");
        speed = serializedObject.FindProperty("speed");
        color = serializedObject.FindProperty("color");
        tileSize = serializedObject.FindProperty("tileSize");
        container = serializedObject.FindProperty("container");
        henyeyCoeff = serializedObject.FindProperty("henyeyCoeff");
        henyeyRatio = serializedObject.FindProperty("henyeyRatio");
        detailAmount = serializedObject.FindProperty("detailAmount");
        detailModifier = serializedObject.FindProperty("detailModifier");
        densityConstant = serializedObject.FindProperty("densityConstant");
        cloudMaxHeight = serializedObject.FindProperty("cloudMaxHeight");
        cloudHeightModifier = serializedObject.FindProperty("cloudHeightModifier");
        cloudBottomModifier = serializedObject.FindProperty("cloudBottomModifier");
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
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(container);
        EditorGUILayout.Space();


        EditorGUILayout.LabelField("--- Main ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(tileSize, new GUIContent("Cloud size"));
        EditorGUILayout.PropertyField(speed, new GUIContent("Speed"));
        EditorGUILayout.PropertyField(color);
        EditorGUILayout.Slider(absorptionCoeff, 0, 1, new GUIContent("Absorption"));
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("--- Shape ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.Slider(densityConstant, 0, 5, new GUIContent("Density Multiplier"));
        EditorGUILayout.Slider(cloudMaxHeight, 0, 1, new GUIContent("Max Height"));
        EditorGUILayout.LabelField("Detail Noise");
        EditorGUI.indentLevel++;
        EditorGUILayout.Slider(detailAmount, 0, 1, new GUIContent("Max Value"));
        EditorGUILayout.Slider(detailModifier, 0, 1, new GUIContent("Detail Modifier"));
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Roundness");
        EditorGUI.indentLevel++;
        EditorGUILayout.Slider(cloudHeightModifier, 0, 1, new GUIContent("Cloud Top"));
        EditorGUILayout.Slider(cloudBottomModifier, 0, (float) 0.2, new GUIContent("Cloud Bottom"));
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();


        EditorGUILayout.LabelField("--- Lighting ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        int[] lightOptionsValues = {0,1,2};
        string[] lightOptionsDisplayed = {"Environmental", "Scene light","None"};
        lightingType.intValue = EditorGUILayout.IntPopup("Sun", lightingType.intValue, lightOptionsDisplayed, lightOptionsValues);
        // if scene light is set, let the user pick which light he wants to use
        if (lightingType.intValue == 1)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(sceneLight, new GUIContent("Scene light"));
            EditorGUI.indentLevel--;
        }

        int[] phaseOptionsValues = {0,1};
        string[] phaseOptionsDisplayed = {"Henyey-Greenstein", "None"};
        phaseType.intValue = EditorGUILayout.IntPopup("Phase function", phaseType.intValue, phaseOptionsDisplayed, phaseOptionsValues);
        if (phaseType.intValue == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(henyeyRatio, 0, 1, new GUIContent("Weight"));
            EditorGUILayout.Slider(henyeyCoeff, -1, 1, new GUIContent("Assymetry parameter"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("--- Performance ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.Toggle("Default settings", true);
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
        
    }
}
