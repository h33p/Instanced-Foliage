Shader "Custom/Grass" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
		[HideInInspector]
		_position_mul ("Position Multiplier 1", Range(0,10)) = 1
		[HideInInspector]
		_windDir ("Wind Direction 1", Vector) = (1, 0.5, 0.5)
		[HideInInspector]
		_intensity ("Wind Intensity 1", Range(0,1)) = 0.7
		[HideInInspector]
		_speed ("Wind Speed 1", Range(0,10)) = 1
		[HideInInspector]
		_position_mul2 ("Position Multiplier 2", Range(0,10)) = 2
		[HideInInspector]
		_windDir2 ("Wind Direction 2", Vector) = (-1, 0.2, 0.4)
		[HideInInspector]
		_intensity2 ("Wind Intensity 2", Range(0,0.2)) = 0.2
		[HideInInspector]
		_speed2 ("Wind Speed 2", Range(0,10)) = 5
		[HideInInspector]
		_position_mul3 ("Position Multiplier 3", Range(0,10)) = 4
		[HideInInspector]
		_windDir3 ("Wind Direction 3", Vector) = (.3, 0.5, 0.7)
		[HideInInspector]
		_intensity3 ("Wind Intensity 3", Range(0,0.2)) = 0.02
		[HideInInspector]
		_speed3 ("Wind Speed 3", Range(0,10)) = 3
		[HideInInspector]
		_position_mul4 ("Position Multiplier 4", Range(0,10)) = 5
		[HideInInspector]
		_windDir4 ("Wind Direction 4", Vector) = (1, 0.5, 0.5)
		[HideInInspector]
		_intensity4 ("Wind Intensity 4", Range(0,0.2)) = 0.01
		[HideInInspector]
		_speed4 ("Wind Speed 4", Range(0,10)) = 10
		[HideInInspector]
		_length ("Wind Complexity", Range(0,4)) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry+1"}
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert alphatest:_Cutoff addshadow

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		float _position_mul;
		float3 _windDir;
        float _intensity;
        float _speed;
        float _position_mul2;
        float3 _windDir2;
        float _intensity2;
        float _speed2;
        float _position_mul3;
        float3 _windDir3;
        float _intensity3;
        float _speed3;
        float _position_mul4;
        float3 _windDir4;
        float _intensity4;
        float _speed4;
        int _length;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_CBUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_CBUFFER_END

		struct VertexInput
 		{
 			float4 vertex   : POSITION;
 			half3 normal    : NORMAL;
 			float2 uv0      : TEXCOORD0;
 		};

		void vert (inout appdata_full v) {
        	float pi = 3.141592654;
        	float4 pos = mul(unity_ObjectToWorld, v.vertex);
        	if (_length >= 1) {
			v.vertex.xyz += normalize(_windDir) *
				sin(pi + _Time.g * _speed + (pos.r + pos.b) * _position_mul)
				 * _intensity * v.texcoord.g;
			}
			if (_length >= 2) {
			v.vertex.xyz += normalize(_windDir2) *
				sin(pi + _Time.g * _speed2 + (pos.r + pos.b) * _position_mul2)
				 * _intensity2 * v.texcoord.g;
			}
			if (_length >= 3) {
			v.vertex.xyz += normalize(_windDir3) *
				sin(pi + _Time.g * _speed3 + (pos.r + pos.b) * _position_mul3)
				 * _intensity3 * v.texcoord.g;
			}
			if (_length >= 4) {
			v.vertex.xyz += normalize(_windDir4) *
				sin(pi + _Time.g * _speed4 + (pos.r + pos.b) * _position_mul4)
				 * _intensity4 * v.texcoord.g;
			}
        }

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			//clip(c.a - _Cutoff);
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
	CustomEditor "GrassShaderGUI"
}
