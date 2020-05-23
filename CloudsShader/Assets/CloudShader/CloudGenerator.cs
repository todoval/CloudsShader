using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudGenerator : MonoBehaviour
{
    // variables used for light manipulation
    public int lightingType;
    public Light sceneLight;
    public int phaseType;
    public float henyeyCoeff;
    public float henyeyRatio;
    public float henyeyIntensity;
    public float powderCoeff;
    public float powderAmount;
    public float powderIntensity;
    public float cloudIntensity;

    // shape properties
    public float detailAmount;
    public float detailModifier;
    public float densityConstant;
    public float cloudMaxHeight;
    public float cloudHeightModifier;
    public float cloudBottomModifier;

    // cloud properties
    public float absorptionCoeff;
    public int tileSize;
    public float speed;
    public Color color;
    public Transform container;

    // performance properties
    public bool useBlueNoiseLight;
    public bool useBlueNoiseRay;
    public float blueNoiseLightAmount;
    public float blueNoiseRayAmount;
    public int lightMarchSteps;
    public float lightMarchDecrease;
    public float rayMarchStepSize;
    public float rayMarchDecrease;
    
    // temporal upsampling
    private Matrix4x4 previousVP;
    private Camera cam;
    private RenderTexture cloudLastFrame;
    private RenderTexture cloud;
    public bool temporalUpsampling;
    public float blendingCoeff;

    // shader properties
    public Material renderingMaterial;
    public Material blendingMaterial;
    public Shader renderingShader;
    public Shader blendingShader;

    // Start is called before the first frame update
    void Start()
    {
        previousVP = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.Log("Cannot use temporal upsampling without camera");
            return;                
        }
        cam.depthTextureMode = DepthTextureMode.Depth;
        cloud = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
        cloudLastFrame = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
    }
    
    void OnEnable()
    {
        previousVP = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.Log("Cannot use temporal upsampling without camera");
            return;                
        }
        cam.depthTextureMode = DepthTextureMode.Depth;
        cloud = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
        cloudLastFrame = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
    }

    // for measuring fps
    void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 100, 100), ((int)(1.0f / Time.smoothDeltaTime)).ToString());        
    }

    // customized blit used only when doing temporal upsampling
    void CustomBlit(RenderTexture source, RenderTexture dest, Material mat)
    {
        float fieldOfView = cam.fieldOfView;
        float camAspect = cam.aspect;

        float halfFOV = fieldOfView/2;

        var cameraTransform = cam.transform;
        Vector3 toRight = camAspect * Mathf.Tan(halfFOV * Mathf.Deg2Rad) * cameraTransform.right;
        Vector3 toTop = Mathf.Tan(halfFOV * Mathf.Deg2Rad) * cameraTransform.up;
        
        // direction of rays
        var forward = cameraTransform.forward;
        Vector3 topLeft = forward - toRight + toTop;
        Vector3 topRight = forward + toRight + toTop;
        Vector3 bottomRight = forward + toRight - toTop;
        Vector3 bottomLeft = forward - toRight - toTop;

        // when applying an image effect we shade a Quad with our rendered screen output (rendertexture) on it
        // this part is from vhttp://www.thelazydev.net/82_/shading-toolbox-3-unity-depth-buffer-and-depth-blur/
        RenderTexture.active = dest; // set the destination renderTexture as active

        GL.PushMatrix(); // calculate MVP Matrix and push it to the GL stack
        GL.LoadOrtho(); // set up Ortho-Perspective Transform

        mat.SetPass(0); // start the first rendering pass

        GL.Begin(GL.QUADS); // begin rendering quads

        GL.MultiTexCoord2(0, 0.0f, 0.0f); // prepare input struct (Texcoord0 (UV's)) for this vertex
        GL.MultiTexCoord(1, bottomLeft);
        GL.Vertex3(0.0f, 0.0f, 0.0f); // finalize and submit this vertex for rendering (bottom left)

        GL.MultiTexCoord2(0, 1.0f, 0.0f); // prepare input struct (Texcoord0 (UV's)) for this vertex
        GL.MultiTexCoord(1, bottomRight);
        GL.Vertex3(1.0f, 0.0f, 0.0f); // finalize and submit this vertex for rendering (bottom right)

        GL.MultiTexCoord2(0, 1.0f, 1.0f); // prepare input struct (Texcoord0 (UV's)) for this vertex
        GL.MultiTexCoord(1, topRight);
        GL.Vertex3(1.0f, 1.0f, 0.0f); // finalize and submit this vertex for rendering (top right)

        GL.MultiTexCoord2(0, 0.0f, 1.0f); // prepare input struct (Texcoord0 (UV's)) for this vertex
        GL.MultiTexCoord(1, topLeft);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // finalize and submit this vertex for rendering (top left)

        GL.End(); // finalize drawing the Quad
        GL.PopMatrix(); // pop the matrices off the stack
    }

    Texture3D LoadTexture3D(string name)
    {
        Texture3D texture = (Texture3D) Resources.Load(name, typeof(Texture3D));
        return texture;
    }

    Texture2D LoadTexture2D(string name)
    {
        Texture2D texture = (Texture2D) Resources.Load(name, typeof(Texture2D));
        return texture;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // first check whether all settings are alright, if not return and output an error
        if (renderingShader == null)
        {
            Debug.Log("Cloud Rendering Shader missing");
            return;
        }

        if (blendingShader == null && temporalUpsampling)
        {
            Debug.Log("Environmental Blending Shader missing");
            return;
        }

        if (null == source) 
        {
            Graphics.Blit(source, destination); // just copy the source
            return;
        }

        // create the materials
        if (renderingMaterial == null || renderingMaterial.shader != renderingShader)
            renderingMaterial = new Material(renderingShader);
        if (blendingMaterial == null || blendingMaterial.shader != blendingShader)
            blendingMaterial = new Material(blendingShader);

        // lighting settings
        renderingMaterial.SetFloat("_absorptionCoef", absorptionCoeff);
        renderingMaterial.SetFloat("_cloudIntensity", cloudIntensity);
        renderingMaterial.SetInt("_useLight", lightingType == 2 || (sceneLight == null && lightingType == 1) ? 0 : 1);
        // if the user has chosen to use sun, get sun settings
        if (lightingType == 0)
        {
            Light sun = RenderSettings.sun;
            if (sun.isActiveAndEnabled)
            {
                renderingMaterial.SetVector("_lightPosition", sun.transform.position);
                renderingMaterial.SetVector("_lightColor", sun.color);
                renderingMaterial.SetFloat("_lightIntensity", sun.intensity);
            }
            else
                renderingMaterial.SetInt("_useLight", 0);
        }
        else if (lightingType == 1 && sceneLight != null) // otherwise get the settings of the light the user has set
        {
            if (sceneLight.isActiveAndEnabled)
            {
                renderingMaterial.SetVector("_lightPosition", sceneLight.transform.position);
                renderingMaterial.SetVector("_lightColor", sceneLight.color);
                renderingMaterial.SetFloat("_lightIntensity", sceneLight.intensity);
            }
            else
                renderingMaterial.SetInt("_useLight", 0);
        }

        // phase function settings
        if (phaseType == 1)
            henyeyRatio = 0;
        renderingMaterial.SetFloat("_henyeyCoeff", henyeyCoeff);
        renderingMaterial.SetFloat("_henyeyRatio", henyeyRatio);
        renderingMaterial.SetFloat("_henyeyIntensity", henyeyIntensity);

        // powder effect settings
        renderingMaterial.SetFloat("_powderCoeff", powderCoeff);
        renderingMaterial.SetFloat("_powderAmount", powderAmount);
        renderingMaterial.SetFloat("_powderIntensity", powderIntensity);

        // performance settings
        renderingMaterial.SetInt("_useBlueNoiseRay", useBlueNoiseRay ? 1 : 0);
        renderingMaterial.SetInt("_useBlueNoiseLight", useBlueNoiseLight ? 1 : 0);
        renderingMaterial.SetFloat("_blueNoiseRayAmount", blueNoiseRayAmount);
        renderingMaterial.SetFloat("_blueNoiseLightAmount", blueNoiseLightAmount);
        renderingMaterial.SetInt("_lightMarchSteps", lightMarchSteps);
        renderingMaterial.SetFloat("_lightMarchDecrease", lightMarchDecrease);
        renderingMaterial.SetFloat("_rayMarchStepSize", rayMarchStepSize);
        renderingMaterial.SetFloat("_rayMarchDecrease", rayMarchDecrease);

        // set other cloud properties
        renderingMaterial.SetVector("_cloudColor", color);
        renderingMaterial.SetFloat("_speed", speed);
        renderingMaterial.SetFloat("_tileSize", tileSize);

        // shape properties
        renderingMaterial.SetFloat("_detailAmount", detailAmount);
        renderingMaterial.SetFloat("_maxDetailModifier", detailModifier);
        renderingMaterial.SetFloat("_densityConstant", densityConstant);
        renderingMaterial.SetFloat("_cloudHeightModifier", cloudHeightModifier);
        renderingMaterial.SetFloat("_cloudMaxHeight", cloudMaxHeight);
        renderingMaterial.SetFloat("_cloudBottomModifier", cloudBottomModifier);

        // set all textures the shader needs
        Texture3D detailTexture = LoadTexture3D("DetailNoise");
        Texture3D shapeTexture = LoadTexture3D("ShapeNoise");
        Texture2D WeatherMap = LoadTexture2D("WeatherMap");
        Texture2D blueNoiseTex = LoadTexture2D("BlueNoise");

        renderingMaterial.SetTexture("ShapeTexture", shapeTexture);
        renderingMaterial.SetTexture("BlueNoise", blueNoiseTex);
        renderingMaterial.SetTexture("DetailTexture", detailTexture);
        renderingMaterial.SetTexture("WeatherMap", WeatherMap);
        renderingMaterial.SetVector("containerBound_Min", container.position - container.localScale/2);
        renderingMaterial.SetVector("containerBound_Max", container.position + container.localScale/2);

        // set the temporal upsampling properties
        renderingMaterial.SetInt("_useTemporalUpsampling", temporalUpsampling ? 1 : 0);
        renderingMaterial.SetFloat("_temporalBlendFactor", blendingCoeff);

        if (temporalUpsampling)
        {
            // for the purposes of temporal upsampling
            renderingMaterial.SetMatrix("_LastVP", previousVP); // set the previous matrix to the current rendering shader
            renderingMaterial.SetTexture("_LastCloudTex", cloudLastFrame); // set the last texture to the current rendering shader
            CustomBlit(null, cloud, renderingMaterial); // render the current clouds
            Graphics.CopyTexture(cloud, cloudLastFrame); // copy the current clouds to the last frame texture
            blendingMaterial.SetTexture("_CloudTex", cloud);
            Graphics.Blit(source, destination, blendingMaterial); // blend the cloud texture with background
            previousVP = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix; // set the new projection matrix
        }
        else
            Graphics.Blit(source, destination, renderingMaterial); // only do a simple one pass rendering
    }
}