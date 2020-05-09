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
    public int weatherMapResolution = 512;

    public ComputeShader NoiseTextureGenerator;
    public ComputeShader slicer;
    public ComputeBuffer worleyFeaturePointsBuffer;

    public SaveTexture textureSaver;
    
    public RenderTexture weatherMap = null;
    public RenderTexture detailTexture = null;
    public RenderTexture shapeTexture = null;

    private int shapeTextureKernel;
    private int detailTextureKernel;
    private int weatherMapKernel;
    private int slicerKernel;
    private int detailKernel;

    // perlin noise settings of the shape texture
    public int shapePerlinTextureResolution;
    public int shapePerlinOctaves;
    public float shapePerlinPersistence;
    public float shapePerlinLacunarity;
    public float shapePerlinFrequency;

    // Worley noise settings of the shape texture
    public int shapeGreenChannelOctaves;
    public int shapeBlueChannelOctaves;
    public int shapeAlphaChannelOctaves;

    public int shapeGreenChannelCellSize;
    public int shapeBlueChannelCellSize;
    public int shapeAlphaChannelCellSize;

    // worley noise settings of the detail texture
    public int detailGreenChannelOctaves;
    public int detailBlueChannelOctaves;
    public int detailRedChannelOctaves;

    public int detailGreenChannelCellSize;
    public int detailBlueChannelCellSize;
    public int detailRedChannelCellSize;

    // weather map options
    public int coveragePerlinTextureResolution;
    public int coveragePerlinOctaves;
    public float coveragePerlinPersistence;
    public float coveragePerlinLacunarity;
    public float coveragePerlinFrequency;

    public int coverageOption; // 0 - constant, 1 - perlin
    public float coverageConstant;
    public float cloudHeight;
    public float cloudType;

    void Start()
    {
        if (null == NoiseTextureGenerator || slicer == null) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        slicerKernel = slicer.FindKernel("Slicer");
        shapeTextureKernel = NoiseTextureGenerator.FindKernel("ShapeTextureGen");
        weatherMapKernel = NoiseTextureGenerator.FindKernel("WeatherMapGen");
        detailTextureKernel = NoiseTextureGenerator.FindKernel("DetailTextureGen");

        if (weatherMapKernel < 0 || shapeTextureKernel < 0 || slicerKernel < 0 || detailTextureKernel < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  
        // create the buffer for worley noise with feature point offsets
        CreateWorleyPointsBuffer();
    }

    void OnDestroy()
    {
        if (worleyFeaturePointsBuffer != null)
            worleyFeaturePointsBuffer.Release();
    }

    void CreateWorleyPointsBuffer ()
    {
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

    private void prepForNewRenderTexture()
    {
        // initialize the texture saver
        textureSaver = new SaveTexture();
        textureSaver.slicer = slicer;
        textureSaver.slicerKernel = slicerKernel;

        CreateWorleyPointsBuffer();
        if(weatherMapKernel < 0 || detailTextureKernel < 0 || shapeTextureKernel < 0 || slicerKernel < 0 || null == NoiseTextureGenerator)
        {
            Debug.Log("Error creating new noise.");
        }
    }

    public void createDetailNoise()
    {
        prepForNewRenderTexture();
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
        // set the properties of the worley cells
        NoiseTextureGenerator.SetInt("detailCellSizeGreen", detailGreenChannelCellSize);
        NoiseTextureGenerator.SetInt("detailCellSizeBlue", detailBlueChannelCellSize);
        NoiseTextureGenerator.SetInt("detailCellSizeRed", detailRedChannelCellSize);
        NoiseTextureGenerator.SetInt("detailRedOctaves", detailRedChannelOctaves);
        NoiseTextureGenerator.SetInt("detailGreenOctaves", detailGreenChannelOctaves);
        NoiseTextureGenerator.SetInt("detailBlueOctaves", detailBlueChannelOctaves);
        int numThreadGroups = detailNoiseResolution/8;
        NoiseTextureGenerator.Dispatch(detailTextureKernel, numThreadGroups, numThreadGroups, numThreadGroups);
        textureSaver.SaveRenderTex(detailTexture, "DetailNoise", detailNoiseResolution);
    }

    public void createShapeNoise()
    {
        prepForNewRenderTexture();

        // create noiseTexture
        shapeTexture = null;
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
        NoiseTextureGenerator.SetInt("shapeCellSizeGreen", shapeGreenChannelCellSize);
        NoiseTextureGenerator.SetInt("shapeCellSizeBlue", shapeBlueChannelCellSize);
        NoiseTextureGenerator.SetInt("shapeCellSizeAlpha", shapeAlphaChannelCellSize);
        NoiseTextureGenerator.SetInt("shapeGreenOctaves", shapeGreenChannelOctaves);
        NoiseTextureGenerator.SetInt("shapeBlueOctaves", shapeBlueChannelOctaves);
        NoiseTextureGenerator.SetInt("shapeAlphaOctaves", shapeAlphaChannelOctaves);

        // set the properties of the perlin noise
        NoiseTextureGenerator.SetInt("perlinTextureResolution", shapePerlinTextureResolution);
        NoiseTextureGenerator.SetInt("perlinOctaves", shapePerlinOctaves);
        NoiseTextureGenerator.SetFloat("perlinFrequency", shapePerlinFrequency);
        NoiseTextureGenerator.SetFloat("perlinPersistence", shapePerlinPersistence);
        NoiseTextureGenerator.SetFloat("perlinLacunarity", shapePerlinLacunarity);
        int threadGroups = shapeNoiseResolution / 8;
        NoiseTextureGenerator.Dispatch(shapeTextureKernel, threadGroups, threadGroups, threadGroups);
        textureSaver.SaveRenderTex(shapeTexture, "ShapeNoise", shapeNoiseResolution);
    }

    Texture2D RenderTextureToTexture2D(RenderTexture rTex, int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, true);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();
        return tex;
    }

    public void createWeatherMap()
    {
        prepForNewRenderTexture();
        weatherMap = null;
        if (null == weatherMap) 
        {
            weatherMap = new RenderTexture(weatherMapResolution, weatherMapResolution, 0);
            weatherMap.enableRandomWrite = true;
            weatherMap.dimension = TextureDimension.Tex2D;
            weatherMap.Create();
        }

        NoiseTextureGenerator.SetBuffer(weatherMapKernel, "FeaturePoints", worleyFeaturePointsBuffer);
        NoiseTextureGenerator.SetTexture(weatherMapKernel, "ResultWeatherMap", weatherMap);
        NoiseTextureGenerator.SetInt("coveragePerlinOctaves",coveragePerlinOctaves);
        NoiseTextureGenerator.SetInt("coveragePerlinTextureResolution",coveragePerlinTextureResolution);
        NoiseTextureGenerator.SetFloat("coveragePerlinPersistence", coveragePerlinPersistence);
        NoiseTextureGenerator.SetFloat("coveragePerlinLacunarity", coveragePerlinLacunarity);
        NoiseTextureGenerator.SetFloat("coveragePerlinFrequency", coveragePerlinFrequency);
        NoiseTextureGenerator.SetBool("coverageOption", (coverageOption == 0) ? false : true);
        NoiseTextureGenerator.SetFloat("coverageConstant", coverageConstant);
        NoiseTextureGenerator.SetFloat("cloudHeight", cloudHeight);
        NoiseTextureGenerator.SetFloat("cloudType", cloudType);

        int threadGroups =  weatherMapResolution / 8;
        NoiseTextureGenerator.Dispatch(weatherMapKernel, 64, 64, 1);
        Texture2D weatherMapAs2D = RenderTextureToTexture2D(weatherMap, weatherMapResolution);
        AssetDatabase.CreateAsset(weatherMapAs2D, "Assets/Resources/WeatherMap.asset");   
    }
}
