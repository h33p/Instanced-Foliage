using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.Linq;

[System.Serializable]
public class FoliageChunk {
	public Matrix4x4[] matrices;
	public Vector2 coords;
	public int len;
	public bool clipping;

	public FoliageChunk (Vector2 c) {
		coords = c;
	}
}

[System.Serializable]
public class FoliageTexture {
	public string name;
	public Texture2D texture;

	public FoliageTexture (string n, Texture2D tex) {
		name = n;
		texture = tex;
	}
}

[System.Serializable]
public class FoliageType {
	public Mesh foliageMesh;
	public Material baseMaterial;
	public Material useMaterial;
	public FoliageTexture[] textures;
	public Vector3 minSize;
	public Vector3 maxSize;
	public float sizeScale;
	[System.NonSerialized]
	public List<FoliageChunk> chunks;
	public int[,] chunks2D;

	//These are used in the thread, trying to render from these leads to undefined behavior
	public List<Matrix4x4[]> renderMatrices;
	[HideInInspector]
	public List<int> renderMatrixCount;

	public List<Matrix4x4[]> renderMatricesSafe;
	[HideInInspector]
	public List<int> renderMatrixCountSafe;

	public int[,] detailMap;
}

public static class Extensions {

	public static float GetHeightTriangle (Vector2 point, Vector3 ta, Vector3 tb, Vector3 tc) {
		Vector3 normal = Vector3.Cross(tb - ta, tc - ta);
		float d = -Vector3.Dot(ta, normal);
		return -(normal.x * point.x + normal.z * point.y + d) / normal.y;
	}

	//Because we can not call internal Unity C++ functions from the other thread
	public static float GetInterpolatedHeightSafe (this TerrainData data, float y, float x, Vector3 size, float[,] heights) {

		//Get offset from the bottom left closest coordinate in the heights field
		float rX = x - (int)x;
		float rY = y - (int)y;

		//Get heights on all points
		float h1 = heights[Mathf.Min((int)x, heights.GetLength(0)-1), Mathf.Min((int)y, heights.GetLength(1)-1)];
		float h2 = heights[Mathf.Min(Mathf.CeilToInt(x), heights.GetLength(0)-1), Mathf.Min((int)y, heights.GetLength(1)-1)];
		float h3 = heights[Mathf.Min((int)x, heights.GetLength(0)-1), Mathf.Min(Mathf.CeilToInt(y), heights.GetLength(1)-1)];
		float h4 = heights[Mathf.Min(Mathf.CeilToInt(x), heights.GetLength(0)-1), Mathf.Min(Mathf.CeilToInt(y), heights.GetLength(1)-1)];

		float ret = 0f;

		//Depending on the relative position, blend differently, each square is made up of 2 triangles, and this way of blending achieves best results
		if (rX < rY)
			ret = GetHeightTriangle (new Vector2 (rX, rY), new Vector3 (0f, h1, 0f), new Vector3 (0f, h3, 1f), new Vector3 (1f, h4, 1f)) * size.y;
		else
			ret = GetHeightTriangle (new Vector2 (rX, rY), new Vector3 (0f, h1, 0f), new Vector3 (1f, h2, 0f), new Vector3 (1f, h4, 1f)) * size.y;

		//Could have returned the value in the statements above, but had to do some debugging and might need to do it later
		return ret;
	}
}

[RequireComponent(typeof(Terrain))]
public class TerrainFoliage : MonoBehaviour {

	public bool draw = true;

	private float _distance = 1f;
	private float _terDistance = 1f;
	//private float _density = 1f;
	public float distance {
		get {
			return _distance;
		}
	}
	public Transform trackingObject;
	public Transform trackObj {
		get {
			if (trackingObject != null)
				return trackingObject;
			return Camera.main.transform;
		}
	}

	Terrain _t;
	public Terrain t {
		get {
			if (_t == null)
				_t = GetComponent<Terrain> ();
			return _t;
		}
	}
	public GameObject prefab;
	public float density = 1f;
	public float noiseScale = 1f;
	public AnimationCurve densityCurve = new AnimationCurve (new Keyframe (0, 0, 3.963f, 3.963f), 
		                                     new Keyframe (0.235f, 0.578f, 1.334f, 1.334f),
		                                     new Keyframe (1, 1, 0.06f, 0.06f));
	public AnimationCurve densitySizeCurve = new AnimationCurve (new Keyframe (0, 0, 7.6f, 7.6f), 
		                                         new Keyframe (0.195f, 0.774f, 0.7f, 0.7f),
		                                         new Keyframe (1, 1, 0.0f, 0.0f));
	public float sizeMultiplier = 0.3f;
	public AnimationCurve sizeCurve = new AnimationCurve (new Keyframe (0, 0.3f, 0.913f, 0.913f),
		                                  new Keyframe (1, 1, 0.0f, 0.0f));
	public Material defaultMaterial;
	public Mesh defaultFoliageMesh;
	public List<Matrix4x4[]> matrices;
	Quaternion rotation = Quaternion.Euler (0, 180, 0);
	[Tooltip("Experimental thread scaling. If set to 2, 2 sides will be generated on separate threads, if set to 3, 4 parts will be generated separately.")]
	[Range(1, 3)]
	public int threadScaling = 2;
	int _threadScaling;

	public FoliageType[] foliage;

	const int CHUNK_SIZE = 31;

	Vector2 chunkPos;

	int detailWidth;
	int detailHeight;
	Vector3 size;
	TerrainData data;
	Vector3 position;
	float[,] heights;


	Thread foliageThread;

	void OnDestroy () {
		if (foliageThread != null)
			foliageThread.Abort ();
	}

	public void DumpDetailPrototypes () {
		foliage = new FoliageType[t.terrainData.detailPrototypes.Length];

		for (int i = 0; i < foliage.Length; i++) {
			foliage [i] = new FoliageType ();
			if (!t.terrainData.detailPrototypes [i].usePrototypeMesh) {
				foliage [i].foliageMesh = defaultFoliageMesh;
				foliage [i].baseMaterial = defaultMaterial;
				foliage [i].textures = new FoliageTexture[1];
				foliage [i].textures [0] = new FoliageTexture ("_MainTex", t.terrainData.detailPrototypes [i].prototypeTexture);
			}

			foliage [i].minSize = new Vector3 (t.terrainData.detailPrototypes [i].minWidth, t.terrainData.detailPrototypes [i].minHeight, t.terrainData.detailPrototypes [i].minWidth);
			foliage [i].maxSize = new Vector3 (t.terrainData.detailPrototypes [i].maxWidth, t.terrainData.detailPrototypes [i].maxHeight, t.terrainData.detailPrototypes [i].maxWidth);
		}
	}

	void SetupFoliage () {
		for (int i = 0; i < foliage.Length; i++) {
			if (foliage [i].useMaterial == null) {
				foliage [i].useMaterial = new Material (foliage [i].baseMaterial);
				for (int o = 0; o < foliage [i].textures.Length; o++)
					foliage [i].useMaterial.SetTexture (foliage [i].textures [o].name, foliage [i].textures [o].texture);
			}
			foliage[i].detailMap = t.terrainData.GetDetailLayer(0, 0, t.terrainData.detailWidth, t.terrainData.detailHeight, i);
		}
	}

	Vector2 PosToChunk(Vector3 pos) {
		Vector3 terrainLocalPos = pos - t.transform.position;
		return new Vector2(terrainLocalPos.z / t.terrainData.size.z * density / CHUNK_SIZE * t.terrainData.detailWidth,
			terrainLocalPos.x / t.terrainData.size.x * density / CHUNK_SIZE * t.terrainData.detailHeight);
	}

	// Use this for initialization
	void Start () {
		SetupFoliage ();
		heights = t.terrainData.GetHeights (0, 0, t.terrainData.heightmapWidth, t.terrainData.heightmapHeight);
	}

	void FoliageLoop (int minX, int maxX, int minY, int maxY, ref List<FoliageChunk> chunkList, int i) {

		if (chunkList == null)
			chunkList = new List<FoliageChunk> ();

		float densityX = density;
		float densityY = density;

		if (size.x > size.z)
			densityY *= size.x / size.y;
		else if (size.x < size.z)
			densityX *= size.y / size.x;

		//int[,] detailMap = foliage [i].detailMap.Select(a => a.ToArray()).ToArray();

		//Loops through close chunks, loads the needed chunks
		for (int xC = minX; xC <= maxX; xC++) {
			for (int yC = minY; yC <= maxY; yC++) {
				//Checks if we should load the chunk
				if ((Mathf.Sqrt ((xC - chunkPos.x) * (xC - chunkPos.x) + (yC - chunkPos.y) * (yC - chunkPos.y)) <= distance
					|| Mathf.Sqrt ((xC - chunkPos.x + 1) * (xC - chunkPos.x + 1) + (yC - chunkPos.y + 1) * (yC - chunkPos.y + 1)) <= distance
					|| Mathf.Sqrt ((xC - chunkPos.x + 1) * (xC - chunkPos.x + 1) + (yC - chunkPos.y) * (yC - chunkPos.y)) <= distance
					|| Mathf.Sqrt ((xC - chunkPos.x) * (xC - chunkPos.x) + (yC - chunkPos.y + 1) * (yC - chunkPos.y + 1)) <= distance) && foliage [i].chunks2D [xC, yC] == -1) {
					FoliageChunk chunk = new FoliageChunk (new Vector2 (xC, yC));
					chunk.matrices = new Matrix4x4[1023];
					chunk.clipping = false;

					for (float x1 = chunk.coords.x; x1 < Mathf.Min ((detailWidth - 1) * density / CHUNK_SIZE, chunk.coords.x + 1); x1 += 1f / CHUNK_SIZE) {
						for (float y1 = chunk.coords.y; y1 < Mathf.Min ((detailHeight - 1) * density / CHUNK_SIZE, chunk.coords.y + 1); y1 += 1f / CHUNK_SIZE) {
							float x = x1 / density * CHUNK_SIZE;
							float y = y1 / density * CHUNK_SIZE;

							if (x >= detailWidth || y >= detailHeight) {
								continue;
							}

							float chanceX = 0f;
							float chanceY = 0f;

							//Calculate the density of the foliage based on the neighboring foliage values
							if ((int)x == 0)
								chanceX = foliage [i].detailMap [(int)x, (int)y];
							else if (x - (int)x < 0.5f)
								chanceX = Mathf.Lerp (foliage [i].detailMap [(int)x - 1, (int)y], foliage [i].detailMap [(int)x, (int)y], (x - (int)x) * 2);
							else if (x + 1 < detailWidth)
								chanceX = Mathf.Lerp (foliage [i].detailMap [(int)x + 1, (int)y], foliage [i].detailMap [(int)x, (int)y], (x - (int)x) * 2);

							if ((int)y == 0)
								chanceY = foliage [i].detailMap [(int)x, (int)y];
							else if (y - (int)y < 0.5f)
								chanceY = Mathf.Lerp (foliage [i].detailMap [(int)x, (int)y - 1], foliage [i].detailMap [(int)x, (int)y], (y - (int)y) * 2);
							else if (y + 1 < detailHeight)
								chanceY = Mathf.Lerp (foliage [i].detailMap [(int)x, (int)y + 1], foliage [i].detailMap [(int)x, (int)y], (y - (int)y) * 2);

							chanceX = densityCurve.Evaluate (chanceX / 16);
							chanceY = densityCurve.Evaluate (chanceY / 16);

							//Checks if should add the foliage at certain point
							if (chunk.len < 1022 && Mathf.Clamp01 (Mathf.PerlinNoise (x / detailWidth * size.z * noiseScale, y / detailWidth * size.x * noiseScale)) < chanceX * chanceY) {

								if (Mathf.Sqrt ((x1 - chunkPos.x) * (x1 - chunkPos.x) + (y1 - chunkPos.y) * (y1 - chunkPos.y)) > distance) {
									chunk.clipping = true;
									continue;
								}

								chunk.len++;

								float densitySized;
								//Weird things happen if not locked
								lock (this) {
									densitySized = densitySizeCurve.Evaluate (chanceX * chanceY);
								}
								float scalePerlin = Mathf.PerlinNoise (x / detailWidth * size.z * foliage [i].sizeScale, y / detailWidth * size.x * foliage [i].sizeScale);
								float posPerlin = Mathf.PerlinNoise (x / detailWidth * size.z * 100f, y / detailHeight * size.x * 100f);

								chunk.matrices [chunk.len - 1] = Matrix4x4.TRS (
									//Position
									new Vector3 (position.x + size.x * (y + posPerlin * 2 * (1f / densityY)) / detailHeight,
										position.y + data.GetInterpolatedHeightSafe ((float)y / (float)detailHeight * (heights.GetLength (0) - 1), (float)x / (float)detailWidth * (heights.GetLength (1) - 1), size, heights),
										position.z + size.z * (x + posPerlin * 2 * (1f / densityX)) / detailWidth),
									//Rotation
									Quaternion.Lerp (new Quaternion (0, 0, 0, 1), rotation, Mathf.PerlinNoise (x / detailWidth * size.z * 1000f, y / detailHeight * size.x * 1000f)),
									//Scale
									new Vector3 (Mathf.Lerp(foliage [i].minSize.x, foliage [i].maxSize.x, sizeCurve.Evaluate (scalePerlin)) * sizeMultiplier * densitySized,
										Mathf.Lerp(foliage [i].minSize.y, foliage [i].maxSize.y, sizeCurve.Evaluate (scalePerlin)) * sizeMultiplier * densitySized,
										Mathf.Lerp(foliage [i].minSize.z, foliage [i].maxSize.z, sizeCurve.Evaluate (scalePerlin)) * sizeMultiplier * densitySized));
							}
						}
					}

					chunkList.Add (chunk);
				}
			}
		}

	}

	// Update is called once per frame
	void FoliageUpdate () {

		float densityX = density;
		float densityY = density;

		if (size.x > size.z)
			densityY *= size.x / size.y;
		else if (size.x < size.z)
			densityX *= size.y / size.x;

		//Loops through the foliage array
		for (int i = 0; i < foliage.Length; i++) {

			if (foliage [i].chunks == null)
				foliage [i].chunks = new List<FoliageChunk> ();

			//If density has changed, clean everything
			if (foliage [i].chunks2D == null || foliage [i].chunks2D.GetLength (0) - 1 < (int)densityX / CHUNK_SIZE * detailWidth || foliage [i].chunks2D.GetLength (1) - 1 < (int)density / CHUNK_SIZE * detailHeight) {
				foliage [i].chunks2D = new int[(int)(density / CHUNK_SIZE * detailWidth) + 1, (int)(densityY / CHUNK_SIZE * detailHeight) + 1];
				for (int x = 0; x < foliage [i].chunks2D.GetLength (0); x++) {
					for (int y = 0; y < foliage [i].chunks2D.GetLength (1); y++) {
						foliage [i].chunks2D [x, y] = -1;
					}
				}
				foliage [i].chunks.Clear ();
			}

			//Checks if loaded chunks have to be unloaded
			for (int o = 0; o < foliage [i].chunks.Count; o++)
				if (Vector2.Distance (foliage [i].chunks [o].coords, chunkPos) > distance || Vector2.Distance (foliage [i].chunks [o].coords + new Vector2(1, 1), chunkPos) > distance || Vector2.Distance (foliage [i].chunks [o].coords + new Vector2(1, 0), chunkPos) > distance || Vector2.Distance (foliage [i].chunks [o].coords + new Vector2(0, 1), chunkPos) > distance || foliage [i].chunks [o].clipping) {
					foliage [i].chunks2D [(int)foliage [i].chunks [o].coords.x, (int)foliage [i].chunks [o].coords.y] = -1;
					foliage [i].chunks.RemoveAt (o--);
				}
			
			if (_threadScaling == 1) {
				List<FoliageChunk> chunkList = new List<FoliageChunk> ();
				FoliageLoop ((int)Mathf.Max (chunkPos.x - distance, 0), (int)Mathf.Min (chunkPos.x + distance, foliage [i].chunks2D.GetLength (0) - 1),
					(int)Mathf.Max (chunkPos.y - distance, 0), (int)Mathf.Min (chunkPos.y + distance, foliage [i].chunks2D.GetLength (1) - 1),
					ref chunkList, i);
				foreach (FoliageChunk c in chunkList) {
					foliage [i].chunks.Add (c);
					foliage [i].chunks2D [(int)c.coords.x, (int)c.coords.y] = foliage [i].chunks.Count - 1;
				}
			} else {
				Thread[] slices = new Thread[(_threadScaling < 3) ? _threadScaling : 4];
				List<FoliageChunk>[] chunkLists = new List<FoliageChunk>[slices.Length];

				switch (slices.Length) {
				case 2:
					slices [0] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x - distance, 0), (int)Mathf.Min (chunkPos.x + distance, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y - distance, 0), (int)Mathf.Min (chunkPos.y, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists [0], i));
					slices [0].Start ();
					slices [1] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x - distance, 0), (int)Mathf.Min (chunkPos.x + distance, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y + 1, 0), (int)Mathf.Min (chunkPos.y + distance, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists[1], i));
					slices [1].Start ();
					break;
				case 4:
					slices [0] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x - distance, 0), (int)Mathf.Min (chunkPos.x, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y - distance, 0), (int)Mathf.Min (chunkPos.y, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists[0], i));
					slices [0].Start ();
					slices [1] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x - distance, 0), (int)Mathf.Min (chunkPos.x, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y + 1, 0), (int)Mathf.Min (chunkPos.y + distance, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists[1], i));
					slices [1].Start ();
					slices [2] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x + 1, 0), (int)Mathf.Min (chunkPos.x + distance, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y - distance, 0), (int)Mathf.Min (chunkPos.y, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists[2], i));
					slices [2].Start ();
					slices [3] = new Thread (() => FoliageLoop ((int)Mathf.Max (chunkPos.x + 1, 0), (int)Mathf.Min (chunkPos.x + distance, foliage [i].chunks2D.GetLength (0) - 1),
						(int)Mathf.Max (chunkPos.y + 1, 0), (int)Mathf.Min (chunkPos.y + distance, foliage [i].chunks2D.GetLength (1) - 1),
						ref chunkLists[3], i));
					slices [3].Start ();
					break;
				}
					
				foreach (Thread t in slices) {
					t.Join ();
				}

				foreach (List<FoliageChunk> cl in chunkLists) {
					if (cl != null) {
						foreach (FoliageChunk c in cl) {
							if (foliage [i].chunks2D [(int)c.coords.x, (int)c.coords.y] == -1) {
								foliage [i].chunks.Add (c);
								foliage [i].chunks2D [(int)c.coords.x, (int)c.coords.y] = foliage [i].chunks.Count - 1;
							} else
								Debug.LogError ("Chunk " + c.coords + " appeared in the list twice!");
						}
					}
				}
			}
			//Batch the foliage into bigger arrays to call a little bit less of DrawMeshInstanced
			foliage [i].renderMatrices = new List<Matrix4x4[]> ();
			foliage [i].renderMatrixCount = new List<int> ();
			int cC = -1; //Counter for the current foliage chunk being checked
			int rmC = 0; //Counter for the current return matrix array
			int rmCT = 0; //Counter for the last matrix in the return array
			foliage [i].renderMatrices.Add (new Matrix4x4[1023]);
			while (++cC < foliage [i].chunks.Count) {
				if (rmCT + foliage [i].chunks [cC].len >= 1023) {
					foliage [i].renderMatrices.Add (new Matrix4x4[1023]);
					foliage [i].renderMatrixCount.Add (rmCT);
					rmC++;
					rmCT = 0;
				}
				int u = rmCT;
				for (u = rmCT; u <= rmCT + foliage [i].chunks [cC].len; u++) {
					foliage [i].renderMatrices [rmC] [u] = foliage [i].chunks [cC].matrices [u-rmCT];
				}
				rmCT = u;
			}
			foliage [i].renderMatrixCount.Add (rmCT);
		}
	}

	void Update() {
		//If the thread has stopped, just start it over, reading the updated values
		if (foliageThread == null || foliageThread.ThreadState == ThreadState.Stopped || foliageThread.ThreadState == ThreadState.Unstarted) {
			foliageThread = new Thread (FoliageUpdate);

			detailWidth = t.terrainData.detailWidth;
			detailHeight = t.terrainData.detailHeight;
			position = t.transform.position;
			size = t.terrainData.size;
			data = t.terrainData;
			chunkPos = PosToChunk (trackObj.position);
			rotation = Quaternion.Euler (0, 180, 0);
			_threadScaling = threadScaling;

			if (t.detailObjectDistance != _terDistance) {
				_terDistance = t.detailObjectDistance;
				_distance = (_terDistance / t.terrainData.size.x) * density / CHUNK_SIZE * t.terrainData.detailWidth;
			}

			for (int i = 0; i < foliage.Length; i++) {
				if (foliage [i].detailMap == null)
					foliage[i].detailMap = t.terrainData.GetDetailLayer(0, 0, t.terrainData.detailWidth, t.terrainData.detailHeight, i);
				if (foliage [i].chunks != null) {
					foliage [i].renderMatricesSafe = foliage [i].renderMatrices;
					foliage [i].renderMatrixCountSafe = foliage [i].renderMatrixCount;
				}
			}
			if (heights == null)
				heights = t.terrainData.GetHeights (0, 0, t.terrainData.heightmapWidth, t.terrainData.heightmapHeight);
			//Kept for debugging, if any errors occur, only if on main thread they will show in the console.
			//FoliageUpdate ();
			foliageThread.Start ();
		}

		//Draw everything
		if (draw) {
			for (int i = 0; i < foliage.Length; i++)
				for (int o = 0; o < ((foliage [i].renderMatricesSafe != null && foliage [i].renderMatrixCountSafe != null) ? foliage [i].renderMatrixCountSafe.Count : 0); o++)
					Graphics.DrawMeshInstanced (foliage [i].foliageMesh, 0, foliage [i].useMaterial, foliage [i].renderMatricesSafe [o], foliage [i].renderMatrixCountSafe [o]);
		}

	}
}
