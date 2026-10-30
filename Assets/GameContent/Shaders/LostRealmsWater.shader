// Lost Realms water: the realm's lakes, tarns and rivers.
//
// Replaces the vendor stylised water, whose bright cyan saturated within centimetres and whose sky
// reflection washed every lake to a milky white. Here the bed shows through clear shallows, light is
// absorbed with depth towards a dark teal, the sky and the far shore are reflected by Fresnel, the sun
// and moon leave a sharp glint, foam gathers along the shore and rivers flow downstream (flow map from
// the river mesh, two phases cross-faded so the ripples never stretch). The hero leaves rings when wading.
//
// _LR_REFRACTION (High/Ultra, needs the camera opaque texture) refracts the scene behind the surface;
// without it the surface is alpha-blended. Both use the camera depth texture for absorption and foam.
// Sky colours come from the atmosphere (_LR_SkyZenith/_LR_SkyHorizon), so reflections follow the
// time of day and weather; the ambient probe is the fallback before the atmosphere drives them.
Shader "LostRealms/Water" {
 Properties {
  _Shallow_Color ("Shallow Colour (a: bed tint)", Color) = (0.24, 0.52, 0.5, 0.55)
  _Deep_Color ("Deep Colour (a: max opacity)", Color) = (0.03, 0.16, 0.21, 0.94)
  _Depth_Distance ("Absorption Depth (m)", Float) = 2.2
  [NoScaleOffset][Normal] _Refraction_Normal ("Ripple Normal Map", 2D) = "bump" {}
  _Ripple_Scale ("Ripple Tiling (per metre)", Float) = 0.22
  _Normal_Strength ("Ripple Strength", Range(0, 2)) = 0.55
  _Water_Speed ("Ripple Speed", Float) = 0.5
  _Refraction_Strength ("Refraction", Range(0, 0.1)) = 0.035
  _Smoothness ("Glint Sharpness", Range(0, 1)) = 0.9
  _Reflection ("Sky Reflection", Range(0, 1)) = 0.6
  _Foam_Color ("Shore Foam Colour", Color) = (0.9, 0.95, 0.94, 0.8)
  _Foam_Amount ("Shore Foam Width (m)", Float) = 0.55
  _Coast_Opacity ("Shore Softness (m)", Float) = 0.22
  _Flow ("River Flow Speed", Float) = 0
 }

 SubShader {
  Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
  LOD 300

  Pass {
   Name "ForwardLit"
   Tags { "LightMode" = "UniversalForward" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Back

   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma shader_feature_local _LR_REFRACTION
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog

   // Transparent surfaces must sample the shadow map directly (the screen-space shadow texture only
   // holds opaque depth).
   #define _SURFACE_TYPE_TRANSPARENT 1

   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #if defined(_LR_REFRACTION)
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #endif

   CBUFFER_START(UnityPerMaterial)
   half4 _Shallow_Color, _Deep_Color, _Foam_Color;
   float _Depth_Distance, _Ripple_Scale, _Normal_Strength, _Water_Speed, _Refraction_Strength;
   float _Smoothness, _Reflection, _Foam_Amount, _Coast_Opacity, _Flow;
   CBUFFER_END

   TEXTURE2D(_Refraction_Normal); SAMPLER(sampler_Refraction_Normal);

   // Set by OpenWorldAtmosphere (a = 1 once driven) and the hero.
   float4 _LR_SkyZenith;
   float4 _LR_SkyHorizon;
   float4 _LR_PlayerPos;

   struct Attributes {
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 flow : TEXCOORD1; // river meshes: downstream direction on the ground plane
    half4 color : COLOR;
   };

   struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float4 data : TEXCOORD1; // xy flow direction, z fog factor, w eye depth
   };

   Varyings Vert(Attributes v) {
    Varyings o = (Varyings)0;
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionWS = positionWS;
    o.positionCS = TransformWorldToHClip(positionWS);
    float2 flow = mul((float3x3)GetObjectToWorldMatrix(), float3(v.flow.x, 0.0, v.flow.y)).xz;
    o.data = float4(flow, ComputeFogFactor(o.positionCS.z), -TransformWorldToView(positionWS).z);
    return o;
   }

   half3 Ripple(float2 uv) {
    return UnpackNormalScale(SAMPLE_TEXTURE2D(_Refraction_Normal, sampler_Refraction_Normal, uv), _Normal_Strength);
   }

   half4 Frag(Varyings i) : SV_Target {
    float3 positionWS = i.positionWS;
    float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
    float t = _Time.y * _Water_Speed;
    float2 uvW = positionWS.xz * _Ripple_Scale;

    // ---- ripples (tangent space of a horizontal plane: x -> world x, y -> world z) ----
    half3 n;
    float2 flowDir = i.data.xy;
    if (_Flow > 0.001 && dot(flowDir, flowDir) > 0.01) {
     float2 f = normalize(flowDir) * _Flow;
     float ph0 = frac(t * 0.25), ph1 = frac(t * 0.25 + 0.5);
     half w = abs(ph0 * 2.0 - 1.0);
     half3 n0 = Ripple(uvW - f * ph0 * 4.0);
     half3 n1 = Ripple(uvW - f * ph1 * 4.0 + 0.5);
     half3 n2 = Ripple(uvW * 2.3 - f * ph0 * 9.0 + 0.27);
     n = lerp(n0, n1, w);
     n = half3(n.xy + n2.xy * 0.35, n.z);
    } else {
     half3 n0 = Ripple(uvW + float2(t * 0.035, t * 0.021));
     half3 n1 = Ripple(uvW * 1.7 + float2(-t * 0.027, t * 0.041) + 0.37);
     n = half3(n0.xy + n1.xy, n0.z * n1.z);
    }
    float3 N = normalize(float3(n.x, max(n.z, 0.2), n.y));

    // Rings around the hero while wading.
    if (_LR_PlayerPos.w > 0.0) {
     float2 dp = positionWS.xz - _LR_PlayerPos.xz;
     float dist = length(dp);
     float wading = step(_LR_PlayerPos.y, positionWS.y + 0.15) * step(positionWS.y - 1.8, _LR_PlayerPos.y);
     float ring = sin(dist * 7.0 - _Time.y * 6.0) * exp(-dist * 0.9) * wading;
     N = normalize(N + float3(dp.x, 0.0, dp.y) / max(dist, 1e-3) * ring * 0.35);
    }

    // ---- depth below the surface along the view ----
    float surfaceEye = i.data.w;
    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
    float thickness = max(0.0, sceneEye - surfaceEye);

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half shadow = mainLight.shadowAttenuation;
    half3 ambient = SampleSH(half3(0.0, 1.0, 0.0));
    half3 bodyLight = ambient + mainLight.color * shadow * saturate(mainLight.direction.y) * 0.6;

    #if defined(_LR_REFRACTION)
    float2 uvR = screenUV + N.xz * _Refraction_Strength * saturate(thickness * 0.5);
    float sceneEyeR = LinearEyeDepth(SampleSceneDepth(uvR), _ZBufferParams);
    if (sceneEyeR < surfaceEye) { uvR = screenUV; sceneEyeR = sceneEye; } // never refract what is above the water
    float depthR = max(0.0, sceneEyeR - surfaceEye);
    #else
    float depthR = thickness;
    #endif

    half absorb = 1.0 - exp(-depthR / max(0.05, _Depth_Distance));
    half3 waterColor = lerp(_Shallow_Color.rgb, _Deep_Color.rgb, absorb) * bodyLight;
    half opacity = lerp(0.0, _Deep_Color.a, absorb);

    #if defined(_LR_REFRACTION)
    half3 bed = SampleSceneColor(uvR) * lerp(half3(1.0, 1.0, 1.0), saturate(_Shallow_Color.rgb * 1.6), _Shallow_Color.a);
    half3 color = lerp(bed, waterColor, opacity);
    half alpha = 1.0;
    #else
    half3 color = waterColor;
    half alpha = max(opacity, 0.35);
    #endif

    // ---- reflection: sky above, the darker far shore near the horizon ----
    float3 R = reflect(-viewDir, N);
    half3 sky = _LR_SkyZenith.a > 0.5 ? lerp(_LR_SkyHorizon.rgb, _LR_SkyZenith.rgb, sqrt(saturate(R.y))) : SampleSH(R) * 1.15;
    sky *= lerp(0.5, 1.0, saturate(R.y * 4.0));
    half NdotV = saturate(dot(N, viewDir));
    half fresnel = (0.02 + 0.98 * pow(1.0 - NdotV, 5.0)) * _Reflection;
    fresnel = min(fresnel, 0.8);
    color = lerp(color, sky, fresnel);
    alpha = max(alpha, fresnel);

    // ---- sun / moon glint ----
    float3 H = normalize(mainLight.direction + viewDir);
    half spec = pow(saturate(dot(N, H)), exp2(_Smoothness * 10.0 + 1.0)) * 4.0 * _Smoothness;
    half3 glint = mainLight.color * spec * shadow;
    color += glint;
    alpha = saturate(alpha + dot(glint, half3(0.3, 0.59, 0.11)));

    // ---- shore foam and soft edge ----
    half foamDepth = saturate(1.0 - thickness / max(0.05, _Foam_Amount));
    half foamNoise = SAMPLE_TEXTURE2D(_Refraction_Normal, sampler_Refraction_Normal, positionWS.xz * 0.6 + t * 0.05).r;
    half band = foamDepth * smoothstep(0.35, 0.75, foamNoise + foamDepth * 0.5 - 0.25);
    color = lerp(color, _Foam_Color.rgb * (ambient + mainLight.color * shadow * 0.8), band * _Foam_Color.a);
    alpha = max(alpha, band * _Foam_Color.a);
    alpha *= saturate(thickness / max(0.01, _Coast_Opacity));

    color = MixFog(color, i.data.z);
    return half4(color, alpha);
   }
   ENDHLSL
  }
 }
 FallBack Off
}
