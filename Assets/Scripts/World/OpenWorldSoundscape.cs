using UnityEngine;

namespace LostRealmsOW {
 // Biome soundscape: procedural ambience beds (wind with gusts, rain, brooks, night crickets, an
 // eerie drone for the marches), positional bird and crow calls, crackling campfires and surface-
 // aware footsteps from the sound bank (grass, leaves, dirt, gravel, stone, sand, snow, water, wood).
 public sealed class OpenWorldSoundscape : MonoBehaviour {
  public static OpenWorldSoundscape Instance { get; private set; }
  public enum Surface { Grass, Leaves, Dirt, Gravel, Tile, Rock, Sand, Snow, Water, Wood, Mud }

  const int Rate = 22050;
  AudioSource wind, rain, water, crickets, drone, steps, music;
  AudioLowPassFilter windFilter, musicFilter;
  readonly AudioSource[] calls = new AudioSource[5];
  readonly AudioSource[] fires = new AudioSource[3];
  float nextBird = 3f, nextCrow = 6f, nextFireScan, gustPhase;
  float musicBaseVolume = 0.38f;
  int callIndex;
  readonly float[] alphaScratch = new float[8];

  public static OpenWorldSoundscape Attach(Transform root) {
   if (Instance) return Instance;
   var go = new GameObject("Soundscape");
   if (root) go.transform.SetParent(root, false);
   var s = go.AddComponent<OpenWorldSoundscape>();
   s.Build(root);
   return s;
  }

  void Build(Transform root) {
   Instance = this;
   var rng = new System.Random(1234);
   wind = Bed("Wind Bed", Loop(rng, 9f, WindSample));
   windFilter = wind.gameObject.AddComponent<AudioLowPassFilter>();
   windFilter.cutoffFrequency = 900f;
   rain = Bed("Rain Bed", Loop(rng, 6f, RainSample));
   water = Bed("Water Bed", Loop(rng, 7f, BrookSample));
   crickets = Bed("Night Bed", Crickets(rng, 8f));
   drone = Bed("Drone Bed", Loop(rng, 10f, DroneSample));
   steps = gameObject.AddComponent<AudioSource>();
   steps.playOnAwake = false; steps.spatialBlend = 0f; steps.volume = 0.4f;
   for (int i = 0; i < calls.Length; i++) calls[i] = Spatial("Call " + i, 4f, 45f);
   for (int i = 0; i < fires.Length; i++) { fires[i] = Spatial("Fire " + i, 2.5f, 22f); fires[i].loop = true; }
   OpenWorldAudio.Register("sfx_thunder", Thunder(rng));
   if (root) {
    var m = root.Find("BiomeMusic");
    if (m) { music = m.GetComponent<AudioSource>(); if (music) { musicBaseVolume = music.volume; musicFilter = music.gameObject.AddComponent<AudioLowPassFilter>(); musicFilter.cutoffFrequency = 22000f; } }
   }
  }

  AudioSource Bed(string name, AudioClip clip) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   var a = go.AddComponent<AudioSource>();
   a.clip = clip; a.loop = true; a.playOnAwake = false; a.spatialBlend = 0f; a.volume = 0f;
   a.time = Random.value * clip.length * 0.9f;
   a.Play();
   return a;
  }

  AudioSource Spatial(string name, float min, float max) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   var a = go.AddComponent<AudioSource>();
   a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
   a.minDistance = min; a.maxDistance = max; a.dopplerLevel = 0f;
   return a;
  }

  // ---------- procedural clips ----------
  delegate float Generator(System.Random r, int i, ref State s);
  struct State { public float b0, b1, b2, b3, b4, b5, b6, brown, lp, hp, prev, env, gust, gustTarget, drop, dropEnv; public int dropTimer; }

  static float White(System.Random r) => (float)r.NextDouble() * 2f - 1f;

  static float Pink(System.Random r, ref State s) {
   float w = White(r);
   s.b0 = 0.99886f * s.b0 + w * 0.0555179f; s.b1 = 0.99332f * s.b1 + w * 0.0750759f; s.b2 = 0.969f * s.b2 + w * 0.153852f;
   s.b3 = 0.8665f * s.b3 + w * 0.3104856f; s.b4 = 0.55f * s.b4 + w * 0.5329522f; s.b5 = -0.7616f * s.b5 - w * 0.016898f;
   float p = s.b0 + s.b1 + s.b2 + s.b3 + s.b4 + s.b5 + s.b6 + w * 0.5362f;
   s.b6 = w * 0.115926f;
   return p * 0.11f;
  }

  static float Brown(System.Random r, ref State s) { s.brown = (s.brown + 0.02f * White(r)) / 1.02f; return s.brown * 3.5f; }

  static float WindSample(System.Random r, int i, ref State s) {
   if (i % 2205 == 0) s.gustTarget = 0.45f + (float)r.NextDouble() * 0.55f;
   s.gust += (s.gustTarget - s.gust) * 0.00012f;
   float n = Brown(r, ref s) * 0.7f + Pink(r, ref s) * 0.5f;
   s.lp += (n - s.lp) * 0.08f;
   return s.lp * (0.35f + s.gust * 0.65f) * 1.6f;
  }

  static float RainSample(System.Random r, int i, ref State s) {
   float w = White(r);
   s.hp = 0.85f * (s.hp + w - s.prev); s.prev = w;
   float hiss = s.hp * 0.22f;
   if (--s.dropTimer <= 0) { s.dropTimer = 40 + r.Next(220); s.dropEnv = 0.25f + (float)r.NextDouble() * 0.6f; }
   s.dropEnv *= 0.93f;
   return hiss + White(r) * s.dropEnv * 0.35f;
  }

  static float BrookSample(System.Random r, int i, ref State s) {
   float b = Brown(r, ref s);
   s.lp += (b - s.lp) * 0.25f;
   float band = b - s.lp * 0.6f;
   float t = i / (float)Rate;
   float burble = 0.6f + 0.25f * Mathf.Sin(t * 6.3f) * Mathf.Sin(t * 2.1f + 1.3f) + 0.15f * Mathf.Sin(t * 11.7f);
   return band * burble * 0.55f;
  }

  static float DroneSample(System.Random r, int i, ref State s) {
   float t = i / (float)Rate;
   float lfo = 0.7f + 0.3f * Mathf.Sin(t * 0.7f);
   float tone = Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.32f + Mathf.Sin(t * 2f * Mathf.PI * 58.27f) * 0.26f + Mathf.Sin(t * 2f * Mathf.PI * 110.6f) * 0.1f;
   s.lp += (Pink(r, ref s) - s.lp) * 0.02f;
   return (tone * lfo + s.lp * 0.8f) * 0.5f;
  }

  static AudioClip Loop(System.Random r, float seconds, Generator gen) {
   int n = Mathf.RoundToInt(seconds * Rate), fade = Rate / 2;
   var raw = new float[n + fade];
   var st = new State { gust = 0.7f, gustTarget = 0.7f };
   for (int i = 0; i < raw.Length; i++) raw[i] = gen(r, i, ref st);
   var data = new float[n];
   for (int i = 0; i < n; i++) data[i] = raw[i];
   for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = raw[i] * k + raw[n + i] * (1f - k); }
   Normalise(data, 0.7f);
   var clip = AudioClip.Create("procedural", n, 1, Rate, false);
   clip.SetData(data, 0);
   return clip;
  }

  static AudioClip Crickets(System.Random r, float seconds) {
   int n = Mathf.RoundToInt(seconds * Rate);
   var data = new float[n];
   float[] freqs = { 4300f, 4650f, 4950f, 4450f };
   float[] amps = { 0.5f, 0.35f, 0.25f, 0.18f };
   for (int c = 0; c < freqs.Length; c++) {
    float t = (float)r.NextDouble() * 0.6f;
    while (t < seconds - 0.3f) {
     int pulses = 3 + r.Next(2);
     for (int p = 0; p < pulses; p++) {
      int start = Mathf.RoundToInt((t + p * 0.042f) * Rate), len = Mathf.RoundToInt(0.018f * Rate);
      for (int k = 0; k < len && start + k < n; k++) {
       float env = Mathf.Sin(k / (float)len * Mathf.PI);
       data[start + k] += Mathf.Sin((start + k) / (float)Rate * 2f * Mathf.PI * freqs[c]) * env * amps[c];
      }
     }
     t += 0.55f + (float)r.NextDouble() * 0.7f;
    }
   }
   Normalise(data, 0.5f);
   var clip = AudioClip.Create("crickets", n, 1, Rate, false);
   clip.SetData(data, 0);
   return clip;
  }

  static AudioClip Thunder(System.Random r) {
   int n = Rate * 5;
   var data = new float[n];
   var st = new State();
   for (int i = 0; i < n; i++) {
    float t = i / (float)Rate;
    float crack = t < 0.12f ? White(r) * Mathf.Exp(-t * 28f) * 0.9f : 0f;
    float rumbleEnv = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t * 0.9f) * (0.75f + 0.25f * Mathf.Sin(t * 3.1f) * Mathf.Sin(t * 1.7f));
    float b = Brown(r, ref st);
    st.lp += (b - st.lp) * 0.04f;
    data[i] = crack + st.lp * rumbleEnv * 2.2f;
   }
   Normalise(data, 0.9f);
   var clip = AudioClip.Create("thunder", n, 1, Rate, false);
   clip.SetData(data, 0);
   return clip;
  }

  static void Normalise(float[] d, float peak) {
   float m = 0.0001f;
   for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
   float k = peak / m;
   for (int i = 0; i < d.Length; i++) d[i] *= k;
  }

  // ---------- surfaces & footsteps ----------
  public static Surface SurfaceAt(Vector3 pos) {
   if (OpenWorldTerrainSculptor.WaterAt(pos.x, pos.z, out float surface, out _) && pos.y < surface + 0.35f) return Surface.Water;
   var gm = OpenWorldManager.I;
   var terrain = gm ? gm.ActiveTerrain : Terrain.activeTerrain;
   if (!terrain) return Surface.Grass;
   float ground = terrain.SampleHeight(pos) + terrain.transform.position.y;
   if (pos.y > ground + 0.45f) return Surface.Wood; // decks, bridges and floors above the terrain
   var td = terrain.terrainData;
   if (!OpenWorldRealm.Instance || td.alphamapLayers < 8) return pos.z > 110f && !OpenWorldRealm.Instance ? Surface.Snow : Surface.Grass;
   Vector3 local = pos - terrain.transform.position;
   int ax = Mathf.Clamp(Mathf.FloorToInt(local.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
   int az = Mathf.Clamp(Mathf.FloorToInt(local.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
   var w = td.GetAlphamaps(ax, az, 1, 1);
   int best = 0; float bestW = -1f;
   for (int i = 0; i < 8; i++) if (w[0, 0, i] > bestW) { bestW = w[0, 0, i]; best = i; }
   switch (best) {
    case OpenWorldTerrainPainter.Forest: return Surface.Leaves;
    case OpenWorldTerrainPainter.Dirt: return OpenWorldAtmosphere.Instance && OpenWorldAtmosphere.Instance.LocalPrecipitation > 0.4f ? Surface.Mud : Surface.Dirt;
    case OpenWorldTerrainPainter.Cobble: return Surface.Tile;
    case OpenWorldTerrainPainter.Rock: return Surface.Gravel;
    case OpenWorldTerrainPainter.Cliff: return Surface.Rock;
    case OpenWorldTerrainPainter.Sand: return Surface.Sand;
    case OpenWorldTerrainPainter.Snow: return Surface.Snow;
    default: return Surface.Grass;
   }
  }

  static string StepGroup(Surface s) {
   switch (s) {
    case Surface.Leaves: return "step_leaves";
    case Surface.Dirt: return "step_dirt";
    case Surface.Gravel: return "step_gravel";
    case Surface.Tile: return "step_tile";
    case Surface.Rock: return "step_rock";
    case Surface.Sand: return "step_sand";
    case Surface.Snow: return "step_snow";
    case Surface.Water: return "step_water";
    case Surface.Wood: return "step_wood";
    case Surface.Mud: return "step_mud";
    default: return "step_grass";
   }
  }

  public static bool Footstep(Vector3 pos, bool running) {
   var s = Instance;
   var bank = OpenWorldSoundBank.Instance;
   if (!s || !bank || !s.steps) return false;
   var surface = SurfaceAt(pos);
   var clip = bank.Random(StepGroup(surface) + (running ? "_run" : "_walk")) ?? bank.Random(StepGroup(surface) + "_walk") ?? bank.Random("step_grass_walk");
   if (!clip) return false;
   s.steps.pitch = Random.Range(0.92f, 1.08f);
   s.steps.PlayOneShot(clip, (running ? 0.42f : 0.32f) * (surface == Surface.Water ? 1.2f : 1f));
   s.lastStep = surface + " " + clip.name;
   // Running dust and snow/sand puffs were removed by request; only wading splashes remain.
   if (surface == Surface.Water) OpenWorldVfx.FootPuff(pos, surface);
   else if (OpenWorldRegionWeather.Raining) OpenWorldWaterFx.RainSplash(pos); // wet footsteps in the rain
   return true;
  }

  // One-shot from a sound bank group ("prop_smash", "splash_big", "body_thud"...), quieter with distance
  // from the hero so far-off events stay in the background.
  public static bool Sfx(string group, Vector3 pos, float volume = 0.5f) {
   var s = Instance; var bank = OpenWorldSoundBank.Instance;
   if (!s || !bank || !s.steps) return false;
   var clip = bank.Random(group);
   if (!clip) return false;
   var gm = OpenWorldManager.I;
   float d = gm && gm.Hero ? Vector3.Distance(gm.Hero.transform.position, pos) : 0f;
   float v = volume * Mathf.Clamp01(1.15f - d / 32f);
   if (v <= 0.01f) return false;
   s.steps.pitch = Random.Range(0.93f, 1.07f);
   s.steps.PlayOneShot(clip, v);
   return true;
  }

  public static void Land(Vector3 pos) {
   var s = Instance; var bank = OpenWorldSoundBank.Instance;
   if (!s || !bank) return;
   var surface = SurfaceAt(pos);
   var clip = bank.Random(StepGroup(surface) + "_land") ?? bank.Random(StepGroup(surface) + "_run");
   if (clip) s.steps.PlayOneShot(clip, 0.5f);
  }

  Vector3 lastMountPos;
  bool wasMounted;
  float hoofTimer;
  int hoofPhase;
  int lastMeatAudio = -1;
  bool wasCookBuffAudio;

  void Hoofbeat(Vector3 pos, bool gallop) {
   var bank = OpenWorldSoundBank.Instance;
   if (!bank || !steps) return;
   var surface = SurfaceAt(pos);
   var clip = bank.Random(StepGroup(surface) + "_run") ?? bank.Random(StepGroup(surface) + "_walk") ?? bank.Random("step_dirt_run");
   if (!clip) return;
   steps.pitch = Random.Range(0.75f, 0.86f);
   steps.PlayOneShot(clip, (gallop ? 0.48f : 0.36f) * (surface == Surface.Water ? 1.2f : 1f));
   lastStep = "hoof_" + surface + " " + clip.name;
   if (surface == Surface.Water) OpenWorldVfx.FootPuff(pos, surface);
   else if (OpenWorldRegionWeather.Raining) OpenWorldWaterFx.RainSplash(pos); // wet hooves in the rain
  }

  // ---------- ambience ----------
  void Update() {
   var gm = OpenWorldManager.I;
   var atm = OpenWorldAtmosphere.Instance;
   if (!gm || !gm.Hero) return;
   Vector3 focus = gm.Hero.transform.position;
   float dt = Time.deltaTime;

   // Mounted horse hoofbeat cadence & hunting/cooking audio cues
   bool mounted = gm.Hero.IsMounted && gm.Hero.Mount;
   if (mounted) {
    Vector3 mPos = gm.Hero.Mount.transform.position;
    if (!wasMounted) {
     lastMountPos = mPos;
     hoofTimer = 0.08f;
     hoofPhase = 0;
     Hoofbeat(mPos, false);
    } else if (dt > 0.0001f) {
     float planarSpeed = new Vector2(mPos.x - lastMountPos.x, mPos.z - lastMountPos.z).magnitude / dt;
     lastMountPos = mPos;
     if (planarSpeed > 0.65f) {
      bool gallop = planarSpeed > 7.6f;
      hoofTimer -= dt;
      if (hoofTimer <= 0f) {
       Hoofbeat(mPos, gallop);
       hoofPhase = (hoofPhase + 1) & 1;
       hoofTimer = gallop ? (hoofPhase == 1 ? 0.11f : 0.24f) : 0.29f;
      }
     } else {
      hoofTimer = 0.06f;
     }
    }
   }
   wasMounted = mounted;

   bool mealBuff = gm.Hero.IsMealBuffActive;
   if (lastMeatAudio < 0) {
    lastMeatAudio = gm.RawMeat;
    wasCookBuffAudio = mealBuff;
   } else {
    if (gm.RawMeat > lastMeatAudio) Sfx("body_thud", focus, 0.35f);
    if (mealBuff && !wasCookBuffAudio) Sfx("fire_medium", focus, 0.55f);
    lastMeatAudio = gm.RawMeat;
    wasCookBuffAudio = mealBuff;
   }
   float frozen = 0f, dunes = 0f, grave = 0f, storm = 0f, verdant = 1f;
   if (OpenWorldRealm.Instance) {
    var w = alphaScratch;
    OpenWorldRegions.BiomeWeights(focus.x, focus.z, w);
    verdant = w[0]; frozen = w[1]; dunes = w[2]; grave = w[3]; storm = w[4];
   }
   bool night = atm && atm.CurrentPreset == OpenWorldAtmosphere.TimeOfDayPreset.StarlitNight;
   float rainK = atm ? atm.LocalPrecipitation : 0f;
   float stormK = atm ? atm.LocalStorm : 0f;
   float altitude = Mathf.Clamp01(focus.y / 190f); // wind picks up towards the high passes
   // Wind: stronger on heights, in the north and over the storm cliffs; gusts open the filter.
   gustPhase += dt * (0.18f + stormK * 0.3f);
   float gust = Mathf.PerlinNoise(gustPhase, 0.37f);
   float windTarget = 0.06f + 0.16f * frozen + 0.12f * dunes + 0.3f * stormK + altitude * 0.3f + rainK * 0.08f;
   Fade(wind, windTarget * (0.6f + gust * 0.8f), dt, 0.6f);
   if (windFilter) windFilter.cutoffFrequency = Mathf.Lerp(500f, 2600f, gust * (0.5f + stormK * 0.5f + frozen * 0.3f));
   Fade(rain, rainK * 0.5f, dt, 0.8f);
   float wd = OpenWorldRealm.Instance ? OpenWorldTerrainSculptor.DistanceToWater(focus.x, focus.z) : 99f;
   bool frozenWater = frozen > 0.6f;
   Fade(water, frozenWater ? 0f : Mathf.Pow(Mathf.Clamp01(1f - wd / 26f), 2f) * 0.45f, dt, 1.2f);
   Fade(crickets, night ? (verdant + grave * 0.6f) * (1f - rainK) * 0.22f : 0f, dt, 0.5f);
   Fade(drone, grave * 0.28f + stormK * 0.08f, dt, 0.4f);
   if (music) {
    music.volume = Mathf.MoveTowards(music.volume, musicBaseVolume * (1f - 0.55f * grave - 0.35f * stormK), dt * 0.2f);
    music.pitch = Mathf.MoveTowards(music.pitch, 1f - 0.07f * grave, dt * 0.05f);
    if (musicFilter) musicFilter.cutoffFrequency = Mathf.Lerp(22000f, 1400f, Mathf.Max(grave, stormK * 0.6f));
   }
   var bank = OpenWorldSoundBank.Instance;
   if (bank) {
    // Songbirds by day in the woods and meadows; crows over the marches and storm cliffs.
    if (Time.time >= nextBird) {
     nextBird = Time.time + Random.Range(2.5f, 7f);
     float chance = night ? 0f : (verdant + frozen * 0.3f) * (1f - rainK) * (1f - stormK);
     if (Random.value < chance) Call(bank.Random("birds"), focus, 0.55f);
    }
    if (Time.time >= nextCrow) {
     nextCrow = Time.time + Random.Range(6f, 15f);
     if (Random.value < grave * 0.9f + storm * 0.4f) Call(bank.Random("crows"), focus, 0.6f);
    }
    if (Time.time >= nextFireScan) { nextFireScan = Time.time + 0.6f; ScanFires(focus, bank); }
   }
  }

  static void Fade(AudioSource a, float target, float dt, float speed) {
   if (!a) return;
   a.volume = Mathf.MoveTowards(a.volume, Mathf.Clamp01(target), dt * speed * 0.5f);
  }

  void Call(AudioClip clip, Vector3 focus, float volume) {
   if (!clip) return;
   var a = calls[callIndex++ % calls.Length];
   float ang = Random.value * Mathf.PI * 2f, r = Random.Range(9f, 28f);
   a.transform.position = focus + new Vector3(Mathf.Cos(ang) * r, Random.Range(3f, 11f), Mathf.Sin(ang) * r);
   a.pitch = Random.Range(0.94f, 1.06f);
   a.PlayOneShot(clip, volume);
  }

  readonly System.Collections.Generic.List<(float d, Vector3 p)> fireScratch = new System.Collections.Generic.List<(float, Vector3)>();
  void ScanFires(Vector3 focus, OpenWorldSoundBank bank) {
   var realm = OpenWorldRealm.Instance;
   fireScratch.Clear();
   if (realm) foreach (var a in realm.LightAnchors) {
    if (a.color.r < 0.9f || a.color.g > 0.65f) continue; // fires are the orange anchors
    float d = (a.position - focus).sqrMagnitude;
    if (d < 24f * 24f) fireScratch.Add((d, a.position));
   }
   if (OpenWorldManager.I) foreach (var c in OpenWorldManager.I.Campfires) {
    if (!c) continue;
    float d = (c.transform.position - focus).sqrMagnitude;
    if (d >= 24f * 24f) continue;
    bool known = false; // realm hearths also register a light anchor at the same spot
    foreach (var f in fireScratch) if ((f.p - c.transform.position).sqrMagnitude < 9f) { known = true; break; }
    if (!known) fireScratch.Add((d, c.transform.position + Vector3.up * 0.5f));
   }
   fireScratch.Sort((x, y) => x.d.CompareTo(y.d));
   for (int i = 0; i < fires.Length; i++) {
    var src = fires[i];
    if (i < fireScratch.Count) {
     src.transform.position = fireScratch[i].p;
     if (!src.isPlaying) { src.clip = bank.Random(i == 0 ? "fire_medium" : "fire_small") ?? bank.Random("fire_small"); if (src.clip) { src.volume = 0.5f; src.time = Random.value * src.clip.length * 0.8f; src.Play(); } }
    } else if (src.isPlaying) src.Stop();
   }
  }

  string lastStep = "none";

  // Bridge helper: logs the ambience mix and the last footstep so audio can be checked from logs.
  public static void DebugReport(string arg) {
   var s = Instance;
   if (!s) { Debug.Log("[Soundscape] not active"); return; }
   string V(AudioSource a) => a ? a.volume.ToString("0.00") + (a.isPlaying ? "" : "(stopped)") : "-";
   int fires = 0; foreach (var f in s.fires) if (f && f.isPlaying) fires++;
   var bank = OpenWorldSoundBank.Instance;
   Debug.Log($"[Soundscape] {arg}: wind {V(s.wind)} rain {V(s.rain)} water {V(s.water)} crickets {V(s.crickets)} drone {V(s.drone)} fires {fires} bank {(bank ? bank.groups.Length + " groups" : "missing")} last step {s.lastStep}" +
             (OpenWorldManager.I && OpenWorldManager.I.Hero ? " surface here " + SurfaceAt(OpenWorldManager.I.Hero.transform.position) : ""));
  }

  void OnDestroy() { if (Instance == this) Instance = null; }
 }
}
