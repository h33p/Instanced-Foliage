Shader "Custom/Grass" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
		_position_mul ("Position Multiplier", Range(0,10)) = 1
		_intensity ("Wind Intensity", Range(0,1)) = 0.5
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
        float _intensity;

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
        	float3 windDir = float3(1,0.5,0.5);
        	float4 pos = mul(unity_ObjectToWorld, v.vertex);
			v.vertex.xyz += windDir *
				sin(pi + _Time.g + (pos.r + pos.b) * _position_mul)
				 * _intensity * v.texcoord.g;

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
}
