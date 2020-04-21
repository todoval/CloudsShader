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

    public SaveTexture textureSaver;
    
    public RenderTexture detailTexture = null;
    public RenderTexture shapeTexture = null;

    private int shapeTextureKernel;
    private int detailTextureKernel;
    private int rndNumberKernel;
    private int slicerKernel;
    private int detailKernel;

    // perlin noise settings

    public int perlinTextureResolution;
    public int perlinOctaves;
    public float perlinPersistence;
    public float perlinLacunarity;
    public float perlinFrequency;
    // Worley noise settings

    public int greenChannelOctaves;
    public int blueChannelOctaves;
    public int alphaChannelOctaves;

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
        shapeTextureKernel = NoiseTextureGenerator.FindKernel("ShapeTextureGen");
        detailTextureKernel = NoiseTextureGenerator.FindKernel("DetailTextureGen");

        if (shapeTextureKernel < 0 || slicerKernel < 0 || detailTextureKernel < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  

        textureSaver = new SaveTexture();
        textureSaver.slicer = slicer;
        textureSaver.slicerKernel = slicerKernel;
        
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
        textureSaver.SaveRenderTex(detailTexture, "DetailNoise", detailNoiseResolution);
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
        NoiseTextureGenerator.SetInt("greenChannelOctaves", greenChannelOctaves);
        NoiseTextureGenerator.SetInt("blueChannelOctaves", blueChannelOctaves);
        NoiseTextureGenerator.SetInt("alphaChannelOctaves", alphaChannelOctaves);

        // set the properties of the perlin noise
        NoiseTextureGenerator.SetInt("perlinTextureResolution", perlinTextureResolution);
        NoiseTextureGenerator.SetInt("perlinOctaves", perlinOctaves);
        NoiseTextureGenerator.SetFloat("perlinFrequency", perlinFrequency);
        NoiseTextureGenerator.SetFloat("perlinPersistence", perlinPersistence);
        NoiseTextureGenerator.SetFloat("perlinLacunarity", perlinLacunarity);
        int threadGroups = shapeNoiseResolution / 8;
        NoiseTextureGenerator.Dispatch(shapeTextureKernel, threadGroups, threadGroups, threadGroups);
        textureSaver.SaveRenderTex(shapeTexture, "ShapeNoise", shapeNoiseResolution);
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
