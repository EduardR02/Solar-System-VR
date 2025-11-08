Shader "Celestial/OceanShell"
{
	Properties
	{
		_WaveNormalA ("Wave Normal A", 2D) = "bump" {}
		_WaveNormalB ("Wave Normal B", 2D) = "bump" {}
	}

	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "../Includes/Math.cginc"
			#include "../Includes/Triplanar.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

		struct v2f {
			float4 pos : SV_POSITION;
			float3 worldPos : TEXCOORD0;
			float3 worldNormal : TEXCOORD1;
			float4 screenPos : TEXCOORD2;
			UNITY_VERTEX_OUTPUT_STEREO
		};

			sampler2D waveNormalA;
			sampler2D waveNormalB;

			float4 colA;
			float4 colB;
			float4 specularCol;
			float depthMultiplier;
			float alphaMultiplier;
			float smoothness;
			float waveStrength;
			float waveNormalScale;
			float waveNormalScaleScaled;
			float waveSpeed;
			float2 waveOffsetA;
			float2 waveOffsetB;
			float planetScale;
			float3 oceanCentre;
			float oceanRadius;
			float3 dirToSun;
			float4 params;

			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

		v2f vert (appdata v) {
			v2f o;
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			float4 world = mul(unity_ObjectToWorld, v.vertex);
			o.worldPos = world.xyz;
			o.worldNormal = UnityObjectToWorldNormal(v.normal);
			o.pos = UnityWorldToClipPos(world);
			o.screenPos = ComputeScreenPos(o.pos);
			return o;
		}

		fixed4 frag (v2f i) : SV_Target {
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float3 rayOrigin = _WorldSpaceCameraPos;
				float3 toPixel = i.worldPos - rayOrigin;
				float3 rayDir = normalize(toPixel);
				float viewLength = length(toPixel);

				float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
				float3 camForward = normalize(-UNITY_MATRIX_V[2].xyz);
				float sceneDepth = LinearEyeDepth(sceneDepthNonLinear) / max(0.0001, dot(rayDir, camForward));

				float2 hitInfo = raySphere(oceanCentre, oceanRadius, rayOrigin, rayDir);
				float dstToOcean = hitInfo.x;
				float dstThroughOcean = hitInfo.y;
				float oceanViewDepth = min(dstThroughOcean, sceneDepth - dstToOcean);
				if (oceanViewDepth <= 0) {
					return 0;
				}

				float3 rayOceanIntersectPos = rayOrigin + rayDir * dstToOcean - oceanCentre;
				float3 oceanSphereNormal = normalize(rayOceanIntersectPos);

				float3 waveNormal = triplanarNormal(rayOceanIntersectPos, oceanSphereNormal, waveNormalScaleScaled, waveOffsetA, waveNormalA);
				waveNormal = triplanarNormal(rayOceanIntersectPos, waveNormal, waveNormalScaleScaled, waveOffsetB, waveNormalB);
				waveNormal = normalize(lerp(oceanSphereNormal, waveNormal, waveStrength));

				float t = 1 - exp(-oceanViewDepth / max(0.0001, planetScale) * depthMultiplier);
				float alpha = 1 - exp(-oceanViewDepth / max(0.0001, planetScale) * alphaMultiplier);
				float4 oceanCol = lerp(colA, colB, t);

				float3 clipPlanePos = rayOrigin + rayDir * _ProjectionParams.y;
				float dstAboveWater = length(clipPlanePos - oceanCentre) - oceanRadius;

				float diffuseLighting = saturate(dot(oceanSphereNormal, dirToSun));
				float3 halfVector = normalize(dirToSun - rayDir);
				float specularDot = saturate(dot(waveNormal, halfVector));
				float specularPower = 1.0 / max(0.001, (1 - smoothness) * (1 - smoothness));
				float specularHighlight = pow(specularDot, specularPower);

				oceanCol *= diffuseLighting;
				oceanCol += specularHighlight * (dstAboveWater > 0) * specularCol;

				return float4(oceanCol.rgb, alpha * params.x);
			}
			ENDCG
		}
	}
}
