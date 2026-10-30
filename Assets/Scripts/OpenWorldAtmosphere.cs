using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Time of day, weather and per-biome atmosphere. A stylised sky (LostRealms/Sky) is kept in step
 // with the sun light, the distance fog fades the 1 km realm into the horizon colour, and the local
 // biome blends its own mood on top: crisp blue haze and snowfall in the Frozen Peaks, warm dust over
 // the Burning Dunes, dense green-grey fog and wisps in the Hollow Marches, rain and lightning over the
 // Stormveil Cliffs. The post-processing grade, ambient light and night lanterns follow the same blend.
 public sealed class OpenWorldAtmosphere : MonoBehaviour {
  public static OpenWorldAtmosphere Instance { get; private set; }

  public enum TimeOfDayPreset { SunlitDawn = 0, VibrantMidday = 1, GoldenHour = 2, StarlitNight = 3 }
  public enum WeatherPreset { ClearSky = 0, SpringRain = 1, Thunderstorm = 2, MysticMist = 3 }

  // Real-time clock (Genshin-style): a full day lasts DayLengthMinutes of play; the realm clock jumps to
  // any hour with a fast-forward of the sky, and can be locked (for testing or screenshots).
  const string HourPref = "LostRealms.Hour", LockPref = "LostRealms.TimeLocked";
  public const float MorningHour = 9f;
  public float Hour { get; private set; } = MorningHour;
  public bool TimeLocked { get; private set; }
  public float DayLengthMinutes = 24f;
  public bool FastForwarding { get; private set; }
  float ffFrom, ffSpan, ffElapsed, ffDuration, nextClockSave;
  // Phase weights of the current hour (0..1), used wherever night or golden light changes a look.
  float nightK, goldenK, dawnK, middayK;
  public float NightFactor => nightK;
  public TimeOfDayPreset CurrentPreset => nightK > 0.5f ? TimeOfDayPreset.StarlitNight : goldenK > 0.45f ? TimeOfDayPreset.GoldenHour : dawnK > 0.45f ? TimeOfDayPreset.SunlitDawn : TimeOfDayPreset.VibrantMidday;
  public WeatherPreset CurrentWeather { get; private set; } = WeatherPreset.ClearSky;
  public OWBiome LocalBiome { get; private set; } = OWBiome.Verdant;
  public float LocalStorm { get; private set; }
  public float LocalPrecipitation { get; private set; }

  struct Look {
   public Color zenith, horizon, ground, sun, cloudLit, cloudShade, ambSky, ambEquator, ambGround, skySun;
   public float sunIntensity, fogStart, fogEnd, legacyFogStart, legacyFogEnd, stars, cloudOpacity, cloudCoverage, lantern;
   public float sunSize, sunGlow, sunGlowPower, reflect;
  }

  struct Mood {
   public Color fogTint, skyTint, ambientTint, sunTint, moteA, moteB;
   public float fogNear, fogFar, sun, cloud, snow, dust, wisps, rain, storm, wind;
   public float temperature, saturation, contrast, vignette, exposure;
  }

  static readonly Mood[] Moods = {
   new Mood { // Verdant Heartlands
    fogTint = new Color(1f, 1f, 1f), skyTint = new Color(1f, 1f, 1f), ambientTint = new Color(1f, 1f, 1f), sunTint = new Color(1f, 1f, 1f),
    moteA = new Color(0.55f, 1f, 0.72f, 0.75f), moteB = new Color(1f, 0.92f, 0.5f, 0.8f),
    fogNear = 1f, fogFar = 1f, sun = 1f, cloud = 1f, wind = 0.45f, temperature = 0f, saturation = 2f, contrast = 6f, vignette = 0.15f, exposure = 0.05f },
   new Mood { // Frozen Peaks
    fogTint = new Color(0.94f, 1f, 1.1f), skyTint = new Color(0.9f, 0.98f, 1.08f), ambientTint = new Color(0.96f, 1.02f, 1.12f), sunTint = new Color(0.97f, 1f, 1.05f),
    moteA = new Color(0.8f, 0.95f, 1f, 0.85f), moteB = new Color(0.55f, 0.85f, 1f, 0.85f),
    fogNear = 0.72f, fogFar = 0.8f, sun = 1.05f, cloud = 1.25f, snow = 1f, wind = 0.8f, temperature = -7f, saturation = -6f, contrast = 8f, vignette = 0.15f, exposure = -0.3f }, // exposed for the snowfield, or it clips flat white
   new Mood { // Burning Dunes
    fogTint = new Color(1.14f, 0.98f, 0.8f), skyTint = new Color(1.06f, 1f, 0.9f), ambientTint = new Color(1.1f, 1f, 0.88f), sunTint = new Color(1.04f, 0.98f, 0.9f),
    moteA = new Color(1f, 0.82f, 0.5f, 0.55f), moteB = new Color(0.95f, 0.72f, 0.45f, 0.45f),
    fogNear = 0.85f, fogFar = 0.9f, sun = 1.12f, cloud = 0.35f, dust = 1f, wind = 0.7f, temperature = 7f, saturation = 2f, contrast = 9f, vignette = 0.18f, exposure = 0.04f },
   new Mood { // Hollow Marches
    fogTint = new Color(0.62f, 0.72f, 0.68f), skyTint = new Color(0.62f, 0.7f, 0.7f), ambientTint = new Color(0.78f, 0.86f, 0.84f), sunTint = new Color(0.8f, 0.9f, 0.88f),
    moteA = new Color(0.45f, 1f, 0.7f, 0.6f), moteB = new Color(0.62f, 0.85f, 1f, 0.5f),
    fogNear = 0.08f, fogFar = 0.26f, sun = 0.62f, cloud = 1.6f, wisps = 1f, wind = 0.3f, temperature = -4f, saturation = -26f, contrast = 12f, vignette = 0.3f, exposure = 0f },
   new Mood { // Stormveil Cliffs
    fogTint = new Color(0.58f, 0.64f, 0.78f), skyTint = new Color(0.5f, 0.58f, 0.72f), ambientTint = new Color(0.7f, 0.76f, 0.9f), sunTint = new Color(0.8f, 0.86f, 1f),
    moteA = new Color(0.55f, 0.78f, 1f, 0.85f), moteB = new Color(0.78f, 0.55f, 1f, 0.85f),
    fogNear = 0.4f, fogFar = 0.5f, sun = 0.52f, cloud = 2f, rain = 1f, storm = 1f, wind = 1.3f, temperature = -6f, saturation = -14f, contrast = 12f, vignette = 0.26f, exposure = 0f },
  };

  Light sunLight;
  Material skyMat;
  bool customSky;
  Look look;
  readonly float[] biomeTarget = { 1f, 0f, 0f, 0f, 0f };
  readonly float[] biome = { 1f, 0f, 0f, 0f, 0f };
  float nextBiomeSample;
  bool firstCompose = true;
  WindZone wind;
  static readonly int WindId = Shader.PropertyToID("_LR_Wind"), PlayerId = Shader.PropertyToID("_LR_PlayerPos");
  // Sky colours for reflective surfaces (LostRealms/Water reflects the sky without a probe re-bake).
  static readonly int SkyZenithId = Shader.PropertyToID("_LR_SkyZenith"), SkyHorizonId = Shader.PropertyToID("_LR_SkyHorizon");

  ParticleSystem moteSystem, rainSystem, snowSystem, sandSystem;
  Material moteMat, rainMat, snowMat, sandMat;
  Texture2D moteTex;

  // Lightning
  float nextLightning = 6f, flashEnd, nextPulse, flash;
  float nextAmbientRefresh;
  Color lastAmbient = new Color(-1f, -1f, -1f);
  int flashPulses;

  // Lanterns
  readonly List<Light> staticLanterns = new List<Light>();
  readonly List<Light> lanternPool = new List<Light>();
  float nextLanternRefresh;
  float lanternLevel;

  static readonly int SunDirId = Shader.PropertyToID("_LR_SunDir");
  static readonly int MoonDirId = Shader.PropertyToID("_LR_MoonDir");

  public string PresetLabel {
   get {
    string w = CurrentWeather == WeatherPreset.SpringRain ? " • RAIN" :
     CurrentWeather == WeatherPreset.Thunderstorm ? " • STORM" :
     CurrentWeather == WeatherPreset.MysticMist ? " • MIST" : "";
    return ClockText(Hour) + " " + PeriodName(Hour).ToUpperInvariant() + w + (TimeLocked ? " • LOCKED" : "");
   }
  }

  public static string ClockText(float hour) {
   int minutes = Mathf.FloorToInt(Wrap24(hour) * 60f + 0.001f) % (24 * 60);
   return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
  }

  public static string PeriodName(float hour) {
   float h = Wrap24(hour);
   if (h < 4.7f || h >= 20.4f) return "Night";
   if (h < 5.9f) return "Daybreak";
   if (h < 7.6f) return "Dawn";
   if (h < 10.8f) return "Morning";
   if (h < 15.2f) return "Midday";
   if (h < 17.9f) return "Golden hour";
   if (h < 19.1f) return "Dusk";
   return "Twilight";
  }

  public static float Wrap24(float h) { h %= 24f; return h < 0f ? h + 24f : h; }

  public string WeatherLabel {
   get {
    switch (CurrentWeather) {
     case WeatherPreset.SpringRain: return "SPRING RAIN";
     case WeatherPreset.Thunderstorm: return "THUNDERSTORM";
     case WeatherPreset.MysticMist: return "MYSTIC MIST";
     default: return "CLEAR SKY";
    }
   }
  }

  public static OpenWorldAtmosphere Attach(Transform worldRoot) {
   var go = new GameObject("OpenWorldAtmosphere");
   go.transform.SetParent(worldRoot, false);
   var atm = go.AddComponent<OpenWorldAtmosphere>();
   atm.Initialize(worldRoot);
   return atm;
  }

  // Bridge / debug entry point: "night", "golden rain", "2 storm", "9:30", "21.5 lock", "unlock", ...
  public static void DebugSetPreset(string arg) {
   var atm = Instance;
   if (!atm || string.IsNullOrEmpty(arg)) return;
   foreach (var token in arg.ToLowerInvariant().Split(' ')) {
    if (token.Contains(":") || token.Contains(".")) {
     var hm = token.Split(':');
     if (hm.Length == 2 && int.TryParse(hm[0], out int hh) && int.TryParse(hm[1], out int mm)) atm.SetHour(hh + mm / 60f, false);
     else if (float.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hf)) atm.SetHour(hf, false);
     continue;
    }
    switch (token) {
     case "lock": atm.SetTimeLocked(true); break;
     case "unlock": atm.SetTimeLocked(false); break;
     case "0": case "dawn": atm.ApplyPreset(TimeOfDayPreset.SunlitDawn, false); break;
     case "1": case "midday": case "day": atm.ApplyPreset(TimeOfDayPreset.VibrantMidday, false); break;
     case "2": case "golden": case "dusk": atm.ApplyPreset(TimeOfDayPreset.GoldenHour, false); break;
     case "3": case "night": atm.ApplyPreset(TimeOfDayPreset.StarlitNight, false); break;
     case "clear": atm.ApplyWeather(WeatherPreset.ClearSky); break;
     case "rain": atm.ApplyWeather(WeatherPreset.SpringRain); break;
     case "storm": atm.ApplyWeather(WeatherPreset.Thunderstorm); break;
     case "mist": atm.ApplyWeather(WeatherPreset.MysticMist); break;
    }
   }
   atm.firstCompose = true;
   atm.Compose(0f);
  }

  void Initialize(Transform worldRoot) {
   Instance = this;
   // A fresh start begins in the morning; afterwards the saved clock carries on where play stopped.
   Hour = Wrap24(PlayerPrefs.GetFloat(HourPref, MorningHour));
   TimeLocked = PlayerPrefs.GetInt(LockPref, 0) == 1;

   foreach (var candidate in FindObjectsByType<Light>()) {
    if (candidate.type == LightType.Directional) { sunLight = candidate; break; }
   }
   if (!sunLight || sunLight.type != LightType.Directional) {
    var lgo = new GameObject("SunLight");
    lgo.transform.SetParent(worldRoot, false);
    sunLight = lgo.AddComponent<Light>();
    sunLight.type = LightType.Directional;
   }
   sunLight.shadows = LightShadows.Soft;
   sunLight.shadowStrength = 0.86f;

   BuildSky();
   BuildWind(worldRoot);
   moteTex = SoftDot(32);
   BuildAmbientMotes(worldRoot);
   BuildRainSystem(worldRoot);
   BuildSnowSystem(worldRoot);
   BuildSandSystem(worldRoot);
   SpawnLanterns(worldRoot);
   OpenWorldPostFX.Attach(worldRoot);
   SampleBiome(Vector3.zero);
   RenderSettings.ambientMode = AmbientMode.Trilight;
   RenderSettings.fog = true;
   RenderSettings.fogMode = FogMode.Linear;
   SetHour(Hour, false, false);
   ApplyWeather(WeatherPreset.ClearSky);
  }

  // Steps to the next period (dawn, midday, golden hour, night) with the fast-forward.
  public void CycleTimeOfDay() {
   int next = ((int)CurrentPreset + 1) % 4;
   SetHour(PresetHour((TimeOfDayPreset)next), true);
  }

  public static float PresetHour(TimeOfDayPreset preset) =>
   preset == TimeOfDayPreset.SunlitDawn ? 6.5f : preset == TimeOfDayPreset.GoldenHour ? 17.25f : preset == TimeOfDayPreset.StarlitNight ? 22.5f : 12f;

  // Sets the realm clock. Animated changes always run forward through the day (the sky turns over
  // the intervening hours in 0.8-3 s, like Genshin's clock); instant changes jump straight there.
  public void SetHour(float hour, bool animate, bool save = true) {
   hour = Wrap24(hour);
   if (animate) {
    float span = Wrap24(hour - Hour);
    if (span < 0.01f) return;
    ffFrom = Hour; ffSpan = span; ffElapsed = 0f;
    ffDuration = Mathf.Clamp(span / 8f, 0.8f, 3f);
    FastForwarding = true;
    return;
   }
   FastForwarding = false;
   Hour = hour;
   firstCompose = true;
   Compose(0f);
   DynamicGI.UpdateEnvironment();
   if (save) SaveClock();
  }

  public void SetTimeLocked(bool locked) {
   TimeLocked = locked;
   SaveClock();
  }

  void SaveClock() {
   PlayerPrefs.SetFloat(HourPref, Hour);
   PlayerPrefs.SetInt(LockPref, TimeLocked ? 1 : 0);
   PlayerPrefs.Save();
   nextClockSave = Time.unscaledTime + 20f;
  }

  void AdvanceClock() {
   if (FastForwarding) {
    ffElapsed += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(ffElapsed / Mathf.Max(0.01f, ffDuration));
    float eased = t * t * (3f - 2f * t);
    Hour = Wrap24(ffFrom + ffSpan * eased);
    if (t >= 1f) { FastForwarding = false; SaveClock(); }
    nextAmbientRefresh = Mathf.Min(nextAmbientRefresh, Time.unscaledTime + 0.2f);
    return;
   }
   if (!TimeLocked && DayLengthMinutes > 0.01f) Hour = Wrap24(Hour + Time.deltaTime * 24f / (DayLengthMinutes * 60f));
   if (Time.unscaledTime >= nextClockSave) SaveClock();
  }

  void OnApplicationPause(bool paused) { if (paused) SaveClock(); }

  public void CycleWeather() {
   int next = ((int)CurrentWeather + 1) % 4;
   ApplyWeather((WeatherPreset)next);
  }

  public void ApplyWeather(WeatherPreset weather) {
   CurrentWeather = weather;
   Compose(0f);
  }

  public void ApplyPreset(TimeOfDayPreset preset, bool savePref = true) => SetHour(PresetHour(preset), false, savePref);

  // ---------- the day as keyframes ----------
  enum Key { Night, Daybreak, Dawn, Morning, Midday, Golden, Dusk, Twilight }
  static readonly (float hour, Key key)[] Track = {
   (0f, Key.Night), (4.7f, Key.Night), (5.4f, Key.Daybreak), (6.3f, Key.Dawn), (8.2f, Key.Morning), (10.8f, Key.Midday),
   (15.2f, Key.Midday), (17.0f, Key.Golden), (18.5f, Key.Dusk), (19.4f, Key.Twilight), (20.4f, Key.Night), (24f, Key.Night)
  };
  static Look[] keyLooks;

  static Look LookAt(float hour) {
   if (keyLooks == null) { keyLooks = new Look[8]; for (int i = 0; i < 8; i++) keyLooks[i] = KeyLook((Key)i); }
   float h = Wrap24(hour);
   for (int i = 0; i < Track.Length - 1; i++) {
    if (h > Track[i + 1].hour) continue;
    float t = Mathf.InverseLerp(Track[i].hour, Track[i + 1].hour, h);
    return Blend(keyLooks[(int)Track[i].key], keyLooks[(int)Track[i + 1].key], t * t * (3f - 2f * t));
   }
   return keyLooks[(int)Key.Night];
  }

  static Look Blend(in Look a, in Look b, float t) => new Look {
   zenith = Color.Lerp(a.zenith, b.zenith, t), horizon = Color.Lerp(a.horizon, b.horizon, t), ground = Color.Lerp(a.ground, b.ground, t),
   sun = Color.Lerp(a.sun, b.sun, t), cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, t), cloudShade = Color.Lerp(a.cloudShade, b.cloudShade, t),
   ambSky = Color.Lerp(a.ambSky, b.ambSky, t), ambEquator = Color.Lerp(a.ambEquator, b.ambEquator, t), ambGround = Color.Lerp(a.ambGround, b.ambGround, t),
   skySun = Color.Lerp(a.skySun, b.skySun, t),
   sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t), fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t), fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
   legacyFogStart = Mathf.Lerp(a.legacyFogStart, b.legacyFogStart, t), legacyFogEnd = Mathf.Lerp(a.legacyFogEnd, b.legacyFogEnd, t),
   stars = Mathf.Lerp(a.stars, b.stars, t), cloudOpacity = Mathf.Lerp(a.cloudOpacity, b.cloudOpacity, t), cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t),
   lantern = Mathf.Lerp(a.lantern, b.lantern, t), sunSize = Mathf.Lerp(a.sunSize, b.sunSize, t), sunGlow = Mathf.Lerp(a.sunGlow, b.sunGlow, t),
   sunGlowPower = Mathf.Lerp(a.sunGlowPower, b.sunGlowPower, t), reflect = Mathf.Lerp(a.reflect, b.reflect, t)
  };

  static Look KeyLook(Key key) {
   switch (key) {
    case Key.Night:
     // Moonlit but readable: paths, silhouettes and faces stay legible on a phone screen.
     return new Look {
      zenith = new Color(0.02f, 0.035f, 0.09f), horizon = new Color(0.08f, 0.13f, 0.24f), ground = new Color(0.035f, 0.045f, 0.07f),
      sun = new Color(0.58f, 0.7f, 1f), sunIntensity = 0.62f, skySun = new Color(0.02f, 0.025f, 0.04f),
      cloudLit = new Color(0.26f, 0.3f, 0.4f), cloudShade = new Color(0.07f, 0.09f, 0.13f),
      ambSky = new Color(0.16f, 0.22f, 0.38f), ambEquator = new Color(0.12f, 0.17f, 0.28f), ambGround = new Color(0.06f, 0.07f, 0.1f),
      fogStart = 140f, fogEnd = 820f, legacyFogStart = 58f, legacyFogEnd = 240f, stars = 1.6f, cloudOpacity = 0.45f, cloudCoverage = 0.55f, lantern = 2.4f,
      sunSize = 0.004f, sunGlow = 0.1f, sunGlowPower = 4f, reflect = 0.2f };
    case Key.Daybreak:
     return new Look {
      zenith = new Color(0.07f, 0.11f, 0.25f), horizon = new Color(0.42f, 0.4f, 0.58f), ground = new Color(0.1f, 0.1f, 0.14f),
      sun = new Color(0.66f, 0.72f, 1f), sunIntensity = 0.5f, skySun = new Color(0.75f, 0.42f, 0.38f),
      cloudLit = new Color(0.5f, 0.46f, 0.6f), cloudShade = new Color(0.15f, 0.16f, 0.24f),
      ambSky = new Color(0.22f, 0.27f, 0.44f), ambEquator = new Color(0.26f, 0.26f, 0.38f), ambGround = new Color(0.08f, 0.08f, 0.11f),
      fogStart = 160f, fogEnd = 900f, legacyFogStart = 62f, legacyFogEnd = 250f, stars = 0.5f, cloudOpacity = 0.5f, cloudCoverage = 0.52f, lantern = 1.8f,
      sunSize = 0.03f, sunGlow = 0.9f, sunGlowPower = 4f, reflect = 0.4f };
    case Key.Dawn:
     return new Look {
      zenith = new Color(0.32f, 0.46f, 0.74f), horizon = new Color(0.98f, 0.76f, 0.62f), ground = new Color(0.42f, 0.38f, 0.36f),
      sun = new Color(1f, 0.8f, 0.62f), sunIntensity = 1.15f, skySun = new Color(1f, 0.8f, 0.62f),
      cloudLit = new Color(1f, 0.86f, 0.76f), cloudShade = new Color(0.56f, 0.56f, 0.66f),
      ambSky = new Color(0.42f, 0.5f, 0.66f), ambEquator = new Color(0.78f, 0.66f, 0.6f), ambGround = new Color(0.26f, 0.24f, 0.22f),
      fogStart = 200f, fogEnd = 1150f, legacyFogStart = 70f, legacyFogEnd = 285f, stars = 0.12f, cloudOpacity = 0.55f, cloudCoverage = 0.5f, lantern = 0.8f,
      sunSize = 0.042f, sunGlow = 1.5f, sunGlowPower = 5f, reflect = 0.85f };
    case Key.Morning:
     return new Look {
      zenith = new Color(0.24f, 0.45f, 0.8f), horizon = new Color(0.8f, 0.82f, 0.86f), ground = new Color(0.42f, 0.44f, 0.42f),
      sun = new Color(1f, 0.92f, 0.8f), sunIntensity = 1.22f, skySun = new Color(1f, 0.92f, 0.8f),
      cloudLit = new Color(1f, 0.97f, 0.93f), cloudShade = new Color(0.62f, 0.68f, 0.78f),
      ambSky = new Color(0.42f, 0.54f, 0.74f), ambEquator = new Color(0.66f, 0.68f, 0.68f), ambGround = new Color(0.25f, 0.25f, 0.22f),
      fogStart = 240f, fogEnd = 1300f, legacyFogStart = 86f, legacyFogEnd = 320f, stars = 0f, cloudOpacity = 0.55f, cloudCoverage = 0.52f, lantern = 0f,
      sunSize = 0.034f, sunGlow = 1f, sunGlowPower = 7f, reflect = 0.95f };
    case Key.Golden:
     return new Look {
      zenith = new Color(0.24f, 0.3f, 0.56f), horizon = new Color(0.98f, 0.7f, 0.5f), ground = new Color(0.36f, 0.3f, 0.26f),
      sun = new Color(1f, 0.64f, 0.34f), sunIntensity = 1.2f, skySun = new Color(1f, 0.64f, 0.34f),
      cloudLit = new Color(1f, 0.74f, 0.5f), cloudShade = new Color(0.48f, 0.42f, 0.52f),
      ambSky = new Color(0.36f, 0.4f, 0.6f), ambEquator = new Color(0.86f, 0.6f, 0.42f), ambGround = new Color(0.26f, 0.2f, 0.18f),
      fogStart = 180f, fogEnd = 1050f, legacyFogStart = 68f, legacyFogEnd = 270f, stars = 0.08f, cloudOpacity = 0.6f, cloudCoverage = 0.48f, lantern = 0.5f,
      sunSize = 0.042f, sunGlow = 1.5f, sunGlowPower = 5f, reflect = 0.8f };
    case Key.Dusk:
     return new Look {
      zenith = new Color(0.16f, 0.18f, 0.4f), horizon = new Color(0.92f, 0.52f, 0.42f), ground = new Color(0.25f, 0.2f, 0.22f),
      sun = new Color(1f, 0.52f, 0.34f), sunIntensity = 0.8f, skySun = new Color(1f, 0.45f, 0.28f),
      cloudLit = new Color(0.98f, 0.6f, 0.5f), cloudShade = new Color(0.36f, 0.3f, 0.42f),
      ambSky = new Color(0.3f, 0.3f, 0.5f), ambEquator = new Color(0.62f, 0.42f, 0.4f), ambGround = new Color(0.16f, 0.13f, 0.14f),
      fogStart = 160f, fogEnd = 950f, legacyFogStart = 62f, legacyFogEnd = 255f, stars = 0.25f, cloudOpacity = 0.6f, cloudCoverage = 0.5f, lantern = 1.8f,
      sunSize = 0.046f, sunGlow = 1.9f, sunGlowPower = 4f, reflect = 0.6f };
    case Key.Twilight:
     return new Look {
      zenith = new Color(0.05f, 0.07f, 0.18f), horizon = new Color(0.3f, 0.25f, 0.42f), ground = new Color(0.07f, 0.07f, 0.1f),
      sun = new Color(0.62f, 0.68f, 1f), sunIntensity = 0.52f, skySun = new Color(0.42f, 0.2f, 0.22f),
      cloudLit = new Color(0.36f, 0.32f, 0.46f), cloudShade = new Color(0.1f, 0.1f, 0.16f),
      ambSky = new Color(0.18f, 0.21f, 0.37f), ambEquator = new Color(0.2f, 0.19f, 0.31f), ambGround = new Color(0.07f, 0.07f, 0.1f),
      fogStart = 150f, fogEnd = 860f, legacyFogStart = 60f, legacyFogEnd = 245f, stars = 1f, cloudOpacity = 0.5f, cloudCoverage = 0.53f, lantern = 2.2f,
      sunSize = 0.02f, sunGlow = 0.7f, sunGlowPower = 3f, reflect = 0.3f };
    default: // Midday
     return new Look {
      zenith = new Color(0.18f, 0.42f, 0.82f), horizon = new Color(0.68f, 0.82f, 0.92f), ground = new Color(0.42f, 0.46f, 0.46f),
      sun = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.25f, skySun = new Color(1f, 0.96f, 0.88f),
      cloudLit = Color.white, cloudShade = new Color(0.66f, 0.74f, 0.84f),
      ambSky = new Color(0.42f, 0.56f, 0.78f), ambEquator = new Color(0.6f, 0.68f, 0.7f), ambGround = new Color(0.24f, 0.26f, 0.22f),
      fogStart = 260f, fogEnd = 1400f, legacyFogStart = 92f, legacyFogEnd = 330f, stars = 0f, cloudOpacity = 0.55f, cloudCoverage = 0.52f, lantern = 0f,
      sunSize = 0.03f, sunGlow = 0.8f, sunGlowPower = 9f, reflect = 1f };
   }
  }

  // The sky of an hour as one colour, for the clock dial ring (night kept readable).
  public static Color DialColor(float hour) {
   var l = LookAt(hour);
   var c = Color.Lerp(l.horizon, l.zenith, 0.45f);
   float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
   if (lum < 0.2f) c = Color.Lerp(c, new Color(0.17f, 0.21f, 0.42f), 0.55f);
   return new Color(c.r, c.g, c.b, 1f);
  }

  static float Smooth(float e0, float e1, float x) { float u = Mathf.Clamp01((x - e0) / (e1 - e0)); return u * u * (3f - 2f * u); }

  // Weights of the named periods at an hour; they drive the grade, lanterns and night-only effects.
  void UpdatePhase(float hour) {
   float h = Wrap24(hour);
   nightK = Mathf.Max(Smooth(18.6f, 20.3f, h), 1f - Smooth(4.6f, 6.2f, h));
   dawnK = Smooth(4.8f, 5.9f, h) * (1f - Smooth(7.0f, 8.6f, h)) * (1f - nightK);
   goldenK = Smooth(15.6f, 17.0f, h) * (1f - Smooth(18.3f, 19.4f, h)) * (1f - nightK);
   middayK = Mathf.Clamp01(1f - nightK - dawnK - goldenK);
  }

  // Sun arc: rises in the east around 05:55, culminates 64 degrees up in the south at 12:30 and sets
  // in the west at 19:00, sinking on below the horizon for the twilight glow. The moon crosses the
  // southern sky from 19:30 to 05:30.
  static Vector3 SunVector(float hour, out float elevation) {
   float h = Wrap24(hour);
   const float rise = 5.9f, set = 19f;
   float az;
   if (h >= rise && h <= set) {
    float t = (h - rise) / (set - rise);
    float s = Mathf.Sin(Mathf.PI * t);
    elevation = 64f * s * s;
    az = Mathf.Lerp(80f, 280f, t);
   } else {
    float after = Wrap24(h - set), before = Wrap24(rise - h);
    elevation = -Mathf.Min(25f, Mathf.Min(after, before) * 13f);
    az = after < before ? 280f : 80f;
   }
   return Polar(az * Mathf.Deg2Rad, elevation);
  }

  static Vector3 MoonVector(float hour, out float elevation) {
   float t = Wrap24(hour - 19.5f) / 10f; // 0 at 19:30, 1 at 05:30
   float az;
   if (t <= 1f) { elevation = 50f * Mathf.Sin(Mathf.PI * t); az = Mathf.Lerp(100f, 260f, t); }
   else { elevation = -12f; az = t < 1.7f ? 260f : 100f; }
   return Polar(az * Mathf.Deg2Rad, elevation);
  }

  static Vector3 Polar(float azimuthRad, float elevationDeg) {
   float el = elevationDeg * Mathf.Deg2Rad;
   return new Vector3(Mathf.Sin(azimuthRad) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(azimuthRad) * Mathf.Cos(el));
  }

  static Vector3 WithMinElevation(Vector3 dir, float minDeg) {
   float min = Mathf.Sin(minDeg * Mathf.Deg2Rad);
   if (dir.y >= min) return dir;
   var flat = new Vector3(dir.x, 0f, dir.z);
   if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
   return (flat.normalized * Mathf.Cos(minDeg * Mathf.Deg2Rad) + Vector3.up * min).normalized;
  }

  // ---------- per-frame composition ----------
  void Compose(float dt) {
   bool realm = OpenWorldRealm.Instance;
   look = LookAt(Hour);
   UpdatePhase(Hour);
   float nk = nightK;
   // Smooth the biome blend so crossing a border is a slow change of mood.
   float k = firstCompose ? 1f : 1f - Mathf.Exp(-dt * 0.9f);
   for (int i = 0; i < 5; i++) biome[i] = Mathf.Lerp(biome[i], biomeTarget[i], k);
   var m = BlendMood();
   float weatherRain = CurrentWeather == WeatherPreset.SpringRain ? 0.7f : CurrentWeather == WeatherPreset.Thunderstorm ? 1f : 0f;
   float mist = CurrentWeather == WeatherPreset.MysticMist ? 1f : 0f;
   float dry = biome[(int)OWBiome.Dunes];
   float frozen = biome[(int)OWBiome.Frozen];
   float rain = Mathf.Clamp01(Mathf.Max(weatherRain, m.rain) * (1f - frozen) * (1f - dry * 0.92f));
   float storm = Mathf.Clamp01(Mathf.Max(CurrentWeather == WeatherPreset.Thunderstorm ? 1f : 0f, m.storm) * (1f - dry));
   LocalPrecipitation = rain; LocalStorm = storm;
   float grey = Mathf.Clamp01(Mathf.Max(rain * 0.8f, storm * 0.9f));
   var stormGrey = new Color(0.36f, 0.42f, 0.5f);

   Color horizon = Mul(look.horizon, m.skyTint), zenith = Mul(look.zenith, m.skyTint);
   horizon = Color.Lerp(horizon, stormGrey * Mathf.Lerp(1f, 0.25f, nk), grey * 0.55f);
   zenith = Color.Lerp(zenith, stormGrey * Mathf.Lerp(0.8f, 0.2f, nk), grey * 0.6f);
   Color fogColor = Mul(horizon, m.fogTint) * 0.97f;
   float fogLum = fogColor.r * 0.3f + fogColor.g * 0.59f + fogColor.b * 0.11f;
   fogColor = Color.Lerp(fogColor, new Color(fogLum, fogLum, fogLum), 0.22f);
   if (mist > 0f) fogColor = Color.Lerp(fogColor, new Color(0.72f, 0.82f, 0.84f) * Mathf.Lerp(1f, 0.3f, nk), 0.45f);

   float fogStart = (realm ? look.fogStart : look.legacyFogStart) * m.fogNear * (1f - 0.35f * rain) * (1f - 0.55f * mist);
   float fogEnd = (realm ? look.fogEnd : look.legacyFogEnd) * m.fogFar * (1f - 0.3f * rain) * (1f - 0.45f * mist);
   fogEnd = Mathf.Max(fogStart + 40f, fogEnd);

   // Lightning flashes over the Stormveil Cliffs and during thunderstorms.
   UpdateLightning(storm, dt);
   float sunIntensity = look.sunIntensity * m.sun * (1f - 0.35f * grey) + flash * 1.6f;
   Color sunColor = Color.Lerp(Mul(look.sun, m.sunTint), new Color(0.75f, 0.85f, 1f), flash > 0f ? 0.7f : 0f);

   // The sun owns the light above the horizon and the moon after dark; around sunrise and sunset the
   // light swings between them while both are dim. Neither light ever grazes the ground.
   Vector3 toSun = SunVector(Hour, out float sunElevation), toMoon = MoonVector(Hour, out _);
   float sunOwns = Smooth(-1.5f, 4f, sunElevation);
   Vector3 toLight = Vector3.Slerp(WithMinElevation(toMoon, 14f), WithMinElevation(toSun, 4f), sunOwns).normalized;
   if (sunLight) {
    sunLight.color = sunColor;
    sunLight.intensity = sunIntensity;
    sunLight.transform.rotation = Quaternion.LookRotation(-toLight, Vector3.up);
    sunLight.shadowStrength = Mathf.Lerp(0.86f, 0.55f, grey) * Mathf.Lerp(1f, 0.8f, nk);
   }
   float ambientScale = (1f - 0.2f * grey) + flash * 0.8f;
   RenderSettings.ambientSkyColor = Mul(look.ambSky, m.ambientTint) * ambientScale;
   RenderSettings.ambientEquatorColor = Mul(look.ambEquator, m.ambientTint) * ambientScale;
   RenderSettings.ambientGroundColor = Mul(look.ambGround, m.ambientTint) * ambientScale;
   RenderSettings.fogColor = fogColor;
   RenderSettings.fogStartDistance = fogStart;
   RenderSettings.fogEndDistance = fogEnd;
   // Environment reflections were captured in daylight; dim them with the sky so glossy leaves,
   // water and metal do not glow at night or under storm cover.
   RenderSettings.reflectionIntensity = look.reflect * (1f - 0.45f * grey) + flash * 0.5f;
   // The ambient probe is rebuilt from the trilight colours when they drift noticeably.
   var ambientNow = RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor + RenderSettings.ambientGroundColor;
   if ((firstCompose || Time.unscaledTime >= nextAmbientRefresh) && (Mathf.Abs(ambientNow.r - lastAmbient.r) + Mathf.Abs(ambientNow.g - lastAmbient.g) + Mathf.Abs(ambientNow.b - lastAmbient.b) > 0.03f || firstCompose)) {
    lastAmbient = ambientNow;
    nextAmbientRefresh = Time.unscaledTime + 1.5f;
    DynamicGI.UpdateEnvironment();
   }

   float skyGlow = 1f + flash * 1.4f;
   Shader.SetGlobalColor(SkyZenithId, new Color(zenith.r * skyGlow, zenith.g * skyGlow, zenith.b * skyGlow, 1f));
   Shader.SetGlobalColor(SkyHorizonId, new Color(fogColor.r * skyGlow, fogColor.g * skyGlow, fogColor.b * skyGlow, 1f));
   if (skyMat) {
    if (customSky) {
     skyMat.SetColor("_ZenithColor", zenith);
     skyMat.SetColor("_HorizonColor", fogColor);
     skyMat.SetColor("_GroundColor", Color.Lerp(Mul(look.ground, m.skyTint), fogColor, 0.5f));
     skyMat.SetColor("_SunColor", Mul(look.skySun, m.sunTint) * (1f - grey * 0.8f));
     skyMat.SetFloat("_SunSize", look.sunSize);
     skyMat.SetFloat("_SunGlow", look.sunGlow);
     skyMat.SetFloat("_SunGlowPower", look.sunGlowPower);
     skyMat.SetColor("_CloudColor", Color.Lerp(look.cloudLit, look.cloudShade, grey * 0.6f));
     skyMat.SetColor("_CloudShade", Color.Lerp(look.cloudShade, stormGrey * 0.5f * Mathf.Lerp(1f, 0.3f, nk), grey));
     skyMat.SetFloat("_CloudOpacity", Mathf.Clamp01(look.cloudOpacity * m.cloud + grey * 0.45f));
     skyMat.SetFloat("_CloudCoverage", Mathf.Clamp01(look.cloudCoverage - grey * 0.28f - (m.cloud - 1f) * 0.08f));
     skyMat.SetFloat("_Exposure", 1f + flash * 1.4f);
     skyMat.SetFloat("_StarIntensity", look.stars * (1f - grey));
     Shader.SetGlobalVector(SunDirId, toSun);
     Shader.SetGlobalVector(MoonDirId, toMoon);
    } else {
     if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", zenith);
     if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", horizon);
     if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", Mathf.Lerp(1.15f, 0.45f, nk) + flash);
    }
   }
   var cam = Camera.main;
   if (cam) cam.backgroundColor = fogColor;

   // Wind follows the biome (vegetation shaders and particles read the WindZone).
   if (wind) {
    float w = m.wind * (1f + rain * 0.6f + storm * 0.8f);
    wind.windMain = w;
    wind.windTurbulence = 0.35f + w * 0.4f;
    wind.windPulseMagnitude = 0.3f + storm * 0.5f;
    // The foliage shader sways along the zone's heading; strength rises with the biome and gusts in storms.
    var heading = wind.transform.forward;
    float gusting = 1f + Mathf.Sin(Time.time * 0.37f) * (0.12f + storm * 0.3f);
    Shader.SetGlobalVector(WindId, new Vector4(heading.x, heading.z, Mathf.Clamp(w / 0.45f, 0.35f, 2.6f) * gusting, 1f));
   }
   // Grass and bushes part around the hero.
   var heroNow = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (heroNow) { var hp = heroNow.transform.position; Shader.SetGlobalVector(PlayerId, new Vector4(hp.x, hp.y, hp.z, 1.35f)); }

   // Particles.
   UpdateWeatherParticles(m, rain, weatherRain, frozen, dry, nk);

   // Lanterns.
   lanternLevel = look.lantern + (grey > 0.4f ? 0.9f * (1f - nk) : 0f) + biome[(int)OWBiome.Graveyard] * 0.8f;

   // Post grade.
   var post = OpenWorldPostFX.Instance;
   if (post) {
    var g = OpenWorldPostFX.Grade.Neutral;
    g.exposure = m.exposure + 0.2f * nk + flash * 0.6f;
    g.contrast = m.contrast + grey * 4f;
    g.saturation = m.saturation - grey * 10f - 6f * nk;
    g.temperature = m.temperature + 4f * goldenK + 3f * dawnK - 8f * nk;
    g.vignette = m.vignette + 0.08f * nk;
    g.bloom = Mathf.Lerp(Mathf.Lerp(0.45f, 0.3f, middayK), 0.6f, nk);
    g.splitHighlights = Color.Lerp(Color.Lerp(new Color(1f, 0.95f, 0.88f), new Color(1f, 0.9f, 0.78f), goldenK), new Color(0.82f, 0.88f, 1f), nk);
    g.splitShadows = Color.Lerp(new Color(0.52f, 0.57f, 0.64f), new Color(0.42f, 0.46f, 0.62f), nk);
    g.splitBalance = -18f;
    post.SetGrade(g);
   }
   firstCompose = false;
  }

  Mood BlendMood() {
   var r = new Mood();
   Color fog = Color.clear, sky = Color.clear, amb = Color.clear, sun = Color.clear, ma = Color.clear, mb = Color.clear;
   for (int i = 0; i < 5; i++) {
    float w = biome[i];
    if (w <= 0.0001f) continue;
    var m = Moods[i];
    fog += m.fogTint * w; sky += m.skyTint * w; amb += m.ambientTint * w; sun += m.sunTint * w; ma += m.moteA * w; mb += m.moteB * w;
    r.fogNear += m.fogNear * w; r.fogFar += m.fogFar * w; r.sun += m.sun * w; r.cloud += m.cloud * w;
    r.snow += m.snow * w; r.dust += m.dust * w; r.wisps += m.wisps * w; r.rain += m.rain * w; r.storm += m.storm * w; r.wind += m.wind * w;
    r.temperature += m.temperature * w; r.saturation += m.saturation * w; r.contrast += m.contrast * w; r.vignette += m.vignette * w; r.exposure += m.exposure * w;
   }
   r.fogTint = fog; r.skyTint = sky; r.ambientTint = amb; r.sunTint = sun; r.moteA = ma; r.moteB = mb;
   // Graveyard fog is a near-field effect: it should not wash out the sky elsewhere.
   r.rain = Mathf.Clamp01((r.rain - 0.45f) / 0.4f);
   r.storm = Mathf.Clamp01((r.storm - 0.4f) / 0.4f);
   return r;
  }

  static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);

  void UpdateLightning(float storm, float dt) {
   float t = Time.time;
   if (t < flashEnd) flash = Mathf.Clamp01((flashEnd - t) / 0.12f);
   else if (flashPulses > 0 && t >= nextPulse) { flashPulses--; flashEnd = t + 0.12f; nextPulse = t + Random.Range(0.12f, 0.26f); flash = 1f; }
   else flash = 0f;
   if (storm < 0.35f) { nextLightning = Mathf.Max(nextLightning, t + 3f); return; }
   if (t < nextLightning) return;
   nextLightning = t + Random.Range(5f, 14f) / Mathf.Lerp(0.6f, 1.4f, storm);
   flashEnd = t + 0.12f; flash = 1f;
   flashPulses = Random.Range(0, 3); nextPulse = t + Random.Range(0.12f, 0.26f);
   OpenWorldAudio.Play("sfx_thunder", Mathf.Lerp(0.35f, 0.7f, storm));
  }

  // ---------- sky ----------
  void BuildSky() {
   var template = Resources.Load<Material>("World/Realm/RealmSky");
   if (template) { skyMat = new Material(template) { name = "OW_Sky" }; customSky = true; }
   else {
    var shader = Shader.Find("LostRealms/Sky");
    if (shader) { skyMat = new Material(shader) { name = "OW_Sky" }; customSky = true; }
    else {
     var procedural = Shader.Find("Skybox/Procedural");
     if (procedural) {
      skyMat = new Material(procedural) { name = "OW_ProceduralSky" };
      skyMat.SetFloat("_SunSize", 0.045f);
      skyMat.SetFloat("_SunSizeConvergence", 6f);
     }
    }
   }
   if (skyMat) RenderSettings.skybox = skyMat;
  }

  void BuildWind(Transform worldRoot) {
   var go = new GameObject("Realm Wind");
   go.transform.SetParent(worldRoot, false);
   go.transform.rotation = Quaternion.Euler(0f, 62f, 0f);
   wind = go.AddComponent<WindZone>();
   wind.mode = WindZoneMode.Directional;
   wind.windMain = 0.45f;
   wind.windTurbulence = 0.5f;
   wind.windPulseMagnitude = 0.3f;
   wind.windPulseFrequency = 0.12f;
  }

  // ---------- lanterns ----------
  void SpawnLanterns(Transform worldRoot) {
   if (OpenWorldRealm.Instance) {
    // One pooled light per additional-light slot: the terrain is a single renderer, so any light past
    // the per-object limit (6 on High, 4 on Balanced) would silently never reach the ground.
    int count = OpenWorldGraphics.Instance && OpenWorldGraphics.Instance.High ? 6 : 4;
    for (int i = 0; i < count; i++) {
     var lgo = new GameObject("Lantern Light " + i);
     lgo.transform.SetParent(worldRoot, false);
     var pl = lgo.AddComponent<Light>();
     pl.type = LightType.Point; pl.range = 10f; pl.intensity = 0f; pl.shadows = LightShadows.None;
     pl.color = new Color(1f, 0.74f, 0.44f);
     pl.enabled = false;
     lanternPool.Add(pl);
    }
    return;
   }
   (Vector3 pos, Color col, float range)[] lights = {
    (new Vector3(0f, 3.2f, 4f), new Color(0.45f, 1.0f, 0.78f), 14f),
    (new Vector3(-11f, 2.8f, 7f), new Color(1.0f, 0.78f, 0.42f), 11f),
    (new Vector3(11f, 2.8f, 7f), new Color(1.0f, 0.78f, 0.42f), 11f),
    (new Vector3(-20f, 3.2f, 10f), new Color(1.0f, 0.68f, 0.32f), 13f),
    (new Vector3(20f, 3.2f, 10f), new Color(1.0f, 0.82f, 0.46f), 13f),
    (new Vector3(0f, 3.0f, 14f), new Color(0.35f, 0.95f, 0.78f), 12f),
    (new Vector3(44f, 3.0f, 42f), new Color(1.0f, 0.78f, 0.42f), 14f),
    (new Vector3(-82f, 3.0f, 14f), new Color(0.55f, 0.88f, 1.0f), 14f),
    (new Vector3(-60f, 3.2f, -62f), new Color(0.68f, 0.42f, 1.0f), 15f),
    (new Vector3(0f, 3.2f, 98f), new Color(0.45f, 1.0f, 0.65f), 15f)
   };
   foreach (var item in lights) {
    float gy = OpenWorldMaterialHelper.SampleGroundHeight(item.pos);
    var lgo = new GameObject("WorldLantern");
    lgo.transform.SetParent(worldRoot, false);
    lgo.transform.position = new Vector3(item.pos.x, gy + item.pos.y, item.pos.z);
    var pl = lgo.AddComponent<Light>();
    pl.type = LightType.Point; pl.color = item.col; pl.range = item.range; pl.intensity = 1.25f; pl.shadows = LightShadows.None;
    staticLanterns.Add(pl);
   }
  }

  readonly List<(float d, int i)> lanternScratch = new List<(float, int)>();
  void UpdateLanterns(Vector3 focus) {
   foreach (var l in staticLanterns) if (l) { l.intensity = lanternLevel; l.enabled = lanternLevel > 0.05f; }
   var realm = OpenWorldRealm.Instance;
   if (!realm || lanternPool.Count == 0) return;
   bool on = lanternLevel > 0.05f;
   if (Time.time >= nextLanternRefresh) {
    nextLanternRefresh = Time.time + 0.5f;
    lanternScratch.Clear();
    var anchors = realm.LightAnchors;
    for (int i = 0; i < anchors.Count; i++) {
     float d = (anchors[i].position - focus).sqrMagnitude;
     if (d < 90f * 90f) lanternScratch.Add((d, i));
    }
    lanternScratch.Sort((a, b) => a.d.CompareTo(b.d));
    for (int i = 0; i < lanternPool.Count; i++) {
     var l = lanternPool[i];
     if (i < lanternScratch.Count && on) {
      var a = anchors[lanternScratch[i].i];
      l.transform.position = a.position;
      l.color = a.color;
      l.range = a.range;
      l.enabled = true;
     } else l.enabled = false;
    }
   }
   float flicker = 0.92f + Mathf.PerlinNoise(Time.time * 3.1f, 0.3f) * 0.16f;
   foreach (var l in lanternPool) if (l.enabled) l.intensity = lanternLevel * flicker;
  }

  // ---------- particles ----------
  static Texture2D SoftDot(int size) {
   var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_SoftDot", wrapMode = TextureWrapMode.Clamp };
   var pixels = new Color[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
    float dx = (x - c) / c, dy = (y - c) / c;
    float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
    pixels[y * size + x] = new Color(1f, 1f, 1f, r * r);
   }
   tex.SetPixels(pixels);
   tex.Apply(false, true);
   return tex;
  }

  Material ParticleMaterial(string name, bool additive) {
   var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
   Material mat;
   if (shader) {
    mat = new Material(shader) { name = name };
    mat.SetTexture("_BaseMap", moteTex);
    mat.SetFloat("_Surface", 1f);
    mat.SetFloat("_Blend", additive ? 2f : 0f);
    mat.SetOverrideTag("RenderType", "Transparent");
    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
    mat.SetInt("_ZWrite", 0);
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    mat.renderQueue = (int)RenderQueue.Transparent;
   } else mat = new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = moteTex };
   return mat;
  }

  ParticleSystem NewSystem(string name, Transform worldRoot, int max, Material mat) {
   var go = new GameObject(name);
   go.transform.SetParent(worldRoot, false);
   var ps = go.AddComponent<ParticleSystem>();
   ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
   var main = ps.main;
   main.loop = true; main.playOnAwake = true; main.maxParticles = max;
   main.simulationSpace = ParticleSystemSimulationSpace.World;
   var em = ps.emission; em.rateOverTime = 0f;
   var psr = go.GetComponent<ParticleSystemRenderer>();
   psr.sharedMaterial = mat;
   psr.shadowCastingMode = ShadowCastingMode.Off;
   psr.receiveShadows = false;
   ps.Play();
   return ps;
  }

  void BuildAmbientMotes(Transform worldRoot) {
   moteMat = ParticleMaterial("OW_Motes", true);
   moteSystem = NewSystem("Ambient Motes", worldRoot, 90, moteMat);
   var main = moteSystem.main;
   main.startLifetime = 5.5f; main.startSpeed = 0.3f;
   main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
   var shape = moteSystem.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(30f, 7f, 30f);
   var noise = moteSystem.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.25f; noise.quality = ParticleSystemNoiseQuality.Low;
   var col = moteSystem.colorOverLifetime; col.enabled = true;
   var g = new Gradient();
   g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
   col.color = g;
  }

  void BuildRainSystem(Transform worldRoot) {
   rainMat = ParticleMaterial("OW_Rain", false);
   rainSystem = NewSystem("Rain", worldRoot, 900, rainMat);
   var main = rainSystem.main;
   main.startLifetime = 0.9f; main.startSpeed = 0f;
   main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
   main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.76f, 0.86f, 1f, 0.38f), new Color(0.88f, 0.94f, 1f, 0.55f));
   var vel = rainSystem.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
   vel.x = new ParticleSystem.MinMaxCurve(-2.4f); vel.y = new ParticleSystem.MinMaxCurve(-21f); vel.z = new ParticleSystem.MinMaxCurve(-1.5f);
   var shape = rainSystem.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(36f, 2f, 36f);
   var psr = rainSystem.GetComponent<ParticleSystemRenderer>();
   psr.renderMode = ParticleSystemRenderMode.Stretch; psr.lengthScale = 4.5f; psr.velocityScale = 0.05f;
   psr.maxParticleSize = 0.012f; // streaks that pass right by the lens stay thin
   var collision = rainSystem.collision; collision.enabled = false;
  }

  void BuildSnowSystem(Transform worldRoot) {
   snowMat = ParticleMaterial("OW_Snow", false);
   snowSystem = NewSystem("Snowfall", worldRoot, 1400, snowMat);
   var main = snowSystem.main;
   main.startLifetime = 7.5f; main.startSpeed = 0f;
   main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
   main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.85f), new Color(0.88f, 0.94f, 1f, 0.95f));
   var vel = snowSystem.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
   vel.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.4f); vel.y = new ParticleSystem.MinMaxCurve(-1.9f, -1.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
   var noise = snowSystem.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.35f; noise.quality = ParticleSystemNoiseQuality.Low;
   var shape = snowSystem.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(44f, 2f, 44f);
  }

  void BuildSandSystem(Transform worldRoot) {
   sandMat = ParticleMaterial("OW_Sand", false);
   sandSystem = NewSystem("Drifting Sand", worldRoot, 260, sandMat);
   var main = sandSystem.main;
   main.startLifetime = 4.5f; main.startSpeed = 0f;
   main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
   main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.96f, 0.82f, 0.58f, 0.10f), new Color(0.9f, 0.74f, 0.5f, 0.18f));
   var vel = sandSystem.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
   vel.x = new ParticleSystem.MinMaxCurve(3.5f, 6.5f); vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.25f); vel.z = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
   var noise = sandSystem.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.2f; noise.quality = ParticleSystemNoiseQuality.Low;
   var shape = sandSystem.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(40f, 1.2f, 40f);
   var col = sandSystem.colorOverLifetime; col.enabled = true;
   var g = new Gradient();
   g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
   col.color = g;
  }

  void UpdateWeatherParticles(in Mood m, float rain, float weatherRain, float frozen, float dry, float night) {
   bool high = !OpenWorldGraphics.Instance || OpenWorldGraphics.Instance.High;
   float q = high ? 1f : 0.45f;
   if (rainSystem) { var em = rainSystem.emission; em.rateOverTime = rain * 620f * q; }
   if (snowSystem) { var em = snowSystem.emission; em.rateOverTime = frozen * (55f + 200f * weatherRain) * q; }
   if (sandSystem) { var em = sandSystem.emission; em.rateOverTime = dry * Mathf.Lerp(26f, 10f, night) * q; }
   if (moteSystem) {
    var main = moteSystem.main;
    main.startColor = new ParticleSystem.MinMaxGradient(m.moteA, m.moteB);
    float wisps = m.wisps;
    main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.1f, 0.5f, wisps), Mathf.Lerp(0.24f, 1.3f, wisps));
    var em = moteSystem.emission;
    float fireflies = 1f + night;
    em.rateOverTime = (8f * fireflies + wisps * 10f) * (1f - rain) * (1f - dry * 0.7f) * q;
   }
  }

  void SampleBiome(Vector3 focus) {
   if (OpenWorldRealm.Instance) OpenWorldRegions.BiomeWeights(focus.x, focus.z, biomeTarget);
   else { for (int i = 0; i < 5; i++) biomeTarget[i] = 0f; biomeTarget[focus.z > 110f ? 1 : 0] = 1f; }
   int best = 0;
   for (int i = 1; i < 5; i++) if (biomeTarget[i] > biomeTarget[best]) best = i;
   LocalBiome = (OWBiome)best;
  }

  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   AdvanceClock();
   Vector3 focus = gm.Hero ? gm.Hero.transform.position : Camera.main ? Camera.main.transform.position : Vector3.zero;
   if (Time.time >= nextBiomeSample) {
    nextBiomeSample = Time.time + 0.25f;
    SampleBiome(focus);
   }
   Compose(Time.deltaTime);
   if (rainSystem) rainSystem.transform.position = focus + new Vector3(2f, 15f, 1.2f);
   if (snowSystem) snowSystem.transform.position = focus + new Vector3(1f, 11f, 0f);
   if (sandSystem) sandSystem.transform.position = focus + new Vector3(-10f, 0.8f, -4f);
   if (moteSystem) moteSystem.transform.position = focus + Vector3.up * 2.4f;
   UpdateLanterns(focus);
  }

  void OnDestroy() {
   if (Instance == this) { SaveClock(); Instance = null; }
   if (skyMat) Destroy(skyMat);
   foreach (var m in new[] { moteMat, rainMat, snowMat, sandMat }) if (m) Destroy(m);
   if (moteTex) Destroy(moteTex);
  }
 }
}
