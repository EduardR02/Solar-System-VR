using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionShape : MonoBehaviour
{
    private Quaternion previousParentRotation;
    private Rigidbody rb;
    private CelestialBody parentPlanet;
    private Rigidbody parentRigidbody;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        parentRigidbody = parentPlanet.Rigidbody;
        previousParentRotation = parentRigidbody.rotation;
        rb.rotation = Random.rotation;
    }

    void FixedUpdate()
    {
        UpdatePosition();
    }

    void UpdatePosition() {
        Vector3 parentMovement = parentPlanet.velocity * Time.fixedDeltaTime;
        Quaternion parentRotation = parentRigidbody.rotation;
        Quaternion deltaRotation = Quaternion.Inverse(previousParentRotation) * parentRotation;
        Vector3 projectedRelativePosition = rb.position + parentMovement - parentRigidbody.position;
        Vector3 addedRotationMovement = deltaRotation * projectedRelativePosition - projectedRelativePosition;
        // only thing that works rn is move position... , addForce just spirals out of control
        rb.MovePosition(parentMovement + addedRotationMovement + rb.position);
        previousParentRotation = parentRotation;
    }

    public void UpdateOrigin(Vector3 originOffset) {
        Rigidbody.position -= originOffset;
    }

    public void SetParentPlanet(CelestialBody parentPlanet) {
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
