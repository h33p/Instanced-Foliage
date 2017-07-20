using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

public class GrassShaderGUI : ShaderGUI {

	private static bool NameMatches (MaterialProperty property, string name) {
		return property.name == name;
	}

	public bool showWind1 = true;
	public bool showWind2 = true;
	public bool showWind3 = true;
	public bool showWind4 = true;

	public override void OnGUI (MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		base.OnGUI (materialEditor, properties);

		int length = 0;

		int posMul = 0;
		int windDir = 0;
		int intens = 0;
		int speed = 0;

		int posMul2 = 0;
		int windDir2 = 0;
		int intens2 = 0;
		int speed2 = 0;

		int posMul3 = 0;
		int windDir3 = 0;
		int intens3 = 0;
		int speed3 = 0;

		int posMul4 = 0;
		int windDir4 = 0;
		int intens4 = 0;
		int speed4 = 0;

		for (int i = 0; i < properties.Length; i++) {
			if (properties [i].name == "_length")
				length = i;

			if (properties [i].name == "_position_mul")
				posMul = i;
			if (properties [i].name == "_windDir")
				windDir = i;
			if (properties [i].name == "_intensity")
				intens = i;
			if (properties [i].name == "_speed")
				speed = i;
			if (properties [i].name == "_position_mul2")
				posMul2 = i;
			if (properties [i].name == "_windDir2")
				windDir2 = i;
			if (properties [i].name == "_intensity2")
				intens2 = i;
			if (properties [i].name == "_speed2")
				speed2 = i;
			if (properties [i].name == "_position_mul3")
				posMul3 = i;
			if (properties [i].name == "_windDir3")
				windDir3 = i;
			if (properties [i].name == "_intensity3")
				intens3 = i;
			if (properties [i].name == "_speed3")
				speed3 = i;
			if (properties [i].name == "_position_mul4")
				posMul4 = i;
			if (properties [i].name == "_windDir4")
				windDir4 = i;
			if (properties [i].name == "_intensity4")
				intens4 = i;
			if (properties [i].name == "_speed4")
				speed4 = i;
			
		}
			
		EditorGUILayout.BeginHorizontal ();
		EditorGUILayout.LabelField (properties[length].displayName);
		properties[length].floatValue = Mathf.RoundToInt (EditorGUILayout.Slider ((int)properties[length].floatValue, 0, 4));
		EditorGUILayout.EndHorizontal ();

		if (properties [length].floatValue >= 1) {
			showWind1 = Foldout (showWind1, "Wind 1", true);
			if (showWind1) {
				EditorGUILayout.BeginVertical ("Box");
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[posMul].displayName);
				properties[posMul].floatValue = EditorGUILayout.Slider (properties[posMul].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.LabelField (properties[windDir].displayName);
				properties[windDir].vectorValue = EditorGUILayout.Vector3Field ("", properties[windDir].vectorValue);
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[intens].displayName);
				properties[intens].floatValue = EditorGUILayout.Slider (properties[intens].floatValue, 0, 1);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[speed].displayName);
				properties[speed].floatValue = EditorGUILayout.Slider (properties[speed].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.EndVertical ();
			}
		}

		if (properties [length].floatValue >= 2) {
			showWind2 = Foldout (showWind2, "Wind 2", true);
			if (showWind2) {
				EditorGUILayout.BeginVertical ("Box");
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[posMul2].displayName);
				properties[posMul2].floatValue = EditorGUILayout.Slider (properties[posMul2].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.LabelField (properties[windDir2].displayName);
				properties[windDir2].vectorValue = EditorGUILayout.Vector3Field ("", properties[windDir2].vectorValue);
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[intens2].displayName);
				properties[intens2].floatValue = EditorGUILayout.Slider (properties[intens2].floatValue, 0, 0.5f);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[speed2].displayName);
				properties[speed2].floatValue = EditorGUILayout.Slider (properties[speed2].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.EndVertical ();
			}
		}

		if (properties [length].floatValue >= 3) {
			showWind3 = Foldout (showWind3, "Wind 3", true);
			if (showWind3) {
				EditorGUILayout.BeginVertical ("Box");
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[posMul3].displayName);
				properties[posMul3].floatValue = EditorGUILayout.Slider (properties[posMul3].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.LabelField (properties[windDir3].displayName);
				properties[windDir3].vectorValue = EditorGUILayout.Vector3Field ("", properties[windDir3].vectorValue);
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[intens3].displayName);
				properties[intens3].floatValue = EditorGUILayout.Slider (properties[intens3].floatValue, 0, 0.2f);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[speed3].displayName);
				properties[speed3].floatValue = EditorGUILayout.Slider (properties[speed3].floatValue, 0, 20);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.EndVertical ();
			}
		}

		if (properties [length].floatValue >= 4) {
			showWind4 = Foldout (showWind4, "Wind 4", true);
			if (showWind4) {
				EditorGUILayout.BeginVertical ("Box");
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[posMul4].displayName);
				properties[posMul4].floatValue = EditorGUILayout.Slider (properties[posMul4].floatValue, 0, 10);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.LabelField (properties[windDir4].displayName);
				properties[windDir4].vectorValue = EditorGUILayout.Vector3Field ("", properties[windDir4].vectorValue);
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[intens4].displayName);
				properties[intens4].floatValue = EditorGUILayout.Slider (properties[intens4].floatValue, 0, 0.1f);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (properties[speed4].displayName);
				properties[speed4].floatValue = EditorGUILayout.Slider (properties[speed4].floatValue, 0, 50);
				EditorGUILayout.EndHorizontal ();
				EditorGUILayout.EndVertical ();
			}
		}
	}

	public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick, GUIStyle style)
	{
		Rect position = GUILayoutUtility.GetRect(40f, 40f, 16f, 16f, style);
		return EditorGUI.Foldout(position, foldout, content, toggleOnLabelClick, style);
	}

	public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick, GUIStyle style) {
		return Foldout(foldout, new GUIContent(content), toggleOnLabelClick, style);
	}

	public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) {
		return Foldout(foldout, new GUIContent(content), toggleOnLabelClick, EditorStyles.foldout);
	}
}
