using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class Ship : GravityObject {

	[Header ("Handling")]
	public float maxThrust = 50;
	[Range (0, 1)]
	public float groundingForce = 0.1f;
	[Range (0, 1)]
	public float groundingForceThresholdVelocity = 0.25f;
	[Header ("Rotation")]
	public float rotationSpeed = 1f;
	[Tooltip ("Furthest distance from the planet where the player's feet will face the planet")]
	public float rotationChangeOrientationDst = 200f;
	[Tooltip ("Furthest distance from the planet where the player will rotate to face the planet")]
	public float maxRotationDistance = 2000f;

	[Header ("Interact")]
	public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    public Transform leftControllerTransform;
    public Transform rightControllerTransform;

	Rigidbody rb;
	CelestialBody referenceBody;
	Camera cam;
	private bool updateTargetRotation = true;
	Quaternion lockedTargetRotation;
	PlayerPathVis playerPathVis;
	ParticleController[] ExhaustParticleSystems;
	bool isColliding = false;

	void Awake () {
		InitRigidbody ();
		cam = GetComponentInChildren<Camera> ();
		playerPathVis = GetComponentInChildren<PlayerPathVis> ();
		ExhaustParticleSystems = GetComponentsInChildren<ParticleController> ();
	}

	void ThrusterMovement() {
		float leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, leftController);
        float rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, rightController);
		Vector3 forceFraction = -leftTriggerValue * leftControllerTransform.forward.normalized - rightTriggerValue * rightControllerTransform.forward.normalized;
        rb.AddForce(forceFraction * maxThrust, ForceMode.Acceleration);
	}

	void FixedUpdate () {
		GravityForce();
		GroundingForce();
		ThrusterMovement();
	}

	void GroundingForce() {
		if (IsGrounded ()) {
			rb.AddForce (-transform.up * groundingForce, ForceMode.Acceleration);
		}
	
	}

	void GravityForceNoRefBody() {
		Vector3 gravity = NBodySimulation.CalculateAcceleration (rb.position);
		rb.AddForce (gravity, ForceMode.Acceleration);
	}

	void GravityForce() {
		CelestialBody[] bodies = NBodySimulation.Bodies;
		Vector3 gravityOfNearestBody = Vector3.zero;
		Vector3 cumulativeAcceleration = Vector3.zero;
		float nearestSurfaceDst = float.MaxValue;
		CelestialBody closestBody = referenceBody;

		// Gravity
		foreach (CelestialBody body in bodies) {
			float sqrDistance = (body.Position - rb.position).sqrMagnitude;
			Vector3 forceDir = (body.Position - rb.position).normalized;
			Vector3 acceleration = forceDir * Universe.gravitationalConstant * body.mass / sqrDistance;
			cumulativeAcceleration += acceleration;
			float dstToSurface = Mathf.Sqrt(sqrDistance) - body.radius;
			// Find closest planet
			if (dstToSurface < nearestSurfaceDst) {
				nearestSurfaceDst = dstToSurface;
				gravityOfNearestBody = acceleration;
				closestBody = body;
			}
		}
		if (closestBody != referenceBody) {
			referenceBody = closestBody;
			if (referenceBody.bodyType != CelestialBody.BodyType.Moon) {
				// rotate don't "rerotate" to face moons, too weird/confusing
				updateTargetRotation = true;
			}
		}
		rb.AddForce (cumulativeAcceleration, ForceMode.Acceleration);
		SmoothRotation (-gravityOfNearestBody.normalized, nearestSurfaceDst);
	}

	bool IsGrounded() {
		if (referenceBody) {
			var relativeVelocity = rb.velocity - referenceBody.velocity;
			return relativeVelocity.y <= (maxThrust * 2 * groundingForceThresholdVelocity);
		}
		return false;
	}

	void SmoothRotation(Vector3 gravityUp, float dstToSurface) {
		if (dstToSurface < rotationChangeOrientationDst) {
			// Smoothly rotate to align with gravity up (player feet are "down" so he can "stand")
			Quaternion targetRotation = Quaternion.FromToRotation (rb.transform.up, gravityUp) * rb.rotation;
			rb.rotation = Quaternion.Slerp (rb.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			updateTargetRotation = true;
		}
		else if (dstToSurface < maxRotationDistance){
			// player is rotated to face the planet (you want to be looking forward when flying around, not down)
			if (updateTargetRotation) {
				// so it doesn't "follow" the planet after the initial rotation
				lockedTargetRotation = Quaternion.FromToRotation (cam.transform.forward, -gravityUp) * rb.rotation;
				updateTargetRotation = false;
			}
			rb.rotation = Quaternion.Slerp (rb.rotation,  lockedTargetRotation, rotationSpeed * Time.deltaTime);
		}
		else {
			updateTargetRotation = true;
		}
	}

	void TeleportToBody (CelestialBody body) {
		rb.velocity = body.velocity;
		rb.MovePosition (body.transform.position + (transform.position - body.transform.position).normalized * body.radius * 2);
	}

	public void UpdateOrigin (Vector3 originOffset) {
		for (int i = 0; i < ExhaustParticleSystems.Length; i++) {
			ExhaustParticleSystems[i].UpdateOrigin(originOffset);
		}
		rb.position -= originOffset;
		playerPathVis.OriginShift (originOffset);
		
    }

	void InitRigidbody () {
		rb = GetComponentInParent<Rigidbody> ();
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.useGravity = false;
		rb.isKinematic = false;
		rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
	}

	public void SetVelocity (Vector3 velocity) {
		rb.velocity = velocity;
	}

	void OnCollisionEnter (Collision collision) {
		isColliding = true;
	}

	void OnCollisionExit (Collision collision) {
		isColliding = false;
	}

	public bool ShowHUD {
		get {
			// variable was called "ship is piloted", hud is turned off anyway so...
			return true;
		}
	}
	public bool HatchOpen {
		get {
			return false;
		}
	}

	public Rigidbody Rigidbody {
		get {
			return rb;
		}
	}

	public CelestialBody ReferenceBody {
		get {
			return referenceBody;
		}
	}

	public void SetReferenceBody (CelestialBody body) {
		referenceBody = body;
	}

	public bool IsColliding() {
		return isColliding;
	}

}