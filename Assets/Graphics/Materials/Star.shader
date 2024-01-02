Shader "Celestial/Star"
{
	SubShader
	{
		Tags { "Queue" = "Overlay" "RenderType" = "Transparent"}
		LOD 100
		ZWrite Off
		Lighting Off
      Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"

			struct appdata
			{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 col : TEXCOORD0;
				float brightnessFalloff : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO

			};

			float daytimeFade;
			sampler2D _MainTex;
			sampler2D _Spectrum;
			sampler2D _OceanMask;
			half4 _MainTex_ST;


			v2f vert (appdata v)
			{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v); //Insert
    				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
    				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert
					o.vertex = UnityObjectToClipPos(v.vertex);
					half2 uv_MainTex = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
					// half2 uv_MainTex = v.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw; // same thing as UnityStereoScreenSpaceUVAdjust
					float4 backgroundCol = tex2Dlod(_MainTex, float4(uv_MainTex, 0, 0));
					float oceanMask = tex2Dlod(_OceanMask, float4(uv_MainTex, 0, 0));
					float backgroundBrightness = saturate(dot(backgroundCol.rgb, 1) / 3 * daytimeFade);
					float starBrightness = (1 - backgroundBrightness) * (1-oceanMask);
					float4 starCol = tex2Dlod(_Spectrum, float4(v.uv.y, 0.5, 0, 0));
					// o.col = lerp(backgroundCol, starCol, starBrightness);
					o.col = float4(starCol.rgb,starBrightness);
					//o.col = float4(starCol.rgb,1);
					o.brightnessFalloff = v.uv.x;
					/*
					if (oceanMask.x >0.5) {
						o.col = float4(1,0,0,0);
					}
					else {
						o.col = float4(0,1,0,0);
					}
					*/
					return o;
			}

			float4 frag (v2f i) : SV_Target
			{	
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float b = i.brightnessFalloff;
				b = saturate (b+0.1);
				b*=b;
				
				return float4(i.col.rgb, i.col.a * b);
			}

			ENDCG
		}
	}
}
