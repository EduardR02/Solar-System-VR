using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinRotator : MonoBehaviour
{
    public float degPerSec = 90f;

    void Start()
    {
        
    }

    void Update()
    {
        // rotate self around y axis accounting for time
        transform.Rotate(0, degPerSec * Time.deltaTime, 0, Space.Self);
    }
}
