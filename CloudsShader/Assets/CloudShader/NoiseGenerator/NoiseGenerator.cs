using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class NoiseGenerator : MonoBehaviour
{
    public int shapeNoiseResolution = 128;
    public int detailNoiseResolution = 32;
    public int worleyPointsPerRes = 128;

    public ComputeShader NoiseTextureGenerator;
    public ComputeShader slicer;
    public ComputeShader randomNumberGenerator;
    public ComputeBuffer worleyFeaturePointsBuffer;

    
    public RenderTexture detailTexture = null;
    public RenderTexture shapeTexture = null;

    private int shapeTextureKernel;
    private int detailTextureKernel;
    private int rndNumberKernel;
    private int slicerKernel;
    private int detailKernel;

    // Perlin noise settings

    public int PerlinResolution = 16;
    public int PerlinOctaves = 8;
    public float PerlinPersistence = 0.6f;
    public float PerlinLacunarity = 2.0f;

    // Worley noise settings

    public int greenChannelCellSize = 32;
    public int blueChannelCellSize = 16;
    public int alphaChannelCellSize = 8;

    void Start()
    {
        if (null == NoiseTextureGenerator || slicer == null || null == randomNumberGenerator) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        shapeNoiseResolution = 128;
        detailNoiseResolution = 32;
        worleyPointsPerRes = 128;

        slicerKernel = slicer.FindKernel("Slicer");
        rndNumberKernel = randomNumberGenerator.FindKernel("RandomNumberGenerator");
        detailTextureKernel = NoiseTextureGenerator.FindKernel("DetailTextureGen");
        shapeTextureKernel = NoiseTextureGenerator.FindKernel("ShapeTextureGen");

        if (shapeTextureKernel < 0 || slicerKernel < 0 || detailTextureKernel < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  
        
        // create the buffer for worley noise with feature point offsets
        CreateWorleyPointsBuffer();
    }
    

    // a helper function returning only one 2D slice (defined by layer) from source
    RenderTexture Copy3DSliceToRenderTexture(RenderTexture source, int layer, int resolution)
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
        slicer.Dispatch(kernelIndex, 16, 16, 1);

        return render;
    }

    // 2D renderTexture to Texture2D conversion
    Texture2D ConvertFromRenderTexture(RenderTexture renderTex, int resolution)
    {
        Texture2D output = new Texture2D(resolution, resolution);
        RenderTexture.active = renderTex;
        output.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        output.Apply();
        return output;
    }

    void OnDestroy()
    {
        if (worleyFeaturePointsBuffer != null)
            worleyFeaturePointsBuffer.Release();
    }

    // convert the input RenderTexture to Texture3D
    void SaveRenderTex (RenderTexture source, string textureName, int resolution)
    {
        // create an array of 2D RenderTextures
        RenderTexture[] layers = new RenderTexture[resolution];
        // slice 3D RenderTexture into this array
        for( int i = 0; i < resolution; i++)        
            layers[i] = Copy3DSliceToRenderTexture(source, i, resolution);

        // transform the 2D RenderTexture into Texture2D
        Texture2D[] finalSlices = new Texture2D[resolution];
        for ( int i = 0; i < resolution; i++)        
            finalSlices[i] = ConvertFromRenderTexture(layers[i], resolution);

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

    void CreateWorleyPointsBuffer ()
    {
        //worleyFeaturePointsBuffer = new ComputeBuffer( worleyPointsPerRes * worleyPointsPerRes * worleyPointsPerRes, sizeof(float) * 3);
      //  randomNumberGenerator.SetBuffer(rndNumberKernel, "FeaturePointOffsets", worleyFeaturePointsBuffer);
       // randomNumberGenerator.Dispatch(rndNumberKernel, 8, 8, 8);

        //int numberOfPoints = worleyPointsPerRes * worleyPointsPerRes * worleyPointsPerRes;
        //worleyFeaturePointsBuffer = new ComputeBuffer( numberOfPoints, sizeof(float) * 3);
        System.Random prng = new System.Random (1);
        int numberOfPoints = worleyPointsPerRes * worleyPointsPerRes * worleyPointsPerRes;
        var points = new Vector3[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            float randomX = (float) prng.NextDouble ();
            float randomY = (float) prng.NextDouble ();
            float randomZ = (float) prng.NextDouble ();
            points[i] = new Vector3( randomX, randomY,randomZ);
        }
        worleyFeaturePointsBuffer = new ComputeBuffer( numberOfPoints, sizeof(float) * 3);
        worleyFeaturePointsBuffer.SetData(points);
    }

    void createDetailNoise()
    {
        if (null == detailTexture) 
        {
            detailTexture = new RenderTexture(detailNoiseResolution, detailNoiseResolution, 0);
            detailTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            detailTexture.volumeDepth = detailNoiseResolution;
            detailTexture.enableRandomWrite = true;
            detailTexture.dimension = TextureDimension.Tex3D;
            detailTexture.Create();
        }

        // call the perlin compute shader which saves the perlin texture into perlinTexture variable
        NoiseTextureGenerator.SetBuffer(detailTextureKernel, "FeaturePoints", worleyFeaturePointsBuffer);
        NoiseTextureGenerator.SetTexture(detailTextureKernel, "Result", detailTexture);
        NoiseTextureGenerator.Dispatch(detailTextureKernel, 4, 4, 4);
        SaveRenderTex(detailTexture, "DetailNoise", detailNoiseResolution);
    }

    void createShapeNoise()
    {
        // create noiseTexture
        if (null == shapeTexture) 
        {
            shapeTexture = new RenderTexture(shapeNoiseResolution, shapeNoiseResolution, 0);
            shapeTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            shapeTexture.volumeDepth = shapeNoiseResolution;
            shapeTexture.enableRandomWrite = true;
            shapeTexture.dimension = TextureDimension.Tex3D;
            shapeTexture.Create();
        }

        // call the worley compute shader which saves the worley texture into shapeTexture variable
        NoiseTextureGenerator.SetBuffer(shapeTextureKernel, "FeaturePoints", worleyFeaturePointsBuffer);
        NoiseTextureGenerator.SetTexture(shapeTextureKernel, "Result", shapeTexture);

        // set the properties of the worley cells
        NoiseTextureGenerator.SetInt("cellSizeGreenShape", greenChannelCellSize);
        NoiseTextureGenerator.SetInt("cellSizeBlueShape", blueChannelCellSize);
        NoiseTextureGenerator.SetInt("cellSizeAlphaShape", alphaChannelCellSize);
        // set the properties of the perlin noise
        NoiseTextureGenerator.SetInt("perlinTextureResolution", PerlinResolution);
        NoiseTextureGenerator.SetInt("perlinOctaves", PerlinOctaves);
        NoiseTextureGenerator.SetFloat("perlinPersistence", PerlinPersistence);
        NoiseTextureGenerator.SetFloat("perlinLacunarity", PerlinLacunarity);
        int threadGroups = shapeNoiseResolution / 8;
        NoiseTextureGenerator.Dispatch(shapeTextureKernel, threadGroups, threadGroups, threadGroups);
        SaveRenderTex(shapeTexture, "ShapeNoise", shapeNoiseResolution);
    }

    void updateNoiseTextures()
    {
        if (null == NoiseTextureGenerator || rndNumberKernel < 0 || detailTextureKernel < 0 || shapeTextureKernel < 0 || slicerKernel < 0)
        {
            Debug.Log("Error creating new noise.");
            return;
        }

        // create all noise textures in the Resources folder
        createShapeNoise();
        createDetailNoise();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            CreateWorleyPointsBuffer();
            updateNoiseTextures();
        }
    }
}
