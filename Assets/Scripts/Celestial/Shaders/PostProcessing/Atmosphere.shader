Shader "Hidden/Atmosphere"
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
			//

			struct appdata {
					float4 vertex : POSITION;
					float4 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					half2 uvST : TEXCOORD1;
					UNITY_VERTEX_OUTPUT_STEREO
			};


			sampler2D _BlueNoise;
			// sampler2D _MainTex;
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
			half4 _MainTex_ST;
			sampler2D _BakedOpticalDepth;
			//sampler2D _CameraDepthTexture;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			float4 params;

			float4x4 UV_TO_EYE_TO_WORLD[2];
			// unity setVectorArray only works with vector4
			float4 _WorldSpaceEyePos[2];
			float4 backgroundColor;

			float3 dirToSun;

			float3 planetCentre;
			float atmosphereRadius;
			float oceanRadius;
			float planetRadius;

			// Paramaters
			int numInScatteringPoints;
			int numOpticalDepthPoints;
			float intensity;
			float4 scatteringCoefficients;
			float ditherStrength;
			float ditherScale;
			float densityFalloff;


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

			float2 squareUV(float2 uv) {
				float width = _ScreenParams.x;
				float height =_ScreenParams.y;
				//float minDim = min(width, height);
				float scale = 1000;
				float x = uv.x * width;
				float y = uv.y * height;
				return float2 (x/scale, y/scale);
			}

			
			float densityAtPoint(float3 densitySamplePoint) {
				float heightAboveSurface = length(densitySamplePoint - planetCentre) - planetRadius;
				float height01 = heightAboveSurface / (atmosphereRadius - planetRadius);
				float localDensity = exp(-height01 * densityFalloff) * (1 - height01);
				return localDensity;
			}
			
			float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength) {
				float3 densitySamplePoint = rayOrigin;
				float stepSize = rayLength / (numOpticalDepthPoints - 1);
				float opticalDepth = 0;

				for (int i = 0; i < numOpticalDepthPoints; i ++) {
					float localDensity = densityAtPoint(densitySamplePoint);
					opticalDepth += localDensity * stepSize;
					densitySamplePoint += rayDir * stepSize;
				}
				return opticalDepth;
			}

			float opticalDepthBaked(float3 rayOrigin, float3 rayDir) {
				float height = length(rayOrigin - planetCentre) - planetRadius;
				float height01 = saturate(height / (atmosphereRadius - planetRadius));

				float uvX = 1 - (dot(normalize(rayOrigin - planetCentre), rayDir) * .5 + .5);
				return tex2Dlod(_BakedOpticalDepth, float4(uvX, height01,0,0));
			}

			float opticalDepthBaked2(float3 rayOrigin, float3 rayDir, float rayLength) {
				float3 endPoint = rayOrigin + rayDir * rayLength;
				float d = dot(rayDir, normalize(rayOrigin-planetCentre));
				float opticalDepth = 0;

				const float blendStrength = 1.5;
				float w = saturate(d * blendStrength + .5);
				
				float d1 = opticalDepthBaked(rayOrigin, rayDir) - opticalDepthBaked(endPoint, rayDir);
				float d2 = opticalDepthBaked(endPoint, -rayDir) - opticalDepthBaked(rayOrigin, -rayDir);

				opticalDepth = lerp(d2, d1, w);
				return opticalDepth;
			}
			
			float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalCol, float2 uv) {
				float blueNoise = tex2Dlod(_BlueNoise, float4(squareUV(uv) * ditherScale,0,0));
				blueNoise = (blueNoise - 0.5) * ditherStrength;
				
				float3 inScatterPoint = rayOrigin;
				float stepSize = rayLength / (numInScatteringPoints - 1);
				float3 inScatteredLight = 0;
				float viewRayOpticalDepth = 0;

				for (int i = 0; i < numInScatteringPoints; i ++) {
					float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
					float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint + dirToSun * ditherStrength, dirToSun);
					float localDensity = densityAtPoint(inScatterPoint);
					viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
					float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients);
					
					inScatteredLight += localDensity * transmittance;
					inScatterPoint += rayDir * stepSize;
				}
				inScatteredLight *= scatteringCoefficients * intensity * stepSize / planetRadius;
				inScatteredLight += blueNoise * 0.01;

				// Attenuate brightness of original col (i.e light reflected from planet surfaces)
				// This is a hacky mess, TODO: figure out a proper way to do this
				const float brightnessAdaptionStrength = 0.15;
				const float reflectedLightOutScatterStrength = 3;
				float brightnessAdaption = dot (inScatteredLight,1) * brightnessAdaptionStrength;
				float brightnessSum = viewRayOpticalDepth * intensity * reflectedLightOutScatterStrength + brightnessAdaption;
				float reflectedLightStrength = exp(-brightnessSum);
				float hdrStrength = saturate(dot(originalCol,1)/3-1);
				reflectedLightStrength = lerp(reflectedLightStrength, 1, hdrStrength);
				float3 reflectedLight = originalCol * reflectedLightStrength;

				float3 finalCol = reflectedLight + inScatteredLight + originalCol/3;

				
				return finalCol;
			}


			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				// float4 originalCol = tex2D(_MainTex, i.uv);
				float4 originalCol = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uvST);
				//float3 viewVector = mul(unity_CameraInvProjection, float4(i.uv.xy * 2 - 1, 0, -1));
				//viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
				// don't need to -1 the z because we already do that in the matrix
				float3 viewVector = mul(UV_TO_EYE_TO_WORLD[unity_StereoEyeIndex], float4(i.uvST.xy * 2 - 1, 0, 1));

				// float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
				float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uvST);
				float sceneDepth = LinearEyeDepth(sceneDepthNonLinear) * length(viewVector);
											
				float3 rayOrigin = _WorldSpaceEyePos[unity_StereoEyeIndex].xyz;
				float3 rayDir = normalize(viewVector);
				
				float dstToOcean = raySphere(planetCentre, oceanRadius, rayOrigin, rayDir);
				float dstToSurface = min(sceneDepth, dstToOcean);
				
				float2 hitInfo = raySphere(planetCentre, atmosphereRadius, rayOrigin, rayDir);
				float dstToAtmosphere = hitInfo.x;
				float dstThroughAtmosphere = min(hitInfo.y, dstToSurface - dstToAtmosphere);
				if (dstThroughAtmosphere > 0) {
					const float epsilon = 0.0001;
					float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
					float3 light = calculateLight(pointInAtmosphere, rayDir, dstThroughAtmosphere - epsilon * 2, originalCol, i.uvST);
					return float4(light, 1);
				}
				return originalCol;
			}


			ENDCG
		}
	}
}
