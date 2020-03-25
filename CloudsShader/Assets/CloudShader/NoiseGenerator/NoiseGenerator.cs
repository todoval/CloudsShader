using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class NoiseGenerator : MonoBehaviour
{

    public int resolution = 64;
    public ComputeShader PerlinCompShader;
    public ComputeShader slicer;
    public RenderTexture noiseTexture = null;
    private int noiseKernel;
    private int slicerKernel;


    void Start()
    {
        if (null == PerlinCompShader || slicer == null) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        slicerKernel = slicer.FindKernel("Slicer");
        noiseKernel = PerlinCompShader.FindKernel("PerlinNoise");

        if (noiseKernel < 0 || slicerKernel < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  
    }

    // a helper function returning only one 2D slice (defined by layer) from source
    RenderTexture Copy3DSliceToRenderTexture(RenderTexture source, int layer)
    {
        // create new 2D RenderTexture
        RenderTexture render = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
        render.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        render.enableRandomWrite = true;
        render.wrapMode = TextureWrapMode.Repeat;
        render.Create();

        // insert one slice of the 3D Texture to the 2D textures with the slicer compute shader
        int kernelIndex = slicer.FindKernel("Slicer");
        slicer.SetTexture(kernelIndex, "voxels", source);
        slicer.SetInt("layer", layer);
        slicer.SetTexture(kernelIndex, "Result", render);
        slicer.Dispatch(kernelIndex, 8, 8, 1);

        return render;
    }

    // 2D renderTexture to Texture2D conversion
    Texture2D ConvertFromRenderTexture(RenderTexture renderTex)
    {
        Texture2D output = new Texture2D(resolution, resolution);
        RenderTexture.active = renderTex;
        output.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        output.Apply();
        return output;
    }

    // convert the input RenderTexture to Texture3D
    void SaveRenderTex (RenderTexture source)
    {
        // create an array of 2D RenderTextures
        RenderTexture[] layers = new RenderTexture[resolution];
        // slice 3D RenderTexture into this array
        for( int i = 0; i < 64; i++)        
            layers[i] = Copy3DSliceToRenderTexture(source, i);

        // transform the 2D RenderTexture into Texture2D
        Texture2D[] finalSlices = new Texture2D[resolution];
        for ( int i = 0; i < 64; i++)        
            finalSlices[i] = ConvertFromRenderTexture(layers[i]);

        // create a new 3D Texture and fill it with the contents of the slices
        Texture3D output = new Texture3D(resolution, resolution, resolution, TextureFormat.ARGB32, true);
        output.filterMode = FilterMode.Trilinear;
        Color[] outputPixels = output.GetPixels();

        for (int k = 0; k < resolution; k++)
        {
            Color[] layerPixels = finalSlices[k].GetPixels();
            for (int i = 0; i < resolution; i++)
                for (int j = 0; j < resolution; j++)
                    outputPixels[i + j * resolution + k * resolution * resolution] = layerPixels[i + j * resolution];
        }
 
        output.SetPixels(outputPixels);
        output.Apply();
        AssetDatabase.CreateAsset(output, "Assets/Resources/noise.asset");
    }

    void createNewNoise()
    {
        if (null == PerlinCompShader || noiseKernel < 0 || slicerKernel < 0)
        {
            Debug.Log("Error creating new noise.");
            return;
        }

        // create noiseTexture
        if (null == noiseTexture) 
        {
            noiseTexture = new RenderTexture(resolution, resolution, 0);
            noiseTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            noiseTexture.volumeDepth = resolution;
            noiseTexture.enableRandomWrite = true;
            noiseTexture.dimension = TextureDimension.Tex3D;
            noiseTexture.Create();
        }

        // call the noise compute shader which saves the noise texture into noiseTexture variable
        PerlinCompShader.SetTexture(noiseKernel, "Result", noiseTexture);
        PerlinCompShader.Dispatch(noiseKernel, 8, 8, 8);

        SaveRenderTex(noiseTexture);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            createNewNoise();
        }
    }
}
