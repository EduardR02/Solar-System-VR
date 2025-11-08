using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ParkourManager : MonoBehaviour
{
    public GameObject coinPrefab;
    public GameObject interactionChallengePrefab;
    public GameObject intercationChallengeBoundingBoxPrefab;
    public GameObject interactionTargetPrefab;
    public GameObject beaconPrefab;
    public int interactionChallenges = 3;
    public int coinsPerChallenge = 10;
    public int randomSeed = 0;
    public float challengeSpawnHeight = 2f;
    [Range(1,3)]
    public float coinSpawnHeight = 1.5f;
    public bool atmosphereOverridesCoinHeight = true;
    public float beaconInnerRadius = 3f;
    public float beaconOuterRadius = 5f;
    public float beaconHeightToPlanetRadiusRatio = 2f;
    public List<ChallengeMetrics> challengeMetrics = new List<ChallengeMetrics>();

    private int currentChallenge = 0;
    public static int coinsCollected = 0;
    private Vector3[][] planetVertices;

    CelestialBody[] planets;
    Quaternion[] initialPlanetRotations;
    Ship player;
    GameObject currentBeacon;
    GameObject currentTShape;
    GameObject currentTTarget;
    private float challengeScaleMult = 5f;
    private float minDistanceToComplete = 100f;
    private int currentPlanetIndex = -1;
    private float taskStartTime;
    bool completed = false;

    void Start()
    {
        player = FindFirstObjectByType<Ship>();
        InitPlanets();
        GenerateInteractionChallenge();
    }

    void Update()
    {
        // A button - use cached input to avoid expensive native calls
        if (VRInputCache.GetButtonDown(OVRInput.Button.One)) {
            CompleteInteractionChallenge();
        }
        if (completed && taskStartTime + 10 < Time.time) {
            SceneManager.LoadScene("Menu Room", LoadSceneMode.Single);
        }
    }

    void InitPlanets() {
        if (randomSeed < 0) {
            int seed = System.Environment.TickCount;
            Random.InitState(seed);
            Debug.Log("Random seed: " + seed);
        }
        else {
            Random.InitState(randomSeed);
        }
        // Use cached bodies from NBodySimulation instead of expensive FindObjectsByType
        planets = NBodySimulation.Bodies.Where(planet => planet.bodyType == CelestialBody.BodyType.Planet).ToArray();
        initialPlanetRotations = new Quaternion[planets.Length];
        // No longer pre-allocate planetVertices array - we'll sample vertices on demand
        planetVertices = null;
        for (int i = 0; i < planets.Length; i++) {
            initialPlanetRotations[i] = planets[i].transform.rotation;
        }
    }

    // Sample a random vertex position from the planet mesh
    Vector3 SampleRandomVertexPosition(CelestialBodyGenerator generator) {
        int LodRes = 0;
        Vector3[] vertices = generator.GetMeshVertices(LodRes);
        float oceanRadius = generator.GetOceanRadius() / generator.BodyScale;

        // If planet has ocean, only pick vertices above ocean level
        if (oceanRadius > 0) {
            // Sample randomly until we find a vertex above ocean
            int maxAttempts = 100;
            for (int attempt = 0; attempt < maxAttempts; attempt++) {
                Vector3 vertex = vertices[Random.Range(0, vertices.Length)];
                if (vertex.sqrMagnitude > oceanRadius * oceanRadius) {
                    return vertex * generator.BodyScale;
                }
            }
        }

        // Fallback: just pick any random vertex
        return vertices[Random.Range(0, vertices.Length)] * generator.BodyScale;
    }

    Vector3[] GetVerticesAboveOcean(float oceanRadius, Vector3[] vertices) {
        return vertices.Where(vertex => vertex.sqrMagnitude > oceanRadius * oceanRadius).ToArray();
    }

    void GenerateCoinsAroundPlanet(CelestialBody planet) {
        if (coinsPerChallenge <= 0) {
            return;
        }
        CelestialBodyGenerator generator = planet.GetComponentInChildren<CelestialBodyGenerator>();
        float spawnRadius = generator.BodyScale * coinSpawnHeight;
        if (generator.body.shading.hasAtmosphere && atmosphereOverridesCoinHeight) {
            spawnRadius = generator.body.shading.atmosphereSettings.GetAtmosphereRadius(generator.BodyScale);
        }
        // spawn coins equidistantly around the planets rotation axis
        Vector3 planetCenter = planet.transform.position;
        Vector3 planetUp = planet.transform.up;
        Vector3 directionVector = planet.transform.forward * spawnRadius;
        float angle = 0;
        float angleIncrement = 360 / coinsPerChallenge;
        GameObject coinParent = new GameObject("Coins");
        coinParent.transform.SetParent(planet.transform);
        for (int i = 0; i < coinsPerChallenge; i++) {
            Vector3 coinPosition = planetCenter + Quaternion.AngleAxis(angle, planetUp) * directionVector;
            GameObject coin = Instantiate(coinPrefab, coinPosition, Quaternion.FromToRotation(Vector3.up, planetUp));
            coin.name = "Coin " + i;
            coin.transform.localScale = Vector3.one * generator.BodyScale / 10;
            coin.transform.SetParent(coinParent.transform);
            angle += angleIncrement;
        }
    }

    void GenerateInteractionChallenge() {
        currentChallenge++;
        int planetIndex = GenerateRandomPlanetIndex();
        CelestialBody planet = planets[planetIndex];
        Vector3 planetPos = planet.transform.position;
        CelestialBodyGenerator generator = planet.GetComponentInChildren<CelestialBodyGenerator>();
        Vector3 randomPosition = SampleRandomVertexPosition(generator);
        // spawn the interaction challenge and a beacon here
        Vector3 normal = randomPosition.normalized;
        Quaternion planetRotationDelta = Quaternion.Inverse(initialPlanetRotations[planetIndex]) * planet.transform.rotation;
        normal = planetRotationDelta * normal;
        randomPosition = planetRotationDelta * randomPosition;
        Quaternion challengeRotation = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 challengePosition = randomPosition + normal * challengeSpawnHeight + planetPos;
        // spawn beacon randomly in donut around challenge position
        float angle = Random.Range(0, 2 * Mathf.PI);
        float radius = Random.Range(beaconInnerRadius, beaconOuterRadius);
        float beaconHeight = beaconHeightToPlanetRadiusRatio * generator.BodyScale;
        float beaconRadius = beaconHeight / 10;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        offset.y += beaconHeight;   // no idea why this is right, should be / 2 (because center is height/2), but this works exactly right...
        offset.y -= beaconHeight / 20; // to account for terrain, somewhat magic number so the beacon is always starting from "fully inside" the planet.
        Vector3 beaconPosition = randomPosition + (challengeRotation * offset) + planetPos;
        // slightly different from challenge rotation because of displacement
        Quaternion beaconRotation = Quaternion.FromToRotation(Vector3.up, (beaconPosition - planetPos).normalized);
        GameObject challenge = Instantiate(interactionChallengePrefab, challengePosition, Random.rotation);
        GameObject box = Instantiate(intercationChallengeBoundingBoxPrefab, challengePosition, challengeRotation);
        GameObject beacon = Instantiate(beaconPrefab, beaconPosition, beaconRotation * planetRotationDelta);
        Vector3 targetPosition = challengePosition + (0.5f * challengeScaleMult * new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f));
        GameObject target = Instantiate(interactionTargetPrefab, targetPosition, Random.rotation);
        GameObject interactionChallenge = new GameObject("Interaction Challenge " + currentChallenge);

        currentTShape = challenge;
        currentTTarget = target;
        beacon.transform.localScale = new Vector3(beaconRadius, beaconHeight, beaconRadius);
        challenge.transform.localScale = Vector3.one * challengeScaleMult;
        target.transform.localScale = Vector3.one * challengeScaleMult;
        box.transform.localScale = Vector3.one * challengeScaleMult;

        challenge.GetComponent<InteractionShape>().SetParentPlanet(planet);

        box.transform.SetParent(interactionChallenge.transform);
        beacon.transform.SetParent(interactionChallenge.transform);
        target.transform.SetParent(interactionChallenge.transform);
        interactionChallenge.transform.SetParent(planet.transform);

        challenge.name = "Interaction Challenge " + currentChallenge + " on " + planet.name;
        beacon.name = "Beacon";
        box.name = "Bounding Box";
        target.name = "Target";
        currentBeacon = beacon;

        taskStartTime = Time.time;

        GenerateCoinsAroundPlanet(planet);
    }

    void CompleteInteractionChallenge() {
        if (currentTShape == null && currentChallenge >= interactionChallenges) {
            return;
        }
        if ((player.Rigidbody.position - currentTShape.GetComponent<Rigidbody>().position).sqrMagnitude > minDistanceToComplete * minDistanceToComplete) {
            return;
        }
        LockCurrentChallenge();
        UpdateChallengeMetrics();
        if (currentChallenge >= interactionChallenges) {
            Debug.Log("End of task");
            taskStartTime = Time.time;
            // once the challenge is completed, the player gets "sucked into" the sun for fun
            IncreaseSunGravity();
            completed = true;
            return;
        }
        GenerateInteractionChallenge();
    }

    void IncreaseSunGravity() {
        CelestialBody sun = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None).Where(planet => planet.bodyType == CelestialBody.BodyType.Sun).First();
        sun.playerGravityMultiplier *= 5;
    }

    int GenerateRandomPlanetIndex() {
        int index = Random.Range(0, planets.Length);
        while (index == currentPlanetIndex) {
            index = Random.Range(0, planets.Length);
        }
        currentPlanetIndex = index;
        return index;
    }

    void LockCurrentChallenge() {
        if (currentBeacon != null) {
            Destroy(currentBeacon);
        }
        currentTShape.GetComponent<InteractionShape>().LockChallenge();
    }

    void UpdateChallengeMetrics() {
        Vector3 manipulationError = Vector3.zero;
        for (int i = 0; i < currentTTarget.transform.childCount; i++)
        {
            manipulationError += currentTTarget.transform.GetChild(i).transform.position - currentTShape.transform.GetChild(i).transform.position;
        }
        // keep error consistent across any scale
        manipulationError /= challengeScaleMult;
        float time = Time.time - taskStartTime;
        int challengeCoins = coinsCollected;
        int score = CalculateScore(time, manipulationError, challengeCoins);
        challengeMetrics.Add(new ChallengeMetrics(time, manipulationError, challengeCoins, score));
        if (currentChallenge >= interactionChallenges) {
            ChallengeMetrics current_run = new ChallengeMetrics(challengeMetrics);
            current_run.SaveRun("last");
            challengeMetrics.Clear();
        }
        Debug.Log("Challenge " + currentChallenge + " completed in " + time + " seconds with " + challengeCoins);
        coinsCollected = 0;
        Debug.Log("Coins collected after reset: " + coinsCollected);
    }

    int CalculateScore(float time, Vector3 manipulationError, int collectedCoins) {
        // lets say 5 mins is max score per challenge
        int score = 5 * 60;
        // coins add to score
        score += collectedCoins * 10;
        // time subtracts from score
        score -= (int) time;
        // manipulation error modifies score, with zero error the score stays the same, with error more than 1 the score is 0
        score = (int) (score * Mathf.Max(1 - manipulationError.magnitude, 0));
        return score;
    }

    public void UpdateOrigin(Vector3 originOffset) {
        if (currentTShape != null) {
            currentTShape.GetComponent<InteractionShape>().UpdateOrigin(originOffset);
        }
    }

    // a bit eh, but whatever
    public static void IncrementCoinCount() {
        coinsCollected++;
    }
}
