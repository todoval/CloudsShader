
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
            SamplerState samplerWeatherMap;

            Texture3D<float4> ShapeTexture;
            Texture3D<float4> DetailTexture;
            Texture2D<float4> WeatherMap;

            // container properties
            float3 containerBound_Min;
            float3 containerBound_Max;

            // sun and light properties
            bool useLight;
            float3 lightPosition;
            float4 lightColor;
            float lightIntensity;

            float henyeyCoeff;
            float henyeyRatio;

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

            // remaps a value from one range (original) to new range (new)
            float remap(float value, float original_min, float original_max, float new_min, float new_max)
            {
                return new_min + (((value - original_min) / (original_max - original_min)) * (new_max - new_min));
            }

            // clamps the value v to be in a range between 0 and 1
            /*float saturate(float v)
            {
                if (v < 0)
                    return 0;
                if (v > 1)
                    return 1;
                return v;
            }*/

            // returns the cloud density at given point
            float getDensity(float3 position)
            {
                // sample the shape and detail textures from position
                position+= _Time * speed;
                float3 samplePos = position/tileSize;

                float globalHeight = 0.8;
                float globalDensity = 0.5;

                // weather texture
                // red channel - coverage, base density of the clouds
                // green channel - height of the clouds
                // blue channel - type of cloud (0 - stratus, 1 - cumulus, 0.5 - stratoculumus)
                float2 weatherSamplePos = float2(samplePos.x, samplePos.z);
                float4 weatherValue = WeatherMap.SampleLevel(samplerWeatherMap, weatherSamplePos, 0);
                
                
                // get the base shape of the clouds from the shape texture, use the perlin noise as base
                float4 shapeDensity = ShapeTexture.SampleLevel(samplerShapeTexture, samplePos, 0);
                float shapeNoise = shapeDensity.g * 0.625 + shapeDensity.b * 0.25 + shapeDensity.a * 0.125;
                shapeNoise = -(1 - shapeNoise);
                shapeNoise = remap(shapeDensity.r, shapeNoise, 1.0, 0.0, 1.0); // this is now the base cloud
                
                // use the red channel from weather texture as cloud coverage and apply it to the base cloud
                float cloudCoverage = weatherValue.r;
                float baseCloudWithCoverage = remap(shapeNoise, cloudCoverage, 1.0, 1.0, 0.0);
                baseCloudWithCoverage *= cloudCoverage;

                // Have density be generally increasing over height
                float percent_height = 0.2;
                float ret_val = percent_height;
                // Reduce density at base
                ret_val  *= saturate(remap(percent_height, 0.0, 0.2, 0.0, 1.0));
                // Apply weather_map density
                ret_val  *= globalDensity;
                // Reduce density for the anvil ( cumulonimbus clouds)
                //ret_val *= lerp (1,saturate(remap(pow(percent_height,0.5)0.4, 0.95, 1.0, 0.2)), cloud_anvil_amount);
                // Reduce density at top to make better transition
                ret_val *= saturate(remap(percent_height, 0.9, 1.0, 1.0, 0.0));
                //baseCloudWithCoverage *= ret_val;
                return baseCloudWithCoverage * ret_val;


                // get the height gradient from weather map
                float heightValue = weatherValue.g;
                //float heightFrequencyModifier = mix(baseCloudWithCoverage, 1.0 - baseCloudWithCoverage, saturate(heightValue * 10));
                //float finalCloud = remap(baseCloudWithCoverage, heightFrequencyModifier * 0.2, 1.0, 0.0, 1.0);

               /* float gMin = remap(heightValue,0,1,0.1,0.5);
                float gMax = remap(heightValue,0,1,gMin,0.9);
                float heightOfContainer = containerBound_Max.y - containerBound_Min.y;
                float heightPercent = (samplePos.y - containerBound_Min.y) / heightOfContainer;
                float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1, gMax, 0, 1));

                baseCloudWithCoverage *= baseCloudWithCoverage * heightGradient * 0.5;*/

                // sample the detail noise (for erosion of the cloud edges) similarly to the shape noise
                float4 detailDensity = DetailTexture.SampleLevel(samplerDetailTexture, samplePos, 0);
                float detailNoise = detailDensity.r * 0.625 + detailDensity.g * 0.25 + detailDensity.a * 0.125;
                // Subtract detail noise from base shape (weighted by inverse density so that edges get eroded more than centre)
                float oneMinusShape = 1 - baseCloudWithCoverage;
                float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
                float cloudDensity = baseCloudWithCoverage;
                //if (baseCloudWithCoverage > 1)
                cloudDensity = baseCloudWithCoverage - detailNoise * 0.2;// (1-detailNoise) * detailErodeWeight * 0.0005;
                return cloudDensity;

                // For low altitude regions the detail noise is used (inverted)
                //to instead of creating round shapes ,
                // create more wispy shapes. Transitions to round shapes over altitude.
                float detail_modifier = lerp (detailNoise, 1-detailNoise, saturate(0));

                // Reduce the amount of detail noise is being "subtracted"
                // with the global_coverage .
                detail_modifier *= 0.35 * exp(-0.001 * 0.75);
                // Carve away more from the shape_noise using detail_noise
                float final_density = saturate(remap(shapeNoise, detail_modifier, 1.0, 0.0, 1.0));
            }

            // implementing the phase function, cosAngle is the cosine of the angle between two vectors, g is a parameter in [-1,1]  
            float getHenyeyGreenstein(float cosAngle, float g)
            {
                return (1 - g*g)/( 4*PI* sqrt(pow(1 + g*g - 2*g*cosAngle, 3)));
            }

            // implementation of the phase function with user-chosen henyey coefficient and the weight/ratio of the phase function
            float phaseFunction(float cosAngle)
            {
                // get the henyey-greenstein phase function
                float hg = getHenyeyGreenstein(cosAngle, henyeyCoeff);
                return (1 - henyeyRatio) + hg * henyeyRatio;
            }

            float getIncidentLighting(float3 pos, float3 incVector)
            {
                // normalized vector from my position to light position
                float3 dirVector = float3(lightPosition.x, lightPosition.y, lightPosition.z) - pos;
                dirVector = dirVector / length(dirVector);

                // light shouldn't be inside the box, but if it is, only light the box from the inside accordingly
                /*if (isInsideBox(lightPosition, dirVector))
                {
                    float distanceToLight = getDistance(float3(lightPosition.x, lightPosition.y, lightPosition.z), pos);
                    float containerMaxDistance = getDistance(containerBound_Min, containerBound_Max);
                    return (containerMaxDistance - distanceToLight)/containerMaxDistance;
                }*/

                // get intersection with the cloud container
                rayContainerInfo containerInfo = getRayContainerInfo(lightPosition, -dirVector);
                float3 entryPoint = lightPosition - dirVector * containerInfo.dstToBox;

                // light marching, march in the direction from the entry point to my point
                float3 currPoint = entryPoint;
                // number of steps should be the same for each light march
                float distanceToMarch = getDistance(entryPoint, pos);
                float noOfSteps = 2;
                float stepSize = distanceToMarch/noOfSteps;
                float accumDensity = 0; // accumulated density over all the ray from my point to the entry point
                float transmittance = 1;
                if (isInsideBox(lightPosition, dirVector))
                {
                    stepSize = getDistance(float3(lightPosition.x, lightPosition.y, lightPosition.z), pos)/noOfSteps;
                    currPoint = lightPosition;
                }
                while (noOfSteps > 0)
                {
                    float density = getDensity(currPoint); // get the density for the current part of the ray
                    if (density > 0)
                        accumDensity += density;
                    
                    // take another step in the direction of the light
                    currPoint += dirVector * stepSize;
                    noOfSteps --;
                }
                // use the beer's law for the light attenuation (from the Fredrik Haggstrom paper)
                float lightAttenuation = exp(-accumDensity * stepSize * absorptionCoef);
                return lightAttenuation * lightIntensity * lightColor; // TO DO add light weights
            }


            // ray marching, implementation mostly from Palenik
            raymarchInfo raymarch(float3 entryPoint, float3 rayDir)
            {
                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float stepSize = 2;
                float4 totalDensity = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching

                while (isInsideBox(currPoint, rayDir))
                {
                    float density = getDensity(currPoint); //density = color that is sampled from the noise texture
                    if (density > 0)
                    {
                        float incLight = 0;
                        float phaseVal = 1;
                        if (useLight)
                        {
                            // use the light marching algorithm to get the light from the light source
                            incLight = getIncidentLighting(currPoint, rayDir);
                            // compute the value of the phase function only if desired
                            if (henyeyRatio > 0)
                            {
                                float3 lightVector = currPoint - float3(lightPosition.x, lightPosition.y, lightPosition.z);
                                lightVector = lightVector / length(lightVector);
                                float cosAngle = dot(rayDir, lightVector);
                                phaseVal = phaseFunction(cosAngle);
                            }
                        }

                        // approximate the attenuation of light with the Beer-Lambert's law
                        float deltaT = exp(-absorptionCoef * stepSize * density);

                        // lower the transmittance as you march further away from the viewer
                        transmittance *= deltaT;
                        if (transmittance < 0.0001) // break if transmittance is too low to avoid performance problems
                            break;

                        // raymarching render equation, the terms that are constant aren't added here to improve performance
                        totalDensity += density * transmittance * incLight * phaseVal;
                    }

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                }
                raymarchInfo result;
                result.transmittance = transmittance;
                
                // the other part of the raymarch render equation - adding the terms that were constant so they didn't have to be added in each raymarch step
                result.density = totalDensity * lightColor  * absorptionCoef * stepSize;
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
