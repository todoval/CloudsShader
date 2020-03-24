using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{
    int resolution = 64;


    public RenderTexture noiseTexture = null;
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
        if (null != noiseTexture) {
            noiseTexture.Release();
            noiseTexture = null;
        }
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
        
        // create noiseTexture
        if (null == noiseTexture || source.width != noiseTexture.width 
            || source.height != noiseTexture.height) 
        {
            if (null != noiseTexture)
            {
                noiseTexture.Release();
            }
            noiseTexture = new RenderTexture(resolution, resolution, 0);
            noiseTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            noiseTexture.volumeDepth = resolution;
            noiseTexture.enableRandomWrite = true;
            noiseTexture.dimension = TextureDimension.Tex3D;
            noiseTexture.Create();
        }

        // set parameters to the shader
        Texture3D tex = LoadTexture("noise");
        material.SetTexture("NoiseTex", tex);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}