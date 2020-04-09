using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class NoiseGenerator : MonoBehaviour
{
    public int tempRes = 64;
    public int shapeNoiseResolution = 128;
    public int worleyPointsPerRes = 128;

    public ComputeShader PerlinCompShader;
    public ComputeShader SimplexCompShader;
    public ComputeShader WorleyCompShader;
    public ComputeShader slicer;
    public ComputeShader randomNumberGenerator;

    public ComputeBuffer worleyFeaturePointsBuffer;
    
    public RenderTexture perlinTexture = null;
    public RenderTexture worleyTexture = null;
    public RenderTexture simplexTexture = null;

    private int perlinKernel;
    private int simplexKernel;
    private int rndNumberKernel;
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
        if (null == PerlinCompShader || null == SimplexCompShader ||slicer == null || null == WorleyCompShader || null == randomNumberGenerator) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        shapeNoiseResolution = 128;
        worleyPointsPerRes = 128;

        slicerKernel = slicer.FindKernel("Slicer");
        simplexKernel = SimplexCompShader.FindKernel("SimplexNoise");
        rndNumberKernel = randomNumberGenerator.FindKernel("RandomNumberGenerator");
        perlinKernel = PerlinCompShader.FindKernel("PerlinNoise");
        worleyNoiseKernel = WorleyCompShader.FindKernel("WorleyNoise");

        if (simplexKernel < 0 | perlinKernel < 0 || slicerKernel < 0 || worleyNoiseKernel < 0)
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
            float randomX = (float) prng.NextDouble (); //* worleyPointsPerRes;
            float randomY = (float) prng.NextDouble (); //* worleyPointsPerRes;
            float randomZ = (float) prng.NextDouble (); //* worleyPointsPerRes;
            points[i] = new Vector3( randomX, randomY,randomZ);
        }
        worleyFeaturePointsBuffer = new ComputeBuffer( numberOfPoints, sizeof(float) * 3);
        worleyFeaturePointsBuffer.SetData(points);
        Debug.Log(numberOfPoints);
        //Debug.Log(points[16*16*16]);
    }

    void createPerlinNoise()
    {
        if (null == perlinTexture) 
        {
            perlinTexture = new RenderTexture(tempRes, tempRes, 0);
            perlinTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            perlinTexture.volumeDepth = tempRes;
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
        PerlinCompShader.SetVector("mask", new Vector4(1,0,0,0));
        PerlinCompShader.Dispatch(perlinKernel, 8, 8, 8);
        SaveRenderTex(perlinTexture, "PerlinNoise", tempRes);
    }

    void createSimplexNoise()
    {
        if (null == simplexTexture) 
        {
            simplexTexture = new RenderTexture(tempRes, tempRes, 0);
            simplexTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            simplexTexture.volumeDepth = tempRes;
            simplexTexture.enableRandomWrite = true;
            simplexTexture.dimension = TextureDimension.Tex3D;
            simplexTexture.Create();
        }

        // call the simplex compute shader which saves the simplex texture into the simplexTexture variable
        SimplexCompShader.SetTexture(simplexKernel, "Result", simplexTexture);
        SimplexCompShader.Dispatch(simplexKernel, 8,8,8);
        SaveRenderTex(simplexTexture, "SimplexNoise", tempRes);
    }

    void createWorleyNoise()
    {
        // create noiseTexture
        if (null == worleyTexture) 
        {
            worleyTexture = new RenderTexture(shapeNoiseResolution, shapeNoiseResolution, 0);
            worleyTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            worleyTexture.volumeDepth = shapeNoiseResolution;
            worleyTexture.enableRandomWrite = true;
            worleyTexture.dimension = TextureDimension.Tex3D;
            worleyTexture.Create();
        }

        // call the worley compute shader which saves the worley texture into worleyTexture variable
        WorleyCompShader.SetBuffer(worleyNoiseKernel, "FeaturePoints", worleyFeaturePointsBuffer);
        WorleyCompShader.SetTexture(worleyNoiseKernel, "Result", worleyTexture);
        int threadGroups = 16;//= shapeNoiseResolution / 8;
        WorleyCompShader.Dispatch(worleyNoiseKernel, threadGroups, threadGroups, threadGroups);
        SaveRenderTex(worleyTexture, "WorleyNoise", shapeNoiseResolution);
    }

    void updateNoiseTextures()
    {
        if (null == PerlinCompShader || rndNumberKernel < 0 || perlinKernel < 0 || worleyNoiseKernel < 0 || slicerKernel < 0)
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
            CreateWorleyPointsBuffer();
            updateNoiseTextures();
        }
    }

    void OnDestroy()
    {
        if (worleyFeaturePointsBuffer != null)
            worleyFeaturePointsBuffer.Release();
    }
}
