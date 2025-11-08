using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Surfaces;
using UnityEngine;

/*
	Responsible for rendering oceans and atmospheres as post processing effect
*/

[CreateAssetMenu (menuName = "PostProcessing/PlanetEffects")]
public class PlanetEffects : PostProcessingEffect {

	public Shader oceanShader;
	public Shader atmosphereShader;
	public bool displayOceans = true;
	public bool displayAtmospheres = true;

	List<EffectHolder> effectHolders;
	readonly EffectHolderDistanceComparer distanceComparer = new EffectHolderDistanceComparer();

	List<Material> postProcessingMaterials;
	bool active = true;
	Plane[][] planes = new Plane[2][];
	bool initialized = false;

	// Cache for frustum plane optimization
	Vector3 lastFrustumCameraPosition;
	Quaternion lastFrustumCameraRotation;
	int lastFrustumStereoDataVersion = -1;

	// Cache for sort optimization
	Vector3 lastSortCameraPosition;
	const float sortDistanceThreshold = 10f; // Only re-sort if camera moved 10+ units

	public override void Render (RenderTexture source, RenderTexture destination) {
		List<Material> materials = GetMaterials ();
		CustomPostProcessing.RenderMaterials (source, destination, materials);
	}

	void Init () {
		// In play mode, only initialize once and cache the results
		if (Application.isPlaying && initialized) {
			if (postProcessingMaterials == null) {
				postProcessingMaterials = new List<Material> ();
			}
			postProcessingMaterials.Clear ();
			return;
		}

		// Initialize effect holders (expensive FindObjectsByType call)
		if (effectHolders == null || effectHolders.Count == 0 || !Application.isPlaying) {
			var generators = FindObjectsByType<CelestialBodyGenerator> (FindObjectsSortMode.None);
			effectHolders = new List<EffectHolder> (generators.Length);
			for (int i = 0; i < generators.Length; i++) {
				effectHolders.Add (new EffectHolder (generators[i]));
			}
			if (Application.isPlaying) {
				initialized = true;
			}
		}

		if (postProcessingMaterials == null) {
			postProcessingMaterials = new List<Material> ();
		}
		postProcessingMaterials.Clear ();
	}

	public List<Material> GetMaterials () {

		if (!active) {
			return null;
		}
		Init ();

		if (effectHolders.Count > 0) {
			Camera cam = Camera.current;
			Vector3 camPos = cam.transform.position;

			// Only sort if camera moved significantly - planet order rarely changes
			if ((camPos - lastSortCameraPosition).sqrMagnitude > sortDistanceThreshold * sortDistanceThreshold) {
				SortFarToNear (camPos);
				lastSortCameraPosition = camPos;
			}
			GetFrustumPlanes();

			for (int i = 0; i < effectHolders.Count; i++) {
				EffectHolder effectHolder = effectHolders[i];
				Material underwaterMaterial = null;
				Vector3 planetCentre = effectHolder.generator.transform.position;
				// Oceans
				if (displayOceans) {
					if (effectHolder.oceanEffect != null && SphereInCameraFrustum (planetCentre, effectHolder.generator.GetOceanRadius())) {

						effectHolder.oceanEffect.UpdateSettings (effectHolder.generator, oceanShader);

						float camDstFromCentre = (camPos - planetCentre).magnitude;
						if (camDstFromCentre < effectHolder.generator.GetOceanRadius ()) {
							underwaterMaterial = effectHolder.oceanEffect.GetMaterial ();
						} else {
							postProcessingMaterials.Add (effectHolder.oceanEffect.GetMaterial ());
						}
					}
				}
				// Atmospheres

				if (displayAtmospheres) {
					if (effectHolder.atmosphereEffect != null  && SphereInCameraFrustum (planetCentre, effectHolder.generator.body.shading.atmosphereSettings.GetAtmosphereRadius(effectHolder.generator.BodyScale))) {
						effectHolder.atmosphereEffect.UpdateSettings (effectHolder.generator);
						postProcessingMaterials.Add (effectHolder.atmosphereEffect.GetMaterial());
					}
				}

				if (underwaterMaterial != null) {
					postProcessingMaterials.Add (underwaterMaterial);
				}
			}
		}

		return postProcessingMaterials;
	}

	bool SphereInCameraFrustum(Vector3 sphereCenter, float sphereRadius) {
		Bounds bounds = new Bounds (sphereCenter, Vector3.one * sphereRadius * 2);
		for (int i = 0; i < 2; i++) {
			if (GeometryUtility.TestPlanesAABB(planes[i], bounds)) {
				return true;
			}
		}
		return false;
	}

	void GetFrustumPlanes() {
		Camera cam = Camera.current;
		if (cam == null) return;

		// Only recalculate if camera transform or stereo matrices changed
		bool cameraTransformChanged = cam.transform.position != lastFrustumCameraPosition ||
		                               cam.transform.rotation != lastFrustumCameraRotation;
		bool stereoDataChanged = CustomPostProcessing.StereoDataVersion != lastFrustumStereoDataVersion;

		if (!cameraTransformChanged && !stereoDataChanged && planes[0] != null && planes[1] != null) {
			return; // Use cached frustum planes
		}

		// Check if stereo rendering is enabled (VR mode)
		if (cam.stereoEnabled) {
			Matrix4x4[] worldToProjectionMatrix = new Matrix4x4[2];
			worldToProjectionMatrix[0] = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left) * cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
			worldToProjectionMatrix[1] = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right) * cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
			planes[0] = GeometryUtility.CalculateFrustumPlanes(worldToProjectionMatrix[0]);
			planes[1] = GeometryUtility.CalculateFrustumPlanes(worldToProjectionMatrix[1]);
		} else {
			// Non-VR mode: use regular projection matrix for both eyes
			Matrix4x4 worldToProjection = cam.projectionMatrix * cam.worldToCameraMatrix;
			Plane[] monoPlanes = GeometryUtility.CalculateFrustumPlanes(worldToProjection);
			planes[0] = monoPlanes;
			planes[1] = monoPlanes;
		}

		// Update cache
		lastFrustumCameraPosition = cam.transform.position;
		lastFrustumCameraRotation = cam.transform.rotation;
		lastFrustumStereoDataVersion = CustomPostProcessing.StereoDataVersion;
	}

	float CalculateMaxClippingDst (Camera cam) {
		float halfHeight = cam.nearClipPlane * Mathf.Tan (cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float halfWidth = halfHeight * cam.aspect;
		float dstToNearClipPlaneCorner = new Vector3 (halfWidth, halfHeight, cam.nearClipPlane).magnitude;
		return dstToNearClipPlaneCorner;
	}

	public class EffectHolder {
		public CelestialBodyGenerator generator;
		public OceanEffect oceanEffect;
		public AtmosphereEffect atmosphereEffect;

		public EffectHolder (CelestialBodyGenerator generator) {
			this.generator = generator;
			if (generator.body.shading.hasOcean && generator.body.shading.oceanSettings) {
				oceanEffect = new OceanEffect ();
			}
			if (generator.body.shading.hasAtmosphere && generator.body.shading.atmosphereSettings) {
				atmosphereEffect = new AtmosphereEffect ();
			}
		}

		public float DstFromSurface (Vector3 viewPos) {
			return Mathf.Max (0, (generator.transform.position - viewPos).magnitude - generator.BodyScale);
		}
	}

	void SortFarToNear (Vector3 viewPos) {
		distanceComparer.ViewPosition = viewPos;
		effectHolders.Sort (distanceComparer);
	}

	class EffectHolderDistanceComparer : IComparer<EffectHolder> {
		public Vector3 ViewPosition;
		public int Compare (EffectHolder x, EffectHolder y) {
			if (x == null && y == null) return 0;
			if (x == null) return 1;
			if (y == null) return -1;
			float dstX = x.DstFromSurface (ViewPosition);
			float dstY = y.DstFromSurface (ViewPosition);
			return dstY.CompareTo (dstX); // far to near
		}
	}
}
