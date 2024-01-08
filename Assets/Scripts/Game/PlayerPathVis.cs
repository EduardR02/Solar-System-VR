using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPathVis : MonoBehaviour
{

    [Tooltip("Number of steps to simulate using the physics timestep")]
    public int numSteps = 500;
    public int PhysicsStepsPerSimUpdate = 5;
    private int PhysicsUpdateCounter = 0;
    private float timeStep;
    private Ship player;
    private VirtualBody[] virtualBodies;
    private LineRenderer lineRenderer;
    private Dictionary<int, int> bodyIDToIndex = new Dictionary<int, int>();
    VirtualBody playerVirt;
    CelestialBody referenceBody;

    // Start is called before the first frame update
    void Start()
    {
        timeStep = Universe.physicsTimeStep * PhysicsStepsPerSimUpdate;
        lineRenderer = GetComponentInChildren<LineRenderer>();
        player = FindObjectOfType<Ship>();
        lineRenderer.positionCount = numSteps;
        playerVirt = new VirtualBody(numSteps);
        referenceBody = player.ReferenceBody;
        InitVirtualBodies();
        InitDictionary();
    }

    void Update()
    {
        DrawPath();
    }

    void FixedUpdate() {
        if (PhysicsUpdateCounter == 0) {
            UpdatePlayerSim(1);
            SimulateBodies(1);
        }
        PhysicsUpdateCounter = (PhysicsUpdateCounter + 1) % PhysicsStepsPerSimUpdate;
    }

    void DrawPath() {
        referenceBody = player.ReferenceBody;
        if (referenceBody == null || player.IsColliding() || !lineRenderer.enabled) {
            lineRenderer.enabled = false;
            return;
        }
        Vector3 offset = player.transform.position - playerVirt.simluatedPositions.Get(0);
        VirtualBody referenceVirtualBody = virtualBodies[bodyIDToIndex[referenceBody.GetInstanceID()]];
        lineRenderer.SetPosition(0, playerVirt.simluatedPositions.Get(0) + offset);
        for (int i = 1; i < numSteps; i++) {
            Vector3 planetOffset = referenceVirtualBody.simluatedPositions.Get(0) - referenceVirtualBody.simluatedPositions.Get(i);
            lineRenderer.SetPosition(i, playerVirt.simluatedPositions.Get(i) + offset + planetOffset);
        }
       
    }

    void UpdatePlayerSim(int steps) {
        Vector3 posDelta = Vector3.one;
        if (player.IsColliding()) {
            return;
        }
        lineRenderer.enabled = true;
        if (referenceBody != null && !playerVirt.simluatedPositions.isEmpty()) {
            Vector3 timeStepAdjust = Vector3.Lerp(playerVirt.simluatedPositions.Get(1), playerVirt.simluatedPositions.Get(2), PhysicsUpdateCounter / (float)PhysicsStepsPerSimUpdate);
            posDelta = player.transform.position - timeStepAdjust;
        }
        if (referenceBody != null && referenceBody == player.ReferenceBody && posDelta.magnitude < 0.01f) {
            CalcPlayerPath(steps);
        }
        else {
            CalcPlayerPath();
        }
    }

    void CalcPlayerPath(int steps = 0) {
        if (steps == 0) {
            playerVirt.simluatedPositions.Add(player.Rigidbody.position);
            playerVirt.velocity = player.Rigidbody.velocity;
        }
        else {
            steps = numSteps - 1 - steps;
        }
        for (int i = steps; i < numSteps - 1; i++) {
            Vector3 acceleration = CalculateAcceleration(playerVirt.simluatedPositions.GetLast(), i);
            playerVirt.velocity += acceleration * timeStep;
            Vector3 newPos = playerVirt.simluatedPositions.GetLast() + playerVirt.velocity * timeStep;
            playerVirt.simluatedPositions.Add(newPos);
        }
    }

    void InitVirtualBodies() {
        CelestialBody[] bodies = FindObjectsOfType<CelestialBody> ();
        virtualBodies = new VirtualBody[bodies.Length];
        for (int i = 0; i < virtualBodies.Length; i++) {
            virtualBodies[i] = new VirtualBody (bodies[i], numSteps);
        }
        SimulateBodies(numSteps - 2);
    }

    void InitDictionary() {
        for (int i = 0; i < virtualBodies.Length; i++) {
            bodyIDToIndex[virtualBodies[i].id] = i;
        }
    }

    void SimulateBodies(int steps) {
        Vector3 newPosition;
        for (int step = 0; step < steps; step++) {
            // Update velocities
            for (int i = 0; i < virtualBodies.Length; i++) {
                virtualBodies[i].velocity += CalculateAcceleration(i) * timeStep;
            }
            // Update positions
            for (int i = 0; i < virtualBodies.Length; i++) {
                newPosition = virtualBodies[i].simluatedPositions.GetLast() + virtualBodies[i].velocity * timeStep;
                virtualBodies[i].simluatedPositions.Add(newPosition);
            }
        }
    }

    Vector3 CalculateAcceleration (int ignoreBody = -1) {
        Vector3 acceleration = Vector3.zero;
        for (int j = 0; j < virtualBodies.Length; j++) {
            if (ignoreBody == j) {
                continue;
            }
            Vector3 forceDir = (virtualBodies[j].simluatedPositions.GetLast() - virtualBodies[ignoreBody].simluatedPositions.GetLast()).normalized;
            float sqrDst = (virtualBodies[j].simluatedPositions.GetLast() - virtualBodies[ignoreBody].simluatedPositions.GetLast()).sqrMagnitude;
            acceleration += forceDir * Universe.gravitationalConstant * virtualBodies[j].mass / sqrDst;
        }
        return acceleration;
    }

    Vector3 CalculateAcceleration (Vector3 point, int step) {
        Vector3 acceleration = Vector3.zero;
        for (int j = 0; j < virtualBodies.Length; j++) {
            Vector3 forceDir = (virtualBodies[j].simluatedPositions.Get(step) - point).normalized;
            float sqrDst = (virtualBodies[j].simluatedPositions.Get(step) - point).sqrMagnitude;
            acceleration += forceDir * Universe.gravitationalConstant * virtualBodies[j].mass / sqrDst;
        }
        return acceleration;
    }

    public void OriginShift(Vector3 offset) {
        for (int i = 0; i < numSteps; i++) {
            for (int j = 0; j < virtualBodies.Length; j++) {
                virtualBodies[j].simluatedPositions.buffer[i] -= offset;
            }
            playerVirt.simluatedPositions.buffer[i] -= offset;
        }
    }

    class VirtualBody {
        public float mass;
        public int id;
        public Vector3 velocity;
        public RingBuffer<Vector3> simluatedPositions;

        public VirtualBody (CelestialBody body, int numSteps) {
            // last position of ringbuffer is duplicate to this, but whatever
            mass = body.mass;
            id = body.GetInstanceID();
            simluatedPositions = new RingBuffer<Vector3>(numSteps);
            // a bit crude to synchronize, so .get(0) is the same as the actual body AFTER the update
            simluatedPositions.Add(body.transform.position);
            simluatedPositions.Add(body.transform.position);
            velocity = body.initialVelocity;
        }

        public VirtualBody (int numSteps) {
            mass = -1;
            id = -1;
            simluatedPositions = new RingBuffer<Vector3>(numSteps);
        }
    }


    class RingBuffer<T> {
        public T[] buffer;
        int index = -1;

        public RingBuffer (int size) {
            buffer = new T[size];
        }

        public void Add (T item) {
            index = (index + 1) % buffer.Length;
            buffer[index] = item;
        }

        // only works when buffer is full
        public T Get (int i) {
            if (index == -1) {
                throw new System.InvalidOperationException("RingBuffer is empty");
            }
            return buffer[(index + i + 1) % buffer.Length];
        }

        public T GetLast() {
            if (index == -1) {
                throw new System.InvalidOperationException("RingBuffer is empty");
            }
            return Get(-1);
        }

        public bool isEmpty() {
            return index == -1;
        }
    }


}
