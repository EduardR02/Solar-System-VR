using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSetUp : MonoBehaviour {
	public enum StartCondition { InShip, OnBody }

	public StartCondition startCondition;
	public CelestialBody startBody;

	void Start () {
		Ship ship = FindFirstObjectByType<Ship> ();
		if (startCondition == StartCondition.InShip) {
			Debug.Log(ship.transform.position);
		} else if (startCondition == StartCondition.OnBody) {
			Debug.Log(startBody);
			if (startBody) {
				Vector3 pointAbovePlanet = startBody.transform.position + Vector3.up * startBody.radius * 1.5f;
				ship.Rigidbody.MovePosition(pointAbovePlanet + Vector3.up * 20);
				ship.SetVelocity(startBody.initialVelocity);
				ship.SetReferenceBody(startBody);
			}
		}
	}
}