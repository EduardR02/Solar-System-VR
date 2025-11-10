using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CelestialBodyGenerator))]
public class PlanetTerrainStreamer : MonoBehaviour {

	[Header("Patch Settings")]
	[Range(8, 96)]
	public int basePatchResolution = 48;
	[Range(1, 6)]
	public int maxSubdivision = 4;
	[Tooltip("Distance thresholds per LOD level (meters). Array length should be maxSubdivision + 1.")]
	public float[] lodTargetDistances = new float[] { 6000f, 3000f, 1500f, 750f, 350f };

	[Header("Visibility")]
	[Range(-1f, 1f)]
	public float backsideCullDot = -0.2f;

	[Header("Player Bias")]
	public float playerCloseRange = 200f;
	public float colliderActivationDistance = 60f;
	[Range(1, 6)]
	public int colliderSubdivision = 4;
	public Ship player;
	public Camera overrideCamera;

	CelestialBodyGenerator generator;
	CelestialBody celestialBody;
	Material terrainMaterial;
	PlanetPatchNode[] roots;
	Camera cachedCamera;
	bool legacyTerrainDisabled;
	readonly List<PlanetTerrainPatch> visiblePatches = new List<PlanetTerrainPatch>(256);
	readonly Stack<PlanetTerrainPatch> patchPool = new Stack<PlanetTerrainPatch>();
	PlanetTerrainPatch colliderPatch;

	int vertsPerEdge;
	int vertexCount;
	int[] triangleTemplate;
	ComputeBuffer vertexBuffer;

	bool initialized;
	bool waitingForSettings;

	void Awake () {
		RefreshCachedReferences ();
	}

	void Start () {
		if (!Application.isPlaying) {
			return;
		}
		Initialize ();
	}

	public void EnsureInitialized () {
		if (!Application.isPlaying) {
			return;
		}
		if (!initialized) {
			Initialize ();
		}
	}

	void LateUpdate () {
		if (!Application.isPlaying || !initialized) {
			return;
		}
		UpdateStreaming ();
	}

	void OnDestroy () {
		ReleaseResources ();
	}

	void Initialize () {
		RefreshCachedReferences ();
		if (initialized) {
			return;
		}

		if (!generator || generator.ShadingProfile == null || generator.ShapeProfile == null) {
			if (!waitingForSettings) {
				Debug.LogWarning ("PlanetTerrainStreamer requires CelestialBodyGenerator with valid shape/shading settings.", this);
				waitingForSettings = true;
			}
			return;
		}
		waitingForSettings = false;

		EnsureLodArray ();

		vertsPerEdge = Mathf.Max (2, basePatchResolution) + 1;
		vertexCount = vertsPerEdge * vertsPerEdge;
		triangleTemplate = BuildTriangleTemplate (vertsPerEdge - 1);

		vertexBuffer = new ComputeBuffer (vertexCount, sizeof (float) * 3);

		terrainMaterial = new Material (generator.ShadingProfile.terrainMaterial) {
			enableInstancing = true
		};
		generator.ShadingProfile.Initialize (generator.ShapeProfile);
		generator.ShadingProfile.SetTerrainProperties (terrainMaterial, generator.HeightRange, generator.BodyScale);

		roots = new PlanetPatchNode[6];
		for (int face = 0; face < 6; face++) {
			var patch = AcquirePatch (new PatchKey (face, 0, 0, 0));
			roots[face] = new PlanetPatchNode { Patch = patch };
		}

		DisableLegacyTerrain ();
		initialized = true;
	}

	void DisableLegacyTerrain () {
		if (legacyTerrainDisabled) {
			return;
		}
		var legacyTerrain = transform.Find ("Terrain Mesh");
		if (legacyTerrain) {
			var renderer = legacyTerrain.GetComponent<MeshRenderer> ();
			if (renderer) {
				renderer.enabled = false;
			}
			var collider = legacyTerrain.GetComponent<MeshCollider> ();
			if (collider) {
				Destroy (collider);
			}
			legacyTerrain.gameObject.SetActive (false);
		}
		legacyTerrainDisabled = true;
	}

	void ReleaseResources () {
		if (vertexBuffer != null) {
			vertexBuffer.Release ();
			vertexBuffer.Dispose ();
			vertexBuffer = null;
		}
		for (int i = 0; i < roots?.Length; i++) {
			roots[i]?.ReleaseRecursive (patchPool);
		}
		while (patchPool.Count > 0) {
			var patch = patchPool.Pop ();
			if (patch != null) {
				Destroy (patch.gameObject);
			}
		}
		if (terrainMaterial) {
			Destroy (terrainMaterial);
		}
		cachedCamera = null;
	}

	void UpdateStreaming () {
		RefreshCachedReferences ();

		Camera cam = GetActiveCamera ();
		if (!cam) {
			return;
		}

		visiblePatches.Clear ();
		Vector3 cameraPosition = cam.transform.position;

		for (int i = 0; i < roots.Length; i++) {
			EvaluateNode (roots[i], cameraPosition);
		}

		UpdateCollider (cameraPosition);
	}

	void EvaluateNode (PlanetPatchNode node, Vector3 cameraPosition) {
		if (node == null || node.Patch == null) {
			return;
		}

		bool culled = ShouldCullPatch (node.Patch, cameraPosition);
		if (culled) {
			node.Patch.SetVisible (false);
			node.ReleaseChildren (patchPool);
			return;
		}

		bool split = ShouldSplitPatch (node.Patch, cameraPosition);
		if (split && node.Patch.Key.level < maxSubdivision) {
			node.EnsureChildren (this, patchPool);
			node.Patch.SetVisible (false);
			for (int i = 0; i < node.Children.Length; i++) {
				EvaluateNode (node.Children[i], cameraPosition);
			}
		} else {
			node.ReleaseChildren (patchPool);
			node.Patch.BuildIfNeeded ();
			node.Patch.SetVisible (true);
			visiblePatches.Add (node.Patch);
		}
	}

	bool ShouldCullPatch (PlanetTerrainPatch patch, Vector3 cameraPosition) {
		Vector3 planetCenter = transform.position;
		Vector3 patchDir = patch.GetWorldCenter (transform) - planetCenter;
		Vector3 cameraDir = cameraPosition - planetCenter;

		if (patchDir.sqrMagnitude < 0.0001f || cameraDir.sqrMagnitude < 0.0001f) {
			return false;
		}

		float dot = Vector3.Dot (patchDir.normalized, cameraDir.normalized);
		return dot < backsideCullDot;
	}

	bool ShouldSplitPatch (PlanetTerrainPatch patch, Vector3 cameraPosition) {
		Vector3 worldCenter = patch.GetWorldCenter (transform);
		float surfaceDistance = CalculateSurfaceDistance (cameraPosition, worldCenter);
		float threshold = GetLodDistance (patch.Key.level);
		bool nearCamera = surfaceDistance < threshold;
		bool forceDetail = ShouldForcePlayerDetail (patch);
		return nearCamera || forceDetail;
	}

	bool ShouldForcePlayerDetail (PlanetTerrainPatch patch) {
		if (!player || player.ReferenceBody == null || player.ReferenceBody != celestialBody) {
			return false;
		}

		Vector3 planetCenter = transform.position;
		Vector3 playerPos = player.transform.position;
		float altitude = Vector3.Distance (playerPos, planetCenter) - generator.BodyScale;
		if (altitude > playerCloseRange) {
			return false;
		}

		Vector3 localDir = (playerPos - planetCenter).normalized;
		return patch.ContainsDirection (transform.InverseTransformDirection (localDir)) && patch.Key.level < colliderSubdivision;
	}

	void UpdateCollider (Vector3 cameraPosition) {
		RefreshCachedReferences ();
		if (!TryGetTargetDirection (cameraPosition, out Vector3 localDir, out float altitude)) {
			SetColliderPatch (null);
			return;
		}

		if (altitude > colliderActivationDistance) {
			SetColliderPatch (null);
			return;
		}

		PlanetTerrainPatch bestPatch = null;
		int bestLevel = -1;

		for (int i = 0; i < visiblePatches.Count; i++) {
			var patch = visiblePatches[i];
			if (!patch.ContainsDirection (localDir)) {
				continue;
			}
			if (patch.Key.level > bestLevel) {
				bestLevel = patch.Key.level;
				bestPatch = patch;
			}
		}

		SetColliderPatch (bestPatch);
	}

	void SetColliderPatch (PlanetTerrainPatch patch) {
		if (colliderPatch == patch) {
			return;
		}

		if (colliderPatch != null) {
			colliderPatch.EnableCollider (false);
			colliderPatch = null;
		}

		if (patch != null) {
			patch.EnableCollider (true);
			colliderPatch = patch;
		}
	}

	float CalculateSurfaceDistance (Vector3 cameraPosition, Vector3 patchCenter) {
		Vector3 planetCenter = transform.position;
		Vector3 cameraVector = cameraPosition - planetCenter;
		Vector3 patchVector = patchCenter - planetCenter;
		float bodyRadius = generator ? Mathf.Max (generator.BodyScale, 1f) : 1f;

		float altitude = Mathf.Max (0f, cameraVector.magnitude - bodyRadius);
		float angle = Vector3.Angle (cameraVector, patchVector) * Mathf.Deg2Rad;
		float arcDistance = angle * bodyRadius;

		return Mathf.Sqrt (arcDistance * arcDistance + altitude * altitude);
	}

	float GetLodDistance (int level) {
		int index = Mathf.Clamp (level, 0, lodTargetDistances.Length - 1);
		float baseDistance = lodTargetDistances[index];
		float bodyRadius = generator ? Mathf.Max (generator.BodyScale, 1f) : 1f;
		const float referenceRadius = 6000f;
		return baseDistance * (bodyRadius / referenceRadius);
	}

	void EnsureLodArray () {
		int required = maxSubdivision + 1;
		if (lodTargetDistances == null || lodTargetDistances.Length < required) {
			float baseDistance = (lodTargetDistances != null && lodTargetDistances.Length > 0)
				? lodTargetDistances[0]
				: generator.BodyScale * 20f;
			var array = new float[required];
			for (int i = 0; i < required; i++) {
				if (lodTargetDistances != null && i < lodTargetDistances.Length) {
					array[i] = lodTargetDistances[i];
				} else {
					array[i] = Mathf.Max (10f, baseDistance / Mathf.Pow (2f, i));
				}
			}
			lodTargetDistances = array;
		}
	}

	int[] BuildTriangleTemplate (int quadsPerEdge) {
		int[] tris = new int[quadsPerEdge * quadsPerEdge * 6];
		int index = 0;
		for (int y = 0; y < quadsPerEdge; y++) {
			for (int x = 0; x < quadsPerEdge; x++) {
				int i0 = y * vertsPerEdge + x;
				int i1 = i0 + 1;
				int i2 = i0 + vertsPerEdge;
				int i3 = i2 + 1;
				tris[index++] = i0;
				tris[index++] = i1;
				tris[index++] = i2;
				tris[index++] = i1;
				tris[index++] = i3;
				tris[index++] = i2;
			}
		}
		return tris;
	}

	PlanetTerrainPatch AcquirePatch (PatchKey key) {
		PlanetTerrainPatch patch = (patchPool.Count > 0) ? patchPool.Pop () : new PlanetTerrainPatch (this, transform, vertexCount);
		patch.Configure (key, terrainMaterial);
		patch.BuildIfNeeded ();
		return patch;
	}

	void PopulateSampleDirections (PatchKey key, Vector3[] destination) {
		int subdivisions = 1 << key.level;
		float patchScale = 1f / subdivisions;

		for (int y = 0; y < vertsPerEdge; y++) {
			for (int x = 0; x < vertsPerEdge; x++) {
				float localU = x / (float) (vertsPerEdge - 1);
				float localV = y / (float) (vertsPerEdge - 1);
				float u = (key.x + localU) * patchScale;
				float v = (key.y + localV) * patchScale;
				int index = y * vertsPerEdge + x;
				destination[index] = CubeSphereUtility.CubeToSphere (key.face, u, v);
			}
		}
	}

	Vector3 SampleHeightsAndShading (Vector3[] directions, Vector3[] vertices, out Vector4[] shading) {
		vertexBuffer.SetData (directions);
		float[] heights = generator.ShapeProfile.CalculateHeights (vertexBuffer);
		shading = generator.ShadingProfile.GenerateShadingData (vertexBuffer);

		Vector3 centroid = Vector3.zero;
		for (int i = 0; i < directions.Length; i++) {
			Vector3 vertex = directions[i] * heights[i];
			vertices[i] = vertex;
			centroid += vertex;
		}

		int count = Mathf.Max (1, directions.Length);
		return centroid / count;
	}

	Camera GetActiveCamera () {
		if (cachedCamera && cachedCamera.enabled) {
			return cachedCamera;
		}

		if (overrideCamera && overrideCamera.enabled) {
			cachedCamera = overrideCamera;
			return cachedCamera;
		}

		var main = Camera.main;
		if (main && main.enabled) {
			cachedCamera = main;
			return cachedCamera;
		}

		if (Camera.current && Camera.current.enabled) {
			cachedCamera = Camera.current;
			return cachedCamera;
		}

		var allCams = Camera.allCameras;
		for (int i = 0; i < allCams.Length; i++) {
			if (allCams[i] && allCams[i].enabled) {
				cachedCamera = allCams[i];
				return cachedCamera;
			}
		}

		return null;
	}

	bool TryGetTargetDirection (Vector3 referencePosition, out Vector3 localDir, out float altitude) {
		Vector3 planetCenter = transform.position;
		float bodyScale = generator ? generator.BodyScale : 0f;

		if (player && player.ReferenceBody == celestialBody) {
			Vector3 playerPos = player.transform.position;
			localDir = transform.InverseTransformDirection ((playerPos - planetCenter).normalized);
			altitude = Vector3.Distance (playerPos, planetCenter) - bodyScale;
			return true;
		}

		Vector3 refVector = referencePosition - planetCenter;
		if (refVector.sqrMagnitude > 0.0001f) {
			localDir = transform.InverseTransformDirection (refVector.normalized);
			altitude = Mathf.Max (0f, refVector.magnitude - bodyScale);
			return true;
		}

		localDir = Vector3.up;
		altitude = float.MaxValue;
		return false;
	}

	void RefreshCachedReferences () {
		if (!generator) {
			generator = GetComponent<CelestialBodyGenerator> ();
		}
		if (!player) {
			player = FindFirstObjectByType<Ship> ();
		}
		if (!celestialBody) {
			celestialBody = GetComponentInParent<CelestialBody> ();
		}
	}

	Vector3 GetPatchNormal (PatchKey key) {
		int subdivisions = 1 << key.level;
		float u = (key.x + 0.5f) / subdivisions;
		float v = (key.y + 0.5f) / subdivisions;
		return CubeSphereUtility.CubeToSphere (key.face, u, v);
	}

	Vector3 GetApproxLocalCenter (PatchKey key) {
		float radius = generator ? generator.BodyScale : 1f;
		return GetPatchNormal (key) * radius;
	}

	#region Nested types

	class PlanetPatchNode {
		public PlanetTerrainPatch Patch;
		public PlanetPatchNode[] Children;

		public void EnsureChildren (PlanetTerrainStreamer owner, Stack<PlanetTerrainPatch> pool) {
			if (Children != null && Children.Length == 4) {
				return;
			}
			Children = new PlanetPatchNode[4];
			int childLevel = Patch.Key.level + 1;
			for (int i = 0; i < 4; i++) {
				int offsetX = i % 2;
				int offsetY = i / 2;
				int childX = Patch.Key.x * 2 + offsetX;
				int childY = Patch.Key.y * 2 + offsetY;
				var key = new PatchKey (Patch.Key.face, childLevel, childX, childY);
				var childPatch = owner.AcquirePatch (key);
				Children[i] = new PlanetPatchNode { Patch = childPatch };
			}
		}

		public void ReleaseChildren (Stack<PlanetTerrainPatch> pool) {
			if (Children == null) {
				return;
			}
			for (int i = 0; i < Children.Length; i++) {
				Children[i]?.ReleaseRecursive (pool);
			}
			Children = null;
		}

		public void ReleaseRecursive (Stack<PlanetTerrainPatch> pool) {
			ReleaseChildren (pool);
			if (Patch != null) {
				Patch.Deactivate ();
				pool.Push (Patch);
				Patch = null;
			}
		}
	}

	struct PatchKey {
		public readonly int face;
		public readonly int level;
		public readonly int x;
		public readonly int y;

		public PatchKey (int face, int level, int x, int y) {
			this.face = face;
			this.level = level;
			this.x = x;
			this.y = y;
		}
	}

	class PlanetTerrainPatch {
		public readonly GameObject gameObject;
		readonly Transform transform;
		readonly MeshFilter filter;
		readonly MeshRenderer renderer;
		MeshCollider meshCollider;
		readonly Mesh mesh;

		readonly Vector3[] sampleDirections;
		readonly Vector3[] vertices;
		Vector4[] shadingData;

		Rect faceRect;
		public PatchKey Key { get; private set; }
		bool meshDirty = true;

		Vector3 localCenter;
		Vector3 localNormal;

		readonly PlanetTerrainStreamer owner;

		public PlanetTerrainPatch (PlanetTerrainStreamer owner, Transform parent, int vertexCount) {
			this.owner = owner;
			gameObject = new GameObject ("Terrain Patch");
			transform = gameObject.transform;
			transform.SetParent (parent, false);
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;
			gameObject.layer = parent.gameObject.layer;

			filter = gameObject.AddComponent<MeshFilter> ();
			renderer = gameObject.AddComponent<MeshRenderer> ();
			mesh = new Mesh { name = "PlanetPatchMesh" };
			mesh.MarkDynamic ();
			filter.sharedMesh = mesh;

			sampleDirections = new Vector3[vertexCount];
			vertices = new Vector3[vertexCount];
			shadingData = new Vector4[vertexCount];

			gameObject.SetActive (false);
		}

		public void Configure (PatchKey key, Material material) {
			Key = key;
			renderer.sharedMaterial = material;
			meshDirty = true;

			int subdivisions = 1 << key.level;
			float patchScale = 1f / subdivisions;
			faceRect = new Rect (key.x * patchScale, key.y * patchScale, patchScale, patchScale);
			gameObject.name = $"Patch_f{key.face}_l{key.level}_{key.x}_{key.y}";
			gameObject.SetActive (true);

			localNormal = owner.GetPatchNormal (key);
			localCenter = owner.GetApproxLocalCenter (key);
		}

		public void BuildIfNeeded () {
			if (!meshDirty) {
				return;
			}

			owner.PopulateSampleDirections (Key, sampleDirections);
			Vector3 centroid = owner.SampleHeightsAndShading (sampleDirections, vertices, out shadingData);

			mesh.Clear ();
			mesh.SetVertices (vertices);
			mesh.SetTriangles (owner.triangleTemplate, 0, true);
			mesh.SetUVs (0, shadingData);
			mesh.RecalculateNormals ();
			mesh.RecalculateBounds ();

			if (centroid.sqrMagnitude > 0.0001f) {
				localCenter = centroid;
				localNormal = centroid.normalized;
			} else {
				localCenter = owner.GetApproxLocalCenter (Key);
				localNormal = owner.GetPatchNormal (Key);
			}

			filter.sharedMesh = mesh;
			meshDirty = false;
		}

		public void SetVisible (bool visible) {
			renderer.enabled = visible;
		}

		public void Deactivate () {
			renderer.enabled = false;
			if (meshCollider) {
				meshCollider.enabled = false;
			}
			gameObject.SetActive (false);
		}

		public Vector3 GetWorldCenter (Transform root) {
			return root.TransformPoint (localCenter);
		}

		public Vector3 GetWorldNormal (Transform root) {
			return root.TransformDirection (localNormal).normalized;
		}

		public bool ContainsDirection (Vector3 localDirection) {
			CubeSphereUtility.DirectionToFaceUV (localDirection, out int face, out Vector2 uv);
			if (face != Key.face) {
				return false;
			}
			return faceRect.Contains (uv);
		}

		public void EnableCollider (bool enable) {
			if (enable) {
				if (meshCollider == null) {
					meshCollider = gameObject.AddComponent<MeshCollider> ();
					meshCollider.convex = false;
					meshCollider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning;
				}
				meshCollider.sharedMesh = mesh;
				meshCollider.enabled = true;
			} else if (meshCollider) {
				meshCollider.enabled = false;
			}
		}
	}

	#endregion
}
