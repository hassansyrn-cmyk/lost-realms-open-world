using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Second pass over the prepared animal prefabs (runs at the end of OpenWorldContentPreparation and on its
 // own). Vendor packs are only read; every output lives under Assets/GameContent/Imported.
 //  - Picks the right clip for each state from the whole pack (in-place forward cycles, not the strafe,
 //    backward or combat-idle variants), including clips whose rig lacks a few bones (birds' flight).
 //  - Loops the cycles and removes any travel baked into them (the game moves the body).
 //  - Checks which way the model walks and turns it to face +Z if the vendor exported it sideways.
 //  - Measures how fast each walk / trot / canter / run cycle really travels (the planted foot's speed
 //    relative to the body) so the runtime can play it at the matching rate: no sliding feet.
 //  - Finds the run-cycle frame with the legs most gathered, used as the pose of an animal lying dead.
 //  - Horses: builds Assets/GameContent/Horses/Horse_Realistic.prefab (LOD0/1/2, URP materials, 2K/1K
 //    textures), measures the saddle (seat point on the back, its bone, barrel width) and the bit, for the
 //    riding pose and the reins.
 // Writes Docs/AssetAudit/ANIMAL_PREPARATION_REPORT.md.
 public static class ClaudeAnimalPrep {
  const string Output = "Assets/GameContent/Imported";

  sealed class Spec {
   public string Source, Idle, Walk, Trot, Canter, Run, Attack, Attack2, Hit, Death, Graze, Rest, Roar, Fly;
   public string WalkLeft,WalkRight,GallopLeft,GallopRight,RunStop,Buck,Swim,Jump;
   public bool Horse, Bird;
   // Yaw (degrees) that turns the model to face +Z, for packs whose walk cycle does not show which way the
   // model faces (set after looking at it in a lineup); null = measure it from the walk cycle.
   public float? Turn;
   // A coat variant to make from the pack's albedo (Resources/World/Coats/<name>.png), for animals that
   // ship in one colour only: "frost" = a pale winter coat.
   public string CoatName, CoatKind;
  }

  static readonly Spec[] Specs = {
   new Spec { Source = "Assets/GameContent/Horses/Horse_Realistic.prefab", Idle = "Horse_Idle", Walk = "Horse_Walk", Trot = "Horse_Trot", Canter = "Horse_Canter", Run = "Horse_Gallop", Graze = "Horse_Graze", Rest = "Horse_Rest", Roar = "Horse_Roar", Attack = "Horse_Attack", Attack2 = "Horse_Attack2", Death = "Horse_Death", WalkLeft="Horse_Walk_L",WalkRight="Horse_Walk_R",GallopLeft="Horse_Gallop_L",GallopRight="Horse_Gallop_R",RunStop="Horse_RunStop",Buck="Horse_Buck",Swim="Horse_Swim",Jump="Horse_Jump",Horse = true, Turn = 0f },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab", Idle = "Deer_001_idle", Walk = "Deer_001_walk", Run = "Deer_001_run" },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab", Idle = "Dog_001_idle", Walk = "Dog_001_walk", Run = "Dog_001_run" },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab", Idle = "Chicken_001_idle", Walk = "Chicken_003_walk", Run = "Chicken_002_run" },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Kitty_001.prefab", Idle = "Kitty_001_idle", Walk = "Kitty_001_walk", Run = "Kitty_001_run" },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Tiger_001.prefab", Idle = "Tiger_001_idle", Walk = "Tiger_001_walk", Run = "Tiger_001_run", Graze = "Tiger_001_idle_rare" },
   new Spec { Source = "Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab", Idle = "Pinguin_001_idle", Walk = "Pinguin_001_walk", Run = "Pinguin_001_run" },
   // The fox pack's root-motion cycles: their baked travel gives the exact gait speeds (it is then removed).
   new Spec { Source = "Assets/Fox/Prefabs/Fox.prefab", Idle = "Fox_Idle", Walk = "Fox_Walk", Run = "Fox_Run", Attack = "Fox_Attack_Paws", Attack2 = "Fox_Attack_Tail", Rest = "Fox_Sit2_Idle", Graze = "Fox_Sit_Idle_Break" },
   new Spec { Source = "Assets/Rabbits/Prefabs/Rabbit 1.prefab", Idle = "Idle", Walk = "Run", Run = "Run", Death = "Dead" },
   new Spec { Source = "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab", Idle = "1 type_Idle Breathing", Walk = "1 type_Walk_Loop", Run = "1 type_Run Loop", Graze = "1 type_Idle_Playing" },
   new Spec { Source = "Assets/Wolf_Animated/Prefabs/Wolf.prefab", Idle = "breathes", Walk = "walk", Run = "run", Attack = "attack1", Attack2 = "attack2", Hit = "damege", Death = "die", Roar = "howl", Graze = "eating", Rest = "sit" },
   new Spec { Source = "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab", Idle = "Bear_Idle", Walk = "Bear_WalkForward", Run = "Bear_RunForward", Attack = "Bear_Attack1", Attack2 = "Bear_Attack3", Hit = "Bear_GetHitFromFront", Death = "Bear_Death", Roar = "Bear_Buff", Graze = "Bear_Eat", Rest = "Bear_Sit", CoatName = "FrostBear", CoatKind = "frost" },
   // The horned brute faces +Z as exported (its walk cycle travels +Z; the 0053 lineup with a 90 degree
   // turn showed its back). Turn 0 keeps it as authored and undoes that trial turn on re-preparation.
   new Spec { Source = "Assets/Creature_Rhinoceros/Prefab/Rhinoceros.prefab", Idle = "Idle", Walk = "Walk", Run = "Run", Attack = "Attack(1)", Attack2 = "Attack(3)", Hit = "Get_Hit", Death = "Dead", Roar = "Shout", Turn = 0f },
   new Spec { Source = "Assets/Rhino/Prefab/Rhino_PBR.prefab", Idle = "Idle", Walk = "Walk", Run = "Run", Attack = "Attack", Hit = "Get_Hit", Death = "Dead", Roar = "shout", Graze = "Eats" },
   new Spec { Source = "Assets/living birds/resources/lb_robin.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_sparrow.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_crow.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "ruffle", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_blueJay.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_cardinal.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_goldFinch.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
   new Spec { Source = "Assets/living birds/resources/lb_chickadee.prefab", Idle = "watch01", Walk = "forwardHop", Run = "forwardHop", Graze = "peck", Rest = "preen", Roar = "sing", Fly = "flyStraight", Death = "die", Bird = true },
  };

  static readonly List<string> report = new List<string>();

  // Metadata-only alignment after the base gallop's export phase was re-centred. No clip changes.
  public static void CalibrateHorseBankPhases(){
   const string path=Output+"/Prefabs/wildlife_a868c1dd18.prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try{
    var driver=root.GetComponent<OpenWorldImportedModel>();var sampler=root.GetComponentInChildren<Animator>().gameObject;
    var spine=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="BN_Spine_01_03");const int n=96;
    float[] Profile(AnimationClip clip){var y=new float[n];for(int i=0;i<n;i++){clip.SampleAnimation(sampler,i/(float)n*clip.length);y[i]=root.transform.InverseTransformPoint(spine.position).y;}float mean=y.Average();for(int i=0;i<n;i++)y[i]-=mean;return y;}
    var reference=Profile(driver.Run);
    float Offset(AnimationClip bank){var values=Profile(bank);float best=float.PositiveInfinity;int offset=0;for(int shift=0;shift<n;shift++){float error=0;for(int i=0;i<n;i++){float d=reference[i]-values[(i+shift)%n];error+=d*d;}if(error<best){best=error;offset=shift;}}return offset/(float)n;}
    driver.GallopLeftPhaseOffset=Offset(driver.GallopLeft);driver.GallopRightPhaseOffset=Offset(driver.GallopRight);driver.Idle.SampleAnimation(sampler,0);PrefabUtility.SaveAsPrefabAsset(root,path);
    File.AppendAllText("Docs/AssetAudit/ANIMAL_PREPARATION_REPORT.md",$"\nBanked torso phase alignment from 96 spine-height samples: L {driver.GallopLeftPhaseOffset:F4}, R {driver.GallopRightPhaseOffset:F4}. Metadata only; source/prepared clip curves unchanged.\n");
    Debug.Log($"[AnimalPrep] gallop bank phase L {driver.GallopLeftPhaseOffset:F4}, R {driver.GallopRightPhaseOffset:F4}");
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }

  [MenuItem("Lost Realms OW/Content/Prepare Animal Motion (gaits, saddles)")]
  public static void Run() {
   report.Clear();
   report.Add("# Animal motion preparation — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
   report.Add("");
   report.Add("Clip picks per state, natural cycle speeds at the prepared prefab's scale (m/s, the runtime scales them with the spawned size), facing corrections and saddle measurements. Vendor packs were only read.");
   report.Add("");
   EnsureHorsePrefab();
   var manifestText = File.ReadAllText("Assets/Resources/World/ImportedContent.json");
   var manifest = JsonUtility.FromJson<ImportedContentManifest>(manifestText);
   int done = 0;
   foreach (var spec in Specs) {
    var entries = manifest.entries.Where(e => e.source == spec.Source || (spec.Horse && e.source.IndexOf("Horse_001.prefab", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
    if (entries.Length == 0) { report.Add("- SKIPPED " + spec.Source + ": not in the prepared manifest (deferred or not selected)."); continue; }
    foreach (var entry in entries) {
     string path = Output + "/Prefabs/" + entry.key + ".prefab";
     if (!File.Exists(path)) { report.Add("- SKIPPED " + entry.key + ": prepared prefab missing."); continue; }
     try { Process(spec, entry, path); done++; }
     catch (Exception e) { report.Add("- FAILED " + entry.key + " (" + spec.Source + "): " + e.Message); Debug.LogException(e); }
    }
   }
   AssetDatabase.SaveAssets();
   report.Add("");
   report.Add($"Processed {done} prepared animal prefabs.");
   Directory.CreateDirectory("Docs/AssetAudit");
   File.WriteAllLines("Docs/AssetAudit/ANIMAL_PREPARATION_REPORT.md", report);
   Debug.Log("[AnimalPrep] " + done + " animal prefabs prepared; see Docs/AssetAudit/ANIMAL_PREPARATION_REPORT.md");
  }

  // Ensures Assets/GameContent/Horses/Horse_Realistic.fbx and its 2K/1K textures are imported with mobile-ready
  // settings, builds URP materials, and saves Assets/GameContent/Horses/Horse_Realistic.prefab with LOD0/1/2.
  static void EnsureHorsePrefab() {
   const string dir = "Assets/GameContent/Horses";
   const string fbxPath = dir + "/Horse_Realistic.fbx";
   const string prefabPath = dir + "/Horse_Realistic.prefab";
   if (!File.Exists(fbxPath)) return;
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

   void ConfigureTex(string texPath, bool isNormal, bool alphaTransparency, int desktopMax, int androidMax) {
    if (!File.Exists(texPath)) return;
    if (AssetImporter.GetAtPath(texPath) is TextureImporter ti) {
     bool dirty = false;
     var wantType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
     if (ti.textureType != wantType) { ti.textureType = wantType; dirty = true; }
     if (!isNormal && ti.sRGBTexture != true) { ti.sRGBTexture = true; dirty = true; }
     if (ti.alphaIsTransparency != alphaTransparency) { ti.alphaIsTransparency = alphaTransparency; dirty = true; }
     if (ti.maxTextureSize != desktopMax) { ti.maxTextureSize = desktopMax; dirty = true; }
     var android = ti.GetPlatformTextureSettings("Android");
     if (!android.overridden || android.maxTextureSize != androidMax) {
      android.overridden = true;
      android.maxTextureSize = androidMax;
      android.format = TextureImporterFormat.Automatic;
      ti.SetPlatformTextureSettings(android);
      dirty = true;
     }
     if (dirty) ti.SaveAndReimport();
    }
   }

   ConfigureTex(dir + "/Textures/Horse_Body_Albedo_2K.png", false, false, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Pinto_Albedo_2K.png", false, false, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Body_Normal_2K.png", true, false, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Hair_Albedo_2K.png", false, true, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Hair_Normal_2K.png", true, false, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Saddle_Albedo_2K.png", false, false, 2048, 1024);
   ConfigureTex(dir + "/Textures/Horse_Saddle_Normal_2K.png", true, false, 2048, 1024);
   ConfigureTex("Assets/Resources/World/Coats/PintoHorse.png", false, false, 1024, 1024);

   if (AssetImporter.GetAtPath(fbxPath) is ModelImporter mi) {
    bool dirty = false;
    if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; dirty = true; }
    if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
    if (!mi.importAnimation) { mi.importAnimation = true; dirty = true; }
    if (dirty) mi.SaveAndReimport();
   }

   Directory.CreateDirectory(dir + "/Materials");
   var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
   Material MakeMat(string matName, string albPath, string nrmPath, float smoothness, float bumpScale, bool alphaClip) {
    string mPath = dir + "/Materials/" + matName + ".mat";
    var mat = AssetDatabase.LoadAssetAtPath<Material>(mPath);
    if (!mat) { mat = new Material(shader) { name = matName }; AssetDatabase.CreateAsset(mat, mPath); }
    else if (mat.shader != shader) mat.shader = shader;
    var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(albPath);
    var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
    mat.SetColor("_BaseColor", Color.white);
    if (alb) mat.SetTexture("_BaseMap", alb);
    if (nrm) {
     mat.SetTexture("_BumpMap", nrm);
     mat.SetFloat("_BumpScale", bumpScale);
     mat.EnableKeyword("_NORMALMAP");
    }
    mat.SetFloat("_Smoothness", smoothness);
    mat.SetFloat("_Metallic", 0f);
    if (alphaClip) {
     mat.SetFloat("_AlphaClip", 1f);
     mat.SetFloat("_Cutoff", 0.22f);
     mat.SetFloat("_Cull", 0f);
     mat.EnableKeyword("_ALPHATEST_ON");
     mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
    } else {
     mat.SetFloat("_AlphaClip", 0f);
     mat.SetFloat("_Cull", 2f);
     mat.DisableKeyword("_ALPHATEST_ON");
     mat.renderQueue = -1;
    }
    EditorUtility.SetDirty(mat);
    return mat;
   }

   var bodyMat = MakeMat("Horse_Body_Mat", dir + "/Textures/Horse_Body_Albedo_2K.png", dir + "/Textures/Horse_Body_Normal_2K.png", 0.26f, 1.18f, false);
   var hairMat = MakeMat("Horse_Hair_Mat", dir + "/Textures/Horse_Hair_Albedo_2K.png", dir + "/Textures/Horse_Hair_Normal_2K.png", 0.22f, 1.0f, true);
   var saddleMat = MakeMat("Horse_Saddle_Mat", dir + "/Textures/Horse_Saddle_Albedo_2K.png", dir + "/Textures/Horse_Saddle_Normal_2K.png", 0.35f, 1.1f, false);

   var fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
   if (!fbxModel) return;
   var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
   PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
   inst.name = "Horse_Realistic";
   try {
    var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
    var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
    if (avatar) anim.avatar = avatar;
    anim.applyRootMotion = false;
    var lod0 = new List<Renderer>();
    var lod1 = new List<Renderer>();
    var lod2 = new List<Renderer>();
    foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) {
     string n = r.name;
     if (n.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0) r.sharedMaterial = bodyMat;
     else if (n.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) r.sharedMaterial = hairMat;
     else if (n.IndexOf("Saddle", StringComparison.OrdinalIgnoreCase) >= 0) r.sharedMaterial = saddleMat;
     if (n.EndsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) lod0.Add(r);
     else if (n.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase)) lod1.Add(r);
     else if (n.EndsWith("_LOD2", StringComparison.OrdinalIgnoreCase)) lod2.Add(r);
    }
    if (lod0.Count > 0 && lod1.Count > 0 && lod2.Count > 0) {
     var lg = inst.GetComponent<LODGroup>();
     if (!lg) lg = inst.AddComponent<LODGroup>();
     lg.SetLODs(new[] {
      new LOD(0.32f, lod0.ToArray()),
      new LOD(0.12f, lod1.ToArray()),
      new LOD(0.01f, lod2.ToArray())
     });
     lg.RecalculateBounds();
    }
    PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
   } finally {
    UnityEngine.Object.DestroyImmediate(inst);
   }
  }

  static void Process(Spec spec, ImportedContentEntry entry, string path) {
   var root = PrefabUtility.LoadPrefabContents(path);
   try {
    var driver = root.GetComponent<OpenWorldImportedModel>();
    if (!driver) throw new InvalidOperationException("no OpenWorldImportedModel on the prepared prefab");
    if (spec.Horse && File.Exists(spec.Source)) {
     var horseSrc = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Source);
     if (horseSrc) {
      for (int i = root.transform.childCount - 1; i >= 0; i--)
       UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
      var newModel = (GameObject)PrefabUtility.InstantiatePrefab(horseSrc, root.transform);
      PrefabUtility.UnpackPrefabInstance(newModel, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
      newModel.name = "Horse_Realistic";
      newModel.transform.localPosition = Vector3.zero;
      newModel.transform.localRotation = Quaternion.identity;
      newModel.transform.localScale = Vector3.one;
      if (!newModel.GetComponent<Animator>()) newModel.AddComponent<Animator>();
      Recenter(root.transform, newModel.transform);
     }
    }
    var animator = root.GetComponentInChildren<Animator>(true);
    var legacyAnim = root.GetComponentInChildren<Animation>(true);
    Transform bindingRoot = animator ? animator.transform : legacyAnim ? legacyAnim.transform : root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
    var model = root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
    var library = Library(spec.Source, bindingRoot);
    var lines = new List<string>();
    AnimationClip Find(string name, string slot) {
     if (string.IsNullOrEmpty(name)) return null;
     var c = library.FirstOrDefault(x => string.Equals(x.clip.name, name, StringComparison.OrdinalIgnoreCase) || x.clip.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
     if (c.clip == null) { lines.Add($"  - {slot}: '{name}' not found or not compatible with the rig"); return null; }
     return c.clip;
    }
    var idle = Find(spec.Idle, "idle") ?? driver.Idle;
    var walk = Find(spec.Walk, "walk") ?? driver.Walk;
    var trot = Find(spec.Trot, "trot");
    var canter = Find(spec.Canter, "canter");
    var run = Find(spec.Run, "run") ?? driver.Run;
    var attack = Find(spec.Attack, "attack") ?? driver.Attack;
    var attack2 = Find(spec.Attack2, "attack2");
    var hit = Find(spec.Hit, "hit") ?? driver.Hit;
    var death = Find(spec.Death, "death") ?? driver.Death;
    var graze = Find(spec.Graze, "graze");
    var rest = Find(spec.Rest, "rest");
    var roar = Find(spec.Roar, "roar");
    var fly = Find(spec.Fly, "fly");
    // Re-prepared clips carry a "c" so they never collide with the first pass's copies.
    string k = entry.key;
    var picked = new Dictionary<string, string>();
    var baked = new Dictionary<string, float>(); // m/s of travel a cycle had baked in (before it was stripped)
    AnimationClip Clean(AnimationClip c, string slot, bool loop, bool stripTravel) {
     if (!c) return null;
     picked[slot] = c.name;
     var copy = CleanClip(c, k + "_c_" + slot, loop, stripTravel, bindingRoot, lines, out float travelled);
     if (loop && travelled > 0f) baked[slot] = travelled / Mathf.Max(0.05f, c.length);
     return copy;
    }
    driver.Idle = Clean(idle, "idle", true, true);
    driver.Walk = Clean(walk, "walk", true, true);
    driver.Trot = Clean(trot, "trot", true, true);
    driver.Canter = Clean(canter, "canter", true, true);
    driver.Run = Clean(run, "run", true, true);
    // One-shots keep their motion but not a net displacement (the game moves the body; a lunge baked into
    // an attack would snap back when the next state starts). Deaths keep theirs: the body stays down.
    driver.Attack = Clean(attack, "attack", false, true);
    driver.Attack2 = Clean(attack2, "attack2", false, true);
    driver.Hit = Clean(hit, "hit", false, true);
    driver.Death = Clean(death, "death", false, false);
    driver.Graze = Clean(graze, "graze", true, true);
    driver.Rest = Clean(rest, "rest", true, true);
    driver.Roar = Clean(roar, "roar", false, true);
    driver.Fly = Clean(fly, "fly", true, true);
    driver.WalkLeft=Clean(Find(spec.WalkLeft,"walk_l"),"walk_l",true,true);
    driver.WalkRight=Clean(Find(spec.WalkRight,"walk_r"),"walk_r",true,true);
    driver.GallopLeft=Clean(Find(spec.GallopLeft,"gallop_l"),"gallop_l",true,true);
    driver.GallopRight=Clean(Find(spec.GallopRight,"gallop_r"),"gallop_r",true,true);
    driver.RunStop=Clean(Find(spec.RunStop,"runstop"),"runstop",false,true);
    driver.Buck=Clean(Find(spec.Buck,"buck"),"buck",false,true);
    driver.Swim=Clean(Find(spec.Swim,"swim"),"swim",true,true);
    driver.Jump=Clean(Find(spec.Jump,"jump"),"jump",false,true);
    if (animator) { animator.runtimeAnimatorController = null; animator.applyRootMotion = false; }

    // Facing and natural speeds, measured on the cleaned cycles.
    var sampler = animator ? animator.gameObject : bindingRoot.gameObject;
    float height = HeightOf(root.transform);
    float walkSpeed = 0f, trotSpeed = 0f, canterSpeed = 0f, runSpeed = 0f;
    Vector3 travel = Vector3.zero;
    if (driver.Walk && !spec.Bird) walkSpeed = MeasureGait(root.transform, sampler, driver.Walk, height, out travel);
    float facing = travel.sqrMagnitude > 0.01f ? Vector3.SignedAngle(Vector3.forward, travel, Vector3.up) : 0f;
    if (!spec.Bird) lines.Add($"  - facing check: walk cycle travels {facing:0} deg from +Z, toes point {ToeAngle(root.transform, sampler, driver.Idle ? driver.Idle : driver.Walk, height):0} deg");
    if (spec.Turn.HasValue) {
     // Set for this pack: the turn is absolute, so preparing again never turns the model twice.
     var pivot = FacingPivot(root.transform, model);
     pivot.localRotation = Quaternion.AngleAxis(spec.Turn.Value, Vector3.up);
     Recenter(root.transform, pivot);
     lines.Add($"  - facing: model turned by {spec.Turn.Value:0} deg (set for this pack)");
     if (driver.Walk && !spec.Bird) walkSpeed = MeasureGait(root.transform, sampler, driver.Walk, height, out travel);
    } else if (Mathf.Abs(facing) > 35f) {
     // The vendor model walks sideways or backwards in its own frame: turn it (through a pivot, so no clip
     // curve on the model's own transform can undo it) to walk along +Z.
     float snap = Mathf.Round(facing / 90f) * 90f;
     var pivot = FacingPivot(root.transform, model);
     pivot.localRotation = Quaternion.AngleAxis(-snap, Vector3.up) * pivot.localRotation;
     Recenter(root.transform, pivot);
     lines.Add($"  - facing: the walk cycle travels {facing:0} deg from +Z; model turned by {-snap:0} deg");
     walkSpeed = MeasureGait(root.transform, sampler, driver.Walk, height, out travel);
    }
    if (driver.Trot && !spec.Bird) trotSpeed = MeasureGait(root.transform, sampler, driver.Trot, height, out _);
    if (driver.Canter && !spec.Bird) canterSpeed = MeasureGait(root.transform, sampler, driver.Canter, height, out _);
    if (driver.Run && !spec.Bird) runSpeed = driver.Run == driver.Walk ? walkSpeed : MeasureGait(root.transform, sampler, driver.Run, height, out _);
    // A cycle with baked travel tells its own speed exactly; otherwise the planted-foot measurement is
    // checked against a Froude-number estimate from the leg length (walk Fr ~0.25, trot Fr ~1, canter Fr ~2, gallop Fr ~3.5).
    int feet = driver.Walk ? CountFeet(sampler, driver.Walk, height) : 0;
    float leg = height * (feet >= 4 ? 0.5f : 0.42f);
    float froudeWalk = Mathf.Sqrt(0.25f * 9.81f * leg);
    float froudeTrot = Mathf.Sqrt(1.0f * 9.81f * leg);
    float froudeCanter = Mathf.Sqrt(2.0f * 9.81f * leg);
    float froudeRun = Mathf.Sqrt((spec.Horse && driver.Canter ? 6.5f : 2.0f) * 9.81f * leg);
    float Pick(string slot, float measured, float froude) {
     if (baked.TryGetValue(slot, out float b) && b > 0.02f) { lines.Add($"  - {slot}: speed from the cycle's baked travel {b:0.00} m/s (planted feet said {measured:0.00})"); return b; }
     if (measured > 0.02f && measured > froude * 0.45f && measured < froude * 2.4f) return measured;
     lines.Add($"  - {slot}: planted-feet speed {measured:0.00} m/s is implausible for a leg of {leg:0.00} m; using the Froude estimate {froude:0.00}");
     return froude;
    }
    if (!spec.Bird && driver.Walk) walkSpeed = Pick("walk", walkSpeed, froudeWalk);
    if (!spec.Bird && driver.Trot) trotSpeed = Pick("trot", trotSpeed, froudeTrot);
    if (!spec.Bird && driver.Canter) canterSpeed = Pick("canter", canterSpeed, froudeCanter);
    if (!spec.Bird && driver.Run) runSpeed = driver.Run == driver.Walk ? walkSpeed : Pick("run", runSpeed, froudeRun);
    if (spec.Bird) { walkSpeed = baked.TryGetValue("walk", out float hop) && hop > 0.02f ? hop : 0.35f * height / 0.2f; runSpeed = walkSpeed; }
    driver.WalkSpeed = walkSpeed; driver.TrotSpeed = trotSpeed; driver.CanterSpeed = canterSpeed; driver.RunSpeed = runSpeed;
    driver.TuckTime = driver.Death || !driver.Run ? -1f : TuckTime(root.transform, sampler, driver.Run);
    if (idle) idle.SampleAnimation(sampler, 0f);

    if (!string.IsNullOrEmpty(spec.CoatName)) MakeCoat(root.transform, spec.CoatName, spec.CoatKind, lines);

    var rig = root.GetComponent<OpenWorldMountRig>();
    if (spec.Horse) {
     if (!rig) rig = root.AddComponent<OpenWorldMountRig>();
     if (driver.Idle) driver.Idle.SampleAnimation(sampler, 0f);
     MeasureSaddle(root.transform, rig, lines);
     var toes=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="BN_L_Toe_042_0"||t.name=="BN_R_Toe_047_0"||t.name=="BN_L_Toe_2_055_0"||t.name=="BN_R_Toe_2_059_0").ToArray();
     driver.HasBankContactHeights=toes.Length==4&&driver.WalkLeft&&driver.WalkRight&&driver.GallopLeft&&driver.GallopRight;
     Vector4 ContactHeights(AnimationClip clip){
      var low=Vector4.one*float.MaxValue;
      for(int frame=0;frame<96;frame++){clip.SampleAnimation(sampler,frame/96f*clip.length);for(int f=0;f<4;f++)low[f]=Mathf.Min(low[f],root.transform.InverseTransformPoint(toes[f].position).y);}
      return low;
     }
     if(driver.HasBankContactHeights){
      driver.WalkLeftContactY=ContactHeights(driver.WalkLeft);driver.WalkRightContactY=ContactHeights(driver.WalkRight);
      driver.GallopLeftContactY=ContactHeights(driver.GallopLeft);driver.GallopRightContactY=ContactHeights(driver.GallopRight);
      lines.Add("  - banked toe contact heights at prefab scale (hierarchy order): walk L "+driver.WalkLeftContactY.ToString("F3")+", walk R "+driver.WalkRightContactY.ToString("F3")+", gallop L "+driver.GallopLeftContactY.ToString("F3")+", gallop R "+driver.GallopRightContactY.ToString("F3"));
      var gallopLow=ContactHeights(driver.Run);driver.GallopContactPhases=new Vector4[96];
      for(int frame=0;frame<96;frame++){
       driver.Run.SampleAnimation(sampler,frame/96f*driver.Run.length);var contacts=Vector4.zero;
       for(int f=0;f<4;f++)contacts[f]=root.transform.InverseTransformPoint(toes[f].position).y<=gallopLow[f]+height*.03f?1:0;
       driver.GallopContactPhases[frame]=contacts;
      }
      lines.Add("  - banked gallop: authored torso overlay over corrected straight-gallop legs; 96 calibrated contact-phase samples, shared natural speed and normalized phase.");
      driver.Idle.SampleAnimation(sampler,0);
     }
    } else if (rig) UnityEngine.Object.DestroyImmediate(rig);

    PrefabUtility.SaveAsPrefabAsset(root, path);
    string N(string slot) => picked.TryGetValue(slot, out var name) ? name : "-";
    string equineExtra = driver.Trot || driver.Canter ? $", trot {trotSpeed:0.00} m/s ({N("trot")}), canter {canterSpeed:0.00} m/s ({N("canter")})" : "";
    report.Add($"- {k} ({Path.GetFileNameWithoutExtension(spec.Source)}, {entry.category}, height {height:0.00} m at prefab scale): walk {walkSpeed:0.00} m/s, run {runSpeed:0.00} m/s{equineExtra}, tuck {driver.TuckTime:0.00}");
    report.Add($"  - clips: idle {N("idle")}, walk {N("walk")}, trot {N("trot")}, canter {N("canter")}, run {N("run")}, attack {N("attack")}, attack2 {N("attack2")}, hit {N("hit")}, death {N("death")}, graze {N("graze")}, rest {N("rest")}, roar {N("roar")}, fly {N("fly")}");
    report.AddRange(lines);
    if(spec.Horse)report.Add($"  - extra horse clips: walk_l {N("walk_l")}, walk_r {N("walk_r")}, gallop_l {N("gallop_l")}, gallop_r {N("gallop_r")}, runstop {N("runstop")}, buck {N("buck")}, swim {N("swim")}, jump {N("jump")}; banked cycles share base gait speeds and phase.");
    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
     foreach (var m in r.sharedMaterials)
      if (m) report.Add($"  - material {m.name} [{(m.shader ? m.shader.name : "none")}] base {(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name + " " + m.GetTexture("_BaseMap").width : "none")}, normal {(m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") ? m.GetTexture("_BumpMap").name : "none")}");
   } finally { PrefabUtility.UnloadPrefabContents(root); }
  }

  struct LibClip { public AnimationClip clip; public float coverage; }

  // Clips of the source's own pack that bind to this rig (at least 70% of the curves; missing bones are
  // ignored at runtime, which is what lets the small birds use the shared flight cycle).
  static List<LibClip> Library(string source, Transform bindingRoot) {
   var parts = source.Split('/');
   string searchFolder = parts.Length > 2 && parts[0] == "Assets" && parts[1] == "GameContent" ? "Assets/" + parts[1] + "/" + parts[2] : "Assets/" + parts[1];
   var clips = new List<AnimationClip>();
   var src = AssetDatabase.LoadAssetAtPath<GameObject>(source);
   if (src) clips.AddRange(AnimationUtility.GetAnimationClips(src));
   clips.AddRange(AssetDatabase.FindAssets("t:AnimationClip t:Model", new[] { searchFolder })
    .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
    .Where(c => !c.name.StartsWith("__preview__")));
   var result = new List<LibClip>();
   foreach (var c in clips.Distinct()) {
    if (c.humanMotion) continue;
    var b = AnimationUtility.GetCurveBindings(c);
    if (b.Length < 6) continue;
    int ok = b.Count(x => string.IsNullOrEmpty(x.path) || bindingRoot.Find(x.path));
    float cov = (float)ok / b.Length;
    if (cov >= 0.7f) result.Add(new LibClip { clip = c, coverage = cov });
   }
   // Prefer fully binding clips, then the longest (the .anim and FBX copies of a clip are equivalent).
   return result.OrderByDescending(x => x.coverage).ThenBy(x => AssetDatabase.GetAssetPath(x.clip).EndsWith(".anim") ? 1 : 0).ToList();
  }

  static AnimationClip CleanClip(AnimationClip source, string key, bool loop, bool stripTravel, Transform bindingRoot, List<string> lines, out float travelled) {
   travelled = 0f;
   string path = Output + "/Animations/" + key + ".anim";
   var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
   if (!copy) { copy = UnityEngine.Object.Instantiate(source); copy.name = key; AssetDatabase.CreateAsset(copy, path); }
   else { if (source != copy) EditorUtility.CopySerialized(source, copy); copy.name = key; }
   foreach (var binding in AnimationUtility.GetCurveBindings(copy)) if (binding.path.EndsWith("/Bip01 Footsteps")) AnimationUtility.SetEditorCurve(copy, binding, null);
   AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
   var settings = AnimationUtility.GetAnimationClipSettings(copy);
   settings.loopTime = loop;
   AnimationUtility.SetAnimationClipSettings(copy, settings);
   if (stripTravel) travelled = StripTravel(copy, bindingRoot, key, lines);
   EditorUtility.SetDirty(copy);
   return copy;
  }

  // Removes the net travel of the top animated bone over a looped cycle (linear trend on its local position
  // curves), keeping the bob and sway. Cycles authored in place are left alone.
  // Returns the largest net travel removed (metres at prefab scale).
  static float StripTravel(AnimationClip clip, Transform bindingRoot, string key, List<string> lines) {
   float most = 0f;
   var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("m_LocalPosition.") && b.type == typeof(Transform)).ToArray();
   if (bindings.Length == 0) return 0f;
   int minDepth = bindings.Min(b => string.IsNullOrEmpty(b.path) ? 0 : b.path.Count(ch => ch == '/') + 1);
   float len = clip.length;
   if (len <= 0.05f) return 0f;
   float height = HeightOf(bindingRoot);
   foreach (var group in bindings.GroupBy(b => b.path)) {
    int depth = string.IsNullOrEmpty(group.Key) ? 0 : group.Key.Count(ch => ch == '/') + 1;
    if (depth > minDepth + 1) continue;
    var target = string.IsNullOrEmpty(group.Key) ? bindingRoot : bindingRoot.Find(group.Key);
    float parentScale = target && target.parent ? target.parent.lossyScale.x : 1f;
    foreach (var b in group) {
     var curve = AnimationUtility.GetEditorCurve(clip, b);
     if (curve == null || curve.length < 2) continue;
     float drift = curve.Evaluate(len) - curve.Evaluate(0f);
     // Only real travel (more than 4% of the body height over the cycle) is removed.
     if (Mathf.Abs(drift * parentScale) < 0.04f * Mathf.Max(0.05f, height)) continue;
     float slope = drift / len;
     var keys = curve.keys;
     for (int i = 0; i < keys.Length; i++) { keys[i].value -= slope * keys[i].time; keys[i].inTangent -= slope; keys[i].outTangent -= slope; }
     curve.keys = keys;
     AnimationUtility.SetEditorCurve(clip, b, curve);
     most = Mathf.Max(most, Mathf.Abs(drift * parentScale));
     lines.Add($"  - {key}: removed {drift * parentScale:0.00} m of baked travel from {(string.IsNullOrEmpty(b.path) ? "<root>" : b.path.Split('/').Last())}.{b.propertyName.Substring(16)}");
    }
   }
   return most;
  }

  static float HeightOf(Transform t) {
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(t);
   return Mathf.Max(0.01f, b.size.y);
  }

  static Transform FacingPivot(Transform root, Transform model) {
   var pivot = root.Find("Facing");
   if (pivot) return pivot;
   pivot = new GameObject("Facing").transform;
   pivot.SetParent(root, false);
   model.SetParent(pivot, true);
   return pivot;
  }

  static void Recenter(Transform root, Transform model) {
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(root);
   model.position += new Vector3(root.position.x - b.center.x, 0f, root.position.z - b.center.z);
  }

  // Skeleton: every bone any skinned mesh of the model uses.
  static HashSet<Transform> Bones(GameObject sampler) {
   var set = new HashSet<Transform>();
   foreach (var smr in sampler.GetComponentsInChildren<SkinnedMeshRenderer>(true)) foreach (var b in smr.bones) if (b) set.Add(b);
   return set;
  }

  // Feet: the lowest end bones of the skeleton in the current pose.
  static List<Transform> Feet(GameObject sampler, float height) {
   var bones = Bones(sampler);
   var leaves = bones.Where(t => !t.Cast<Transform>().Any(c => bones.Contains(c))).ToList();
   if (leaves.Count == 0) return leaves;
   float floor = leaves.Min(t => t.position.y);
   return leaves.Where(t => t.position.y < floor + 0.1f * height).ToList();
  }

  // Body reference: the deepest bone that still carries nearly the whole skeleton (pelvis / root bone).
  static Transform Body(GameObject sampler) {
   var bones = Bones(sampler);
   if (bones.Count == 0) return sampler.transform;
   int Under(Transform t) => t.GetComponentsInChildren<Transform>(true).Count(x => bones.Contains(x));
   var top = bones.OrderByDescending(Under).First();
   int total = Under(top);
   var cur = top;
   while (true) {
    Transform next = null;
    foreach (Transform c in cur) if (Under(c) >= total * 0.8f) { next = c; break; }
    if (!next) break;
    cur = next;
   }
   return cur;
  }

  // Natural ground speed of a cycle: while a foot is planted it moves backwards relative to the body at the
  // speed the body travels. Each foot's planted spans (within 3% of the body height of that foot's own
  // lowest point) give a backward displacement over a duration; the median of those speeds is robust to
  // the swing phase and to hooves rolling over. Also returns the travel direction in the prefab frame.
  static float MeasureGait(Transform root, GameObject sampler, AnimationClip clip, float height, out Vector3 travel) {
   travel = Vector3.zero;
   clip.SampleAnimation(sampler, 0f);
   var feet = Feet(sampler, height);
   var body = Body(sampler);
   if (feet.Count == 0 || !body) return 0f;
   int n = Mathf.Clamp(Mathf.RoundToInt(clip.length * 120f), 32, 360);
   float dt = clip.length / n;
   var rel = new Vector3[feet.Count, n + 1];
   var y = new float[feet.Count, n + 1];
   for (int s = 0; s <= n; s++) {
    clip.SampleAnimation(sampler, s * dt);
    Vector3 bp = root.InverseTransformPoint(body.position);
    for (int f = 0; f < feet.Count; f++) {
     Vector3 fp = root.InverseTransformPoint(feet[f].position);
     rel[f, s] = fp - bp; y[f, s] = fp.y;
    }
   }
   var spans = new List<(Vector3 disp, float dur)>();
   for (int f = 0; f < feet.Count; f++) {
    float low = float.MaxValue, high = float.MinValue;
    for (int s = 0; s <= n; s++) { low = Mathf.Min(low, y[f, s]); high = Mathf.Max(high, y[f, s]); }
    if (high - low < 0.02f * height) continue;              // never lifts: not a foot (or a dragging tail)
    float limit = low + 0.03f * height;
    int start = -1;
    for (int s = 0; s <= n + 1; s++) {
     bool planted = s <= n && y[f, s] < limit;
     if (planted && start < 0) start = s;
     if (!planted && start >= 0) {
      int end = s - 1;
      if (end - start >= Mathf.Max(2, n / 14)) {
       Vector3 d = rel[f, end] - rel[f, start]; d.y = 0f;
       spans.Add((d, (end - start) * dt));
      }
      start = -1;
     }
    }
   }
   if (spans.Count == 0) return 0f;
   Vector3 sum = Vector3.zero;
   foreach (var sp in spans) sum += sp.disp;
   if (sum.sqrMagnitude < 1e-8f) return 0f;
   Vector3 back = sum.normalized;
   travel = -back;
   var speeds = spans.Select(sp => Vector3.Dot(sp.disp, back) / sp.dur).Where(v => v > 0f).OrderBy(v => v).ToList();
   return speeds.Count == 0 ? 0f : speeds[speeds.Count / 2];
  }

  // Which way the toes point (ankle to toe tip of each foot, in the prefab frame) in a pose: a second
  // opinion on the facing, for the report.
  static float ToeAngle(Transform root, GameObject sampler, AnimationClip clip, float height) {
   if (!clip) return 0f;
   clip.SampleAnimation(sampler, 0f);
   Vector3 sum = Vector3.zero;
   foreach (var toe in Feet(sampler, height)) {
    if (!toe.parent) continue;
    Vector3 d = root.InverseTransformPoint(toe.position) - root.InverseTransformPoint(toe.parent.position); d.y = 0f;
    if (d.sqrMagnitude > 1e-6f) sum += d.normalized;
   }
   return sum.sqrMagnitude > 0.01f ? Vector3.SignedAngle(Vector3.forward, sum, Vector3.up) : 0f;
  }

  // A coat variant from the pack's own albedo. "frost": the fur's light and shade lifted to a cool cream
  // white, the darkest features (nose, eyes, claws) kept. Saved under Resources so the population can
  // swap it onto individual animals with a property block (the prepared prefab itself is unchanged).
  static void MakeCoat(Transform root, string name, string kind, List<string> lines) {
   var body = root.GetComponentsInChildren<Renderer>(true).OrderByDescending(r => r is SkinnedMeshRenderer s && s.sharedMesh ? s.sharedMesh.vertexCount : 0).FirstOrDefault();
   var material = body ? body.sharedMaterial : null;
   var source = material && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
   if (!source) { lines.Add($"  - coat {name}: no base map to start from"); return; }
   int w = Mathf.Min(1024, source.width), h = Mathf.Min(1024, source.height);
   var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
   var previous = RenderTexture.active;
   Graphics.Blit(source, rt);
   RenderTexture.active = rt;
   var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
   tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
   RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
   var px = tex.GetPixels();
   double sum = 0; int count = 0;
   foreach (var c in px) { float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f; if (l > 0.03f) { sum += l; count++; } }
   float mean = count > 0 ? (float)(sum / count) : 0.35f;
   var cream = kind == "frost" ? new Color(0.93f, 0.92f, 0.9f) : new Color(0.8f, 0.7f, 0.55f);
   for (int i = 0; i < px.Length; i++) {
    var c = px[i];
    float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
    float shade = Mathf.InverseLerp(0.45f, 1.4f, Mathf.Clamp(l / Mathf.Max(0.05f, mean), 0.45f, 1.4f));
    var pale = cream * Mathf.Lerp(0.7f, 1.05f, shade);
    pale.b = Mathf.Min(1f, pale.b * 1.03f);
    float keep = 1f - Mathf.InverseLerp(0.3f * mean, 0.55f * mean, l);
    var o = Color.Lerp(pale, c, keep); o.a = c.a;
    px[i] = o;
   }
   tex.SetPixels(px); tex.Apply();
   const string folder = "Assets/Resources/World/Coats";
   Directory.CreateDirectory(folder);
   string path = folder + "/" + name + ".png";
   File.WriteAllBytes(path, tex.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(tex);
   AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
   if (AssetImporter.GetAtPath(path) is TextureImporter importer) {
    importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
    importer.textureCompression = TextureImporterCompression.Compressed; importer.alphaSource = TextureImporterAlphaSource.FromInput;
    importer.SaveAndReimport();
   }
   lines.Add($"  - coat {name} ({kind}) made from {source.name} ({w}x{h}), saved to {path}");
  }

  static int CountFeet(GameObject sampler, AnimationClip clip, float height) {
   clip.SampleAnimation(sampler, 0f);
   return Feet(sampler, height).Count;
  }

  // Normalised time of the run cycle where the feet are closest to the body (legs gathered).
  static float TuckTime(Transform root, GameObject sampler, AnimationClip clip) {
   float height = HeightOf(root);
   clip.SampleAnimation(sampler, 0f);
   var feet = Feet(sampler, height);
   var body = Body(sampler);
   if (feet.Count == 0) return -1f;
   int n = 32; float best = float.MaxValue, bestT = 0f;
   for (int s = 0; s < n; s++) {
    float t = s / (float)n;
    clip.SampleAnimation(sampler, t * clip.length);
    float d = 0f;
    foreach (var f in feet) d += (f.position - body.position).magnitude;
    if (d < best) { best = d; bestT = t; }
   }
   return bestT;
  }

  // Seat point on the back (lowest point of the topline between withers and croup), the bone that carries
  // it, the barrel's half width at knee height and the bit at the mouth, from the skinned mesh in its idle
  // pose (computed with the bone matrices directly, so no BakeMesh scale conventions are involved).
  static void MeasureSaddle(Transform root, OpenWorldMountRig rig, List<string> lines) {
   var smr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
    .Where(r => r.sharedMesh && r.name.IndexOf("Saddle", StringComparison.OrdinalIgnoreCase) < 0 && r.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) < 0)
    .OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault()
    ?? root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh ? r.sharedMesh.vertexCount : 0).FirstOrDefault();
   if (!smr || !smr.sharedMesh) { lines.Add("  - saddle: no skinned mesh"); return; }
   var mesh = smr.sharedMesh;
   var verts = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes; var bones = smr.bones;
   if (weights.Length != verts.Length || bones.Length == 0) { lines.Add("  - saddle: mesh has no bone weights"); return; }
   var skin = new Matrix4x4[bones.Length];
   for (int i = 0; i < bones.Length; i++) skin[i] = bones[i] ? bones[i].localToWorldMatrix * bind[i] : smr.transform.localToWorldMatrix;
   var skinned = new Vector3[verts.Length];
   for (int v = 0; v < verts.Length; v++) {
    var w = weights[v];
    Vector3 world = skin[w.boneIndex0].MultiplyPoint3x4(verts[v]) * w.weight0 + skin[w.boneIndex1].MultiplyPoint3x4(verts[v]) * w.weight1
     + skin[w.boneIndex2].MultiplyPoint3x4(verts[v]) * w.weight2 + skin[w.boneIndex3].MultiplyPoint3x4(verts[v]) * w.weight3;
    skinned[v] = root.InverseTransformPoint(world);
   }
   // Low-poly bodies have few vertex rings along the back: sample the triangles' surfaces as well, so
   // every slice of the body has points on its top, sides and underside.
   var cloud = new List<Vector3>(skinned);
   var tris = mesh.triangles;
   const int sub = 4;
   for (int t = 0; t + 2 < tris.Length; t += 3) {
    Vector3 a = skinned[tris[t]], b = skinned[tris[t + 1]], c = skinned[tris[t + 2]];
    for (int i = 0; i <= sub; i++) for (int j = 0; j <= sub - i; j++) {
     if ((i == 0 && j == 0) || (i == sub && j == 0) || (i == 0 && j == sub)) continue; // corners are vertices
     cloud.Add(a + (b - a) * (i / (float)sub) + (c - a) * (j / (float)sub));
    }
   }
   var p = cloud.ToArray();
   // Bone weights only exist for real vertices; the carrier search below uses those.
   float zmin = p.Min(q => q.z), zmax = p.Max(q => q.z), ymin = p.Min(q => q.y);
   float L = zmax - zmin;
   float xc = p.Average(q => q.x);
   // Top line along the spine.
   const int bins = 48;
   var top = new float[bins];
   for (int i = 0; i < bins; i++) top[i] = float.MinValue;
   // Side silhouette: the highest point of each slice (low-poly backs often have no vertex on the spine).
   foreach (var q in p) {
    int i = Mathf.Clamp((int)((q.z - zmin) / L * bins), 0, bins - 1);
    top[i] = Mathf.Max(top[i], q.y);
   }
   var profile = new StringBuilder();
   for (int i = 0; i < bins; i += 4) profile.Append(top[i] > float.MinValue ? (top[i] - ymin).ToString("0.00") + " " : "- ");
   lines.Add($"  - back profile, tail to head every {L * 4f / bins:0.00} m (height above the hooves): {profile}");
   int lo = Mathf.FloorToInt(bins * 0.30f), hi = Mathf.CeilToInt(bins * 0.62f);
   int seatBin = -1; float seatY = float.MaxValue;
   for (int i = lo; i <= hi; i++) if (top[i] > float.MinValue && top[i] < seatY) { seatY = top[i]; seatBin = i; }
   if (seatBin < 0) { lines.Add("  - saddle: no back profile found"); return; }
   float seatZ = zmin + (seatBin + 0.5f) / bins * L;
   var seat = new Vector3(xc, seatY, seatZ);
   // Barrel half width a little below the seat (where the knees grip) and the belly under the seat.
   float half = 0f, belly = seatY, drop = seatY - ymin;
   foreach (var q in p) {
    if (Mathf.Abs(q.z - seatZ) > 0.06f * L) continue;
    if (q.y < seatY - 0.45f * drop || q.y > seatY - 0.08f * drop) continue;
    half = Mathf.Max(half, Mathf.Abs(q.x - xc));
   }
   // Underside of the barrel: the lowest point of the slice that is not a leg (legs hang outside the
   // central half of the barrel's width).
   foreach (var q in p) {
    if (Mathf.Abs(q.z - seatZ) > 0.06f * L || Mathf.Abs(q.x - xc) > Mathf.Max(0.04f * L, half * 0.5f)) continue;
    if (q.y < seatY - 0.7f * drop) continue;
    belly = Mathf.Min(belly, q.y);
   }
   // Bit: the front of the muzzle.
   Vector3 mouth = Vector3.zero; int mouthCount = 0;
   foreach (var q in p) if (q.z > zmax - 0.04f * L) { mouth += q; mouthCount++; }
   mouth = mouthCount > 0 ? mouth / mouthCount : new Vector3(xc, seatY, zmax);
   mouth.z = zmax - 0.05f * L; mouth.x = xc;
   // Bones that carry the seat and the bit (largest summed skin weight near each point).
   Transform Carrier(Vector3 point, float radius) {
    var score = new float[bones.Length];
    for (int v = 0; v < skinned.Length; v++) {
     if ((skinned[v] - point).sqrMagnitude > radius * radius) continue;
     var w = weights[v];
     score[w.boneIndex0] += w.weight0; score[w.boneIndex1] += w.weight1; score[w.boneIndex2] += w.weight2; score[w.boneIndex3] += w.weight3;
    }
    int best = 0; for (int i = 1; i < score.Length; i++) if (score[i] > score[best]) best = i;
    return score[best] > 0f ? bones[best] : null;
   }
   var seatBone = Carrier(seat, 0.1f * L) ?? smr.rootBone;
   var bitBone = Carrier(mouth, 0.08f * L) ?? seatBone;
   rig.SaddleBone = seatBone;
   rig.SaddleLocal = seatBone.InverseTransformPoint(root.TransformPoint(seat));
   rig.SaddleRestRotation = Quaternion.Inverse(root.rotation) * seatBone.rotation;
   rig.SeatRest = seat;
   rig.BitBone = bitBone;
   rig.BitLocal = bitBone.InverseTransformPoint(root.TransformPoint(mouth));
   rig.BarrelHalfWidth = half;
   rig.SeatHeight = seatY - ymin;
   rig.BellyDepth = seatY - belly;
   rig.BodyLength = L;
   lines.Add($"  - saddle: seat {seat.y - ymin:0.00} above the hooves at z {seatZ - zmin:0.00} of {L:0.00} (bone {seatBone.name}); barrel half width {half:0.00}; belly {seatY - belly:0.00} below the seat; bit on {bitBone.name}");
  }
 }
}
