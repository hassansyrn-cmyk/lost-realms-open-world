using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Region weather scheduler (GLM Phase 3): the atmosphere already blends each biome's mood and applies
 // the weather presets; nothing decided WHEN. This watches the hero's biome and the hour, and applies
 // the region's weather: spring showers in the Verdant core, whiteout mist over the Frozen Peaks, dust
 // haze across the Dunes, night mist in the Hollow Marches, and thunderstorms over Stormveil. Each
 // chosen preset holds for minutes so the blend reads as weather, not flicker. A preset picked by hand
 // in the clock is respected for five minutes before the schedule resumes, and the current choice per
 // biome is kept in PlayerPrefs so weather survives save and load.
 public sealed class OpenWorldRegionWeather : MonoBehaviour {
  static OpenWorldRegionWeather instance;
  const string PrefKey = "LostRealms.RegionWeather";
  const float HoldSeconds = 150f;
  const float ManualBackoff = 300f;

  struct Climate { public OpenWorldAtmosphere.WeatherPreset clear, wet; public float wetDay, wetNight; }
  static readonly Dictionary<OWBiome, Climate> Climates = new Dictionary<OWBiome, Climate> {
   { OWBiome.Verdant, new Climate { clear = OpenWorldAtmosphere.WeatherPreset.ClearSky, wet = OpenWorldAtmosphere.WeatherPreset.SpringRain, wetDay = 0.4f, wetNight = 0.2f } },
   { OWBiome.Frozen, new Climate { clear = OpenWorldAtmosphere.WeatherPreset.ClearSky, wet = OpenWorldAtmosphere.WeatherPreset.MysticMist, wetDay = 0.3f, wetNight = 0.35f } },
   { OWBiome.Dunes, new Climate { clear = OpenWorldAtmosphere.WeatherPreset.ClearSky, wet = OpenWorldAtmosphere.WeatherPreset.MysticMist, wetDay = 0.2f, wetNight = 0.1f } },
   { OWBiome.Graveyard, new Climate { clear = OpenWorldAtmosphere.WeatherPreset.ClearSky, wet = OpenWorldAtmosphere.WeatherPreset.MysticMist, wetDay = 0.3f, wetNight = 0.7f } },
   { OWBiome.Storm, new Climate { clear = OpenWorldAtmosphere.WeatherPreset.ClearSky, wet = OpenWorldAtmosphere.WeatherPreset.Thunderstorm, wetDay = 0.55f, wetNight = 0.5f } },
  };

  float nextDecision, manualUntil;
  OpenWorldAtmosphere.WeatherPreset lastApplied = (OpenWorldAtmosphere.WeatherPreset)(-1);

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldRegionWeather>()) new GameObject("Region weather").AddComponent<OpenWorldRegionWeather>(); }
  void OnDestroy() { if (instance == this) instance = null; }

  public static bool Raining {
   get {
    var atm = OpenWorldAtmosphere.Instance;
    return atm && (atm.CurrentWeather == OpenWorldAtmosphere.WeatherPreset.SpringRain || atm.CurrentWeather == OpenWorldAtmosphere.WeatherPreset.Thunderstorm);
   }
  }

  public static bool HeroReady;

  void Update() {
   var atm = OpenWorldAtmosphere.Instance;
   var gm = OpenWorldManager.I;
   HeroReady = gm && gm.Hero;
   if (!atm || !gm || !gm.Hero || Time.unscaledTime < nextDecision) return;
   nextDecision = Time.unscaledTime + 20f;

   // Someone (the clock UI, a quest) chose a different preset: stand down for a while.
   if (lastApplied != (OpenWorldAtmosphere.WeatherPreset)(-1) && atm.CurrentWeather != lastApplied) { manualUntil = Time.unscaledTime + ManualBackoff; }
   if (Time.unscaledTime < manualUntil) return;

   if (!Climates.TryGetValue(atm.LocalBiome, out var climate)) return;
   var chosen = Pick(atm, climate);
   if (atm.CurrentWeather == chosen) { lastApplied = chosen; return; }
   atm.ApplyWeather(chosen);
   lastApplied = chosen;
   Persist(atm.LocalBiome, chosen);
  }

  OpenWorldAtmosphere.WeatherPreset Pick(OpenWorldAtmosphere atm, Climate climate) {
   // The kept choice for this biome wins on load; otherwise a stable per-biome roll decides the spell.
   var kept = Restore(atm.LocalBiome);
   if (kept.HasValue && Time.unscaledTime < 30f) return kept.Value;
   float wet = atm.NightFactor > 0.5f ? climate.wetNight : climate.wetDay;
   return Random.value < wet ? climate.wet : climate.clear;
  }

  static void Persist(OWBiome biome, OpenWorldAtmosphere.WeatherPreset preset) {
   var map = new Dictionary<string, string>();
   var stored = PlayerPrefs.GetString(PrefKey, null);
   if (!string.IsNullOrEmpty(stored))
    foreach (var pair in stored.Split(';'))
     if (!string.IsNullOrEmpty(pair)) { var kv = pair.Split(':'); if (kv.Length == 2) map[kv[0]] = kv[1]; }
   map[biome.ToString()] = ((int)preset).ToString();
   var sb = new System.Text.StringBuilder();
   foreach (var kv in map) sb.Append(kv.Key).Append(':').Append(kv.Value).Append(';');
   PlayerPrefs.SetString(PrefKey, sb.ToString());
  }

  static OpenWorldAtmosphere.WeatherPreset? Restore(OWBiome biome) {
   var stored = PlayerPrefs.GetString(PrefKey, null);
   if (string.IsNullOrEmpty(stored)) return null;
   foreach (var pair in stored.Split(';')) {
    var kv = pair.Split(':');
    if (kv.Length == 2 && kv[0] == biome.ToString() && int.TryParse(kv[1], out int code)) return (OpenWorldAtmosphere.WeatherPreset)code;
   }
   return null;
  }
 }
}
