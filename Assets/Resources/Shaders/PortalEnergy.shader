Shader "LostRealms/PortalEnergy" {
 Properties {
  _Color ("Portal Color", Color) = (0.0, 0.85, 0.55, 1.0)
  _SecondaryColor ("Secondary Portal Color", Color) = (0.1, 0.95, 0.85, 1.0)
  _Speed ("Animation Speed", Float) = 1.0
  _Distort ("Distortion Strength", Range(0.0, 1.0)) = 0.3
  _Opacity ("Opacity", Range(0.0, 1.0)) = 0.85
  _Softness ("Edge Softness", Range(0.01, 0.8)) = 0.3
  _Emission ("Emission Intensity", Float) = 2.0
  _Rotation ("Rotation Speed", Float) = 1.5
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off
  ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha

  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"

   fixed4 _Color;
   fixed4 _SecondaryColor;
   float _Speed;
   float _Distort;
   float _Opacity;
   float _Softness;
   float _Emission;
   float _Rotation;

   struct appdata_t {
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
   };

   struct v2f {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
   };

   v2f vert(appdata_t v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
   }

   fixed4 frag(v2f i) : SV_Target {
    float2 c = i.uv - 0.5;
    float dist = length(c) * 2.0;

    float alphaMask = smoothstep(1.0, 1.0 - _Softness, dist);
    if (alphaMask <= 0.001) discard;

    float angle = atan2(c.y, c.x);
    float time = _Time.y * _Speed;

    float rot1 = angle + time * _Rotation + dist * 3.14159;
    float rot2 = angle - time * (_Rotation * 0.7) - dist * 4.5;

    float wave1 = sin(rot1 * 3.0 + dist * 8.0 - time * 2.0);
    float wave2 = cos(rot2 * 4.0 - dist * 6.0 + time * 1.5);
    float wave = (wave1 + wave2) * 0.5;

    float ripple = sin(dist * 12.0 - time * 3.0) * _Distort;
    float pattern = saturate(wave * 0.5 + 0.5 + ripple);

    float core = saturate(1.0 - dist * 1.2);
    fixed4 col = lerp(_Color, _SecondaryColor, pattern);
    col.rgb += fixed3(1.0, 1.0, 1.0) * (core * 0.6);

    col.rgb *= _Emission;
    col.a = pattern * _Opacity * alphaMask;

    return col;
   }
   ENDCG
  }
 }
}
