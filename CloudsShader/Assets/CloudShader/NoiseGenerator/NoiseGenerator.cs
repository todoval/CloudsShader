using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class NoiseGenerator : MonoBehaviour
{

    public int resolution = 64;
    public int worleyResolution = 64;
    public int worleyPointsPerRes = 8;

    public ComputeShader PerlinCompShader;
    public ComputeShader WorleyCompShader;
    public ComputeShader slicer;

    public ComputeBuffer worleyFeaturePointsBuffer;
    
    public RenderTexture perlinTexture = null;
    public RenderTexture worleyTexture = null;

    public RenderTexture tempRes = null;

    private int perlinKernel;
    private int worleyNoiseKernel;
    private int slicerKernel;

    // Perlin noise settings

    public int PerlinRes = 8;
    public int PerlinOctaves = 8;
    public float PerlinPersistence = 0.6f;
    public float PerlinLacunarity = 2.0f;

    // Worley noise settings

    void Start()
    {
        if (null == PerlinCompShader || slicer == null || null == WorleyCompShader) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        slicerKernel = slicer.FindKernel("Slicer");
        perlinKernel = PerlinCompShader.FindKernel("PerlinNoise");
        worleyNoiseKernel = WorleyCompShader.FindKernel("WorleyNoise");

        if (perlinKernel < 0 || slicerKernel < 0 || worleyNoiseKernel < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  
        
        CreateWorleyPointsBuffer();
        //worleyFeaturePointsBuffer = new ComputeBuffer( worleyResolution * worleyResolution * worleyResolution, sizeof(float) * 3);
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
    void SaveRenderTex (RenderTexture source, string textureName)
    {
        // create an array of 2D RenderTextures
        RenderTexture[] layers = new RenderTexture[resolution];
        // slice 3D RenderTexture into this array
        for( int i = 0; i < resolution; i++)        
            layers[i] = Copy3DSliceToRenderTexture(source, i);

        // transform the 2D RenderTexture into Texture2D
        Texture2D[] finalSlices = new Texture2D[resolution];
        for ( int i = 0; i < resolution; i++)        
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
        AssetDatabase.CreateAsset(output, "Assets/Resources/" + textureName + ".asset");
    }

    void createPerlinNoise()
    {
        if (null == perlinTexture) 
        {
            perlinTexture = new RenderTexture(resolution, resolution, 0);
            perlinTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            perlinTexture.volumeDepth = resolution;
            perlinTexture.enableRandomWrite = true;
            perlinTexture.dimension = TextureDimension.Tex3D;
            perlinTexture.Create();
        }

        // call the perlin compute shader which saves the perlin texture into perlinTexture variable
        PerlinCompShader.SetTexture(perlinKernel, "Result", perlinTexture);
        PerlinCompShader.SetInt("texRes", PerlinRes);
        PerlinCompShader.SetInt("octaves", PerlinOctaves);
        PerlinCompShader.SetFloat("persistence", PerlinPersistence);
        PerlinCompShader.SetFloat("lacunarity", PerlinLacunarity);
        PerlinCompShader.Dispatch(perlinKernel, 8, 8, 8);
        SaveRenderTex(perlinTexture, "PerlinNoise");
    }

    void CreateWorleyPointsBuffer ()
    {
        System.Random prng = new System.Random (1);
        int numberOfPoints = worleyPointsPerRes * worleyPointsPerRes * worleyPointsPerRes;
        var points = new Vector3[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            float randomX = (float) prng.NextDouble () * worleyPointsPerRes;
            float randomY = (float) prng.NextDouble () * worleyPointsPerRes;
            float randomZ = (float) prng.NextDouble () * worleyPointsPerRes;
            points[i] = new Vector3( (int)randomX, (int)randomY, (int)randomZ);
        }
        worleyFeaturePointsBuffer = new ComputeBuffer( numberOfPoints, sizeof(float) * 3);
        worleyFeaturePointsBuffer.SetData(points);
    }

    void createWorleyNoise()
    {
        // create noiseTexture
        if (null == worleyTexture) 
        {
            worleyTexture = new RenderTexture(resolution, resolution, 0);
            worleyTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            worleyTexture.volumeDepth = resolution;
            worleyTexture.enableRandomWrite = true;
            worleyTexture.dimension = TextureDimension.Tex3D;
            worleyTexture.Create();
        }

        // call the worley compute shader which saves the worley texture into worleyTexture variable
        WorleyCompShader.SetBuffer(worleyNoiseKernel, "FeaturePoints", worleyFeaturePointsBuffer);
        WorleyCompShader.SetTexture(worleyNoiseKernel, "Result", worleyTexture);
        WorleyCompShader.SetInt("FeatPointBufferSize", worleyPointsPerRes);
        WorleyCompShader.Dispatch(worleyNoiseKernel, 8, 8, 8);
        SaveRenderTex(worleyTexture, "WorleyNoise");
    }

    void updateNoiseTextures()
    {
        if (null == PerlinCompShader || perlinKernel < 0 || worleyNoiseKernel < 0 || slicerKernel < 0)
        {
            Debug.Log("Error creating new noise.");
            return;
        }

        // create all noise textures in the Resources folder
        createPerlinNoise();
        createWorleyNoise();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            updateNoiseTextures();
        }
    }

    void OnDestroy()
    {
        if (worleyFeaturePointsBuffer != null)
            worleyFeaturePointsBuffer.Release();
    }
}
