// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

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
                float3 viewVector : TEXCOORD1;
            };

            VertToFrag vert (VertInput v)
            {
                VertToFrag o;
                o.vertex = UnityObjectToClipPos(v.pos);
                o.uv = v.uv;
                o.color = v.pos.xyz;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return o;
            }

            // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
            float4 rayDistBox(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
                // Adapted from: http://jcgt.org/published/0007/03/04/
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
                // dstA is dst to nearest intersection, dstB dst to far intersection

                // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
                // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

                // CASE 3: ray misses box (dstA > dstB)
                
                /*if (dstA > dstB)
                    return true;
                return false;*/

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float4(dstToBox, dstInsideBox, dstA, dstB);
            }

            sampler2D _MainTex;
           // sampler2D _NoiseTex;

            SamplerState samplerNoiseTex;

            Texture3D<float4> NoiseTex;

            // container properties
            float3 lowerBound;
            float3 upperBound;

            fixed4 frag (VertToFrag i) : COLOR
            {
               // float4 currColor = NoiseTex[uint3(1,1,0)];
                float4 currColor = NoiseTex.Sample(samplerNoiseTex, i.vertex);

                float4 base = tex2D(_MainTex, i.uv);
                //float4 other = tex2D(_NoiseTex, i.uv);

                float viewLength = length(i.viewVector);
                float3 rayDir = i.viewVector / viewLength;
                float3 rayOrigin = _WorldSpaceCameraPos;
                float4 res = rayDistBox(lowerBound, upperBound, rayOrigin, 1/rayDir);
                bool isInBox = (res.z < res.w);

                if (isInBox && res.x )
                    return base * currColor;

                return base;
            }
            ENDCG
        }
    }
}
