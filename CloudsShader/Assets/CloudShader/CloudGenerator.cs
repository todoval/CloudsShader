using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

//ExecuteInEditMode,
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
    private bool upsamplingBoolChanged;

    // shader properties
    public Material renderingMaterial;
    public Material blendingMaterial;
    public Shader renderingShader;
    public Shader blendingShader;

    // debugging
    Vector3 lastDragPosition;


    // Start is called before the first frame update
    void Start()
    {
        previousVP = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;
        cloud = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
        cloudLastFrame = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
        upsamplingBoolChanged = false;
    }

    void UpdateDrag()
    {
        if (Input.GetMouseButtonDown(2))
            lastDragPosition = Input.mousePosition;
        if (Input.GetMouseButton(2))
        {
            var delta = lastDragPosition - Input.mousePosition;
            transform.Translate(delta * Time.deltaTime * 0.25f * 20);
            lastDragPosition = Input.mousePosition;
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateDrag();
        Vector3 pivotPointWorld = new Vector3(0,0,0);
         if (Input.GetMouseButtonDown (1)) {
            Vector3 pivotPointScreen = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(pivotPointScreen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
                pivotPointWorld = hit.point;
            Cursor.visible = false;
        }
        else if(Input.GetMouseButtonUp(1))
            Cursor.visible = true;
        if (Input.GetMouseButton (1)) {
            //Rotate the camera X wise
            float angularSpeed = 2;
            cam.transform.RotateAround(pivotPointWorld,Vector3.up, angularSpeed * Input.GetAxis ("Mouse X"));
            //Rotate the camera Y wise
            cam.transform.RotateAround(pivotPointWorld,Vector3.right, angularSpeed * Input.GetAxis ("Mouse Y"));
        }

        // also debugging purposes, for moving the camera
        if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(new Vector3(speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(new Vector3(-speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(new Vector3(0,-speed * Time.deltaTime,0));
        }
        if(Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(new Vector3(0,speed * Time.deltaTime,0));
        }

        float zoomSpeed = 200;
        // Mouse wheel moving forwards
        var mouseScroll = Input.GetAxis("Mouse ScrollWheel");

        if (mouseScroll!=0)
        {
            transform.Translate(transform.forward * mouseScroll * zoomSpeed * Time.deltaTime, Space.Self);
        }
    }

    void OnDestroy() 
    {
    }

    void OnEnable()
    {
        previousVP = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;
        cloud = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
        cloudLastFrame = new RenderTexture(1920, 1080, 24, RenderTextureFormat.Default);
    }

    // for measuring fps
    void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 100, 100), ((int)(1.0f / Time.smoothDeltaTime)).ToString());        
    }

    void CustomBlit(RenderTexture source, RenderTexture dest, Material mat)
    {
        float camFov = cam.fieldOfView;
        float camAspect = cam.aspect;

        float fovWHalf = camFov * 0.5f;

        var cameraTransform = cam.transform;
        Vector3 toRight = camAspect * Mathf.Tan(fovWHalf * Mathf.Deg2Rad) * cameraTransform.right;
        Vector3 toTop = Mathf.Tan(fovWHalf * Mathf.Deg2Rad) * cameraTransform.up;
        //direction of rays
        var forward = cameraTransform.forward;
        Vector3 topLeft = forward - toRight + toTop;
        Vector3 topRight = forward + toRight + toTop;
        Vector3 bottomRight = forward + toRight - toTop;
        Vector3 bottomLeft = forward - toRight - toTop;

        RenderTexture.active = dest;

        GL.PushMatrix();
        GL.LoadOrtho();

        mat.SetPass(0);

        GL.Begin(GL.QUADS);

        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.MultiTexCoord(1, bottomLeft);
        GL.Vertex3(0.0f, 0.0f, 0.0f);

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.MultiTexCoord(1, bottomRight);
        GL.Vertex3(1.0f, 0.0f, 0.0f);

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.MultiTexCoord(1, topRight);
        GL.Vertex3(1.0f, 1.0f, 0.0f);

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.MultiTexCoord(1, topLeft);
        GL.Vertex3(0.0f, 1.0f, 0.0f);

        GL.End();
        GL.PopMatrix();
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
        renderingMaterial.SetFloat("cloudIntensity", cloudIntensity);
        renderingMaterial.SetInt("useLight", lightingType == 2 || (sceneLight == null && lightingType == 1) ? 0 : 1);
        // if the user has chosen to use sun, get sun settings
        if (lightingType == 0)
        {
            Light sun = RenderSettings.sun;
            if (sun.isActiveAndEnabled)
            {
                renderingMaterial.SetVector("lightPosition", sun.transform.position);
                renderingMaterial.SetVector("lightColor", sun.color);
                renderingMaterial.SetFloat("lightIntensity", sun.intensity);
            }
            else
                renderingMaterial.SetInt("useLight", 0);
        }
        else if (lightingType == 1 && sceneLight != null) // otherwise get the settings of the light the user has set
        {
            if (sceneLight.isActiveAndEnabled)
            {
                renderingMaterial.SetVector("lightPosition", sceneLight.transform.position);
                renderingMaterial.SetVector("lightColor", sceneLight.color);
                renderingMaterial.SetFloat("lightIntensity", sceneLight.intensity);
            }
            else
                renderingMaterial.SetInt("useLight", 0);
        }

        // phase function settings
        if (phaseType == 1)
            henyeyRatio = 0;
        renderingMaterial.SetFloat("henyeyCoeff", henyeyCoeff);
        renderingMaterial.SetFloat("henyeyRatio", henyeyRatio);
        renderingMaterial.SetFloat("henyeyIntensity", henyeyIntensity);

        renderingMaterial.SetFloat("powderCoeff", powderCoeff);
        renderingMaterial.SetFloat("powderAmount", powderAmount);
        renderingMaterial.SetFloat("powderIntensity", powderIntensity);

        renderingMaterial.SetFloat("absorptionCoef", absorptionCoeff);

        // performance settings
        renderingMaterial.SetInt("useBlueNoiseRay", useBlueNoiseRay ? 1 : 0);
        renderingMaterial.SetInt("useBlueNoiseLight", useBlueNoiseLight ? 1 : 0);
        renderingMaterial.SetFloat("blueNoiseRayAmount", blueNoiseRayAmount);
        renderingMaterial.SetFloat("blueNoiseLightAmount", blueNoiseLightAmount);
        renderingMaterial.SetInt("lightMarchSteps", lightMarchSteps);
        renderingMaterial.SetFloat("lightMarchDecrease", lightMarchDecrease);
        renderingMaterial.SetFloat("rayMarchStepSize", rayMarchStepSize);
        renderingMaterial.SetFloat("rayMarchDecrease", rayMarchDecrease);

        // set other cloud properties
        renderingMaterial.SetVector("cloudColor", color);
        renderingMaterial.SetFloat("speed", speed);
        renderingMaterial.SetFloat("tileSize", tileSize);

        // shape properties
        renderingMaterial.SetFloat("detailAmount", detailAmount);
        renderingMaterial.SetFloat("maxDetailModifier", detailModifier);
        renderingMaterial.SetFloat("densityConstant", densityConstant);
        renderingMaterial.SetFloat("cloudHeightModifier", cloudHeightModifier);
        renderingMaterial.SetFloat("cloudMaxHeight", cloudMaxHeight);
        renderingMaterial.SetFloat("cloudBottomModifier", cloudBottomModifier);

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
        renderingMaterial.SetInt("useTemporalUpsampling", temporalUpsampling ? 1 : 0);

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
            Graphics.Blit(source, destination, renderingMaterial); // blend the cloud texture with background
    }
}