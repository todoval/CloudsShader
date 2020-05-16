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
    private SerializedProperty useBlueNoiseRay;
    private SerializedProperty useBlueNoiseLight;
    private SerializedProperty blueNoiseLightAmount;
    private SerializedProperty blueNoiseRayAmount;
    private SerializedProperty lightMarchSteps;
    private SerializedProperty powderCoeff;
    private SerializedProperty powderAmount;
    private SerializedProperty powderIntensity;

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
        useBlueNoiseRay = serializedObject.FindProperty("useBlueNoiseRay");
        useBlueNoiseLight = serializedObject.FindProperty("useBlueNoiseLight");
        blueNoiseLightAmount = serializedObject.FindProperty("blueNoiseLightAmount");
        blueNoiseRayAmount = serializedObject.FindProperty("blueNoiseRayAmount");
        lightMarchSteps = serializedObject.FindProperty("lightMarchSteps");
        powderCoeff = serializedObject.FindProperty("powderCoeff");
        powderAmount = serializedObject.FindProperty("powderAmount");
        powderIntensity = serializedObject.FindProperty("powderIntensity");
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
        EditorGUILayout.LabelField("Powder Effect");
        EditorGUI.indentLevel++;
        EditorGUILayout.Slider(powderAmount, 0, 1, new GUIContent("Amount"));
        EditorGUILayout.Slider(powderIntensity, 0, 50, new GUIContent("Intensity"));
        EditorGUILayout.Slider(powderCoeff, 0, 1, new GUIContent("Extinction Coefficient"));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("--- Performance ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Ray march");
        EditorGUI.indentLevel++;
        useBlueNoiseRay.boolValue = EditorGUILayout.Toggle("Blue Noise",  useBlueNoiseRay.boolValue);
        if (useBlueNoiseRay.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(blueNoiseRayAmount, 1, 2, new GUIContent("Amount"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Light march");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(lightMarchSteps, 1, 4, new GUIContent("Step size"));
        useBlueNoiseLight.boolValue = EditorGUILayout.Toggle("Blue Noise", useBlueNoiseLight.boolValue);
        if (useBlueNoiseLight.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(blueNoiseLightAmount, 0, (float) 0.5, new GUIContent("Amount"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
        
    }
}
