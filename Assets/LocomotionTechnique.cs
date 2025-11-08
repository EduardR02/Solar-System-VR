using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocomotionTechnique : MonoBehaviour
{

    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    public Transform leftControllerTransform;  // Assign the left controller Transform
    public Transform rightControllerTransform; // Assign the right controller Transform
    private Vector3 startPos;
    float maxThrust = 10.0f;
    private Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // Use cached input to avoid expensive native calls
        float leftTriggerValue = VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryIndexTrigger, leftController);
        float rightTriggerValue = VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryIndexTrigger, rightController);

        rb.AddForce((-leftTriggerValue * leftControllerTransform.forward.normalized - rightTriggerValue * rightControllerTransform.forward.normalized) * maxThrust * Time.deltaTime, ForceMode.Impulse);
    }
}
