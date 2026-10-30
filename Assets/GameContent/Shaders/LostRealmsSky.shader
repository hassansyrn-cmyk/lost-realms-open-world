// Lost Realms stylised sky: zenith/horizon gradient that matches the distance fog, a soft sun disc
// with forward-scatter glow, drifting high cirrus, a moon and twinkling stars for the night presets.
// All colours are driven by OpenWorldAtmosphere; the sun and moon directions come from script so the
// sky agrees with the directional light on every renderer path.
Shader "LostRealms/Sky" {
 Properties {
  _ZenithColor ("Zenith", Color) = (0.22, 0.45, 0.82, 1)
  _HorizonColor ("Horizon", Color) = (0.68, 0.80, 0.88, 1)
  _GroundColor ("Below Horizon", Color) = (0.36, 0.40, 0.38, 1)
  _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.6
  _HorizonLift ("Horizon Lift", Range(-0.2, 0.3)) = 0.02
  _SunColor ("Sun Colour", Color) = (1, 0.92, 0.78, 1)
  _SunSize ("Sun Size", Range(0.005, 0.12)) = 0.035
  _SunGlow ("Sun Glow", Range(0, 4)) = 1.1
  _SunGlowPower ("Sun Glow Power", Range(1, 64)) = 7
  _SunHalo ("Sun Halo", Range(0, 2)) = 0.4
  _MoonColor ("Moon Colour", Color) = (0.78, 0.86, 1, 1)
  _MoonSize ("Moon Size", Range(0.005, 0.1)) = 0.028
  _StarIntensity ("Stars", Range(0, 4)) = 0
  _StarDensity ("Star Density", Range(50, 600)) = 280
  _CloudColor ("Cloud Lit", Color) = (1, 0.98, 0.95, 1)
  _CloudShade ("Cloud Shade", Color) = (0.62, 0.70, 0.80, 1)
  _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.55
  _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.52
  _CloudSoftness ("Cloud Softness", Range(0.01, 0.6)) = 0.24
  _CloudScale ("Cloud Scale", Range(0.05, 3)) = 0.55
  _CloudSpeed ("Cloud Speed", Vector) = (0.004, 0.0015, 0, 0)
  _Exposure ("Exposure", Range(0.1, 3)) = 1
 }
 SubShader {
  Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

   CBUFFER_START(UnityPerMaterial)
   half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _MoonColor, _CloudColor, _CloudShade;
   float _HorizonSharpness, _HorizonLift, _SunSize, _SunGlow, _SunGlowPower, _SunHalo, _MoonSize;
   float _StarIntensity, _StarDensity, _CloudOpacity, _CloudCoverage, _CloudSoftness, _CloudScale, _Exposure;
   float4 _CloudSpeed;
   CBUFFER_END
   float4 _LR_SunDir;   // xyz: direction towards the sun
   float4 _LR_MoonDir;  // xyz: direction towards the moon

   struct Attributes { float4 positionOS : POSITION; };
   struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

   Varyings vert(Attributes v) {
    Varyings o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.dir = v.positionOS.xyz;
    return o;
   }

   float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
   float hash31(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

   float vnoise(float2 p) {
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
   }

   float fbm(float2 p) {
    float s = 0, a = 0.5;
    [unroll] for (int i = 0; i < 4; i++) { s += vnoise(p) * a; p = p * 2.03 + float2(17.1, 9.7); a *= 0.5; }
    return s / 0.9375;
   }

   half4 frag(Varyings i) : SV_Target {
    float3 dir = normalize(i.dir);
    float3 sunDir = normalize(_LR_SunDir.xyz + float3(0, 1e-4, 0));
    float y = dir.y - _HorizonLift;
    // Sky gradient: horizon band widens towards the sun for a warm forward-scatter look.
    float upper = pow(saturate(y), 1.0 / _HorizonSharpness);
    float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upper);
    col = lerp(col, _GroundColor.rgb, saturate(-y * 5.0));
    float sunDot = dot(dir, sunDir);
    float forward = saturate(sunDot);
    col += _SunColor.rgb * (pow(forward, _SunGlowPower) * _SunGlow + pow(forward, 2.0) * _SunHalo * 0.25) * saturate(sunDir.y + 0.25);
    // High cirrus on a flattened dome.
    if (_CloudOpacity > 0.001 && dir.y > 0.0) {
     float2 uv = dir.xz / (dir.y + 0.12) * _CloudScale + _Time.y * _CloudSpeed.xy;
     float n = fbm(uv);
     float cover = smoothstep(_CloudCoverage, _CloudCoverage + _CloudSoftness, n) * saturate(dir.y * 7.0);
     float lit = saturate(0.45 + 0.55 * forward) * saturate(sunDir.y * 2.0 + 0.6);
     float3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, lit) + _SunColor.rgb * pow(forward, 6.0) * 0.35;
     col = lerp(col, cloud, cover * _CloudOpacity);
    }
    // Sun disc (hidden below the horizon).
    float sunEdge = cos(_SunSize);
    float disc = smoothstep(sunEdge - 0.0009, sunEdge + 0.0004, sunDot) * saturate(dir.y * 30.0 + 0.5);
    col += _SunColor.rgb * disc * 6.0;
    // Night sky.
    if (_StarIntensity > 0.001) {
     float3 moonDir = normalize(_LR_MoonDir.xyz + float3(0, 1e-4, 0));
     float moonDot = dot(dir, moonDir);
     float moonEdge = cos(_MoonSize);
     float moon = smoothstep(moonEdge - 0.0007, moonEdge + 0.0003, moonDot);
     col += _MoonColor.rgb * (moon * 2.2 + pow(saturate(moonDot), 220.0) * 0.6 + pow(saturate(moonDot), 12.0) * 0.08);
     float3 cell = floor(dir * _StarDensity);
     float h = hash31(cell);
     float star = step(0.9965, h);
     float3 local = frac(dir * _StarDensity) - 0.5;
     float point_ = saturate(1.0 - length(local) * 2.6);
     float twinkle = 0.65 + 0.35 * sin(_Time.y * (2.0 + h * 5.0) + h * 61.0);
     col += star * point_ * point_ * twinkle * _StarIntensity * saturate(dir.y * 3.0) * (1.0 - moon);
    }
    return half4(col * _Exposure, 1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
