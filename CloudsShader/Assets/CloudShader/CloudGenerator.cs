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
    public float henyeyIntensity;
    public float powderCoeff;
    public float powderAmount;
    public float powderIntensity;
    public float cloudIntensity;

    // shape properties
    public float detailAmount;
    public float detailModifier;
    public float densityConstant;
    public float cloudMaxHeight;
    public float cloudHeightModifier;
    public float cloudBottomModifier;

    // cloud properties
    public float absorptionCoeff;
    public int tileSize;
    public float speed;
    public Color color;

    public Transform container;

    // performance properties
    public bool useBlueNoiseLight;
    public bool useBlueNoiseRay;
    public float blueNoiseLightAmount;
    public float blueNoiseRayAmount;
    public int lightMarchSteps;

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
        // also debugging purposes, for moving the camera
        if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(new Vector3(speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(new Vector3(-speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(new Vector3(0,-speed * Time.deltaTime,0));
        }
        if(Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(new Vector3(0,speed * Time.deltaTime,0));
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.Translate(0, 0, scroll * 100, Space.World);
    }

    void OnDestroy() 
    {
    }

    // for measuring fps
    void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 100, 100), ((int)(1.0f / Time.smoothDeltaTime)).ToString());        
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

        // lighting settings
        material.SetFloat("cloudIntensity", cloudIntensity);
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
        material.SetFloat("henyeyIntensity", henyeyIntensity);

        material.SetFloat("powderCoeff", powderCoeff);
        material.SetFloat("powderAmount", powderAmount);
        material.SetFloat("powderIntensity", powderIntensity);

        material.SetFloat("absorptionCoef", absorptionCoeff);

        // performance settings
        material.SetInt("useBlueNoiseRay", useBlueNoiseRay ? 1 : 0);
        material.SetInt("useBlueNoiseLight", useBlueNoiseLight ? 1 : 0);
        material.SetFloat("blueNoiseRayAmount", blueNoiseRayAmount);
        material.SetFloat("blueNoiseLightAmount", blueNoiseLightAmount);
        material.SetInt("lightMarchSteps", lightMarchSteps);

        // set other cloud properties
        material.SetVector("cloudColor", color);
        material.SetFloat("speed", speed);
        material.SetFloat("tileSize", tileSize);

        // shape properties
        material.SetFloat("detailAmount", detailAmount);
        material.SetFloat("maxDetailModifier", detailModifier);
        material.SetFloat("densityConstant", densityConstant);
        material.SetFloat("cloudHeightModifier", cloudHeightModifier);
        material.SetFloat("cloudMaxHeight", cloudMaxHeight);
        material.SetFloat("cloudBottomModifier", cloudBottomModifier);

        // set all textures the shader needs
        Texture3D detailTexture = LoadTexture3D("DetailNoise");
        Texture3D shapeTexture = LoadTexture3D("ShapeNoise");
        Texture2D WeatherMap = LoadTexture2D("WeatherMap");
        Texture2D blueNoiseTex = LoadTexture2D("BlueNoise");

        material.SetTexture("ShapeTexture", shapeTexture);
        material.SetTexture("BlueNoise", blueNoiseTex);
        material.SetTexture("DetailTexture", detailTexture);
        material.SetTexture("WeatherMap", WeatherMap);
        material.SetVector("containerBound_Min", container.position - container.localScale/2);
        material.SetVector("containerBound_Max", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}