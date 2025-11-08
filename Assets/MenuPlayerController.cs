using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuPlayerController : MonoBehaviour
{
    public float maxThrust = 50;

    [Header ("Interact")]
	public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    public Transform leftControllerTransform;
    public Transform rightControllerTransform;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        ThrusterMovement();
    }

    void ThrusterMovement() {
		// Use cached input to avoid expensive native calls
		float leftTriggerValue = VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryIndexTrigger, leftController);
        float rightTriggerValue = VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryIndexTrigger, rightController);
		Vector3 forceFraction = -leftTriggerValue * leftControllerTransform.forward.normalized - rightTriggerValue * rightControllerTransform.forward.normalized;
        rb.AddForce(forceFraction * maxThrust, ForceMode.Acceleration);
	}
}
