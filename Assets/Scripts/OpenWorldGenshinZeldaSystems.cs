using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Lumina, the Floating Aether Companion Wisp (Genshin Impact Paimon / Zelda Navi-inspired).
 // Orbits gently near Aster's left shoulder with fluttering crystal wings and fires bonus Aether bolts on attacks.
 public sealed class OpenWorldCompanionWisp : MonoBehaviour {
  Transform targetHero;
  Transform leftWing;
  Transform rightWing;
  float orbitPhase;

  public static OpenWorldCompanionWisp Attach(Transform heroTransform, Transform worldRoot) {
   var go = new GameObject("CompanionWisp_Lumina");
   if (worldRoot) go.transform.SetParent(worldRoot, false);
   go.transform.position = heroTransform.position + new Vector3(-0.85f, 1.75f, -0.35f);

   var wisp = go.AddComponent<OpenWorldCompanionWisp>();
   wisp.targetHero = heroTransform;
   wisp.BuildVisuals();
   return wisp;
  }

  // A fairy light rather than primitives: a glowing core and soft halo (billboards, so she reads as
  // light from any angle), translucent flapping wings, and a trail of sparkles that drift up and fade
  // from aether blue to gold as she moves.
  void BuildVisuals() {
   var dot = SoftDot();
   var glowMat = Additive(dot, "Lumina Glow", Color.white);
   Glow("Core", glowMat, 0.15f, 0.2f, 9f, 0.5f, new Color(1.6f, 2.2f, 2.3f, 1f));
   Glow("Halo", glowMat, 0.5f, 0.68f, 5f, 0.9f, new Color(0.35f, 0.95f, 1f, 0.32f));
   Sparkles(glowMat);
   var wingMat = Additive(WingTexture(), "Lumina Wing", new Color(1.35f, 1.2f, 0.75f, 0.85f));
   leftWing = CreateWing("LeftWing", new Vector3(-0.07f, 0.03f, -0.03f), wingMat, -1f);
   rightWing = CreateWing("RightWing", new Vector3(0.07f, 0.03f, -0.03f), wingMat, 1f);

   var trail = gameObject.AddComponent<TrailRenderer>();
   trail.time = 0.45f;
   trail.startWidth = 0.07f;
   trail.endWidth = 0f;
   trail.minVertexDistance = 0.03f;
   trail.material = glowMat;
   trail.startColor = new Color(0.55f, 1.4f, 1.6f, 0.55f);
   trail.endColor = new Color(1.3f, 1.05f, 0.45f, 0f);
   trail.shadowCastingMode = ShadowCastingMode.Off;
   trail.receiveShadows = false;
  }

  void Glow(string name, Material mat, float minSize, float maxSize, float rate, float life, Color color) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   var ps = go.AddComponent<ParticleSystem>();
   ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
   var main = ps.main;
   main.loop = true; main.playOnAwake = true; main.maxParticles = 16;
   main.simulationSpace = ParticleSystemSimulationSpace.Local;
   main.startLifetime = life; main.startSpeed = 0f;
   main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
   main.startColor = color;
   var em = ps.emission; em.rateOverTime = rate;
   var shape = ps.shape; shape.enabled = false;
   var col = ps.colorOverLifetime; col.enabled = true;
   var g = new Gradient();
   g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
   col.color = g;
   var psr = go.GetComponent<ParticleSystemRenderer>();
   psr.sharedMaterial = mat;
   psr.shadowCastingMode = ShadowCastingMode.Off; psr.receiveShadows = false;
   psr.sortingFudge = name == "Core" ? -2f : 0f;
   ps.Play();
  }

  void Sparkles(Material mat) {
   var go = new GameObject("Sparkles");
   go.transform.SetParent(transform, false);
   var ps = go.AddComponent<ParticleSystem>();
   ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
   var main = ps.main;
   main.loop = true; main.playOnAwake = true; main.maxParticles = 80;
   main.simulationSpace = ParticleSystemSimulationSpace.World;
   main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
   main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
   main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
   main.startColor = new Color(0.8f, 1.6f, 1.8f, 1f);
   main.gravityModifier = -0.035f;
   var em = ps.emission; em.rateOverTime = 7f; em.rateOverDistance = 14f;
   var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.07f;
   var noise = ps.noise; noise.enabled = true; noise.strength = 0.18f; noise.frequency = 1.4f; noise.quality = ParticleSystemNoiseQuality.Low;
   var col = ps.colorOverLifetime; col.enabled = true;
   var g = new Gradient();
   g.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 1f, 1f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.4f), 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
   col.color = g;
   var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
   var psr = go.GetComponent<ParticleSystemRenderer>();
   psr.sharedMaterial = mat;
   psr.shadowCastingMode = ShadowCastingMode.Off; psr.receiveShadows = false;
   ps.Play();
  }

  Transform CreateWing(string name, Vector3 localPos, Material mat, float side) {
   // Pivot at the wing root so flapping hinges at her back; the quad extends outwards.
   var pivot = new GameObject(name).transform;
   pivot.SetParent(transform, false);
   pivot.localPosition = localPos;
   var w = GameObject.CreatePrimitive(PrimitiveType.Quad);
   w.name = name + "Blade";
   w.transform.SetParent(pivot, false);
   w.transform.localPosition = new Vector3(side * 0.11f, 0.03f, 0f);
   w.transform.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
   w.transform.localScale = new Vector3(0.22f, 0.13f, 1f);
   DestroyCollider(w);
   var r = w.GetComponent<Renderer>();
   r.sharedMaterial = mat;
   r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
   return pivot;
  }

  static Texture2D dotTex, wingTex;
  static Texture2D SoftDot() {
   if (dotTex) return dotTex;
   const int size = 64;
   dotTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Lumina Dot", wrapMode = TextureWrapMode.Clamp };
   var px = new Color[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
    float dx = (x - c) / c, dy = (y - c) / c;
    float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
    px[y * size + x] = new Color(1f, 1f, 1f, r * r * (0.6f + 0.4f * r));
   }
   dotTex.SetPixels(px); dotTex.Apply(false, true);
   return dotTex;
  }

  // A soft teardrop membrane with a brighter rim and a few veins.
  static Texture2D WingTexture() {
   if (wingTex) return wingTex;
   const int w = 64, h = 40;
   wingTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Lumina Wing", wrapMode = TextureWrapMode.Clamp };
   var px = new Color[w * h];
   for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
    float u = x / (w - 1f), v = y / (h - 1f) * 2f - 1f;
    float halfWidth = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (0.55f + 0.45f * u);
    float d = halfWidth > 0.001f ? Mathf.Abs(v) / halfWidth : 2f;
    float body = Mathf.Clamp01(1f - d);
    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.92f) * 9f);
    float vein = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(u * 9f + v * 2.2f)) * 6f) * body * 0.35f;
    float a = Mathf.Clamp01(body * 0.28f + rim * 0.75f + vein) * Mathf.Clamp01(u * 6f);
    px[y * w + x] = new Color(1f, 1f, 1f, a);
   }
   wingTex.SetPixels(px); wingTex.Apply(false, true);
   return wingTex;
  }

  static Material Additive(Texture tex, string name, Color tint) {
   var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
   if (!shader) return new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = tex };
   var m = new Material(shader) { name = name };
   m.SetTexture("_BaseMap", tex);
   m.SetColor("_BaseColor", tint);
   m.SetFloat("_Surface", 1f);
   m.SetFloat("_Blend", 2f);
   m.SetOverrideTag("RenderType", "Transparent");
   m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
   m.SetInt("_DstBlend", (int)BlendMode.One);
   m.SetInt("_ZWrite", 0);
   m.SetFloat("_Cull", 0f);
   m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   m.renderQueue = (int)RenderQueue.Transparent;
   return m;
  }

  static void DestroyCollider(GameObject go) {
   var col = go.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
  }

  void Update() {
   if (!targetHero) return;
   orbitPhase += Time.deltaTime * 2.6f;
   Vector3 desired = targetHero.position
    - targetHero.right * 0.78f
    - targetHero.forward * 0.28f
    + new Vector3(Mathf.Cos(orbitPhase * 0.8f) * 0.14f, 1.68f + Mathf.Sin(orbitPhase) * 0.14f, Mathf.Sin(orbitPhase * 0.8f) * 0.14f);

   transform.position = Vector3.Lerp(transform.position, desired, 9f * Time.deltaTime);
   transform.rotation = Quaternion.Slerp(transform.rotation, targetHero.rotation, 8f * Time.deltaTime);

   float flap = Mathf.Sin(Time.time * 16f) * 32f;
   if (leftWing) leftWing.localRotation = Quaternion.Euler(0f, -20f, flap);
   if (rightWing) rightWing.localRotation = Quaternion.Euler(0f, 20f, -flap);
  }
 }

 // Firelight that breathes with the flames: layered noise on intensity and a little on range.
 public sealed class OpenWorldFireFlicker : MonoBehaviour {
  Light glow; float baseIntensity, baseRange, seed;
  void Awake() { glow = GetComponent<Light>(); if (glow) { baseIntensity = glow.intensity; baseRange = glow.range; } seed = Random.value * 100f; }
  void Update() {
   if (!glow) return;
   float t = Time.time * 7.5f + seed;
   float n = Mathf.PerlinNoise(t, seed) * 0.65f + Mathf.PerlinNoise(t * 2.3f, seed + 7f) * 0.35f;
   glow.intensity = baseIntensity * (0.78f + 0.4f * n);
   glow.range = baseRange * (0.94f + 0.1f * n);
  }
 }

 // Zelda / Genshin Interactive Culinary Campfire & Cooking Pot.
 // Allows Aster to cook Hearty Aether Rations for full restoration + Satiated Attack/Stamina buff.
 public sealed class OpenWorldCampfire : MonoBehaviour {
  public string CampfireTitle;
  TextMesh label;

  public static OpenWorldCampfire Spawn(string title, Vector3 pos, Transform parent) {
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);
   var go = new GameObject("Campfire_" + title);
   if (parent) go.transform.SetParent(parent, false);
   go.transform.position = pos;

   var cf = go.AddComponent<OpenWorldCampfire>();
   cf.CampfireTitle = title;
   cf.BuildVisuals(pos);
   return cf;
  }

  void BuildVisuals(Vector3 pos) {
   if (BuildRealmHearth()) {
    // In the realm the firelight comes from the pooled night lights, which stay inside the per-object
    // light budget so the ground around the hearth is actually lit.
    OpenWorldRealm.Instance.LightAnchors.Add(new OpenWorldRealm.LightAnchor { position = pos + Vector3.up * 0.9f, color = new Color(1f, 0.58f, 0.26f), range = 12f });
   } else {
    BuildPrimitiveHearth(pos);
    // Warm campfire point light, flickering with the flames
    var lgo = new GameObject("CampfireLight");
    lgo.transform.SetParent(transform, false);
    lgo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
    var pl = lgo.AddComponent<Light>();
    pl.type = LightType.Point;
    pl.color = new Color(1.0f, 0.62f, 0.26f);
    pl.intensity = 1.85f;
    pl.range = 10f;
    pl.shadows = LightShadows.None;
    lgo.AddComponent<OpenWorldFireFlicker>();
   }

   var ltext = new GameObject("CampfireLabel");
   ltext.transform.SetParent(transform, false);
   ltext.transform.localPosition = new Vector3(0f, 1.65f, 0f);
   label = ltext.AddComponent<TextMesh>();
   label.text = $"{CampfireTitle}\n[INTERACT: COOK HEARTY STEW]";
   label.fontSize = 28;
   label.characterSize = 0.04f;
   label.anchor = TextAnchor.MiddleCenter;
   label.alignment = TextAlignment.Center;
   label.color = new Color(1.0f, 0.84f, 0.42f);
   ltext.SetActive(false);
  }

  // In the realm the hearth is built from store props: a stone ring, crossed logs, a cook-pot hung on
  // its frame and a live fire with smoke, embers and heat haze (Vefects).
  bool BuildRealmHearth() {
   if (!OpenWorldRealm.Instance || !OpenWorldWorldKit.Prefab("fire_bonfire") || !OpenWorldWorldKit.Prefab("bonfire_c")) return false;
   KitPiece("bonfire_c", Vector3.zero, 0f, 0.8f);
   KitPiece("bonfire_b", new Vector3(0f, 0.02f, 0f), 35f, 0.55f);
   KitPiece("camp_cook", Vector3.zero, 90f, 0.5f);
   OpenWorldVfx.Attach("fire_bonfire", transform, new Vector3(0f, 0.1f, 0f), 0.85f);
   return true;
  }

  void KitPiece(string key, Vector3 local, float yaw, float scale) {
   var prefab = OpenWorldWorldKit.Prefab(key);
   if (!prefab) return;
   var go = Instantiate(prefab, transform);
   go.name = key;
   go.transform.localPosition = local;
   go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
   go.transform.localScale = Vector3.one * scale;
   OpenWorldRealm.StripLegacyFire(go);
  }

  void BuildPrimitiveHearth(Vector3 pos) {
   // Firewood logs & stone ring
   OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_log_wood_01", "Props/Textures/rpgpp_lt_tex_a",
    transform, pos, 0.42f, Color.white, false);

   // Glowing ember core
   var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   core.name = "EmberCore";
   core.transform.SetParent(transform, false);
   core.transform.localPosition = new Vector3(0f, 0.22f, 0f);
   core.transform.localScale = new Vector3(0.55f, 0.32f, 0.55f);
   var col = core.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   Color fireCol = new Color(1.0f, 0.48f, 0.12f);
   core.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("CampfireCore", fireCol, null, null, 0.9f, 0.1f, fireCol * 1.6f);

   // Suspended cooking pot above fire
   var pot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   pot.name = "CookingPot";
   pot.transform.SetParent(transform, false);
   pot.transform.localPosition = new Vector3(0f, 0.68f, 0f);
   pot.transform.localScale = new Vector3(0.52f, 0.38f, 0.52f);
   var pcol = pot.GetComponent<Collider>();
   if (pcol) {
    if (Application.isPlaying) Destroy(pcol);
    else DestroyImmediate(pcol);
   }
   pot.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("CookingPotMat", new Color(0.22f, 0.24f, 0.28f), null, null, 0.65f, 0.75f);
  }

  public void CookMeal(OpenWorldManager gm) {
   if (!gm || !gm.Hero) return;
   if (gm.RawMeat < 1) { gm.ShowBanner("You need raw meat • Hunt deer and collect their meat first"); return; }
   gm.RawMeat--; gm.MarkProgressDirty();
   OpenWorldAudio.Play("sfx_ember_cast", 0.55f);
   gm.SpawnBurst(transform.position + Vector3.up * 0.8f, new Color(1.0f, 0.72f, 0.24f), 20);
   gm.Hero.Heal(gm.Hero.MaxHealth);
   gm.Hero.RestoreAether(gm.Hero.MaxAether);
   gm.Hero.RefillStamina();
   gm.Hero.ActivateMealBuff(180f);
   if (gm.CollectedWorldIds.Add("cooking:" + CampfireTitle)) {
    gm.MarkProgressDirty();
    gm.AddGold(15);
    gm.ShowBanner("COOKED GOLDEN AETHER STEW! (+15% Attack & Stamina Buff, Full HP/Aether, +15 Gold)");
   } else {
    gm.ShowBanner("COOKED GOLDEN AETHER STEW! (+15% Attack & Stamina Buff, Full HP & Aether Restored)");
   }
   gm.RecordVerdantMeal(this);
  }

  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.IsModalOpen) return;
   if (label) {
    float d = Vector3.Distance(gm.Hero.transform.position, transform.position);
    bool show = d <= 13.5f;
    if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    if (show && Camera.main) label.transform.rotation = Camera.main.transform.rotation;
   }
  }
 }

 // Wuthering Waves style Guardian Echo Spirit Summon & Genshin Impact Elemental Burst Visuals
 public sealed class OpenWorldEchoSpirit : MonoBehaviour {
  float timer;
  float fullScale;
  const float GuardianHeight = 3.5f;

  public static void Summon(OpenWorldHero hero) {
   if (!hero) return;
   var gm = OpenWorldManager.I;
   Vector3 spawnPos = hero.transform.position + hero.transform.forward * 2.7f;
   spawnPos.y = OpenWorldMaterialHelper.SampleGroundHeight(spawnPos, hero.transform);

   var go = new GameObject("GuardianEchoSpirit");
   if (gm && gm.WorldRoot) go.transform.SetParent(gm.WorldRoot, false);
   go.transform.position = spawnPos;
   go.transform.rotation = hero.transform.rotation;

   var vis = OpenWorldCharacterVisual.Attach(go.transform, "StoneBrute");
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(go.transform);
   float size = bounds.size.y > .01f ? GuardianHeight / bounds.size.y : 1.8f;
   go.transform.localScale = Vector3.one * size;
   bounds = OpenWorldMaterialHelper.ComputeRendererBounds(go.transform);
   if (vis) vis.transform.position += Vector3.up * (spawnPos.y - bounds.min.y);
   Color spectral = new Color(0.32f, 0.92f, 1.0f);
   OpenWorldMaterialHelper.TintHierarchy(go, spectral, spectral * 1.25f);
   vis?.Play("slam", true, "slash", 1.35f);

   OpenWorldSlashWave.SpawnSlash(spawnPos + Vector3.up * 0.45f, hero.transform.rotation, spectral, 4.8f, true);
   gm?.SpawnBurst(spawnPos + Vector3.up * 1.1f, spectral, 26);
   gm?.CameraRig?.AddShake(0.24f, 0.25f);
   OpenWorldAudio.Play("sfx_boss_heartwood_roar", 0.55f);
   OpenWorldCombatPopup.Spawn(spawnPos + Vector3.up * 3.8f, "SUMMON: GUARDIAN SLAM!", spectral, 1.25f);

   var spirit = go.AddComponent<OpenWorldEchoSpirit>();
   spirit.timer = 1.15f; spirit.fullScale = size;
  }

  void Update() {
   timer -= Time.deltaTime;
   transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * fullScale, Mathf.Clamp01(timer / 0.35f));
   if (timer <= 0f) {
    if (Application.isPlaying) Destroy(gameObject);
    else DestroyImmediate(gameObject);
   }
  }
 }

 // Builds the breathtaking Hub Courtyard Medallion, Grand Northern Avenue Arch & Watchtowers,
 // and the 3-Island Floating Celestial Sky Archipelago directly in the Hub's northern horizon vista!
 public static class OpenWorldVistaBuilder {
  public static void BuildHubVistaAndCampfires(OpenWorldManager gm) {
   var vista = new GameObject("HubVistaAndSkyArchipelago").transform;
   vista.SetParent(gm.WorldRoot, false);

   // 1. Ornate Runic Sun-Plaza Medallion under the Hub Courtyard (non-colliding flush visual)
   Vector3 medallionPos = new Vector3(0f, 0f, -1.2f);
   medallionPos.y = OpenWorldMaterialHelper.SampleGroundHeight(medallionPos) + 0.02f;
   var sunMedallion = OpenWorldMaterialHelper.SpawnModel("Astral/SunPad", "Astral/Textures/SunPad_basecolor",
    vista, medallionPos, 0.16f, Color.white, false);
   if (sunMedallion) sunMedallion.transform.localScale = new Vector3(2.8f, 0.45f, 2.8f);

   // 2. Grand Northern Avenue Triumphal Archway & Twin Watchtowers framing the horizon behind the Hub Gate
   Vector3 archPos = new Vector3(0f, 0f, 25f);
   archPos.y = OpenWorldMaterialHelper.SampleGroundHeight(archPos);
   gm.ReserveSpot(archPos, 5.5f);
   OpenWorldMaterialHelper.SpawnModel("Astral/Arch", "Astral/Textures/Arch_basecolor",
    vista, archPos, 6.2f, Color.white, false);

   Vector3 westTower = new Vector3(-13.5f, 0f, 30f);
   westTower.y = OpenWorldMaterialHelper.SampleGroundHeight(westTower);
   gm.ReserveSpot(westTower, 5.5f);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/Tower", "Stormveil/Textures/Tower_basecolor",
    vista, westTower, 9.2f, Color.white, true);

   Vector3 eastTower = new Vector3(13.5f, 0f, 30f);
   eastTower.y = OpenWorldMaterialHelper.SampleGroundHeight(eastTower);
   gm.ReserveSpot(eastTower, 5.5f);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/Tower", "Stormveil/Textures/Tower_basecolor",
    vista, eastTower, 9.2f, Color.white, true);

   // Framed half-timbered guildhouses angled along the Northern Plaza View
   Vector3 westGuild = new Vector3(-13.8f, 0f, 15.5f);
   westGuild.y = OpenWorldMaterialHelper.SampleGroundHeight(westGuild);
   gm.ReserveSpot(westGuild, 5.8f);
   var wg = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_building_04", "Props/Textures/rpgpp_lt_tex_a",
    vista, westGuild, 5.8f, Color.white, true);
   if (wg) wg.transform.LookAt(new Vector3(0f, westGuild.y, 6f));

   Vector3 eastGuild = new Vector3(13.8f, 0f, 15.5f);
   eastGuild.y = OpenWorldMaterialHelper.SampleGroundHeight(eastGuild);
   gm.ReserveSpot(eastGuild, 5.8f);
   var eg = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_building_02", "Props/Textures/rpgpp_lt_tex_a",
    vista, eastGuild, 5.8f, Color.white, true);
   if (eg) eg.transform.LookAt(new Vector3(0f, eastGuild.y, 6f));

   // 3. Zelda: TotK / Genshin Celestia Floating Sky-Sanctuary Archipelago directly in the Northern Horizon Vista!
   // The realm's horizon is real mountains; the archipelago only hovered over its hills, so it stays
   // a small-world feature (the quest's Celestial Sky-Isle is built by the manager).
   if (!OpenWorldRealm.Instance) {
     Vector3 centerSky = new Vector3(0f, 18.5f, 64f);
     OpenWorldMaterialHelper.SpawnModel("Islands/Island_Meadow", "Islands/Textures/Island_Meadow_basecolor",
      vista, centerSky, 7.2f, Color.white, true);
     OpenWorldMaterialHelper.SpawnModel("Astral/Arch", "Astral/Textures/Arch_basecolor",
      vista, centerSky + new Vector3(0f, 5.2f, 0f), 5.2f, Color.white, false);
     OpenWorldMaterialHelper.SpawnModel("Astral/Orrery", "Astral/Textures/Orrery_basecolor",
      vista, centerSky + new Vector3(0f, 5.4f, -1.2f), 3.2f, Color.white, false);

     Vector3 westSky = new Vector3(-19f, 21.5f, 72f);
     OpenWorldMaterialHelper.SpawnModel("Islands/Island_Plateau", "Islands/Textures/Island_Plateau_basecolor",
      vista, westSky, 6.2f, Color.white, true);
     OpenWorldMaterialHelper.SpawnModel("Stormveil/Tower", "Stormveil/Textures/Tower_basecolor",
      vista, westSky + new Vector3(0f, 4.6f, 0f), 6.8f, Color.white, false);

     Vector3 eastSky = new Vector3(19f, 20.5f, 70f);
     OpenWorldMaterialHelper.SpawnModel("Islands/Island_Plateau", "Islands/Textures/Island_Plateau_basecolor",
      vista, eastSky, 6.2f, Color.white, true);
     OpenWorldMaterialHelper.SpawnModel("Astral/WardFocus", "Astral/Textures/WardFocus_basecolor",
      vista, eastSky + new Vector3(0f, 4.5f, 0f), 3.6f, Color.white, false);
   }

   // 4. Interactive Culinary Campfires in the Capital Courtyard, Oakvale Hamlet & Frostpine Village
   Vector3[] campfires = {
    new Vector3(5.6f, 0f, -1.6f),
    new Vector3(41.5f, 0f, 40.5f),
    OpenWorldRealm.Instance ? OpenWorldRegions.Frostpine + new Vector3(2.5f, 0f, -9f) : new Vector3(-79.5f, 0f, 13.5f)
   };
   string[] cfNames = { "SANCTUARY HEARTH", "OAKVALE CAMPFIRE", "FROSTPINE HEARTH" };
   for (int i = 0; i < campfires.Length; i++) {
    gm.ReserveSpot(campfires[i], 2.5f);
    gm.Campfires.Add(OpenWorldCampfire.Spawn(cfNames[i], campfires[i], vista));
   }
  }
 }
}

