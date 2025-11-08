using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView, RequireComponent(typeof(PlanetShellRenderer))]
public class CustomPostProcessing : MonoBehaviour {

	public PostProcessingEffect[] effects;
	List<RenderTexture> temporaryTextures = new List<RenderTexture> ();
	Camera cam;

	public event System.Action<RenderTexture> onPostProcessingComplete;
	public event System.Action<RenderTexture> onPostProcessingBegin;
	public static Matrix4x4[] _uvToEyeToWorld = new Matrix4x4[2];
	public static Vector4[] _eyePosition = new Vector4[2];
	public static int StereoDataVersion { get; private set; }

	// Cache for stereo matrix optimization
	Vector3 lastCameraPosition;
	Quaternion lastCameraRotation;

	// Persistent ping-pong textures for performance
	RenderTexture pingPongA;
	RenderTexture pingPongB;
	RenderTextureDescriptor lastDescriptor;

	void Init () {
		cam = Camera.main;
	}

	[ImageEffectOpaque]
	void OnRenderImage (RenderTexture intialSource, RenderTexture finalDestination) {
		if (onPostProcessingBegin != null) {
			onPostProcessingBegin (finalDestination);
		}
		Init ();

		temporaryTextures.Clear ();

		RenderTexture currentSource = intialSource;
		RenderTexture currentDestination = null;
		if (Application.isPlaying && cam.stereoEnabled) {
			// Only recalculate stereo matrices if camera transform changed
			bool cameraTransformChanged = cam.transform.position != lastCameraPosition ||
			                               cam.transform.rotation != lastCameraRotation;

			if (cameraTransformChanged) {
				CalculateStereoViewMatrix();
				lastCameraPosition = cam.transform.position;
				lastCameraRotation = cam.transform.rotation;
			}
		} else {
			// Ensure mono rendering path still has valid matrices for post effects
			CalculateMonoViewMatrix();
			lastCameraPosition = cam.transform.position;
			lastCameraRotation = cam.transform.rotation;
		}

		if (effects != null) {
			// Use persistent ping-pong textures for intermediate results
			RenderTextureDescriptor descriptor = finalDestination.descriptor;
			bool usePingPong = effects.Length > 1;

			for (int i = 0; i < effects.Length; i++) {
				PostProcessingEffect effect = effects[i];
				if (effect == null || effect is PlanetEffects) {
					continue;
				}

				if (i == effects.Length - 1) {
					// Final effect, so render into final destination texture
					currentDestination = finalDestination;
				} else if (usePingPong) {
					// Ping-pong between persistent textures
					bool useA = (i % 2 == 0);
					currentDestination = useA ? GetPingPongTexture(ref pingPongA, descriptor) : GetPingPongTexture(ref pingPongB, descriptor);
				} else {
					// Fallback to temporary texture for single effect
					currentDestination = TemporaryRenderTexture (finalDestination);
					temporaryTextures.Add (currentDestination);
				}

				effect.Render (currentSource, currentDestination); // render the effect
				currentSource = currentDestination; // output texture of this effect becomes input for next effect
			}
		}

		// In case dest texture was not rendered into (due to being provided a null effect), copy current src to dest
		if (currentDestination != finalDestination) {
			Graphics.Blit (currentSource, finalDestination);
		}

		// Release temporary textures
		for (int i = 0; i < temporaryTextures.Count; i++) {
			RenderTexture.ReleaseTemporary (temporaryTextures[i]);
		}

		// Trigger post processing complete event
		if (onPostProcessingComplete != null) {
			onPostProcessingComplete (finalDestination);
		}

	}

	// Helper function for blitting a list of materials
	public static void RenderMaterials (RenderTexture source, RenderTexture destination, List<Material> materials) {
		List<RenderTexture> temporaryTextures = new List<RenderTexture> ();

		RenderTexture currentSource = source;
		RenderTexture currentDestination = null;

		if (materials != null) {
			for (int i = 0; i < materials.Count; i++) {
				Material material = materials[i];
				if (material != null) {

					if (i == materials.Count - 1) { // last material
						currentDestination = destination;
					} else {
						// get temporary texture to render this effect into
						currentDestination = TemporaryRenderTexture (destination);
						temporaryTextures.Add (currentDestination);
					}
					Graphics.Blit (currentSource, currentDestination, material);
					currentSource = currentDestination;
				}
			}
		}

		// In case dest texture was not rendered into (due to being provided a null material), copy current src to dest
		if (currentDestination != destination) {
			// for some reason goes white screen if new Material (Shader.Find ("Unlit/Texture")) is used
			Graphics.Blit (currentSource, destination);
		}
		// Release temporary textures
		for (int i = 0; i < temporaryTextures.Count; i++) {
			RenderTexture.ReleaseTemporary (temporaryTextures[i]);
		}
	}

	void CalculateStereoViewMatrix() {
		Matrix4x4[] _eyeProjection = new Matrix4x4[2];
		Matrix4x4[] _eyeToWorld = new Matrix4x4[2];
		// stolen from https://github.com/sigtrapgames/VrTunnellingPro-Unity
        _eyeProjection[0] = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
        _eyeProjection[1] = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
        _eyeProjection[0] = GL.GetGPUProjectionMatrix(_eyeProjection[0], true).inverse;
        _eyeProjection[1] = GL.GetGPUProjectionMatrix(_eyeProjection[1], true).inverse;
        
		var api = SystemInfo.graphicsDeviceType;
			if (
				api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 &&
				api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
			){
				_eyeProjection[0][1, 1] *= -1f;
				_eyeProjection[1][1, 1] *= -1f;
			}
		_eyeToWorld[0] = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
		_eyeToWorld[1] = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
	
		// remove translational part of the matrix so that we don't have to do it every time in the shader
		for (int i = 0; i < 2; i++) {
			_eyeToWorld[i].m03 = 0;
        	_eyeToWorld[i].m13 = 0;
        	_eyeToWorld[i].m23 = 0;
		}
		// precompute the matrix, otherwise have to do it in every frag...
		_uvToEyeToWorld[0] = _eyeToWorld[0] * _eyeProjection[0];
		_uvToEyeToWorld[1] = _eyeToWorld[1] * _eyeProjection[1];

		_eyePosition[0] = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3);
		_eyePosition[1] = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3);
		StereoDataVersion++;
	}

	void CalculateMonoViewMatrix() {
		Matrix4x4 invProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true).inverse;

		var api = SystemInfo.graphicsDeviceType;
		if (
			api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 &&
			api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
		){
			invProj[1, 1] *= -1f;
		}

		Matrix4x4 eyeToWorld = cam.cameraToWorldMatrix;

		// Remove translation to match stereo calculation expectations
		Matrix4x4 eyeToWorldRotationOnly = eyeToWorld;
		eyeToWorldRotationOnly.m03 = 0;
		eyeToWorldRotationOnly.m13 = 0;
		eyeToWorldRotationOnly.m23 = 0;

		Matrix4x4 uvToEyeToWorld = eyeToWorldRotationOnly * invProj;
		Vector4 eyePos = eyeToWorld.GetColumn(3);

		// Populate both array slots so shaders using stereo indexing still work in mono
		for (int i = 0; i < 2; i++) {
			_uvToEyeToWorld[i] = uvToEyeToWorld;
			_eyePosition[i] = eyePos;
		}
		StereoDataVersion++;
	}

	public static RenderTexture TemporaryRenderTexture (RenderTexture template) {
		return RenderTexture.GetTemporary (template.descriptor);
	}

	// Get a ping-pong texture, reallocating only if resolution changed
	RenderTexture GetPingPongTexture(ref RenderTexture texture, RenderTextureDescriptor descriptor) {
		bool needsReallocation = texture == null ||
		                         !texture.IsCreated() ||
		                         texture.width != descriptor.width ||
		                         texture.height != descriptor.height ||
		                         texture.format != descriptor.colorFormat;

		if (needsReallocation) {
			if (texture != null) {
				texture.Release();
				DestroyImmediate(texture);
			}
			texture = new RenderTexture(descriptor);
			texture.Create();
		}

		return texture;
	}

	void OnDisable() {
		if (pingPongA != null) {
			pingPongA.Release();
			DestroyImmediate(pingPongA);
		}
		if (pingPongB != null) {
			pingPongB.Release();
			DestroyImmediate(pingPongB);
		}
	}

}
