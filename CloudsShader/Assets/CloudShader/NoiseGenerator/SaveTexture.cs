using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class SaveTexture
{
    public ComputeShader slicer;
    public int slicerKernel;

    // convert the input RenderTexture to Texture3D
    public void SaveRenderTex (RenderTexture source, string textureName, int resolution)
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


    // a helper function returning only one 2D slice (defined by layer) from source
    private RenderTexture Copy3DSliceToRenderTexture(RenderTexture source, int layer, int resolution)
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
    private Texture2D ConvertFromRenderTexture(RenderTexture renderTex, int resolution)
    {
        Texture2D output = new Texture2D(resolution, resolution);
        RenderTexture.active = renderTex;
        output.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        output.Apply();
        return output;
    }

}