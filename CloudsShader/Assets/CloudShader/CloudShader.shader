
Shader "CloudShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define PI 3.141592653

            struct VertInput
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertToFrag
            {
                float2 uv : TEXCOORD0;
                float4 vertex : POSITION;
                half3 color : COLOR;
                // viewVector represents the ray from camera to the current point
                float3 viewVector : TEXCOORD1;
            };

            VertToFrag vert (VertInput v)
            {
                VertToFrag o;
                o.vertex = UnityObjectToClipPos(v.pos);
                o.uv = v.uv;
                o.color = v.pos.xyz;
                // transform the v.uv vector from projection space to camera space
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                // transform view vector from camera space to world space
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return o;
            }

            //cloud properties
            float4 cloudColor;
            float speed;
            float tileSize;
            float absorptionCoef;

            // texture and sampler properties
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            SamplerState samplerShapeTexture;
            SamplerState samplerDetailTexture;

            Texture3D<float4> ShapeTexture;
            Texture3D<float4> DetailTexture;

            // container properties
            float3 containerBound_Min;
            float3 containerBound_Max;

            // sun and light properties
            bool useLight;
            float3 lightPosition;
            float4 lightColor;
            float lightIntensity;

            // structures used for storage of results from different functions
            struct rayContainerInfo
            {
                bool intersectedBox;
                float dstInsideBox; // 0 if does not intersect box
                float dstToBox; // 0 if inside box
            };

            struct raymarchInfo
            {
                float transmittance;
                float4 density;
            };

            /*
            input:
                rayOrigin - the start of the ray (camera position)
                rayDir - the direction of the incoming ray 
            outputs a rayContainerInfo structure
            */
            rayContainerInfo getRayContainerInfo(float3 rayOrigin, float3 rayDir)
            {
                // this function implements the AABB algorithm (e.g. https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection)
                
                // tA and tB are from the line equation of the ray: rayOrigin + tA*rayDir
                float3 tA = (containerBound_Min - rayOrigin) / rayDir; // for the point on boundsMin - A
                float3 tB = (containerBound_Max - rayOrigin) / rayDir; // for the point on boundsMax - B

                // compare components by pairs and save results
                float3 tmin = min(tA, tB);
                float3 tmax = max(tA, tB);
                
                // get intersection distance - dstFirst for the first intersection with box, dstSecond for the second
                float dstFirst = max(max(tmin.x, tmin.y), tmin.z);
                float dstSecond = min(tmax.x, min(tmax.y, tmax.z));

                rayContainerInfo containerInfo;
                // ray intersected the box if the first distance is smaller than the second
                containerInfo.intersectedBox = (dstFirst > dstSecond) ? false : true;
                // ray intersected the box from the outside if 0 <= dstFirst <= dstSecond
                containerInfo.dstToBox = max(0, dstFirst);
                // ray intersected the box from the inside if dstFirst < 0 < dstSecond (dstA < 0 < dstB)
                containerInfo.dstInsideBox = max(0, dstSecond - containerInfo.dstToBox);

                return containerInfo;
            }

            // a helper function that returns true if the given point is inside the container
            bool isInsideBox(float3 position, float3 rayDir)
            {
                rayContainerInfo containerInfo = getRayContainerInfo(position, rayDir);
                return (containerInfo.dstInsideBox > 0);
            }

            // returns the distance between two positions in 3D
            float getDistance(float3 A, float3 B)
            {
                return sqrt(pow(A.x-B.x, 2) + pow(A.y-B.y, 2) + pow(A.z-B.z, 2));
            }

            // returns the cloud density at given point
            float getDensity(float3 position)
            {
                position+= _Time * speed;
                float4 shapeDensity = ShapeTexture.SampleLevel(samplerShapeTexture, position/(128)*tileSize, 0);
                float4 detailDensity = DetailTexture.SampleLevel(samplerDetailTexture, position/(128)*tileSize, 0);
                float detailColor = (detailDensity.g + detailDensity.b + detailDensity.r)/4;
                float shapeColor = (shapeDensity.a + shapeDensity.g + shapeDensity.b) * shapeDensity.r;
                return (shapeColor - detailColor) * 2;
            }

            // implementing the phase function, cosAngle is the cosine of the angle between two vectors, g is a parameter in [-1,1]  
            float getHenyeyGreenstein(float cosAngle, float g)
            {
                return (1 - g*g)/( 4*PI* sqrt(pow(1 + g*g - 2*g*cosAngle, 3)));
            }

            float phase(float cosAngle) {
                float blend = .5;
                float hgBlend = getHenyeyGreenstein(cosAngle,0.8); // * (1-blend) + getHenyeyGreenstein(a,-0.3) * blend;
                return 0.8 + hgBlend*0.2; // base brightness + phase factor * 
            }

            float getIncidentLighting(float3 pos, float3 incVector)
            {
                // normalized vector from my position to light position
                float3 dirVector = float3(lightPosition.x, lightPosition.y, lightPosition.z) - pos;
                dirVector = dirVector / length(dirVector);

                // get intersection with the cloud container
                rayContainerInfo containerInfo = getRayContainerInfo(lightPosition, -dirVector);
                float3 entryPoint = lightPosition - dirVector * containerInfo.dstToBox;

                // light marching, march in the direction from the entry point to my point
                float3 currPoint = entryPoint;
                // number of steps should be the same for each light march
                float distanceToMarch = getDistance(entryPoint, pos);
                float noOfSteps = 4;
                float stepSize = distanceToMarch/noOfSteps;
                // if light is inside box, set number of steps accordingly
                /*if (getDistance(noOfSteps * stepSize * dirVector + currPoint, pos) > getDistance(lightPosition, pos))
                    stepSize = getDistance(lightPosition, pos)/noOfSteps;*/
                
                float accumDensity = 0; // accumulated density over all the ray from my point to the entry point
                float transmittance = 1;
                float absorptionCoef = 0.6;
                while (noOfSteps > 0)
                {
                    float density = getDensity(currPoint) * stepSize; // get the density for the current part of the ray
                    if (density > 0)
                        accumDensity += density;
                    
                    // take another step in the direction of the light
                    currPoint += dirVector * stepSize;
                    noOfSteps --;
                }
                // use the beer's law for the light attenuation (from the Fredrik Haggstrom paper)
                float lightAttenuation = exp(-accumDensity * absorptionCoef);
                lightAttenuation = 0.2 + lightAttenuation * 0.8;

                // get cosine of the angle between incDir and dirVector
                float cosAngle = dot(dirVector, incVector)/ (length(dirVector) * length(incVector));
                return lightAttenuation; // * getHenyeyGreenstein(cosAngle, 0.6);
            }


            // ray marching, implementation mostly from Palenik
            raymarchInfo raymarch(float3 entryPoint, float3 rayDir)
            {
                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float stepSize = 0.2;
                float4 totalDensity = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching

               // float cosAngle = dot(rayDir, _WorldSpaceLightPos0.xyz);
                //float phaseVal = phase(cosAngle);

                while (isInsideBox(currPoint, rayDir))
                {
                    float density = getDensity(currPoint); //density = color that is sampled from the noise texture
                    if (density > 0)
                    {
                        float incLight = 0;
                        if (useLight)
                            incLight = getIncidentLighting(currPoint, rayDir); // use the light marching algorithm to get the light from the light source
                            
                        // approximate the attenuation of light with the Beer-Lambert's law
                        float3 lightVector = currPoint - float3(lightPosition.x, lightPosition.y, lightPosition.z);
                        lightVector = lightVector / length(lightVector);
                        float cosAngle = dot(rayDir, lightVector);
                        float phaseVal = phase(cosAngle);

                        float deltaT = exp(-absorptionCoef * stepSize * density);

                        // lower the transmittance as you march further away from the viewer
                        transmittance *= deltaT;
                        if (transmittance < 0.01) // break if transmittance is too low to avoid performance problems
                            break;


                        // raymarching render equation
                        totalDensity += density * stepSize * transmittance * absorptionCoef * incLight * phaseVal;
                    }

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                }
                raymarchInfo result;
                result.transmittance = transmittance;
                result.density = totalDensity;
                return result;
            }

            fixed4 frag (VertToFrag i) : COLOR
            {
                // get the normalized ray direction
                float viewLength = length(i.viewVector);
                float3 rayDir = i.viewVector / viewLength;

                // ray starts at the camera position
                float3 rayOrigin = _WorldSpaceCameraPos;

                // get the information about the intersection of ray and the container
                rayContainerInfo containerInfo = getRayContainerInfo(rayOrigin, rayDir);
                
                // return base if the box was not intersected 
                float4 base = tex2D(_MainTex, i.uv);
                
                //if there are other objects, do not render clouds
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonLinearDepth) * viewLength;

                if (!containerInfo.intersectedBox && !containerInfo.dstToBox < depth)
                    return base;

                float3 entryPoint = rayOrigin + rayDir * containerInfo.dstToBox; // intersection with the cloud container
                raymarchInfo raymarchInfo = raymarch(entryPoint, rayDir); // raymarch through the clouds and get the density and transmittance

                // TO DO - eliminate bounding artifacts with depth
                float4 result = raymarchInfo.transmittance * base + raymarchInfo.density * cloudColor;
                return result;
            }
            ENDCG
        }
    }
}
