using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LostRealmsOW {
 // Gameplay belongs on the streamed wrapper, never on the vendor prefab.
 //  - Prey (deer, elk, hares) can be hunted with melee, spells and bursts; a carcass is collected once.
 //  - Horses are ridden: walk with a light touch of the stick, trot/canter with a push, gallop with the
 //    sprint button. They accelerate and brake like a heavy animal, turn wide at speed, slow uphill,
 //    wade fords, lean into turns and pitch with the ground. Aster vaults on and hops off (no teleport);
 //    her seated pose follows the horse's real gait (OpenWorldCharacterVisual.RidePose).
 //  - Whistle (H): the last horse she rode, or the nearest one, comes to her.
 [DefaultExecutionOrder(11000)]
 public sealed class OpenWorldAnimalInteraction : MonoBehaviour {
  static readonly List<OpenWorldAnimalInteraction> live = new List<OpenWorldAnimalInteraction>();
  public static OpenWorldAnimalInteraction LastRidden { get; private set; }
  public bool Horse, Deer;  // Deer: any huntable prey (the HUD shows its health bar and carcass marker)
  public int Meat = 2;
  public string SpeciesName = "Wild Deer";
  public string PersistentId;
  public bool Dead => health <= 0f;
  public OpenWorldHero Rider { get; private set; }
  float health = 3f, calmUntil;
  OpenWorldWildlife wildlife;
  OpenWorldImportedModel motion;
  CharacterController capsule;
  OpenWorldMountRig rig;
  float speed, yawRate, lastYaw, bank;
  // Mounting: Aster's root travels from where she stood to the saddle along an arc.
  const float MountTime = 0.55f;
  float mountT = 1f, mountHipsHeight = 0.95f;
  Vector3 mountFrom; Quaternion mountFromRot;
  float splashAt;
  bool buckPending,hadDrive;float buckUntil,stopUntil;
  public bool IsBucking => buckPending || Time.time < buckUntil;
  public bool ControlsLocked => mountT<1f || IsBucking;

  // Paces matched to the horse's cycles (walk ~1.7 m/s, trot ~4.0 m/s, canter ~6.0 m/s, gallop ~9.0 m/s).
  public const float WalkPace = 1.7f, TrotPace = 4.0f, CanterPace = 6.0f, GallopPace = 9.0f;
  public float Speed => speed;
  public float Gallop01 => Mathf.Clamp01((speed - CanterPace * 0.8f) / (GallopPace - CanterPace * 0.8f));
  public float TurnRate => yawRate;
  public OpenWorldMountRig Rig => rig;
  public bool Seated => Rider && mountT >= 1f;
  public bool JustTamed { get; private set; }
  public bool Calm => Horse || Time.time < calmUntil || Dead;
  public bool BeingPetted => Time.time < calmUntil;
  public string Prompt => Dead ? "COLLECT " + Meat + " RAW MEAT" : Horse ? "RIDE HORSE" : "PET ANIMAL";

  public static OpenWorldAnimalInteraction Attach(GameObject root, string species, string id) {
   string name = (species ?? "").ToLowerInvariant();
   var existing = root.GetComponent<OpenWorldAnimalInteraction>();
   if (existing) return existing;
   var a = root.AddComponent<OpenWorldAnimalInteraction>();
   a.Horse = name.Contains("horse");
   a.Deer = name.Contains("deer") || name.Contains("elk") || name.Contains("stag") || name.Contains("hare") || name.Contains("rabbit") || name.Contains("boar");
   a.Meat = name.Contains("hare") || name.Contains("rabbit") ? 1 : 2;
   a.SpeciesName = ResolveSpeciesName(species, root ? root.name : "");
   a.PersistentId = id;
   a.wildlife = root.GetComponent<OpenWorldWildlife>();
   a.motion = root.GetComponentInChildren<OpenWorldImportedModel>();
   if (a.Horse) {
    a.capsule = root.AddComponent<CharacterController>();
    a.capsule.height = 1.7f; a.capsule.radius = .65f;
    a.capsule.center = Vector3.up * .85f; a.capsule.stepOffset = .35f; a.capsule.slopeLimit = 30f;
    a.capsule.enabled = false;
    a.rig = OpenWorldMountRig.For(root);
    a.ConfigureSaddle(!name.Contains("wild"));
   }
   return a;
  }

  // Ensures the leather saddle mesh is never tinted by the horse's coat colour property block,
  // restores the hair-card alpha texture if Coat() replaced _BaseMap on all SkinnedMeshRenderers,
  // and hides the saddle on untamed wild horses until Aster mounts them.
  void ConfigureSaddle(bool showSaddle) {
   var mpb = new MaterialPropertyBlock();
   foreach (var r in GetComponentsInChildren<Renderer>(true)) {
    if (!r) continue;
    if (r.name.IndexOf("Saddle", StringComparison.OrdinalIgnoreCase) >= 0) {
     r.SetPropertyBlock(null);
     r.enabled = showSaddle;
    } else if (r.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 && r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseMap")) {
     var hairTex = r.sharedMaterial.GetTexture("_BaseMap");
     if (hairTex) {
      r.GetPropertyBlock(mpb);
      mpb.SetTexture("_BaseMap", hairTex);
      r.SetPropertyBlock(mpb);
     }
    }
   }
  }

  static string ResolveSpeciesName(string species, string objName) {
   string raw = (species ?? "").Trim();
   if (!string.IsNullOrEmpty(raw) && !raw.Contains("/") && !raw.Contains("_") && !raw.Contains(".")) return raw;
   string s = (raw + " " + (objName ?? "")).ToLowerInvariant();
   if (s.Contains("snow") && (s.Contains("hare") || s.Contains("rabbit"))) return "Snow Hare";
   if (s.Contains("hare") || s.Contains("rabbit")) return "Hare";
   if (s.Contains("elk")) return "Snow Elk";
   if (s.Contains("boar") || s.Contains("hog")) return "Wild Boar";
   if (s.Contains("stag")) return "Wild Stag";
   if (s.Contains("deer")) return "Meadow Deer";
   if (s.Contains("fox")) return "Red Fox";
   if (s.Contains("horse")) return s.Contains("wild") ? "Wild Horse" : "Saddle Horse";
   return "Wild Deer";
  }
  void OnEnable() { if (!live.Contains(this)) live.Add(this); }
  void OnDisable() {
   live.Remove(this);
   if (Rider) Rider.LeaveMount(true);
  }
  public static OpenWorldAnimalInteraction Nearest(OpenWorldHero hero) {
   if (!hero || hero.Dead) return null;
   OpenWorldAnimalInteraction best = null; float distance = 3f;
   foreach (var a in live) {
    if (!a || a.Rider || (a.Deer && !a.Dead)) continue;
    float d = Vector3.Distance(hero.transform.position, a.transform.position);
    if (d < distance) { best = a; distance = d; }
   }
   return best;
  }
  public void Interact(OpenWorldManager gm) {
   if (!gm || !gm.Hero || Vector3.Distance(gm.Hero.transform.position, transform.position) > 3.2f) return;
   if (Dead) {
    if (gm.RawMeat > 999 - Meat) { gm.ShowBanner("Meat pouch full • Cook a meal first"); return; }
    if (!Deer || string.IsNullOrEmpty(PersistentId) || !gm.CollectedWorldIds.Add(PersistentId)) return;
    gm.RawMeat = Mathf.Min(999, gm.RawMeat + Meat); gm.MarkProgressDirty();
    gm.ShowBanner("Collected " + Meat + " raw meat • Cook at a campfire");
    gameObject.SetActive(false); Destroy(gameObject);
   } else if (Horse) gm.Hero.EnterMount(this);
   else { calmUntil = Time.time + 8f; if (motion) motion.State = "idle"; gm.ShowBanner("The animal relaxes beside Aster."); }
  }

  // Melee swings, spells and bursts: prey in reach takes the blow; every animal near the commotion bolts.
  public static int Strike(Vector3 origin, Vector3 forward, float reach, float damage, bool radial) {
   int hits = 0;
   for (int i = live.Count - 1; i >= 0; i--) {
    var a = live[i];
    if (!a || a.Dead || a.Rider) continue;
    Vector3 delta = a.transform.position - origin;
    if (Mathf.Abs(delta.y) > 2.5f) continue;
    delta.y = 0f;
    bool inReach = a.Deer && delta.sqrMagnitude <= reach * reach && (radial || delta.sqrMagnitude <= 1f || Vector3.Dot(forward, delta.normalized) >= .25f);
    // Walls and large scenery stop a melee hit reaching the animal.
    if (inReach && Physics.Linecast(origin + Vector3.up, a.transform.position + Vector3.up, out var hit, ~0, QueryTriggerInteraction.Ignore)
        && !hit.transform.IsChildOf(a.transform) && !hit.transform.GetComponentInParent<OpenWorldHero>()) inReach = false;
    if (!inReach) {
     if (a.wildlife && delta.sqrMagnitude < reach * reach * 5f) a.wildlife.Startle(origin);
     continue;
    }
    a.health = Mathf.Max(0f, a.health - damage); hits++;
    OpenWorldVfx.Hit(-1, a.transform.position + Vector3.up * 0.6f, false);
    if (a.Dead) a.Die(delta);
    else if (a.wildlife) a.wildlife.Startle(origin);
   }
   return hits;
  }

  void Die(Vector3 hitDirection) {
   if (wildlife) wildlife.enabled = false;
   if (motion) {
    motion.FallDirection = hitDirection;
    motion.State = "death"; motion.RestartVersion++;
   }
   OpenWorldSoundscape.Sfx("body_thud", transform.position, 0.45f);
   OpenWorldManager.I?.ShowBanner((Meat > 1 ? "Deer hunted" : "Hare hunted") + " • Approach and collect the meat");
   // The herd flees from the kill.
   if (wildlife) foreach (var a in live) if (a && a != this && a.wildlife && (a.transform.position - transform.position).sqrMagnitude < 30f * 30f) a.wildlife.Startle(transform.position - hitDirection.normalized * 2f);
  }

  public bool Mount(OpenWorldHero hero) {
   if (!Horse || Dead || Rider || !capsule) return false;
   Rider = hero; LastRidden = this; speed = 0f;hadDrive=false;stopUntil=buckUntil=0; buckPending=false;
   JustTamed = false;
   if (wildlife && wildlife.Nature != OpenWorldWildlife.Temper.Tame) {
    // Riding a wild horse calms it: from now on it is hers (no longer bolts with its herd).
    wildlife.Nature = OpenWorldWildlife.Temper.Tame;
    wildlife.Herd?.Members.Remove(wildlife); wildlife.Herd = null;
    JustTamed = true;
   }
   ConfigureSaddle(true);
   if (wildlife) wildlife.enabled = false;
   capsule.enabled = true;
   if (!rig) rig = OpenWorldMountRig.For(gameObject);
   mountT = 0f; mountFrom = hero.transform.position; mountFromRot = hero.transform.rotation;
   mountHipsHeight = hero.Visual ? hero.Visual.HipsAboveRoot() : 0.95f;
   if (motion) motion.State = "idle";
   buckPending=JustTamed&&motion&&motion.Buck;
   lastYaw = transform.eulerAngles.y;
   return true;
  }
  public void Release() {
   Rider = null; speed = 0f;buckPending=hadDrive=false;buckUntil=stopUntil=0;
   if (capsule) capsule.enabled = false;
   if (motion) motion.State = "idle";
   if (wildlife) { wildlife.ResetHome(); wildlife.enabled = true; }
  }

  public void Move(Vector2 input, bool gallop) {
   if (!Rider || !capsule) return;
   var gm = OpenWorldManager.I;
   float dt = Time.deltaTime;
   if (dt <= 0f) return;
   if(mountT>=1f&&buckPending){buckPending=false;buckUntil=Time.time+motion.Buck.length;motion.State="buck";motion.RestartVersion++;}
   if(ControlsLocked){
    speed=0;hadDrive=false;stopUntil=0;
    if(motion)motion.State=mountT>=1f&&Time.time<buckUntil?"buck":"idle";
    capsule.Move(Vector3.down*12f*dt);return;
   }
   if(buckUntil>0){buckUntil=0;gm?.ShowBanner("Horse calmed • Move to ride • Interact to dismount");}
   Vector3 f = Camera.main ? Camera.main.transform.forward : Rider.transform.forward;
   f.y = 0f; f.Normalize();
   Vector3 direction = Vector3.ClampMagnitude(f * input.y + Vector3.Cross(Vector3.up, f) * input.x, 1f);
   float mag = direction.magnitude;
   bool driving=mag>=.08f;
   if(!driving&&hadDrive&&speed>=CanterPace*.8f&&motion&&motion.RunStop){stopUntil=Time.time+motion.RunStop.length;motion.RestartVersion++;}
   if(driving)stopUntil=0;
   hadDrive=driving;
   // Gait from the stick: a light touch walks, a push canters, the sprint button gallops.
   float want = mag < 0.08f ? 0f : gallop ? GallopPace : mag < 0.6f ? Mathf.Lerp(1.1f, WalkPace, mag / 0.6f) : Mathf.Lerp(WalkPace * 1.6f, CanterPace, (mag - 0.6f) / 0.4f);
   if (mountT < 1f) want = 0f;
   // Uphill costs speed, downhill a little extra (from the ground ahead of and behind the hooves).
   var terrain = gm ? gm.ActiveTerrain : null;
   if (terrain && want > 0f) {
    float H(Vector3 q) => terrain.SampleHeight(q) + terrain.transform.position.y;
    float slope = (H(transform.position + transform.forward * 1.2f) - H(transform.position - transform.forward * 1.2f)) / 2.4f;
    want *= Mathf.Clamp(1f - slope * 1.4f, 0.45f, 1.12f);
   }
   if (mag > 0.08f) {
    var wantRot = Quaternion.LookRotation(direction);
    float angle = Quaternion.Angle(transform.rotation, wantRot);
    // A horse turns sharply at a walk and in wide arcs at a gallop; asked to turn round, it brakes first.
    if (angle > 100f && speed > WalkPace) want = Mathf.Min(want, WalkPace);
    float rate = Mathf.Lerp(200f, 85f, speed / GallopPace);
    transform.rotation = Quaternion.RotateTowards(transform.rotation, wantRot, rate * dt);
   }
   float accel = want > speed ? (want > CanterPace ? 5.5f : 3.6f) : 7.5f;
   speed = Mathf.MoveTowards(speed, want, accel * dt);
   float yaw = transform.eulerAngles.y;
   yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-8f * dt));
   lastYaw = yaw;
   Vector3 next = transform.position + transform.forward * speed * dt;
   bool shallow = true;
   if (OpenWorldTerrainSculptor.WaterAt(next.x, next.z, out float surface, out float depth)) {
    float ground = terrain ? terrain.SampleHeight(next) + terrain.transform.position.y : next.y;
    shallow = surface - ground < 0.95f; // fords and shallows only: horses do not swim
    if (shallow && speed > 1f && Time.time > splashAt) {
     splashAt = Time.time + Mathf.Lerp(0.5f, 0.18f, speed / GallopPace);
     OpenWorldWaterFx.SplashAt(transform.position + transform.forward * 0.9f + transform.right * Random.Range(-0.3f, 0.3f), Mathf.Lerp(0.25f, 0.8f, speed / GallopPace));
    }
   }
   float groundHere;
   bool safe = gm && terrain && OpenWorldGrounding.TryWalk(terrain, transform.position, next, out groundHere) && shallow
    && Mathf.Abs(next.x) < OpenWorldHero.WorldBound && Mathf.Abs(next.z) < OpenWorldHero.WorldBound;
   if (!safe) speed = Mathf.MoveTowards(speed, 0f, 25f * dt);
   var flags = capsule.Move((safe ? transform.forward * speed : Vector3.zero) * dt + Vector3.down * 12f * dt);
   if ((flags & CollisionFlags.Sides) != 0) speed = Mathf.MoveTowards(speed, 0f, 30f * dt);
   if (motion) {
    if (Time.time<stopUntil) motion.State="runstop";
    else if (motion.Has("trot") || motion.Has("canter")) {
     motion.State = speed < 0.15f ? "idle" : speed < 2.65f ? "walk" : speed < 5.0f ? "trot" : speed < 7.4f ? "canter" : "gallop";
    } else {
     float wn = motion.NaturalSpeed(false), rn = motion.NaturalSpeed(true);
     float split = wn > 0.05f && rn > wn * 1.3f ? (wn + rn) * 0.5f : 4f;
     motion.State = speed < 0.15f ? "idle" : speed > split ? "run" : "walk";
    }
   }
   float targetBank = Mathf.Clamp(-yawRate * speed * 0.011f, -10f, 10f);
   bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-5f * dt));
   if (wildlife) wildlife.ExternalTilt(bank, dt);
  }

  // Places the rider: during the vault her root rises from the ground onto the saddle along an arc,
  // then it rides with the horse (gait bounce goes to her hips in the pose, not to the camera).
  public void SeatRider(float dt) {
   var hero = Rider;
   if (!hero) return;
   Vector3 seat = rig ? rig.SeatSteady : transform.position + Vector3.up * 1.3f;
   Vector3 rootPos = seat + Vector3.up * (0.1f - mountHipsHeight);
   Quaternion rootRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
   float weight = 1f;
   if (mountT < 1f) {
    mountT = Mathf.Min(1f, mountT + dt / MountTime);
    float u = mountT * mountT * (3f - 2f * mountT);
    Vector3 p = Vector3.Lerp(mountFrom, rootPos, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.55f);
    hero.transform.SetPositionAndRotation(p, Quaternion.Slerp(mountFromRot, rootRot, u));
    weight = u;
   } else hero.transform.SetPositionAndRotation(rootPos, rootRot);
   hero.Visual?.SetRide(rig, weight, speed, Gallop01, yawRate);
  }

  static readonly RaycastHit[] wallHits = new RaycastHit[8];
  // Where she lands hopping off: the left side first (the usual side), then the right, then behind.
  // The spot must be free and nothing solid may stand between the saddle and it (a barn wall beside
  // the horse would otherwise put her inside the barn).
  public bool DismountPoint(OpenWorldHero hero, out Vector3 point) {
   if (!OpenWorldManager.I || !OpenWorldManager.I.ActiveTerrain) { point = transform.position; return false; }
   var terrain = OpenWorldManager.I.ActiveTerrain;
   float side = 1.6f * Mathf.Max(1f, rig ? rig.BarrelHalf / 0.3f : 1f);
   var directions = new[] { -transform.right, transform.right, (-transform.right - transform.forward).normalized, (transform.right - transform.forward).normalized, -transform.forward };
   foreach (var direction in directions) {
    for (int step = 0; step < 2; step++) {
     Vector3 p = transform.position + direction * (side + step * 0.6f);
     if (!OpenWorldGrounding.TryWalk(terrain, transform.position, p, out float ground)) continue;
     if (OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _)) continue;
     p.y = ground + .1f;
     if (Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.5f, .35f, ~0, QueryTriggerInteraction.Ignore)) continue;
     Vector3 from = (rig ? rig.SeatSteady : transform.position + Vector3.up * 1.2f) - Vector3.up * 0.2f, to = p + Vector3.up * 0.9f;
     Vector3 d = to - from;
     int n = Physics.SphereCastNonAlloc(from, 0.2f, d.normalized, wallHits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
     bool walled = false;
     for (int i = 0; i < n && !walled; i++) {
      var c = wallHits[i].collider;
      if (!c || c is TerrainCollider || c.transform.IsChildOf(transform) || c.GetComponentInParent<OpenWorldHero>()) continue;
      if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue;
      walled = true;
     }
     if (walled) continue;
     point = p; return true;
    }
   }
   point = transform.position; return false;
  }

  // Puts an animal on free ground behind Aster, out of the camera's view, so it can run in to her
  // (a whistled horse that is far away or penned in). False when no spot was found.
  public static bool BringRound(Transform animal, Transform hero, float distance) {
   var gm = OpenWorldManager.I;
   if (!animal || !hero || !gm || !gm.ActiveTerrain) return false;
   var terrain = gm.ActiveTerrain;
   Vector3 back = Camera.main ? -Camera.main.transform.forward : -hero.forward; back.y = 0f;
   if (back.sqrMagnitude < 0.01f) back = -hero.forward;
   back.Normalize();
   for (int i = 0; i < 10; i++) {
    float angle = (i + 1) / 2 * 22f * (i % 2 == 0 ? 1f : -1f);
    Vector3 p = hero.position + Quaternion.AngleAxis(angle, Vector3.up) * back * distance;
    if (OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _)) continue;
    if (Mathf.Abs(p.x) > OpenWorldRegions.PlayableHalf - 8f || Mathf.Abs(p.z) > OpenWorldRegions.PlayableHalf - 8f) continue;
    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.05f;
    if (Vector3.Angle(OpenWorldTerrainSculptor.NormalAt(p.x, p.z), Vector3.up) > 28f) continue;
    if (Physics.CheckCapsule(p + Vector3.up * 0.7f, p + Vector3.up * 1.6f, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
    animal.position = p;
    animal.rotation = Quaternion.LookRotation(new Vector3(hero.position.x - p.x, 0f, hero.position.z - p.z));
    return true;
   }
   return false;
  }

  // Whistle: the last horse ridden (or the nearest one) gallops over; from far away it is brought round
  // out of sight first.
  public static void CallHorse(OpenWorldHero hero) {
   if (!hero || hero.IsMounted) return;
   var gm = OpenWorldManager.I;
   OpenWorldAnimalInteraction horse = LastRidden && LastRidden.isActiveAndEnabled && !LastRidden.Dead ? LastRidden : null;
   if (!horse) {
    float best = 90f * 90f;
    foreach (var a in live) {
     if (!a || !a.Horse || a.Dead || a.Rider) continue;
     float d = (a.transform.position - hero.transform.position).sqrMagnitude;
     if (d < best) { best = d; horse = a; }
    }
   }
   if (!horse || !horse.wildlife) { gm?.ShowBanner("No horse within earshot"); return; }
   Vector3 to = horse.transform.position - hero.transform.position; to.y = 0f;
   // From far away it is brought round from behind her, beyond the camera's view.
   if (to.magnitude > 70f) BringRound(horse.transform, hero.transform, 38f);
   horse.wildlife.Summon(hero.transform);
   OpenWorldAudio.Play("sfx_power_select", 0.35f);
   gm?.ShowBanner("Aster whistles for her horse");
  }
 }

 public sealed partial class OpenWorldHero {
  public OpenWorldAnimalInteraction Mount { get; private set; }
  public bool IsMounted => Mount;
  // Hopping off: the root travels from the saddle to the ground beside the horse.
  float dismountT = 1f;
  Vector3 dismountFrom, dismountTo;
  OpenWorldMountRig dismountRig;
  public bool IsDismounting => dismountT < 1f;

  public void EnterMount(OpenWorldAnimalInteraction animal) {
   if (Mount || Dead || IsDismounting || !Grounded || IsClimbing || IsSwimming || IsGliding || IsKnockedDown || !animal.Mount(this)) return;
   EndAction(); CancelPendingStrikes(); dashing = sprinting = false;
   velocity = planarVelocity = Vector3.zero; LockOnTarget = null;
   SetGliding(false);
   Mount = animal; Controller.enabled = false;
   Visual?.SetLocomotion(0f, false); Visual?.Play("idle");
   OpenWorldManager.I?.ShowBanner(animal.IsBucking?"HOLD STEADY • The wild horse is bucking":(animal.JustTamed ? "Aster calms the wild horse • " : "Mounted • ") + "Move to ride • Hold Dash to gallop • Interact to dismount" + (Application.isMobilePlatform ? "" : " • H whistles for her"));
  }
  public void LeaveMount(bool force = false) {
   if (!Mount) return;
   if(!force&&Mount.IsBucking)return;
   if (!Mount.DismountPoint(this, out Vector3 point) && !force) { OpenWorldManager.I?.ShowBanner("Move to clear ground before dismounting"); return; }
   var horse = Mount;
   horse.Release(); Mount = null;
   if (force) {
    // Thrown or teleported: no hop.
    dismountT = 1f;
    transform.position = point;
    Controller.enabled = true; velocity = planarVelocity = Vector3.zero;
    Grounded = true; ClearInput(); Visual?.EndRide(); Visual?.Play("idle");
    return;
   }
   dismountT = 0f; dismountFrom = transform.position; dismountTo = point; dismountRig = horse.Rig;
   ClearInput();
  }
  // Runs from Update while hopping off; true while the hop is in progress.
  bool StepDismount() {
   if (dismountT >= 1f) return false;
   dismountT = Mathf.Min(1f, dismountT + Time.deltaTime / 0.42f);
   float u = dismountT * dismountT * (3f - 2f * dismountT);
   transform.position = Vector3.Lerp(dismountFrom, dismountTo, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.35f);
   Visual?.SetRide(dismountRig, 1f - u, 0f, 0f, 0f);
   if (dismountT >= 1f) {
    Controller.enabled = true; velocity = planarVelocity = Vector3.zero; Grounded = true;
    Visual?.EndRide(); Visual?.PlayAt("land", "slash", 1.2f, 0f, 0.1f);
   }
   return true;
  }
  // While mounted only the one-shot buttons are cleared: the joystick value stays, because the touch
  // stick only writes it on drag events (holding the thumb still must keep the horse going).
  void ClearMountedInput() {
   TouchJump = TouchDodge = TouchAttack = TouchCharged = TouchParry = TouchSpell = TouchEcho = TouchBurst = false;
  }
 }
}
