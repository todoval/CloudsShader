using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{

    public Shader shader;

    public ComputeShader noiseCompShader;

    static public ComputeBuffer colorsBuffer;   

    public Color color;

    private int handleTintMain;

    public RenderTexture noiseTexture = null;

    public Transform container;

    public Material material;

    // Start is called before the first frame update
    void Start()
    {
        if (null == noiseCompShader) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }
        
        if (null != colorsBuffer)
            colorsBuffer.Release();
        Color[] colorArray;	
        colorArray = new Color[256];
		int i = 0;
		while (i < colorArray.Length){
			colorArray[i] = new Color(0, 0, 0, 1);
			if (i >= 0 && i < 128)
				colorArray[i] += new Color(0, 0, Mathf.PingPong(i * 4, 256) / 256, 1);
			if (i >= 64 && i < 192)
				colorArray[i] += new Color(0, Mathf.PingPong((i - 64) * 4, 256) / 256, 0, 1);
			if (i >= 128 && i < 256)
				colorArray[i] += new Color(Mathf.PingPong(i * 4, 256) / 256, 0, 0, 1);
			i++;
		}

        colorsBuffer = new ComputeBuffer(colorArray.Length, 4 * 4); // Color size is four values of four bytes, so 4 * 4
        colorsBuffer.SetData(colorArray);

        handleTintMain = noiseCompShader.FindKernel("CSMain");
        
        if (handleTintMain < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }  
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
        if (null != colorsBuffer)
        {
            colorsBuffer.Release();
            colorsBuffer = null;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {      
        if (null == noiseCompShader || handleTintMain < 0 || null == source) 
        {
            Graphics.Blit(source, destination); // just copy
            return;
        }

        if (material == null || material.shader != shader) {
            material = new Material (shader);
        }
        
        // do we need to create a new temporary destination render texture?
        if (null == noiseTexture || source.width != noiseTexture.width 
            || source.height != noiseTexture.height) 
        {
            if (null != noiseTexture)
            {
                noiseTexture.Release();
            }
            noiseTexture = new RenderTexture(64, 64, 1);
           // noiseTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
          //  noiseTexture.volumeDepth = 64;
            noiseTexture.enableRandomWrite = true;
            noiseTexture.dimension = TextureDimension.Tex2D;
            noiseTexture.Create();
        }

        // call the compute shader

        noiseCompShader.SetVector("Color", (Vector4)color);
        noiseCompShader.SetTexture(handleTintMain, "Result", noiseTexture);
        noiseCompShader.SetBuffer(handleTintMain, "colors", colorsBuffer);
        //noiseCompShader.SetTexture(handleTintMain, "Source", source);
        noiseCompShader.Dispatch(handleTintMain, 8, 8, 1);

        // copy the result
        //material.SetTexture("NoiseTex", noiseTexture);
        material.SetTexture("_NoiseTex", noiseTexture);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        Graphics.Blit(source, destination, material);
    }
}