using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // A herd, pack or flock spawned together: shares its alarm and keeps its members' homes near the
 // planned group centre.
 public sealed class OpenWorldHerd {
  public readonly List<OpenWorldWildlife> Members = new List<OpenWorldWildlife>();
  public Vector3 Origin;
  public float Range = 55f;
  public float PanicUntil;
  public Vector3 PanicFrom;
  public void Alarm(Vector3 from, float seconds) {
   PanicFrom = from;
   PanicUntil = Mathf.Max(PanicUntil, Time.time + seconds);
  }
  public Vector3 Centroid(Vector3 fallback) {
   Vector3 sum = Vector3.zero; int n = 0;
   foreach (var m in Members) if (m && m.isActiveAndEnabled) { sum += m.transform.position; n++; }
   return n > 0 ? sum / n : fallback;
  }
 }

 // Ambient animal behaviour (Zelda / RDR2 style):
 //  - Skittish prey (deer, elk, hares, wild horses) notice Aster sooner the faster and louder she moves:
 //    for a deer sprinting ~15 m, running ~10 m, walking ~4 m, standing ~3 m (scaled per species). They freeze and
 //    stare for a moment, then bolt; the whole herd goes with the first one that runs. They burst away
 //    faster than she can sprint, then lope, keep running while she follows, and settle to graze well
 //    out of reach. For half a minute after a scare they are jumpy (notice her from further away).
 //  - Wary animals (foxes, hens, penguins) only scatter a short way when she rushes at them.
 //  - Tame village animals stay calm; dogs trot over to greet her.
 //  - Birds peck and hop on paths, rocks and rooftops, take off when she comes close (the whole flock
 //    goes), fly off and land where the grass is short enough to see them.
 // Movement: they turn at a limited rate and walk where they face (no sliding sideways), accelerate,
 // brake, avoid steep ground, deep water and obstacles, tilt with the ground and lean into turns.
 // Clips play at the rate that matches the real speed (OpenWorldImportedModel).
 public sealed class OpenWorldWildlife : MonoBehaviour {
  public enum Temper { Skittish, Wary, Tame }
  public bool Flying;
  public Temper Nature = Temper.Skittish;
  public float WalkSpeed = 1.1f, RunSpeed = 5f, Awareness = 9f, FleeDistance = 26f, HomeRadius = 9f, TurnRate = 170f;
  public bool Friendly, ZigZag;
  public bool Bounding; // hares: the body bounds in arcs when it runs (their clip is a slow hop)
  public string Voice = ""; // sound bank group for its call ("crows"); songbirds use the shared chirp
  public OpenWorldHerd Herd;

  enum Mode { Idle, Wander, Alert, Flee, Settle, Greet, Summoned, Perch, TakeOff, Flight, Land }
  Mode mode = Mode.Idle;
  Vector3 home, target, wantDir, fleeFrom;
  float speed, wantSpeed, modeUntil, alertAt, nextChirp, greetCooldown, personal, yawRate, lastYaw, bank;
  string idleState = "idle";
  OpenWorldImportedModel anim;
  OpenWorldAnimalInteraction interaction;
  Transform model;
  OpenWorldBodyTilt tilt;
  float length = 1f, width = 0.4f, height = 1f;
  Vector3 lastHero; bool heroKnown; float heroSpeed;
  AudioSource birdVoice;
  Transform summonTarget;
  float altitude, climb;
  bool started;
  float fleeStart, waryUntil;           // burst then lope; jumpy for a while after a scare
  float hopPhase; Vector3 modelBase; bool modelBaseKnown;
  float summonBest, summonProgressAt;   // a summoned horse that stops getting closer is brought round
  static readonly RaycastHit[] hits = new RaycastHit[8];

  public bool IsFleeing => mode == Mode.Flee;
  public string ModeName => mode.ToString();
  public bool Airborne => mode == Mode.TakeOff || mode == Mode.Flight || mode == Mode.Land;

  public void ResetHome() { home = target = transform.position; mode = Flying ? Mode.Perch : Mode.Idle; modeUntil = 0f; speed = 0f; summonTarget = null; }

  // Called when struck or when a fight breaks out next to it.
  public void Startle(Vector3 from) {
   if (Flying) { if (!Airborne) BeginTakeOff(from); return; }
   if (Nature == Temper.Tame && !interaction) return;
   BeginFlee(from);
   Herd?.Alarm(from, 5f);
  }

  // While ridden the rider's mount code moves the body; the model still tilts with the ground and banks.
  public void ExternalTilt(float bankDegrees, float dt) {
   if (!started) return;
   if (height > 0.45f) tilt.Step(model, transform, length, width, bankDegrees, dt);
  }

  // Horse whistle: come to the caller and wait there.
  public void Summon(Transform caller) {
   if (!caller) return;
   summonTarget = caller; mode = Mode.Summoned; modeUntil = Time.time + 40f;
   summonBest = float.MaxValue; summonProgressAt = Time.time;
  }

  void Start() {
   started = true;
   anim = GetComponentInChildren<OpenWorldImportedModel>();
   interaction = GetComponent<OpenWorldAnimalInteraction>();
   model = anim ? anim.transform : transform.childCount > 0 ? transform.GetChild(0) : null;
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(transform);
   height = Mathf.Max(0.05f, b.size.y);
   length = Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z));
   width = Mathf.Max(0.05f, Mathf.Min(b.size.x, b.size.z));
   personal = Random.Range(0.88f, 1.12f);
   home = target = transform.position;
   lastYaw = transform.eulerAngles.y;
   mode = Flying ? Mode.Perch : Mode.Idle;
   modeUntil = Time.time + Random.Range(0.5f, 4f);
   PickIdle();
  }

  void OnEnable() { if (started) { speed = 0f; heroKnown = false; } }
  // Killed mid-bound (or switched off for riding): the body goes back down to the ground first.
  void OnDisable() { if (Bounding && modelBaseKnown && model) model.localPosition = modelBase; }

  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.IsModalOpen) return;
   float dt = Time.deltaTime;
   if (dt <= 0f) return;
   if (interaction && (interaction.Dead || interaction.Rider)) return;
   var hero = gm.Hero;
   Vector3 hp = hero.IsMounted && hero.Mount ? hero.Mount.transform.position : hero.transform.position;
   if (heroKnown) {
    Vector3 hv = hp - lastHero; hv.y = 0f;
    heroSpeed = Mathf.Lerp(heroSpeed, Mathf.Min(hv.magnitude / dt, 30f), 1f - Mathf.Exp(-6f * dt));
   }
   lastHero = hp; heroKnown = true;
   Vector3 toHero = hp - transform.position; toHero.y = 0f;
   float d = toHero.magnitude;
   if (!hero.Dead && interaction && interaction.BeingPetted) {
    wantSpeed = 0f; wantDir = toHero; Steer(dt, gm, d); Animate(dt, "idle"); return;
   }
   if (Flying) { StepBird(dt, gm, hero, hp, d); return; }
   Think(gm, hero, hp, d);
   Steer(dt, gm, d);
   Animate(dt, null);
  }

  // ---------------------------------------------------------------- ground animals

  float AlertRadius(OpenWorldHero hero) {
   float v = heroSpeed;
   // Sprinting is heard from far, running from a fair way off; walking slowly lets her get close (about
   // four metres from a deer, inside a spell's reach), standing still closer still.
   float f = v > 6.5f ? 1.5f : v > 3.2f ? 1f : v > 0.6f ? 0.42f : 0.3f;
   if (hero.IsMounted) f = v > 7f ? 1.3f : v > 3f ? 0.8f : 0.45f;  // a rider walking up spooks them less
   if (hero.IsSwimming) f *= 0.5f;
   if (hero.IsGliding) f *= 0.7f;
   if (Time.time < waryUntil) f *= 1.7f;
   return Awareness * f;
  }

  void Think(OpenWorldManager gm, OpenWorldHero hero, Vector3 hp, float d) {
   float now = Time.time;
   bool threat = !hero.Dead;
   // The herd's alarm reaches everyone, a beat apart.
   if (Herd != null && Herd.PanicUntil > now && mode != Mode.Flee && mode != Mode.Summoned && Nature != Temper.Tame) {
    if (alertAt <= 0f) alertAt = now + Random.Range(0.05f, 0.45f);
    if (now >= alertAt) { BeginFlee(Herd.PanicFrom); alertAt = 0f; }
   }
   switch (mode) {
    case Mode.Summoned: {
     if (!summonTarget || now > modeUntil) { mode = Mode.Idle; summonTarget = null; PickIdle(); break; }
     Vector3 to = summonTarget.position - transform.position; to.y = 0f;
     float far = to.magnitude;
     if (far < 3.2f) { mode = Mode.Idle; modeUntil = now + 30f; summonTarget = null; home = transform.position; idleState = "idle"; wantSpeed = 0f; wantDir = to; break; }
     // Penned in or stuck behind a wall: once it is out of view (or has tried for a while) it is brought
     // round behind Aster, the way a far-off horse is when she whistles.
     if (far < summonBest - 0.5f) { summonBest = far; summonProgressAt = now; }
     else if (now - summonProgressAt > 2.5f) {
      var cam = Camera.main;
      bool inView = cam && far < 45f && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), OpenWorldMaterialHelper.ComputeRendererBounds(transform));
      if ((!inView || now - summonProgressAt > 8f) && OpenWorldAnimalInteraction.BringRound(transform, summonTarget, 16f)) { summonBest = float.MaxValue; summonProgressAt = now; speed = RunSpeed * 0.6f; }
      else if (!inView) summonProgressAt = now;
     }
     wantDir = to; wantSpeed = far > 14f ? RunSpeed : Mathf.Lerp(WalkSpeed, RunSpeed * 0.6f, Mathf.InverseLerp(4f, 14f, far));
     return;
    }
    case Mode.Flee: {
     // Keep running from where she is now, not from where she was first seen.
     if (threat && d < FleeDistance * 0.9f) fleeFrom = hp;
     Vector3 away = transform.position - fleeFrom; away.y = 0f;
     float fromThreat = away.magnitude;
     if (away.sqrMagnitude < 0.01f) away = transform.forward;
     away.Normalize();
     // Stay together: steer a little toward the herd while running.
     if (Herd != null && Herd.Members.Count > 1) {
      Vector3 c = Herd.Centroid(transform.position) - transform.position; c.y = 0f;
      if (c.magnitude > 3f) away = (away + c.normalized * 0.3f).normalized;
     }
     // Keep clear of the edge of the home range: bend along it rather than out of it.
     Vector3 fromOrigin = transform.position - (Herd != null ? Herd.Origin : home); fromOrigin.y = 0f;
     float range = Herd != null ? Herd.Range : 55f;
     if (fromOrigin.magnitude > range * 0.85f) away = (away - fromOrigin.normalized * 0.8f).normalized;
     if (ZigZag) away = Quaternion.AngleAxis(Mathf.Sin(now * 5.3f + personal * 10f) * 38f, Vector3.up) * away;
     wantDir = away;
     // A burst at full speed for a few seconds, then a lope (the herd keeps together at about one pace).
     float run = now - fleeStart;
     float pace = run < 3.5f ? 1f : Mathf.Lerp(1f, 0.72f, (run - 3.5f) / 3f);
     wantSpeed = RunSpeed * Mathf.Lerp(1f, personal, 0.35f) * pace;
     bool far = fromThreat > FleeDistance && d > FleeDistance * 0.8f;
     bool spent = now > modeUntil && d > FleeDistance * 0.55f;
     if (far || spent || now > modeUntil + 10f) { mode = Mode.Settle; modeUntil = now + Random.Range(1.6f, 3f); }
     return;
    }
    case Mode.Settle:
     wantDir = transform.forward; wantSpeed = WalkSpeed;
     if (now > modeUntil) { SetHomeHere(); mode = Mode.Idle; modeUntil = now + Random.Range(3f, 7f); PickIdle(); }
     break;
    case Mode.Alert:
     wantSpeed = 0f; wantDir = hp - transform.position;
     if (threat && d < AlertRadius(hero) * 0.55f) { BeginFlee(hp); Herd?.Alarm(hp, 5f); return; }
     if (now >= alertAt) {
      if (threat && d < AlertRadius(hero) * 1.15f) { BeginFlee(hp); Herd?.Alarm(hp, 5f); }
      else { mode = Mode.Idle; modeUntil = now + Random.Range(2f, 5f); PickIdle(); }
     }
     return;
    case Mode.Greet:
     if (hero.Dead || d > 12f || now > modeUntil) { mode = Mode.Wander; PickTarget(); greetCooldown = now + 25f; break; }
     wantDir = hp - transform.position;
     wantSpeed = d > 2.4f ? Mathf.Lerp(WalkSpeed, RunSpeed * 0.7f, Mathf.InverseLerp(2.4f, 8f, d)) : 0f;
     return;
   }
   // Notice Aster.
   if (threat) {
    float r = AlertRadius(hero);
    if (Nature == Temper.Skittish && d < r) {
     if (d < r * 0.5f) { BeginFlee(hp); Herd?.Alarm(hp, 5f); }
     else { mode = Mode.Alert; alertAt = now + Random.Range(0.35f, 0.9f); }
     return;
    }
    if (Nature == Temper.Wary && d < r && heroSpeed > 2.5f) { BeginFlee(hp); return; }
    if (Nature == Temper.Tame) {
     if (Friendly && d < 7f && heroSpeed < 3.5f && now > greetCooldown && !hero.IsMounted) { mode = Mode.Greet; modeUntil = now + Random.Range(5f, 8f); return; }
     if (!Friendly && height < 0.6f && d < Awareness && heroSpeed > 6f) { BeginFlee(hp); return; } // cats dart off from a sprint (horses stay)
    }
   }
   switch (mode) {
    case Mode.Idle:
     wantSpeed = 0f;
     if (Time.time > modeUntil) { mode = Mode.Wander; PickTarget(); }
     break;
    case Mode.Wander: {
     Vector3 to = target - transform.position; to.y = 0f;
     if (to.magnitude < 0.6f || Time.time > modeUntil) { mode = Mode.Idle; modeUntil = Time.time + Random.Range(3f, 9f); PickIdle(); wantSpeed = 0f; break; }
     wantDir = to; wantSpeed = WalkSpeed * personal * Mathf.Clamp01(to.magnitude / 1.5f + 0.35f);
     break;
    }
   }
  }

  void BeginFlee(Vector3 from) {
   fleeFrom = from;
   if (mode != Mode.Flee) fleeStart = Time.time;
   mode = Mode.Flee;
   modeUntil = Time.time + Random.Range(6f, 9f);
   waryUntil = Time.time + 30f;
   idleState = "idle";
  }

  void SetHomeHere() {
   home = transform.position;
   if (Herd == null) return;
   Vector3 off = home - Herd.Origin; off.y = 0f;
   if (off.magnitude > Herd.Range) home = Herd.Origin + off.normalized * Herd.Range;
  }

  void PickTarget() {
   Vector2 r = Random.insideUnitCircle * HomeRadius;
   target = home + new Vector3(r.x, 0f, r.y);
   var terrain = OpenWorldManager.I ? OpenWorldManager.I.ActiveTerrain : null;
   if (!Flying && terrain && height < 1.2f) {
    float best = float.MaxValue;
    for (int i = 0; i < 10; i++) {
     Vector2 offset = Random.insideUnitCircle * HomeRadius;
     Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
     if (!OpenWorldGrounding.TryWalk(terrain, transform.position, candidate, out float ground) || OpenWorldTerrainSculptor.WaterAt(candidate.x, candidate.z, out _, out _)) continue;
     candidate.y = ground;
     if (OpenWorldPopulation.Blocked(candidate, .6f, .15f)) continue;
     float grass = GrassAt(terrain, candidate);
     if (grass < best) { best = grass; target = candidate; }
    }
   }
   modeUntil = Time.time + 14f;
  }

  void PickIdle() {
   var terrain = OpenWorldManager.I ? OpenWorldManager.I.ActiveTerrain : null;
   if (!Flying && height < 1.2f && terrain && GrassAt(terrain, transform.position) > 2f) {
    PickTarget();
    if (GrassAt(terrain, target) < GrassAt(terrain, transform.position)) { mode = Mode.Wander; return; }
   }
   float roll = Random.value;
   if (anim && anim.Rest && roll < 0.12f && Nature != Temper.Skittish) { idleState = "rest"; modeUntil = Time.time + Random.Range(10f, 22f); }
   else if (anim && anim.Graze && roll < 0.6f) idleState = "graze";
   else idleState = "idle";
  }

  void Steer(float dt, OpenWorldManager gm, float heroDistance) {
   float accel = wantSpeed > speed ? (mode == Mode.Flee || mode == Mode.Summoned ? 8f : 2.5f) : (mode == Mode.Flee ? 4f : 5f);
   speed = Mathf.MoveTowards(speed, wantSpeed, accel * dt);
   if (wantDir.sqrMagnitude > 0.0001f) {
    float rate = TurnRate * (mode == Mode.Flee ? 1.4f : 1f) * (speed > RunSpeed * 0.7f ? 0.7f : 1f);
    if (speed < 0.1f) rate *= 0.6f; // turning on the spot is slower
    var want = Quaternion.LookRotation(new Vector3(wantDir.x, 0f, wantDir.z));
    transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
    // Do not walk off at a right angle to where it is heading: slow until it has turned.
    float off = Vector3.Angle(transform.forward, new Vector3(wantDir.x, 0f, wantDir.z));
    if (off > 60f) speed = Mathf.MoveTowards(speed, Mathf.Min(speed, WalkSpeed * 0.6f), accel * 2f * dt);
   }
   float yaw = transform.eulerAngles.y;
   yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-8f * dt));
   lastYaw = yaw;
   if (speed < 0.02f) return;
   if (!gm.ActiveTerrain) return;
   Vector3 pos = transform.position;
   Vector3 dir = transform.forward;
   float step = speed * dt;
   if (TryStep(gm, pos, dir, step, out Vector3 next)) { transform.position = next; return; }
   foreach (float a in new[] { 35f, -35f, 70f, -70f }) {
    Vector3 d2 = Quaternion.AngleAxis(a, Vector3.up) * dir;
    if (!TryStep(gm, pos, d2, step, out next)) continue;
    transform.position = next;
    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d2), TurnRate * 2f * dt);
    return;
   }
   speed *= 0.4f;
   if (mode == Mode.Wander) { mode = Mode.Idle; modeUntil = Time.time + Random.Range(1f, 3f); }
   else if (mode == Mode.Flee) fleeFrom = transform.position - Quaternion.AngleAxis(Random.Range(-120f, 120f), Vector3.up) * transform.forward * 5f;
  }

  bool TryStep(OpenWorldManager gm, Vector3 pos, Vector3 dir, float step, out Vector3 next) {
   next = pos + dir * step;
   var terrain = gm.ActiveTerrain;
   if (!OpenWorldGrounding.TryWalk(terrain, pos, next, out float ground)) return false;
   if (Mathf.Abs(next.x) > OpenWorldRegions.PlayableHalf - 6f || Mathf.Abs(next.z) > OpenWorldRegions.PlayableHalf - 6f) return false;
   // Shallow fords are fine (they wade), deeper water is not.
   if (OpenWorldTerrainSculptor.WaterAt(next.x, next.z, out float surface, out float depth) && surface - ground > height * 0.35f) return false;
   Vector3 origin = pos + Vector3.up * Mathf.Clamp(height * 0.5f, 0.15f, 1.1f);
   float radius = Mathf.Clamp(width * 0.4f, 0.08f, 0.45f);
   int n = Physics.SphereCastNonAlloc(origin, radius, dir, hits, step + length * 0.45f, ~0, QueryTriggerInteraction.Ignore);
   for (int i = 0; i < n; i++) {
    var h = hits[i];
    if (!h.collider || h.collider.transform.IsChildOf(transform) || h.collider is TerrainCollider) continue; // slopes are TryWalk's job
    if (h.rigidbody && !h.rigidbody.isKinematic) continue;      // loose props get knocked, not walked around
    if (h.distance <= 0f) continue;                               // already overlapping: let it walk out
    return false;
   }
   next.y = ground + 0.02f;
   return true;
  }

  void Animate(float dt, string forced) {
   if (anim) {
    string s;
    if (forced != null) s = forced;
    else if (speed < 0.12f) s = mode == Mode.Idle ? idleState : "idle";
    else {
     float wn = anim.NaturalSpeed(false), rn = anim.NaturalSpeed(true);
     float split = wn > 0.05f && rn > wn * 1.3f ? (wn + rn) * 0.5f : Mathf.Max(WalkSpeed * 1.6f, RunSpeed * 0.5f);
     s = speed > split ? "run" : "walk";
    }
    anim.State = s;
   }
   float targetBank = Mathf.Clamp(-yawRate * speed * 0.012f, -12f, 12f);
   bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-6f * dt));
   if (height > 0.45f) tilt.Step(model, transform, length, width, bank, dt);
   else if (Bounding && model) {
    // Bounds of about four and a half body heights, as high as half the body, when it really runs.
    if (!modelBaseKnown) { modelBase = model.localPosition; modelBaseKnown = true; }
    float lift = 0f;
    if (speed > 1.2f) {
     hopPhase += speed * dt / Mathf.Max(0.3f, height * 4.5f);
     lift = Mathf.Abs(Mathf.Sin(hopPhase * Mathf.PI)) * height * 0.6f * Mathf.Clamp01((speed - 1.2f) / 3f);
    }
    model.localPosition = Vector3.MoveTowards(model.localPosition, modelBase + Vector3.up * lift, dt * 6f);
   }
  }

  // ---------------------------------------------------------------- birds

  void StepBird(float dt, OpenWorldManager gm, OpenWorldHero hero, Vector3 hp, float d) {
   float now = Time.time;
   var terrain = gm.ActiveTerrain;
   if (!terrain) return;
   float Ground(Vector3 q) => terrain.SampleHeight(q) + terrain.transform.position.y;
   if (d < 22f && now > nextChirp) {
    nextChirp = now + Random.Range(7f, 16f);
    var clip = string.IsNullOrEmpty(Voice) ? OpenWorldImportedContent.Sound("sfx_bird") : null;
    if (!string.IsNullOrEmpty(Voice)) OpenWorldSoundscape.Sfx(Voice, pos0(), 0.35f);
    if (clip) {
     if (!birdVoice) { birdVoice = gameObject.AddComponent<AudioSource>(); birdVoice.playOnAwake = false; birdVoice.spatialBlend = 1; birdVoice.minDistance = 2; birdVoice.maxDistance = 22; birdVoice.volume = .2f; }
     birdVoice.pitch = Random.Range(0.92f, 1.12f);
     birdVoice.PlayOneShot(clip);
    }
   }
   Vector3 pos = transform.position;
   float ground = Ground(pos);
   switch (mode) {
    default:
    case Mode.Perch: {
     bool flush = !hero.Dead && (d < 6f || (d < 11f && heroSpeed > 4f));
     if (Herd != null && Herd.PanicUntil > now) flush = true;
     if (flush) { BeginTakeOff(hp); Herd?.Alarm(hp, 2f); break; }
     float surface = SurfaceY(pos, ground);
     if (now > modeUntil) {
      // Now and then it flies off on its own, and often the flock goes with it.
      if (Random.value < 0.12f) { BeginTakeOff(pos - transform.forward); if (Random.value < 0.5f) Herd?.Alarm(pos, 0.6f); break; }
      // Hop a little, peck, look around.
      float roll = Random.value;
      if (roll < 0.35f) { target = pos + Quaternion.AngleAxis(Random.Range(-90f, 90f), Vector3.up) * transform.forward * Random.Range(0.3f, 1.2f); idleState = "walk"; modeUntil = now + 1.2f; }
      else { idleState = anim && anim.Graze && roll < 0.75f ? "graze" : "idle"; modeUntil = now + Random.Range(1.5f, 4f); target = pos; }
     }
     Vector3 to = target - pos; to.y = 0f;
     if (idleState == "walk" && to.magnitude > 0.05f) {
      transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);
      Vector3 next = pos + to.normalized * Mathf.Min(to.magnitude, (anim ? Mathf.Max(0.2f, anim.NaturalSpeed(false)) : 0.35f) * dt);
      float nextSurface = SurfaceY(next, Ground(next));
      // Hops stay on the rock, roof or path it is on (no stepping off an edge into the air).
      if (Mathf.Abs(nextSurface - surface) < 0.3f && !OpenWorldTerrainSculptor.WaterAt(next.x, next.z, out _, out _)) { next.y = nextSurface + 0.01f; transform.position = next; }
      else { idleState = "idle"; target = pos; }
      if (anim) anim.State = "walk";
     } else {
      if (idleState == "walk") idleState = "idle";
      pos.y = Mathf.MoveTowards(pos.y, surface + 0.01f, dt * 2f); transform.position = pos;
      if (anim) anim.State = idleState;
     }
     break;
    }
    case Mode.TakeOff: {
     speed = Mathf.MoveTowards(speed, 5f, 9f * dt);
     climb = Mathf.MoveTowards(climb, 3.2f, 10f * dt);
     transform.position = pos + transform.forward * speed * dt + Vector3.up * climb * dt;
     if (pos.y - ground > altitude * 0.6f || now > modeUntil) { mode = Mode.Flight; modeUntil = now + Random.Range(4f, 9f); }
     if (anim) anim.State = "fly";
     break;
    }
    case Mode.Flight: {
     Vector3 to = target - pos; to.y = 0f;
     float wantAlt = ground + altitude;
     climb = Mathf.Lerp(climb, Mathf.Clamp((wantAlt - pos.y) * 1.2f, -2.5f, 2.5f), 1f - Mathf.Exp(-3f * dt));
     if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 110f * dt);
     speed = Mathf.MoveTowards(speed, 5.5f * personal, 4f * dt);
     transform.position = pos + transform.forward * speed * dt + Vector3.up * climb * dt;
     if (to.magnitude < 5f && now > modeUntil - 3f) mode = Mode.Land;
     if (now > modeUntil + 10f) mode = Mode.Land;
     if (anim) anim.State = "fly";
     break;
    }
    case Mode.Land: {
     // A glide path down to the landing spot (its y is the top of the perch: path, rock or roof).
     Vector3 to = target - pos; to.y = 0f;
     float flat = to.magnitude;
     if (to.sqrMagnitude > 0.04f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 160f * dt);
     speed = Mathf.MoveTowards(speed, Mathf.Clamp(flat * 1.2f, 0.6f, 4.5f), 5f * dt);
     float wantY = target.y + flat * 0.5f;
     climb = Mathf.MoveTowards(climb, Mathf.Clamp((wantY - pos.y) * 2.2f, -2.8f, 0.6f), 8f * dt);
     Vector3 next = pos + transform.forward * Mathf.Min(speed * dt, Mathf.Max(flat, 0.02f)) + Vector3.up * climb * dt;
     float g = Ground(next);
     bool arrived = flat < 0.3f && next.y <= target.y + 0.08f;
     if (arrived || next.y <= g + 0.01f) {
      float rest = arrived ? Mathf.Max(target.y, g) : g;
      next.y = rest + 0.01f; transform.position = next;
      if (rest <= g + 0.05f && OpenWorldTerrainSculptor.WaterAt(next.x, next.z, out _, out _)) { BeginTakeOff(next - transform.forward); break; }
      mode = Mode.Perch; speed = 0f; climb = 0f; home = next; target = next; modeUntil = now + Random.Range(2f, 6f); idleState = "idle";
     } else transform.position = next;
     if (anim) anim.State = next.y - Mathf.Max(g, target.y) > 0.25f ? "fly" : "idle";
     break;
    }
   }
   // Nose up while climbing, down while descending.
   if (model && Airborne) {
    float pitch = Mathf.Clamp(-climb * 9f, -25f, 25f);
    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(pitch, 0f, Mathf.Clamp(-yawRate * 0.08f, -25f, 25f)), 1f - Mathf.Exp(-6f * dt));
   } else if (model) model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
   float yaw = transform.eulerAngles.y;
   yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-8f * dt));
   lastYaw = yaw;
  }

  Vector3 pos0() => transform.position;

  void BeginTakeOff(Vector3 from) {
   Vector3 away = transform.position - from; away.y = 0f;
   if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
   away.y = 0f;
   away = Quaternion.AngleAxis(Random.Range(-40f, 40f), Vector3.up) * away.normalized;
   transform.rotation = Quaternion.LookRotation(away);
   mode = Mode.TakeOff; modeUntil = Time.time + 1.4f;
   OpenWorldSoundscape.Sfx("bird_flyaway", transform.position, 0.3f);
   altitude = Random.Range(5f, 11f);
   speed = 1.5f; climb = 1f;
   // Land again a little way off, inside the flock's range.
   Vector3 origin = Herd != null ? Herd.Origin : home;
   Vector3 land = transform.position + away * Random.Range(12f, 28f);
   Vector3 off = land - origin; off.y = 0f;
   float range = Herd != null ? Herd.Range : 30f;
   if (off.magnitude > range) land = origin + Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * off.normalized * range * 0.6f;
   target = PickLanding(land, origin, range, from);
  }

  // Where to come down near a preferred point: the top of a rock, a roof or a fence rail first, then a
  // path or short grass (a bird in tall grass cannot be seen), never water or right beside the threat.
  Vector3 PickLanding(Vector3 preferred, Vector3 origin, float range, Vector3 threat) {
   var gm = OpenWorldManager.I;
   var terrain = gm ? gm.ActiveTerrain : null;
   if (!terrain) return preferred;
   Vector3 best = preferred; float bestScore = float.MaxValue;
   for (int i = 0; i < 16; i++) {
    Vector3 c = i == 0 ? preferred : preferred + Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward * Random.Range(2f, 10f);
    Vector3 o = c - origin; o.y = 0f;
    if (o.magnitude > range) c = origin + o.normalized * range;
    if (OpenWorldTerrainSculptor.WaterAt(c.x, c.z, out _, out _)) continue;
    float ground = terrain.SampleHeight(c) + terrain.transform.position.y;
    float top = ground, score;
    if (Physics.Raycast(new Vector3(c.x, ground + 12f, c.z), Vector3.down, out var hit, 12.2f, ~0, QueryTriggerInteraction.Ignore)
        && !(hit.collider is TerrainCollider) && hit.normal.y > 0.75f && hit.point.y - ground < 7f && !hit.collider.GetComponentInParent<OpenWorldHero>()
        && !hit.collider.GetComponentInParent<OpenWorldEnemy>()) { top = hit.point.y; score = -6f; }
    else score = GrassAt(terrain, c) * 0.5f + (OpenWorldTerrainSculptor.RoadDistance(c.x, c.z) < 2f ? -4f : 0f);
    Vector3 fromThreat = c - threat; fromThreat.y = 0f;
    if (fromThreat.magnitude < 8f) score += 8f;
    if (score < bestScore) { bestScore = score; best = new Vector3(c.x, top, c.z); }
   }
   return best;
  }

  // Top of whatever is under the bird: a rock, a roof, a fence rail, or the ground.
  float SurfaceY(Vector3 p, float ground) {
   float from = Mathf.Max(p.y, ground) + 0.5f;
   int n = Physics.RaycastNonAlloc(new Vector3(p.x, from, p.z), Vector3.down, hits, from - ground + 0.05f, ~0, QueryTriggerInteraction.Ignore);
   float best = ground;
   for (int i = 0; i < n; i++) {
    var h = hits[i];
    if (!h.collider || h.collider is TerrainCollider || h.normal.y < 0.6f || h.collider.transform.IsChildOf(transform)) continue;
    if (h.collider.GetComponentInParent<OpenWorldHero>() || h.collider.GetComponentInParent<OpenWorldEnemy>()) continue;
    if (h.point.y > p.y + 0.15f) continue; // something overhead (a cart bed, eaves): not a perch
    if (h.point.y > best) best = h.point.y;
   }
   return best;
  }

  // Terrain grass at a point (summed detail layers): how hidden a small animal on the ground would be.
  static Terrain grassTerrain; static int grassLayers;
  public static float GrassAt(Terrain terrain, Vector3 p) {
   if (!terrain || !terrain.terrainData) return 0f;
   var td = terrain.terrainData;
   if (grassTerrain != terrain) { grassTerrain = terrain; grassLayers = td.detailPrototypes.Length; }
   int res = td.detailResolution;
   if (res <= 0 || grassLayers == 0) return 0f;
   Vector3 local = p - terrain.transform.position;
   int x = Mathf.Clamp((int)(local.x / td.size.x * res), 0, res - 1), z = Mathf.Clamp((int)(local.z / td.size.z * res), 0, res - 1);
   float sum = 0f;
   for (int layer = 0; layer < grassLayers; layer++) sum += td.GetDetailLayer(x, z, 1, 1, layer)[0, 0];
   return sum;
  }
 }
}
