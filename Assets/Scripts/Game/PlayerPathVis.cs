using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPathVis : MonoBehaviour
{

    public int numSteps = 500;
    public float timeStep = 0.01f;
    public bool usePhysicsTimeStep;
    private Ship player;
    private VirtualBody[] virtualBodies;
    private LineRenderer lineRenderer;
    private Dictionary<int, int> bodyIDToIndex = new Dictionary<int, int>();
    Vector3[] playerPath;



    // Start is called before the first frame update
    void Start()
    {
        if (usePhysicsTimeStep) {
            timeStep = Universe.physicsTimeStep;
        }
        lineRenderer = GetComponentInChildren<LineRenderer>();
        player = FindObjectOfType<Ship>();
        lineRenderer.positionCount = numSteps;
        playerPath = new Vector3[numSteps];
        InitVirtualBodies();
        InitDictionary();
    }

    // Update is called once per frame
    void Update()
    {
        DrawPath();
    }

    void FixedUpdate() {
        SimulateBodies(1);
    }

    void DrawPath() {
        CalcPlayerPath();
        lineRenderer.SetPositions(playerPath);
    }

    void CalcPlayerPath() {
        playerPath[0] = player.transform.position;
        CelestialBody referenceBody = player.ReferenceBody;
        VirtualBody referenceVirtualBody = (referenceBody != null) ? virtualBodies[bodyIDToIndex[referenceBody.GetInstanceID()]] : null;
        Vector3 PlayerVelocity = player.Rigidbody.velocity - ((referenceBody != null) ? referenceBody.Rigidbody.velocity: Vector3.zero);
        for (int i = 0; i < numSteps - 1; i++) {
            Vector3 acceleration = CalculateAcceleration(playerPath[i], i);
            PlayerVelocity += (acceleration - ((referenceVirtualBody != null) ? referenceVirtualBody.simulatedAccs.Get(i+1): Vector3.zero)) * timeStep;
            playerPath[i + 1] = playerPath[i] + PlayerVelocity * timeStep;
        }
    }

    void InitVirtualBodies() {
        CelestialBody[] bodies = FindObjectsOfType<CelestialBody> ();
        virtualBodies = new VirtualBody[bodies.Length];
        for (int i = 0; i < virtualBodies.Length; i++) {
            virtualBodies[i] = new VirtualBody (bodies[i], numSteps);
        }
        SimulateBodies(numSteps - 1);
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
                virtualBodies[i].simulatedAccs.Add(CalculateAcceleration(i));
                virtualBodies[i].velocity +=  virtualBodies[i].simulatedAccs.GetLast() * timeStep;
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
        for (int i = 0; i < virtualBodies.Length; i++) {
            for (int j = 0; j < virtualBodies[i].simluatedPositions.buffer.Length; j++) {
                virtualBodies[i].simluatedPositions.buffer[j] -= offset;
            }
        }
    }

    class VirtualBody {
        public float mass;
        public int id;
        public Vector3 velocity;
        public RingBuffer<Vector3> simluatedPositions;
        public RingBuffer<Vector3> simulatedAccs;

        public VirtualBody (CelestialBody body, int numSteps) {
            // last position of ringbuffer is duplicate to this, but whatever
            mass = body.mass;
            id = body.GetInstanceID();
            simluatedPositions = new RingBuffer<Vector3>(numSteps);
            simluatedPositions.Add(body.transform.position);
            velocity = body.initialVelocity;
            simulatedAccs = new RingBuffer<Vector3>(numSteps);
            simulatedAccs.Add(Vector3.zero);
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
    }


}
