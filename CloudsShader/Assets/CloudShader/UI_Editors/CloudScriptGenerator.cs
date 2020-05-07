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
        EditorGUILayout.PropertyField(container);
        EditorGUILayout.Slider(absorptionCoeff, 0, 1, new GUIContent("Absorption"));
        EditorGUILayout.PropertyField(tileSize, new GUIContent("Tile size"));
        EditorGUILayout.PropertyField(speed, new GUIContent("Speed"));
        EditorGUILayout.PropertyField(color);

        // draw a line between above lighting settings
        DrawUILine(new Color((float)0.5,(float)0.5,(float)0.5,1), 1, 10);

        EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
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

        // draw a line between above performance settings
        DrawUILine(new Color((float)0.5,(float)0.5,(float)0.5,1), 1, 10);

        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.Toggle("Default settings", true);
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
        
    }
}
