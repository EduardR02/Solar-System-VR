using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtmosphereEffect {

	Light light;
	protected Material material;
	bool materialInitialized;
	int lastStereoDataVersion = -1;
	Shader cachedShader; // Cache to avoid expensive shader comparison

	public void UpdateSettings (CelestialBodyGenerator generator) {

		Shader shader = generator.body.shading.atmosphereSettings.atmosphereShader;

		// Initialize material and set constant properties once
		// Only check shader change if reference actually changed (avoids material.shader property access)
		if (material == null || cachedShader != shader) {
			material = new Material (shader);
			cachedShader = shader;
			materialInitialized = false;
		}

		if (!materialInitialized) {
			// Set constant properties only once when material is created
			generator.body.shading.atmosphereSettings.SetProperties (material, generator.BodyScale);
			materialInitialized = true;
		}

		// Find light reference if needed (cached after first find)
		if (light == null) {
			light = GameObject.FindFirstObjectByType<SunShadowCaster> ()?.GetComponent<Light> ();
		}

		// Update dynamic properties every frame
		material.SetVector ("planetCentre", generator.transform.position);
		material.SetFloat ("oceanRadius", generator.GetOceanRadius ());

		if (lastStereoDataVersion != CustomPostProcessing.StereoDataVersion) {
			material.SetMatrixArray ("UV_TO_EYE_TO_WORLD", CustomPostProcessing._uvToEyeToWorld);
			material.SetVectorArray ("_WorldSpaceEyePos", CustomPostProcessing._eyePosition);
			lastStereoDataVersion = CustomPostProcessing.StereoDataVersion;
		}
		material.SetVector("backgroundColor", Camera.main.backgroundColor);

		if (light) {
			Vector3 dirFromPlanetToSun = (light.transform.position - generator.transform.position).normalized;
			material.SetVector ("dirToSun", dirFromPlanetToSun);
		} else {
			material.SetVector ("dirToSun", Vector3.up);
		}
	}

	public Material GetMaterial () {
		return material;
	}
}
