using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{
    int resolution = 64;
    static public ComputeBuffer colorsBuffer;   

    public Color color;


    public RenderTexture noiseTexture = null;
    public Transform container;

    // shader properties
    public Material material;
    public Shader shader;

    public Texture3D texture;

    // Start is called before the first frame update
    void Start()
    {
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

    Texture3D LoadTexture(string name)
    {
        Texture3D texture = (Texture3D) Resources.Load("noise", typeof(Texture3D));
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

        // call the noise compute shader which saves the noise texture into noiseTexture variable
       /* noiseCompShader.SetVector("Color", (Vector4)color);
        noiseCompShader.SetTexture(noiseKernel, "Result", noiseTexture);
        noiseCompShader.SetBuffer(noiseKernel, "colors", colorsBuffer);
        noiseCompShader.Dispatch(noiseKernel, 8, 8, 1);*/

        // set parameters to the shader
        Texture3D tex = LoadTexture("noise");
        material.SetTexture("NoiseTex", tex);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);

        // apply the shader to the source and copy it to destination
        Graphics.Blit(source, destination, material);
    }
}