using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SunTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        FindFirstObjectByType<Light>().transform.forward = -transform.position.normalized;
		FindFirstObjectByType<Light>().transform.position = transform.position;
    }
}
