using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

[System.Serializable]
public class GrassChunk {
	public Matrix4x4[] matrices;
	public Vector2 coords;
	public int len;

	public GrassChunk (Vector2 c) {
		coords = c;
	}
}

[System.Serializable]
public class GrassType {
	public Mesh grassMesh;
	public Material material;
	public float sizeScale;
	[System.NonSerialized]
	public List<GrassChunk> chunks;
	public int[,] chunks2D;

	public List<Matrix4x4[]> renderMatrices;
	public List<int> renderMatrixCount;

	public List<Matrix4x4[]> renderMatricesSafe;
	public List<int> renderMatrixCountSafe;

	public int[,] detailMap;
}

public static class Extensions {

	public static float GetHeightTriangle (Vector2 point, Vector3 ta, Vector3 tb, Vector3 tc) {
		Vector3 normal = Vector3.Cross(tb - ta, tc - ta);
		float d = -Vector3.Dot(ta, normal);
		return -(normal.x * point.x + normal.z * point.y + d) / normal.y;
	}

	public static float GetInterpolatedHeightSafe (this TerrainData data, float y, float x, Vector3 size, float[,] heights) {

		float rX = x - (int)x;
		float rY = y - (int)y;

		float h1 = heights[Mathf.Min((int)x, heights.GetLength(0)-1), Mathf.Min((int)y, heights.GetLength(1)-1)];
		float h2 = heights[Mathf.Min(Mathf.CeilToInt(x), heights.GetLength(0)-1), Mathf.Min((int)y, heights.GetLength(1)-1)];
		float h3 = heights[Mathf.Min((int)x, heights.GetLength(0)-1), Mathf.Min(Mathf.CeilToInt(y), heights.GetLength(1)-1)];
		float h4 = heights[Mathf.Min(Mathf.CeilToInt(x), heights.GetLength(0)-1), Mathf.Min(Mathf.CeilToInt(y), heights.GetLength(1)-1)];

		float ret = 0f;

		if (rX < rY) {
			ret = GetHeightTriangle (new Vector2 (rX, rY), new Vector3 (0f, h1, 0f), new Vector3 (0f, h3, 1f), new Vector3 (1f, h4, 1f)) * size.y;
		} else {
			ret = GetHeightTriangle (new Vector2 (rX, rY), new Vector3 (0f, h1, 0f), new Vector3 (1f, h2, 0f), new Vector3 (1f, h4, 1f)) * size.y;
		}

		//Debug.Log (ret);

		return ret;
	}
}

public class TerrainGrass : MonoBehaviour {

	public float distance = 30f;
	public Transform trackingObject;

	public Terrain t;
	public GameObject prefab;
	public float density = 10f;
	public float noiseScale = 10f;
	public AnimationCurve densityCurve;
	public float sizeScale = 0.1f;
	public float sizeMultiplier = 0.3f;
	public AnimationCurve sizeCurve;
	public Material mat;
	public Mesh grassMesh;
	public List<Matrix4x4[]> matrices;
	Quaternion rotation = Quaternion.Euler (0, 180, 0);

	public GrassType[] grass;

	const int CHUNK_SIZE = 31;

	Thread grassThread;

	Vector2 PosToChunk(Vector3 pos) {
		Vector3 terrainLocalPos = pos - t.transform.position;
		return new Vector2(Mathf.InverseLerp(0.0f, t.terrainData.size.z, terrainLocalPos.z) * density / CHUNK_SIZE * t.terrainData.detailWidth,
			Mathf.InverseLerp(0.0f, t.terrainData.size.x, terrainLocalPos.x) * density / CHUNK_SIZE * t.terrainData.detailHeight);
		
	}

	// Use this for initialization
	void Start () {
		//matrices = new List<Matrix4x4[]> ();
		//matrices.Add (new Matrix4x4[1023]);
		//SpawnGrass ();

		for (int i = 0; i < grass.Length; i++)
			grass[i].detailMap = t.terrainData.GetDetailLayer(0, 0, t.terrainData.detailWidth, t.terrainData.detailHeight, i);
		heights = t.terrainData.GetHeights (0, 0, t.terrainData.heightmapWidth, t.terrainData.heightmapHeight);
	}

	public Vector2 chunkPos;

	int detailWidth;
	int detailHeight;
	Vector3 size;
	TerrainData data;
	Vector3 position;
	float[,] heights;

	// Update is called once per frame
	void GrassUpdate () {

		//Loops through the grass array
		for (int i = 0; i < grass.Length; i++) {

			if (grass [i].chunks == null)
				grass [i].chunks = new List<GrassChunk> ();

			//If density has changed, clean everything
			if (grass [i].chunks2D == null || grass [i].chunks2D.GetLength (0) - 1 < (int)density / CHUNK_SIZE * detailWidth || grass [i].chunks2D.GetLength (1) - 1 < (int)density / CHUNK_SIZE * detailHeight) {
				grass [i].chunks2D = new int[(int)(density / CHUNK_SIZE * detailWidth) + 1, (int)(density / CHUNK_SIZE * detailHeight) + 1];
				for (int x = 0; x < grass [i].chunks2D.GetLength (0); x++) {
					for (int y = 0; y < grass [i].chunks2D.GetLength (1); y++) {
						grass [i].chunks2D [x, y] = -1;
					}
				}
				grass [i].chunks.Clear ();
			}

			//Checks if loaded chunks have to be unloaded
			for (int o = 0; o < grass [i].chunks.Count; o++)
				if (Vector2.Distance (grass [i].chunks [o].coords, chunkPos) > distance) {
					grass [i].chunks2D [(int)grass [i].chunks [o].coords.x, (int)grass [i].chunks [o].coords.y] = -1;
					grass [i].chunks.RemoveAt (o--);
				}

			//Loops through close chunks, loads the needed chunks
			for (int xC = (int)Mathf.Max (chunkPos.x - distance, 0); xC < (int)Mathf.Min (chunkPos.x + distance, grass [i].chunks2D.GetLength(0)); xC++) {
				for (int yC = (int)Mathf.Max (chunkPos.y - distance, 0); yC < (int)Mathf.Min (chunkPos.y + distance, grass [i].chunks2D.GetLength(1)); yC++) {
					//Checks if we should load the chunk
					if (Mathf.Sqrt ((xC - chunkPos.x) * (xC - chunkPos.x) + (yC - chunkPos.y) * (yC - chunkPos.y)) <= distance && grass [i].chunks2D [xC, yC] == -1) {
						GrassChunk chunk = new GrassChunk (new Vector2 (xC, yC));
						chunk.matrices = new Matrix4x4[1023];

						for (float x1 = chunk.coords.x; x1 < Mathf.Min ((detailWidth - 1) * density / CHUNK_SIZE, chunk.coords.x + 1); x1 += 1f / CHUNK_SIZE) {
							for (float y1 = chunk.coords.y; y1 < Mathf.Min ((detailHeight - 1) * density / CHUNK_SIZE, chunk.coords.y + 1); y1 += 1f / CHUNK_SIZE) {
								float x = x1 / density * CHUNK_SIZE;
								float y = y1 / density * CHUNK_SIZE;

								if (x >= detailWidth || y >= detailHeight) {
									continue;
								}

								float chanceX = 0f;
								float chanceY = 0f;

								//Calculate the density of the grass based on the neighboring grass values
								if ((int)x == 0)
									chanceX = grass [i].detailMap [(int)x, (int)y];
								else if (x - (int)x < 0.5f)
									chanceX = Mathf.Lerp (grass [i].detailMap [(int)x - 1, (int)y], grass [i].detailMap [(int)x, (int)y], (x - (int)x) * 2);
								else if (x + 1 < detailWidth)
									chanceX = Mathf.Lerp (grass [i].detailMap [(int)x + 1, (int)y], grass [i].detailMap [(int)x, (int)y], (x - (int)x) * 2);

								if ((int)y == 0)
									chanceY = grass [i].detailMap [(int)x, (int)y];
								else if (y - (int)y < 0.5f)
									chanceY = Mathf.Lerp (grass [i].detailMap [(int)x, (int)y - 1], grass [i].detailMap [(int)x, (int)y], (y - (int)y) * 2);
								else if (y + 1 < detailHeight)
									chanceY = Mathf.Lerp (grass [i].detailMap [(int)x, (int)y + 1], grass [i].detailMap [(int)x, (int)y], (y - (int)y) * 2);

								chanceX = densityCurve.Evaluate (chanceX / 16);
								chanceY = densityCurve.Evaluate (chanceY / 16);

								//Checks if should add the grass at certain point
								if (chunk.len < 1022 && Mathf.Clamp01 (Mathf.PerlinNoise (x / detailWidth * size.z * noiseScale, y / detailWidth * size.x * noiseScale)) < chanceX * chanceY) {
									chunk.len++;
									chunk.matrices [chunk.len - 1] = Matrix4x4.TRS (
										//Position
										new Vector3 (position.x + size.x * (y + Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f) * 2 * (1f / density)) / detailHeight,
											position.y + data.GetInterpolatedHeightSafe ((float)y / (float)detailHeight * (heights.GetLength (0) - 1), (float)x / (float)detailWidth * (heights.GetLength (1) - 1), size, heights),
											position.z + size.z * (x + Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f) * 2 * (1f / density)) / detailWidth),
										//Rotation
										Quaternion.Lerp(new Quaternion (0, 0, 0, 1), rotation, Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f)),
										//Scale
										new Vector3 (sizeCurve.Evaluate (Mathf.PerlinNoise (x / detailWidth * size.z * sizeScale, y / detailWidth * size.x * sizeScale)) * sizeMultiplier,
											sizeCurve.Evaluate (Mathf.PerlinNoise (x / detailWidth * size.z * sizeScale, y / detailWidth * size.x * sizeScale)) * sizeMultiplier,
											sizeCurve.Evaluate (Mathf.PerlinNoise (x / detailWidth * size.z * sizeScale, y / detailWidth * size.x * sizeScale)) * sizeMultiplier));
								}
							}
						}

						grass [i].chunks.Add (chunk);
						grass [i].chunks2D [xC, yC] = grass [i].chunks.Count - 1;
					}
				}
			}

			//Batch the grass into bigger arrays to call a little bit less of DrawMeshInstanced
			grass [i].renderMatrices = new List<Matrix4x4[]> ();
			grass [i].renderMatrixCount = new List<int> ();
			int cC = -1; //Counter for the current grass chunk being checked
			int rmC = 0; //Counter for the current return matrix array
			int rmCT = 0; //Counter for the last matrix in the return array
			grass [i].renderMatrices.Add (new Matrix4x4[1023]);
			while (++cC < grass [i].chunks.Count) {
				if (rmCT + grass [i].chunks [cC].len >= 1023) {
					grass [i].renderMatrices.Add (new Matrix4x4[1023]);
					grass [i].renderMatrixCount.Add (rmCT);
					rmC++;
					rmCT = 0;
				}
				int u = rmCT;
				for (u = rmCT; u <= rmCT + grass [i].chunks [cC].len; u++) {
					grass [i].renderMatrices [rmC] [u] = grass [i].chunks [cC].matrices [u-rmCT];
				}
				rmCT = u;
			}
			grass [i].renderMatrixCount.Add (rmCT);
		}
	}

	void Update() {
		if (grassThread == null || grassThread.ThreadState == ThreadState.Stopped || grassThread.ThreadState == ThreadState.Unstarted) {
			grassThread = new Thread (GrassUpdate);

			detailWidth = t.terrainData.detailWidth;
			detailHeight = t.terrainData.detailHeight;
			position = t.transform.position;
			size = t.terrainData.size;
			data = t.terrainData;
			chunkPos = PosToChunk (trackingObject.position);
			rotation = Quaternion.Euler (0, 180, 0);
			for (int i = 0; i < grass.Length; i++) {
				if (grass [i].chunks != null) {
					grass [i].renderMatricesSafe = grass [i].renderMatrices;
					grass [i].renderMatrixCountSafe = grass [i].renderMatrixCount;
				}
			}
			//GrassUpdate ();
			grassThread.Start ();
		}
		for (int i = 0; i < grass.Length; i++)
			for (int o = 0; o < ((grass [i].renderMatricesSafe != null && grass [i].renderMatrixCountSafe != null) ? grass [i].renderMatrixCountSafe.Count : 0); o++) {
				Graphics.DrawMeshInstanced (grass [i].grassMesh, 0, grass [i].material, grass [i].renderMatricesSafe [o], grass [i].renderMatrixCountSafe [o]);
			}
	}
}
