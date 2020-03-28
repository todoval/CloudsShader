using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

//ExecuteInEditMode,
[ImageEffectAllowedInSceneView]
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

    Texture3D CreateTexture3D (int size)
    {
        Color[] colorArray = new Color[size * size * size];
        Texture3D texture = new Texture3D (size, size, size, TextureFormat.RGBA32, true);
        float r = 1.0f / (size - 1.0f);
        for (int x = 0; x < size; x++) {
            for (int y = 0; y < size; y++) {
                for (int z = 0; z < size; z++) {
                    Color c = new Color (x * r, y * r, z * r, 0);
                    colorArray[x + (y * size) + (z * size * size)] = c;
                }
            }
        }
        texture.SetPixels (colorArray);
        texture.Apply ();
        return texture;
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
        Texture3D perlin = LoadTexture("PerlinNoise");
        Texture3D worley = LoadTexture("WorleyNoise");
        material.SetTexture("NoiseTex", worley);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}