Shader "Custom/CLUT" {
	Properties {
		_MainTex ("Albedo (R)", 2D) = "white" {}
		_LightGrid("Light grid (RG)", 2D) = "black" {}
		_CLUT("CLUT (RGB)", 2D) = "black" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows
		#pragma multi_compile __ TRANSPARENCY_ON
		#pragma multi_compile __ LINEAR

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))
			#define UNITY_ARGS_TEX2D(tex) Texture2D tex, SamplerState sampler##tex
			#define UNITY_PASS_TEX2D(tex) tex, sampler##tex
		#else
			#define UNITY_ARGS_TEX2D(tex) sampler2D tex
			#define UNITY_PASS_TEX2D(tex) tex
		#endif

		UNITY_DECLARE_TEX2D(_MainTex);
		uniform float4 _MainTex_TexelSize;

		UNITY_DECLARE_TEX2D(_LightGrid);

		UNITY_DECLARE_TEX2D(_CLUT);

		struct Input {
			float2 uv_MainTex;
			float2 uv_LightGrid;
			float w_LightGrid;
		};

		float4 clut(float index, float shade) {
			float4 c = UNITY_SAMPLE_TEX2D(_CLUT, float2(index, shade));

			#if defined(TRANSPARENCY_ON)
			c.a = index < 1.0/255.0 ? 0.0 : 1.0;
			#endif

			return c;
		}

		fixed4 texture2D_bilinear(UNITY_ARGS_TEX2D(tex), float2 uv, float4 texelSize, float shade) {
	#if defined(LINEAR)
			return clut(UNITY_SAMPLE_TEX2D(tex, uv).r, shade);
	#else
			uv -= texelSize.xy / 2;
			
			float tli = UNITY_SAMPLE_TEX2D(tex, uv).r;
			float tri = UNITY_SAMPLE_TEX2D(tex, uv + float2(texelSize.x, 0.0)).r;
			float bli = UNITY_SAMPLE_TEX2D(tex, uv + float2(0.0, texelSize.y)).r;
			float bri = UNITY_SAMPLE_TEX2D(tex, uv + texelSize.xy).r;

			float4 tl = clut(tli, shade);
			float4 tr = clut(tri, shade);
			float4 bl = clut(bli, shade);
			float4 br = clut(bri, shade);

			float2 f = frac(uv * texelSize.zw);
			float4 tA = lerp(tl, tr, f.x);
			float4 tB = lerp(bl, br, f.x);
			return lerp(tA, tB, f.y);
	#endif
		}


		void surf (Input IN, inout SurfaceOutputStandard o) {
			float2 maskedLight = UNITY_SAMPLE_TEX2D(_LightGrid, IN.uv_LightGrid).rg;
			float l = lerp(maskedLight.r, maskedLight.g, IN.w_LightGrid);
			fixed4 c = texture2D_bilinear(UNITY_PASS_TEX2D(_MainTex), IN.uv_MainTex, _MainTex_TexelSize, l);

			o.Albedo = c.rgb;
			o.Emission = texture2D_bilinear(UNITY_PASS_TEX2D(_MainTex), IN.uv_MainTex, _MainTex_TexelSize, 15.0).rgb;
			o.Metallic = 0;
			o.Smoothness = 0;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}