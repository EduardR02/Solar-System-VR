Shader "Hidden/OceanMask"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"
			#include "../../Includes/Math.cginc"

			struct appdata
			{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
					half2 uvST : TEXCOORD1;
					UNITY_VERTEX_OUTPUT_STEREO
			};


			sampler2D _MainTex;
			half4 _MainTex_ST;
			static const int maxNumSpheres = 12;
			float4 spheres[maxNumSpheres];
			int numSpheres;

			float4x4 UV_TO_EYE_TO_WORLD[2];
			// unity setVectorArray only works with vector4
			float4 _WorldSpaceEyePos[2];


			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //Insert
				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.uvST = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
				return o;
			}

			
			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				// don't need to -1 the z because we already do that in the matrix
				float3 viewVector = mul(UV_TO_EYE_TO_WORLD[unity_StereoEyeIndex], float4(i.uvST.xy * 2 - 1, 0, 1));
				float3 rayOrigin = _WorldSpaceEyePos[unity_StereoEyeIndex].xyz;
				float3 rayDir = normalize(viewVector);

				

				float nearest = 0;
				for (int sphereIndex = 0; sphereIndex < numSpheres; sphereIndex ++) {
					float2 hitInfo = raySphere(spheres[sphereIndex].xyz, spheres[sphereIndex].w, rayOrigin, rayDir);
					if (hitInfo.y > 0) {
						return 1;
					}
				}
				return 0;
			}
			ENDCG
		}
	}
}
