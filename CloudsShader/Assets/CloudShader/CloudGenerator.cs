using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

//ExecuteInEditMode,
[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{
    // variables used for light manipulation
    public int lightingType;
    public Light sceneLight;
    public int phaseType;
    public float henyeyCoeff;
    public float henyeyRatio;

    // cloud properties
    public float absorptionCoeff;
    public int tileSize;
    public float speed;
    public Color color;
    public float rotation;

    public Transform container;

    // shader properties
    public Material material;
    public Shader shader;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy() 
    {
    }

    Texture3D LoadTexture3D(string name)
    {
        Texture3D texture = (Texture3D) Resources.Load(name, typeof(Texture3D));
        return texture;
    }

    Texture2D LoadTexture2D(string name)
    {
        Texture2D texture = (Texture2D) Resources.Load(name, typeof(Texture2D));
        return texture;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {      
        if (null == source) 
        {
            Graphics.Blit(source, destination); // just copy the source
            return;
        }

        // create the material
        if (material == null || material.shader != shader)
        {
            material = new Material (shader);
        }

        // set parameters to the shader
        Texture3D detailTexture = LoadTexture3D("DetailNoise");
        Texture3D shapeTexture = LoadTexture3D("ShapeNoise");
        Texture2D WeatherMap = LoadTexture2D("WeatherMap");

        // lighting settings
        material.SetInt("useLight", lightingType == 2 || (sceneLight == null && lightingType == 1) ? 0 : 1);
        // if the user has chosen to use sun, get sun settings
        if (lightingType == 0)
        {
            Light sun = RenderSettings.sun;
            if (sun.isActiveAndEnabled)
            {
                material.SetVector("lightPosition", sun.transform.position);
                material.SetVector("lightColor", sun.color);
                material.SetFloat("lightIntensity", sun.intensity);
            }
            else
                material.SetInt("useLight", 0);
        }
        else if (lightingType == 1 && sceneLight != null) // otherwise get the settings of the light the user has set
        {
            if (sceneLight.isActiveAndEnabled)
            {
                material.SetVector("lightPosition", sceneLight.transform.position);
                material.SetVector("lightColor", sceneLight.color);
                material.SetFloat("lightIntensity", sceneLight.intensity);
            }
            else
                material.SetInt("useLight", 0);
        }

        // phase function settings
        if (phaseType == 1)
            henyeyRatio = 0;
        material.SetFloat("henyeyCoeff", henyeyCoeff);
        material.SetFloat("henyeyRatio", henyeyRatio);
        material.SetFloat("absorptionCoef", absorptionCoeff);

        // performance settings

        // set other cloud properties
        material.SetVector("cloudColor", color);
        material.SetFloat("speed", speed);
        material.SetFloat("tileSize", tileSize);
        material.SetFloat("rotation", rotation);

        material.SetTexture("ShapeTexture", shapeTexture);
        material.SetTexture("DetailTexture", detailTexture);
        material.SetTexture("WeatherMap", WeatherMap);
        material.SetVector("containerBound_Min", container.position - container.localScale/2);
        material.SetVector("containerBound_Max", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}