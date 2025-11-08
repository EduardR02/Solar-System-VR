using System.Collections.Generic;
using UnityEngine;

public static class PlanetEnvironmentRegistry {

	static readonly List<CelestialBodyGenerator> bodies = new List<CelestialBodyGenerator> ();
	static int version;

	public static IReadOnlyList<CelestialBodyGenerator> Bodies => bodies;
	public static int Version => version;

	public static bool Register (CelestialBodyGenerator generator) {
		if (!generator || bodies.Contains (generator)) {
			return false;
		}

		bodies.Add (generator);
		version++;
		return true;
	}

	public static bool Unregister (CelestialBodyGenerator generator) {
		if (!generator) {
			return false;
		}

		if (bodies.Remove (generator)) {
			version++;
			return true;
		}

		return false;
	}

	public static void MarkDirty () {
		version++;
	}
}
