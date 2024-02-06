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
    private bool locked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        parentRigidbody = parentPlanet.Rigidbody;
        UpdatePosition();
        RotateWithPlanet();
    }

    void FixedUpdate()
    {
        if (locked) {
            return;
        }
        UpdatePosition();
        RotateWithPlanet();
    }

    void UpdatePosition() {
        Vector3 projectedRelativePosition = rb.position - parentRigidbody.position;
        Vector3 v = Vector3.Cross(parentRigidbody.angularVelocity, projectedRelativePosition);
        rb.AddForce(parentPlanet.velocity - prevParentVelocity + v - prevRotationalVelocity, ForceMode.VelocityChange);
        prevRotationalVelocity = v;
        prevParentVelocity = parentPlanet.velocity;
    }

    void RotateWithPlanet() {
        // rotate the object so it stays relative to the planet
        rb.MoveRotation(Quaternion.AngleAxis(parentPlanet.rotationPerSecondDeg * Time.deltaTime, parentPlanet.transform.up) * rb.rotation);
    }

    public void LockChallenge() {
        // remove the rigidbody and parent it to the planet
        Destroy(rb);
        gameObject.name = "Completed " + gameObject.name;
        transform.SetParent(parentPlanet.transform);
        locked = true;
    }

    public void UpdateOrigin(Vector3 originOffset) {
        if (locked) {
            return;
        }
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
