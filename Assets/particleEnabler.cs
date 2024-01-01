using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public OVRInput.Controller controller;
    private ParticleSystem particles;

    float maxLifetime;
    float minLifetimePercent = 0.35f;



    void Start()
    {
        particles = GetComponent<ParticleSystem>();
        maxLifetime = particles.main.startLifetime.constantMax;
    }  

    void Update()
    {
        if (controller == OVRInput.Controller.LTouch || controller == OVRInput.Controller.RTouch)
        {
            float TriggerVal = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            if (TriggerVal > 0.0f) {
                var main = particles.main;
                main.startLifetime = maxLifetime * (minLifetimePercent + (1.0f - minLifetimePercent) * TriggerVal);
                particles.Play();
            }
            else if (TriggerVal == 0.0f && !particles.isStopped) {
                particles.Stop();
            }
        }
        
    }
}