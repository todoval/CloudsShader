
Shader "CloudRendering"
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
            float4 _cloudColor;
            float _speed;
            float _tileSize;
            float _absorptionCoef;

            //shape properties
            float _detailAmount;
            float _maxDetailModifier;
            float _densityConstant;
            float _cloudMaxHeight;
            float _cloudHeightModifier;
            float _cloudBottomModifier;

            // texture and sampler properties
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            sampler2D _LastCloudTex;

            SamplerState samplerShapeTexture;
            SamplerState samplerDetailTexture;
            SamplerState samplerWeatherMap;
            SamplerState samplerBlueNoise;

            Texture3D<float4> ShapeTexture;
            Texture3D<float4> DetailTexture;
            Texture2D<float4> WeatherMap;
            Texture2D<float4> BlueNoise;

            // performance properties
            bool _useBlueNoiseRay;
            bool _useBlueNoiseLight;
            float _blueNoiseLightAmount;
            float _blueNoiseRayAmount;
            int _lightMarchSteps;
            float _rayMarchStepSize;
            float _lightMarchDecrease;
            float _rayMarchDecrease;
            float4x4 _LastVP;
            int _useTemporalUpsampling;
            float _temporalBlendFactor;

            // container properties
            float3 containerBound_Min;
            float3 containerBound_Max;

            // sun and light properties
            bool _useLight;
            float3 _lightPosition;
            float4 _lightColor;
            float _lightIntensity;
            float _cloudIntensity;

            float _henyeyCoeff;
            float _henyeyRatio;
            float _henyeyIntensity;

            float _powderCoeff;
            float _powderAmount;
            float _powderIntensity;

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
            float remap(float value, float originalMin, float originalMax, float newMin, float newMax)
            {
                return newMin + (((value - originalMin) / (originalMax - originalMin)) * (newMax - newMin));
            }

            // alter the cloud shape so that the clouds are slightly rounded towards the bottom and a lot to the top (depending on the height value from weather map)
            float heightAlter(float percentHeight, float heightValue)
            {
                // round a bit to the bottom, almost unpercievable
                float bottomRound = saturate(remap(percentHeight, 0.0, _cloudBottomModifier, 0.0, 1.0));
                // round at the top
                float stopHeight = saturate(heightValue + _cloudMaxHeight);
                float topRound = saturate(remap(percentHeight, stopHeight * _cloudHeightModifier, stopHeight, 1.0, 0.0));
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
                return percentHeight * bottomDensity * topDensity * cloudType * _densityConstant;
            }

            // returns the cloud density at given point
            float getDensity(float3 position)
            {
                // get the sample position
                float3 samplePos = (position + _Time * _speed)/_tileSize;

                // do not even sample if outside of box
                if (!isInsideBox(position, _lightPosition))
                    return 0;

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
                detailModifier *= _detailAmount * exp(-_maxDetailModifier);

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
                return (1-g*g)/ (PI) * sqrt(pow(1 + g*g - 2*g*cosAngle, 3)); // I left the 4 factor out due to the result being too dark
            }

            // implementation of the phase function with user-chosen henyey coefficient and the weight/ratio of the phase function
            float phaseFunction(float cosAngle)
            {
                // get the henyey-greenstein phase function
                float hg = getHenyeyGreenstein(cosAngle, _henyeyCoeff);
                return (1 - _henyeyRatio) + hg * _henyeyRatio *  _henyeyIntensity;
            }

            // returns the beer-powder effect value from Horizon Zero Dawn
            float powderEffect(float depth)
            {
                return (1 - exp(-_powderCoeff * 2 * depth)) * _powderAmount * _powderIntensity + (1 - _powderAmount);
            }

            float lightmarch(float3 pos, float heightPercentage)
            {
                // normalized vector from my position to light position
                float3 dirVector = float3(_lightPosition.x, _lightPosition.y, _lightPosition.z) - pos;
                dirVector = dirVector / length(dirVector);

                // get intersection with the cloud container
                rayContainerInfo containerInfo = getRayContainerInfo(_lightPosition, -dirVector);
                float3 entryPoint = _lightPosition - dirVector * containerInfo.dstToBox;

                // light marching, march from my position to the entry point
                float3 currPoint = pos;
                // number of steps should be the same for each light march
                float distanceToMarch = getDistance(entryPoint, pos) * heightPercentage; // do not march over the maximum height
                float noOfSteps = _lightMarchSteps; // light march steps set up by users
                float stepDecrease = _lightMarchDecrease;
                float stepSize = distanceToMarch/noOfSteps;

                // use this property to decrease the step size when marching, number of steps can be max 4
                if (noOfSteps == 2)
                    stepSize = distanceToMarch/(1 + stepDecrease);
                else if (stepSize == 3)
                    stepSize = distanceToMarch/(1 + stepDecrease + pow(stepDecrease,2));
                else if (stepSize == 4)
                    stepSize = distanceToMarch/(1 + stepDecrease + pow(stepDecrease,2) + pow(stepDecrease,3));

                float accumDensity = 0; // accumulated density over all the ray from my point to the entry point
                float transmittance = 1;

                while (noOfSteps > 0)
                {
                    float density = getDensity(currPoint); // get the density for the current part of the ray
                    if (density > 0)
                        accumDensity += density * stepSize;
                    else
                        break; //performance measures, when density is zero it usually means we're out of the clouds
                    // take another step in the direction of the light
                    currPoint += dirVector * stepSize;
                    stepSize *= stepDecrease;
                    noOfSteps--;
                }

                // use the beer's law for the light attenuation (from the Fredrik Haggstrom paper)
                float lightAttenuation = exp(-accumDensity * _absorptionCoef);
                return lightAttenuation * _lightIntensity * _lightColor; // multiply by user set intensity and color
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
                if (_henyeyRatio > 0)
                {
                    float3 lightVector = pos - float3(_lightPosition.x, _lightPosition.y, _lightPosition.z);
                    lightVector = lightVector / length(lightVector);
                    float cosAngle = dot(normalize(incVector), lightVector);
                    phaseVal = phaseFunction(cosAngle);
                }
                float incLight = phaseVal * lightFromSun;

                // add blue noise at the end if desired
                if (_useBlueNoiseLight)
                {
                    float2 blueNoiseSamplePos = float2(pos.x, pos.y);
                    float4 blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, blueNoiseSamplePos, 0);
                    float bnVal = saturate(blueNoise.x + blueNoise.y + blueNoise.z)/3;
                    incLight += bnVal * _blueNoiseLightAmount;
                }
                return incLight;
            }

            // ray marching, implementation mostly from Palenik
            raymarchInfo raymarch(float3 entryPoint, float3 rayDir)
            {
                float stepSize = _rayMarchStepSize; // user defined step size
                // a check so that we're not dividing by zero
                if (stepSize == 0 || _rayMarchDecrease == 0)
                {
                    raymarchInfo result;
                    result.transmittance = 0;
                    result.density = 0;
                    return result;
                }

                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float4 totalDensity = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching

                // effect of blue noise
                if (_useBlueNoiseRay)
                {
                    float2 blueNoiseSamplePos = float2(entryPoint.x, entryPoint.y);
                    float4 blueNoise = BlueNoise.SampleLevel(samplerBlueNoise, blueNoiseSamplePos, 0);
                    float bnVal = saturate(blueNoise.x + blueNoise.y + blueNoise.z)/3;
                    currPoint += rayDir * ((bnVal* _blueNoiseRayAmount - 0.5) * stepSize);
                }

                // the raymarching algorithm
                while (isInsideBox(currPoint, rayDir))
                {
                    float density = getDensity(currPoint); //density = color that is sampled from the noise texture
                    if (density > 0)
                    {
                        float incLight = 0;
                        if (_useLight)
                            incLight = getIncidentLighting(currPoint, rayDir, density);

                        // approximate the attenuation of light with the Beer-Lambert's law
                        float deltaT = exp(-_absorptionCoef * stepSize * density);

                        // lower the transmittance as you march further away from the viewer
                        transmittance *= deltaT;
                        if (transmittance < 0.01) // break if transmittance is too low to avoid performance problems
                            break;

                        // raymarching render equation, the terms that are constant aren't added here to improve performance
                        totalDensity += density * transmittance * incLight;
                    }

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                    stepSize *= _rayMarchDecrease; // user defined decrease
                    
                }
                raymarchInfo result;
                result.transmittance = transmittance;
                
                // the other part of the raymarch render equation - adding the terms that were constant so they didn't have to be added in each raymarch step
                result.density = totalDensity * _absorptionCoef * stepSize;
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
                
                // if there are other objects, do not render clouds
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonLinearDepth) * viewLength;

                // return base if the box was not intersected 
                float4 base = tex2D(_MainTex, i.uv);

                if (!containerInfo.intersectedBox || containerInfo.dstToBox > depth)
                    return base;

                float3 entryPoint = rayOrigin + rayDir * containerInfo.dstToBox; // intersection with the cloud container
                raymarchInfo raymarchInfo = raymarch(entryPoint, rayDir); // raymarch through the clouds and get the density and transmittance

                // add user defined intensity and cloud color to the already sampled density, also add the light color from the environmental sun
                float4 result = raymarchInfo.transmittance * base + raymarchInfo.density * _cloudColor * _cloudIntensity;
                if (_useTemporalUpsampling == 0) // if no need to upsample, return current result
                    return result;

                // this part is only for the temporal upsampling
                float3 endPoint = entryPoint + containerInfo.dstInsideBox * rayDir;
				float4 reprojectionPoint = float4((entryPoint + endPoint)/2,1);
                float4 lastFrameClipCoord = mul(_LastVP, reprojectionPoint);
                float2 lastFrameUV  = float2(lastFrameClipCoord.x / lastFrameClipCoord.w, lastFrameClipCoord.y / lastFrameClipCoord.w) * 0.5 + 0.5;
				float4 lastFrameCol = tex2D(_LastCloudTex, lastFrameUV);    

				if (lastFrameUV.x < 0 || lastFrameUV.x > 1 || lastFrameUV.y < 0 || lastFrameUV.y > 1)
				    _temporalBlendFactor = 1;
                return result * _temporalBlendFactor + lastFrameCol * (1 - _temporalBlendFactor);
            }
            ENDCG
        }
    }
}
