// Dash afterimage: an additive, view-dependent silhouette (bright rim, faint body) that fades out.
// Lives under Resources so player builds always include it.
Shader "LostRealms/Afterimage" {
 Properties {
  _Color ("Tint", Color) = (0.45, 0.9, 1, 1)
  _Fade ("Fade", Range(0, 1)) = 1
  _RimPower ("Rim Power", Float) = 2.2
  _Body ("Body Glow", Range(0, 1)) = 0.16
 }
 SubShader {
  Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
  Pass {
   Name "Afterimage"
   Tags { "LightMode" = "UniversalForward" }
   Blend One One
   ZWrite Off
   Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _Fade;
    half _RimPower;
    half _Body;
   CBUFFER_END
   struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
   struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
   Varyings vert (Attributes i) {
    Varyings o;
    VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
    o.positionCS = p.positionCS;
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.viewWS = GetWorldSpaceViewDir(p.positionWS);
    return o;
   }
   half4 frag (Varyings i) : SV_Target {
    half rim = pow(1.0h - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), _RimPower);
    half a = (_Body + rim) * _Fade;
    return half4(_Color.rgb * a, a);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
