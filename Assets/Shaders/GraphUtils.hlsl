#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"

#ifndef GRAPH_UTILS_INCLUDED
#define GRAPH_UTILS_INCLUDED

half4 clut(UnityTexture2D CLUT, half index, half shade) {
  #if !defined(_BILINEAR)
    half4 c = CLUT.Sample(CLUT.samplerstate, half2(index, shade));
  #else
    shade -= 0.5 / 16.0;
    half4 uc = CLUT.Sample(CLUT.samplerstate, half2(index, shade));
    half4 lc = CLUT.Sample(CLUT.samplerstate, half2(index, shade + (1.0 / 16.0)));
    half4 c = lerp(uc, lc, frac(shade * 16.0));
  #endif

  #if defined(_ALPHATEST_ON)
    c.a = index < 1.0/255.0 ? 0.0 : 1.0;
  #endif

  return c;
}

void colorLookup_float(UnityTexture2D CLUT, UnityTexture2D indexTexture, half2 uv, half shade, out half3 color, out half alpha) {
  #if !defined(_BILINEAR)
    half index = indexTexture.Sample(indexTexture.samplerstate, uv).r;
    half4 result = clut(CLUT, index, shade);
  #else
    half4 texelSize = indexTexture.texelSize;

    uv -= texelSize.xy / 2.0;
                
    half tli = indexTexture.Sample(indexTexture.samplerstate, uv).r;
    half tri = indexTexture.Sample(indexTexture.samplerstate, uv + half2(texelSize.x, 0.0)).r;
    half bli = indexTexture.Sample(indexTexture.samplerstate, uv + half2(0.0, texelSize.y)).r;
    half bri = indexTexture.Sample(indexTexture.samplerstate, uv + texelSize.xy).r;

    half4 tl = clut(CLUT, tli, shade);
    half4 tr = clut(CLUT, tri, shade);
    half4 bl = clut(CLUT, bli, shade);
    half4 br = clut(CLUT, bri, shade);

    float2 f = frac(uv * texelSize.zw);

    half4 tA = lerp(tl, tr, f.x);
    half4 tB = lerp(bl, br, f.x);
    half4 result = lerp(tA, tB, f.y);
  #endif
    
    color = result.rgb;
    alpha = result.a;
}

void translucency_float(half3 background, half opacity, half purity, half3 color, out half3 output) {
  half3 base = color * opacity;

  half density = (1.0 - opacity) * purity;
  half clarity = 1.0 - opacity - density;

  half3 filter = clarity + ((color * density) / 64.0);
  output = background * filter + base;
}

void shade_float(UnityTexture2D lightGridTexture, half3 worldPosition, half lightGridInterpolation, out half shade) {
  half4 texelSize = lightGridTexture.texelSize;

  half2 lightmap = lightGridTexture.Sample(lightGridTexture.samplerstate, (worldPosition.xz + 0.5) * texelSize.xy).rg;
  shade = lerp(lightmap.r, lightmap.g, lightGridInterpolation);
}

#if SHADER_TARGET < 45 || !defined(PLATFORM_SUPPORT_GATHER)
SAMPLER(SmpClampPoint);
#endif

void shadePrecise_float(UnityTexture2D lightGridTexture, float3 worldPosition, float lightGridInterpolation, out float shade) {
  float4 texelSize = lightGridTexture.texelSize;
  float2 uv = floor(worldPosition.xz) * texelSize.xy + texelSize.xy / 2.0;
  
#if defined(SHADERGRAPH_PREVIEW) || defined(UNITY_PASS_SHADOWCASTER)
  shade = 1.0;
#else
  
#if SHADER_TARGET >= 45 && defined(PLATFORM_SUPPORT_GATHER)
  float4 floors = lightGridTexture.GatherRed(lightGridTexture.samplerstate, uv);
  float4 ceilings = lightGridTexture.GatherGreen(lightGridTexture.samplerstate, uv);
#else
  float2 tl = lightGridTexture.Sample(SmpClampPoint, uv).rg;
  float2 tr = lightGridTexture.Sample(SmpClampPoint, uv + float2(texelSize.x, 0.0)).rg;
  float2 bl = lightGridTexture.Sample(SmpClampPoint, uv + float2(0.0, texelSize.y)).rg;
  float2 br = lightGridTexture.Sample(SmpClampPoint, uv + texelSize.xy).rg;

  float4 floors = float4(bl.r, br.r, tr.r, tl.r);
  float4 ceilings = float4(bl.g, br.g, tr.g, tl.g);
#endif

  float2 floorRow = lerp(floors.wx, floors.zy, frac(worldPosition.x));
  float floorShade = lerp(floorRow.x, floorRow.y, frac(worldPosition.z));

  float2 ceilingRow = lerp(ceilings.wx, ceilings.zy, frac(worldPosition.x));
  float ceilingShade = lerp(ceilingRow.x, ceilingRow.y, frac(worldPosition.z));

  shade = lerp(floorShade, ceilingShade, lightGridInterpolation);
  
#endif
}

#endif //GRAPH_UTILS_INCLUDED