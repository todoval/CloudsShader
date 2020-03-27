
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

            struct rayContainerInfo
            {
                bool intersectedBox;
                float dstInsideBox; // 0 if does not intersect box
                float dstToBox; // 0 if inside box
            };

            /*
            input:
                boundsMin, boundsMax - bounds of the container
                rayOrigin - the start of the ray (camera position)
                rayDir - the direction of the incoming ray 
            outputs a rayContainerInfo structure
            */
            rayContainerInfo getRayContainerInfo(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir)
            {
                // this function implements the AABB algorithm (e.g. https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection)
                
                // tA and tB are from the line equation of the ray: rayOrigin + tA*rayDir
                float3 tA = (boundsMin - rayOrigin) / rayDir; // for the point on boundsMin - A
                float3 tB = (boundsMax - rayOrigin) / rayDir; // for the point on boundsMax - B

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
            bool isInsideBox(float3 position, float3 boundsMin, float3 boundsMax, float3 rayDir)
            {
                rayContainerInfo containerInfo = getRayContainerInfo(boundsMin, boundsMax, position, rayDir);
                return (containerInfo.dstInsideBox > 0);
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            SamplerState samplerNoiseTex;

            Texture3D<float4> NoiseTex;

            // container properties
            float3 lowerBound;
            float3 upperBound;

            // properties of volume
            float absorptionCoef; // kapa

            float getDensity(float3 position)
            {
                float4 currColor = NoiseTex.SampleLevel(samplerNoiseTex, position, 0);
                return currColor.r;
            }

            fixed4 frag (VertToFrag i) : COLOR
            {
                // get the normalized ray direction
                float viewLength = length(i.viewVector);
                float3 rayDir = i.viewVector / viewLength;

                // ray starts at the camera position
                float3 rayOrigin = _WorldSpaceCameraPos;

                // get the information about the intersection of ray and the container
                rayContainerInfo containerInfo = getRayContainerInfo(lowerBound, upperBound, rayOrigin, rayDir);
                
                // return base if the box was not intersected 
                float4 base = tex2D(_MainTex, i.uv);
                
                //if there are other objects, do not render clouds
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonLinearDepth) * viewLength;

                if (!containerInfo.intersectedBox && !containerInfo.dstToBox < depth)
                    return base;

                // get intersection with the cloud container
                float3 entryPoint = rayOrigin + rayDir * containerInfo.dstToBox;

                // ray marching
                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float stepSize = 0.2;
                float maxDistance = containerInfo.dstInsideBox / stepSize;
                float currDistance = 0;
                float4 resColor = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching
                absorptionCoef = 0.2;

                float totalDens = 0;
                int steps = 0;
                float tra = 0;
                while (currDistance < maxDistance) //(isInsideBox(currPoint, lowerBound, upperBound, rayDir))
                {
                    float3 currPos = entryPoint + rayDir * currDistance;
                    float dens = getDensity(currPoint);
                    if (dens > 1)
                        tra = 1;
                    totalDens += getDensity(currPoint) * stepSize;


                    // take a step forward along the ray
                    currDistance += stepSize;
                }
                if (tra == 0)
                    tra = exp(-totalDens);
                

                /*while (isInsideBox(currPoint, lowerBound, upperBound, rayDir))
                {
                   // float4 currColor =  NoiseTex.SampleLevel(samplerNoiseTex, currPoint, 0);// get the color at the current point

                    float4 currColor = NoiseTex.SampleLevel(samplerNoiseTex, currPoint, 0);// get the density at current position
                    float density = currColor.x;
                    float deltaT =  exp(-absorptionCoef * stepSize * density);
                    //float incLghting = evalIncLighting(); // evaluates the incident lighting
                    transmittance *= deltaT;

                    // break if transmittance is too low to avoid performance problems
                    if (transmittance < 0.000001)
                        break;

                    // Rendering equation 
                    resColor += density * stepSize * transmittance * absorptionCoef;

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                }*/


                float4 result = tra * base  /*+ resColor*/;
                return result;

               /* float4 currColor = NoiseTex.SampleLevel(samplerNoiseTex, entryPoint, 0);
               // float4 base = tex2D(_MainTex, i.uv);
                if (isInsideBox(float3(entryPoint.x, entryPoint.y, entryPoint.z + 2), lowerBound, upperBound, rayDir))
                    return base * currColor;
                return base;*/
            }
            ENDCG
        }
    }
}
