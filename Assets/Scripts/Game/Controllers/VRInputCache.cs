using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches OVRInput calls for one frame to avoid expensive native calls
/// </summary>
public class VRInputCache : MonoBehaviour {
	private static VRInputCache instance;
	public static VRInputCache Instance {
		get {
			if (instance == null) {
				GameObject go = new GameObject("VRInputCache");
				instance = go.AddComponent<VRInputCache>();
				DontDestroyOnLoad(go);
			}
			return instance;
		}
	}

	// Cache for Axis1D values
	private Dictionary<(OVRInput.Axis1D, OVRInput.Controller), float> axis1DCache = new Dictionary<(OVRInput.Axis1D, OVRInput.Controller), float>();
	// Cache for button GetDown values
	private Dictionary<OVRInput.Button, bool> buttonDownCache = new Dictionary<OVRInput.Button, bool>();
	private int lastCacheFrame = -1;

	void LateUpdate() {
		// Clear cache at end of frame so next frame gets fresh values
		axis1DCache.Clear();
		buttonDownCache.Clear();
		lastCacheFrame = Time.frameCount;
	}

	public static float GetAxis1D(OVRInput.Axis1D axis, OVRInput.Controller controller) {
		VRInputCache cache = Instance;

		// If we're in a new frame, clear the old cache
		if (Time.frameCount != cache.lastCacheFrame) {
			cache.axis1DCache.Clear();
			cache.buttonDownCache.Clear();
			cache.lastCacheFrame = Time.frameCount;
		}

		var key = (axis, controller);
		if (!cache.axis1DCache.TryGetValue(key, out float value)) {
			// Cache miss - read from OVRInput and cache it
			value = OVRInput.Get(axis, controller);
			cache.axis1DCache[key] = value;
		}

		return value;
	}

	public static bool GetButtonDown(OVRInput.Button button) {
		VRInputCache cache = Instance;

		// If we're in a new frame, clear the old cache
		if (Time.frameCount != cache.lastCacheFrame) {
			cache.axis1DCache.Clear();
			cache.buttonDownCache.Clear();
			cache.lastCacheFrame = Time.frameCount;
		}

		if (!cache.buttonDownCache.TryGetValue(button, out bool value)) {
			// Cache miss - read from OVRInput and cache it
			value = OVRInput.GetDown(button);
			cache.buttonDownCache[button] = value;
		}

		return value;
	}
}
