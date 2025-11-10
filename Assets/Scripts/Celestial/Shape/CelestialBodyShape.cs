using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CelestialBodyShape : ScriptableObject {

	public bool randomize;
	public int seed;
	public ComputeShader heightMapCompute;

	public bool perturbVertices;
	public ComputeShader perturbCompute;
	[Range (0, 1)]
	public float perturbStrength = 0.7f;

	public event System.Action OnSettingChanged;

	ComputeBuffer heightBuffer;
	int heightBufferCapacity;

	public virtual float[] CalculateHeights (ComputeBuffer vertexBuffer, int vertexCount) {
		//Debug.Log (System.Environment.StackTrace);
		// Set data
		SetShapeData ();
		heightMapCompute.SetInt ("numVertices", vertexCount);
		heightMapCompute.SetBuffer (0, "vertices", vertexBuffer);
		EnsureHeightBufferCapacity (vertexCount);
		heightMapCompute.SetBuffer (0, "heights", heightBuffer);

		// Run
		ComputeHelper.Run (heightMapCompute, vertexCount);

		// Get heights
		var heights = new float[vertexCount];
		heightBuffer.GetData (heights, 0, 0, vertexCount);
		return heights;
	}

	void EnsureHeightBufferCapacity (int required) {
		if (heightBuffer != null && heightBufferCapacity >= required) {
			return;
		}
		ComputeHelper.Release (heightBuffer);
		heightBuffer = new ComputeBuffer (required, System.Runtime.InteropServices.Marshal.SizeOf (typeof (float)));
		heightBufferCapacity = required;
	}

	public virtual void ReleaseBuffers () {
		ComputeHelper.Release (heightBuffer);
		heightBufferCapacity = 0;
	}

	protected virtual void SetShapeData () {

	}

	protected virtual void OnValidate () {
		if (OnSettingChanged != null) {
			OnSettingChanged ();
		}
	}

}
