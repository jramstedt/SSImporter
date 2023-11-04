//UNITY_SHADER_NO_UPGRADE
#ifndef GRAPH_UTILS_INCLUDED
#define GRAPH_UTILS_INCLUDED

void shade_float(UnityTexture2D lightGridTexture, half3 worldPosition, half lightGridInterpolation, out half shade) {
    half4 texelSize = lightGridTexture.texelSize;

    half2 lightmap = SAMPLE_TEXTURE2D(lightGridTexture, lightGridTexture.samplerstate, (worldPosition.xz + 0.5) * texelSize.xy).rg;
    shade = lerp(lightmap.r, lightmap.g, lightGridInterpolation);
}

half4 clut(UnityTexture2D CLUT, half index, half shade) {
  #if !defined(_BILINEAR)
    half4 c = SAMPLE_TEXTURE2D(CLUT, CLUT.samplerstate, half2(index, shade));
  #else
    shade -= 0.5 / 16.0;
    half4 uc = SAMPLE_TEXTURE2D(CLUT, CLUT.samplerstate, half2(index, shade));
    half4 lc = SAMPLE_TEXTURE2D(CLUT, CLUT.samplerstate, half2(index, shade + (1.0 / 16.0)));
    half4 c = lerp(uc, lc, frac(shade * 16.0));
  #endif

  #if defined(_ALPHATEST_ON)
    c.a = index < 1.0/255.0 ? 0.0 : 1.0;
  #endif

  return c;
}

void colorLookup_float(UnityTexture2D CLUT, UnityTexture2D indexTexture, half2 uv, half shade, out half3 color, out half alpha) {
  #if !defined(_BILINEAR)
    half index = SAMPLE_TEXTURE2D(indexTexture, indexTexture.samplerstate, uv).r;
    half4 result = clut(CLUT, index, shade);
  #else
    half4 texelSize = indexTexture.texelSize;

    uv -= texelSize.xy / 2.0;
                
    half tli = SAMPLE_TEXTURE2D(indexTexture, indexTexture.samplerstate, uv).r;
    half tri = SAMPLE_TEXTURE2D(indexTexture, indexTexture.samplerstate, uv + half2(texelSize.x, 0.0)).r;
    half bli = SAMPLE_TEXTURE2D(indexTexture, indexTexture.samplerstate, uv + half2(0.0, texelSize.y)).r;
    half bri = SAMPLE_TEXTURE2D(indexTexture, indexTexture.samplerstate, uv + texelSize.xy).r;

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

#endif //GRAPH_UTILS_INCLUDED