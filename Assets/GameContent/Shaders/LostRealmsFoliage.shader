// Lost Realms foliage: grass, flowers, bushes and tree leaves for the realm.
//
// Replaces the Idyllic vegetation look, whose colour was emitted through a fixed ramp and so ignored
// the sun and moon (grass glowed at night, never warmed at golden hour). Here the same painted
// textures and bottom-to-top tints are lit properly: wrapped diffuse from the main light with soft
// shadows, light shining through leaves when looking towards the sun, spherical-harmonic ambient with
// base occlusion, additional lights (lanterns, fires), SSAO and fog.
//
// Property names match the Idyllic vegetation graph so the World Kit converter can copy a vendor
// material's values straight across. Wind follows the atmosphere (_LR_Wind) and the hero parts the
// grass (_LR_PlayerPos). Foliage within arm's reach of the camera dissolves so it never blocks the view.
Shader "LostRealms/Foliage" {
 Properties {
  [MainTexture] _Texture ("Texture", 2D) = "white" {}
  [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
  [ToggleUI] _Custom_Color ("Use Gradient Tint", Float) = 1
  [ToggleUI] _Enable_Top_Color ("Enable Top Colour", Float) = 1
  _Top_Color ("Top Colour", Color) = (1, 1, 1, 1)
  _Bottom_Color ("Bottom Colour", Color) = (1, 1, 1, 1)
  _Blend_Height ("Gradient Height", Float) = 0.3
  _COLOR_OVERLAY ("Gradient Source (1 = UV, 0 = object height)", Float) = 1
  _Alpha_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
  _Translucency ("Translucency", Range(0, 2)) = 0.55
  _NormalUp ("Normal Up Bias", Range(0, 1)) = 0.55
  _Wrap ("Light Wrap", Range(0, 1)) = 0.5
  _AO ("Base Occlusion", Range(0, 1)) = 0.3
  _Wind_Strength ("Wind Strength", Range(0, 2)) = 0.2
  _Wind_Speed ("Wind Speed", Range(0, 4)) = 1
  _Wind_Variation ("Wind Flutter", Range(0, 1)) = 0.2
  _Wave_Scale ("Wave Scale", Range(0, 2)) = 1
  _Wind_Direction ("Wind Direction", Vector) = (0.1, 0.1, 0, 0)
  _Start_Bend_Range ("Bend Start", Range(0, 1)) = 0.3
  _Bend_Strength ("Player Push", Range(0, 1)) = 0.25
  [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
 }

 SubShader {
  Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
  LOD 300

  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

  CBUFFER_START(UnityPerMaterial)
  float4 _Texture_ST;
  half4 _BaseColor, _Top_Color, _Bottom_Color;
  float4 _Wind_Direction;
  half _Custom_Color, _Enable_Top_Color, _Blend_Height, _COLOR_OVERLAY, _Alpha_Cutoff;
  half _Translucency, _NormalUp, _Wrap, _AO;
  half _Wind_Strength, _Wind_Speed, _Wind_Variation, _Wave_Scale, _Start_Bend_Range, _Bend_Strength, _Cull;
  CBUFFER_END

  TEXTURE2D(_Texture); SAMPLER(sampler_Texture);

  // Set by OpenWorldAtmosphere / OpenWorldHero (zero until then: material wind, no push).
  float4 _LR_Wind;       // xy: wind direction on the ground plane, z: strength multiplier, w: 1 when driven
  float4 _LR_PlayerPos;  // xyz: hero feet, w: push radius (0 disables)
  float4 _LR_Clearings[4]; // xyz ground centre, w radius; only authored trail clearings
  float _LR_ClearingCount;

  float2 LRHash22(float2 p) {
   p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
   return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
  }

  // Gradient noise, roughly -0.7..0.7.
  float LRNoise(float2 p) {
   float2 i = floor(p);
   float2 f = frac(p);
   float2 u = f * f * (3.0 - 2.0 * f);
   float a = dot(LRHash22(i), f);
   float b = dot(LRHash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
   float c = dot(LRHash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
   float d = dot(LRHash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));
   return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
  }

  // World position after wind sway, leaf flutter and the hero pushing through. The sway grows with
  // the vertex's elevation above the pivot (0 at the base ring, 1 straight up), masked by vertex red.
  float3 LRWindWS(float3 positionOS, half4 color) {
   float3 positionWS = TransformObjectToWorld(positionOS);
   float2 dir = _Wind_Direction.xy;
   float strength = _Wind_Strength;
   if (_LR_Wind.w > 0.5) { dir = _LR_Wind.xy; strength *= _LR_Wind.z; }
   dir = normalize(dir + float2(1e-4, 2e-4));
   float lift = saturate(normalize(positionOS + float3(0.0, 1e-4, 0.0)).y);
   lift = lift * lift * (3.0 - 2.0 * lift);
   float t = _Time.y * _Wind_Speed;
   float wave = LRNoise(positionWS.xz * (0.3 * _Wave_Scale + 0.02) - dir * t * 0.8);
   float gust = 0.5 + 0.5 * sin(_Time.y * 0.7 + dot(positionWS.xz, dir) * 0.045);
   float sway = (wave * 0.8 + gust * 0.35) * strength;
   float flutter = LRNoise(positionWS.xz * 2.7 + t * 3.1) * (_Wind_Variation * 0.25 + 0.05) * strength;
   float3 offset = float3(dir.x, 0.0, dir.y) * (sway + flutter) * lift;
   offset.y -= abs(sway) * lift * 0.25;
   if (_LR_PlayerPos.w > 0.0) {
    float2 d = positionWS.xz - _LR_PlayerPos.xz;
    float dist = length(d);
    float k = saturate(1.0 - dist / _LR_PlayerPos.w) * saturate(1.5 - abs(positionWS.y - _LR_PlayerPos.y) * 0.5);
    float2 away = d / max(dist, 1e-3);
    offset.xz += away * k * _Bend_Strength * 1.6 * lift;
    offset.y -= k * _Bend_Strength * 0.6 * lift;
   }
   float3 result = positionWS + offset * color.r;
   // Trampled low growth in local clearings; upper branches and every other location keep their wind.
   [unroll] for (int ci = 0; ci < 4; ++ci) {
    if (ci >= _LR_ClearingCount) break;
    float4 clearing = _LR_Clearings[ci];
    float2 delta = result.xz - clearing.xz;
    float outer = clearing.w * clearing.w;
    float inner = max(0.0, clearing.w - 0.65);
    float flatten = saturate((outer - dot(delta, delta)) / max(0.01, outer - inner * inner));
    flatten *= saturate((clearing.y + 1.45 - result.y) * 4.0);
    float ground = min(clearing.y - 0.08, TransformObjectToWorld(float3(0,0,0)).y - 0.055);
    result.y = lerp(result.y, min(result.y, ground), flatten);
   }
   return result;
  }

  // 0 at the base of the plant, 1 at the top (the vendor gradient source and scale).
  half LRGradient(float3 positionOS, float2 uv) {
   float g = _COLOR_OVERLAY > 0.5 ? uv.y * _Blend_Height * 5.0 : positionOS.y * _Blend_Height * 2.0;
   return saturate(g);
  }

  half3 LRTint(half gradient) {
   if (_Custom_Color < 0.5) return half3(1.0, 1.0, 1.0);
   half3 top = _Enable_Top_Color > 0.5 ? _Top_Color.rgb : _Bottom_Color.rgb;
   return lerp(_Bottom_Color.rgb, top, gradient);
  }

  // Leaves and blades right in front of the camera dissolve with a screen-door dither, so bushes and
  // canopies never block the view when the follow camera passes through them.
  void LRCameraFade(float3 positionWS, float4 positionCS) {
   float d = distance(positionWS, GetCameraPositionWS());
   float fade = saturate((d - 0.5) / 1.2);
   float dither = frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
   clip(fade - dither * 0.999 - 0.0005);
  }

  // Per-instance brightness variation so neighbouring clumps and trees never look stamped.
  half LRVariation() {
   float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
   return lerp(0.86, 1.08, frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453));
  }
  ENDHLSL

  Pass {
   Name "ForwardLit"
   Tags { "LightMode" = "UniversalForward" }
   Cull [_Cull]
   ZWrite On

   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

   struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 data : TEXCOORD3; // x gradient, y variation, z fog
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
   };

   Varyings Vert(Attributes v) {
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS = LRWindWS(v.positionOS.xyz, v.color);
    o.positionWS = positionWS;
    o.positionCS = TransformWorldToHClip(positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _Texture);
    o.data = half4(LRGradient(v.positionOS.xyz, v.uv), LRVariation(), ComputeFogFactor(o.positionCS.z), 0.0);
    return o;
   }

   half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv);
    clip(tex.a * _BaseColor.a - _Alpha_Cutoff);
    LRCameraFade(i.positionWS, i.positionCS);
    half gradient = i.data.x;
    half3 albedo = tex.rgb * LRTint(gradient) * _BaseColor.rgb * i.data.y;

    half3 n = normalize(i.normalWS);
    n = IS_FRONT_VFACE(face, n, -n);
    n = normalize(lerp(n, half3(0.0, 1.0, 0.0), _NormalUp));
    half3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);
    Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
    half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    half wrapped = saturate((dot(n, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
    half through = pow(saturate(dot(viewWS, -mainLight.direction)), 5.0) * _Translucency;
    half3 direct = mainLight.color * atten * (wrapped + through * 0.6);

    half occlusion = lerp(1.0 - _AO, 1.0, gradient);
    #if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
    occlusion *= ao.indirectAmbientOcclusion;
    direct *= ao.directAmbientOcclusion;
    #endif
    half3 ambient = SampleSH(n) * occlusion;

    #if defined(_ADDITIONAL_LIGHTS)
    uint lightCount = GetAdditionalLightsCount();
    for (uint li = 0u; li < lightCount; ++li) {
     Light light = GetAdditionalLight(li, i.positionWS, shadowMask);
     half nl = saturate((dot(n, light.direction) + _Wrap) / (1.0 + _Wrap));
     direct += light.color * (light.distanceAttenuation * light.shadowAttenuation) * nl;
    }
    #endif

    half3 color = albedo * (direct + ambient);
    color = MixFog(color, i.data.z);
    return half4(color, 1.0);
   }
   ENDHLSL
  }

  Pass {
   Name "ShadowCaster"
   Tags { "LightMode" = "ShadowCaster" }
   ZWrite On
   ZTest LEqual
   ColorMask 0
   Cull [_Cull]

   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

   float3 _LightDirection;
   float3 _LightPosition;

   struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   Varyings Vert(Attributes v) {
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    float3 positionWS = LRWindWS(v.positionOS.xyz, v.color);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    o.positionCS = positionCS;
    o.uv = TRANSFORM_TEX(v.uv, _Texture);
    return o;
   }

   half4 Frag(Varyings i) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    clip(SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).a * _BaseColor.a - _Alpha_Cutoff);
    return 0;
   }
   ENDHLSL
  }

  Pass {
   Name "DepthOnly"
   Tags { "LightMode" = "DepthOnly" }
   ZWrite On
   ColorMask R
   Cull [_Cull]

   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing

   struct Attributes {
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
   };

   Varyings Vert(Attributes v) {
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS = LRWindWS(v.positionOS.xyz, v.color);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.uv = TRANSFORM_TEX(v.uv, _Texture);
    return o;
   }

   half4 Frag(Varyings i) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    clip(SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).a * _BaseColor.a - _Alpha_Cutoff);
    LRCameraFade(i.positionWS, i.positionCS);
    return i.positionCS.z;
   }
   ENDHLSL
  }

  Pass {
   Name "DepthNormals"
   Tags { "LightMode" = "DepthNormals" }
   ZWrite On
   Cull [_Cull]

   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing

   struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
   };

   Varyings Vert(Attributes v) {
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS = LRWindWS(v.positionOS.xyz, v.color);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _Texture);
    return o;
   }

   half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    clip(SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).a * _BaseColor.a - _Alpha_Cutoff);
    LRCameraFade(i.positionWS, i.positionCS);
    half3 n = normalize(i.normalWS);
    n = IS_FRONT_VFACE(face, n, -n);
    n = normalize(lerp(n, half3(0.0, 1.0, 0.0), _NormalUp));
    return half4(n, 0.0);
   }
   ENDHLSL
  }
 }
 FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
