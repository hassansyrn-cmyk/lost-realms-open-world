using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Ambient life (GLM Phase 3): pooled procedural particles that make the realm feel inhabited —
 // butterflies over meadows by day, fireflies at dusk near water and forest, dragonflies over ponds,
 // fish jumping in lakes, falling leaves in the woods, dust devils in the Dunes and spindrift off the
 // snowy ridges. Everything is emitted into a handful of shared world-space systems on a slow cadence,
 // tier-scaled for Balanced and culled by camera distance, so the cost stays inside the Phase 1 budget.
 public sealed class OpenWorldAmbientLife : MonoBehaviour {
  static OpenWorldAmbientLife instance;
  ParticleSystem flutter, glow, darts, leaves, dust, snow;
  Texture2D dotTex, leafTex;
  float nextCadence, nextFish;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldAmbientLife>()) new GameObject("Ambient life").AddComponent<OpenWorldAmbientLife>(); }

  static OpenWorldAmbientLife Ensure() {
   if (instance) return instance;
   var gm = OpenWorldManager.I;
   var go = new GameObject("OpenWorldAmbientLife");
   if (gm && gm.WorldRoot) go.transform.SetParent(gm.WorldRoot, false);
   instance = go.AddComponent<OpenWorldAmbientLife>();
   instance.Build();
   return instance;
  }
  void OnDestroy() { if (instance == this) instance = null; }

  void Update() {
   if (!OpenWorldManager.I || !OpenWorldManager.I.Hero) return;
   if (Time.unscaledTime < nextCadence) return;
   nextCadence = Time.unscaledTime + 2f;
   Ensure();
   var atm = OpenWorldAtmosphere.Instance;
   if (!atm) return;
   bool day = atm.NightFactor < 0.5f;
   float tier = OpenWorldGraphics.Instance && (int)OpenWorldGraphics.Instance.Current == 0 ? 0.55f : 1f;
   var hero = OpenWorldManager.I.Hero.transform.position;
   var biome = atm.LocalBiome;

   // Fish jump in the nearest lake: the splash reads as the jump.
   if (Time.unscaledTime > nextFish && Random.value < 0.5f) {
    var lake = NearestLake(hero);
    if (lake.HasValue) {
     Vector2 r = Random.insideUnitCircle * lake.Value.w;
     var p = new Vector3(lake.Value.x + r.x, 0f, lake.Value.z + r.y);
     p.y = OpenWorldMaterialHelper.SampleGroundHeight(p) + 0.4f;
     OpenWorldWaterFx.SplashAt(p, 0.8f);
    }
    nextFish = Time.unscaledTime + Random.Range(6f, 16f);
   }

   if (biome == OWBiome.Verdant) {
    if (day) {
     Burst(flutter, hero, 18f, 34f, Mathf.RoundToInt(5 * tier), new Color(1f, 0.96f, 0.75f), new Color(1f, 0.72f, 0.4f), 0.1f, 3.4f, 1.6f);
     Burst(leaves, hero, 10f, 26f, Mathf.RoundToInt(3 * tier), new Color(0.55f, 0.74f, 0.3f), new Color(0.78f, 0.62f, 0.25f), 0.14f, 5.2f, 0.4f);
     if (Random.value < 0.4f) Burst(darts, hero, 8f, 22f, Mathf.RoundToInt(3 * tier), new Color(0.5f, 0.85f, 1f), new Color(0.6f, 0.9f, 1f), 0.09f, 1.6f, 2.2f);
    }
    if (!day && Random.value < 0.65f) Burst(glow, hero, 10f, 30f, Mathf.RoundToInt(7 * tier), new Color(0.72f, 1f, 0.8f), new Color(0.5f, 1f, 0.72f), 0.07f, 5.5f, 0.25f);
   } else if (biome == OWBiome.Graveyard && !day) {
    Burst(glow, hero, 12f, 32f, Mathf.RoundToInt(5 * tier), new Color(0.6f, 0.9f, 1f), new Color(0.75f, 0.85f, 1f), 0.06f, 4.8f, 0.3f);
   } else if (biome == OWBiome.Dunes && day) {
    Burst(dust, hero, 14f, 38f, Mathf.RoundToInt(10 * tier), new Color(0.85f, 0.7f, 0.45f), new Color(0.8f, 0.62f, 0.38f), 0.5f, 3.2f, 0.5f);
   } else if (biome == OWBiome.Frozen) {
    Burst(snow, hero, 12f, 36f, Mathf.RoundToInt(8 * tier), new Color(0.95f, 0.97f, 1f), new Color(0.88f, 0.93f, 1f), 0.4f, 2.6f, 0.7f);
   }
  }

  // One burst of life at a ground point in the ring around the hero. Velocity is a gentle random
  // drift; the particle curves do the character (flutter, glow pulse, tumble).
  void Burst(ParticleSystem ps, Vector3 hero, float minR, float maxR, int count, Color a, Color b, float size, float life, float speed) {
   if (!ps || count <= 0) return;
   Vector2 d = Random.insideUnitCircle.normalized * Random.Range(minR, maxR);
   var p = hero + new Vector3(d.x, 0f, d.y);
   p.y = OpenWorldMaterialHelper.SampleGroundHeight(p) + Random.Range(0.4f, 1.6f);
   if (Camera.main == null || (Camera.main.transform.position - p).sqrMagnitude > 60f * 60f) return;
   var ep = new ParticleSystem.EmitParams();
   for (int i = 0; i < count; i++) {
    ep.position = p + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0f, 1.4f), Random.Range(-2.5f, 2.5f));
    ep.velocity = new Vector3(Random.Range(-speed, speed), Random.Range(-0.25f, 0.45f), Random.Range(-speed, speed));
    ep.startLifetime = life * Random.Range(0.7f, 1.25f);
    ep.startSize = size * Random.Range(0.7f, 1.4f);
    ep.startColor = Lit(Color.Lerp(a, b, Random.value));
    ep.rotation = Random.Range(0f, 360f);
    ep.angularVelocity = Random.Range(-160f, 160f);
    ps.Emit(ep, 1);
   }
  }

  (float x, float z, float w)? NearestLake(Vector3 hero) {
   (float x, float z, float w)? best = null;
   float bestD = 70f * 70f;
   foreach (var lake in OpenWorldRegions.Lakes) {
    float dx = lake.Center.x - hero.x, dz = lake.Center.y - hero.z;
    float d = dx * dx + dz * dz;
    if (d < bestD) { bestD = d; best = (lake.Center.x, lake.Center.y, lake.Radius * 0.6f); }
   }
   return best;
  }

  static Color Lit(Color c) {
   float night = OpenWorldAtmosphere.Instance ? OpenWorldAtmosphere.Instance.NightFactor : 0f;
   float k = Mathf.Lerp(1f, 0.35f, night);
   return new Color(c.r * k, c.g * k, c.b * k, c.a);
  }

  void Build() {
   if (!dotTex) dotTex = SoftTexture(64, 1.6f);
   if (!leafTex) leafTex = SoftTexture(64, 0.55f);
   flutter = NewSystem("Butterflies", 48, Material("OW Butterfly", leafTex), 0f);
   glow = NewSystem("Fireflies", 64, Material("OW Firefly", dotTex), 0f);
   darts = NewSystem("Dragonflies", 24, Material("OW Dragonfly", leafTex), 0f);
   leaves = NewSystem("Leaves", 48, Material("OW Leaf", leafTex), 0f);
   dust = NewSystem("Dust Devils", 96, Material("OW Dust", dotTex), 0f);
   snow = NewSystem("Spindrift", 96, Material("OW Spindrift", dotTex), 0f);
   var glowCol = glow.colorOverLifetime; glowCol.enabled = true;
   var g = new Gradient();
   g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.15f, 0.6f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
   glowCol.color = new ParticleSystem.MinMaxGradient(g);
  }

  ParticleSystem NewSystem(string name, int max, Material mat, float gravity) {
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
   r.renderMode = ParticleSystemRenderMode.Billboard;
   r.sharedMaterial = mat;
   r.shadowCastingMode = ShadowCastingMode.Off;
   r.receiveShadows = false;
   ps.Play();
   return ps;
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
   mat.renderQueue = (int)RenderQueue.Transparent;
   return mat;
  }

  // Soft round dot (power < 1 keeps a bright core for fireflies) or a squashed leaf-ish petal.
  static Texture2D SoftTexture(int size, float power) {
   var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_Ambient_" + power, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
   var px = new Color32[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++) {
     float dx = (x - c) / c, dy = (y - c) / c;
     if (power < 1f) dy *= 2.1f; // squash into a petal/leaf silhouette
     float r = Mathf.Sqrt(dx * dx + dy * dy);
     float a = Mathf.Pow(Mathf.Clamp01(1f - r), power);
     px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
    }
   tex.SetPixels32(px);
   tex.Apply(false, true);
   return tex;
  }
 }
}
