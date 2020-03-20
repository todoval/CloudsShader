using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CloudGenerator : MonoBehaviour
{

   // public Shader shader;

    public ComputeShader noiseCompShader;

    public Color color;

    private int handleTintMain;

    public RenderTexture noiseTexture = null;

    public GameObject container;

   // public Material material;

    // Start is called before the first frame update
    void Start()
    {
        if (null == noiseCompShader) 
        {
            Debug.Log("Shader missing.");
            enabled = false;
            return;
        }

        handleTintMain = noiseCompShader.FindKernel("CSMain");

        if (handleTintMain < 0)
        {
            Debug.Log("Initialization failed.");
            enabled = false;
            return;
        }
        
        noiseTexture = new RenderTexture(64, 64, 0);
        noiseTexture.enableRandomWrite = true;  

        noiseTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
        noiseTexture.volumeDepth = 64;
        noiseTexture.dimension = TextureDimension.Tex3D;
        noiseTexture.Create();
        noiseCompShader.SetVector("Color", (Vector4)color);
        noiseCompShader.SetTexture(handleTintMain, "Result", noiseTexture);

        Material mat = new Material();
        container.GetComponent<Renderer>().material.mainTexture = noiseTexture;
    }

    // Update is called once per frame
    void Update()
    {
        noiseCompShader.Dispatch(handleTintMain, 8, 8, 8);
    }

    void OnDestroy() 
    {
        // if the noise texture exists, destroy it
        if (null != noiseTexture) {
            noiseTexture.Release();
            noiseTexture = null;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {      
       /* if (null == noiseCompShader || handleTintMain < 0 || null == source) 
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
            noiseTexture = new RenderTexture(64, 64, 0);
            noiseTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
            noiseTexture.volumeDepth = 64;
            noiseTexture.enableRandomWrite = true;
            noiseTexture.dimension = TextureDimension.Tex3D;
            noiseTexture.Create();
        }

        // call the compute shader

        noiseCompShader.SetVector("Color", (Vector4)color);
        noiseCompShader.SetTexture(handleTintMain, "Result", noiseTexture);
        noiseCompShader.Dispatch(handleTintMain, 8, 8, 8);*/

        // copy the result
        /* material.SetTexture("NoiseTex", noiseTexture);
        material.SetVector("lowerBound", container.position - container.localScale/2);
        material.SetVector("upperBound", container.position + container.localScale/2);*/

        //Graphics.Blit(noiseTexture, destination);
        //Graphics.Blit(source, destination, material);
    }
}