Shader "Hidden/Ocean"
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
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "../Includes/Math.cginc"
			#include "../Includes/Triplanar.cginc"

			struct appdata {
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					half2 uvST : TEXCOORD1;
					UNITY_VERTEX_OUTPUT_STEREO
			};


			float4 colA;
			float4 colB;
			float4 specularCol;
			float depthMultiplier;
			float alphaMultiplier;
			float smoothness;


			sampler2D waveNormalA;
			sampler2D waveNormalB;
			float waveStrength;
			float waveNormalScale;
			float waveSpeed;

			// sampler2D _MainTex;
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
			half4 _MainTex_ST;
			//sampler2D _CameraDepthTexture;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			float4 params;

			float4x4 UV_TO_EYE_TO_WORLD[2];
			// unity setVectorArray only works with vector4
			float3 _WorldSpaceEyePos[2];

			float planetScale;
			float3 oceanCentre;
			float oceanRadius;
			float3 dirToSun;

			
			v2f vert (appdata v) {
				v2f output;
				UNITY_SETUP_INSTANCE_ID(v); //Insert
				UNITY_INITIALIZE_OUTPUT(v2f, output); //Insert
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); //Insert
				output.pos = UnityObjectToClipPos(v.vertex);
				output.uv = v.uv;
				output.uvST = UnityStereoScreenSpaceUVAdjust(v.uv, _MainTex_ST);
				return output;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				//fixed4 originalCol = tex2D(_MainTex, i.uv);
				fixed4 originalCol = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uvST);
				//float3 viewVector = mul(unity_CameraInvProjection, float4(i.uv.xy * 2 - 1, 0, -1));
				//viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
				
				// don't need to -1 the z because we already do that in the matrix
				float3 viewVector = mul(UV_TO_EYE_TO_WORLD[unity_StereoEyeIndex], float4(i.uvST.xy * 2 - 1, 0, 1));

				float3 rayPos = _WorldSpaceEyePos[unity_StereoEyeIndex].xyz;
				float viewLength = length(viewVector);
				float3 rayDir = viewVector / viewLength;

				float nonlin_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uvST);
            	float sceneDepth = LinearEyeDepth(nonlin_depth) * viewLength;

				float2 hitInfo = raySphere(oceanCentre, oceanRadius, rayPos, rayDir);
				float dstToOcean = hitInfo.x;
				float dstThroughOcean = hitInfo.y;
				float3 rayOceanIntersectPos = rayPos + rayDir * dstToOcean - oceanCentre;

				// dst that view ray travels through ocean (before hitting terrain / exiting ocean)
				float oceanViewDepth = min(dstThroughOcean, sceneDepth - dstToOcean);


				if (oceanViewDepth > 0) {
					float3 clipPlanePos = rayPos + viewVector * _ProjectionParams.y;
					float dstAboveWater = length(clipPlanePos - oceanCentre) - oceanRadius;

					float t = 1 - exp(-oceanViewDepth / planetScale * depthMultiplier);
					float alpha =  1-exp(-oceanViewDepth / planetScale * alphaMultiplier);
					float4 oceanCol = lerp(colA, colB, t);

					float3 oceanSphereNormal = normalize(rayOceanIntersectPos);

					float2 waveOffsetA = float2(_Time.x * waveSpeed, _Time.x * waveSpeed * 0.8);
					float2 waveOffsetB = float2(_Time.x * waveSpeed * - 0.8, _Time.x * waveSpeed * -0.3);
					float3 waveNormal = triplanarNormal(rayOceanIntersectPos, oceanSphereNormal, waveNormalScale / planetScale, waveOffsetA, waveNormalA);
					waveNormal = triplanarNormal(rayOceanIntersectPos, waveNormal, waveNormalScale / planetScale, waveOffsetB, waveNormalB);
					waveNormal = normalize(lerp(oceanSphereNormal, waveNormal, waveStrength));
					//return float4(oceanNormal * .5 + .5,1);
					float diffuseLighting = saturate(dot(oceanSphereNormal, dirToSun));
					float specularAngle = acos(dot(normalize(dirToSun - rayDir), waveNormal));
					float specularExponent = specularAngle / (1 - smoothness);
					float specularHighlight = exp(-specularExponent * specularExponent);
				
					oceanCol *= diffuseLighting;
					oceanCol += specularHighlight * (dstAboveWater > 0) * specularCol;
					
					//return float4(oceanSphereNormal,1);
					float4 finalCol =  originalCol * (1-alpha) + oceanCol * alpha;
					return float4(finalCol.xyz, params.x);
				}

				
				return originalCol;
			}
			ENDCG
		}
	}
}
