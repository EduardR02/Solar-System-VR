using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionShape : MonoBehaviour
{
    private Rigidbody parentRigidbody;
    private Quaternion previousParentRotation;
    private Rigidbody rb;
    private CelestialBody parentPlanet;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.velocity = parentRigidbody.velocity;
        previousParentRotation = parentRigidbody.rotation;
    }

    void FixedUpdate()
    {
        Vector3 parentMovement = parentPlanet.velocity * Time.fixedDeltaTime;
        Quaternion parentRotation = parentRigidbody.rotation;
        Quaternion deltaRotation = Quaternion.Inverse(previousParentRotation) * parentRotation;
        Vector3 projectedRelativePosition = rb.position + parentMovement - parentRigidbody.position;
        Vector3 addedRotationMovement = deltaRotation * projectedRelativePosition - projectedRelativePosition;
        rb.MovePosition(parentMovement + addedRotationMovement + rb.position);
        previousParentRotation = parentRotation;
    }

    public void UpdateOrigin(Vector3 originOffset) {
        Rigidbody.position -= originOffset;
    }

    public void SetParentPlanet(CelestialBody parentPlanet) {
        parentRigidbody = parentPlanet.Rigidbody;
        this.parentPlanet = parentPlanet;
    }

    public Rigidbody Rigidbody {
        get {
            if (!rb) {
                rb = GetComponent<Rigidbody> ();
            }
            return rb;
        }
    }
}
