using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public OVRInput.Controller controller;
    private ParticleSystem particles;

    float maxLifetime;
    float minLifetimePercent = 0.35f;
    private ParticleSystem.Particle[] particlesArr;



    void Start()
    {
        particles = GetComponent<ParticleSystem>();
        maxLifetime = particles.main.startLifetime.constantMax;
        particlesArr = new ParticleSystem.Particle[particles.main.maxParticles];
    }  

    void Update()
    {
        if (controller == OVRInput.Controller.LTouch || controller == OVRInput.Controller.RTouch)
        {
            // to make the interaction challenge nicer, the Hand trigger will shoot particles, but not trigger player movement
            // Use cached input to avoid expensive native calls
            float TriggerVal = VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            TriggerVal = Mathf.Max(TriggerVal, VRInputCache.GetAxis1D(OVRInput.Axis1D.PrimaryHandTrigger, controller));
            TriggerVal = Mathf.Clamp01(TriggerVal);
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

    public void UpdateOrigin(Vector3 originOffset) {
        int numParticlesAlive = particles.GetParticles(particlesArr);
        for (int i = 0; i < numParticlesAlive; i++)
        {
            particlesArr[i].position -= originOffset;
        }
        particles.SetParticles(particlesArr, numParticlesAlive);
    }

    public ParticleSystem GetParticleSystem() {
        return particles;
    }
}