using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.XR;
#endif

[RequireComponent (typeof (Camera))]
public class PlanetShellRenderer : MonoBehaviour {

	const float sortDistanceThreshold = 10f;

	[Range (8, 256)]
	public int shellResolution = 64;

	[SerializeField] Shader oceanShellShader;
	[SerializeField] Shader atmosphereShellShader;

	Mesh shellMesh;
	Material oceanMaterial;
	Material atmosphereMaterial;
	Camera cam;
	Light sunLight;
	Vector3 lastSortPosition = Vector3.one * float.MaxValue;
	int lastRegistryVersion = -1;
	CommandBuffer renderCommandBuffer;
	static readonly int PlanetShellBackbufferId = Shader.PropertyToID ("_PlanetShellBackbuffer");

	readonly List<EffectHolder> effectHolders = new List<EffectHolder> ();

	void Awake () {
		cam = GetComponent<Camera> ();
	}

	void OnEnable () {
		cam = cam ? cam : GetComponent<Camera> ();
		cam.depthTextureMode |= DepthTextureMode.Depth;

		EnsureResources ();
		EnsureCommandBuffer ();
		lastRegistryVersion = -1; // force rebuild
	}

	void OnDisable () {
		DisposeCommandBuffer ();
		effectHolders.Clear ();
		DisposeResources ();
	}

	void Update () {
		if (!cam) {
			return;
		}

		if (PlanetEnvironmentRegistry.Version != lastRegistryVersion) {
			RebuildEffectHolders ();
		} else if (NeedsStateRefresh ()) {
			RebuildEffectHolders ();
		}

		UpdateSunReference ();
		UpdateDynamicProperties ();
		SortIfNeeded ();
		BuildCommandBuffer ();
	}

	void EnsureResources () {
		if (!shellMesh) {
			shellMesh = BuildSphereMesh (shellResolution);
		}

		if (!oceanShellShader) {
			oceanShellShader = Shader.Find ("Celestial/OceanShell");
		}
		if (!atmosphereShellShader) {
			atmosphereShellShader = Shader.Find ("Celestial/AtmosphereShell");
		}

		if (oceanShellShader && !oceanMaterial) {
			oceanMaterial = new Material (oceanShellShader) {
				hideFlags = HideFlags.HideAndDontSave
			};
		}

		if (atmosphereShellShader && !atmosphereMaterial) {
			atmosphereMaterial = new Material (atmosphereShellShader) {
				hideFlags = HideFlags.HideAndDontSave
			};
		}
	}

	void DisposeResources () {
		if (shellMesh) {
			DestroyImmediate (shellMesh);
			shellMesh = null;
		}

		if (oceanMaterial) {
			DestroyImmediate (oceanMaterial);
			oceanMaterial = null;
		}

		if (atmosphereMaterial) {
			DestroyImmediate (atmosphereMaterial);
			atmosphereMaterial = null;
		}
	}

	void EnsureCommandBuffer () {
		if (renderCommandBuffer != null || !cam) {
			return;
		}
		renderCommandBuffer = new CommandBuffer {
			name = "Planet Shell Renderer"
		};
		cam.AddCommandBuffer (CameraEvent.BeforeImageEffects, renderCommandBuffer);
	}

	void DisposeCommandBuffer () {
		if (renderCommandBuffer != null && cam) {
			cam.RemoveCommandBuffer (CameraEvent.BeforeImageEffects, renderCommandBuffer);
		}
		if (renderCommandBuffer != null) {
			renderCommandBuffer.Release ();
			renderCommandBuffer = null;
		}
	}

	void RebuildEffectHolders () {
		lastRegistryVersion = PlanetEnvironmentRegistry.Version;
		effectHolders.Clear ();

		var bodies = PlanetEnvironmentRegistry.Bodies;
		for (int i = 0; i < bodies.Count; i++) {
			var generator = bodies[i];
			if (!generator || !generator.body || !generator.body.shading) {
				continue;
			}

			var shading = generator.body.shading;
			var holder = new EffectHolder (generator);

			if (shading.hasOcean && shading.oceanSettings) {
				holder.oceanSettings = shading.oceanSettings;
				holder.randomize = shading.randomize;
				holder.seed = shading.seed;
				holder.oceanBlock = CreateOceanBlock (generator, shading.oceanSettings, shading.randomize, shading.seed);
			}

			if (shading.hasAtmosphere && shading.atmosphereSettings) {
				holder.atmosphereSettings = shading.atmosphereSettings;
				holder.atmosphereBlock = CreateAtmosphereBlock (generator, shading.atmosphereSettings);
			}

			if (holder.HasShell) {
				effectHolders.Add (holder);
			}
		}

		lastSortPosition = Vector3.one * float.MaxValue;
	}

	bool NeedsStateRefresh () {
		for (int i = 0; i < effectHolders.Count; i++) {
			var holder = effectHolders[i];
			var shading = holder.generator.body?.shading;
			if (!shading) {
				return true;
			}
			bool wantsOcean = shading.hasOcean && shading.oceanSettings;
			bool hasOcean = holder.oceanBlock != null;
			if (wantsOcean != hasOcean) {
				return true;
			}

			bool wantsAtmosphere = shading.hasAtmosphere && shading.atmosphereSettings;
			bool hasAtmosphere = holder.atmosphereBlock != null;
			if (wantsAtmosphere != hasAtmosphere) {
				return true;
			}
		}
		return false;
	}

	void UpdateSunReference () {
		if (sunLight && sunLight.isActiveAndEnabled) {
			return;
		}

		var caster = GameObject.FindFirstObjectByType<SunShadowCaster> ();
		if (caster) {
			sunLight = caster.GetComponent<Light> ();
		}

		if (!sunLight) {
			sunLight = FindAnyObjectByType<Light> ();
		}
	}

	void UpdateDynamicProperties () {
		if (effectHolders.Count == 0) {
			return;
		}

		Vector3 sunDir = Vector3.up;
		if (sunLight) {
			if (sunLight.type == LightType.Directional) {
				sunDir = -sunLight.transform.forward;
			}
		}

		for (int i = 0; i < effectHolders.Count; i++) {
			var holder = effectHolders[i];
			Vector3 centre = holder.generator.transform.position;

			if (holder.oceanBlock != null && holder.oceanSettings) {
				float radius = holder.generator.GetOceanRadius ();
				holder.oceanVisible = radius > 0.0001f;
				if (holder.oceanVisible) {
					holder.oceanMatrix = Matrix4x4.TRS (centre, Quaternion.identity, Vector3.one * radius);
					holder.oceanBlock.SetVector ("oceanCentre", centre);
					holder.oceanBlock.SetFloat ("oceanRadius", radius);

					float time = Time.time * holder.oceanSettings.waveSpeed;
					holder.oceanBlock.SetVector ("waveOffsetA", new Vector2 (time, time * 0.8f));
					holder.oceanBlock.SetVector ("waveOffsetB", new Vector2 (time * -0.8f, time * -0.3f));

					Vector3 dir = sunDir;
					if (sunLight && sunLight.type != LightType.Directional) {
						dir = -(centre - sunLight.transform.position).normalized;
					}
					holder.oceanBlock.SetVector ("dirToSun", dir);
					holder.oceanBlock.SetFloat ("planetScale", holder.generator.BodyScale);
				} else {
					holder.oceanVisible = false;
				}
			}

			if (holder.atmosphereBlock != null && holder.atmosphereSettings) {
				float planetRadius = holder.generator.BodyScale;
				float oceanRadius = holder.generator.GetOceanRadius ();
				float atmosphereRadius = holder.atmosphereSettings.GetAtmosphereRadius (planetRadius);

				holder.atmosphereVisible = atmosphereRadius > 0.0001f;
				if (holder.atmosphereVisible) {
					holder.atmosphereMatrix = Matrix4x4.TRS (centre, Quaternion.identity, Vector3.one * atmosphereRadius);

					holder.atmosphereBlock.SetVector ("planetCentre", centre);
					holder.atmosphereBlock.SetFloat ("planetRadius", planetRadius);
					holder.atmosphereBlock.SetFloat ("oceanRadius", oceanRadius);
					holder.atmosphereBlock.SetFloat ("atmosphereRadius", atmosphereRadius);
					holder.atmosphereBlock.SetVector ("backgroundColor", cam ? (Vector4) cam.backgroundColor : Color.black);

					Vector3 dirToSun;
					if (sunLight) {
						if (sunLight.type == LightType.Directional) {
							dirToSun = -sunLight.transform.forward;
						} else {
							dirToSun = (sunLight.transform.position - centre).normalized;
						}
					} else {
						dirToSun = Vector3.up;
					}
					holder.atmosphereBlock.SetVector ("dirToSun", dirToSun);
				} else {
					holder.atmosphereVisible = false;
				}
			}
		}
	}

	void SortIfNeeded () {
		if (!cam) {
			return;
		}
		Vector3 camPos = cam.transform.position;
		if ((camPos - lastSortPosition).sqrMagnitude < sortDistanceThreshold * sortDistanceThreshold) {
			return;
		}

		effectHolders.Sort ((a, b) => {
			float dstA = a.DistanceFromSurface (camPos);
			float dstB = b.DistanceFromSurface (camPos);
			return dstB.CompareTo (dstA); // far to near
		});
		lastSortPosition = camPos;
	}

	void BuildCommandBuffer () {
		if (!cam) {
			return;
		}

		EnsureResources ();
		EnsureCommandBuffer ();

		if (renderCommandBuffer == null) {
			return;
		}

		renderCommandBuffer.Clear ();

		if (effectHolders.Count == 0 || shellMesh == null || oceanMaterial == null || atmosphereMaterial == null) {
			renderCommandBuffer.SetGlobalTexture (PlanetShellBackbufferId, Texture2D.blackTexture);
			return;
		}

		bool needsBackbuffer = false;
		for (int i = 0; i < effectHolders.Count; i++) {
			var holder = effectHolders[i];
			if (holder.atmosphereBlock != null && holder.atmosphereVisible) {
				needsBackbuffer = true;
				break;
			}
		}

		if (needsBackbuffer) {
			var descriptor = CreateBackbufferDescriptor (Mathf.Max (1, cam.pixelWidth), Mathf.Max (1, cam.pixelHeight));
			renderCommandBuffer.GetTemporaryRT (PlanetShellBackbufferId, descriptor, FilterMode.Bilinear);
			CopyCameraToBackbuffer ();
		} else {
			renderCommandBuffer.SetGlobalTexture (PlanetShellBackbufferId, Texture2D.blackTexture);
		}

		for (int i = 0; i < effectHolders.Count; i++) {
			var holder = effectHolders[i];

			if (holder.oceanBlock != null && holder.oceanVisible) {
				renderCommandBuffer.DrawMesh (shellMesh, holder.oceanMatrix, oceanMaterial, 0, 0, holder.oceanBlock);
			}

			if (holder.atmosphereBlock != null && holder.atmosphereVisible) {
				renderCommandBuffer.DrawMesh (shellMesh, holder.atmosphereMatrix, atmosphereMaterial, 0, 0, holder.atmosphereBlock);
				if (needsBackbuffer) {
					CopyCameraToBackbuffer ();
				}
			}
		}

		if (needsBackbuffer) {
			renderCommandBuffer.ReleaseTemporaryRT (PlanetShellBackbufferId);
		}
	}

	RenderTextureDescriptor CreateBackbufferDescriptor (int width, int height) {
		RenderTextureDescriptor descriptor;

		if (cam && cam.targetTexture) {
			descriptor = cam.targetTexture.descriptor;
			descriptor.width = width;
			descriptor.height = height;
		}
#if UNITY_2019_1_OR_NEWER
		else if (XRSettings.enabled && cam && cam.stereoTargetEye != StereoTargetEyeMask.None) {
			descriptor = XRSettings.eyeTextureDesc;
			descriptor.width = width;
			descriptor.height = height;
		}
#endif
		else {
			RenderTextureFormat format = cam && cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
			descriptor = new RenderTextureDescriptor (width, height, format, 0) {
				vrUsage = VRTextureUsage.None,
				dimension = TextureDimension.Tex2D,
				volumeDepth = 1
			};
		}

		descriptor.msaaSamples = 1;
		descriptor.depthBufferBits = 0;
		descriptor.useMipMap = false;
		descriptor.autoGenerateMips = false;
		descriptor.enableRandomWrite = false;
#if UNITY_2019_1_OR_NEWER
		if (!XRSettings.enabled) {
			descriptor.vrUsage = VRTextureUsage.None;
			descriptor.dimension = TextureDimension.Tex2D;
			descriptor.volumeDepth = 1;
		}
#endif
		descriptor.sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;
		return descriptor;
	}

	void CopyCameraToBackbuffer () {
		renderCommandBuffer.Blit (BuiltinRenderTextureType.CameraTarget, PlanetShellBackbufferId);
		renderCommandBuffer.SetGlobalTexture (PlanetShellBackbufferId, PlanetShellBackbufferId);
	}

	MaterialPropertyBlock CreateOceanBlock (CelestialBodyGenerator generator, OceanSettings settings, bool randomize, int seed) {
		var block = new MaterialPropertyBlock ();
		block.SetFloat ("depthMultiplier", settings.depthMultiplier);
		block.SetFloat ("alphaMultiplier", settings.alphaMultiplier);
		block.SetFloat ("smoothness", settings.smoothness);
		block.SetFloat ("waveStrength", settings.waveStrength);
		block.SetFloat ("waveNormalScale", settings.waveScale);
		block.SetFloat ("waveNormalScaleScaled", settings.waveScale / Mathf.Max (0.0001f, generator.BodyScale));
		block.SetFloat ("waveSpeed", settings.waveSpeed);
		block.SetVector ("params", settings.testParams);

		block.SetTexture ("waveNormalA", settings.waveNormalA);
		block.SetTexture ("waveNormalB", settings.waveNormalB);

		if (randomize) {
			var random = new PRNG (seed);
			var randomColA = Color.HSVToRGB (random.Value (), random.Range (0.6f, 0.8f), random.Range (0.65f, 1));
			var randomColB = ColourHelper.TweakHSV (randomColA,
				random.SignedValue () * 0.2f,
				random.SignedValue () * 0.2f,
				random.Range (-0.5f, -0.4f)
			);

			block.SetColor ("colA", randomColA);
			block.SetColor ("colB", randomColB);
			block.SetColor ("specularCol", Color.white);
		} else {
			block.SetColor ("colA", settings.colA);
			block.SetColor ("colB", settings.colB);
			block.SetColor ("specularCol", settings.specularCol);
		}

		return block;
	}

	MaterialPropertyBlock CreateAtmosphereBlock (CelestialBodyGenerator generator, AtmosphereSettings settings) {
		var block = new MaterialPropertyBlock ();
		settings.ApplyTo (block, generator.BodyScale);
		return block;
	}

	Mesh BuildSphereMesh (int resolution) {
		var sphere = new SphereMesh (resolution);
		var mesh = new Mesh {
			name = $"PlanetShell_{resolution}"
		};
		mesh.vertices = sphere.Vertices;
		mesh.triangles = sphere.Triangles;
		mesh.RecalculateNormals ();
		mesh.RecalculateBounds ();
		mesh.UploadMeshData (false);
		return mesh;
	}

	class EffectHolder {
		public readonly CelestialBodyGenerator generator;
		public MaterialPropertyBlock oceanBlock;
		public MaterialPropertyBlock atmosphereBlock;
		public Matrix4x4 oceanMatrix;
		public Matrix4x4 atmosphereMatrix;
		public OceanSettings oceanSettings;
		public AtmosphereSettings atmosphereSettings;
		public bool randomize;
		public int seed;
		public bool oceanVisible;
		public bool atmosphereVisible;

		public EffectHolder (CelestialBodyGenerator generator) {
			this.generator = generator;
		}

		public bool HasShell => oceanBlock != null || atmosphereBlock != null;

		public float DistanceFromSurface (Vector3 viewPosition) {
			return Mathf.Max (0, (generator.transform.position - viewPosition).magnitude - generator.BodyScale);
		}
	}
}
