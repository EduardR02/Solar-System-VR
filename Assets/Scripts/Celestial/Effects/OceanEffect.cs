using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanEffect {

	Light light;
	protected Material material;

	public void UpdateSettings (CelestialBodyGenerator generator, Shader shader) {
		if (material == null || material.shader != shader) {
			material = new Material (shader);
		}

		if (light == null) {
			light = GameObject.FindFirstObjectByType<SunShadowCaster> ()?.GetComponent<Light> ();
		}

		Vector3 centre = generator.transform.position;
		float radius = generator.GetOceanRadius ();
		material.SetVector ("oceanCentre", centre);
		material.SetFloat ("oceanRadius", radius);
		
		material.SetMatrixArray("UV_TO_EYE_TO_WORLD", CustomPostProcessing._uvToEyeToWorld);
		material.SetVectorArray("_WorldSpaceEyePos", CustomPostProcessing._eyePosition);

		material.SetFloat ("planetScale", generator.BodyScale);
		if (light) {
			material.SetVector ("dirToSun", -light.transform.forward);
		} else {
			material.SetVector ("dirToSun", Vector3.up);
			Debug.Log ("No SunShadowCaster found");
		}
		generator.body.shading.SetOceanProperties (material);
	}

	public Material GetMaterial () {
		return material;
	}

}