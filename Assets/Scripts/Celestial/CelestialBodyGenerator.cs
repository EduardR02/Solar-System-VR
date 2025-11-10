using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteInEditMode, RequireComponent(typeof(PlanetTerrainStreamer))]
public class CelestialBodyGenerator : MonoBehaviour {

	public enum PreviewMode { LOD0, LOD1, LOD2, CollisionRes }
	public ResolutionSettings resolutionSettings;
	public PreviewMode previewMode;

	public bool logTimers;

	public CelestialBodySettings body;

	bool debugDoubleUpdate = true;
	int debug_numUpdates;

	// Private variables
	Mesh previewMesh;
	Mesh[] lodMeshes;
	PlanetTerrainStreamer terrainStreamer;

	ComputeBuffer vertexBuffer;
	int vertexBufferCapacity;
	readonly Dictionary<int, CachedVertexData> vertexCache = new Dictionary<int, CachedVertexData> ();
	bool heightRangeInitialized;

	bool shapeSettingsUpdated;
	bool shadingNoiseSettingsUpdated;
	Camera cam;

	Vector2 heightMinMax;

	// Game mode data 
	int activeLODIndex = -1;
	MeshFilter terrainMeshFilter;

	static Dictionary<int, SphereMesh> sphereGenerators;

	void Awake () {
		terrainStreamer = GetComponent<PlanetTerrainStreamer> ();
	}

	void Start () {
		if (InGameMode) {
			cam = Camera.main;
			HandleGameModeGeneration ();
		}
	}

	void OnEnable () {
		if (Application.isPlaying) {
			PlanetEnvironmentRegistry.Register (this);
		}
	}

	void OnDisable () {
		if (Application.isPlaying) {
			PlanetEnvironmentRegistry.Unregister (this);
		}
	}

	void Update () {
		if (InEditMode) {
			HandleEditModeGeneration ();
		}

		if (Input.GetKeyDown (KeyCode.O)) {
			//body.shading.atmosphereSettings.useOptimVersion = !body.shading.atmosphereSettings.useOptimVersion;
		}

		if (Input.GetKeyDown (KeyCode.I)) {
			//	var m = terrainMeshFilter.GetComponent<MeshRenderer> ();
			//m.enabled = !m.enabled;
		}
	}

	// Handles creation of celestial body when entering game mode using the streamed terrain pipeline.
	void HandleGameModeGeneration () {
		if (!CanGenerateMesh ()) {
			Debug.Log ("Could not generate mesh");
			ReleaseAllBuffers ();
			return;
		}

		InitializeStreamedRuntimeData ();

		ReleaseAllBuffers ();
	}

	void InitializeStreamedRuntimeData () {
		if (!terrainStreamer) {
			terrainStreamer = GetComponent<PlanetTerrainStreamer> ();
		}

		if (!terrainStreamer) {
			Debug.LogError ($"[{name}] PlanetTerrainStreamer component is required for runtime generation.", this);
			return;
		}

		EnsureHeightRangeCached ();
		terrainMeshFilter = null;
		terrainStreamer.EnsureInitialized ();
	}

	// Handles creation of celestial body in the editor
	// This allows for updating the shape/shading settings
	void HandleEditModeGeneration () {
		if (InEditMode) {
			ComputeHelper.shouldReleaseEditModeBuffers -= ReleaseAllBuffers;
			ComputeHelper.shouldReleaseEditModeBuffers += ReleaseAllBuffers;
		}

		if (CanGenerateMesh ()) {
			// Update shape settings and shading noise
			if (shapeSettingsUpdated) {
				shapeSettingsUpdated = false;
				shadingNoiseSettingsUpdated = false;

				var terrainMeshTimer = System.Diagnostics.Stopwatch.StartNew ();
				heightMinMax = GenerateTerrainMesh (ref previewMesh, PickTerrainRes ());
				heightRangeInitialized = true;

				LogTimer (terrainMeshTimer, "Generate terrain mesh");
				DrawEditModeMesh ();
			}
			// If only shading noise has changed, update it separately from shape to save time
			else if (shadingNoiseSettingsUpdated) {
				shadingNoiseSettingsUpdated = false;
				ComputeHelper.CreateStructuredBuffer<Vector3> (ref vertexBuffer, previewMesh.vertices);
				body.shading.Initialize (body.shape);
				Vector4[] shadingData = body.shading.GenerateShadingData (vertexBuffer, previewMesh.vertexCount);
				previewMesh.SetUVs (0, shadingData);

				// FIXME/TODO: Double-update workaround for "invalid data from compute shader"
				// Running the shading a second time fixes it.
				//
				// LIKELY ROOT CAUSE: ComputeHelper had 2 critical bugs that were fixed:
				//   1. ComputeHelper.Run() used .y for Z-axis dispatch (line 19) - causing under-dispatch
				//   2. PackFloats() returned input array instead of packed array (line 134) - returned zeros
				// These bugs would cause compute shaders to:
				//   - Miss work groups in 3D dispatches (height/shading compute shaders)
				//   - Return uninitialized/zero data on first run
				//   - Work correctly on second run after GPU state initialized
				//
				// RECOMMENDATION: Test if this workaround is still needed after ComputeHelper fixes.
				// If zeros still appear on first run, check for GPU/CPU synchronization issues.
				//
				debug_numUpdates++;
				if (debugDoubleUpdate && debug_numUpdates < 2) {
					shadingNoiseSettingsUpdated = true;
					HandleEditModeGeneration ();
				}
				if (debug_numUpdates == 2) {
					debug_numUpdates = 0;
				}

			}
		}

		// Update shading
		if (body.shading) {
			// Set material properties
			EnsureHeightRangeCached ();
			body.shading.Initialize (body.shape);
			body.shading.SetTerrainProperties (body.shading.terrainMaterial, heightMinMax, BodyScale);
		}

		ReleaseAllBuffers (); //
	}

	public void SetLOD (int lodIndex) {
		if (!InEditMode || terrainMeshFilter == null || lodMeshes == null || lodMeshes.Length == 0) {
			return;
		}

		int clampedIndex = Mathf.Clamp (lodIndex, 0, lodMeshes.Length - 1);
		if (clampedIndex != activeLODIndex) {
			activeLODIndex = clampedIndex;
			terrainMeshFilter.sharedMesh = lodMeshes[activeLODIndex];
		}
	}

	public void OnShapeSettingChanged () {
		shapeSettingsUpdated = true;
		InvalidateCachedData ();
		PlanetEnvironmentRegistry.MarkDirty ();
	}

	public void OnShadingNoiseSettingChanged () {
		shadingNoiseSettingsUpdated = true;
		InvalidateCachedData ();
		PlanetEnvironmentRegistry.MarkDirty ();
	}

	void OnValidate () {
		if (body) {
			if (body.shape) {
				body.shape.OnSettingChanged -= OnShapeSettingChanged;
				body.shape.OnSettingChanged += OnShapeSettingChanged;
			}
			if (body.shading) {
				body.shading.OnSettingChanged -= OnShadingNoiseSettingChanged;
				body.shading.OnSettingChanged += OnShadingNoiseSettingChanged;
			}
		}

		if (resolutionSettings != null) {
			resolutionSettings.ClampResolutions ();
		}
		OnShapeSettingChanged ();
	}

	// Generates terrain mesh based on heights generated by the Shape object
	// Shading data from the Shading object is stored in the mesh uvs
	// Returns the min/max height of the terrain
	Vector2 GenerateTerrainMesh (ref Mesh mesh, int resolution) {
		var (vertices, triangles) = CreateSphereVertsAndTris (resolution);
		ComputeHelper.CreateStructuredBuffer<Vector3> (ref vertexBuffer, vertices);

		float edgeLength = (vertices[triangles[0]] - vertices[triangles[1]]).magnitude;

		// Set heights
		float[] heights = body.shape.CalculateHeights (vertexBuffer, vertices.Length);

		// Perturb vertices to give terrain a less perfectly smooth appearance
		if (body.shape.perturbVertices && body.shape.perturbCompute) {
			ComputeShader perturbShader = body.shape.perturbCompute;
			float maxperturbStrength = body.shape.perturbStrength * edgeLength / 2;

			perturbShader.SetBuffer (0, "points", vertexBuffer);
			perturbShader.SetInt ("numPoints", vertices.Length);
			perturbShader.SetFloat ("maxStrength", maxperturbStrength);

			ComputeHelper.Run (perturbShader, vertices.Length);
			Vector3[] pertData = new Vector3[vertices.Length];
			vertexBuffer.GetData (vertices);
		}

		// Calculate terrain min/max height and set heights of vertices
		float minHeight = float.PositiveInfinity;
		float maxHeight = float.NegativeInfinity;
		for (int i = 0; i < heights.Length; i++) {
			float height = heights[i];
			vertices[i] *= height;
			minHeight = Mathf.Min (minHeight, height);
			maxHeight = Mathf.Max (maxHeight, height);
		}

		// Create mesh
		CreateMesh (ref mesh, vertices.Length);
		mesh.SetVertices (vertices);
		mesh.SetTriangles (triangles, 0, true);
		mesh.RecalculateNormals (); //

		// Shading noise data
		body.shading.Initialize (body.shape);
		Vector4[] shadingData = body.shading.GenerateShadingData (vertexBuffer, vertices.Length);
		mesh.SetUVs (0, shadingData);

		// Create crude tangents (vectors perpendicular to surface normal)
		// This is needed (even though normal mapping is being done with triplanar)
		// because surfaceshader wants normals in tangent space
		var normals = mesh.normals;
		var crudeTangents = new Vector4[mesh.vertices.Length];
		for (int i = 0; i < vertices.Length; i++) {
			Vector3 normal = normals[i];
			crudeTangents[i] = new Vector4 (-normal.z, 0, normal.x, 1);
		}
		mesh.SetTangents (crudeTangents);

		return new Vector2 (minHeight, maxHeight);
	}

	CachedVertexData EnsureVertexDataForResolution (int resolution, bool updateHeightRange = false) {
		if (!vertexCache.TryGetValue (resolution, out var cachedData) || cachedData.vertices == null || cachedData.vertices.Length == 0) {
			cachedData = BuildVertexData (resolution);
			vertexCache[resolution] = cachedData;
		}

		if (updateHeightRange) {
			heightMinMax = cachedData.heightRange;
			heightRangeInitialized = true;
		}

		return cachedData;
	}

	CachedVertexData BuildVertexData (int resolution) {
		var (directions, _) = CreateSphereVertsAndTris (resolution);
		EnsureVertexBufferCapacity (directions.Length);
		vertexBuffer.SetData (directions, 0, 0, directions.Length);

		float[] heights = body.shape.CalculateHeights (vertexBuffer, directions.Length);
		var vertices = new Vector3[directions.Length];
		float minHeight = float.PositiveInfinity;
		float maxHeight = float.NegativeInfinity;
		for (int i = 0; i < directions.Length; i++) {
			float height = heights[i];
			vertices[i] = directions[i] * height;
			minHeight = Mathf.Min (minHeight, height);
			maxHeight = Mathf.Max (maxHeight, height);
		}

		return new CachedVertexData {
			vertices = vertices,
			heightRange = new Vector2 (minHeight, maxHeight)
		};
	}

	void EnsureVertexBufferCapacity (int requiredVertexCount) {
		if (vertexBuffer != null && vertexBufferCapacity >= requiredVertexCount) {
			return;
		}

		ComputeHelper.Release (vertexBuffer);
		vertexBuffer = new ComputeBuffer (requiredVertexCount, Marshal.SizeOf (typeof (Vector3)));
		vertexBufferCapacity = requiredVertexCount;
	}

	void EnsureHeightRangeCached () {
		if (heightRangeInitialized) {
			return;
		}
		int sampleResolution = (resolutionSettings != null) ? Mathf.Max (8, resolutionSettings.lod1) : 64;
		EnsureVertexDataForResolution (sampleResolution, true);
	}

	void InvalidateCachedData () {
		vertexCache.Clear ();
		heightRangeInitialized = false;
	}

	void CreateMesh (ref Mesh mesh, int numVertices) {
		const int vertexLimit16Bit = (1 << 16) - 1; // 65535
		if (mesh == null) {
			mesh = new Mesh ();
		} else {
			mesh.Clear ();
		}
		mesh.indexFormat = (numVertices < vertexLimit16Bit) ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
	}

	void DrawEditModeMesh () {
		GameObject terrainHolder = GetOrCreateMeshObject ("Terrain Mesh", previewMesh, body.shading.terrainMaterial);
	}

	// Gets child object with specified name.
	// If it doesn't exist, then creates object with that name, adds mesh renderer/filter and attaches mesh and material
	GameObject GetOrCreateMeshObject (string name, Mesh mesh, Material material) {
		// Find/create object
		var child = transform.Find (name);
		if (!child) {
			child = new GameObject (name).transform;
			child.parent = transform;
			child.localPosition = Vector3.zero;
			child.localRotation = Quaternion.identity;
			child.localScale = Vector3.one;
			child.gameObject.layer = gameObject.layer;
		}

		// Add mesh components
		MeshFilter filter;
		if (!child.TryGetComponent<MeshFilter> (out filter)) {
			filter = child.gameObject.AddComponent<MeshFilter> ();
		}
		filter.sharedMesh = mesh;

		MeshRenderer renderer;
		if (!child.TryGetComponent<MeshRenderer> (out renderer)) {
			renderer = child.gameObject.AddComponent<MeshRenderer> ();
		}
		renderer.sharedMaterial = material;

		return child.gameObject;
	}

	public int PickTerrainRes () {
		if (!Application.isPlaying) {
			switch (previewMode) {
				case PreviewMode.LOD0:
					return resolutionSettings.lod0;
				case PreviewMode.LOD1:
					return resolutionSettings.lod1;
				case PreviewMode.LOD2:
					return resolutionSettings.lod2;
				case PreviewMode.CollisionRes:
					return resolutionSettings.collider;
			}
		}

		return 0;

	}

	// Radius of the ocean (0 if no ocean exists)
	public float GetOceanRadius () {
		if (!body.shading.hasOcean) {
			return 0;
		}
		EnsureHeightRangeCached ();
		return UnscaledOceanRadius * BodyScale;
	}

	float UnscaledOceanRadius {
		get {
			return Mathf.Lerp (heightMinMax.x, 1, body.shading.oceanLevel);
		}
	}

	public float BodyScale {
		get {
			// Body radius is determined by the celestial body class,
			// which sets the local scale of the generator object (this object)
			return transform.localScale.x;
		}
	}

	public Vector2 HeightRange {
		get {
			EnsureHeightRangeCached ();
			return heightMinMax;
		}
	}

	public CelestialBodyShading ShadingProfile {
		get {
			return body ? body.shading : null;
		}
	}

	public CelestialBodyShape ShapeProfile {
		get {
			return body ? body.shape : null;
		}
	}

	// Generate sphere (or reuse if already generated) and return a copy of the vertices and triangles
	(Vector3[] vertices, int[] triangles) CreateSphereVertsAndTris (int resolution) {
		if (sphereGenerators == null) {
			sphereGenerators = new Dictionary<int, SphereMesh> ();
		}

		if (!sphereGenerators.ContainsKey (resolution)) {
			sphereGenerators.Add (resolution, new SphereMesh (resolution));
		}

		var generator = sphereGenerators[resolution];

		var vertices = new Vector3[generator.Vertices.Length];
		var triangles = new int[generator.Triangles.Length];
		System.Array.Copy (generator.Vertices, vertices, vertices.Length);
		System.Array.Copy (generator.Triangles, triangles, triangles.Length);
		return (vertices, triangles);
	}

	void ReleaseAllBuffers () {
		ComputeHelper.Release (vertexBuffer);
		vertexBuffer = null;
		vertexBufferCapacity = 0;
		if (body.shape) {
			body.shape.ReleaseBuffers ();
		}
		if (body.shading) {
			body.shading.ReleaseBuffers ();
		}
	}

	void OnDestroy () {
		ReleaseAllBuffers ();
		vertexCache.Clear ();
		heightRangeInitialized = false;
		if (Application.isPlaying) {
			PlanetEnvironmentRegistry.Unregister (this);
		}
	}

	bool CanGenerateMesh () {
		if (Application.isPlaying) {
			return body && body.shape && body.shape.heightMapCompute;
		}
		return ComputeHelper.CanRunEditModeCompute && body && body.shape && body.shape.heightMapCompute;
	}

	void LogTimer (System.Diagnostics.Stopwatch sw, string text) {
		if (logTimers) {
			Debug.Log (text + " " + sw.ElapsedMilliseconds + " ms.");
		}
	}

	bool InGameMode {
		get {
			return Application.isPlaying;
		}
	}

	bool InEditMode {
		get {
			return !Application.isPlaying;
		}
	}

	public Vector3[] GetMeshVertices (int lodIndex) {
		int resolution = 0;
		if (resolutionSettings != null) {
			resolution = resolutionSettings.GetLODResolution (Mathf.Clamp (lodIndex, 0, ResolutionSettings.numLODLevels - 1));
		}
		if (resolution <= 0) {
			resolution = Mathf.Max (8, (resolutionSettings != null) ? resolutionSettings.lod1 : 64);
		}
		return EnsureVertexDataForResolution (resolution).vertices;
	}

	public bool MeshesAreGenerated {
		get {
			return vertexCache.Count > 0 || (lodMeshes != null && lodMeshes.Length > 0);
		}
	}

	struct CachedVertexData {
		public Vector3[] vertices;
		public Vector2 heightRange;
	}

	public class TerrainData {
		public float[] heights;
		public Vector4[] uvs;
	}

	[System.Serializable]
	public class ResolutionSettings {

		public const int numLODLevels = 3;
		const int maxAllowedResolution = 500;

		public int lod0 = 300;
		public int lod1 = 100;
		public int lod2 = 50;
		public int collider = 100;

		public int GetLODResolution (int lodLevel) {
			switch (lodLevel) {
				case 0:
					return lod0;
				case 1:
					return lod1;
				case 2:
					return lod2;
			}
			return lod2;
		}

		public void ClampResolutions () {
			lod0 = Mathf.Min (maxAllowedResolution, lod0);
			lod1 = Mathf.Min (maxAllowedResolution, lod1);
			lod2 = Mathf.Min (maxAllowedResolution, lod2);
			collider = Mathf.Min (maxAllowedResolution, collider);
		}
	}

}
