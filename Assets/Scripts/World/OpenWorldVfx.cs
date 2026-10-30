using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Pooled library of the prepared World Kit effects (Hovl Studio, Free Slash VFX, Eric VFX Studio,
 // Vefects fire, Gabriel Aguiar). Gameplay asks for meaning (slash, hit, burst, death, heal, loot)
 // and the library picks, tints, scales and recycles the matching professional effect.
 public sealed class OpenWorldVfx : MonoBehaviour {
  public static OpenWorldVfx Instance { get; private set; }
  public const int Physical = -1, Ember = 0, Frost = 1, Gale = 2, Storm = 3, Earth = 4, Holy = 5, Arcane = 6, Nature = 7;

  sealed class Fx {
   public string key; public GameObject go; public Transform t;
   public ParticleSystem[] systems; public ParticleSystem.MinMaxGradient[] baseColors;
   public Color tint = Color.white; public bool loop; public float life, stopAt, releaseAt;
   public Transform follow; public Vector3 offset; public bool following;
  }

  readonly Dictionary<string, Stack<Fx>> pools = new Dictionary<string, Stack<Fx>>();
  readonly Dictionary<string, int> created = new Dictionary<string, int>();
  readonly List<Fx> playing = new List<Fx>();
  int budget = 26, perKey = 6;
  float cullDistance = 110f;

  public static OpenWorldVfx Ensure(Transform parent) {
   if (Instance) return Instance;
   var go = new GameObject("OpenWorldVfx");
   if (parent) go.transform.SetParent(parent, false);
   Instance = go.AddComponent<OpenWorldVfx>();
   Instance.ApplyTier(OpenWorldGraphics.Instance ? (int)OpenWorldGraphics.Instance.Current : 1);
   return Instance;
  }

  public void ApplyTier(int tier) {
   budget = tier >= 2 ? 36 : tier == 1 ? 26 : 12;
   perKey = tier >= 1 ? 6 : 3;
   cullDistance = tier >= 2 ? 150f : tier == 1 ? 110f : 70f;
  }

  public static bool Has(string key) => OpenWorldWorldKit.Entry(key) != null && OpenWorldWorldKit.Prefab(key);

  // ---------- core ----------
  public static bool Play(string key, Vector3 position, Quaternion rotation, float scale = 1f, Color? tint = null, float life = -1f, Transform follow = null) {
   var lib = Instance;
   if (!lib) {
    var gm = OpenWorldManager.I;
    lib = Ensure(gm ? gm.WorldRoot : null);
   }
   return lib.Spawn(key, position, rotation, scale, tint ?? Color.white, life, follow) != null;
  }

  Fx Spawn(string key, Vector3 position, Quaternion rotation, float scale, Color tint, float life, Transform follow) {
   if (string.IsNullOrEmpty(key)) return null;
   var cam = Camera.main;
   if (cam && (cam.transform.position - position).sqrMagnitude > cullDistance * cullDistance && !follow) return null;
   var fx = Acquire(key);
   if (fx == null) return null;
   fx.t.SetParent(transform, false);
   fx.t.SetPositionAndRotation(position, rotation);
   fx.t.localScale = Vector3.one * Mathf.Clamp(scale, 0.05f, 12f);
   fx.follow = follow;
   fx.following = follow;
   fx.offset = follow ? position - follow.position : Vector3.zero;
   ApplyTint(fx, tint);
   fx.go.SetActive(true);
   foreach (var s in fx.systems) {
    if (!s) continue;
    s.Clear(false);
    s.Play(false);
   }
   float duration = life > 0f ? life : fx.life;
   fx.stopAt = Time.time + Mathf.Max(0.05f, duration);
   fx.releaseAt = fx.stopAt + (fx.loop || life > 0f ? MaxParticleLife(fx) : 0.1f);
   playing.Add(fx);
   if (armedDelay > 0f) { freezeAt = Time.time + armedDelay; armedDelay = -1f; }
   return fx;
  }

  Fx Acquire(string key) {
   if (pools.TryGetValue(key, out var stack) && stack.Count > 0) return stack.Pop();
   created.TryGetValue(key, out int count);
   if (count < perKey && playing.Count < budget) return Create(key, count);
   // Over the budget or the per-effect cap: recycle the oldest one-shot (same effect preferred) so
   // fresh combat feedback always shows.
   int index = -1;
   for (int i = 0; i < playing.Count; i++) {
    if (playing[i].key == key) { index = i; break; }
    if (index < 0 && count < perKey) index = i;
   }
   if (index < 0) return null;
   var old = playing[index];
   playing.RemoveAt(index);
   if (old.key == key) { old.go.SetActive(false); return old; }
   Return(old);
   return Create(key, count);
  }

  Fx Create(string key, int count) {
   var prefab = OpenWorldWorldKit.Prefab(key);
   if (!prefab) return null;
   var entry = OpenWorldWorldKit.Entry(key);
   var go = Instantiate(prefab, transform);
   go.name = "fx " + key;
   go.SetActive(false);
   var fx = new Fx { key = key, go = go, t = go.transform, systems = go.GetComponentsInChildren<ParticleSystem>(true), loop = entry != null && entry.loop, life = entry != null && entry.life > 0f ? entry.life : 2f };
   fx.baseColors = new ParticleSystem.MinMaxGradient[fx.systems.Length];
   for (int i = 0; i < fx.systems.Length; i++) {
    var main = fx.systems[i].main;
    main.playOnAwake = false;
    fx.baseColors[i] = main.startColor;
   }
   Calibrate(go);
   created[key] = count + 1;
   return fx;
  }

  // The Vefects fire materials were authored for an ACES demo scene at 20-400x emission; in the realm's
  // neutral grade that flattens flames into a solid yellow sheet. Per-renderer property blocks bring
  // them to the realm's exposure (bright core, orange body, red tips) without touching vendor assets.
  static MaterialPropertyBlock calibration;
  public static void Calibrate(GameObject go) {
   if (!go) return;
   bool vefects = false;
   foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
    var mats = r.sharedMaterials;
    for (int i = 0; i < mats.Length; i++) {
     var m = mats[i];
     if (!m || !m.shader || !m.shader.name.StartsWith("SH_Vefects_VFX_URP")) continue;
     vefects = true;
     // Heat haze refracts the camera colour texture, which the Balanced tier does not render.
     if (m.shader.name.Contains("Heat_Haze") && !OpaqueTextureAvailable()) { r.enabled = false; break; }
     if (calibration == null) calibration = new MaterialPropertyBlock();
     r.GetPropertyBlock(calibration, i);
     bool changed = false;
     foreach (var prop in EmissionProps) {
      if (!m.HasProperty(prop)) continue;
      float v = m.GetFloat(prop);
      if (v <= 4f) continue;
      calibration.SetFloat(prop, Mathf.Max(3f, v * 0.16f));
      changed = true;
     }
     if (changed) r.SetPropertyBlock(calibration, i);
    }
   }
   // The fires' particle-spawned point lights would compete with the pooled night lights for the
   // terrain's per-object light slots; fire light comes from the realm's light anchors instead.
   if (vefects) foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var lights = ps.lights; if (lights.enabled) lights.enabled = false; }
  }
  static readonly string[] EmissionProps = { "_EmissionIntensity", "_EmissiveIntensity" };

  static bool OpaqueTextureAvailable() {
   var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
   return !urp || urp.supportsCameraOpaqueTexture;
  }

  void Return(Fx fx) {
   if (fx == null || !fx.go) return;
   fx.go.SetActive(false);
   fx.follow = null; fx.following = false;
   fx.t.SetParent(transform, false);
   if (!pools.TryGetValue(fx.key, out var stack)) pools[fx.key] = stack = new Stack<Fx>();
   stack.Push(fx);
  }

  static float MaxParticleLife(Fx fx) {
   float m = 0.2f;
   foreach (var s in fx.systems) if (s) m = Mathf.Max(m, s.main.startLifetime.constantMax);
   return Mathf.Min(m, 4f);
  }

  static void ApplyTint(Fx fx, Color tint) {
   if (fx.tint == tint) return;
   fx.tint = tint;
   bool identity = tint == Color.white;
   for (int i = 0; i < fx.systems.Length; i++) {
    var s = fx.systems[i];
    if (!s) continue;
    var main = s.main;
    main.startColor = identity ? fx.baseColors[i] : Tinted(fx.baseColors[i], tint);
   }
  }

  static ParticleSystem.MinMaxGradient Tinted(ParticleSystem.MinMaxGradient g, Color tint) {
   switch (g.mode) {
    case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Mul(g.color, tint));
    case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Mul(g.colorMin, tint), Mul(g.colorMax, tint));
    case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(TintGradient(g.gradient, tint));
    case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(TintGradient(g.gradientMin, tint), TintGradient(g.gradientMax, tint));
    case ParticleSystemGradientMode.RandomColor: { var r = new ParticleSystem.MinMaxGradient(TintGradient(g.gradient, tint)); r.mode = ParticleSystemGradientMode.RandomColor; return r; }
    default: return g;
   }
  }

  // Tint keeps the effect's luminance structure but moves its hue towards the requested colour.
  static Color Mul(Color c, Color tint) {
   float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
   var hueShift = new Color(tint.r * lum, tint.g * lum, tint.b * lum, c.a);
   var mixed = Color.Lerp(c * tint, hueShift, 0.6f);
   mixed.a = c.a * tint.a;
   return mixed;
  }

  static Gradient TintGradient(Gradient g, Color tint) {
   if (g == null) return null;
   var keys = g.colorKeys;
   for (int i = 0; i < keys.Length; i++) keys[i].color = Mul(keys[i].color, tint);
   var result = new Gradient { mode = g.mode };
   result.SetKeys(keys, g.alphaKeys);
   return result;
  }

  void LateUpdate() {
   float now = Time.time;
   for (int i = playing.Count - 1; i >= 0; i--) {
    var fx = playing[i];
    if (fx == null || !fx.go) { playing.RemoveAt(i); continue; }
    if (fx.follow) fx.t.position = fx.follow.position + fx.offset;
    else if (fx.following) { fx.following = false; fx.releaseAt = Mathf.Min(fx.releaseAt, now + 0.5f); } // owner destroyed
    if (now >= fx.stopAt && fx.stopAt > 0f) {
     foreach (var s in fx.systems) if (s && s.isEmitting) s.Stop(false, ParticleSystemStopBehavior.StopEmitting);
     fx.stopAt = -1f;
    }
    if (now >= fx.releaseAt) { playing.RemoveAt(i); Return(fx); }
   }
  }

  // ---------- attached loops (campfires, torches, portals, auras) ----------
  public static GameObject Attach(string key, Transform parent, Vector3 localPosition, float scale = 1f, Color? tint = null) {
   var lib = Instance ? Instance : Ensure(OpenWorldManager.I ? OpenWorldManager.I.WorldRoot : null);
   var prefab = OpenWorldWorldKit.Prefab(key);
   if (!prefab || !parent) return null;
   var go = Instantiate(prefab, parent);
   go.name = "fx " + key;
   go.transform.localPosition = localPosition;
   go.transform.localRotation = Quaternion.identity;
   go.transform.localScale = Vector3.one * scale;
   Calibrate(go);
   if (tint.HasValue) foreach (var s in go.GetComponentsInChildren<ParticleSystem>(true)) { var main = s.main; main.startColor = Tinted(main.startColor, tint.Value); }
   foreach (var s in go.GetComponentsInChildren<ParticleSystem>(true)) { var main = s.main; main.cullingMode = ParticleSystemCullingMode.Pause; s.Play(false); }
   return go;
  }

  // ---------- semantic helpers ----------
  static int ElementFromColor(Color c) {
   if (c.r > 0.85f && c.g > 0.65f && c.b < 0.5f) return Holy;
   if (c.r > c.g * 1.35f && c.r > c.b * 1.6f) return Ember;
   if (c.b > c.r * 1.25f && c.g > 0.62f) return Frost;
   if (c.r > 0.5f && c.b > 0.8f && c.g < 0.62f) return Arcane;
   if (c.g > c.r * 1.2f) return Nature;
   return Physical;
  }

  // Replacement for the old coloured sprite burst.
  public static bool Burst(Vector3 pos, Color color, int count) {
   if (count <= 3) return Play("dust_puff", pos, Quaternion.identity, 0.35f);
   float s = Mathf.Clamp(count / 20f, 0.55f, 1.5f);
   switch (ElementFromColor(color)) {
    case Ember: return Play("hit_fire", pos, Quaternion.identity, s * 0.55f);
    case Frost: return Play("hit_frost", pos, Quaternion.identity, s * 0.7f);
    case Nature: return count >= 20 ? Play("ga_heal", pos - Vector3.up * 0.8f, Quaternion.identity, s * 0.8f) : Play("hit_nature", pos, Quaternion.identity, s * 0.7f);
    case Holy: return Play("sparks_gold", pos, Quaternion.identity, s * 0.8f) | Play("hit_holy", pos, Quaternion.identity, s * 0.55f);
    case Arcane: return Play("hit_holy", pos, Quaternion.identity, s * 0.6f, color) | Play("sparks_white", pos, Quaternion.identity, s * 0.7f, color);
    default: return Play("sparks_white", pos, Quaternion.identity, s * 0.7f, color);
   }
  }

  public static bool Slash(int element, Vector3 pos, Quaternion rot, float reach, bool heavy) {
   float s = Mathf.Clamp(reach / 3f, 0.6f, 1.9f);
   switch (element) {
    case Ember: return heavy ? Play("slash_heavy_fire", pos, rot, s * 0.75f) : Play("slash_fire", pos, rot, s);
    case Frost: return heavy ? Play("slash_heavy_frost", pos, rot, s * 0.75f) : Play("slash_frost", pos, rot, s);
    case Gale: return heavy ? Play("slash_electro", pos, rot, s * 0.9f, new Color(0.45f, 1f, 0.82f)) : Play("slash_physical", pos, rot, s, new Color(0.55f, 1f, 0.86f));
    case Storm: return Play(heavy ? "slash_electro" : "slash_storm", pos, rot, s);
    case Earth: return Play(heavy ? "slash_stone" : "slash_earth", pos, rot, s);
    case Arcane: return Play("slash_heavy_arcane", pos, rot, s * 0.75f);
    default: return heavy ? Play("slash_heavy_arcane", pos, rot, s * 0.7f, new Color(0.95f, 0.97f, 1f)) : Play("slash_physical", pos, rot, s);
   }
  }

  public static bool SlashFromColor(Vector3 pos, Quaternion rot, Color color, float reach, bool heavy) {
   int e = ElementFromColor(color);
   if (e == Nature) e = Gale;
   if (e == Holy) e = Physical;
   return Slash(e, pos, rot, reach, heavy);
  }

  public static bool Hit(int element, Vector3 pos, bool heavy = false) {
   float s = heavy ? 1.15f : 0.8f;
   switch (element) {
    case Ember: return Play("hit_fire", pos, Quaternion.identity, s * 0.6f);
    case Frost: return Play("hit_frost", pos, Quaternion.identity, s);
    case Gale: return Play("hit_nature", pos, Quaternion.identity, s);
    case Storm: return Play("hit_storm", pos, Quaternion.identity, s);
    case Earth: return Play("hit_stone", pos, Quaternion.identity, s);
    case Holy: return Play("hit_holy", pos, Quaternion.identity, s);
    default: return Play(heavy ? "hit_crit" : "impact_generic", pos, Quaternion.identity, heavy ? 0.7f : 0.8f);
   }
  }

  // Area spell around the caster. Directional effects (crystal fronts) follow the caster's facing;
  // Hovl's front attacks travel along their local +X, hence the -90 degree yaw.
  // Layered like a hand-authored skill: a ground shockwave sells the radius, a core burst sells the
  // element, and sparks/embers/snow carry it into the air.
  static readonly Color EmberTint = new Color(1f, 0.52f, 0.18f), FrostTint = new Color(0.55f, 0.88f, 1f), GaleTint = new Color(0.55f, 1f, 0.85f);

  public static bool Spell(int element, Vector3 groundPos, float radius, Quaternion facing = default) {
   if (facing == default) facing = Quaternion.identity;
   float s = Mathf.Clamp(radius / 4.5f, 0.6f, 2f);
   var chest = groundPos + Vector3.up * 1f;
   // Rings ride at waist height: flat ground decals vanish into knee-high grass.
   var ring = groundPos + Vector3.up * 0.55f;
   switch (element) {
    case Ember:
     return Play("aoe_slash", ring, facing, s * 1.1f) | Play("hit_fire", chest, facing, s * 0.8f) |
            Play("ga_shockwave", ring, Quaternion.identity, s * 1.1f, EmberTint) | Play("sparks_white", chest, Quaternion.identity, s, EmberTint);
    case Frost:
     return Play("aoe_snow", groundPos, facing, s, null, 2.2f) | Play("circle_freeze", groundPos + Vector3.up * 0.08f, Quaternion.identity, s, null, 1.6f) |
            Play("ga_shockwave", ring, Quaternion.identity, s * 1.1f, FrostTint) | Play("hit_frost", chest, Quaternion.identity, s);
    default:
     return Play("ga_shockwave", ring, Quaternion.identity, s * 1.2f, GaleTint) | Play("ga_tornado", groundPos, Quaternion.identity, s * 0.55f, GaleTint, 1.1f) |
            Play("hit_nature", chest, Quaternion.identity, s) | Play("sparks_white", chest, Quaternion.identity, s * 0.9f, GaleTint);
   }
  }

  // Ultimate (Elemental Burst).
  public static bool Ultimate(int element, Vector3 groundPos, float radius, Quaternion facing = default) {
   if (facing == default) facing = Quaternion.identity;
   float s = Mathf.Clamp(radius / 5f, 0.8f, 2.4f);
   var ring = groundPos + Vector3.up * 0.55f;
   switch (element) {
    case Ember:
     return Play("aoe_meteors", groundPos, facing, s, null, 3.2f) | Play("ga_explosion", groundPos, facing, s * 0.55f) |
            Play("aoe_slash", ring, facing, s * 1.2f) | Play("ga_shockwave", ring, Quaternion.identity, s * 1.5f, EmberTint);
    case Frost:
     return Play("aoe_snow", groundPos, facing, s * 1.2f, null, 3f) | Play("aoe_crystals", groundPos, facing * Quaternion.Euler(0f, -90f, 0f), s) |
            Play("circle_freeze", groundPos + Vector3.up * 0.08f, Quaternion.identity, s * 1.3f, null, 2.4f) | Play("ga_shockwave", ring, Quaternion.identity, s * 1.4f, FrostTint);
    default:
     return Play("ga_tornado", groundPos, Quaternion.identity, s, GaleTint, 2.6f) | Play("ga_shockwave", ring, Quaternion.identity, s * 1.4f, GaleTint) |
            Play("aura_lightning", groundPos + Vector3.up * 0.5f, Quaternion.identity, s * 0.9f, GaleTint, 2f) | Play("sparks_white", groundPos + Vector3.up * 1.2f, Quaternion.identity, s, GaleTint);
   }
  }

  public static bool Death(Vector3 pos, float size, bool boss = false) {
   if (boss) return Play("ga_explosion", pos, Quaternion.identity, Mathf.Clamp(size, 1f, 3f)) | Play("smoke_puff", pos, Quaternion.identity, Mathf.Clamp(size, 1f, 3f));
   return Play("smoke_puff", pos, Quaternion.identity, Mathf.Clamp(size * 0.7f, 0.5f, 1.6f), new Color(0.75f, 0.68f, 0.62f)) | Play("sparks_gold", pos + Vector3.up * 0.4f, Quaternion.identity, 0.6f);
  }

  public static bool Heal(Vector3 groundPos, Transform follow = null) => Play("ga_heal", groundPos, Quaternion.identity, 0.9f, null, -1f, follow);
  public static bool Reward(Vector3 pos) => Play("sparks_gold", pos, Quaternion.identity, 1f) | Play("levelup_pillar", pos - Vector3.up * 0.9f, Quaternion.identity, 0.8f);
  public static bool Parry(Vector3 pos) => Play("hit_holy", pos, Quaternion.identity, 0.75f) | Play("sparks_gold", pos, Quaternion.identity, 0.7f);
  public static bool Evade(Vector3 pos) => Play("smoke_puff", pos, Quaternion.identity, 0.6f, new Color(0.5f, 0.95f, 1f)) | Play("sparks_white", pos, Quaternion.identity, 0.6f, new Color(0.45f, 0.95f, 1f));
  public static bool Dust(Vector3 pos, float scale = 0.5f) => Play("dust_puff", pos, Quaternion.identity, scale);
  public static bool Teleport(Vector3 groundPos) => Play("teleport", groundPos, Quaternion.identity, 1f);

  // Kicked-up snow, sand or splash under running feet.
  public static bool FootPuff(Vector3 pos, OpenWorldSoundscape.Surface surface) {
   switch (surface) {
    case OpenWorldSoundscape.Surface.Snow: return Play("dust_puff", pos + Vector3.up * 0.05f, Quaternion.identity, 0.38f, new Color(0.95f, 0.97f, 1f));
    case OpenWorldSoundscape.Surface.Sand: return Play("dust_puff", pos + Vector3.up * 0.05f, Quaternion.identity, 0.42f, new Color(0.93f, 0.8f, 0.58f));
    // Wading: a small splash and ring at the surface above the foot (OpenWorldWaterFx).
    case OpenWorldSoundscape.Surface.Water: return OpenWorldWaterFx.SplashAt(pos, 0.3f);
    default: return false;
   }
  }

  // Bridge helper: "key [scale]" plays a kit effect 4 m in front of the hero at ground level.
  public static void DebugPlay(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || string.IsNullOrEmpty(arg)) return;
   var p = arg.Trim().Split(' ');
   float scale = p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sc) ? sc : 1f;
   var t = gm.Hero.transform;
   var pos = t.position + t.forward * 4f;
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos) + (p.Length > 2 && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float up) ? up : 0f);
   bool ok = Play(p[0], pos, t.rotation, scale);
   Debug.Log("[Vfx] debug play " + p[0] + " x" + scale + (ok ? "" : " (failed)"));
  }

  // Bridge helper for deterministic captures on a slow editor: arms a freeze of game time that fires
  // the given number of game seconds after the next effect starts, so the following screenshot shows
  // it mid-flight however long the editor takes. A negative value unfreezes.
  float freezeAt = -1f, armedDelay = -1f;
  bool frozeTime;
  public static void DebugFreeze(string arg) {
   float s = 0.35f;
   if (!string.IsNullOrEmpty(arg)) float.TryParse(arg.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s);
   var gm = OpenWorldManager.I;
   var lib = Instance ? Instance : Ensure(gm ? gm.WorldRoot : null);
   if (s < 0f) { lib.freezeAt = -1f; lib.armedDelay = -1f; if (lib.frozeTime) Time.timeScale = 1f; lib.frozeTime = false; Debug.Log("[Vfx] unfrozen"); return; }
   lib.armedDelay = Mathf.Max(0.02f, s);
   Debug.Log("[Vfx] freeze armed " + lib.armedDelay.ToString("0.00") + "s after the next effect");
  }

  void Update() {
   if (freezeAt > 0f && Time.time >= freezeAt) { freezeAt = -1f; frozeTime = true; Time.timeScale = 0f; Debug.Log("[Vfx] frozen"); }
  }

  void OnDestroy() {
   if (Instance == this) Instance = null;
   if (frozeTime) Time.timeScale = 1f;
  }
 }
}
