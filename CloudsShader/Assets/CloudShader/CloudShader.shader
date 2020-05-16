
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

            //shape properties
            float detailAmount;
            float maxDetailModifier;
            float densityConstant;
            float cloudMaxHeight;
            float cloudHeightModifier;
            float cloudBottomModifier;

            // texture and sampler properties
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            SamplerState samplerShapeTexture;
            SamplerState samplerDetailTexture;
            SamplerState samplerWeatherMap;
            SamplerState samplerBlueNoise;

            Texture3D<float4> ShapeTexture;
            Texture3D<float4> DetailTexture;
            Texture2D<float4> WeatherMap;
            Texture2D<float4> BlueNoise;

            // performance properties
            bool useBlueNoiseRay;
            bool useBlueNoiseLight;
            float blueNoiseLightAmount;
            float blueNoiseRayAmount;
            int lightMarchSteps;

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

            float powderCoeff;
            float powderAmount;
            float powderIntensity;


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

            // alter the cloud shape so that the clouds are slightly rounded towards the bottom and a lot to the top (depending on the height value from weather map)
            float heightAlter(float percentHeight, float heightValue)
            {
                // round a bit to the bottom, almost unpercievable
                float bottomRound = saturate(remap(percentHeight, 0.0, cloudBottomModifier, 0.0, 1.0));
                // round at the top
                float stopHeight = saturate(heightValue + cloudMaxHeight);
                float topRound = saturate(remap(percentHeight, stopHeight * cloudHeightModifier, stopHeight, 1.0, 0.0));
                return topRound * bottomRound;
            }

            // depending on the cloud type, make the clouds more fluffy at the bottom and more rounded at the top
            float densityAlter (float percentHeight, float cloudType)
            {
                // Reduce density at bottom to make more fluffy clouds, bottom is to 0.2
                float bottomDensity = saturate(remap(percentHeight, 0.0, 0.2, 0.0, 1.0));
                // create more defined shape at the top, top is from 0.9
                float topDensity = saturate(remap(percentHeight, 0.9, 1.0, 1.0, 0.0));

                //density computation equation, applying both mapDensity, bottom and top density and a user-defined constant
                return percentHeight * bottomDensity * topDensity * cloudType * densityConstant;
            }

            // returns the cloud density at given point
            float getDensity(float3 position)
            {
                // get the sample position
                float3 samplePos = (position + _Time * speed)/tileSize;

                 // sample the weather texture, with red channel being coverage, green height and blue the density of the clouds
                float2 weatherSamplePos = float2(samplePos.x, samplePos.z);
                float4 weatherValue = WeatherMap.SampleLevel(samplerWeatherMap, weatherSamplePos, 0);
                // get the individual values from the weather texture
                float heightValue = weatherValue.g;
                float globalDensity = weatherValue.b;
                float cloudCoverage = weatherValue.r;
                
                // get the base shape of the clouds from the shape texture, use the perlin noise (red channel) as base
                float4 shapeDensity = ShapeTexture.SampleLevel(samplerShapeTexture, samplePos, 0);
                float shapeNoise = shapeDensity.g * 0.625 + shapeDensity.b * 0.25 + shapeDensity.a * 0.125;
                shapeNoise = -(1 - shapeNoise);
                shapeNoise = remap(shapeDensity.r, shapeNoise, 1.0, 0.0, 1.0); // this is now the base cloud

                // get the height percentage of the current point
                float heightPercentage = (position.y - containerBound_Min.y) / (containerBound_Max.y - containerBound_Min.y);

                // sample the detail noise that will be used to erode edges               
                float4 detailDensity = DetailTexture.SampleLevel(samplerDetailTexture, samplePos, 0);
                float detailNoise = detailDensity.r * 0.625 + detailDensity.g * 0.25 + detailDensity.a * 0.125; // similar sampling to shape texture
                // adjust the detail noise with regards to the height percentage - the lower the point is, the wispier the shapes are
                float detailModifier = lerp (detailNoise, 1-detailNoise, saturate(heightPercentage));
                // reduce the amount of detail noise so the maximum is the detailAmount (inputted by user)
                detailModifier *= detailAmount * exp(-maxDetailModifier);

                // sample the height and density altering functions from the current heightPercetange
                float heightModifier = heightAlter(heightPercentage, heightValue); //round the clouds to the bottom and to the top according to their height
                float densityFactor = densityAlter(heightPercentage, globalDensity); //edit the density so clouds are more fluffier at bottom and rounder at top
                
                // equation from the Haggstrom paper
                float shapeND = saturate(remap(heightModifier * shapeNoise, 1 - cloudCoverage, 1.0, 0.0, 1.0));

                // subtract the detail noise from the shape noise to erode the edges
                float finalDensity = saturate(remap(shapeND, detailModifier, 1.0, 0.0, 1.0));

                // alter the density at the end, once again Haggstrom
                return finalDensity * densityFactor;
            }

            // implementing the phase function, cosAngle is the cosine of the angle between two vectors, g is a parameter in [-1,1]  
            float getHenyeyGreenstein(float cosAngle, float g)
            {
                float k = 100 * 3.0 / (8.0 * 3.1415926f) * (1.0 - g * g) / (2.0 + g * g);
	            //return k * (1.0 + cosTheta * cosTheta) / pow(abs(1.0 + g * g - 2.0 * g * cosTheta), 1.5);
                
                return k * (1 - g*g)/( 4*PI* sqrt(pow(abs(1 + g*g - 2*g*cosAngle), 3)));
            }

            // implementation of the phase function with user-chosen henyey coefficient and the weight/ratio of the phase function
            float phaseFunction(float cosAngle)
            {
                // get the henyey-greenstein phase function
                float hg = getHenyeyGreenstein(cosAngle, henyeyCoeff);
                return (1 - henyeyRatio) + hg * henyeyRatio;
            }

            float powderEffect(float depth)
            {
                return (1 - exp(-powderCoeff * 2 * depth)) * powderAmount * powderIntensity + (1 - powderAmount);
            }

            float lightmarch(float3 pos, float heightPercentage)
            {
                // normalized vector from my position to light position
                float3 dirVector = float3(lightPosition.x, lightPosition.y, lightPosition.z) - pos;
                dirVector = dirVector / length(dirVector);

                // get intersection with the cloud container
                rayContainerInfo containerInfo = getRayContainerInfo(lightPosition, -dirVector);
                float3 entryPoint = lightPosition - dirVector * containerInfo.dstToBox;

                // light marching, march from my position to the entry point
                float3 currPoint = pos;
                // number of steps should be the same for each light march
                float distanceToMarch = getDistance(entryPoint, pos);
                float noOfSteps = lightMarchSteps; // light march steps set up by users
                float stepSize = heightPercentage * distanceToMarch/noOfSteps; //distanceToMarch/(noOfSteps * 10);
                float accumDensity = 0; // accumulated density over all the ray from my point to the entry point
                float transmittance = 1;
                if (isInsideBox(lightPosition, dirVector))
                {
                    stepSize = getDistance(float3(lightPosition.x, lightPosition.y, lightPosition.z), pos)/noOfSteps;
                    //currPoint = lightPosition;
                }
                while (noOfSteps > 0)
                {
                    float density = getDensity(currPoint); // get the density for the current part of the ray
                    if (density > 0)
                        accumDensity += density * stepSize;
                    // take another step in the direction of the light
                    currPoint += dirVector * stepSize;
                    noOfSteps --;
                }

                // use the beer's law for the light attenuation (from the Fredrik Haggstrom paper)
                float lightAttenuation = exp(-accumDensity * absorptionCoef);
                return lightAttenuation * lightIntensity * lightColor;
            }

            float getIncidentLighting(float3 pos, float3 incVector, float currDensity)
            {
                // will be used multiple times in lighting calculations
                float heightPercentage = (pos.y - containerBound_Min.y) / (containerBound_Max.y - containerBound_Min.y);

                // get the light coming from sun, use the light marching algorithm
                float lightFromSun = lightmarch(pos, heightPercentage);
               lightFromSun *= powderEffect(currDensity);

                // compute the value of the phase function only if desired
                float phaseVal = 1;
                if (henyeyRatio > 0)
                {
                    float3 lightVector = pos - float3(lightPosition.x, lightPosition.y, lightPosition.z);
                    lightVector = lightVector / length(lightVector);
                    lightVector = normalize(lightVector);
                    float cosAngle = dot(normalize(incVector), lightVector);
                    phaseVal =  1;//phaseFunction(cosAngle);
                }
                float incLight = phaseVal * lightFromSun;

                // add blue noise at the end if desired
                if (useBlueNoiseLight)
                {
                    float2 blueNoiseSamplePos = float2(pos.x, pos.y);
                    float4 blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, blueNoiseSamplePos, 0);
                    float bnVal = saturate(blueNoise.x);
                    incLight += bnVal * blueNoiseLightAmount;
                }

                return incLight;
            }

            // ray marching, implementation mostly from Palenik
            raymarchInfo raymarch(float3 entryPoint, float3 rayDir)
            {
                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float stepSize = 2;
                float depth = 0;
                float4 totalDensity = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching

                // effect of blue noise
                if (useBlueNoiseRay)
                {
                    float2 blueNoiseSamplePos = float2(entryPoint.x, entryPoint.y);
                    float4 blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, blueNoiseSamplePos, 0);
                    float bnVal = saturate((blueNoise.x + blueNoise.y + blueNoise.z)/3);
                    currPoint += rayDir *  ((bnVal* blueNoiseRayAmount - 0.5) * stepSize);
                }

                // the raymarching algorithm
                while (isInsideBox(currPoint, rayDir))
                {
                    float density = getDensity(currPoint); //density = color that is sampled from the noise texture
                    if (density > 0)
                    {
                        float incLight = 0;
                        if (useLight)
                            incLight = getIncidentLighting(currPoint, rayDir, density);

                        // approximate the attenuation of light with the Beer-Lambert's law
                        float deltaT = exp(-absorptionCoef * stepSize * density);

                        // lower the transmittance as you march further away from the viewer
                        transmittance *= deltaT;
                        if (transmittance < 0.01) // break if transmittance is too low to avoid performance problems
                            break;

                        // raymarching render equation, the terms that are constant aren't added here to improve performance
                        totalDensity += density * transmittance * incLight;// * BeerPowder(depth) * 50;
                        depth+= density * stepSize; 
                    }

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                   // stepSize*= 1.01;
                    
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
