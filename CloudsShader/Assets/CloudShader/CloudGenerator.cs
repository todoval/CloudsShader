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
    public int tileSize;

    public float speed;
    public Color color;

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

    Texture3D LoadTexture(string name)
    {
        Texture3D texture = (Texture3D) Resources.Load(name, typeof(Texture3D));
        return texture;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {      
        if (null == source) 
        {
            // just copy the source
            Graphics.Blit(source, destination); 
            return;
        }

        // create the material
        if (material == null || material.shader != shader) {
            material = new Material (shader);
        }

        // set parameters to the shader
        Texture3D detailTexture = LoadTexture("DetailNoise");
        Texture3D shapeTexture = LoadTexture("ShapeNoise");

        // sun settings
        material.SetInt("useLight", lightingType == 2 || (sceneLight == null && lightingType == 1) ? 0 : 1);
        // if the user has chosen to use sun, get sun settings
        if (lightingType == 0)
        {
            Light sun = RenderSettings.sun;
            material.SetVector("lightPosition", sun.transform.position);
            material.SetVector("lightColor", sun.color);
            material.SetFloat("lightIntensity", sun.intensity);
        }
        else if (lightingType == 1 && sceneLight != null) // otherwise get the settings of the light the user has set
        {
            material.SetVector("lightPosition", sceneLight.transform.position);
            material.SetVector("lightColor", sceneLight.color);
            material.SetFloat("lightIntensity", sceneLight.intensity);
        }

        // set other cloud properties
        material.SetVector("cloudColor", color);
        material.SetFloat("speed", speed);
        material.SetFloat("tileSize", tileSize);

        material.SetTexture("ShapeTexture", shapeTexture);
        material.SetTexture("DetailTexture", detailTexture);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}