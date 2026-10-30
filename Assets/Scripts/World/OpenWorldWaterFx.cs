using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Water contact effects. The effect kits have no water splash (the old stand-in was the ice-shard hit,
 // which read as white sparkles under the surface), so these are drawn here:
 //  - ripple rings that lie flat on the surface and spread out as they fade,
 //  - droplets thrown up that fall back and vanish exactly as they reach the surface again,
 //  - a short puff of spray for bigger splashes.
 // Everything starts on the water surface, so nothing shows below the waterline. Three pooled particle
 // systems serve the whole realm.
 public sealed class OpenWorldWaterFx : MonoBehaviour {
  static OpenWorldWaterFx instance;
  ParticleSystem rings, drops, spray;
  static Texture2D ringTex, dotTex;
  const float Gravity = 9.81f;

  static OpenWorldWaterFx Ensure() {
   if (instance) return instance;
   var gm = OpenWorldManager.I;
   var go = new GameObject("OpenWorldWaterFx");
   if (gm && gm.WorldRoot) go.transform.SetParent(gm.WorldRoot, false);
   instance = go.AddComponent<OpenWorldWaterFx>();
   instance.Build();
   return instance;
  }

  void OnDestroy() { if (instance == this) instance = null; }

  // The water surface under or around pos (pos may be a foot or hand below it). False on dry land.
  public static bool SurfaceAt(Vector3 pos, out Vector3 surfacePoint) {
   surfacePoint = pos;
   if (!OpenWorldRealm.Instance || !OpenWorldTerrainSculptor.WaterAt(pos.x, pos.z, out float y, out _)) return false;
   if (pos.y > y + 0.6f) return false;
   surfacePoint = new Vector3(pos.x, y, pos.z);
   return true;
  }

  // One ring spreading from a point on the surface; size is its final diameter in metres.
  public static void Ripple(Vector3 surfacePoint, float size = 1.2f, float strength = 1f) {
   var fx = Ensure();
   if (!fx.rings || Far(surfacePoint)) return;
   var ep = new ParticleSystem.EmitParams {
    position = surfacePoint + Vector3.up * 0.03f,
    startSize = Mathf.Max(0.2f, size),
    startLifetime = Mathf.Lerp(0.85f, 1.7f, Mathf.Clamp01(size / 4.5f)),
    startColor = Lit(new Color(0.94f, 0.98f, 1f, Mathf.Clamp01(0.5f * strength))),
    rotation = Random.Range(0f, 360f)
   };
   fx.rings.Emit(ep, 1);
  }

  // A splash where something meets the water: about 0.3 for a footstep or a swimming stroke, 1 for a
  // body dropping in, 2-3 for a heavy fall.
  public static void Splash(Vector3 surfacePoint, float strength) {
   var fx = Ensure();
   if (!fx.drops || Far(surfacePoint)) return;
   float s = Mathf.Clamp(strength, 0.1f, 3f);
   float k = Mathf.InverseLerp(0.1f, 2.5f, s);
   Ripple(surfacePoint, 0.8f + 1.5f * s, 1f);
   if (s > 0.55f) Ripple(surfacePoint, 0.5f + 0.9f * s, 0.75f);
   float tierScale = OpenWorldGraphics.Instance && (int)OpenWorldGraphics.Instance.Current == 0 ? 0.5f : 1f;
   int n = Mathf.RoundToInt(Mathf.Lerp(5f, 46f, k) * tierScale);
   var ep = new ParticleSystem.EmitParams();
   for (int i = 0; i < n; i++) {
    Vector2 d = Random.insideUnitCircle;
    // A heavy entry throws a tall central column as well as the crown of droplets around it.
    bool column = s > 1.2f && i < n / 3;
    float up = column ? Random.Range(3.2f, 5.6f) * Mathf.Lerp(0.8f, 1.15f, k) : Random.Range(1.3f, 2.5f) * Mathf.Lerp(0.75f, 1.75f, k);
    if (column) d *= 0.25f;
    float spread = Random.Range(0.25f, 1.15f) * Mathf.Lerp(0.6f, 1.5f, k);
    ep.position = surfacePoint + new Vector3(d.x, 0f, d.y) * (0.12f + 0.2f * s) + Vector3.up * 0.02f;
    ep.velocity = new Vector3(d.x * spread, up, d.y * spread);
    // Gone the moment it falls back to the surface it left.
    ep.startLifetime = 2f * up / Gravity * 0.97f;
    ep.startSize = Random.Range(0.025f, 0.06f) * Mathf.Lerp(0.85f, 2f, k);
    ep.startColor = Lit(new Color(0.88f, 0.95f, 1f, 0.9f));
    fx.drops.Emit(ep, 1);
   }
   int m = s < 0.25f ? 0 : Mathf.RoundToInt((2f + 9f * k) * tierScale);
   for (int i = 0; i < m; i++) {
    Vector2 d = Random.insideUnitCircle * (0.1f + 0.25f * s);
    ep.position = surfacePoint + new Vector3(d.x, 0.08f, d.y);
    bool plume = s > 1.2f && i < m / 2; // spray rising with the column
    ep.velocity = new Vector3(d.x * 0.8f, (plume ? Random.Range(2.6f, 4.2f) : Random.Range(0.5f, 1.3f)) * Mathf.Lerp(0.7f, 1.6f, k), d.y * 0.8f);
    ep.startLifetime = Random.Range(0.45f, 0.75f);
    ep.startSize = Random.Range(0.35f, 0.7f) * Mathf.Lerp(0.6f, 2.2f, k);
    ep.startColor = Lit(new Color(0.92f, 0.97f, 1f, Mathf.Lerp(0.3f, 0.5f, k)));
    fx.spray.Emit(ep, 1);
   }
  }

  // A tiny splash where a rain-wet foot lands on dry ground; OpenWorldSoundscape calls this while it
  // is raining. The wading splash above only fires on actual water.
  public static void RainSplash(Vector3 pos) {
   var fx = Ensure();
   if (!fx.drops || Far(pos)) return;
   var ep = new ParticleSystem.EmitParams();
   for (int i = 0; i < 3; i++) {
    Vector2 d = Random.insideUnitCircle * 0.12f;
    ep.position = pos + new Vector3(d.x, 0.03f, d.y);
    ep.velocity = new Vector3(d.x * 2f, Random.Range(0.7f, 1.3f), d.y * 2f);
    ep.startLifetime = 0.4f;
    ep.startSize = Random.Range(0.02f, 0.045f);
    ep.startColor = Lit(new Color(0.88f, 0.95f, 1f, 0.75f));
    fx.drops.Emit(ep, 1);
   }
  }

  // Splash at a foot, hand or body, snapped to the surface; nothing on dry land.
  public static bool SplashAt(Vector3 pos, float strength, bool ripplesOnly = false) {
   if (!SurfaceAt(pos, out var p)) return false;
   if (ripplesOnly) Ripple(p, 0.9f + strength * 1.2f, 0.85f);
   else Splash(p, strength);
   return true;
  }

  static bool Far(Vector3 p) {
   var cam = Camera.main;
   return cam && (cam.transform.position - p).sqrMagnitude > 70f * 70f;
  }

  // Unlit particles would glow at night; follow the day light.
  static Color Lit(Color c) {
   float night = OpenWorldAtmosphere.Instance ? OpenWorldAtmosphere.Instance.NightFactor : 0f;
   float k = Mathf.Lerp(1f, 0.3f, night);
   return new Color(c.r * k, c.g * k, c.b * k, c.a);
  }

  // ---------- setup ----------
  void Build() {
   if (!ringTex) ringTex = RingTexture(128);
   if (!dotTex) dotTex = DotTexture(64);

   rings = NewSystem("Ripples", 96, Material("OW Water Ripple", ringTex), ParticleSystemRenderMode.HorizontalBillboard, 0f);
   var size = rings.sizeOverLifetime; size.enabled = true;
   size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.18f, 2.6f, 2.6f), new Keyframe(1f, 1f, 0.2f, 0.2f)));
   Fade(rings, 0.07f, 0.45f);

   drops = NewSystem("Droplets", 320, Material("OW Water Droplet", dotTex), ParticleSystemRenderMode.Stretch, 1f);
   var dr = drops.GetComponent<ParticleSystemRenderer>();
   dr.velocityScale = 0.045f; dr.lengthScale = 1.6f;
   Fade(drops, 0f, 0.85f);

   spray = NewSystem("Spray", 64, Material("OW Water Spray", dotTex), ParticleSystemRenderMode.Billboard, 0.35f);
   var grow = spray.sizeOverLifetime; grow.enabled = true;
   grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.35f)));
   var drag = spray.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 3f; drag.limit = 50f;
   Fade(spray, 0.12f, 0.3f);
  }

  ParticleSystem NewSystem(string name, int max, Material mat, ParticleSystemRenderMode mode, float gravity) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   var ps = go.AddComponent<ParticleSystem>();
   ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
   var main = ps.main;
   main.loop = true; main.playOnAwake = false; main.maxParticles = max;
   main.simulationSpace = ParticleSystemSimulationSpace.World;
   main.startSpeed = 0f; main.startLifetime = 1f; main.startSize = 1f;
   main.gravityModifier = gravity;
   var em = ps.emission; em.rateOverTime = 0f;
   var shape = ps.shape; shape.enabled = false;
   var r = go.GetComponent<ParticleSystemRenderer>();
   r.renderMode = mode;
   r.sharedMaterial = mat;
   r.shadowCastingMode = ShadowCastingMode.Off;
   r.receiveShadows = false;
   ps.Play();
   return ps;
  }

  // Alpha rises over the first fadeIn of the life, holds, then falls to zero from fadeOutFrom.
  static void Fade(ParticleSystem ps, float fadeIn, float fadeOutFrom) {
   var col = ps.colorOverLifetime; col.enabled = true;
   var g = new Gradient();
   var alpha = fadeIn > 0f
    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, Mathf.Max(fadeIn + 0.01f, fadeOutFrom)), new GradientAlphaKey(0f, 1f) }
    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeOutFrom), new GradientAlphaKey(0.35f, 1f) };
   g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
   col.color = new ParticleSystem.MinMaxGradient(g);
  }

  static Material Material(string name, Texture2D tex) {
   var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
   if (!shader) return new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = tex };
   var mat = new Material(shader) { name = name };
   mat.SetTexture("_BaseMap", tex);
   mat.SetFloat("_Surface", 1f);
   mat.SetFloat("_Blend", 0f);
   mat.SetOverrideTag("RenderType", "Transparent");
   mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
   mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
   mat.SetInt("_ZWrite", 0);
   mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   mat.renderQueue = (int)RenderQueue.Transparent; // after the water surface (Transparent-50)
   return mat;
  }

  // A crisp outer ring with a fainter inner one, like the double ring a stroke leaves.
  static Texture2D RingTexture(int size) {
   var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_RippleRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
   var px = new Color32[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++) {
     float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
     float outer = Mathf.Exp(-Sq((r - 0.84f) / 0.045f));
     float inner = 0.3f * Mathf.Exp(-Sq((r - 0.66f) / 0.035f));
     float fill = r < 0.84f ? 0.04f * (r / 0.84f) : 0f;
     float a = Mathf.Clamp01(outer + inner + fill) * Mathf.Clamp01((1f - r) * 12f);
     px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
    }
   tex.SetPixels32(px);
   tex.Apply(false, true);
   return tex;
  }

  static Texture2D DotTexture(int size) {
   var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_WaterDot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
   var px = new Color32[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++) {
     float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
     float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
     px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
    }
   tex.SetPixels32(px);
   tex.Apply(false, true);
   return tex;
  }

  static float Sq(float v) => v * v;
 }
}
