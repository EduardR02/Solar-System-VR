using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;

[CreateAssetMenu (menuName = "Celestial Body/Atmosphere")]
public class AtmosphereSettings : ScriptableObject {

	public bool enabled = true;
	public Shader atmosphereShader;
	public ComputeShader opticalDepthCompute;
	public int textureSize = 256;

	public int inScatteringPoints = 10;
	public int opticalDepthPoints = 10;
	public float densityFalloff = 0.25f;

	public Vector3 wavelengths = new Vector3 (700, 530, 460);

	public Vector4 testParams = new Vector4 (7, 1.26f, 0.1f, 3);
	public float scatteringStrength = 20;
	public float intensity = 1;

	public float ditherStrength = 0.8f;
	public float ditherScale = 4;
	public Texture2D blueNoise;

	[Range (0, 1)]
	public float atmosphereScale = 0.5f;

	[Header ("Test")]
	public float timeOfDay;
	public float sunDst = 1;

	RenderTexture opticalDepthTexture;
	bool settingsUpToDate;

	public void SetProperties (Material material, float bodyRadius) {
		if (material == null) {
			return;
		}
		ApplyProperties (new MaterialBinder (material), bodyRadius);
	}

	public void ApplyTo (MaterialPropertyBlock block, float bodyRadius) {
		if (block == null) {
			return;
		}
		ApplyProperties (new PropertyBlockBinder (block), bodyRadius);
	}

	void ApplyProperties (IPropertyBinder binder, float bodyRadius) {
		float atmosphereRadius = (1 + atmosphereScale) * bodyRadius;

		binder.SetVector ("params", testParams);
		binder.SetInt ("numInScatteringPoints", inScatteringPoints);
		binder.SetInt ("numOpticalDepthPoints", opticalDepthPoints);
		binder.SetFloat ("atmosphereRadius", atmosphereRadius);
		binder.SetFloat ("planetRadius", bodyRadius);
		binder.SetFloat ("densityFalloff", densityFalloff);

		// Strength of (rayleigh) scattering is inversely proportional to wavelength^4
		float scatterX = Pow (400 / wavelengths.x, 4);
		float scatterY = Pow (400 / wavelengths.y, 4);
		float scatterZ = Pow (400 / wavelengths.z, 4);
		binder.SetVector ("scatteringCoefficients", new Vector3 (scatterX, scatterY, scatterZ) * scatteringStrength);
		binder.SetFloat ("intensity", intensity);
		binder.SetFloat ("ditherStrength", ditherStrength);
		binder.SetFloat ("ditherScale", ditherScale);
		binder.SetTexture ("_BlueNoise", blueNoise);

		EnsureOpticalDepthTexture ();
		binder.SetTexture ("_BakedOpticalDepth", opticalDepthTexture);
	}

	void PrecomputeOutScattering () {
		if (!settingsUpToDate || opticalDepthTexture == null || !opticalDepthTexture.IsCreated ()) {
			ComputeHelper.CreateRenderTexture (ref opticalDepthTexture, textureSize, FilterMode.Bilinear);
			opticalDepthCompute.SetTexture (0, "Result", opticalDepthTexture);
			opticalDepthCompute.SetInt ("textureSize", textureSize);
			opticalDepthCompute.SetInt ("numOutScatteringSteps", opticalDepthPoints);
			opticalDepthCompute.SetFloat ("atmosphereRadius", (1 + atmosphereScale));
			opticalDepthCompute.SetFloat ("densityFalloff", densityFalloff);
			opticalDepthCompute.SetVector ("params", testParams);
			ComputeHelper.Run (opticalDepthCompute, textureSize, textureSize);
			settingsUpToDate = true;
		}

	}

	void EnsureOpticalDepthTexture () {
		if (!settingsUpToDate || opticalDepthTexture == null || !opticalDepthTexture.IsCreated ()) {
			PrecomputeOutScattering ();
		}
	}

	void OnValidate () {
		settingsUpToDate = false;
	}

	public float GetAtmosphereRadius (float bodyRadius) {
		return (1 + atmosphereScale) * bodyRadius;
	}

	interface IPropertyBinder {
		void SetFloat (string name, float value);
		void SetInt (string name, int value);
		void SetVector (string name, Vector4 value);
		void SetTexture (string name, Texture value);
	}

	class MaterialBinder : IPropertyBinder {
		readonly Material material;
		public MaterialBinder (Material material) {
			this.material = material;
		}

		public void SetFloat (string name, float value) => material.SetFloat (name, value);
		public void SetInt (string name, int value) => material.SetInt (name, value);
		public void SetVector (string name, Vector4 value) => material.SetVector (name, value);
		public void SetTexture (string name, Texture value) => material.SetTexture (name, value);
	}

	class PropertyBlockBinder : IPropertyBinder {
		readonly MaterialPropertyBlock block;
		public PropertyBlockBinder (MaterialPropertyBlock block) {
			this.block = block;
		}

		public void SetFloat (string name, float value) => block.SetFloat (name, value);
		public void SetInt (string name, int value) => block.SetInt (name, value);
		public void SetVector (string name, Vector4 value) => block.SetVector (name, value);
		public void SetTexture (string name, Texture value) => block.SetTexture (name, value);
	}
}
