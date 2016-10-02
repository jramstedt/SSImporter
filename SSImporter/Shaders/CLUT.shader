Shader "Custom/CLUT" {
	Properties {
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_EmissionMap ("Emission (RGB)", 2D) = "black" {}
		_CLUT("CLUT (RGB)", 2D) = "black" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC)
			#define UNITY_ARGS_TEX2D(tex) Texture2D tex, SamplerState sampler##tex
			#define UNITY_PASS_TEX2D(tex) tex, sampler##tex
		#else
			#define UNITY_ARGS_TEX2D(tex) sampler2D tex
			#define UNITY_PASS_TEX2D(tex) tex
		#endif

		UNITY_DECLARE_TEX2D(_MainTex);
		uniform float4 _MainTex_TexelSize;

		UNITY_DECLARE_TEX2D(_EmissionMap);
		uniform float4 _EmissionMap_TexelSize;

		UNITY_DECLARE_TEX2D(_CLUT);

		struct Input {
			float2 uv_MainTex;
		};

		float4 clut(float index) {
			return UNITY_SAMPLE_TEX2D(_CLUT, float2(index, 0.5));
		}

		fixed4 texture2D_bilinear(UNITY_ARGS_TEX2D(tex), float2 uv, float4 texelSize) {
			uv -= texelSize.xy / 2;

			float4 tl = clut(UNITY_SAMPLE_TEX2D(tex, uv).r);
			float4 tr = clut(UNITY_SAMPLE_TEX2D(tex, uv + float2(texelSize.x, 0.0)).r);
			float4 bl = clut(UNITY_SAMPLE_TEX2D(tex, uv + float2(0.0, texelSize.y)).r);
			float4 br = clut(UNITY_SAMPLE_TEX2D(tex, uv + texelSize.xy).r);

			float2 f = frac(uv * texelSize.zw);
			float4 tA = lerp(tl, tr, f.x);
			float4 tB = lerp(bl, br, f.x);
			return lerp(tA, tB, f.y);
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			fixed4 c = texture2D_bilinear(UNITY_PASS_TEX2D(_MainTex), IN.uv_MainTex, _MainTex_TexelSize);
			o.Albedo = c.rgb;
			o.Emission = texture2D_bilinear(UNITY_PASS_TEX2D(_EmissionMap), IN.uv_MainTex, _EmissionMap_TexelSize);
			o.Metallic = 0;
			o.Smoothness = 0;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}