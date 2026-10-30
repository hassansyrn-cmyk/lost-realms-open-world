using UnityEngine;

namespace LostRealmsOW {
 // Region music (GLM Part B, brief Docs/TASKS_GLM_2026-10-30.md): cross-fades the project's own realm
 // themes by biome, day-night filtering, a combat layer, and stinger ducks. Self-installing, with two
 // AudioSources for smooth fades. Supersedes the manager's single-theme BiomeMusic source (found and
 // removed at runtime — no manager edit); the soundscape's adjustments to that old source are guarded
 // and simply stop applying.
 //
 // Region picks: Verdant/hub → verdant, Frozen Peaks → whiteout, Burning Dunes → sunscar, Stormveil →
 // stormveil, sky isles (by height near the isle) → astral. Hollow Marches plays the astral theme at
 // 45% under an always-darker filter: its sparse, weightless texture reads as ghostly over a graveyard,
 // where the verdant melody would feel too warm.
 //
 // Combat reads public members only (Manager.Enemies: Dead, Health, Telegraphing, AggroRadius, Role,
 // IsBoss, IsBountyElite). Ordinary fights swell the region music; elites, bounties and bosses
 // cross-fade to boss_fight_theme (storm_guardian_theme in Stormveil) and return to the region track
 // 7 s after the last engagement. Stingers duck the music when the public banner announces a
 // completion or discovery.
 public sealed class OpenWorldMusic : MonoBehaviour {
  public static OpenWorldMusic Instance { get; private set; }
  const float FadeSeconds = 3.5f;
  const float RegionHoldSeconds = 4f; // hysteresis: a new region must hold this long before switching
  const float CombatReturnSeconds = 7f;
  const float BaseVolume = 0.38f;

  struct RegionTrack { public string track; public float volumeFactor; }
  static readonly RegionTrack Verdant = new RegionTrack { track = "Audio/verdant_realm_theme", volumeFactor = 1f };
  static readonly RegionTrack Frozen = new RegionTrack { track = "Audio/whiteout_realm_theme", volumeFactor = 1f };
  static readonly RegionTrack Dunes = new RegionTrack { track = "Audio/sunscar_realm_theme", volumeFactor = 1f };
  static readonly RegionTrack Storm = new RegionTrack { track = "Audio/stormveil_theme", volumeFactor = 0.92f };
  static readonly RegionTrack Astral = new RegionTrack { track = "Audio/astral_theme", volumeFactor = 1f };
  static readonly RegionTrack Marches = new RegionTrack { track = "Audio/astral_theme", volumeFactor = 0.45f };
  static readonly Vector3 SkyIsleCentre = new Vector3(-80f, 0f, 88f);

  AudioSource sourceA, sourceB;
  AudioLowPassFilter filterA, filterB;
  bool playingA = true;
  string loadedTrackA = "", loadedTrackB = "";
  string currentTrack = "";
  string pendingTrack;
  float pendingSince = -999f;
  float currentVolume, currentCutoff = 22000f;
  float combatFirstSeen = -999f, bossFirstSeen = -999f;
  float combatSessionUntil = -999f, bossSessionUntil = -999f;
  string lastBanner = "";
  float nextBannerCheck;
  float duckUntil;

  // Diagnostics for the bridge validation.
  public static string CurrentTrack => Instance ? Instance.currentTrack : "";
  public static float CurrentVolume => Instance ? Instance.currentVolume : 0f;
  public static float CurrentCutoff => Instance ? Instance.currentCutoff : 0f;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() {
   if (FindAnyObjectByType<OpenWorldMusic>()) return;
   var go = new GameObject("Region music");
   var gm = OpenWorldManager.I;
   if (gm && gm.WorldRoot) go.transform.SetParent(gm.WorldRoot, false);
   go.AddComponent<OpenWorldMusic>();
  }
  void OnDestroy() { if (Instance == this) Instance = null; }

  void Awake() {
   Instance = this;
   supersedeCheck = Time.unscaledTime + 1f;
   sourceA = NewSource("Music A", ref filterA);
   sourceB = NewSource("Music B", ref filterB);
  }

  float supersedeCheck = 1f;
  bool superseded;

  // The manager creates its single-theme BiomeMusic during its own build, which can happen after this
  // component installs — retry until it exists, then stop and remove it so only this system plays.
  void SupersedeOldSource() {
   if (superseded || !OpenWorldManager.I || !OpenWorldManager.I.WorldRoot) return;
   var old = OpenWorldManager.I.WorldRoot.Find("BiomeMusic");
   if (!old) return;
   var oldAudio = old.GetComponent<AudioSource>();
   if (oldAudio) oldAudio.Stop();
   Destroy(old.gameObject);
   superseded = true;
   Debug.Log("[GlmMusic] superseded the manager's BiomeMusic source");
  }

  AudioSource NewSource(string name, ref AudioLowPassFilter filter) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   var a = go.AddComponent<AudioSource>();
   a.loop = true;
   a.playOnAwake = false;
   a.spatialBlend = 0f;
   a.volume = 0f;
   filter = go.AddComponent<AudioLowPassFilter>();
   filter.cutoffFrequency = 22000f;
   return a;
  }

  void Update() {
   SupersedeOldSource();
   var gm = OpenWorldManager.I;
   var atm = OpenWorldAtmosphere.Instance;
   if (!gm || !gm.Hero || !atm) return;
   var hero = gm.Hero.transform;

   // --- combat layer from public members only ---
   // A combat session starts after 2 s of continuous engagement (spawn-in windups and wandering
   // elites must not flip the track), lasts while the fight does, and ends 7 s after the last
   // engagement. Elites, bounties and bosses run the boss layer with their own hold.
   bool engaged = false, boss = false;
   foreach (var e in gm.Enemies) {
    if (!e || e.Dead) continue;
    float d = Vector3.Distance(e.transform.position, hero.position);
    if (d > 50f) continue;
    // Engagement needs actual aggression, not just an idle enemy standing nearby: chasing (moving
    // fast), attacking (windup), or already wounded. Distance alone is not enough.
    bool aggressive = e.Telegraphing || e.Health < e.MaxHealth
     || (e.Controller && e.Controller.velocity.magnitude > 1.2f);
    if (d > 15f || !aggressive) continue;
    engaged = true;
    if (e.IsBoss || e.IsBountyElite || e.Role == "Elite") boss = true;
    if (boss) break;
   }
   if (engaged) {
    if (combatFirstSeen < 0f) combatFirstSeen = Time.unscaledTime;
    if (Time.unscaledTime - combatFirstSeen >= 2f) {
     combatSessionUntil = Time.unscaledTime + CombatReturnSeconds;
     if (boss) {
      if (bossFirstSeen < 0f) bossFirstSeen = Time.unscaledTime;
      if (Time.unscaledTime - bossFirstSeen >= 2f) bossSessionUntil = combatSessionUntil;
     } else bossFirstSeen = -999f;
    }
   } else {
    combatFirstSeen = -999f;
    bossFirstSeen = -999f;
   }
   bool combatOn = Time.unscaledTime < combatSessionUntil;
   bool bossOn = Time.unscaledTime < bossSessionUntil;

   // --- stinger ducking from the public banner ---
   if (Time.unscaledTime >= nextBannerCheck) {
    nextBannerCheck = Time.unscaledTime + 0.15f;
    string banner = gm.CurrentBanner;
    if (!string.IsNullOrEmpty(banner) && banner != lastBanner) {
     if (banner.Contains("COMPLETE") || banner.Contains("DISCOVERY") || banner.Contains("LEVEL UP")) duckUntil = Time.unscaledTime + 1.2f;
     lastBanner = banner;
    }
   }

   // --- target region with hysteresis ---
   var region = TargetRegion(hero.position, atm.LocalBiome);
   string desired = bossOn
    ? (atm.LocalBiome == OWBiome.Storm ? "Audio/storm_guardian_theme" : "Audio/boss_fight_theme")
    : region.track;
   if (desired != currentTrack) {
    if (desired != pendingTrack) { pendingTrack = desired; pendingSince = Time.unscaledTime; }
    else if (Time.unscaledTime - pendingSince >= RegionHoldSeconds) SwitchTo(desired);
   } else {
    pendingTrack = null;
   }

   // --- target volume ---
   var audio = OpenWorldAudio.Instance;
   float master = audio ? audio.MasterVolume : 1f;
   float targetVolume = BaseVolume * region.volumeFactor * master;
   targetVolume *= 1f - 0.3f * atm.NightFactor; // nights play quieter and darker
   if (gm.IsModalOpen) targetVolume *= 0.35f; // pause and any modal duck the music
   if (TitleShowing) targetVolume = 0f; // the title screen stops the music
   if (Time.unscaledTime < duckUntil) targetVolume *= 0.35f; // stinger duck
   if (engaged && !bossOn) targetVolume = Mathf.Min(0.5f, targetVolume * 1.18f); // gentle fight swell

   // --- fade the two sources toward their targets over FadeSeconds ---
   var live = playingA ? sourceA : sourceB;
   var dead = playingA ? sourceB : sourceA;
   float riseRate = Mathf.Max(0.02f, targetVolume) / FadeSeconds;
   live.volume = Mathf.MoveTowards(live.volume, targetVolume, Time.unscaledDeltaTime * riseRate);
   dead.volume = Mathf.MoveTowards(dead.volume, 0f, Time.unscaledDeltaTime * Mathf.Max(0.02f, dead.volume) / FadeSeconds);
   currentVolume = live.volume;

   // --- day-night + biome filter (blended with the hour through the night factor) ---
   float grave = atm.LocalBiome == OWBiome.Graveyard ? 1f : 0f;
   currentCutoff = Mathf.Lerp(22000f, 1500f, Mathf.Max(atm.NightFactor, grave * 0.85f));
   filterA.cutoffFrequency = currentCutoff;
   filterB.cutoffFrequency = currentCutoff;
  }

  void SwitchTo(string track) {
   if (track == currentTrack) return;
   currentTrack = track;
   pendingTrack = null;
   playingA = !playingA;
   var live = playingA ? sourceA : sourceB;
   if ((playingA ? loadedTrackA : loadedTrackB) != track) {
    var clip = Resources.Load<AudioClip>(track);
    if (!clip) { Debug.LogWarning("[GlmMusic] missing clip " + track); return; }
    live.clip = clip;
    if (playingA) loadedTrackA = track; else loadedTrackB = track;
   }
   live.volume = 0f;
   live.Play();
   Debug.Log($"[GlmMusic] track -> {track}");
  }

  RegionTrack TargetRegion(Vector3 hero, OWBiome biome) {
   // Sky isles: standing on the isle platform above the hub's northern horizon.
   var flat = new Vector2(hero.x - SkyIsleCentre.x, hero.z - SkyIsleCentre.z);
   if (hero.y > 25f && flat.magnitude < 70f) return Astral;
   switch (biome) {
    case OWBiome.Frozen: return Frozen;
    case OWBiome.Dunes: return Dunes;
    case OWBiome.Graveyard: return Marches;
    case OWBiome.Storm: return Storm;
    default: return Verdant;
   }
  }

  // Title screen detection from my own partial accessor (no HUD file edits).
  static bool TitleShowing => OpenWorldMobileHud.TitleShowing;
 }

 public sealed partial class OpenWorldMobileHud {
  // Read-only accessor for the music system: is the title screen up? (titleRoot stays HUD-private.)
  internal static bool TitleShowing => Instance && Instance.titleRoot && Instance.titleRoot.activeSelf;
 }
}
