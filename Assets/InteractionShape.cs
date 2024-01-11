using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionShape : MonoBehaviour
{
    private Rigidbody rb;
    private CelestialBody parentPlanet;
    private Rigidbody parentRigidbody;
    Vector3 prevRotationalVelocity = Vector3.zero;
    Vector3 prevParentVelocity = Vector3.zero;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        parentRigidbody = parentPlanet.Rigidbody;
    }

    void FixedUpdate()
    {
        UpdatePosition();
        RotateWithPlanet();
    }

    void UpdatePosition() {
        Vector3 projectedRelativePosition = rb.position - parentRigidbody.position;
        Vector3 v = Vector3.Cross(parentRigidbody.angularVelocity, projectedRelativePosition);
        // only thing that works rn is move position... , addForce just spirals out of control
        rb.AddForce(parentPlanet.velocity - prevParentVelocity + v - prevRotationalVelocity, ForceMode.VelocityChange);
        prevRotationalVelocity = v;
        prevParentVelocity = parentPlanet.velocity;
    }

    void RotateWithPlanet() {
        // rotate the object so it stays relative to the planet
        rb.MoveRotation(Quaternion.AngleAxis(parentPlanet.rotationPerSecondDeg * Time.deltaTime, parentPlanet.transform.up) * rb.rotation);
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
