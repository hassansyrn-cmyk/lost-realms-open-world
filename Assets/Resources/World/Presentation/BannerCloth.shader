Shader "LostRealms/BannerCloth" {
 Properties {
  [MainTexture] _BaseMap("Original banner atlas",2D)="white"{}
  _BumpMap("Normal map",2D)="bump"{}
  _Captured("Captured",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;half _Captured;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;};
   Varyings Vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.tangentWS=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings v,half facing:VFACE):SV_Target {
    half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb;
    // Red-dominant textile dye only. Ochre/gold, blue jewels, stone and bronze stay untouched.
    half dominance=(original.r-max(original.g,original.b))/max(original.r,.001h);
    half goldProtection=smoothstep(1.5h,2.0h,original.g/max(original.b,.001h));
    half cloth=smoothstep(.52h,.70h,dominance)*(1-goldProtection);
    half3 emerald=half3(original.g*.85h,original.r*.85h,original.b+original.r*.12h);
    SurfaceData surface=(SurfaceData)0;surface.albedo=lerp(original,emerald,cloth*_Captured);surface.alpha=1;surface.occlusion=1;
    surface.metallic=(1-cloth)*.25h;surface.smoothness=lerp(.35h,.16h,cloth);
    half3 n=normalize(v.normalWS)*(facing>=0?1:-1);half3 t=normalize(v.tangentWS.xyz);half3 b=cross(n,t)*v.tangentWS.w;
    half3 normalTS=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,v.uv));
    InputData input=(InputData)0;input.positionWS=v.positionWS;input.normalWS=NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,half3x3(t,b,n)));input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);input.shadowCoord=TransformWorldToShadowCoord(v.positionWS);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);input.shadowMask=half4(1,1,1,1);
    half4 color=UniversalFragmentPBR(input,surface);color.rgb=MixFog(color.rgb,v.fog);return color;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
