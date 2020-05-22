Shader "EnvironmentBlending"
{
    // implementation from riverluara's github: https://github.com/riverluara/VolumetricCloudRendering/

    Properties
    {
		_MainTex("Texture", 2D) = "white" {}
		_CloudTex("Cloud Texture", 2D) = "white" {}
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

			sampler2D _MainTex;
			sampler2D _CloudTex;
			sampler2D_float _CameraDepthTexture;

            struct appdata
            {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
            };

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 uv_depth : TEXCOORD1;
			};

            float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv.xy;
				o.uv_depth = v.uv.xy;

				#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0)
					o.uv.y = 1 - o.uv.y;
				#endif				
				return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				half4 base = tex2D(_MainTex, i.uv); // sample base color
				float4 cloud = tex2D(_CloudTex, i.uv); // sample the clouds from the given texture
				float d = Linear01Depth(tex2D(_CameraDepthTexture, i.uv_depth));
				if (cloud.a > 0.97)
                    cloud.a = 1;
				if (d == 1)
					return lerp(base, cloud, 1 - cloud.a); // the blending of the two layers
				else
					return base; // if we're not in the clouds, return only base
            }
            ENDCG
        }
    }
}
