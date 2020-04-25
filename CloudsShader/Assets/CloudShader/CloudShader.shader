
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

            struct rayContainerInfo
            {
                bool intersectedBox;
                float dstInsideBox; // 0 if does not intersect box
                float dstToBox; // 0 if inside box
            };

            //cloud properties
            float4 cloudColor;
            float speed;
            float tileSize;

            // texture and sampler properties
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            SamplerState samplerShapeTexture;
            SamplerState samplerDetailTexture;

            Texture3D<float4> ShapeTexture;
            Texture3D<float4> DetailTexture;

            // container properties
            float3 lowerBound;
            float3 upperBound;

            // sun and light properties
            bool useLight;
            float3 lightPosition;
            float4 lightColor;
            float lightIntensity;

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
                return (1 - g*g)/( 4*PI* (1 + g*g - 2*g*cosAngle));
            }

            float getIncidentLighting(float3 pos, float3 incVector)
            {
                // vector from the light position to my position
                float3 dirVector = pos - float3(lightPosition.x, lightPosition.y, lightPosition.z);
                // get the normalized ray direction
                dirVector = dirVector / length(dirVector);

                // get intersection with the cloud container
                rayContainerInfo containerInfo = getRayContainerInfo(lowerBound, upperBound, lightPosition, dirVector);
                float3 entryPoint = lightPosition + dirVector * containerInfo.dstToBox;

                // light marching, march in the direction from the entry point to my point
                float3 currPoint = entryPoint;
                float distanceToMarch = getDistance(entryPoint, pos);
                float noOfSteps = 4;
                float stepSize = distanceToMarch/noOfSteps;
                // if light is inside box, set number of steps accordingly
                if (getDistance(noOfSteps * stepSize * dirVector + currPoint, pos) > getDistance(lightPosition, pos))
                    stepSize = getDistance(lightPosition, pos)/noOfSteps;
                
                float resLight = 0;
                float transmittance = 1;
                float absorptionCoef = 0.6;
                while (distanceToMarch > stepSize)
                {
                    // get the density (= color that is sampler from the noise texture) at current position 
                    float density = getDensity(currPoint); 
                    if (density > 0)
                    {
                        // approximate the attenuation of light with the Beer-Lambert's law
                       // float deltaT = exp(-absorptionCoef * stepSize * density);
                        // lower the transmittance as you march further away from the viewer
                       // transmittance *= deltaT;
                        // break if transmittance is too low to avoid performance problems
                       // if (transmittance < 0.01)
                         //   break;
                        // Rendering equation 
                        resLight += density * stepSize; // * transmittance * absorptionCoef;
                    }
                    
                    // take another step in the direction of the light
                    currPoint += dirVector * stepSize;
                    distanceToMarch -= stepSize;
                }

                resLight = exp(-resLight * 0.4);


                // get cosine of the angle between incDir and dirVector
                float cosAngle = dot(dirVector, incVector)/ (length(dirVector) * length(incVector));
                return resLight;//getHenyeyGreenstein(cosAngle, 0.6);
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

                // ray marching, implementation mostly from Palenik
                float transmittance = 1; // the current ratio between light that was emitted and light that is received (accumulating variable for transparency)
                float stepSize = 0.2;
                float4 resColor = float4(0,0,0,0); // accumulating variable for the resulting color
                float3 currPoint = entryPoint; // current point on the ray during ray marching
                float absorptionCoef = 0.2;

                while (isInsideBox(currPoint, lowerBound, upperBound, rayDir))
                {
                    // get the density (= color that is sampler from the noise texture) at current position 
                    float density = getDensity(currPoint); 
                    
                    // compute the incident light only if there is some density
                    if (density > 0)
                    {
                        // use the light marching algorithm to get the light from the light source 
                        float incLight = 0;
                        if (useLight)
                            incLight = getIncidentLighting(currPoint, rayDir);
                            
                        // approximate the attenuation of light with the Beer-Lambert's law
                        float deltaT = exp(-absorptionCoef * stepSize * density);

                        // lower the transmittance as you march further away from the viewer
                        transmittance *= deltaT;
                        // break if transmittance is too low to avoid performance problems
                        if (transmittance < 0.01)
                            break;

                        // Rendering equation 
                        resColor += density * stepSize * transmittance * absorptionCoef * incLight ;
                    }

                    // take a step forward along the ray
                    currPoint += rayDir * stepSize;
                }

                // TO DO - eliminate bounding artifacts with depth
                float4 result = transmittance * base + resColor * cloudColor;
                return result;
            }
            ENDCG
        }
    }
}
