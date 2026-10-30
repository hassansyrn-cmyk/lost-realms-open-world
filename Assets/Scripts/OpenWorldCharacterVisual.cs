using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LostRealmsOW {
 // Playables animation driver for Aster, villagers and open-world enemies.
 // Clips come from the Phase 1 baker with every RootT curve flattened to 0, so the capsule moves the body
 // and no clip can pop or teleport the model. On top of that:
 //  - Locomotion: idle / walk / run / sprint blended by speed, phase-synchronised on the left-foot plant,
 //    each clip played at the rate that keeps its planted foot still (stride speeds from the baker's
 //    motion meta), so feet neither skate nor stutter.
 //  - Every other state owns two mixer ports (A/B): restarting a clip, or chaining combo hits, crossfades
 //    from the current pose instead of snapping. Fades are eased and timed per kind of move.
 //  - Body height: the baker's side-car curves put crouches, rolls, landings and sprint strides back on
 //    the ground without root curves.
 //  - Hero rig (LateUpdate): foot IK on slopes and steps, lean into turns and acceleration, head look,
 //    the paraglider hang with both hands on the bar, hands and feet placed on the rock while climbing,
 //    and a procedural breaststroke / treading-water pose for swimming (no swim clips in the packs).
 //  - Riding: seated on the measured saddle of the horse's animated back, legs round the barrel with the
 //    feet in stirrup position, hands forward on the reins (drawn to the bit), a forward seat at the
 //    gallop that absorbs part of the bounce, and a lean into turns.
 //  - Physics hand-off: EnterRagdoll stops the animation so OpenWorldRagdoll can take the bones.
 public sealed class OpenWorldCharacterVisual : MonoBehaviour {
  static readonly string[] StateNames = {
   "idle", "walk", "run", "sprint", "fall", "talk",
   "attack", "attack_1", "attack_2", "attack_3", "attack_4", "charged",
   "jump", "double_jump", "land", "land_hard", "dash", "dodge", "glide",
   "hit", "gethit", "death", "block", "hit_2", "hit_3", "hit_4", "hit_5", "cast", "roll",
   "chop_1", "chop_2", "chop_3", "chop_4",
   "spear_1", "spear_2", "spear_3", "spear_4",
   "unarmed_1", "unarmed_2", "unarmed_3", "unarmed_4",
   "charged_chop", "charged_spear", "charged_unarmed",
   "cast_ember", "cast_frost", "cast_gale", "burst",
   "slam", "stomp", "sweep", "roar", "victory", "charge_ready", "eruption",
   "knockdown", "knockdown_loop", "getup", "knockback",
   "climb_idle", "climb_up", "climb_down", "climb_top"
  };
  // Shared enemy reactions keep their root height (enemy convention); Aster's flattened rig must never
  // pick them up, or she would hover. Her own versions come from the Phase 1 baker.
  static bool AsterOnly(string state) => state == "knockdown" || state == "knockdown_loop" || state == "getup" || state == "knockback" || state.StartsWith("climb_");
  static readonly string[] LocoStates = { "idle", "walk", "run", "sprint" };
  static readonly float[] DefaultStride = { 0f, 1.6f, 4.2f, 6f };

  public static readonly string[] AsterSkins = {
   "Aster",
   "Aster_Knight",
   "Aster_KnightArmored",
   "Aster_Viking",
   "Aster_Assassin",
   "Aster_DesertWarrior"
  };

  public static readonly string[] AsterSkinTitles = {
   "Wayfinder Aster",
   "Vanguard Knight",
   "Royal Plate",
   "Northland Raider",
   "Shadowblade",
   "Dune Strider"
  };

  public GameObject ModelInstance { get; private set; }
  public Animator Animator { get; private set; }
  public Transform RightHandBone { get; private set; }
  public string CurrentState { get; private set; } = "idle";
  public bool Ragdolled { get; private set; }
  // Raised on each foot plant of the locomotion cycle (true = left), for footsteps in time with the feet.
  public event Action<bool> FootPlanted;
  // Raised at the start of each swimming stroke (splash sounds and ripples).
  public event Action SwimStroke;
  public bool InLocomotion => graphValid && activeSlot == null;
  // Seconds into the current one-shot state.
  public float ActionTime => activeSlot != null ? (float)(activeSlot.useB ? activeSlot.b : activeSlot.a).GetTime() : 0f;

  sealed class Slot {
   public string state;
   public float length;
   public bool loop;
   public OWMotionClip meta;
   public AnimationClipPlayable a, b;
   public int portA, portB;
   public bool useB;
   public float speed = 1f;
  }

  [NonSerialized] PlayableGraph graph;
  [NonSerialized] AnimationMixerPlayable top, loco;
  [NonSerialized] bool graphValid;
  [NonSerialized] bool setupDone; // false again after a script reload: the first Update rebuilds the graph
  [NonSerialized] Slot activeSlot;
  [NonSerialized] Slot overlaySlot;
  [NonSerialized] float overlayUntil;
  [NonSerialized] float[] weight, target, rate, applied;
  [NonSerialized] Slot[] portSlot;
  readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
  readonly Slot[] locoSlots = new Slot[4];
  readonly float[] locoWeight = new float[4];
  readonly float[] locoRate = new float[4];
  readonly float[] locoStride = new float[4];
  [NonSerialized] Dictionary<string, OWMotionClip> meta;
  string roleName = "Aster";
  int currentSkinIndex;
  OpenWorldImportedModel importedMotion;
  Vector3 modelBaseLocal;

  // Locomotion
  float locoSpeed, locoPhase, idleTime, sprintBlend = 1f;
  bool sprintGait = true;
  readonly float[] gaitA = new float[4], gaitB = new float[4];
  bool externalSpeed;
  float externalSpeedValue, externalSpeedTime = -10f;
  Vector3 lastLocomotionPosition;
  bool locomotionInitialized;

  // Whole-body tilt (pivot at the feet)
  float lastParentYaw, bankRoll, flinchPitch, flinchRoll, turnRate;
  float hitStopUntil;

  // Hero rig
  Transform heroRoot;
  OpenWorldHero heroBody; // the hero's controller, for states the visual must respect (riding)
  Transform hips, spine, chest, neck, head, lThigh, lKnee, lFoot, rThigh, rKnee, rFoot, lArm, lElbow, lHand, rArm, rElbow, rHand, lToe, rToe;
  float footIkWeight, footIkTarget, pelvisOffset, pelvisVel, lFootOffset, rFootOffset, lFootVel, rFootVel;
  Vector3 lFootNormal = Vector3.up, rFootNormal = Vector3.up;
  float leanRoll, leanPitch, lastSpeed, accelSmoothed;
  float lookYaw, lookPitch, lookWeight;
  Transform lookTarget;
  Transform gripLeft, gripRight;
  float glideWeight, glideTarget;
  float extraLean;
  bool ragdollAnimating;
  // Climbing: body aligned with the rock, hands and feet on it.
  float climbWeight, climbTarget;
  Vector3 climbNormal = Vector3.back;
  // Swimming
  float swimWeight, swimTarget, swimSpeedTarget, swimSpeed, swimPhase, swimSurfaceY = float.NaN, swimLift;
  // Riding
  OpenWorldMountRig rideRig;
  float rideWeight, rideSpeed, rideGallop, rideTurn, rideStamp = -10f, rideLean, rideRoll, rideTorso, rideTorsoVel, rideBounce;
  OpenWorldReins reins;
  readonly RaycastHit[] footHits = new RaycastHit[6];

  public static OpenWorldCharacterVisual Attach(Transform parent, string role, int skinIndex = 0) {
   var holder = new GameObject("Visual");
   holder.transform.SetParent(parent, false);
   holder.transform.localPosition = Vector3.zero;
   holder.transform.localRotation = Quaternion.identity;
   var vis = holder.AddComponent<OpenWorldCharacterVisual>();
   vis.Build(role, skinIndex);
   return vis;
  }

  // Imported actors already own their rig/clips; do not construct and discard a fallback rig/graph.
  public static OpenWorldCharacterVisual AttachImported(Transform parent, string role, GameObject prefab, float height) {
   if (!prefab) return Attach(parent, role);
   var holder = new GameObject("Visual"); holder.transform.SetParent(parent, false);
   var vis = holder.AddComponent<OpenWorldCharacterVisual>(); vis.roleName = role;
   vis.AdoptImportedModel(prefab, height); return vis;
  }
  public void AdoptImportedModel(GameObject prefab, float height) {
   if (!prefab) return;
   DestroyGraph();
   if (ModelInstance) { ModelInstance.SetActive(false); Destroy(ModelInstance); }
   ModelInstance = Instantiate(prefab, transform, false);
   ModelInstance.name = prefab.name;
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(ModelInstance.transform);
   if (bounds.size.y > .001f) ModelInstance.transform.localScale *= height / bounds.size.y;
   bounds = OpenWorldMaterialHelper.ComputeRendererBounds(ModelInstance.transform);
   ModelInstance.transform.position += new Vector3(transform.position.x - bounds.center.x, transform.position.y - bounds.min.y, transform.position.z - bounds.center.z);
   modelBaseLocal = ModelInstance.transform.localPosition;
   Animator = ModelInstance.GetComponentInChildren<Animator>(true);
   if (Animator) { Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
   var imported = ModelInstance.GetComponent<OpenWorldImportedModel>();
   importedMotion = imported;
   if (imported) imported.Driver = this;
   RightHandBone = Animator && Animator.isHuman ? Animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
   heroRoot = null;
  }

  public void SetAsterSkin(int skinIndex) {
   skinIndex = Mathf.Clamp(skinIndex, 0, AsterSkins.Length - 1);
   if (roleName == "Aster" && currentSkinIndex == skinIndex && ModelInstance != null) return;
   string prevState = CurrentState;
   var rigRoot = heroRoot;
   DestroyGraph();
   if (ModelInstance) Destroy(ModelInstance);
   Build("Aster", skinIndex);
   if (rigRoot) EnableHeroRig(rigRoot);
   Play(prevState, true);
  }

  void Build(string role, int skinIndex) {
   roleName = role;
   currentSkinIndex = Mathf.Clamp(skinIndex, 0, AsterSkins.Length - 1);
   string prefabName = role == "Aster" ? AsterSkins[currentSkinIndex] : role;
   var prefab = Resources.Load<GameObject>("Characters/" + prefabName) ?? Resources.Load<GameObject>("Characters/" + role);

   if (prefab) {
    ModelInstance = Instantiate(prefab, transform, false);
    ModelInstance.name = prefabName + "_Model";
    // Keep the prefab's local position/scale (Aster's child carries the baselineRootY compensation).
    modelBaseLocal = ModelInstance.transform.localPosition;
    EnsureValidMaterials(ModelInstance, prefabName);
    Animator = ModelInstance.GetComponentInChildren<Animator>(true);
    if (Animator) {
     Animator.applyRootMotion = false;
     Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
     if (Animator.isHuman) RightHandBone = Animator.GetBoneTransform(HumanBodyBones.RightHand);
    }
    if (!RightHandBone) RightHandBone = FindDeepChild(ModelInstance.transform, "RightHand", "Hand_R", "mixamorig:RightHand", "weapon_r");
    SetupPlayables(role);
   } else {
    // Fallback procedural visual if a role prefab isn't present
    ModelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    ModelInstance.transform.SetParent(transform, false);
    ModelInstance.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    modelBaseLocal = ModelInstance.transform.localPosition;
    var col = ModelInstance.GetComponent<Collider>();
    if (col) Destroy(col);
    ModelInstance.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit(role, new Color(0.75f, 0.35f, 0.25f));
   }
  }

  static void EnsureValidMaterials(GameObject root, string prefabName) {
   var mat = Resources.Load<Material>("Materials/" + prefabName) ?? Resources.Load<Material>("Materials/Aster");
   var tex = Resources.Load<Texture2D>("Characters/Textures/" + prefabName + "_basecolor");
   foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
    if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
    bool invalid = r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("InternalError");
    if (invalid || r.sharedMaterial.shader.name == "Standard") {
     if (mat && mat.shader && !mat.shader.name.Contains("InternalError")) {
      if (mat.shader.name == "Standard") {
       Texture main = mat.mainTexture;
       mat.shader = OpenWorldMaterialHelper.LitShader;
       if (main) { mat.mainTexture = main; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", main); }
      }
      r.sharedMaterial = mat;
     } else {
      Texture existingTex = r.sharedMaterial ? r.sharedMaterial.mainTexture : tex;
      Color existingCol = r.sharedMaterial && r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.color : Color.white;
      r.sharedMaterial = OpenWorldMaterialHelper.CreateLit(prefabName, existingCol, existingTex as Texture2D);
     }
    }
   }
  }

  void SetupPlayables(string role) {
   if (!Animator) return;
   DestroyGraph();
   slots.Clear();
   for (int i = 0; i < 4; i++) locoSlots[i] = null;
   meta = OpenWorldMotionMeta.For(role == "Aster" ? "Aster" : role);
   var sharedMeta = OpenWorldMotionMeta.For("Shared");

   string animFolder = role == "Aster" ? "Animations/Aster/" : "Animations/" + role + "/";
   var loaded = new List<(string state, AnimationClip clip, bool shared)>();
   foreach (var st in StateNames) {
    var clip = Resources.Load<AnimationClip>(animFolder + st) ?? Resources.Load<AnimationClip>("Animations/" + role + "_" + st);
    bool shared = false;
    if (!clip && !(role == "Aster" && AsterOnly(st))) { clip = Resources.Load<AnimationClip>("Animations/Shared/" + st); shared = clip; }
    if (!clip) continue;
    if (clip.humanMotion && !Animator.isHuman) continue; // a humanoid clip on a generic rig plays nothing
    loaded.Add((st, clip, shared));
   }
   if (loaded.Count == 0) return;

   graph = PlayableGraph.Create("OW_Anim_" + role);
   graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
   var output = AnimationPlayableOutput.Create(graph, "AnimOut", Animator);
   int actionCount = 0;
   foreach (var (st, _, _) in loaded) if (Array.IndexOf(LocoStates, st) < 0) actionCount++;
   int portCount = 1 + actionCount * 2;
   top = AnimationMixerPlayable.Create(graph, portCount);
   loco = AnimationMixerPlayable.Create(graph, 4);
   graph.Connect(loco, 0, top, 0);
   output.SetSourcePlayable(top);
   weight = new float[portCount]; target = new float[portCount]; rate = new float[portCount]; applied = new float[portCount];
   portSlot = new Slot[portCount];
   int port = 1;
   foreach (var (st, clip, shared) in loaded) {
    var slot = new Slot { state = st, length = Mathf.Max(0.05f, clip.length), loop = clip.isLooping };
    (shared ? sharedMeta : meta).TryGetValue(st, out slot.meta);
    int li = Array.IndexOf(LocoStates, st);
    if (li >= 0) {
     slot.a = AnimationClipPlayable.Create(graph, clip);
     slot.a.SetSpeed(0); // locomotion time is set explicitly every frame (phase sync)
     graph.Connect(slot.a, 0, loco, li);
     locoSlots[li] = slot;
    } else {
     slot.a = AnimationClipPlayable.Create(graph, clip);
     slot.b = AnimationClipPlayable.Create(graph, clip);
     slot.portA = port; slot.portB = port + 1;
     graph.Connect(slot.a, 0, top, port);
     graph.Connect(slot.b, 0, top, port + 1);
     portSlot[port] = slot; portSlot[port + 1] = slot;
     port += 2;
    }
    slots[st] = slot;
   }
   for (int i = 0; i < 4; i++) {
    var s = locoSlots[i];
    locoStride[i] = s != null && s.meta != null && s.meta.stride > 0.2f ? s.meta.stride : DefaultStride[i];
   }
   weight[0] = target[0] = applied[0] = 1f;
   for (int i = 0; i < portCount; i++) { rate[i] = 8f; top.SetInputWeight(i, weight[i]); }
   activeSlot = null; overlaySlot = null;
   UpdateLocomotion(0f);
   graph.Play();
   graphValid = true;
   setupDone = true;
  }

  public bool HasState(string state) => slots.ContainsKey(state);

  public float GetClipLength(string state, string style = "slash") {
   if (importedMotion) {
    var clip = state == "talk" ? importedMotion.Talk : state == "death" ? importedMotion.Death : state == "walk" || state == "run" ? importedMotion.Walk : state.Contains("attack") || state == "slam" ? importedMotion.Attack : state == "hit" || state == "gethit" ? importedMotion.Hit : importedMotion.Idle;
    if (clip) return clip.length;
   }
   string resolved = ResolveState(state, style);
   return slots.TryGetValue(resolved, out var slot) ? slot.length : 0.65f;
  }

  // Impact frames (seconds at playback rate 1) measured by the baker; falls back to 40% of the clip.
  public float[] GetStrikes(string state, string style = "slash") {
   string resolved = ResolveState(state, style);
   if (slots.TryGetValue(resolved, out var slot)) {
    if (slot.meta != null && slot.meta.strikes != null && slot.meta.strikes.Length > 0) return slot.meta.strikes;
    return new[] { slot.length * 0.4f };
   }
   return new[] { 0.3f };
  }

  // Baked stride of a state: planted-foot speed for locomotion, vertical speed for climbing (m/s at rate 1).
  public float GetStride(string state) {
   string resolved = ResolveState(state, "slash");
   return slots.TryGetValue(resolved, out var slot) && slot.meta != null ? slot.meta.stride : 0f;
  }

  public Vector3 FootPosition(bool left) {
   var f = left ? lFoot : rFoot;
   return f ? f.position : transform.position;
  }

  public Vector3 HandPosition(bool left) {
   var h = left ? lHand : rHand;
   return h ? h.position : transform.position + Vector3.up * 1.2f;
  }

  // Capture diagnostics: how the procedural swimming pose sits this frame (body tilt from upright,
  // bone heights, blend weights).
  public string DebugPoseInfo() {
   if (!hips) return "no hero rig";
   Vector3 top = neck ? neck.position : head ? head.position : hips.position + Vector3.up;
   float tilt = Vector3.Angle(top - hips.position, Vector3.up);
   float legs = lFoot && lThigh ? Vector3.Angle(lThigh.position - lFoot.position, Vector3.up) : -1f;
   string ride = "";
   if (rideRig && rideWeight > 0f) {
    Vector3 seat = rideRig.Seat;
    ride = " | rideW " + rideWeight.ToString("0.00") + " gallop " + rideGallop.ToString("0.00") + " hips-seat " + (hips.position.y - seat.y).ToString("0.00") +
           " feet-seat " + (lFoot ? lFoot.position.y - seat.y : 0f).ToString("0.00") + " footOut " + (lFoot ? Vector3.Dot(lFoot.position - seat, rideRig.transform.right) : 0f).ToString("0.00") +
           "/" + (rFoot ? Vector3.Dot(rFoot.position - seat, rideRig.transform.right) : 0f).ToString("0.00") + " barrel " + rideRig.BarrelHalf.ToString("0.00") +
           " hands-seat " + (lHand ? lHand.position.y - seat.y : 0f).ToString("0.00") + " fwd " + (lHand ? Vector3.Dot(lHand.position - seat, rideRig.transform.forward) : 0f).ToString("0.00");
   }
   return "swimW " + swimWeight.ToString("0.00") + " sp " + swimSpeed.ToString("0.00") + " target " + swimSpeedTarget.ToString("0.00") + ride +
          " tilt " + tilt.ToString("0") + " legTilt " + legs.ToString("0") + " hipsY " + hips.position.y.ToString("0.00") +
          " neckY " + top.y.ToString("0.00") + " headY " + (head ? head.position.y : 0f).ToString("0.00") +
          " handY " + (lHand ? lHand.position.y : 0f).ToString("0.00") + "/" + (rHand ? rHand.position.y : 0f).ToString("0.00");
  }

  public string ResolveState(string state, string style) {
   if (string.IsNullOrEmpty(state)) return "idle";
   if (roleName == "Aster" && !string.IsNullOrEmpty(style) && style != "slash") {
    if (state.StartsWith("attack_")) {
     string styled = style + state.Substring("attack".Length); // "_1".."_4"
     if (slots.ContainsKey(styled)) return styled;
    }
    if (state == "charged" && slots.ContainsKey("charged_" + style)) return "charged_" + style;
   }
   if (slots.ContainsKey(state)) return state;
   if (state == "sprint" && slots.ContainsKey("run")) return "run";
   if (state == "run" && slots.ContainsKey("walk")) return "walk";
   if (state == "dash" && slots.ContainsKey("roll")) return "roll";
   if (state == "roll" && slots.ContainsKey("dodge")) return "dodge";
   if ((state == "land" || state == "land_hard") && slots.ContainsKey("idle")) return "idle";
   if (state == "glide" && slots.ContainsKey("fall")) return "fall";
   if (state.StartsWith("climb_")) {
    if (state != "climb_idle" && slots.ContainsKey("climb_idle")) return "climb_idle";
    if (slots.ContainsKey("fall")) return "fall";
   }
   if (state == "knockdown_loop" && slots.ContainsKey("death")) return "death";
   if (state == "hit" && slots.ContainsKey("gethit")) return "gethit";
   if (state == "gethit" && slots.ContainsKey("hit")) return "hit";
   if (state.StartsWith("cast_") && slots.ContainsKey("cast")) return "cast";
   if (state == "burst" && slots.ContainsKey("cast")) return "cast";
   if (state.StartsWith("attack_")) {
    if (state == "attack_4" && slots.ContainsKey("attack_3")) return "attack_3";
    if (slots.ContainsKey("attack_1")) return "attack_1";
    if (slots.ContainsKey("attack")) return "attack";
   }
   if ((state == "slam" || state == "sweep" || state == "stomp") && slots.ContainsKey("attack_2")) return "attack_2";
   if ((state == "slam" || state == "sweep" || state == "stomp" || state == "attack") && slots.ContainsKey("attack")) return "attack";
   return "idle";
  }

  static bool IsLoco(string s) => s == "idle" || s == "walk" || s == "run" || s == "sprint";

  static float FadeIn(string s) {
   if (s.StartsWith("hit") || s == "gethit") return 0.05f;
   if (s == "dash" || s == "land" || s == "land_hard") return 0.05f;
   if (s.StartsWith("attack") || s.StartsWith("chop") || s.StartsWith("spear") || s.StartsWith("unarmed")) return 0.08f;
   if (s.StartsWith("charged") || s.StartsWith("cast") || s == "burst") return 0.1f;
   if (s == "jump" || s == "double_jump" || s == "roll" || s == "dodge") return 0.07f;
   if (s == "fall") return 0.25f;
   if (s == "glide") return 0.3f;
   if (s == "death") return 0.12f;
   return 0.16f;
  }

  static float LocoFadeFrom(string s) {
   if (s == null) return 0.2f;
   if (s == "land") return 0.18f;
   if (s == "land_hard") return 0.28f;
   if (s == "dash") return 0.16f;
   if (s == "fall" || s == "jump" || s == "double_jump" || s == "glide") return 0.12f;
   if (s.StartsWith("hit")) return 0.2f;
   return 0.24f;
  }

  public void Play(string state, bool restart = false, string style = "slash", float speed = 1f) => PlayAt(state, style, speed, 0f, -1f, restart);

  // startTime skips into the clip (seconds at rate 1); fade < 0 uses the default for the state.
  public void PlayAt(string state, string style, float speed, float startTime, float fade, bool restart = true) {
   if (!graphValid) {
    if (importedMotion && (restart || CurrentState != state)) importedMotion.RestartVersion++;
    CurrentState = state;
    return;
   }
   string resolved = ResolveState(state, style);
   if (IsLoco(resolved)) {
    if (activeSlot != null || restart) EnterLocomotion(fade >= 0f ? fade : LocoFadeFrom(activeSlot != null ? activeSlot.state : CurrentState));
    CurrentState = resolved;
    return;
   }
   if (!slots.TryGetValue(resolved, out var slot) || slot.portA == 0) return;
   bool same = activeSlot == slot;
   if (same && !restart) {
    slot.speed = speed;
    (slot.useB ? slot.b : slot.a).SetSpeed(speed);
    return;
   }
   if (overlaySlot == slot) overlaySlot = null;
   // A restart, or a state that is still visible, starts on the other port and crossfades from it.
   int livePort = slot.useB ? slot.portB : slot.portA;
   if (same || weight[livePort] > 0.02f) slot.useB = !slot.useB;
   var cp = slot.useB ? slot.b : slot.a;
   float t0 = Mathf.Clamp(startTime, 0f, slot.length * 0.98f);
   cp.SetTime(t0);
   cp.SetTime(t0); // twice: also resets the previous time, so the jump is not treated as a delta
   cp.SetDone(false);
   cp.SetSpeed(speed);
   slot.speed = speed;
   int port = slot.useB ? slot.portB : slot.portA;
   float f = Mathf.Max(0.01f, fade >= 0f ? fade : FadeIn(resolved));
   for (int i = 0; i < target.Length; i++) {
    target[i] = i == port ? 1f : 0f;
    rate[i] = 1f / f;
   }
   activeSlot = slot;
   CurrentState = resolved;
  }

  // Plays a one-shot on top of locomotion at partial weight (a landing dip while running). It fades
  // back out by itself.
  public void PlayOverlay(string state, float amount, float speed = 1f, float duration = -1f) {
   if (!graphValid || activeSlot != null) return;
   string resolved = ResolveState(state, "slash");
   if (!slots.TryGetValue(resolved, out var slot) || slot.portA == 0) return;
   int livePort = slot.useB ? slot.portB : slot.portA;
   if (weight[livePort] > 0.02f) slot.useB = !slot.useB;
   var cp = slot.useB ? slot.b : slot.a;
   cp.SetTime(0); cp.SetTime(0); cp.SetDone(false); cp.SetSpeed(speed);
   slot.speed = speed;
   int port = slot.useB ? slot.portB : slot.portA;
   float w = Mathf.Clamp(amount, 0f, 0.9f);
   target[port] = w / (1f - w); // relative to the locomotion port's 1, before normalisation
   rate[port] = 1f / 0.05f;
   overlaySlot = slot;
   overlayUntil = Time.time + (duration > 0f ? duration : slot.length / Mathf.Max(0.1f, speed)) * 0.75f;
  }

  void EnterLocomotion(float fade) {
   float f = Mathf.Max(0.01f, fade);
   for (int i = 0; i < target.Length; i++) { target[i] = i == 0 ? 1f : 0f; rate[i] = 1f / f; }
   activeSlot = null;
  }

  // The hero reports its real horizontal speed and whether it is sprinting (the sprint cycle is only
  // used then; a fast run stays a run). Other characters are measured from their movement.
  public void SetLocomotion(float speed, bool sprinting = true) {
   externalSpeed = true;
   externalSpeedValue = Mathf.Max(0f, speed);
   externalSpeedTime = Time.time;
   sprintGait = sprinting;
  }

  public void TriggerFlinch(Vector3 attackerPos, float intensity = 1f) {
   if (!transform.parent) return;
   Vector3 away = (transform.parent.position - attackerPos);
   away.y = 0f;
   if (away.sqrMagnitude < 0.001f) away = -transform.parent.forward;
   else away.Normalize();
   Vector3 localAway = transform.parent.InverseTransformDirection(away);
   float deg = Mathf.Clamp(11f * intensity, 4f, 20f);
   flinchPitch = Mathf.Clamp(flinchPitch + localAway.z * deg, -22f, 22f);
   flinchRoll = Mathf.Clamp(flinchRoll - localAway.x * deg, -22f, 22f);
  }

  public void TriggerHitStop(float duration = 0.045f) {
   hitStopUntil = Mathf.Max(hitStopUntil, Time.time + duration);
  }

  // Extra forward lean for dashes and sprint starts (degrees, decays by itself).
  public void AddLean(float degrees) { extraLean = Mathf.Max(extraLean, degrees); }

  // Physics takes over the body: the animation stops (or keeps playing on a rigid tumble) and the driver
  // stops writing the model's position and tilt.
  public void EnterRagdoll(bool keepAnimating = false) {
   Ragdolled = true;
   ragdollAnimating = keepAnimating;
   heroRoot = null;
   transform.localRotation = Quaternion.identity;
   transform.localPosition = Vector3.zero;
   if (keepAnimating) return;
   if (graph.IsValid()) graph.Stop();
   if (Animator) Animator.enabled = false;
   if (importedMotion) importedMotion.enabled = false;
   var legacy = ModelInstance ? ModelInstance.GetComponentInChildren<Animation>() : null;
   if (legacy) legacy.enabled = false;
  }

  // Wall climbing: the body aligns with the rock face and the limbs reach for it.
  public void SetClimb(bool on, Vector3 wallNormal) {
   climbTarget = on ? 1f : 0f;
   if (on && wallNormal.sqrMagnitude > 0.01f) climbNormal = wallNormal.normalized;
  }

  // Swimming: pace 0 treads water upright, 1 is a full, nearly flat breaststroke, up to 2 strokes faster
  // (the fast swim). surfaceY, when known, floats the body at the waterline whatever the capsule does.
  public void SetSwim(bool on, float pace, float surfaceY = float.NaN) {
   swimTarget = on ? 1f : 0f;
   swimSpeedTarget = Mathf.Clamp(pace, 0f, 2f);
   if (!float.IsNaN(surfaceY)) swimSurfaceY = surfaceY; // kept through the fade-out, cleared at zero weight
  }
  public bool IsSwimPose => swimWeight > 0.5f;

  void OnEnable() {
   if (ModelInstance && Animator && !importedMotion && (!graphValid || !graph.IsValid())) RebuildAfterReload();
  }

  void Update() {
   if (Ragdolled && !ragdollAnimating) return;
   // A script reload in Play mode keeps serialized fields but invalidates every playable handle.
   if (!setupDone) { setupDone = true; if (!graphValid && ModelInstance && Animator && !importedMotion && !Ragdolled) RebuildAfterReload(); }
   if (graphValid && (!graph.IsValid() || !top.IsValid())) { graphValid = false; RebuildAfterReload(); }
   if (!graphValid) return;
   float dt = Time.deltaTime;
   if (dt <= 0.0001f) return;
   bool stopped = Time.time < hitStopUntil;

   // Speed: reported by the hero, measured for everyone else.
   if (transform.parent) {
    Vector3 position = transform.parent.position;
    float measured = locomotionInitialized ? Vector3.ProjectOnPlane(position - lastLocomotionPosition, Vector3.up).magnitude / dt : 0;
    lastLocomotionPosition = position; locomotionInitialized = true;
    float s = externalSpeed && Time.time - externalSpeedTime < 0.25f ? externalSpeedValue : Mathf.Clamp(measured, 0, 12);
    locoSpeed = Mathf.Lerp(locoSpeed, s, 1 - Mathf.Exp(-14 * dt));
   }
   UpdateLocomotion(stopped ? 0f : dt);

   // Hit-stop freezes the active clip for a few frames of impact weight.
   if (activeSlot != null) {
    var cp = activeSlot.useB ? activeSlot.b : activeSlot.a;
    cp.SetSpeed(stopped ? 0.04f : activeSlot.speed);
   }
   if (overlaySlot != null && Time.time > overlayUntil) {
    target[overlaySlot.portA] = 0f; target[overlaySlot.portB] = 0f;
    rate[overlaySlot.portA] = rate[overlaySlot.portB] = 1f / 0.2f;
    overlaySlot = null;
   }

   // Eased weights, normalised; the body height follows the same weights.
   float sum = 0f;
   for (int i = 0; i < weight.Length; i++) {
    weight[i] = Mathf.MoveTowards(weight[i], target[i], rate[i] * dt);
    sum += Ease(weight[i]);
   }
   float height = 0f;
   if (sum > 0.0001f) {
    for (int i = 0; i < weight.Length; i++) {
     float w = Ease(weight[i]) / sum;
     if (Mathf.Abs(w - applied[i]) > 0.00005f) { top.SetInputWeight(i, w); applied[i] = w; }
     if (w <= 0.0001f) continue;
     if (i == 0) height += w * LocoHeight();
     else {
      var slot = portSlot[i];
      if (slot != null && slot.meta != null) {
       var cp = i == slot.portA ? slot.a : slot.b;
       height += w * OpenWorldMotionMeta.Height(slot.meta, (float)cp.GetTime(), slot.loop);
      }
     }
    }
   }
   if (Ragdolled) return;
   if (ModelInstance) ModelInstance.transform.localPosition = modelBaseLocal + Vector3.up * height;

   // Whole-body tilt: a little bank into fast turns (pivot at the feet) and directional flinches.
   climbWeight = Mathf.MoveTowards(climbWeight, climbTarget, dt / 0.18f);
   swimWeight = Mathf.MoveTowards(swimWeight, swimTarget, dt / 0.3f);
   swimSpeed = Mathf.MoveTowards(swimSpeed, swimSpeedTarget * swimTarget, dt * 1.6f);
   if (swimWeight <= 0f) swimSurfaceY = float.NaN;
   if (transform.parent) {
    float yaw = transform.parent.eulerAngles.y;
    float yawDelta = Mathf.DeltaAngle(lastParentYaw, yaw);
    lastParentYaw = yaw;
    turnRate = Mathf.Lerp(turnRate, Mathf.Clamp(yawDelta / dt, -400f, 400f), 1 - Mathf.Exp(-10 * dt));
    bool moving = activeSlot == null && locoSpeed > 2.5f;
    float targetBank = moving ? Mathf.Clamp(-turnRate * locoSpeed * 0.0016f, -6f, 6f) : 0f;
    bankRoll = Mathf.Lerp(bankRoll, targetBank, 1 - Mathf.Exp(-8 * dt));
    flinchPitch = Mathf.MoveTowards(flinchPitch, 0f, 55f * dt);
    flinchRoll = Mathf.MoveTowards(flinchRoll, 0f, 55f * dt);
    Quaternion tilt = CurrentState == "death" ? Quaternion.identity : Quaternion.Euler(flinchPitch, 0f, bankRoll * (1f - climbWeight) * (1f - swimWeight) + flinchRoll);
    Vector3 offset = Vector3.zero;
    if (climbWeight > 0.001f) {
     // Lean into slanted rock: the body's up follows the surface, pivoting at the chest so the hands
     // stay on the wall.
     Vector3 surfaceUp = Vector3.ProjectOnPlane(Vector3.up, climbNormal);
     if (surfaceUp.sqrMagnitude > 0.0001f) {
      Quaternion world = Quaternion.FromToRotation(Vector3.up, surfaceUp.normalized);
      Quaternion local = Quaternion.Inverse(transform.parent.rotation) * world * transform.parent.rotation;
      local = Quaternion.Slerp(Quaternion.identity, local, climbWeight);
      Vector3 pivot = Vector3.up * 1.15f;
      offset = pivot - local * pivot;
      tilt = local * tilt;
     }
    }
    transform.localRotation = tilt;
    transform.localPosition = offset;
   }
  }

  // Smoothstep on 0..1; overlay ports may carry relative weights above 1, which pass through unchanged.
  static float Ease(float w) => w >= 1f ? w : w <= 0f ? 0f : w * w * (3f - 2f * w);

  // Weights for one gait set: neighbouring gaits blend by speed.
  void GaitWeights(float v, bool allowSprint, float[] w) {
   for (int i = 0; i < 4; i++) w[i] = 0f;
   int prev = 0;
   bool placed = false;
   if (v < 0.12f) { w[0] = 1f; placed = true; }
   for (int i = 1; i < 4 && !placed; i++) {
    if (locoSlots[i] == null || i == 3 && !allowSprint) continue;
    if (v <= locoStride[i]) {
     float lo = prev == 0 ? 0f : locoStride[prev];
     float t = Mathf.InverseLerp(lo, locoStride[i], v);
     if (prev == 0) t = Mathf.Clamp01(t * 2.2f); // a full walk quickly; idle only near standstill
     w[prev] = 1f - t;
     w[i] = t;
     placed = true;
    }
    prev = i;
   }
   if (!placed) w[prev] = 1f;
   if (locoSlots[0] == null && w[0] > 0f) {
    int fallback = 1; while (fallback < 4 && locoSlots[fallback] == null) fallback++;
    if (fallback < 4) w[fallback] += w[0];
    w[0] = 0f;
   }
  }

  void UpdateLocomotion(float dt) {
   // Blend between neighbouring gaits by speed; each plays at its stride-matched rate. The sprint cycle
   // fades in only while the hero sprints (other characters may use it whenever they are fast).
   float v = locoSpeed;
   bool allowSprint = !externalSpeed || sprintGait;
   sprintBlend = Mathf.MoveTowards(sprintBlend, allowSprint ? 1f : 0f, dt * 4f);
   GaitWeights(v, false, gaitA);
   GaitWeights(v, true, gaitB);
   for (int i = 0; i < 4; i++) locoWeight[i] = Mathf.Lerp(gaitA[i], gaitB[i], sprintBlend);

   float cycles = 0f, movingWeight = 0f;
   for (int i = 1; i < 4; i++) {
    if (locoSlots[i] == null || locoWeight[i] <= 0f) { locoRate[i] = 1f; continue; }
    locoRate[i] = Mathf.Clamp(v / Mathf.Max(0.1f, locoStride[i]), 0.55f, 1.6f);
    cycles += locoWeight[i] * locoRate[i] / locoSlots[i].length;
    movingWeight += locoWeight[i];
   }
   float previousPhase = locoPhase;
   if (movingWeight > 0.001f) locoPhase = Mathf.Repeat(locoPhase + dt * cycles / movingWeight, 1f);
   if (dt > 0f && movingWeight > 0.45f && activeSlot == null && FootPlanted != null) {
    if (locoPhase < previousPhase) FootPlanted(true);                                  // left foot down
    else if (previousPhase < 0.5f && locoPhase >= 0.5f) FootPlanted(false);             // right foot down
   }
   idleTime += dt;
   for (int i = 0; i < 4; i++) {
    var s = locoSlots[i];
    if (s == null) continue;
    loco.SetInputWeight(i, locoWeight[i]);
    if (i == 0) s.a.SetTime(Mathf.Repeat(idleTime, s.length));
    else {
     float plant = s.meta != null && s.meta.plant >= 0f ? s.meta.plant : 0f;
     s.a.SetTime(Mathf.Repeat(locoPhase + plant, 1f) * s.length);
    }
   }
  }

  float LocoHeight() {
   float h = 0f;
   for (int i = 0; i < 4; i++) {
    var s = locoSlots[i];
    if (s == null || s.meta == null || locoWeight[i] <= 0f) continue;
    h += locoWeight[i] * OpenWorldMotionMeta.Height(s.meta, (float)s.a.GetTime(), true);
   }
   return h;
  }

  // ---------------------------------------------------------------- hero rig

  public void EnableHeroRig(Transform root) {
   heroRoot = root;
   heroBody = root ? root.GetComponent<OpenWorldHero>() : null;
   if (!Animator || !Animator.isHuman) return;
   Transform B(HumanBodyBones b) => Animator.GetBoneTransform(b);
   hips = B(HumanBodyBones.Hips); spine = B(HumanBodyBones.Spine); chest = B(HumanBodyBones.Chest) ?? spine;
   neck = B(HumanBodyBones.Neck); head = B(HumanBodyBones.Head);
   lThigh = B(HumanBodyBones.LeftUpperLeg); lKnee = B(HumanBodyBones.LeftLowerLeg); lFoot = B(HumanBodyBones.LeftFoot);
   rThigh = B(HumanBodyBones.RightUpperLeg); rKnee = B(HumanBodyBones.RightLowerLeg); rFoot = B(HumanBodyBones.RightFoot);
   lArm = B(HumanBodyBones.LeftUpperArm); lElbow = B(HumanBodyBones.LeftLowerArm); lHand = B(HumanBodyBones.LeftHand);
   rArm = B(HumanBodyBones.RightUpperArm); rElbow = B(HumanBodyBones.RightLowerArm); rHand = B(HumanBodyBones.RightHand);
   lToe = B(HumanBodyBones.LeftToes); rToe = B(HumanBodyBones.RightToes);
  }

  // Riding: the mount reports the saddle, the blend (0 on the ground, 1 seated) and its gait every frame.
  public void SetRide(OpenWorldMountRig rig, float weight, float speed, float gallop01, float turnRate) {
   rideRig = rig; rideWeight = Mathf.Clamp01(weight); rideSpeed = speed; rideTurn = turnRate;
   rideGallop = Mathf.Lerp(rideGallop, Mathf.Clamp01(gallop01), 1f - Mathf.Exp(-4f * Time.deltaTime));
   rideStamp = Time.time;
  }
  public void EndRide() { rideWeight = 0f; rideRig = null; if (reins) reins.Hide(); }
  // Height of the hips above the character's root in the current (standing) pose.
  public float HipsAboveRoot() => hips && heroRoot ? Mathf.Max(0.5f, hips.position.y - heroRoot.position.y) : 0.95f;

  // Foot IK amount the hero wants this frame (grounded, not mid-air).
  public void SetFootIk(float amount) { footIkTarget = Mathf.Clamp01(amount); }
  public void SetLookTarget(Transform target) { lookTarget = target; }

  // Paraglider: the body hangs from the bar and both hands hold its grips.
  public void SetGlide(bool on, Transform leftGrip, Transform rightGrip) {
   glideTarget = on ? 1f : 0f;
   if (leftGrip) gripLeft = leftGrip;
   if (rightGrip) gripRight = rightGrip;
  }

  void LateUpdate() {
   if (Ragdolled || !graphValid || !heroRoot || !hips) return;
   float dt = Time.deltaTime;
   if (dt <= 0.0001f) return;
   bool dead = CurrentState == "death";
   glideWeight = Mathf.MoveTowards(glideWeight, glideTarget, dt / 0.25f);

   // Lean: into turns at speed, forward when accelerating, sprinting or dashing.
   float speed = locoSpeed;
   float accel = (speed - lastSpeed) / dt;
   lastSpeed = speed;
   accelSmoothed = Mathf.Lerp(accelSmoothed, Mathf.Clamp(accel, -20f, 20f), 1 - Mathf.Exp(-6 * dt));
   extraLean = Mathf.MoveTowards(extraLean, 0f, 40f * dt);
   bool locomotion = activeSlot == null;
   float rollTarget = locomotion ? Mathf.Clamp(-turnRate * speed * 0.0035f, -11f, 11f) : 0f;
   float pitchTarget = (locomotion ? Mathf.Clamp(Mathf.Max(0f, speed - 3f) * 1.2f + accelSmoothed * 0.35f, -3f, 8f) : 0f) + extraLean;
   if (glideWeight > 0.01f) { rollTarget = Mathf.Lerp(rollTarget, Mathf.Clamp(-turnRate * 0.06f, -14f, 14f), glideWeight); pitchTarget *= 1f - glideWeight; }
   leanRoll = Mathf.Lerp(leanRoll, dead ? 0f : rollTarget, 1 - Mathf.Exp(-9 * dt));
   leanPitch = Mathf.Lerp(leanPitch, dead ? 0f : pitchTarget, 1 - Mathf.Exp(-9 * dt));
   Vector3 fwd = heroRoot.forward, right = heroRoot.right;
   if (Mathf.Abs(leanRoll) > 0.05f || Mathf.Abs(leanPitch) > 0.05f) {
    Quaternion lean = Quaternion.AngleAxis(leanRoll, fwd) * Quaternion.AngleAxis(leanPitch, right);
    if (spine && spine != chest) spine.rotation = Quaternion.Slerp(Quaternion.identity, lean, 0.4f) * spine.rotation;
    if (chest) chest.rotation = Quaternion.Slerp(Quaternion.identity, lean, spine != chest ? 0.6f : 1f) * chest.rotation;
   }

   if (glideWeight > 0.01f) GlideHang();
   if (Time.time - rideStamp > 0.25f && rideWeight > 0f) rideWeight = Mathf.MoveTowards(rideWeight, 0f, dt * 4f); // the mount vanished
   if (swimWeight > 0.01f) SwimPose(dt);
   else if (climbWeight > 0.01f) ClimbIk();
   else if (rideWeight > 0.001f && rideRig) RidePose(dt);
   if (reins && (rideWeight < 0.35f || !rideRig)) reins.Hide();
   // No ground IK in the saddle: the seated pose (OpenWorldAnimalInteraction) owns the legs there.
   bool mounted = heroBody && heroBody.IsMounted;
   FootIk(dt, dead || mounted || climbWeight > 0.3f || swimWeight > 0.2f);
   if (swimWeight < 0.5f && climbWeight < 0.5f) HeadLook(dt, dead);
  }

  // ---------------------------------------------------------------- climbing

  void ClimbIk() {
   Vector3 into = -climbNormal;
   float w = climbWeight * 0.9f;
   if (lArm && lElbow && lHand) ReachWall(lArm, lElbow, lHand, into, 0.035f, w);
   if (rArm && rElbow && rHand) ReachWall(rArm, rElbow, rHand, into, 0.035f, w);
   if (lThigh && lKnee && lFoot) ReachWall(lThigh, lKnee, lFoot, into, 0.08f, w * 0.8f);
   if (rThigh && rKnee && rFoot) ReachWall(rThigh, rKnee, rFoot, into, 0.08f, w * 0.8f);
  }

  // Moves a hand or foot onto the rock behind (or in front of) its animated position.
  void ReachWall(Transform a, Transform b, Transform c, Vector3 into, float standoff, float weight) {
   Vector3 p = c.position;
   int n = Physics.RaycastNonAlloc(p - into * 0.45f, into, footHits, 0.95f, ~0, QueryTriggerInteraction.Ignore);
   float best = float.MaxValue; Vector3 point = Vector3.zero; bool found = false;
   for (int i = 0; i < n; i++) {
    var h = footHits[i];
    if (!h.collider || h.collider.transform.IsChildOf(heroRoot) || h.collider is CharacterController || (h.rigidbody && !h.rigidbody.isKinematic)) continue;
    if (h.distance < best) { best = h.distance; point = h.point; found = true; }
   }
   if (!found) return;
   SolveTwoBone(a, b, c, point - into * standoff, -into, weight);
  }

  // ---------------------------------------------------------------- riding

  void RidePose(float dt) {
   if (!lThigh || !rThigh || !lKnee || !rKnee || !lFoot || !rFoot || !lArm || !rArm || !lElbow || !rElbow || !lHand || !rHand) return;
   float w = Ease(rideWeight);
   Vector3 seat = rideRig.Seat;
   Vector3 steady = rideRig.SeatSteady;
   Vector3 hf = Vector3.ProjectOnPlane(rideRig.transform.forward, Vector3.up);
   if (hf.sqrMagnitude < 1e-4f) hf = heroRoot.forward;
   Quaternion yawFrame = Quaternion.LookRotation(hf.normalized, Vector3.up);
   // The rider balances on the back: half of its pitch and roll reach her pelvis.
   Quaternion frame = Quaternion.Slerp(yawFrame, rideRig.SeatRotation, 0.5f);
   Vector3 F = frame * Vector3.forward, U = frame * Vector3.up, R = frame * Vector3.right;
   // Gait bounce: sitting to the walk and canter, rising out of the saddle at the gallop (the legs take
   // part of the bounce, the hips float).
   float bounce = Vector3.Dot(seat - steady, Vector3.up);
   rideBounce = Mathf.Lerp(rideBounce, bounce, 1f - Mathf.Exp(-6f * dt));
   float follow = Mathf.Lerp(0.95f, 0.5f, rideGallop);
   Vector3 saddle = seat - Vector3.up * ((bounce - rideBounce) * (1f - follow));
   Vector3 hipsTarget = saddle + U * (0.1f + 0.05f * rideGallop) - F * 0.04f;
   hips.position = Vector3.Lerp(hips.position, hipsTarget, w);
   // Upper body: upright at a walk, a forward seat at the gallop, leaning into turns.
   rideLean = Mathf.Lerp(rideLean, Mathf.Lerp(5f, 24f, rideGallop) + Mathf.Clamp(rideSpeed - 2f, 0f, 4f) * 1.2f, 1f - Mathf.Exp(-4f * dt));
   rideRoll = Mathf.Lerp(rideRoll, Mathf.Clamp(-rideTurn * rideSpeed * 0.006f, -14f, 14f), 1f - Mathf.Exp(-5f * dt));
   Vector3 spineUp = Quaternion.AngleAxis(rideRoll, F) * (Quaternion.AngleAxis(rideLean, R) * U);
   Vector3 top = neck ? neck.position : head ? head.position : hips.position + U;
   Vector3 spineNow = top - hips.position;
   if (spineNow.sqrMagnitude > 1e-4f) hips.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(spineNow.normalized, spineUp), w) * hips.rotation;
   // The torso lags the saddle's bounce a little (a soft spine rather than a rigid post).
   rideTorso = Mathf.SmoothDamp(rideTorso, bounce, ref rideTorsoVel, 0.12f, 10f, dt);
   float flex = Mathf.Clamp((bounce - rideTorso) * 70f, -8f, 8f) * w;
   if (chest) chest.rotation = Quaternion.AngleAxis(flex, R) * chest.rotation;
   // Legs round the barrel, feet in the stirrups: outside the flank below the seat, a little forward;
   // knees forward and out.
   float barrel = rideRig.BarrelHalf;
   float belly = Mathf.Max(0.25f, rideRig.Belly);
   for (int side = -1; side <= 1; side += 2) {
    Transform a = side < 0 ? lThigh : rThigh, b = side < 0 ? lKnee : rKnee, c = side < 0 ? lFoot : rFoot, toe = side < 0 ? lToe : rToe;
    float leg = (b.position - a.position).magnitude + (c.position - b.position).magnitude;
    float drop = Mathf.Min(leg * 0.72f, belly + 0.04f);
    Vector3 stirrup = saddle + R * (side * (barrel + 0.07f)) - U * drop + F * (0.05f + 0.07f * rideGallop);
    Vector3 kneePole = a.position + F * 0.6f + R * (side * (barrel + 0.35f)) - U * 0.15f;
    SolveTwoBonePole(a, b, c, stirrup, kneePole, w);
    if (toe) {
     // Heels down, toes forward and a little out.
     Vector3 now = toe.position - c.position;
     Vector3 want = (F * 0.97f + R * (side * 0.21f) + U * 0.12f).normalized * now.magnitude;
     if (now.sqrMagnitude > 1e-6f) c.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), w) * c.rotation;
    }
   }
   // Hands forward over the withers on the reins, elbows bent down and in.
   Vector3 grip = saddle + F * (0.36f + 0.1f * rideGallop) + U * (0.25f - 0.07f * rideGallop);
   for (int side = -1; side <= 1; side += 2) {
    Transform a = side < 0 ? lArm : rArm, b = side < 0 ? lElbow : rElbow, c = side < 0 ? lHand : rHand;
    Vector3 target = grip + R * (side * 0.085f);
    Vector3 pole = a.position + R * (side * 0.3f) - U * 0.42f - F * 0.18f;
    SolveTwoBonePole(a, b, c, target, pole, w);
   }
   if (w > 0.35f) {
    if (!reins) reins = OpenWorldReins.Ensure(heroRoot);
    reins.Draw(lHand.position, rideRig.Bit, rHand.position, Mathf.Lerp(0.07f, 0.03f, rideGallop));
   }
  }

  // ---------------------------------------------------------------- swimming

  // Breaststroke path of the left hand relative to its shoulder in a level frame (forward, outward, up),
  // at the cycle phases in StrokeTimes: glide with the arms out front, sweep out, pull in under the chest,
  // hands together under the chin, shoot forward again. The right hand mirrors it. Hands stay under the
  // surface (clamped in SwimPose).
  static readonly float[] StrokeTimes = { 0f, 0.22f, 0.38f, 0.5f, 0.66f, 1f };
  static readonly Vector3[] StrokeHand = {
   new Vector3(0.6f, 0.08f, -0.08f), new Vector3(0.46f, 0.4f, -0.1f), new Vector3(0.16f, 0.26f, -0.3f),
   new Vector3(0.22f, 0.07f, -0.2f), new Vector3(0.6f, 0.08f, -0.08f), new Vector3(0.6f, 0.08f, -0.08f)
  };
  // Frog kick for the left foot relative to its hip: (down the legs, outward, toward the back).
  static readonly float[] KickTimes = { 0f, 0.4f, 0.56f, 0.7f, 0.84f, 1f };
  static readonly Vector3[] KickFoot = {
   new Vector3(0.86f, 0.08f, 0f), new Vector3(0.86f, 0.08f, 0f), new Vector3(0.44f, 0.27f, 0.2f),
   new Vector3(0.72f, 0.42f, 0.06f), new Vector3(0.88f, 0.08f, 0f), new Vector3(0.86f, 0.08f, 0f)
  };

  // The body rises a little through the pull (a breath) and settles through the glide.
  static readonly float[] BreathTimes = { 0f, 0.22f, 0.42f, 0.62f, 1f };
  static readonly float[] BreathLift = { 0f, 0.025f, 0.07f, 0.02f, 0f };

  static Vector3 Keyed(float[] times, Vector3[] keys, float t) {
   for (int i = 0; i < times.Length - 1; i++) {
    if (t > times[i + 1]) continue;
    float u = Mathf.InverseLerp(times[i], times[i + 1], t);
    return Vector3.Lerp(keys[i], keys[i + 1], u * u * (3f - 2f * u));
   }
   return keys[keys.Length - 1];
  }

  static float Keyed(float[] times, float[] keys, float t) {
   for (int i = 0; i < times.Length - 1; i++) {
    if (t > times[i + 1]) continue;
    float u = Mathf.InverseLerp(times[i], times[i + 1], t);
    return Mathf.Lerp(keys[i], keys[i + 1], u * u * (3f - 2f * u));
   }
   return keys[keys.Length - 1];
  }

  void SwimPose(float dt) {
   if (!lArm || !rArm || !lElbow || !rElbow || !lHand || !rHand || !lThigh || !rThigh || !lKnee || !rKnee || !lFoot || !rFoot) return;
   float w = swimWeight;
   float sp = Mathf.Min(1f, swimSpeed), fast = Mathf.Clamp01(swimSpeed - 1f);
   float previous = swimPhase;
   swimPhase = Mathf.Repeat(swimPhase + dt * (Mathf.Lerp(0.6f, 0.95f, sp) + 0.5f * fast), 1f);
   if (sp > 0.25f && previous < 0.04f && swimPhase >= 0.04f) SwimStroke?.Invoke();
   Vector3 R = heroRoot.right, U = Vector3.up, F = heroRoot.forward;
   // Whole body about the hips, set to an exact angle from upright whatever the idle pose underneath:
   // nearly flat while stroking (the head stays up), upright while treading, surging with each kick.
   float surge = Mathf.Sin((swimPhase - 0.62f) * Mathf.PI * 2f) * 4f * sp;
   float lean = Mathf.Lerp(10f, 64f, sp) + 6f * fast + surge;
   Quaternion body = Quaternion.AngleAxis(lean, R);
   Vector3 H = body * U;   // toward the head
   Vector3 B = body * F;   // the belly side
   Vector3 top = neck ? neck.position : head ? head.position : hips.position + U;
   Vector3 spine = top - hips.position;
   if (spine.sqrMagnitude > 1e-4f) {
    var turn = Quaternion.FromToRotation(spine.normalized, H);
    hips.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * hips.rotation;
   }
   // Float at the waterline: hips just under it while stroking (the back awash, shoulders and head out),
   // lower while treading so only the shoulders and head show. The pull lifts her a little to breathe and
   // the glide settles her.
   float pull = Keyed(BreathTimes, BreathLift, swimPhase) * sp;
   swimLift = Mathf.Lerp(swimLift, pull, 1f - Mathf.Exp(-10f * dt));
   if (!float.IsNaN(swimSurfaceY)) {
    float wantHips = swimSurfaceY - Mathf.Lerp(0.33f, 0.08f, sp) + 0.02f * fast + swimLift + Mathf.Sin(swimPhase * Mathf.PI * 4f) * 0.025f * (1f - sp);
    hips.position += U * ((wantHips - hips.position.y) * w);
   } else hips.position += U * (swimLift * w);
   // Head up out of the water, looking ahead.
   float pitch = lean * w;
   if (neck) neck.rotation = Quaternion.AngleAxis(-pitch * 0.38f, R) * neck.rotation;
   if (head) head.rotation = Quaternion.AngleAxis(-pitch * 0.47f, R) * head.rotation;
   float cycle = swimPhase * Mathf.PI * 2f;
   for (int side = -1; side <= 1; side += 2) {
    Transform a = side < 0 ? lArm : rArm, b = side < 0 ? lElbow : rElbow, c = side < 0 ? lHand : rHand;
    Vector3 S = a.position;
    Vector3 k = Keyed(StrokeTimes, StrokeHand, swimPhase);
    Vector3 stroke = S + F * k.x + R * (side * k.y) + U * k.z;
    // Treading: hands scull in small circles out to the sides at chest depth, elbows bent.
    Vector3 tread = S + F * (0.2f + 0.05f * Mathf.Cos(cycle * 2f)) + R * (side * (0.3f + 0.07f * Mathf.Sin(cycle * 2f))) - U * 0.18f;
    Vector3 target = Vector3.Lerp(tread, stroke, sp);
    if (!float.IsNaN(swimSurfaceY)) target.y = Mathf.Min(target.y, swimSurfaceY - 0.04f);
    // Elbows bend out and down in the stroke, out and back while sculling.
    Vector3 pole = S + R * (side * 0.5f) - U * Mathf.Lerp(0.1f, 0.25f, sp) - F * Mathf.Lerp(0.25f, 0.1f, sp);
    SolveTwoBonePole(a, b, c, target, pole, w);
   }
   for (int side = -1; side <= 1; side += 2) {
    Transform a = side < 0 ? lThigh : rThigh, b = side < 0 ? lKnee : rKnee, c = side < 0 ? lFoot : rFoot;
    Vector3 P = a.position;
    Vector3 k = Keyed(KickTimes, KickFoot, swimPhase);
    Vector3 kick = P - H * k.x + R * (side * k.y) - B * k.z;
    // Treading: an eggbeater kick, each foot circling out of phase.
    float th = cycle * 2f + side * 1.57f;
    Vector3 tread = P - U * 0.76f + R * (side * 0.13f) + (F * Mathf.Cos(th) + R * (side * Mathf.Sin(th))) * 0.13f;
    Vector3 target = Vector3.Lerp(tread, kick, sp);
    Vector3 pole = P - H * 0.4f + R * (side * 0.45f) + B * 0.3f;
    SolveTwoBonePole(a, b, c, target, pole, w);
   }
  }

  // Two-bone IK with an explicit pole: the middle joint bends toward the pole point.
  static void SolveTwoBonePole(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight) {
   if (weight <= 0.001f) return;
   Vector3 pa = a.position, pb = b.position, pc = c.position;
   float lab = (pb - pa).magnitude, lbc = (pc - pb).magnitude;
   if (lab < 1e-4f || lbc < 1e-4f) return;
   Vector3 t = Vector3.Lerp(pc, target, weight);
   Vector3 at = t - pa;
   float d = Mathf.Clamp(at.magnitude, Mathf.Abs(lab - lbc) + 0.002f, lab + lbc - 0.002f);
   Vector3 dir = at.sqrMagnitude > 1e-8f ? at.normalized : (pc - pa).normalized;
   Vector3 bend = Vector3.ProjectOnPlane(pole - pa, dir);
   if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(pb - pa, dir);
   if (bend.sqrMagnitude < 1e-6f) return;
   bend.Normalize();
   float cosA = Mathf.Clamp((lab * lab + d * d - lbc * lbc) / (2f * lab * d), -1f, 1f);
   Vector3 elbow = pa + dir * (cosA * lab) + bend * (Mathf.Sqrt(1f - cosA * cosA) * lab);
   Vector3 wantedElbow = Vector3.Lerp(pb, elbow, weight);
   a.rotation = Quaternion.FromToRotation(pb - pa, wantedElbow - pa) * a.rotation;
   Vector3 nb = b.position, nc = c.position;
   b.rotation = Quaternion.FromToRotation(nc - nb, t - nb) * b.rotation;
  }

  void FootIk(float dt, bool dead) {
   float wanted = dead || glideWeight > 0.2f ? 0f : footIkTarget;
   footIkWeight = Mathf.MoveTowards(footIkWeight, wanted, dt / (wanted > footIkWeight ? 0.12f : 0.08f));
   if (!lFoot || !rFoot || !lThigh || !rThigh || !lKnee || !rKnee) return;
   float baseY = heroRoot.position.y;
   float lTarget = 0f, rTarget = 0f;
   Vector3 lNormal = Vector3.up, rNormal = Vector3.up;
   Vector3 lPos = lFoot.position, rPos = rFoot.position;
   if (footIkWeight > 0.001f) {
    lTarget = GroundOffset(lPos, baseY, out lNormal);
    rTarget = GroundOffset(rPos, baseY, out rNormal);
   }
   lFootOffset = Mathf.SmoothDamp(lFootOffset, lTarget, ref lFootVel, 0.06f, 8f, dt);
   rFootOffset = Mathf.SmoothDamp(rFootOffset, rTarget, ref rFootVel, 0.06f, 8f, dt);
   lFootNormal = Vector3.Slerp(lFootNormal, lNormal, 1 - Mathf.Exp(-14 * dt));
   rFootNormal = Vector3.Slerp(rFootNormal, rNormal, 1 - Mathf.Exp(-14 * dt));
   float pelvisTarget = Mathf.Min(0f, Mathf.Min(lFootOffset, rFootOffset));
   pelvisOffset = Mathf.SmoothDamp(pelvisOffset, pelvisTarget * footIkWeight, ref pelvisVel, 0.07f, 6f, dt);
   if (footIkWeight <= 0.001f && Mathf.Abs(pelvisOffset) < 0.001f) return;
   hips.position += Vector3.up * pelvisOffset;
   Vector3 hint = heroRoot.forward;
   SolveTwoBone(lThigh, lKnee, lFoot, lPos + Vector3.up * lFootOffset, hint, footIkWeight);
   SolveTwoBone(rThigh, rKnee, rFoot, rPos + Vector3.up * rFootOffset, hint, footIkWeight);
   AlignFoot(lFoot, lFootNormal, footIkWeight);
   AlignFoot(rFoot, rFootNormal, footIkWeight);
  }

  float GroundOffset(Vector3 foot, float baseY, out Vector3 normal) {
   normal = Vector3.up;
   var origin = new Vector3(foot.x, baseY + 0.55f, foot.z);
   int n = Physics.RaycastNonAlloc(origin, Vector3.down, footHits, 1.15f, ~0, QueryTriggerInteraction.Ignore);
   float best = float.MaxValue; bool found = false;
   for (int i = 0; i < n; i++) {
    var h = footHits[i];
    if (!h.collider || h.collider.transform.IsChildOf(heroRoot) || h.collider is CharacterController || (h.rigidbody && !h.rigidbody.isKinematic)) continue;
    if (h.distance < best) { best = h.distance; normal = h.normal; found = true; }
   }
   if (!found) return 0f;
   return Mathf.Clamp(origin.y - best - baseY, -0.42f, 0.38f);
  }

  static void AlignFoot(Transform foot, Vector3 normal, float weight) {
   if (weight <= 0.001f) return;
   var tilt = Quaternion.FromToRotation(Vector3.up, normal);
   float angle = Quaternion.Angle(Quaternion.identity, tilt);
   if (angle > 28f) tilt = Quaternion.Slerp(Quaternion.identity, tilt, 28f / angle);
   foot.rotation = Quaternion.Slerp(Quaternion.identity, tilt, weight) * foot.rotation;
  }

  // Analytic two-bone IK: bend the middle joint to the needed length, then aim the chain at the target.
  // The bend plane of the animation is kept, so knees and elbows point where the clip pointed them.
  static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float weight) {
   if (weight <= 0.001f) return;
   Vector3 pa = a.position, pb = b.position, pc = c.position;
   Vector3 t = Vector3.Lerp(pc, target, weight);
   float lab = (pb - pa).magnitude, lbc = (pc - pb).magnitude;
   if (lab < 1e-4f || lbc < 1e-4f) return;
   float lat = Mathf.Clamp((t - pa).magnitude, Mathf.Abs(lab - lbc) + 0.002f, lab + lbc - 0.002f);
   float inner0 = Vector3.Angle(pa - pb, pc - pb);
   float inner1 = Mathf.Acos(Mathf.Clamp((lab * lab + lbc * lbc - lat * lat) / (2f * lab * lbc), -1f, 1f)) * Mathf.Rad2Deg;
   Vector3 axis = Vector3.Cross(pb - pa, pc - pb);
   if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(pc - pa, hint);
   if (axis.sqrMagnitude < 1e-8f) return;
   b.rotation = Quaternion.AngleAxis(inner0 - inner1, axis.normalized) * b.rotation;
   Vector3 reach = c.position - pa;
   if (reach.sqrMagnitude > 1e-8f) a.rotation = Quaternion.FromToRotation(reach, t - pa) * a.rotation;
  }

  void HeadLook(float dt, bool dead) {
   if (!head) return;
   float wanted = 0f;
   Vector3 dir = heroRoot.forward;
   var cam = Camera.main;
   bool locomotion = activeSlot == null;
   if (!dead && glideWeight < 0.5f && swimWeight < 0.5f && climbWeight < 0.5f) {
    if (lookTarget) {
     dir = lookTarget.position + Vector3.up * 1.2f - head.position;
     wanted = 0.8f;
    } else if (cam && locomotion) {
     Vector3 cf = cam.transform.forward;
     // Facing the camera: look into the lens (Genshin-style); otherwise glance where the player looks.
     if (Vector3.Dot(Vector3.ProjectOnPlane(cf, Vector3.up).normalized, heroRoot.forward) < -0.35f) dir = cam.transform.position - head.position;
     else dir = cf;
     wanted = locoSpeed < 2.5f ? 0.65f : 0.3f;
    }
   }
   Vector3 local = heroRoot.InverseTransformDirection(dir);
   float yaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Abs(local.z) + 0.001f) * Mathf.Rad2Deg, -60f, 60f);
   float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -22f, 26f);
   lookWeight = Mathf.MoveTowards(lookWeight, wanted, dt * 2.5f);
   lookYaw = Mathf.LerpAngle(lookYaw, yaw, 1 - Mathf.Exp(-6 * dt));
   lookPitch = Mathf.Lerp(lookPitch, pitch, 1 - Mathf.Exp(-6 * dt));
   if (lookWeight <= 0.001f) return;
   Quaternion turn = Quaternion.AngleAxis(lookYaw * lookWeight, Vector3.up) * Quaternion.AngleAxis(lookPitch * lookWeight, heroRoot.right);
   if (neck) neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.4f) * neck.rotation;
   head.rotation = Quaternion.Slerp(Quaternion.identity, turn, neck ? 0.6f : 1f) * head.rotation;
  }

  void GlideHang() {
   if (!gripLeft || !gripRight || !lHand || !rHand || !ModelInstance) return;
   // Move the whole body so the hands meet the bar, then close the last centimetres with arm IK.
   Vector3 hands = (lHand.position + rHand.position) * 0.5f;
   Vector3 grips = (gripLeft.position + gripRight.position) * 0.5f;
   ModelInstance.transform.position += (grips - hands) * glideWeight;
   Vector3 outward = heroRoot.right;
   if (lArm && lElbow) SolveTwoBone(lArm, lElbow, lHand, gripLeft.position, -outward, glideWeight);
   if (rArm && rElbow) SolveTwoBone(rArm, rElbow, rHand, gripRight.position, outward, glideWeight);
   // Dangling legs trail the turn a little.
   float sway = Mathf.Clamp(-turnRate * 0.05f, -12f, 12f) * glideWeight;
   if (lThigh) lThigh.rotation = Quaternion.AngleAxis(sway + Mathf.Sin(Time.time * 2.1f) * 4f * glideWeight, heroRoot.forward) * lThigh.rotation;
   if (rThigh) rThigh.rotation = Quaternion.AngleAxis(sway + Mathf.Sin(Time.time * 2.1f + 1.3f) * 4f * glideWeight, heroRoot.forward) * rThigh.rotation;
  }

  // ---------------------------------------------------------------- afterimages

  public void SpawnAfterimage(Color tint, float life = 0.34f) {
   if (ModelInstance) OpenWorldAfterimage.Spawn(ModelInstance, tint, life);
  }

  void RebuildAfterReload() {
   if (!Animator) Animator = GetComponentInChildren<Animator>(true);
   if (!Animator) return;
   if (!ModelInstance) ModelInstance = Animator.transform.parent && Animator.transform.parent != transform ? Animator.transform.parent.gameObject : Animator.gameObject;
   string state = CurrentState;
   SetupPlayables(roleName);
   if (heroRoot) EnableHeroRig(heroRoot);
   if (graphValid) Play(state, true);
  }

  void DestroyGraph() {
   if (graph.IsValid()) graph.Destroy();
   graphValid = false;
   activeSlot = null; overlaySlot = null;
  }

  void OnDestroy() {
   DestroyGraph();
  }

  static Transform FindDeepChild(Transform root, params string[] names) {
   foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
    foreach (var n in names) {
     if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase) || t.name.EndsWith(n, StringComparison.OrdinalIgnoreCase))
      return t;
    }
   }
   return null;
  }
 }
}

