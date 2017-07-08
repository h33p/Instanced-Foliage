using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(TerrainFoliage))]
public class TerrainFoliageInspector : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		TerrainFoliage myScript = (TerrainFoliage)target;
		if(GUILayout.Button("Copy Terrain Detail Prototypes"))
		{
			myScript.DumpDetailPrototypes();
		}
	}
}
