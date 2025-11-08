using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanEffect {

	Light light;
	protected Material material;
	bool materialInitialized;
	int lastStereoDataVersion = -1;
	Shader cachedShader; // Cache to avoid expensive shader comparison

	public void UpdateSettings (CelestialBodyGenerator generator, Shader shader) {
		// Initialize material and set constant properties once
		// Only check shader change if reference actually changed (avoids material.shader property access)
		if (material == null || cachedShader != shader) {
			material = new Material (shader);
			cachedShader = shader;
			materialInitialized = false;
		}

		OceanSettings oceanSettings = generator.body.shading.oceanSettings;

		if (!materialInitialized) {
			// Set constant properties only once when material is created
			generator.body.shading.SetOceanProperties (material);
			material.SetFloat ("planetScale", generator.BodyScale);

			// Precompute constant scale value
			if (oceanSettings != null) {
				material.SetFloat ("waveNormalScaleScaled", oceanSettings.waveScale / generator.BodyScale);
			}

			materialInitialized = true;
		}

		// Find light reference if needed (cached after first find)
		if (light == null) {
			light = GameObject.FindFirstObjectByType<SunShadowCaster> ()?.GetComponent<Light> ();
		}

		// Update dynamic properties every frame
		Vector3 centre = generator.transform.position;
		float radius = generator.GetOceanRadius ();
		material.SetVector ("oceanCentre", centre);
		material.SetFloat ("oceanRadius", radius);

		if (lastStereoDataVersion != CustomPostProcessing.StereoDataVersion) {
			material.SetMatrixArray ("UV_TO_EYE_TO_WORLD", CustomPostProcessing._uvToEyeToWorld);
			material.SetVectorArray ("_WorldSpaceEyePos", CustomPostProcessing._eyePosition);
			lastStereoDataVersion = CustomPostProcessing.StereoDataVersion;
		}

		if (light) {
			material.SetVector ("dirToSun", -light.transform.forward);
		} else {
			material.SetVector ("dirToSun", Vector3.up);
		}

		// Update time-based wave offsets every frame
		if (oceanSettings != null) {
			float timeWaveSpeed = Time.time * oceanSettings.waveSpeed;
			material.SetVector ("waveOffsetA", new Vector2(timeWaveSpeed, timeWaveSpeed * 0.8f));
			material.SetVector ("waveOffsetB", new Vector2(timeWaveSpeed * -0.8f, timeWaveSpeed * -0.3f));
		}
	}

	public Material GetMaterial () {
		return material;
	}

}
