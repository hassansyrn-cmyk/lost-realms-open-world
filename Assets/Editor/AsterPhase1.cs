using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AsterPhase1 {
 const string ModelPath = "Assets/Art/Models/Aster.fbx";
 const string MaterialPath = "Assets/Resources/Materials/Aster.mat";
 const string PrefabPath = "Assets/Resources/Characters/Aster.prefab";
 const string AnimationSourceRoot = "Assets/Art/Animations/AsterMixamo";
 const string AnimationOutputRoot = "Assets/Resources/Animations/Aster";

 readonly struct ClipSpec {
  public readonly string State;
  public readonly string AssetPath;
  public readonly bool Loop;
  public readonly bool AutoAvatar; // true = auto-map humanoid from this file
  public ClipSpec(string state,string assetPath,bool loop,bool autoAvatar=false){State=state;AssetPath=assetPath;Loop=loop;AutoAvatar=autoAvatar;}
 }

 static readonly ClipSpec[] Clips={
  new ClipSpec("idle",ModelPath,true),
  new ClipSpec("walk",AnimationSourceRoot+"/Aster_walk.fbx",true),
  new ClipSpec("run",AnimationSourceRoot+"/Aster_run.fbx",true),
  new ClipSpec("attack_1",AnimationSourceRoot+"/Aster_attack_1.fbx",false),
  new ClipSpec("attack_2",AnimationSourceRoot+"/Aster_attack_2.fbx",false),
  new ClipSpec("attack_3",AnimationSourceRoot+"/Aster_attack_3.fbx",false),
  new ClipSpec("charged",AnimationSourceRoot+"/Aster_charged.fbx",false),
  new ClipSpec("jump",AnimationSourceRoot+"/Aster_jump.fbx",false),
  new ClipSpec("double_jump",AnimationSourceRoot+"/Aster_double_jump.fbx",false),
  new ClipSpec("dodge",AnimationSourceRoot+"/Aster_dodge.fbx",false),
  new ClipSpec("hit",AnimationSourceRoot+"/Aster_hit.fbx",false),
   new ClipSpec("death",AnimationSourceRoot+"/Aster_death.fbx",false),
   // Per-weapon attack styles (Sep 2026 user pass): chop for overhead grips,
   // spear for pole grips, unarmed for fists. Baked by the same Prepare() run
   // with identical root-flattening; missing outputs fall back to slash ingame.
   // Several states share one Mixamo take file (each spec independently
   // renames + bakes its own state, so sharing is safe).
   new ClipSpec("chop_1",AnimationSourceRoot+"/Aster_Take_Downward.fbx",false),
   new ClipSpec("chop_2",AnimationSourceRoot+"/Aster_Take_ClubCombo.fbx",false),
   new ClipSpec("chop_3",AnimationSourceRoot+"/Aster_Take_GSJump.fbx",false),
   new ClipSpec("spear_1",AnimationSourceRoot+"/Aster_Take_Boxing.fbx",false),
   new ClipSpec("spear_2",AnimationSourceRoot+"/Aster_Take_SnSAttack2.fbx",false),
   new ClipSpec("spear_3",AnimationSourceRoot+"/Aster_Take_SnSAttack3.fbx",false),
   new ClipSpec("unarmed_1",AnimationSourceRoot+"/Aster_Take_Kick.fbx",false),
   new ClipSpec("unarmed_2",AnimationSourceRoot+"/Aster_Take_Boxing.fbx",false),
   new ClipSpec("unarmed_3",AnimationSourceRoot+"/Aster_Take_SnSKick.fbx",false),
   new ClipSpec("block",AnimationSourceRoot+"/Aster_Take_Block.fbx",false),
   new ClipSpec("hit_2",AnimationSourceRoot+"/Aster_Take_Hit2.fbx",false),
   new ClipSpec("hit_3",AnimationSourceRoot+"/Aster_Take_Hit3.fbx",false),
   new ClipSpec("cast",AnimationSourceRoot+"/Aster_Take_Cast.fbx",false),
   // ExplosiveLLC RPG-Character takes (Sep 2026 feel pass): ground dash rolls,
   // air dash keeps the existing dodge pose; two extra hit reactions widen the
   // HitVariant pool. Humanoid retarget via CopyFromOther, same as Mixamo takes.
   new ClipSpec("roll",AnimationSourceRoot+"/Aster_Take_DiveRoll.fbx",false,true),
   new ClipSpec("hit_4",AnimationSourceRoot+"/Aster_Take_GetHitF.fbx",false,true),
   new ClipSpec("hit_5",AnimationSourceRoot+"/Aster_Take_GetHitB.fbx",false,true)
  };

 [MenuItem("Lost Realms/Phase 1/Prepare Aster Mixamo Character")]
 public static void Prepare(){
  EnsureDirectories();
  PrepareTexture("Assets/Art/Textures/Aster_0.jpg",true,1024);
  PrepareTexture("Assets/Art/Textures/Aster_1.jpg",false,2048);
  ConfigureBaseModel();

  var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
  if(!sourceModel)throw new Exception("Missing supplied Aster model: "+ModelPath);
  var sourceAnimator=sourceModel.GetComponentInChildren<Animator>(true);
  var avatar=sourceAnimator?sourceAnimator.avatar:null;
  if(!avatar||!avatar.isHuman||!avatar.isValid)throw new Exception("Supplied Aster FBX did not produce a valid Humanoid avatar.");

var report=new List<string>{"Aster Phase 1 Mixamo integration","Model: "+ModelPath,"Avatar: valid humanoid"};
   // Record the idle clip's root height. Zeroing ALL RootT curves (including Y)
   // removes the transition glitches, but it sinks the skeleton by baselineRootY;
   // BuildPrefab pushes the prefab up by exactly that amount so the character
   // stands at the correct height while every clip keeps a flat zeroed root.
   float baselineRootY=0f;
   var idleSource=FindImportedClip(ModelPath);
   if(idleSource){
    foreach(var b in AnimationUtility.GetCurveBindings(idleSource)){
     if(b.propertyName=="RootT.y"){
      var c=AnimationUtility.GetEditorCurve(idleSource,b);
      if(c!=null&&c.keys.Length>0)baselineRootY=c.keys[0].value;
      break;
     }
    }
   }
   foreach(var spec in Clips){
    if(spec.AssetPath!=ModelPath)ConfigureAnimation(spec,avatar);
    var imported=FindImportedClip(spec.AssetPath);
    if(!imported)throw new Exception("No animation clip imported from "+spec.AssetPath);
    string outputPath=AnimationOutputRoot+"/"+spec.State+".anim";
    SaveClip(imported,outputPath,spec.State,spec.Loop,baselineRootY);
    var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
    if(!saved)throw new Exception("Failed to create "+outputPath);
    report.Add(spec.State+": "+saved.length.ToString("0.000")+"s, loop="+spec.Loop);
   }

   BuildPrefab(sourceModel,avatar,baselineRootY);
  AssetDatabase.SaveAssets();
  File.WriteAllLines("ASTER_PHASE1_REPORT.txt",report);
  BakeHeroMotionSet();
  Debug.Log("MODEL_INTEGRATION_PASSED: supplied Mixamo Aster model, material, and 12 animation states prepared.");
 }

 // Asset Store humanoid motions pass through the same root-normalizing baker.
 [MenuItem("Lost Realms/Phase 1/Bake Imported Motion Set")]
 public static void PrepareImportedMotionSet() {
  EnsureDirectories();
  var report = new List<string> { "# Imported humanoid animation preparation", "", "Generated by Aster Phase 1; every RootT curve is constant zero. Original pack files are unchanged. Rendered retargeting and combat timing still require review.", "" };
  var motions = new[] {
   ("talk", "Assets/Kevin Iglesias/Human Animations/Animations/Female/Social/Conversation/HumanF@Talk01.fbx", true),
   ("spear_1", "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/Polearm/HumanF@AttackPolearm01.fbx", false),
   ("spear_2", "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/Polearm/HumanF@AttackPolearm01.fbx", false),
   ("spear_3", "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/Polearm/HumanF@AttackPolearm01.fbx", false),
   ("run", "Assets/EverydayMotionPackFree/Motion/02_Move/@female_move_run_sprint.FBX", true),
   ("unarmed_1", "Assets/FightingMotionsVolume1/FBX/hp_straight_left_A.fbx", false),
   ("unarmed_2", "Assets/FightingMotionsVolume1/FBX/nomove/hp_straight_right_A2.fbx", false),
   ("unarmed_3", "Assets/FightingMotionsVolume1/FBX/nomove/hk_rh_right_A2.fbx", false),
   ("fall", "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Fall.FBX", true)
  };
  foreach (var motion in motions) {
   var clip = FindImportedClip(motion.Item2);
   if (!clip || !clip.humanMotion || clip.length < .1f) { report.Add("- DEFERRED " + motion.Item1 + ": valid humanoid clip unavailable at " + motion.Item2); continue; }
   SaveClip(clip, AnimationOutputRoot + "/" + motion.Item1 + ".anim", motion.Item1, motion.Item3, 0);
   report.Add("- BAKED " + motion.Item1 + ": " + motion.Item2 + " (" + clip.length.ToString("0.00") + "s).");
  }
  AssetDatabase.SaveAssets();
  File.WriteAllLines("Docs/AssetAudit/ANIMATION_PREPARATION.md", report);
  BakeHeroMotionSet();
 }

 // ---------------------------------------------------------------------------------------------
 // Hero motion set (Oct 2026 animation pass): locomotion, jumps, dash, glide, four-hit combos per
 // weapon style, charged attacks, elemental casts and the burst, all from the Asset Store packs and
 // baked through the same flattening as every other Aster clip (OpenWorldMotionLab.BakeCopy: neutral
 // root settings, every RootT curve constant 0). Alongside the clips it writes motion_meta.json:
 // stride speed and foot phase for locomotion, strike frames, and the body-height side-car that puts
 // crouches and landings back on the ground without root curves.
 const string KI = "Assets/Kevin Iglesias/Human Animations/Animations/Male";
 const string EX = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations";
 const string WP1 = "Assets/ExplosiveLLC/Warrior Pack Bundle 1 FREE";
 const string WP2 = "Assets/ExplosiveLLC/Warrior Pack Bundle 2 FREE";
 const string DL = "Assets/DoubleL/FBX Unity";
 const string BL = "Assets/Blink/Art/Animations";
 const string KiIdle = KI + "/Idles/HumanM@Idle01.fbx";
 const string FM = "Assets/FightingMotionsVolume1/FBX";
 const string DLA = DL + "/Actions/Ladder";

 enum Height { None, First, Last, KiIdle }

 readonly struct HeroMotion {
  public readonly string State, Path, Clip;
  public readonly bool Loop;
  public readonly float Start, End;
  public readonly Height Height;
  public readonly float[] Strikes;
  public HeroMotion(string state, string path, bool loop, Height height, string clip = "", float start = 0f, float end = -1f, float[] strikes = null) {
   State = state; Path = path; Loop = loop; Height = height; Clip = clip; Start = start; End = end; Strikes = strikes;
  }
 }

 static readonly HeroMotion[] HeroMotions = {
  new HeroMotion("idle", KiIdle, true, Height.KiIdle),
  new HeroMotion("walk", KI + "/Movement/Walk/HumanM@Walk01_Forward.fbx", true, Height.KiIdle),
  new HeroMotion("run", KI + "/Movement/Run/HumanM@Run01_Forward.fbx", true, Height.KiIdle),
  new HeroMotion("sprint", KI + "/Movement/Sprint/HumanM@Sprint01_Forward.fbx", true, Height.KiIdle),
  new HeroMotion("dash", KI + "/Movement/Sprint/HumanM@Sprint01_Forward.fbx", true, Height.KiIdle),
  new HeroMotion("jump", KI + "/Movement/Jump/HumanM@Jump01.fbx", false, Height.None, "", 0.12f, 0.95f),
  new HeroMotion("fall", KI + "/Movement/Jump/HumanM@Fall01.fbx", true, Height.None),
  new HeroMotion("land", KI + "/Movement/Jump/HumanM@Jump01 - Land.fbx", false, Height.KiIdle),
  new HeroMotion("land_hard", EX + "/Unarmed/RPG-Character@Unarmed-Jump.FBX", false, Height.Last, "Unarmed-Land"),
  new HeroMotion("double_jump", EX + "/Unarmed/RPG-Character@Unarmed-Jump-Flip.FBX", false, Height.None),
  // Arms raised overhead: the driver's arm IK closes the hands on the paraglider bar.
  new HeroMotion("glide", KI + "/Movement/Jump/HumanM@Fall01.fbx", true, Height.None),
  new HeroMotion("attack_1", DL + "/One Hand Up/Attack_A/InPlace/1Hand_Up_Attack_A_1_InPlace.fbx", false, Height.First),
  new HeroMotion("attack_2", DL + "/One Hand Up/Attack_A/InPlace/1Hand_Up_Attack_A_2_InPlace.fbx", false, Height.First),
  new HeroMotion("attack_3", DL + "/One Hand Up/Attack_A/InPlace/1Hand_Up_Attack_A_3_InPlace.fbx", false, Height.First),
  new HeroMotion("attack_4", DL + "/One Hand Up/Attack_B/InPlace/1Hand_Up_Attack_B_3_InPlace.fbx", false, Height.First),
  new HeroMotion("charged", DL + "/One Hand Up/Attack_B/InPlace/1Hand_Up_Attack_B_2_InPlace.fbx", false, Height.First),
  new HeroMotion("chop_1", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack1.FBX", false, Height.First),
  new HeroMotion("chop_2", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack3.FBX", false, Height.First),
  new HeroMotion("chop_3", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack5.FBX", false, Height.First),
  new HeroMotion("chop_4", KI + "/Combat/2H/HumanM@Attack2H01.fbx", false, Height.First),
  new HeroMotion("charged_chop", BL + "/Combat/SpinAttack_TwoHanded.fbx", true, Height.First, "SpinLoop_TwoHanded"),
  new HeroMotion("spear_1", KI + "/Combat/Polearm/HumanM@AttackPolearm01.fbx", false, Height.First),
  new HeroMotion("spear_2", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack9.FBX", false, Height.First),
  new HeroMotion("spear_3", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack5.FBX", false, Height.First),
  new HeroMotion("spear_4", WP2 + "/Knight Warrior Mecanim Animation Pack/Animations/Knight@Attack1.FBX", false, Height.First),
  new HeroMotion("charged_spear", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Run-Forward-Attack1.FBX", false, Height.First),
  new HeroMotion("unarmed_1", EX + "/Unarmed/RPG-Character@Unarmed-Attack-L1.FBX", false, Height.First),
  new HeroMotion("unarmed_2", EX + "/Unarmed/RPG-Character@Unarmed-Attack-R1.FBX", false, Height.First),
  new HeroMotion("unarmed_3", EX + "/Unarmed/RPG-Character@Unarmed-Attack-L3.FBX", false, Height.First),
  new HeroMotion("unarmed_4", EX + "/Unarmed/RPG-Character@Unarmed-Attack-R3.FBX", false, Height.First),
  new HeroMotion("charged_unarmed", WP1 + "/Karate Warrior Mecanim Animation Pack/Animations/Karate@Attack1.FBX", false, Height.First, "", 0f, -1f, new[] { 0.42f }),
  new HeroMotion("cast_ember", WP2 + "/Mage Warrior Mecanim Animation Pack/Animations/Mage@Attack1.FBX", false, Height.First),
  new HeroMotion("cast_frost", WP1 + "/Sorceress Warrior Mecanim Animation Pack/Animations/Sorceress@Attack1.FBX", false, Height.First),
  new HeroMotion("cast_gale", WP1 + "/Ninja Warrior Mecanim Animation Pack/Animations/Ninja@Attack1.FBX", false, Height.First),
  // Both hands raise the blade to the sky, then slam it into the ground (impact ~0.65 s): the burst's
  // cinematic beat. (The Mixamo cast take played upside down in game, so it is no longer used here.)
  new HeroMotion("burst", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack11.FBX", false, Height.First),
  // Physics pass: knocked down by heavy blows and back up (strike = the moment the body lies flat /
  // stands again), and climbing from the ladder set (the driver's IK puts hands and feet on the rock;
  // stride = the vertical climbing speed of the original root motion, so the climb rate matches).
  new HeroMotion("knockdown", FM + "/knockdown_A.fbx", false, Height.First),
  new HeroMotion("getup", FM + "/getup_A.fbx", false, Height.Last),
  new HeroMotion("climb_idle", DLA + "/Ladder_Idle.fbx", true, Height.None),
  new HeroMotion("climb_up", DLA + "/Ladder_Up_Play.fbx", true, Height.None),
  new HeroMotion("climb_down", DLA + "/Ladder_Down_Play.fbx", true, Height.None),
  new HeroMotion("climb_top", DLA + "/Ladder_Up_End.fbx", false, Height.None)
 };

 // Body down / body up moments from the raw height curve (30 Hz): the first frame within 6 cm of the
 // lowest point, or of the highest.
 static float LowMoment(float[] raw, bool lowest) {
  if (raw == null || raw.Length == 0) return 0f;
  float target = lowest ? raw.Min() : raw.Max();
  for (int i = 0; i < raw.Length; i++) if (Mathf.Abs(raw[i] - target) < 0.06f) return i / LostRealmsOW.OpenWorldMotionMeta.Rate;
  return 0f;
 }

 [MenuItem("Lost Realms/Phase 1/Bake Hero Motion Set")]
 public static void BakeHeroMotionSet() {
  EnsureDirectories();
  var report = new List<string> { "# Hero motion set", "", "Baked by AsterPhase1.BakeHeroMotionSet through OpenWorldMotionLab.BakeCopy: every RootT curve is constant 0; vendor files are only read.", "" };
  var metas = new List<LostRealmsOW.OWMotionClip>();
  var baked = new HashSet<string>();
  using (var probe = new LostRealmsOW.OpenWorldMotionLab.Probe()) {
   float kiIdle = 0f;
   var kiIdleClip = LostRealmsOW.OpenWorldMotionLab.LoadClip(KiIdle, "");
   if (kiIdleClip) kiIdle = LostRealmsOW.OpenWorldMotionLab.RawHeightMean(probe, kiIdleClip);
   foreach (var m in HeroMotions) {
    var source = LostRealmsOW.OpenWorldMotionLab.LoadClip(m.Path, m.Clip);
    if (!source || !source.humanMotion) { report.Add("- DEFERRED " + m.State + ": no humanoid clip at " + m.Path + " " + m.Clip); continue; }
    var flat = LostRealmsOW.OpenWorldMotionLab.BakeCopy(source, false, m.Start, m.End);
    var keep = LostRealmsOW.OpenWorldMotionLab.BakeCopy(source, true, m.Start, m.End);
    try {
     var settings = AnimationUtility.GetAnimationClipSettings(flat);
     settings.loopTime = m.Loop;
     AnimationUtility.SetAnimationClipSettings(flat, settings);
     var a = LostRealmsOW.OpenWorldMotionLab.Analyse(probe, flat, keep, m.Loop);
     var meta = new LostRealmsOW.OWMotionClip { state = m.State, source = m.Path + (string.IsNullOrEmpty(m.Clip) ? "" : " " + m.Clip), length = flat.length, loop = m.Loop, stride = a.strideSpeed, plant = a.leftPlant, strikes = m.Strikes ?? a.strikes };
     if (m.State.StartsWith("climb_") && a.height.Length > 1) meta.stride = Mathf.Abs(a.height[a.height.Length - 1] - a.height[0]) / Mathf.Max(0.05f, flat.length);
     if (m.State == "knockdown") meta.strikes = new[] { LowMoment(a.height, true) };
     if (m.State == "getup") meta.strikes = new[] { LowMoment(a.height, false) };
     if (m.Height != Height.None && a.height.Length > 0) {
      float reference = m.Height == Height.First ? a.height[0] : m.Height == Height.Last ? a.height[a.height.Length - 1] : kiIdle;
      meta.height = a.height.Select(h => Mathf.Clamp(h - reference, -1.1f, 0.6f)).Select(h => Mathf.Round(h * 1000f) / 1000f).ToArray();
     } else meta.height = Array.Empty<float>();
     metas.Add(meta);
     string outputPath = AnimationOutputRoot + "/" + m.State + ".anim";
     flat.name = m.State;
     var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
     if (existing) { EditorUtility.CopySerialized(flat, existing); EditorUtility.SetDirty(existing); }
     else { AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(flat), outputPath); }
     baked.Add(m.State);
     report.Add($"- BAKED {m.State}: {meta.source} [{m.Start:0.00}..{(m.End > 0 ? m.End : source.length):0.00}s] {flat.length:0.00}s loop={m.Loop} stride={meta.stride:0.00} plant={meta.plant:0.00} strikes={string.Join(",", meta.strikes.Select(x => x.ToString("0.00")))} height={(meta.height.Length > 0 ? meta.height.Min().ToString("0.00") + ".." + meta.height.Max().ToString("0.00") : "-")}");
    } finally {
     probe.Forget();
     UnityEngine.Object.DestroyImmediate(flat);
     UnityEngine.Object.DestroyImmediate(keep);
    }
   }
   // Strike frames for the other baked states (hits, block, talk, death...), without height.
   foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationOutputRoot })) {
    var path = AssetDatabase.GUIDToAssetPath(guid);
    string state = Path.GetFileNameWithoutExtension(path);
    if (baked.Contains(state)) continue;
    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    if (!clip || !clip.humanMotion) continue;
    var a = LostRealmsOW.OpenWorldMotionLab.Analyse(probe, clip, clip, clip.isLooping);
    probe.Forget();
    metas.Add(new LostRealmsOW.OWMotionClip { state = state, source = path, length = clip.length, loop = clip.isLooping, stride = a.strideSpeed, plant = a.leftPlant, strikes = a.strikes, height = Array.Empty<float>() });
   }
  }
  AssetDatabase.SaveAssets();
  var file = new LostRealmsOW.OWMotionMetaFile { version = 1, clips = metas.OrderBy(c => c.state).ToArray() };
  File.WriteAllText(AnimationOutputRoot + "/motion_meta.json", JsonUtility.ToJson(file, true));
  AssetDatabase.ImportAsset(AnimationOutputRoot + "/motion_meta.json");
  BakeSharedReactions(report);
  Directory.CreateDirectory("Docs/AssetAudit");
  File.WriteAllLines("Docs/AssetAudit/HERO_MOTION_SET.md", report);
  Debug.Log("[AsterPhase1] hero motion set: " + baked.Count + " clips baked, meta for " + metas.Count + " states");
 }

 // Knockdown, lying and get-up for every humanoid enemy (Resources/Animations/Shared). Enemy clips keep
 // their root height (the enemy convention, like Shared/death), so only RootT.x/z are flattened; the
 // side-car meta records when the body lies flat and when it stands again.
 static readonly (string state, string path, bool loop)[] SharedReactions = {
  // Thrown back off its feet (legs up, lands flat), lying, getting up, and a heavy-hit stagger back.
  ("knockdown", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Knockdown1.FBX", false),
  ("knockdown_loop", FM + "/knockdown_loop_A.fbx", true),
  ("getup", FM + "/getup_A.fbx", false),
  ("knockback", EX + "/2Hand-Sword/RPG-Character@2Hand-Sword-Knockback-Back1.FBX", false)
 };

 const string SharedOutputRoot = "Assets/Resources/Animations/Shared";

 static void BakeSharedReactions(List<string> report) {
  EnsureFolder(SharedOutputRoot);
  report.Add("");
  report.Add("## Shared enemy reactions (RootT.x/z flattened, RootT.y kept)");
  var metas = new List<LostRealmsOW.OWMotionClip>();
  using (var probe = new LostRealmsOW.OpenWorldMotionLab.Probe()) {
   foreach (var r in SharedReactions) {
    var source = LostRealmsOW.OpenWorldMotionLab.LoadClip(r.path, "");
    if (!source || !source.humanMotion) { report.Add("- DEFERRED " + r.state + ": no humanoid clip at " + r.path); continue; }
    var flat = LostRealmsOW.OpenWorldMotionLab.BakeCopy(source, false);
    var keep = LostRealmsOW.OpenWorldMotionLab.BakeCopy(source, true);
    try {
     var settings = AnimationUtility.GetAnimationClipSettings(keep);
     settings.loopTime = r.loop;
     AnimationUtility.SetAnimationClipSettings(keep, settings);
     var a = LostRealmsOW.OpenWorldMotionLab.Analyse(probe, flat, keep, r.loop);
     float moment = r.state == "getup" ? LowMoment(a.height, false) : r.state == "knockback" ? (a.strikes.Length > 0 ? a.strikes[0] : 0.3f) : LowMoment(a.height, true);
     metas.Add(new LostRealmsOW.OWMotionClip { state = r.state, source = r.path, length = keep.length, loop = r.loop, stride = 0f, plant = -1f, strikes = new[] { moment }, height = Array.Empty<float>() });
     string outputPath = SharedOutputRoot + "/" + r.state + ".anim";
     keep.name = r.state;
     var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
     if (existing) { EditorUtility.CopySerialized(keep, existing); EditorUtility.SetDirty(existing); }
     else AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(keep), outputPath);
     report.Add($"- BAKED {r.state}: {r.path} {keep.length:0.00}s loop={r.loop} {(r.state == "getup" ? "stands" : "lies")} at {moment:0.00}s");
    } finally {
     probe.Forget();
     UnityEngine.Object.DestroyImmediate(flat);
     UnityEngine.Object.DestroyImmediate(keep);
    }
   }
  }
  AssetDatabase.SaveAssets();
  var file = new LostRealmsOW.OWMotionMetaFile { version = 1, clips = metas.ToArray() };
  File.WriteAllText(SharedOutputRoot + "/motion_meta.json", JsonUtility.ToJson(file, true));
  AssetDatabase.ImportAsset(SharedOutputRoot + "/motion_meta.json");
 }

 static void EnsureDirectories(){
  EnsureFolder("Assets/Resources/Characters");
  EnsureFolder("Assets/Resources/Animations/Aster");
  EnsureFolder("Assets/Resources/Materials");
 }

 static void EnsureFolder(string path){
  if(AssetDatabase.IsValidFolder(path))return;
  string parent=Path.GetDirectoryName(path).Replace('\\','/');
  EnsureFolder(parent);
  AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
 }

 static void PrepareTexture(string path,bool normalMap,int maxSize){
  var importer=AssetImporter.GetAtPath(path) as TextureImporter;
  if(!importer)throw new Exception("Missing supplied Aster texture: "+path);
  importer.textureType=normalMap?TextureImporterType.NormalMap:TextureImporterType.Default;
  importer.sRGBTexture=!normalMap;
  importer.maxTextureSize=maxSize;
  importer.textureCompression=TextureImporterCompression.Compressed;
  importer.mipmapEnabled=true;
  importer.SaveAndReimport();
 }

 static void ConfigureBaseModel(){
  var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
  if(!importer)throw new Exception("Missing supplied Aster FBX: "+ModelPath);
  ConfigureShared(importer);
  importer.importAnimation=true;
  importer.animationType=ModelImporterAnimationType.Human;
  importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
  // A new FBX's defaultClipAnimations list is populated only after its first
  // animation-enabled import. Import once, reload the importer, then configure it.
  importer.SaveAndReimport();
  importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
  var clips=importer.defaultClipAnimations;
  ConfigureClips(clips,"idle",true);
  importer.clipAnimations=clips;
  importer.SaveAndReimport();
 }

 static void ConfigureAnimation(ClipSpec spec,Avatar avatar){
  var importer=AssetImporter.GetAtPath(spec.AssetPath) as ModelImporter;
  if(!importer)throw new Exception("Missing supplied animation FBX: "+spec.AssetPath);
  ConfigureShared(importer);
  importer.importAnimation=true;
  importer.animationType=ModelImporterAnimationType.Human;
  if(spec.AutoAvatar){
   // ExplosiveLLC RPG-Character takes (B_* bones): CopyFromOther with the
   // Mixamo Aster avatar matches no bones and Human import yields ZERO clips.
   // CreateFromThisModel auto-maps 49 humanoid bones and produces the clip;
   // the baked muscle curves are avatar-agnostic and play on Aster at runtime.
   importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
  }else{
   importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
   importer.sourceAvatar=avatar;
  }
  importer.SaveAndReimport();
  importer=AssetImporter.GetAtPath(spec.AssetPath) as ModelImporter;
  var clips=importer.defaultClipAnimations;
  ConfigureClips(clips,spec.State,spec.Loop);
  importer.clipAnimations=clips;
  importer.SaveAndReimport();
 }

 static void ConfigureShared(ModelImporter importer){
  importer.materialImportMode=ModelImporterMaterialImportMode.None;
  importer.importCameras=false;
  importer.importLights=false;
  importer.importBlendShapes=true;
  importer.isReadable=false;
  importer.meshCompression=ModelImporterMeshCompression.Low;
  importer.optimizeGameObjects=false;
  importer.globalScale=1f;
  importer.useFileScale=true;
 }

 static void ConfigureClips(ModelImporterClipAnimation[] clips,string state,bool loop){
  if(clips==null||clips.Length==0)throw new Exception("FBX has no default animation clip for state "+state);
  foreach(var clip in clips){
   clip.name=state;
   clip.loopTime=loop;
   clip.keepOriginalOrientation=true;
   clip.lockRootRotation=true;
   clip.keepOriginalPositionY=true;
   clip.lockRootHeightY=true;
   clip.keepOriginalPositionXZ=true;
   clip.lockRootPositionXZ=true;
  }
 }

 static AnimationClip FindImportedClip(string path){
  return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal));
 }

static void SaveClip(AnimationClip source,string outputPath,string state,bool loop,float baselineRootY){
   var clip=UnityEngine.Object.Instantiate(source);
   clip.name=state;
   NormalizeRoot(clip);
   var settings=AnimationUtility.GetAnimationClipSettings(clip);
   settings.loopTime=loop;
   AnimationUtility.SetAnimationClipSettings(clip,settings);
   AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
   var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
   if (existing) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(clip); }
   else AssetDatabase.CreateAsset(clip,outputPath);
  }

  // Mixamo exports carry ramped root-translation curves (RootT.x/y/z ramp
  // 1-3+ units across walk/run/dodge clips). The game capsule supplies all
  // locomotion, so we flatten EVERY RootT curve to zero. This prevents the
  // "model grows/teleports at walk start" popping and keeps transitions clean.
  // The resulting root-height sink is compensated in BuildPrefab.
  static void NormalizeRoot(AnimationClip clip){
   foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
    if(binding.propertyName.StartsWith("RootT",StringComparison.Ordinal))
     AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0f,clip.length,0f));
   }
  }

 static void BuildPrefab(GameObject sourceModel,Avatar avatar,float baselineRootY){
  var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
  if(!material||!material.mainTexture)throw new Exception("Aster runtime material is missing its supplied albedo texture.");

  var root=new GameObject("Aster");
  try{
   var model=(GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
   model.name="Aster Mixamo Character";
   model.transform.SetParent(root.transform,false);
   var renderers=model.GetComponentsInChildren<Renderer>(true);
   if(renderers.Length==0)throw new Exception("Supplied Aster FBX contains no renderers.");
   foreach(var renderer in renderers){
    renderer.sharedMaterials=Enumerable.Repeat(material,Math.Max(1,renderer.sharedMaterials.Length)).ToArray();
    if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=false;
   }

   var bounds=renderers[0].bounds;
   foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
   float scale=1.8f/Mathf.Max(.01f,bounds.size.y);
   model.transform.localScale=Vector3.one*scale;
   model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
   // NormalizeRoot zeros RootT (incl. Y) to eliminate transition glitches,
   // which sinks the skeleton by baselineRootY. Push the model up by exactly
   // that amount so the offset is baked into the prefab geometry instead of
   // relying on animation curves.
   model.transform.localPosition+=Vector3.up*(baselineRootY*scale);

   var animator=model.GetComponentInChildren<Animator>(true);
   if(!animator)animator=model.AddComponent<Animator>();
   animator.avatar=avatar;
   animator.applyRootMotion=false;
   animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;

   var lod=root.AddComponent<LODGroup>();
   lod.SetLODs(new[]{new LOD(.025f,renderers)});
   lod.RecalculateBounds();
   if(!PrefabUtility.SaveAsPrefabAsset(root,PrefabPath))throw new Exception("Failed to save Aster prefab.");
  }finally{
   UnityEngine.Object.DestroyImmediate(root);
  }
 }
}
