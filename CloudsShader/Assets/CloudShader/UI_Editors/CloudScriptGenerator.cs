using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

[CustomEditor(typeof(CloudGenerator))]
[CanEditMultipleObjects]
public class CloudScript : Editor
{
    // crucial properties, shader fails without them
    
    private SerializedProperty container;
    private SerializedProperty renderingShader;
    private SerializedProperty blendingShader;

    // main properties
    private SerializedProperty speed;
    private SerializedProperty color;
    private SerializedProperty tileSize;
    private SerializedProperty absorptionCoeff;

    // shape properties
    private SerializedProperty densityConstant;
    private SerializedProperty cloudMaxHeight;
    private SerializedProperty cloudHeightModifier;
    private SerializedProperty detailAmount;
    private SerializedProperty detailModifier;
    private SerializedProperty cloudBottomModifier;

    // lighting properties
    private SerializedProperty lightingType;
    private SerializedProperty sceneLight;
    
    private SerializedProperty cloudIntensity;

    // phase function properties
    private SerializedProperty phaseType;
    private SerializedProperty henyeyCoeff;
    private SerializedProperty henyeyRatio;
    
    private SerializedProperty henyeyIntensity;


    // powder effect properties
    private SerializedProperty powderCoeff;
    private SerializedProperty powderAmount;
    private SerializedProperty powderIntensity;


    // performance properties
    private SerializedProperty useBlueNoiseRay;
    private SerializedProperty useBlueNoiseLight;
    private SerializedProperty blueNoiseLightAmount;
    private SerializedProperty blueNoiseRayAmount;
    private SerializedProperty lightMarchSteps;
    private SerializedProperty lightMarchDecrease;
    private SerializedProperty rayMarchDecrease;
    private SerializedProperty rayMarchStepSize;
    private SerializedProperty temporalUpsampling;
    private SerializedProperty blendingCoeff;
    
    private void OnEnable()
    {
        // crucial properties, shader fails without them
        container = serializedObject.FindProperty("container");
        renderingShader = serializedObject.FindProperty("renderingShader");
        blendingShader = serializedObject.FindProperty("blendingShader");

        // main properties
        absorptionCoeff = serializedObject.FindProperty("absorptionCoeff");
        speed = serializedObject.FindProperty("speed");
        color = serializedObject.FindProperty("color");
        tileSize = serializedObject.FindProperty("tileSize");

        // shape properties
        densityConstant = serializedObject.FindProperty("densityConstant");
        detailAmount = serializedObject.FindProperty("detailAmount");
        detailModifier = serializedObject.FindProperty("detailModifier");
        cloudMaxHeight = serializedObject.FindProperty("cloudMaxHeight");
        cloudHeightModifier = serializedObject.FindProperty("cloudHeightModifier");
        cloudBottomModifier = serializedObject.FindProperty("cloudBottomModifier");

        // lighting properties
        sceneLight = serializedObject.FindProperty("sceneLight");
        lightingType = serializedObject.FindProperty("lightingType");
        cloudIntensity = serializedObject.FindProperty("cloudIntensity");

        // phase function properties
        phaseType = serializedObject.FindProperty("phaseType");
        henyeyCoeff = serializedObject.FindProperty("henyeyCoeff");
        henyeyRatio = serializedObject.FindProperty("henyeyRatio");
        henyeyIntensity = serializedObject.FindProperty("henyeyIntensity");

        // performance properties for ray march
        rayMarchStepSize = serializedObject.FindProperty("rayMarchStepSize");
        rayMarchDecrease = serializedObject.FindProperty("rayMarchDecrease");
        useBlueNoiseRay = serializedObject.FindProperty("useBlueNoiseRay");
        blueNoiseRayAmount = serializedObject.FindProperty("blueNoiseRayAmount");

        // performance settings for light march
        lightMarchSteps = serializedObject.FindProperty("lightMarchSteps");
        lightMarchDecrease = serializedObject.FindProperty("lightMarchDecrease");
        useBlueNoiseLight = serializedObject.FindProperty("useBlueNoiseLight");
        blueNoiseLightAmount = serializedObject.FindProperty("blueNoiseLightAmount");

        // powder effect settings
        powderCoeff = serializedObject.FindProperty("powderCoeff");
        powderAmount = serializedObject.FindProperty("powderAmount");
        powderIntensity = serializedObject.FindProperty("powderIntensity");

        // temporal upsampling properties
        temporalUpsampling = serializedObject.FindProperty("temporalUpsampling");
        blendingCoeff = serializedObject.FindProperty("blendingCoeff");
    }
    public override void OnInspectorGUI() 
    {
        serializedObject.Update();

        // the crucial parameters that need to be set by the user
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(container, new GUIContent("Container"));
        EditorGUILayout.PropertyField(renderingShader, new GUIContent("Cloud rendering shader"));
        EditorGUILayout.PropertyField(blendingShader, new GUIContent("Environment blending shader"));
        EditorGUILayout.Space();

        // the main properties of the cloud shader
        EditorGUILayout.LabelField("--- Main ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(tileSize, new GUIContent("Cloud size"));
        EditorGUILayout.PropertyField(speed, new GUIContent("Speed"));
        EditorGUILayout.PropertyField(color, new GUIContent("Color"));
        EditorGUILayout.Slider(absorptionCoeff, 0, 1, new GUIContent("Absorption"));
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // shape properties
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

        // lighting properties
        EditorGUILayout.LabelField("--- Lighting ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(cloudIntensity, new GUIContent("Light intensity"));
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
        // if the phase function is chosen, choose its properties
        if (phaseType.intValue == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(henyeyRatio, 0, 1, new GUIContent("Weight"));
            EditorGUILayout.PropertyField(henyeyIntensity, new GUIContent("Intensity"));
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

        // the performance parameters 
        EditorGUILayout.LabelField("--- Performance ---", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Ray march");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(rayMarchStepSize, new GUIContent("Step size"));
        EditorGUILayout.PropertyField(rayMarchDecrease, new GUIContent("Step size decrease"));
        useBlueNoiseRay.boolValue = EditorGUILayout.Toggle("Blue Noise",  useBlueNoiseRay.boolValue);
        // if blue noise for raymarch is chosen, choose its settings
        if (useBlueNoiseRay.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(blueNoiseRayAmount, 1, 2, new GUIContent("Amount"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Light march");
        EditorGUI.indentLevel++;
        EditorGUILayout.IntSlider(lightMarchSteps, 1, 4, new GUIContent("Number of steps"));
        EditorGUILayout.Slider(lightMarchDecrease, 1, 10, new GUIContent("Step size decrease"));
        useBlueNoiseLight.boolValue = EditorGUILayout.Toggle("Blue Noise", useBlueNoiseLight.boolValue);
        // if blue noise for lightmarch is chosen, choose its settings
        if (useBlueNoiseLight.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(blueNoiseLightAmount, 0, (float) 0.5, new GUIContent("Amount"));
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
        temporalUpsampling.boolValue = EditorGUILayout.Toggle("Temporal Upsampling", temporalUpsampling.boolValue);
        // if temporal upsampling is chosen, choose its settings
        if (temporalUpsampling.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(blendingCoeff, 0, 1, new GUIContent("Blending Coefficient"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
        
    }
}
