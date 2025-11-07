using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessManager : MonoBehaviour {

    public float distanceThreshold = 1000;
    List<CelestialBody> physicsObjects;
    Ship ship;
    Camera playerCamera;
    StarTest starTest;
    ParkourManager parkourManager;
    public event System.Action PostFloatingOriginUpdate;

    void Awake () {
        ship = FindFirstObjectByType<Ship> ();
        var bodies = FindObjectsByType<CelestialBody> (FindObjectsSortMode.None);
        parkourManager = FindFirstObjectByType<ParkourManager> ();
        physicsObjects = new List<CelestialBody>(bodies);
        playerCamera = Camera.main;
        starTest = FindFirstObjectByType<StarTest> ();
    }

    // used lateupdate before, but it was causing lag with transform.position updates, for example in the particle system
    // instead i use fixedupdate, and placed the scripting order as the first one...
    void FixedUpdate () {
        UpdateFloatingOrigin ();
        if (PostFloatingOriginUpdate != null) {
            PostFloatingOriginUpdate ();
        }
    }

    void UpdateFloatingOrigin () {
        Vector3 originOffset = playerCamera.transform.position;
        float dstFromOrigin = originOffset.magnitude;
        if (dstFromOrigin > distanceThreshold) {
            // rb.position combined with lateupdate works, don't put .MovePosition as you don't want interpolation
            // wihch would create bad feedback loop. If you use LateUpdate there is a small "shift" because the interpolation gets "thrown off"
            // so this works best by far!
            ship.UpdateOrigin(originOffset);
            starTest.UpdateOrigin(originOffset);
            parkourManager.UpdateOrigin(originOffset);
            foreach (CelestialBody cb in physicsObjects) {
                cb.UpdateOrigin(originOffset);
            }
        }
    }

}