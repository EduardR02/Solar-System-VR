using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessManager : MonoBehaviour {

    public float distanceThreshold = 1000;
    List<Rigidbody> physicsObjects;
    Ship ship;
    Camera playerCamera;
    PlayerPathVis pathVis;

    public event System.Action PostFloatingOriginUpdate;

    void Awake () {
        ship = FindObjectOfType<Ship> ();
        var bodies = FindObjectsOfType<CelestialBody> ();
        pathVis = FindObjectOfType<PlayerPathVis> ();

        physicsObjects = new List<Rigidbody>
        {
            ship.Rigidbody
        };
        foreach (var c in bodies) {
            physicsObjects.Add (c.Rigidbody);
        }
        playerCamera = Camera.main;
    }

    void LateUpdate () {
        UpdateFloatingOrigin ();
        if (PostFloatingOriginUpdate != null) {
            PostFloatingOriginUpdate ();
        }
    }

    void UpdateFloatingOrigin () {
        Vector3 originOffset = playerCamera.transform.position;
        float dstFromOrigin = originOffset.magnitude;
        if (dstFromOrigin > distanceThreshold) {
            pathVis.OriginShift (originOffset);
            foreach (Rigidbody rb in physicsObjects) {
                // rb.position combined with lateupdate works, don't put .MovePosition as you don't want interpolation
                // wihch would create bad feedback loop. If you use fixedupdate there is a small "shift" because the interpolation gets "thrown off"
                // so this works best by far!
                rb.position -= originOffset;
            }
        }
    }

}