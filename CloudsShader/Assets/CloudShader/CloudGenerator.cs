using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

//ExecuteInEditMode,
[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{
    public Light mainLight;

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
        // if the noise texture exists, destroy it
        
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

        Light sun = RenderSettings.sun;

        // set parameters to the shader
        Texture3D detailTexture = LoadTexture("DetailNoise");
        Texture3D shapeTexture = LoadTexture("ShapeNoise");

        // sun settings
        material.SetVector("sunPosition", sun.transform.position);
        material.SetVector("sunColor", sun.color);
        material.SetFloat("sunIntensity", sun.intensity);


        material.SetVector("lightPos", mainLight.transform.position);
        material.SetTexture("ShapeTexture", shapeTexture);
        material.SetTexture("DetailTexture", detailTexture);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}