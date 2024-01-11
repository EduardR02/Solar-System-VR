using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ParkourManager : MonoBehaviour
{
    public GameObject coinPrefab;
    public GameObject interactionChallengePrefab;
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

    private int currentChallenge;
    private int coinsCollected = 0;
    private Vector3[][] planetVertices;

    CelestialBody[] planets;
    Quaternion[] initialPlanetRotations;
    GameObject currentBeacon;
    List<GameObject> TShapes = new List<GameObject>();

    void Start()
    {
        InitPlanets();
        GenerateInteractionChallenge();
    }

    void InitPlanets() {
        Random.InitState(randomSeed);
        // don't include the sun or tiny moons
        planets = FindObjectsOfType<CelestialBody>().Where(planet => planet.bodyType == CelestialBody.BodyType.Planet).ToArray();
        initialPlanetRotations = new Quaternion[planets.Length];
        planetVertices = new Vector3[planets.Length][];
        int lowestResLod = 2;
        for (int i = 0; i < planets.Length; i++) {
            CelestialBodyGenerator generator = planets[i].GetComponentInChildren<CelestialBodyGenerator>();
            planetVertices[i] = generator.GetMeshVertices(lowestResLod);
            if (generator.GetOceanRadius() > 0) {
                planetVertices[i] = GetVerticesAboveOcean(planets[i].transform.position, generator.GetOceanRadius(), planetVertices[i]);
            }
            initialPlanetRotations[i] = planets[i].transform.rotation;
        }
    }

    Vector3[] GetVerticesAboveOcean(Vector3 oceanCenter, float oceanRadius, Vector3[] vertices) {
        return vertices.Where(vertex => Vector3.Distance(vertex, oceanCenter) > oceanRadius).ToArray();
    }

    // Update is called once per frame
    void Update()
    {
        // if i press the A button on the meta quest 3 vr controller, the interaction challenge is completed
        if (OVRInput.GetDown(OVRInput.Button.One)) {
            CompleteInteractionChallenge();
        }
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
            coin.transform.SetParent(coinParent.transform);
            angle += angleIncrement;
        }
    }

    void GenerateInteractionChallenge() {
        int planetIndex = Random.Range(0, planets.Length);
        CelestialBody planet = planets[planetIndex];
        Vector3 planetPos = planet.transform.position;
        CelestialBodyGenerator generator = planet.GetComponentInChildren<CelestialBodyGenerator>();
        Vector3 randomPosition = planetVertices[planetIndex][Random.Range(0, planetVertices[planetIndex].Length)] * generator.BodyScale;
        // spawn the interaction challenge and a beacon here
        Vector3 normal = randomPosition.normalized;
        Quaternion planetRotationDelta = Quaternion.Inverse(initialPlanetRotations[planetIndex]) * planet.transform.rotation;
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
        GameObject challenge = Instantiate(interactionChallengePrefab, challengePosition, challengeRotation * planetRotationDelta);
        GameObject beacon = Instantiate(beaconPrefab, beaconPosition, beaconRotation * planetRotationDelta);

        TShapes.Add(challenge);
        beacon.transform.localScale = new Vector3(beaconRadius, beaconHeight, beaconRadius);
        challenge.transform.localScale = Vector3.one * 5f;
        challenge.GetComponent<InteractionShape>().SetParentPlanet(planet);
        //challenge.transform.SetParent(planet.transform);  // would be nice for "structure", but because both have rigiboies this causes a bunch of weirdness, so just leave it
        
        beacon.transform.SetParent(planet.transform);
        challenge.name = "Interaction Challenge " + currentChallenge + " on " + planet.name;
        beacon.name = "Beacon " + currentChallenge;
        currentBeacon = beacon;

        GenerateCoinsAroundPlanet(planet);
    }

    void CompleteInteractionChallenge() {
    }

    public void UpdateOrigin(Vector3 originOffset) {
        if (TShapes.Count > 0) {
            foreach (GameObject tShape in TShapes) {
                tShape.GetComponent<InteractionShape>().UpdateOrigin(originOffset);
            }
        }
    }
}
