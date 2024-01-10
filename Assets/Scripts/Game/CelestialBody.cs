using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent (typeof (Rigidbody))]
public class CelestialBody : GravityObject {

    public enum BodyType { Planet, Moon, Sun }
    public BodyType bodyType;
    public float radius;
    public float surfaceGravity;
    public Vector3 initialVelocity;
    public float rotationPerSecond = 90;
    public string bodyName = "Unnamed";
    Transform meshHolder;

    public Vector3 velocity { get; private set;}
    public float mass { get; private set; }
    Rigidbody rb;

    void Awake () {

        rb = GetComponent<Rigidbody> ();
        // sadly: i tried doing Kinematic = false, and using rigidbody physics (addforce instead of moveposition)
        // but unity say's that the mesh has to be convex to detect collisions, and these are not...
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        velocity = initialVelocity;
        rb.AddForce (initialVelocity, ForceMode.VelocityChange);
        RecalculateMass ();
    }

    void Update () {
        if (Application.isPlaying) {
            RotatePlanet ();
        }
    }

    public void UpdateVelocity (Vector3 acceleration, float timeStep) {
        velocity += acceleration * timeStep;
        rb.AddForce (acceleration, ForceMode.Acceleration);
    }

    public void UpdatePosition (float timeStep) {
        rb.MovePosition (rb.position + velocity * timeStep);
    }

    void OnValidate () {
        RecalculateMass ();
        if (GetComponentInChildren<CelestialBodyGenerator> ()) {
            GetComponentInChildren<CelestialBodyGenerator> ().transform.localScale = Vector3.one * radius;
        }
        gameObject.name = bodyName;
    }

    public void RecalculateMass () {
        mass = surfaceGravity * radius * radius / Universe.gravitationalConstant;
        Rigidbody.mass = mass;
    }

    public void UpdateOrigin (Vector3 originOffset) {
        rb.position -= originOffset;
    }

    public void RotatePlanet() {
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0, rotationPerSecond * Time.deltaTime, 0));
    }

    public Rigidbody Rigidbody {
        get {
            if (!rb) {
                rb = GetComponent<Rigidbody> ();
            }
            return rb;
        }
    }

    public Vector3 Position {
        get {
            return rb.position;
        }
    }

}