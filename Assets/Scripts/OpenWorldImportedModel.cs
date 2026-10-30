using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LostRealmsOW {
 // Prepared copies retain the source rig and its own clips; Aster's baked clips are untouched.
 // Plays one clip per state with short crossfades. Walk, trot, canter and run/gallop cycles are played at
 // the rate that matches how fast the body really moves (natural speeds measured at preparation), so paws
 // and hooves do not slide. Animals without an authored death clip fall onto their side with the legs gathered.
 public sealed class OpenWorldImportedModel : MonoBehaviour {
  public AnimationClip Idle, Walk, Attack, Hit, Death, Talk, Run;
  // Extra animal states (prepared by ClaudeAnimalPrep): grazing / pecking, sitting or sleeping, a roar or
  // howl, the flight cycle of birds, a second attack, and distinct equine trot / canter gaits.
  public AnimationClip Graze, Rest, Roar, Fly, Attack2, Trot, Canter;
  public AnimationClip WalkLeft, WalkRight, GallopLeft, GallopRight, RunStop, Buck, Swim, Jump;
  public bool HasBankContactHeights;
  public Vector4 WalkLeftContactY,WalkRightContactY,GallopLeftContactY,GallopRightContactY;
  public Vector4[] GallopContactPhases;
  public float GallopLeftPhaseOffset,GallopRightPhaseOffset;
  // Natural ground speed of the walk, trot, canter and run/gallop cycles at this prefab's own scale (m/s, 0 = not measured).
  public float WalkSpeed, TrotSpeed, CanterSpeed, RunSpeed;
  // Normalised time of the run cycle where the legs are most gathered (the pose a fallen animal lies in).
  public float TuckTime = -1f;
  public string State = "idle";
  public int RestartVersion;
  public OpenWorldCharacterVisual Driver;
  // Set by gameplay before the death state: the direction the body falls (world space).
  [System.NonSerialized] public Vector3 FallDirection;

  const int SIdle = 0, SWalk = 1, SAttack = 2, SHit = 3, SDeath = 4, STalk = 5, SRun = 6, SGraze = 7, SRest = 8, SRoar = 9, SFly = 10, SAttack2 = 11, SPose = 12, STrot = 13, SCanter = 14, SWalkLeft = 15, SWalkRight = 16, SRunLeft = 17, SRunRight = 18, SRunStop = 19, SBuck = 20, SSwim = 21, SJump = 22, Count = 23;
  int turnBank;
  public string PlayingState => current==SWalkLeft?"walk_l":current==SWalkRight?"walk_r":current==SRunLeft?"gallop_l":current==SRunRight?"gallop_r":current==SRunStop?"runstop":current==SBuck?"buck":current==SSwim?"swim":current==SJump?"jump":current==SWalk?"walk":current==STrot?"trot":current==SCanter?"canter":current==SRun?"gallop":"idle";
  PlayableGraph graph;
  AnimationMixerPlayable mixer;
  AnimationClipPlayable[] players;
  AnimationClipPlayable[] gallopLegs;AvatarMask gallopTorsoMask;
  AnimationClip[] clips;
  bool[] loops;
  int current = -1;
  int observedRestart;
  Animation legacy;
  string legacyState;
  Vector3 lastPosition;
  bool positionKnown;
  float measuredSpeed, rate = 1f, maxRate = -1f;
  // Secondary equine motion (damped tail/mane sway + head turn look-at)
  bool secondaryScanned;
  Transform neckBone, headBone, lEarBone, rEarBone;
  Transform[] tailBones;
  float lastYaw, yawRate, tailSway, tailVel, headYaw;
  // Hoof stance ground-lock
  Transform[] legHooves, legAnkles;
  Vector3[] hoofRestLocal;Transform[][] stanceJoints;
  Vector3[] plantPos;
  bool[] isPlanted;
  // Procedural fall (no death clip)
  bool falling;
  float fallTime, fallSide = 1f, fallHalfWidth = 0.3f;
  Vector3 fallBasePos, fallPivot;
  Quaternion fallBaseRot;

  public bool Has(string state) {
   switch (state) {
    case "walk_l": return WalkLeft;case "walk_r": return WalkRight;case "gallop_l": return GallopLeft;case "gallop_r": return GallopRight;
    case "runstop": return RunStop;case "buck": return Buck;case "swim": return Swim;case "jump": return Jump;
    case "graze": return Graze; case "rest": return Rest; case "roar": return Roar; case "fly": return Fly;
    case "trot": return Trot; case "canter": return Canter; case "gallop": return Run;
    case "death": return Death; case "attack": return Attack; case "run": return Run; case "walk": return Walk; case "hit": return Hit;
    default: return Idle;
   }
  }
  // World-scale natural speed of a gait (0 when not measured).
  public float NaturalSpeed(bool run) => (run ? RunSpeed : WalkSpeed) * Mathf.Abs(transform.lossyScale.y);
  public float NaturalSpeed(string gait) {
   float scale = Mathf.Abs(transform.lossyScale.y);
   switch (gait) {
    case "trot": return (TrotSpeed > 0.05f ? TrotSpeed : WalkSpeed * 2.2f) * scale;
    case "canter": return (CanterSpeed > 0.05f ? CanterSpeed : RunSpeed > 0.05f ? RunSpeed * 0.65f : WalkSpeed * 3.4f) * scale;
    case "run": case "gallop": case "sprint": case "gallop_l": case "gallop_r": return RunSpeed * scale;
    default: return WalkSpeed * scale;
   }
  }
  public float MeasuredSpeed => measuredSpeed;

  void Start() {
   ScanSecondaryBones();
   if (Idle && Idle.legacy) { StartLegacy(); return; }
   var animator = GetComponentInChildren<Animator>();
   if (!animator) return;
   animator.applyRootMotion = false;
   animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
   if (!Idle || Idle.legacy) return;
   var walk = Walk ? Walk : Idle;
   var run = Run ? Run : walk;
   var trot = Trot ? Trot : walk;
   var canter = Canter ? Canter : run;
   var attack = Attack ? Attack : Run ? Run : Idle;
   clips = new AnimationClip[Count];
   clips[SIdle] = Idle; clips[SWalk] = walk; clips[SAttack] = attack; clips[SHit] = Hit ? Hit : Idle;
   clips[SDeath] = Death ? Death : Idle; clips[STalk] = Talk ? Talk : Idle; clips[SRun] = run;
   clips[SGraze] = Graze ? Graze : Idle; clips[SRest] = Rest ? Rest : Graze ? Graze : Idle; clips[SRoar] = Roar ? Roar : Idle;
   clips[SFly] = Fly ? Fly : walk; clips[SAttack2] = Attack2 ? Attack2 : attack; clips[SPose] = Run ? Run : Idle;
   clips[STrot] = trot; clips[SCanter] = canter;
   clips[SWalkLeft]=WalkLeft?WalkLeft:walk;clips[SWalkRight]=WalkRight?WalkRight:walk;
   clips[SRunLeft]=GallopLeft?GallopLeft:run;clips[SRunRight]=GallopRight?GallopRight:run;
   clips[SRunStop]=RunStop?RunStop:Idle;clips[SBuck]=Buck?Buck:Idle;clips[SSwim]=Swim?Swim:Idle;clips[SJump]=Jump?Jump:Idle;
   loops = new bool[Count];
   loops[SIdle] = loops[SWalk] = loops[STalk] = loops[SRun] = loops[SGraze] = loops[SRest] = loops[SFly] = loops[STrot] = loops[SCanter] = true;
   loops[SWalkLeft]=loops[SWalkRight]=loops[SRunLeft]=loops[SRunRight]=loops[SSwim]=true;
   if (!Hit) loops[SHit] = true;          // stands in for a reaction with the idle loop
   if (!Roar) loops[SRoar] = true;
   graph = PlayableGraph.Create("Imported asset animation");
   graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
   mixer = AnimationMixerPlayable.Create(graph, Count);
   players = new AnimationClipPlayable[Count];
   gallopLegs=new AnimationClipPlayable[Count];
   if(GallopLeft||GallopRight){
    var bones=animator.GetComponentsInChildren<Transform>(true);gallopTorsoMask=new AvatarMask{transformCount=bones.Length};
    for(int i=0;i<bones.Length;i++){
     var bone=bones[i];string path="";for(var node=bone;node&&node!=animator.transform;node=node.parent)path=node.name+(path.Length>0?"/"+path:"");
     gallopTorsoMask.SetTransformPath(i,path);string name=bone.name;
     gallopTorsoMask.SetTransformActive(i,name.StartsWith("BN_Spine_")||name.StartsWith("BN_Neck_")||name.StartsWith("BN_Head_")||name.Contains("Ear_")||name.StartsWith("BN_Tail_"));
    }
   }
   var output = AnimationPlayableOutput.Create(graph, "Model", animator);
   output.SetSourcePlayable(mixer);
   for (int i = 0; i < Count; i++) {
    if (!clips[i] || clips[i].legacy) continue;
    players[i] = AnimationClipPlayable.Create(graph, clips[i]);
    players[i].SetApplyFootIK(false);
    if((i==SRunLeft||i==SRunRight)&&gallopTorsoMask){
     gallopLegs[i]=AnimationClipPlayable.Create(graph,run);gallopLegs[i].SetApplyFootIK(false);
     var layer=AnimationLayerMixerPlayable.Create(graph,2);graph.Connect(gallopLegs[i],0,layer,0);layer.SetInputWeight(0,1);
     graph.Connect(players[i],0,layer,1);layer.SetInputWeight(1,1);layer.SetLayerMaskFromAvatarMask(1,gallopTorsoMask);graph.Connect(layer,0,mixer,i);
    }else graph.Connect(players[i], 0, mixer, i);
   }
   graph.Play();
  }

  void StartLegacy() {
   legacy = GetComponentInChildren<Animation>();
   if (!legacy) legacy = (transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject).AddComponent<Animation>();
   legacy.AddClip(Idle, "ow_idle");
   legacy.AddClip(Walk ? Walk : Idle, "ow_walk");
   legacy.AddClip(Run ? Run : Walk ? Walk : Idle, "ow_run");
   legacy.AddClip(Attack ? Attack : Idle, "ow_attack");
   legacy.AddClip(Hit ? Hit : Idle, "ow_hit");
   legacy.AddClip(Death ? Death : Idle, "ow_death");
   legacy.AddClip(Talk && Talk.legacy ? Talk : Idle, "ow_talk");
   legacy.AddClip(Graze && Graze.legacy ? Graze : Idle, "ow_graze");
   legacy.AddClip(Roar && Roar.legacy ? Roar : Idle, "ow_roar");
   foreach (var legacyAnimator in GetComponentsInChildren<Animator>()) legacyAnimator.enabled = false;
   foreach (var key in new[] { "ow_idle", "ow_walk", "ow_run", "ow_talk", "ow_graze" }) legacy[key].wrapMode = WrapMode.Loop;
   foreach (var key in new[] { "ow_attack", "ow_hit", "ow_death", "ow_roar" }) legacy[key].wrapMode = WrapMode.ClampForever;
  }

  static int Slot(string state) {
   switch (state) {
    case "runstop":return SRunStop;case "buck":return SBuck;case "swim":return SSwim;case "jump":return SJump;
    case "walk_l":return SWalkLeft;case "walk_r":return SWalkRight;case "gallop_l":return SRunLeft;case "gallop_r":return SRunRight;
    case "talk": return STalk;
    case "death": return SDeath;
    case "hit": case "gethit": case "knockback": case "knockdown": case "knockdown_loop": case "stagger": return SHit;
    case "trot": return STrot;
    case "canter": return SCanter;
    case "gallop": case "run": case "sprint": return SRun;
    case "walk": return SWalk;
    case "graze": case "eat": return SGraze;
    case "rest": case "sit": case "sleep": return SRest;
    case "roar": case "howl": case "charge_ready": case "victory": return SRoar;
    case "fly": return SFly;
    case "pose": return SPose;
    case "attack_2": return SAttack2;
   }
   if (state.Contains("attack") || state == "slam" || state == "charged" || state == "cast" || state == "stomp" || state == "sweep") return SAttack;
   return SIdle;
  }

  static bool IsLocoSlot(int slot) => slot == SWalk || slot == STrot || slot == SCanter || slot == SRun || slot == SFly || slot==SWalkLeft || slot==SWalkRight || slot==SRunLeft || slot==SRunRight;
  float BankPhaseOffset(int slot)=>slot==SRunLeft?GallopLeftPhaseOffset:slot==SRunRight?GallopRightPhaseOffset:0;
  double StridePhase(int slot)=>Mathf.Repeat((float)(players[slot].GetTime()/Mathf.Max(.01f,clips[slot].length))-BankPhaseOffset(slot),1);

  public static int SelectTurnBank(float degreesPerSecond,int previous){
   if(previous<0&&degreesPerSecond<-14)return -1;if(previous>0&&degreesPerSecond>14)return 1;
   return degreesPerSecond>28?1:degreesPerSecond<-28?-1:0;
  }
  int ResolveTurnSlot(int slot){
   bool walk=slot==SWalk,run=slot==SRun;
   if((!walk&&!run)||measuredSpeed<.3f){turnBank=0;return slot;}
   turnBank=SelectTurnBank(yawRate,turnBank);
   if(walk)return turnBank<0&&WalkLeft?SWalkLeft:turnBank>0&&WalkRight?SWalkRight:slot;
   return turnBank<0&&GallopLeft?SRunLeft:turnBank>0&&GallopRight?SRunRight:slot;
  }

  // When an equine model has distinct Trot / Canter clips, select the natural gait for the current speed
  // even if a generic caller (e.g. OpenWorldWildlife) only requested "walk" or "run".
  int ResolveEquineSlot(string state, int baseSlot) {
   if (!Trot && !Canter) return baseSlot;
   if (state == "trot" && Trot) return STrot;
   if (state == "canter" && Canter) return SCanter;
   if (state == "gallop") return SRun;
   if (baseSlot != SWalk && baseSlot != SRun) return baseSlot;
   float v = measuredSpeed;
   float w = NaturalSpeed("walk");
   float t = NaturalSpeed("trot");
   float c = NaturalSpeed("canter");
   float r = NaturalSpeed("run");
   float splitWT = w > 0.05f && t > w ? (w + t) * 0.5f : 2.6f;
   float splitTC = t > 0.05f && c > t ? (t + c) * 0.5f : 5.0f;
   float splitCR = c > 0.05f && r > c ? (c + r) * 0.5f : 7.4f;
   if (v < splitWT) return SWalk;
   if (v < splitTC && Trot) return STrot;
   if (v < splitCR && Canter) return SCanter;
   return SRun;
  }

  void Update() {
   string state = Driver ? Driver.CurrentState : State;
   float dt = Time.deltaTime;
   // Measured ground speed of the body (whoever moves it: wildlife brain, rider, enemy controller).
   Vector3 p = transform.position;
   if (positionKnown && dt > 0.0001f) {
    Vector3 d = p - lastPosition; d.y = 0f;
    float s = Mathf.Min(d.magnitude / dt, 30f);
    measuredSpeed = Mathf.Lerp(measuredSpeed, s, 1f - Mathf.Exp(-10f * dt));
    float yaw = transform.eulerAngles.y;
    yawRate = Mathf.Lerp(yawRate, Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / dt, -240f, 240f), 1f - Mathf.Exp(-8f * dt));
    lastYaw = yaw;
   } else if (!positionKnown) {
    lastYaw = transform.eulerAngles.y;
   }
   lastPosition = p; positionKnown = true;

   if (legacy) {
    string key = state == "run" || state == "sprint" || state == "gallop" || state == "canter" ? "ow_run" : state == "talk" ? "ow_talk" : state == "death" ? "ow_death" : state == "walk" || state == "trot" || state == "fly" ? "ow_walk" :
     state == "graze" ? "ow_graze" : state == "roar" || state == "howl" ? "ow_roar" : Slot(state) == SAttack || Slot(state) == SAttack2 ? "ow_attack" : Slot(state) == SHit ? "ow_hit" : "ow_idle";
    if (legacyState != key || observedRestart != RestartVersion) { legacyState = key; observedRestart = RestartVersion; legacy[key].time = 0; legacy.CrossFade(key, .15f); }
    if (key == "ow_walk" || key == "ow_run") legacy[key].speed = GaitRate(key == "ow_run" ? "run" : "walk");
    if (state == "death" && !Death) StepFall(dt);
    return;
   }
   if (!graph.IsValid()) return;
   int next = ResolveTurnSlot(ResolveEquineSlot(state, Slot(state)));
   bool deathPose = next == SDeath && !Death;
   if (deathPose) next = SPose;
   if (next != current || observedRestart != RestartVersion) {
    bool restartOnly = next == current;
    bool fromLoco = IsLocoSlot(current);
    bool toLoco = IsLocoSlot(next);
    double t = 0;
    // Gait changes keep the cycle phase so the legs do not jump; everything else starts at the top.
    if (!restartOnly && fromLoco && toLoco && current >= 0 && players[current].IsValid() && clips[current] && clips[next])
     t = StridePhase(current) * clips[next].length;
    current = next; observedRestart = RestartVersion;
    if (players[next].IsValid()) {
     if(next==SRunLeft||next==SRunRight)t=Mathf.Repeat((float)(t/Mathf.Max(.01f,clips[next].length))+BankPhaseOffset(next),1)*clips[next].length;
     if (next == SPose) t = (TuckTime >= 0f ? TuckTime : 0f) * clips[SPose].length;
     players[next].SetTime(t); players[next].SetTime(t);
    }
   }
   float fade = current == SPose ? 4f : current == SDeath || current == SHit || current == SAttack || current == SAttack2 ? 12f : 6f;
   for (int i = 0; i < players.Length; i++) {
    if (!players[i].IsValid()) continue;
    float w = Mathf.MoveTowards(mixer.GetInputWeight(i), i == current ? 1f : 0f, dt * fade);
    mixer.SetInputWeight(i, w);
    if (loops[i] && clips[i] && players[i].GetTime() > clips[i].length)
     players[i].SetTime(players[i].GetTime() % Mathf.Max(.01f, clips[i].length));
   }
   float speed = 1f;
   if (current == SWalk || current==SWalkLeft || current==SWalkRight) speed = GaitRate("walk");
   else if (current == STrot) speed = GaitRate("trot");
   else if (current == SCanter) speed = GaitRate("canter");
   else if (current == SRun || current==SRunLeft || current==SRunRight) speed = GaitRate("run");
   else if (current == SPose) speed = 0f;
   for(int i=0;i<gallopLegs.Length;i++)if(gallopLegs[i].IsValid()){
    double phase=StridePhase(i);
    gallopLegs[i].SetTime(phase*Run.length);gallopLegs[i].SetSpeed(players[i].GetSpeed()*Run.length/Mathf.Max(.01f,clips[i].length));
   }
   if (players[current].IsValid()){
    if(gallopLegs[current].IsValid()){gallopLegs[current].SetSpeed(speed);players[current].SetSpeed(speed*clips[current].length/Mathf.Max(.01f,Run.length));}
    else players[current].SetSpeed(speed);
   }
   if (deathPose) StepFall(dt);
  }

  void LateUpdate() {
   if ((!Trot && !Canter) || current == SDeath || current == SPose || current==SBuck || current==SJump || current==SSwim || falling) {if(isPlanted!=null)System.Array.Clear(isPlanted,0,isPlanted.Length);return;}
   if (!secondaryScanned) ScanSecondaryBones();
   float dt = Time.deltaTime;
   if (dt <= 0.0001f) return;
   // Damped tail lift and turn-lag sway + head turn look-at
   float targetSway = Mathf.Clamp(-yawRate * 0.16f, -22f, 22f);
   tailSway = Mathf.SmoothDamp(tailSway, targetSway, ref tailVel, 0.14f, 90f, dt);
   float speed01 = Mathf.Clamp01(measuredSpeed / 9f);
   float tailLift = speed01 * 18f;
   if (tailBones != null && tailBones.Length > 0) {
    for (int i = 0; i < tailBones.Length; i++) {
     var tb = tailBones[i];
     if (!tb) continue;
     float wave = Mathf.Sin(Time.time * (3.5f + speed01 * 6f) - i * 0.85f) * (2.5f + speed01 * 5.5f);
     tb.rotation = Quaternion.AngleAxis((tailSway * 0.35f + wave) * (i + 1) / tailBones.Length, Vector3.up)
      * Quaternion.AngleAxis(-tailLift / tailBones.Length, transform.right) * tb.rotation;
    }
   }
   float wantHeadYaw = Mathf.Clamp(yawRate * 0.14f, -18f, 18f);
   headYaw = Mathf.Lerp(headYaw, wantHeadYaw, 1f - Mathf.Exp(-7f * dt));
   if (Mathf.Abs(headYaw) > 0.1f) {
    if (neckBone) neckBone.rotation = Quaternion.AngleAxis(headYaw * 0.45f, Vector3.up) * neckBone.rotation;
    if (headBone) headBone.rotation = Quaternion.AngleAxis(headYaw * 0.55f, Vector3.up) * headBone.rotation;
   }
   if (measuredSpeed < 0.2f && (lEarBone || rEarBone)) {
    float earFlick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 1.9f + GetEntityId().GetHashCode() * 0.37f)), 12f) * 14f;
    if (lEarBone) lEarBone.localRotation *= Quaternion.Euler(0f, 0f, earFlick);
    if (rEarBone) rEarBone.localRotation *= Quaternion.Euler(0f, 0f, -earFlick * 0.7f);
   }

   // Planted hoof stance ground-lock: keeps the planted hoof anchored to the ground contact point
   if (legHooves != null && legHooves.Length == 4 && measuredSpeed > 0.15f) {
    for (int i = 0; i < 4; i++) {
     var h = legHooves[i];
     if (!h) continue;
     Vector3 p = h.position;
     float ground = OpenWorldMaterialHelper.SampleGroundHeight(p, transform);
     float hAboveGround = p.y - ground;
     bool banked=current==SWalkLeft||current==SWalkRight||current==SRunLeft||current==SRunRight;
     // The BN toe markers are above the visible sole. Preserve their standing clearance,
     // rather than treating a marker as a point on the bottom of the hoof.
     Vector3 rest=hoofRestLocal[i];
     if(banked&&HasBankContactHeights){var heights=current==SWalkLeft?WalkLeftContactY:current==SWalkRight?WalkRightContactY:current==SRunLeft?GallopLeftContactY:GallopRightContactY;rest.y=heights[i];}
     float contactY=banked?Mathf.Max(ground,transform.TransformPoint(rest).y):ground;
     bool bankedGallop=current==SRunLeft||current==SRunRight;
     bool planted = banked?p.y<=contactY+.035f:hAboveGround<=.045f;
     if(bankedGallop&&GallopContactPhases!=null&&GallopContactPhases.Length>0){
      double phase=StridePhase(current);
      planted=GallopContactPhases[Mathf.Min(GallopContactPhases.Length-1,(int)(phase*GallopContactPhases.Length))][i]>.5f;
      contactY=Mathf.Max(ground,transform.TransformPoint(hoofRestLocal[i]).y);
     }
     if (planted) {
      if (!isPlanted[i]) {
       isPlanted[i] = true;
       plantPos[i] = p;
      plantPos[i].y = banked?Mathf.Max(ground,Mathf.Min(p.y,contactY)):ground;
      }
      Vector3 diff = plantPos[i] - p;
      diff.y = 0f;
      float limit=banked ? ((current==SRunLeft||current==SRunRight) ? .65f : .45f) : .22f;
      if (diff.magnitude < limit) {
       var ankle = legAnkles[i];
       if (ankle) {
        if(banked){
         // During turns, ankle rotation alone cannot compensate the sideways motion of the
         // shoulder/hip. Correct the cached local leg joints, bounded to avoid stretching.
         for(int iteration=0;iteration<5;iteration++){
          foreach(var joint in stanceJoints[i]){
           Vector3 cur=h.position-joint.position,want=plantPos[i]-joint.position;
           if(cur.sqrMagnitude<1e-6f||want.sqrMagnitude<1e-6f)continue;
           joint.rotation=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(cur,want),18)*joint.rotation;
          }
         }
        }else{
         Vector3 curV=h.position-ankle.position,wantV=plantPos[i]-ankle.position;
         if(wantV.sqrMagnitude>1e-4f&&curV.sqrMagnitude>1e-4f)ankle.rotation=Quaternion.FromToRotation(curV,wantV)*ankle.rotation;
        }
       }
      } else {
       isPlanted[i] = false;
      }
     } else {
      isPlanted[i] = false;
     }
    }
   }
  }

  void ScanSecondaryBones() {
   if (secondaryScanned) return;
   secondaryScanned = true;
   var list = new System.Collections.Generic.List<Transform>();
   foreach (var t in GetComponentsInChildren<Transform>(true)) {
    string n = t.name;
    if (n == "Bip01 Neck1" || n == "Bip01 Neck" || n == "BN_Neck_03_012" || (!neckBone && n.StartsWith("BN_Neck_"))) neckBone = t;
    else if (n == "Bip01 Head" || n.StartsWith("BN_Head_00")) headBone = t;
    else if (n == "Bip01 L Ear" || n.StartsWith("BN_L_Ear_00")) lEarBone = t;
    else if (n == "Bip01 R Ear" || n.StartsWith("BN_R_Ear_00")) rEarBone = t;
    else if (n.StartsWith("Bip01 Tail") || (n.StartsWith("BN_Tail_") && !n.EndsWith("_0"))) list.Add(t);
   }
   if (!neckBone && headBone && headBone.parent && headBone.parent.name.IndexOf("Neck", System.StringComparison.OrdinalIgnoreCase) >= 0) neckBone = headBone.parent;
   list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
   tailBones = list.ToArray();

   // Scan leg bones for stance ground-lock (BN_* rig)
   var hooves = new System.Collections.Generic.List<Transform>();
   var ankles = new System.Collections.Generic.List<Transform>();
   foreach (var t in GetComponentsInChildren<Transform>(true)) {
    string n = t.name;
    if (n == "BN_L_Toe_042_0" || n == "BN_R_Toe_047_0" || n == "BN_L_Toe_2_055_0" || n == "BN_R_Toe_2_059_0") {
     hooves.Add(t);
     Transform ank = t.parent;
     if (ank && (ank.name.EndsWith("_042") || ank.name.EndsWith("_047") || ank.name.EndsWith("_055") || ank.name.EndsWith("_059")))
      ank = ank.parent;
     ankles.Add(ank);
    }
   }
   if (hooves.Count == 4) {
    legHooves = hooves.ToArray();
    legAnkles = ankles.ToArray();
    plantPos = new Vector3[4];
    isPlanted = new bool[4];
    hoofRestLocal=new Vector3[4];stanceJoints=new Transform[4][];
    for(int i=0;i<4;i++){
     hoofRestLocal[i]=transform.InverseTransformPoint(legHooves[i].position);
     var joints=new System.Collections.Generic.List<Transform>();var joint=legAnkles[i];
     for(int j=0;j<4&&joint;j++,joint=joint.parent){if(joint.name.StartsWith("BN_Spine_")||joint.name.Contains("Pelvis"))break;joints.Add(joint);}
     stanceJoints[i]=joints.ToArray();
    }
   }
  }

  // Playback rate that keeps the planted foot still at the measured body speed. Small animals may spin
  // their legs faster (a hare's scurry) than big ones (a deer's gallop stays readable).
  float GaitRate(string gait) {
   float natural = NaturalSpeed(gait);
   if (natural <= 0.05f) return 1f;
   if (maxRate < 0f) maxRate = Mathf.Lerp(4.5f, 3f, Mathf.InverseLerp(0.3f, 1.4f, OpenWorldMaterialHelper.ComputeRendererBounds(transform).size.y));
   float want = measuredSpeed > 0.08f ? Mathf.Clamp(measuredSpeed / natural, 0.45f, maxRate) : 0.6f;
   rate = Mathf.Lerp(rate, want, 1f - Mathf.Exp(-18f * Time.deltaTime));
   return rate;
  }

  // Tips the body over onto its side about the lower edge of the flank, accelerating like a fall,
  // with a small settle at the end.
  void StepFall(float dt) {
   if (!falling) {
    falling = true; fallTime = 0f;
    var b = OpenWorldMaterialHelper.ComputeRendererBounds(transform);
    fallHalfWidth = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * 0.7f);
    Vector3 right = transform.right;
    Vector3 away = FallDirection; away.y = 0f;
    fallSide = away.sqrMagnitude > 0.01f ? Mathf.Sign(Vector3.Dot(away, right) + 0.001f) : (Random.value < 0.5f ? -1f : 1f);
    var parent = transform.parent;
    fallBasePos = parent ? parent.InverseTransformPoint(transform.position) : transform.position;
    fallBaseRot = parent ? Quaternion.Inverse(parent.rotation) * transform.rotation : transform.rotation;
    Vector3 groundCenter = new Vector3(b.center.x, b.min.y, b.center.z) + right * (fallSide * fallHalfWidth);
    fallPivot = parent ? parent.InverseTransformPoint(groundCenter) : groundCenter;
   }
   fallTime += dt;
   float u = Mathf.Clamp01(fallTime / 0.55f);
   float angle = 84f * u * u;                                     // gravity: slow start, fast finish
   if (u >= 1f) angle = 84f + Mathf.Sin(Mathf.Min(1f, (fallTime - 0.55f) / 0.25f) * Mathf.PI) * 4f; // a little bounce
   var par = transform.parent;
   Vector3 axis = fallBaseRot * Vector3.forward;
   Quaternion q = Quaternion.AngleAxis(-fallSide * angle, axis);
   Vector3 local = fallPivot + q * (fallBasePos - fallPivot);
   Quaternion rot = q * fallBaseRot;
   if (par) transform.SetPositionAndRotation(par.TransformPoint(local), par.rotation * rot);
   else transform.SetPositionAndRotation(local, rot);
  }

  public void ResetFall() {
   if (!falling) return;
   falling = false;
   var parent = transform.parent;
   if (parent) transform.SetPositionAndRotation(parent.TransformPoint(fallBasePos), parent.rotation * fallBaseRot);
  }

  void OnDestroy() { if (graph.IsValid()) graph.Destroy();if(gallopTorsoMask)Destroy(gallopTorsoMask); }
 }
}
