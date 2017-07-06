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

	[System.NonSerialized]
	public GrassChunk[] chunksSafe;

	public int[,] detailMap;
}

public static class Extensions {

	public static float DistanceToLine(Vector2 linePnt, Vector2 lineDir, Vector2 pnt)
	{
		lineDir.Normalize();//this needs to be a unit vector
		var v = pnt - linePnt;
		var d = Vector2.Dot(v, lineDir);
		return Vector2.Distance(linePnt, linePnt + lineDir * d);
	}

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
			//for (int xC = 0; xC < grass [i].chunks2D.GetLength(0); xC++) {
			//	for (int yC = 0; yC < grass [i].chunks2D.GetLength(1); yC++) {
					//Checks if we should load the chunk
					if (Mathf.Sqrt ((xC - chunkPos.x) * (xC - chunkPos.x) + (yC - chunkPos.y) * (yC - chunkPos.y)) <= distance && grass [i].chunks2D [xC, yC] == -1) {
						GrassChunk chunk = new GrassChunk (new Vector2 (xC, yC));
						//GetChunk (ref chunk, ref grass [i].detailMap, t.terrainData.detailWidth, t.terrainData.detailHeight);

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

								//Debug.Log (x + " " + x1 + " " + (t.terrainData.detailWidth - 1) * density / CHUNK_SIZE + " " + t.terrainData.detailHeight + " " + map.GetLength(0));

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

								//float interpX = Mathf.Lerp()

								//grass [i].detailMap[(int) x, (int) y] > 0.001f) {
								if (chunk.len < 1022 && Mathf.Clamp01 (Mathf.PerlinNoise (x / detailWidth * size.z * noiseScale, y / detailWidth * size.x * noiseScale)) < chanceX * chanceY) {
									//Debug.Log (chanceX * chanceY);
									//Debug.Log (x + " " + y);
									chunk.len++;
									//Debug.Log (data.GetInterpolatedHeight ((float)y / (float)detailHeight, (float)x / (float)detailWidth));
									chunk.matrices [chunk.len - 1] = Matrix4x4.TRS (
										//Position
										new Vector3 (position.x + size.x * (y + Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f) * 2 * (1f / density)) / detailHeight,
											position.y + data.GetInterpolatedHeightSafe ((float)y / (float)detailHeight * (heights.GetLength (0) - 1), (float)x / (float)detailWidth * (heights.GetLength (1) - 1), size, heights),
											position.z + size.z * (x + Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f) * 2 * (1f / density)) / detailWidth),
										//new Vector3(size.x * y / detailHeight, 0f, size.z * x / detailWidth),
											
										//Rotation
										new Quaternion (0, 0, 0, 1),
										//Scale
										//new Vector3(1, 1, 1));
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
			for (int i = 0; i < grass.Length; i++) {
				if (grass [i].chunks != null) {
					grass [i].chunksSafe = new GrassChunk[grass [i].chunks.Count];
					for (int o = 0; o < grass [i].chunksSafe.Length; o++)
						grass [i].chunksSafe[o] = grass [i].chunks [o];
				}
			}
			//GrassUpdate ();
			grassThread.Start ();
		}
		for (int i = 0; i < grass.Length; i++)
			for (int o = 0; o < (grass [i].chunksSafe != null ? grass [i].chunksSafe.Length : 0); o++)
				Graphics.DrawMeshInstanced (grass [i].grassMesh, 0, grass [i].material, grass [i].chunksSafe[o].matrices, grass [i].chunksSafe [o].len);
	}
}
