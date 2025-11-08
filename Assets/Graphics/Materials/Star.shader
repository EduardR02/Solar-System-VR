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
				half2 uv : TEXCOORD0;
				half2 uv_MainTex : TEXCOORD1;
				float brightnessFalloff : TEXCOORD2;
				UNITY_VERTEX_OUTPUT_STEREO

			};

				float daytimeFade;
				sampler2D _MainTex;
				sampler2D _Spectrum;
				half4 _MainTex_ST;

				static const int MAX_OCEAN_SPHERES = 32;
				float4 _OceanSpheres[MAX_OCEAN_SPHERES];
				int _NumOceanSpheres;

				float4x4 UV_TO_EYE_TO_WORLD[2];
				float4 _WorldSpaceEyePos[2];


			v2f vert (appdata v)
			{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v); //Insert
    				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
    				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;
					o.uv_MainTex = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
					o.brightnessFalloff = v.uv.x;
					return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

					// Texture fetches now in fragment shader for performance
					float4 backgroundCol = tex2D(_MainTex, i.uv_MainTex);
					float backgroundBrightness = saturate(dot(backgroundCol.rgb, 1) / 3 * daytimeFade);
					float4 starCol = tex2D(_Spectrum, float2(i.uv.y, 0.5));

					float oceanOcclusion = 0;
					if (_NumOceanSpheres > 0) {
						float3 viewVector = mul(UV_TO_EYE_TO_WORLD[unity_StereoEyeIndex], float4(i.uv_MainTex.xy * 2 - 1, 0, 1)).xyz;
						float3 rayOrigin = _WorldSpaceEyePos[unity_StereoEyeIndex].xyz;
						float3 rayDir = normalize(viewVector);

						[loop]
						for (int sphereIndex = 0; sphereIndex < _NumOceanSpheres; sphereIndex++) {
							float4 sphere = _OceanSpheres[sphereIndex];
							float3 centre = sphere.xyz;
							float radius = sphere.w;
							float3 toCentre = centre - rayOrigin;
							float b = dot(toCentre, rayDir);
							float c = dot(toCentre, toCentre) - radius * radius;
							float discriminant = b * b - c;
							if (discriminant >= 0) {
								float t = b - sqrt(discriminant);
								if (t > 0) {
									oceanOcclusion = 1;
									break;
								}
							}
						}
					}

					float starBrightness = (1 - backgroundBrightness) * (1 - oceanOcclusion);

				float b = i.brightnessFalloff;
				b = saturate (b+0.1);
				b*=b;

				return float4(starCol.rgb, starBrightness * b);
			}

			ENDCG
		}
	}
}
