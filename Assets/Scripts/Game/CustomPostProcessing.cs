using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CustomPostProcessing : MonoBehaviour {

	public PostProcessingEffect[] effects;
	Shader defaultShader;
	Material defaultMat;
	List<RenderTexture> temporaryTextures = new List<RenderTexture> ();
	Camera cam;
	public bool debugOceanMask;

	public event System.Action<RenderTexture> onPostProcessingComplete;
	public event System.Action<RenderTexture> onPostProcessingBegin;
	public static Matrix4x4[] _uvToEyeToWorld = new Matrix4x4[2];
	public static Vector4[] _eyePosition = new Vector4[2];

	void Init () {
		if (defaultShader == null) {
			defaultShader = Shader.Find ("Unlit/Texture");
		}
		defaultMat = new Material (defaultShader);
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
			CalculateStereoViewMatrix();
		}

		if (effects != null) {
			for (int i = 0; i < effects.Length; i++) {
				PostProcessingEffect effect = effects[i];
				if (effect != null) {
					if (i == effects.Length - 1) {
						// Final effect, so render into final destination texture
						currentDestination = finalDestination;
					} else {
						// Get temporary texture to render this effect into
						currentDestination = TemporaryRenderTexture (finalDestination);
						temporaryTextures.Add (currentDestination); //
					}

					effect.Render (currentSource, currentDestination); // render the effect
					currentSource = currentDestination; // output texture of this effect becomes input for next effect
				}
			}
		}

		// In case dest texture was not rendered into (due to being provided a null effect), copy current src to dest
		if (currentDestination != finalDestination) {
			Graphics.Blit (currentSource, finalDestination, defaultMat);
		}

		// Release temporary textures
		for (int i = 0; i < temporaryTextures.Count; i++) {
			RenderTexture.ReleaseTemporary (temporaryTextures[i]);
		}

		if (debugOceanMask) {
			Graphics.Blit (FindObjectOfType<OceanMaskRenderer> ().oceanMaskTexture, finalDestination, defaultMat);
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
			Graphics.Blit (currentSource, destination, new Material (Shader.Find ("Unlit/Texture")));
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
        
        #if (!UNITY_STANDALONE_OSX && !UNITY_ANDROID) || UNITY_EDITOR_WIN 
			var api = SystemInfo.graphicsDeviceType;
			if (
				api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 &&
				api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 &&
				api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore &&
				api != UnityEngine.Rendering.GraphicsDeviceType.Vulkan
			){
				_eyeProjection[0][1, 1] *= -1f;
				_eyeProjection[1][1, 1] *= -1f;
			}
		#endif
        
		/// material.SetMatrixArray(_propEyeProjection, _eyeProjection);

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
	}

	public static RenderTexture TemporaryRenderTexture (RenderTexture template) {
		return RenderTexture.GetTemporary (template.descriptor);
	}

}