Shader "Celestial/AtmosphereShell"
{
	Properties
	{
		_BlueNoise ("Blue Noise", 2D) = "white" {}
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "../Includes/Math.cginc"

			struct appdata {
				float4 vertex : POSITION;
			};

		struct v2f {
			float4 pos : SV_POSITION;
			float3 worldPos : TEXCOORD0;
			float4 screenPos : TEXCOORD1;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		UNITY_DECLARE_SCREENSPACE_TEXTURE(_PlanetShellBackbuffer);
			sampler2D _BlueNoise;
			sampler2D _BakedOpticalDepth;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

			float4 params;
			int numInScatteringPoints;
			int numOpticalDepthPoints;
			float intensity;
			float4 scatteringCoefficients;
			float ditherStrength;
			float ditherScale;
			float densityFalloff;
			float3 dirToSun;
			float3 planetCentre;
			float atmosphereRadius;
			float oceanRadius;
			float planetRadius;
			float4 backgroundColor;

		v2f vert (appdata v) {
			v2f o;
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			float4 world = mul(unity_ObjectToWorld, v.vertex);
			o.worldPos = world.xyz;
			o.pos = UnityWorldToClipPos(world);
			o.screenPos = ComputeScreenPos(o.pos);
			return o;
		}

			float2 SquareUV(float2 uv) {
				float2 resolution = _ScreenParams.xy;
				float2 pixel = uv * resolution;
				const float scale = 1000;
				return pixel / scale;
			}

			float DensityAtPoint(float3 densitySamplePoint) {
				float heightAboveSurface = length(densitySamplePoint - planetCentre) - planetRadius;
				float height01 = heightAboveSurface / max(0.0001, (atmosphereRadius - planetRadius));
				float localDensity = exp(-height01 * densityFalloff) * (1 - height01);
				return saturate(localDensity);
			}

			float OpticalDepthBaked(float3 rayOrigin, float3 rayDir) {
				float height = length(rayOrigin - planetCentre) - planetRadius;
				float height01 = saturate(height / max(0.0001, (atmosphereRadius - planetRadius)));
				float uvX = 1 - (dot(normalize(rayOrigin - planetCentre), rayDir) * 0.5 + 0.5);
				return tex2Dlod(_BakedOpticalDepth, float4(uvX, height01, 0, 0));
			}

			float3 CalculateLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalCol, float2 uv) {
				float blueNoise = tex2Dlod(_BlueNoise, float4(SquareUV(uv) * ditherScale,0,0));
				blueNoise = (blueNoise - 0.5) * ditherStrength;

				float3 inScatterPoint = rayOrigin;
				float stepSize = rayLength / max(1, (numInScatteringPoints - 1));
				float3 inScatteredLight = 0;
				float viewRayOpticalDepth = 0;

				float3 sunDitherOffset = dirToSun * ditherStrength;
				float rayOriginDepthForward = OpticalDepthBaked(rayOrigin, rayDir);
				float rayOriginDepthBackward = OpticalDepthBaked(rayOrigin, -rayDir);

				float3 planetNormal = normalize(rayOrigin - planetCentre);

				for (int i = 0; i < numInScatteringPoints; i++) {
					float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
					float sunRayOpticalDepth = OpticalDepthBaked(inScatterPoint + sunDitherOffset, dirToSun);
					float localDensity = DensityAtPoint(inScatterPoint);

					float3 endPoint = rayOrigin + rayDir * (stepSize * i);
					float d = dot(rayDir, planetNormal);
					const float blendStrength = 1.5;
					float w = saturate(d * blendStrength + 0.5);
					float d1 = rayOriginDepthForward - OpticalDepthBaked(endPoint, rayDir);
					float d2 = OpticalDepthBaked(endPoint, -rayDir) - rayOriginDepthBackward;
					viewRayOpticalDepth = lerp(d2, d1, w);

					float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients);

					inScatteredLight += localDensity * transmittance;
					inScatterPoint += rayDir * stepSize;
				}

				inScatteredLight *= scatteringCoefficients * intensity * stepSize / max(0.0001, planetRadius);
				inScatteredLight += blueNoise * 0.01;

				const float brightnessAdaptionStrength = 0.15;
				const float reflectedLightOutScatterStrength = 3;
				float brightnessAdaption = dot(inScatteredLight, 1) * brightnessAdaptionStrength;
				float brightnessSum = viewRayOpticalDepth * intensity * reflectedLightOutScatterStrength + brightnessAdaption;
				float reflectedLightStrength = exp(-brightnessSum);
				float hdrStrength = saturate(dot(originalCol,1)/3 - 1);
				reflectedLightStrength = lerp(reflectedLightStrength, 1, hdrStrength);
				float3 reflectedLight = originalCol * reflectedLightStrength;

				return reflectedLight + inScatteredLight + originalCol / 3;
			}

		float4 frag (v2f i) : SV_Target {
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float3 rayOrigin = _WorldSpaceCameraPos;
				float3 rayDir = normalize(i.worldPos - rayOrigin);

		float2 uv = i.screenPos.xy / i.screenPos.w;
		float4 originalCol = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_PlanetShellBackbuffer, i.screenPos);

		float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
		float3 camForward = normalize(-UNITY_MATRIX_V[2].xyz);
		float sceneDepth = LinearEyeDepth(sceneDepthNonLinear);
		float viewProj = dot(rayDir, camForward);
		bool hasSceneDepth = sceneDepthNonLinear > 0.0001 && abs(viewProj) > 0.0001;
		if (hasSceneDepth) {
			sceneDepth /= viewProj;
		}

		float2 planetHit = raySphere(planetCentre, planetRadius, rayOrigin, rayDir);
		float dstToPlanetSurface = (planetHit.x > 0) ? planetHit.x : 1e6;
		float surfaceDepth = hasSceneDepth ? sceneDepth : dstToPlanetSurface;

		float dstToOcean = raySphere(planetCentre, oceanRadius, rayOrigin, rayDir);
		float dstToSurface = min(surfaceDepth, dstToOcean);

		float2 hitInfo = raySphere(planetCentre, atmosphereRadius, rayOrigin, rayDir);
		float dstToAtmosphere = hitInfo.x;
		float dstThroughAtmosphere = min(hitInfo.y, dstToSurface - dstToAtmosphere);
		if (dstThroughAtmosphere <= 0) {
			float fallbackSurface = min(dstToOcean, dstToPlanetSurface);
			dstThroughAtmosphere = min(hitInfo.y, fallbackSurface - dstToAtmosphere);
			if (dstThroughAtmosphere <= 0) {
				return float4(0,0,0,0);
			}
		}

				const float epsilon = 0.0001;
				float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
				float3 light = CalculateLight(pointInAtmosphere, rayDir, dstThroughAtmosphere - epsilon * 2, originalCol.rgb, uv);
				return float4(light, 1);
			}
			ENDCG
		}
	}
}
