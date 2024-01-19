using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public float degPerSec = 90f;

    void Update()
    {
        // rotate self around y axis accounting for time
        transform.Rotate(0, degPerSec * Time.deltaTime, 0, Space.Self);
    }

    void OnTriggerEnter(Collider other) {
        // layer 9 is Ship, which includes the player hitbox and the booster exhaust particles
        if (other.gameObject.layer == 9) {
            ParkourManager.IncrementCoinCount();
            Destroy(gameObject);
        }
    }
}
