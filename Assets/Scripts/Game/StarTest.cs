using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class StarTest : MonoBehaviour {

	public int seed = 0;
	public int numStars;
	public int numVertsPerStar = 5;
	public Vector2 sizeMinMax;
	public float minBrightness;
	public float maxBrightness = 1;
	public float dst = 10;
	public float daytimeFade = 4; // higher value means it needs to be darker before stars will appear 
	public Material mat;
	Mesh mesh;
	Camera cam;

	public Gradient colourSpectrum;
	Texture2D spectrum;
	bool settingsUpdated;

	CelestialBodyGenerator[] oceanBodies;
	Vector4[] oceanSpheres;
	const int MaxOceanSpheres = 32;

	void Start () {
		Init (true);
	}

	void OnValidate () {
		settingsUpdated = true;
	}

	void Update () {
		if (!Application.isPlaying) {
			Init (settingsUpdated);
			settingsUpdated = false;
		}
	}

	void Init (bool regenerateMesh) {
		if (regenerateMesh) {
			GenerateMesh ();
		}
		var customPostProcessing = FindFirstObjectByType<CustomPostProcessing> ();
		customPostProcessing.onPostProcessingComplete -= Set;
		customPostProcessing.onPostProcessingComplete += Set;
		cam = customPostProcessing.GetComponent<Camera> ();
		TextureHelper.TextureFromGradient (colourSpectrum, 64, ref spectrum);
		mat.SetTexture ("_Spectrum", spectrum);
		mat.SetFloat ("daytimeFade", daytimeFade);

		CacheOceanBodies ();
	}

	public void UpdateOrigin(Vector3 originOffset) {
		transform.position -= originOffset;
	}

	public void Set (RenderTexture screen) {
		mat.SetTexture ("_MainTex", screen);
		UpdateOceanUniforms ();
		mat.SetMatrixArray ("UV_TO_EYE_TO_WORLD", CustomPostProcessing._uvToEyeToWorld);
		mat.SetVectorArray ("_WorldSpaceEyePos", CustomPostProcessing._eyePosition);
		if (Camera.current == cam) { // ignore in scene view
			transform.position = cam.transform.position;
		}
	}

	void CacheOceanBodies () {
		var generators = FindObjectsByType<CelestialBodyGenerator> (FindObjectsSortMode.None);
		var list = new List<CelestialBodyGenerator> ();
		for (int i = 0; i < generators.Length; i++) {
			if (generators[i].body != null && generators[i].body.shading != null && generators[i].body.shading.hasOcean && generators[i].body.shading.oceanSettings != null) {
				list.Add (generators[i]);
			}
		}
		oceanBodies = list.ToArray ();
		oceanSpheres = new Vector4[Mathf.Min (oceanBodies.Length, MaxOceanSpheres)];
	}

	void UpdateOceanUniforms () {
		if (oceanBodies == null) {
			CacheOceanBodies ();
		}

		int sphereCount = (oceanBodies != null) ? Mathf.Min (oceanBodies.Length, MaxOceanSpheres) : 0;
		if ((oceanSpheres == null || oceanSpheres.Length != sphereCount) && sphereCount > 0) {
			oceanSpheres = new Vector4[sphereCount];
		}

		for (int i = 0; i < sphereCount; i++) {
			Vector3 pos = oceanBodies[i].transform.position;
			float radius = oceanBodies[i].GetOceanRadius ();
			oceanSpheres[i] = new Vector4 (pos.x, pos.y, pos.z, radius);
		}

		mat.SetInt ("_NumOceanSpheres", sphereCount);
		if (sphereCount > 0) {
			mat.SetVectorArray ("_OceanSpheres", oceanSpheres);
		}
	}

	void GenerateMesh () {
		if (mesh) {
			mesh.Clear ();
		}

		mesh = new Mesh ();
		var tris = new List<int> ();
		var verts = new List<Vector3> ();
		var uvs = new List<Vector2> ();

		Random.InitState (seed);
		for (int starIndex = 0; starIndex < numStars; starIndex++) {
			var dir = Random.onUnitSphere;
			var (circleVerts, circleTris, circleUvs) = GenerateCircle (dir, verts.Count);
			verts.AddRange (circleVerts);
			tris.AddRange (circleTris);
			uvs.AddRange (circleUvs);
		}

		mesh.SetVertices (verts);
		mesh.SetTriangles (tris, 0, true);
		mesh.SetUVs (0, uvs);
		var meshRenderer = GetComponent<MeshRenderer> ();
		GetComponent<MeshFilter> ().sharedMesh = mesh;
		meshRenderer.sharedMaterial = mat;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
	}

	(Vector3[] verts, int[] tris, Vector2[] uvs) GenerateCircle (Vector3 dir, int indexOffset) {
		float size = Random.Range (sizeMinMax.x, sizeMinMax.y);
		float brightness = Random.Range (minBrightness, maxBrightness);
		float spectrumT = Random.value;

		var axisA = Vector3.Cross (dir, Vector3.up).normalized;
		if (axisA == Vector3.zero) {
			axisA = Vector3.Cross (dir, Vector3.forward).normalized;
		}
		var axisB = Vector3.Cross (dir, axisA);
		var centre = dir * dst;

		Vector3[] verts = new Vector3[numVertsPerStar + 1];
		Vector2[] uvs = new Vector2[numVertsPerStar + 1];
		int[] tris = new int[numVertsPerStar * 3];

		verts[0] = centre;
		uvs[0] = new Vector2 (brightness, spectrumT);

		for (int vertIndex = 0; vertIndex < numVertsPerStar; vertIndex++) {
			float currAngle = (vertIndex / (float) (numVertsPerStar)) * Mathf.PI * 2;
			var vert = centre + (axisA * Mathf.Sin (currAngle) + axisB * Mathf.Cos (currAngle)) * size;
			verts[vertIndex + 1] = vert;
			uvs[vertIndex + 1] = new Vector2 (0, spectrumT);

			if (vertIndex < numVertsPerStar) {
				tris[vertIndex * 3 + 0] = 0 + indexOffset;
				tris[vertIndex * 3 + 1] = (vertIndex + 1) + indexOffset;
				tris[vertIndex * 3 + 2] = ((vertIndex + 1) % (numVertsPerStar) + 1) + indexOffset;
			}
		}

		return (verts, tris, uvs);
	}
}
