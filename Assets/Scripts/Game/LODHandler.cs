using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LODHandler : MonoBehaviour {
	[Header ("LOD screen heights")]
	// LOD level is determined by body's screen height (1 = taking up entire screen, 0 = teeny weeny speck) 
	public float lod1Threshold = .5f;
	public float lod2Threshold = .2f;

	[Header ("Update frequency (seconds)")]
	[Min (0.05f)]
	public float updateFrequency = 0.25f;

	[Header ("Debug")]
	public bool debug;
	public CelestialBody debugBody;

	Camera cam;
	Transform camT;
	CelestialBody[] bodies;
	CelestialBodyGenerator[] generators;
	private float timePassedSinceUpdate;

	void Start () {
		timePassedSinceUpdate = updateFrequency;	// so that LODs are updated on first frame
		if (Application.isPlaying) {
			EnsureCamera ();
			bodies = FindObjectsByType<CelestialBody> (FindObjectsSortMode.None);
			generators = new CelestialBodyGenerator[bodies.Length];
			for (int i = 0; i < generators.Length; i++) {
				generators[i] = bodies[i].GetComponentInChildren<CelestialBodyGenerator> ();
			}
		}
	}

	void Update () {
		EnsureCamera ();
		DebugLODInfo ();
		timePassedSinceUpdate += Time.deltaTime;

		float targetInterval = Mathf.Max (updateFrequency, 0.05f);
		if (Application.isPlaying && timePassedSinceUpdate >= targetInterval) {
			HandleLODs ();
			timePassedSinceUpdate = 0f;
		}

	}

	void EnsureCamera () {
		if (cam == null) {
			cam = Camera.main;
			if (cam != null) {
				camT = cam.transform;
			}
		}
	}

	void HandleLODs () {
		for (int i = 0; i < bodies.Length; i++) {
			if (generators[i] != null) {
				float screenHeight = CalculateScreenHeightManual (bodies[i]);
				int lodIndex = CalculateLODIndex (screenHeight);
				generators[i].SetLOD (lodIndex);
			}
		}
	}

	int CalculateLODIndex (float screenHeight) {
		if (screenHeight > lod1Threshold) {
			return 0;
		} else if (screenHeight > lod2Threshold) {
			return 1;
		}
		return 2;
	}

	void DebugLODInfo () {
		if (debugBody && debug) {
			float h = CalculateScreenHeightManual (debugBody);
			int index = CalculateLODIndex (h);
			Debug.Log ($"Screen height of {debugBody.name}: {h} (lod = {index})");
		}
	}

	// don't want other scripts potentially messing with the camera's transform
	private float CalculateScreenHeight (CelestialBody body) {
		if (cam == null) {
			cam = Camera.main;
			camT = cam.transform;
		}
		Quaternion originalRot = camT.rotation;
		Vector3 bodyCentre = body.transform.position;
		camT.LookAt (bodyCentre);

		Vector3 viewA = cam.WorldToViewportPoint (bodyCentre - camT.up * body.radius);
		Vector3 viewB = cam.WorldToViewportPoint (bodyCentre + camT.up * body.radius);
		float screenHeight = Mathf.Abs (viewA.y - viewB.y);
		camT.rotation = originalRot;

		return screenHeight;
	}

	// matches CalculateScreenHeight exactly without changing the camera's transform
	// didn't want to mess with changing the camera's transform
	// sadly no way to precompute final matrix because rotation is different for each body
	float CalculateScreenHeightManual (CelestialBody body) {
		if (cam == null) {
			cam = Camera.main;
			camT = cam.transform;
		}
		Vector3 bodyCentre = body.transform.position;
		// look direction vector as rotation
		Quaternion targetRot = Quaternion.LookRotation ((bodyCentre - camT.position).normalized, Vector3.up);
		Quaternion deltaRot = Quaternion.Inverse (camT.rotation) * targetRot;
		// flip z rotation because Unity convention
		deltaRot.z *= -1;
		// "would be" camT.up if we were to rotate camT to targetRot
		Vector3 rotatedUp = targetRot * Vector3.up;
		//Matrix4x4 manualViewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS (camT.position, targetRot, new Vector3(1, 1, -1)));
		Matrix4x4 R = Matrix4x4.Rotate(deltaRot);
		//Matrix4x4 MVP = cam.projectionMatrix * customViewMatrix; // Skipping M, point in world coordinates
		Matrix4x4 VP = cam.projectionMatrix * R * cam.worldToCameraMatrix; // Skipping M, point in world coordinates
		//Debug.Log ($"view matrix custom:\n {manualViewMatrix}, R * unity view matrix:\n {R*cam.worldToCameraMatrix} Z flipped :\n {R}");
		Vector3 viewA = VP.MultiplyPoint (bodyCentre - rotatedUp * body.radius);
		Vector3 viewB = VP.MultiplyPoint (bodyCentre + rotatedUp * body.radius);
		// normalize y to 0 - 1 from (-1) to 1 and get screen height
		float screenHeight = Mathf.Abs (viewA.y - viewB.y) * 0.5f;
		return screenHeight;
	}
}
