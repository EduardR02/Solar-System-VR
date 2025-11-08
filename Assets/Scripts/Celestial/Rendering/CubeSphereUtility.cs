using UnityEngine;

public static class CubeSphereUtility {

	public static Vector3 CubeToSphere (int faceIndex, float u, float v) {
		float x = u * 2f - 1f;
		float y = v * 2f - 1f;
		Vector3 dir;
		switch (faceIndex) {
			case 0: dir = new Vector3 (1, y, -x); break;      // +X
			case 1: dir = new Vector3 (-1, y, x); break;      // -X
			case 2: dir = new Vector3 (x, 1, -y); break;      // +Y
			case 3: dir = new Vector3 (x, -1, y); break;      // -Y
			case 4: dir = new Vector3 (x, y, 1); break;       // +Z
			case 5: dir = new Vector3 (-x, y, -1); break;     // -Z
			default: dir = Vector3.forward; break;
		}
		return dir.normalized;
	}

	public static Vector3 FaceNormal (int faceIndex) {
		switch (faceIndex) {
			case 0: return Vector3.right;
			case 1: return Vector3.left;
			case 2: return Vector3.up;
			case 3: return Vector3.down;
			case 4: return Vector3.forward;
			case 5: return Vector3.back;
			default: return Vector3.up;
		}
	}

	public static void DirectionToFaceUV (Vector3 dir, out int faceIndex, out Vector2 uv) {
		dir.Normalize ();
		Vector3 absDir = new Vector3 (Mathf.Abs (dir.x), Mathf.Abs (dir.y), Mathf.Abs (dir.z));
		float u, v;

		if (absDir.x >= absDir.y && absDir.x >= absDir.z) {
			if (dir.x > 0) {
				faceIndex = 0;
				u = -dir.z / absDir.x;
				v = dir.y / absDir.x;
			} else {
				faceIndex = 1;
				u = dir.z / absDir.x;
				v = dir.y / absDir.x;
			}
		} else if (absDir.y >= absDir.x && absDir.y >= absDir.z) {
			if (dir.y > 0) {
				faceIndex = 2;
				u = dir.x / absDir.y;
				v = -dir.z / absDir.y;
			} else {
				faceIndex = 3;
				u = dir.x / absDir.y;
				v = dir.z / absDir.y;
			}
		} else {
			if (dir.z > 0) {
				faceIndex = 4;
				u = dir.x / absDir.z;
				v = dir.y / absDir.z;
			} else {
				faceIndex = 5;
				u = -dir.x / absDir.z;
				v = dir.y / absDir.z;
			}
		}

		uv = new Vector2 (u * 0.5f + 0.5f, v * 0.5f + 0.5f);
	}
}
