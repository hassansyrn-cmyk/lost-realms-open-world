using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LostRealmsOW {
 public static class VerdantSliceValidation {
  const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static OpenWorldSaveData snapshot;
  static Vector3 oldPosition;
  static bool oldBlocked, oldDirty;
  static float oldHealth;
  static float oldHour, oldCameraDistance;
  static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
  static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
  static void Invoke(object obj, string method, params object[] values) => obj.GetType().GetMethod(method, Private).Invoke(obj, values);
  public static void Begin() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero, "World required");
   if (snapshot != null) Cleanup();
   snapshot = gm.CaptureAdventure(); oldPosition = gm.Hero.transform.position; oldHealth = gm.Hero.Health;
   oldHour = OpenWorldAtmosphere.Instance ? OpenWorldAtmosphere.Instance.Hour : 9f;
   oldCameraDistance = gm.CameraRig.Distance;
   if (OpenWorldAtmosphere.Instance) OpenWorldAtmosphere.Instance.SetHour(11f,false,false);
   oldBlocked = (bool)typeof(OpenWorldManager).GetField("saveBlocked", Private).GetValue(gm);
   oldDirty = (bool)typeof(OpenWorldManager).GetField("saveDirty", Private).GetValue(gm);
   Set(gm, "saveBlocked", true); // Test tours must not write the user's adventure slots.
   gm.CollectedWorldIds.RemoveWhere(x => x.StartsWith("verdant-hunt:"));
   var kael = gm.NPCs.Find(n => n && n.NpcId == "kael"); Check(kael,"Kael missing");
   gm.Hero.WarpTo(kael.transform.position + Vector3.back * 2f); gm.Interact();
   Check(gm.VerdantHuntStage == 1,"NPC interaction did not accept quest");
   gm.Hero.WarpTo(gm.VerdantHuntTarget);
   gm.CameraRig?.SnapToTarget();
  }
  public static void NextTrail() {
   var gm = OpenWorldManager.I;
   gm.Hero.WarpTo(gm.VerdantHuntTarget);
   bool hadFirst = gm.CollectedWorldIds.Contains("verdant-hunt:trail1"); gm.Interact();
   Check(hadFirst ? gm.VerdantHuntStage == 2 : gm.CollectedWorldIds.Contains("verdant-hunt:trail1"), "Track interaction failed");
   gm.Hero.WarpTo(gm.VerdantHuntTarget + new Vector3(0, 0, -7));
   gm.CameraRig.Yaw = 0; gm.CameraRig.Pitch = 18; gm.CameraRig.SnapToTarget();
  }
  public static void FinishHunt() {
   var gm = OpenWorldManager.I;
   var quarry = UnityEngine.Object.FindFirstObjectByType<VerdantHuntQuarry>();
   Check(quarry, "Prepared bear did not spawn");
   var enemy = quarry.GetComponent<OpenWorldEnemy>();
   Check(enemy.Beast && enemy.MeatDrop == 3 && enemy.Visual.ModelInstance, "Quarry setup invalid");
   enemy.TakeHit(1000f, gm.Hero.transform.position, true);
   Check(gm.VerdantHuntStage == 3, "Quest did not observe quarry defeat");
   gm.Hero.WarpTo(gm.VerdantHuntTarget + new Vector3(0, 0, -2));
  }
  public static void CookAndReward() {
   var gm = OpenWorldManager.I;
   Check(gm.VerdantHuntStage == 3 && gm.RawMeat >= 1, "Meat/quest stage missing");
   var wrong = gm.Campfires.Find(f => f.CampfireTitle != "OAKVALE CAMPFIRE");
   gm.RecordVerdantMeal(wrong); Check(gm.VerdantHuntStage == 3, "Wrong campfire advanced quest");
   var fire = gm.Campfires.Find(f => f.CampfireTitle == "OAKVALE CAMPFIRE");
   int meat = gm.RawMeat; gm.Hero.WarpTo(fire.transform.position);
   Check(gm.InteractionPrompt.StartsWith("COOK OAKVALE"),"NPC blocked quest cooking prompt"); gm.Interact();
   Check(gm.RawMeat == meat - 1 && gm.VerdantHuntStage == 4, "Cooking did not advance quest/cost meat");
   var saved = JsonUtility.FromJson<OpenWorldSaveData>(JsonUtility.ToJson(gm.CaptureAdventure()));
   Check(saved.collected.Contains("verdant-hunt:cooked") && saved.collected.Contains("verdant-hunt:slain"), "Quest flags missing from snapshot");
   int gold = gm.Gold, gems = gm.Gems;
   gm.Hero.WarpTo(gm.VerdantHuntTarget + Vector3.back * 2f); gm.Interact();
   Check(gm.VerdantHuntStage == 5 && gm.Gold == gold + 120 && gm.Gems == gems + 5 && gm.UnlockedWeapons.Contains(4), "Reward failed");
   gm.Interact(); Check(gm.Gold == gold + 120 && gm.Gems == gems + 5, "Duplicate reward");
   var store = new OpenWorldSaveStore(Path.GetFullPath("Temp/VerdantValidation/QuestSave_" + DateTime.UtcNow.Ticks));
   store.Write(gm.CaptureAdventure()); var restored = store.Load();
   Check(restored.collected.Contains("verdant-hunt:reward") && restored.weapons.Contains(4),"Quest reward did not survive disk save/load");
   File.AppendAllText("Docs/verdant-slice-validation.txt", "PASS: accept, two tracks, prepared bear spawn, real quarry defeat/meat, correct-fire cooking/cost, saved quest flags, one-time gold/gem/weapon reward.\n");
  }
  public static void VerifyCombat() {
   var gm = OpenWorldManager.I; var hero = gm.Hero;
   Vector3 start = hero.transform.position; float health = hero.Health;
   var enemy = OpenWorldEnemy.Spawn("Goblin", "Validation enemy", gm.HuntGround(new Vector3(25,0,-25)), gm.WorldRoot);
   GameObject wall = null;
   try {
    Vector3 center = enemy.transform.position;
    enemy.transform.rotation = Quaternion.identity;
    hero.WarpTo(center + Vector3.forward * 1.5f);
    Set(hero, "invulnerableUntil", -1f); Set(hero, "parryWindowUntil", -1f); hero.Health = hero.MaxHealth;
    Invoke(enemy, "BeginWindup"); Invoke(enemy, "PerformStrike", hero);
    Check(hero.Health < hero.MaxHealth, "Forward melee missed");
    hero.Health = hero.MaxHealth; Set(hero, "invulnerableUntil", -1f);
    Set(enemy, "bossSwingCount", 0); Invoke(enemy, "BeginWindup"); hero.WarpTo(center - Vector3.forward * 1.5f);
    Invoke(enemy, "PerformStrike", hero); Check(hero.Health == hero.MaxHealth, "Melee hit behind committed facing");
    hero.WarpTo(center + Vector3.forward * 1.5f); Set(enemy, "bossSwingCount", 0); Invoke(enemy, "BeginWindup");
    wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = center + Vector3.forward * .75f + Vector3.up;
    wall.transform.localScale = new Vector3(2,2,.2f); Physics.SyncTransforms();
    Invoke(enemy, "PerformStrike", hero); Check(hero.Health == hero.MaxHealth, "Melee passed through wall");
    wall.SetActive(false);
    enemy.Role = "Elite"; Set(enemy, "bossSwingCount", 1); Invoke(enemy, "BeginWindup");
    Vector3 mark = (Vector3)typeof(OpenWorldEnemy).GetField("strikeCenter", Private).GetValue(enemy);
    hero.WarpTo(center + Vector3.right * 7f); Invoke(enemy, "PerformStrike", hero);
    Check(hero.Health == hero.MaxHealth && (Vector3)typeof(OpenWorldEnemy).GetField("strikeCenter", Private).GetValue(enemy) == mark, "Special retargeted after warning");
    enemy.Beast = true; enemy.DisplayName = "Grey Wolf"; Set(enemy,"bossSwingCount",1);
    hero.WarpTo(center + Vector3.forward * 1.5f); Invoke(enemy,"BeginWindup"); Invoke(enemy,"PerformStrike",hero);
    Check(hero.Health == hero.MaxHealth && (bool)typeof(OpenWorldEnemy).GetField("awaitingImpact",Private).GetValue(enemy), "Lunge hit before arrival");
    enemy.Stun(1f); Check(!(bool)typeof(OpenWorldEnemy).GetField("awaitingImpact",Private).GetValue(enemy), "Stun did not cancel pending impact");
    enemy.Beast = false; Invoke(enemy,"BeginWindup");
    Set(hero,"parryWindowUntil",Time.time + 1f); hero.TakeDamage(1f,enemy.transform);
    Check(hero.Health == hero.MaxHealth && !enemy.Telegraphing, "Parry did not cancel attack");
    Set(hero,"parryWindowUntil",-1f); Set(hero,"invulnerableUntil",Time.time + 1f); hero.TakeDamage(1f,enemy.transform);
    Check(hero.Health == hero.MaxHealth,"Dodge invulnerability failed");
    var b = new Bounds(Vector3.zero,new Vector3(1,1,10));
    Check(Mathf.Abs(OpenWorldWeaponCatalog.GripOffset(1,b).z + 4.2f) < .01f,"Horizontal weapon grip did not use its length axis");
    File.AppendAllText("Docs/verdant-slice-validation.txt", "PASS: forward melee, committed facing/back dodge, wall occlusion, fixed AoE mark, delayed lunge impact, stun cancellation, parry and dodge invulnerability, length-axis weapon grip.\n");
   } finally {
    if(wall) UnityEngine.Object.Destroy(wall); UnityEngine.Object.Destroy(enemy.gameObject);
    hero.Health=health; Set(hero,"parryWindowUntil",-1f); Set(hero,"invulnerableUntil",-1f); hero.WarpTo(start);
   }
  }
  public static void EquipCase(string args) {
   var p = args.Split(' '); var h = OpenWorldManager.I.Hero;
   h.EquipSkin(int.Parse(p[0])); h.EquipWeapon(int.Parse(p[1]));
   OpenWorldManager.I.CameraRig.Yaw = h.transform.eulerAngles.y + 60; OpenWorldManager.I.CameraRig.Pitch=15; OpenWorldManager.I.CameraRig.SnapToTarget();
  }
  public static void VerifyCamera() {
   var camera = OpenWorldManager.I.CameraRig;
   Vector3 focus = OpenWorldManager.I.Hero.transform.position + Vector3.up * 25f;
   var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
   try {
    wall.transform.position = focus + Vector3.forward * .5f; wall.transform.localScale=new Vector3(2,2,.2f); Physics.SyncTransforms();
    object[] args = { focus,focus+Vector3.forward*3f,false };
    var resolved=(Vector3)typeof(OpenWorldCamera).GetMethod("ResolvePosition",Private).Invoke(camera,args);
    Check((bool)args[2] && Vector3.Distance(focus,resolved)<.2f,"Near-wall camera clearance failed");
    File.AppendAllText("Docs/verdant-slice-validation.txt","PASS: near-wall camera resolver respects lens clearance instead of forcing the old 0.35 m minimum.\n");
   } finally { UnityEngine.Object.Destroy(wall); }
  }
  public static void GearPose(string args) {
   var gm=OpenWorldManager.I; var hero=gm.Hero; var p=args.Split(' ');
   hero.WarpTo(gm.HuntGround(new Vector3(30,0,-25)));
   EquipCase(args);
   Check(hero.Visual.ModelInstance.name == OpenWorldCharacterVisual.AsterSkins[int.Parse(p[0])] + "_Model","Wrong visual skin");
   Set(hero,"animHoldUntil",Time.time+5f);
   hero.Visual.PlayAt("attack_1",OpenWorldWeaponCatalog.Get(int.Parse(p[1])).Style,.01f,.45f,.01f);
   gm.CameraRig.Distance=2.8f; gm.CameraRig.Yaw=hero.transform.eulerAngles.y+150; gm.CameraRig.Pitch=12; gm.CameraRig.SnapToTarget();
  }
  public static bool Busy { get; private set; }
  public static void ReviewGear() { Check(snapshot != null, "Begin a protected tour first"); OpenWorldManager.I.StartCoroutine(GearChecks()); }
  static System.Collections.IEnumerator GearChecks() {
   Busy = true;
   var gm = OpenWorldManager.I; var hero = gm.Hero;
   try {
    float deadline = Time.realtimeSinceStartup + 45f;
    while (OpenWorldWeaponCatalog.All.Any(w => !Resources.Load<GameObject>(w.Resource) && !OpenWorldImportedContent.Prefab(w.Resource))) {
     Check(Time.realtimeSinceStartup < deadline,"Timed out loading the arsenal"); yield return null;
    }
    int checks = 0;
    for (int skin = 0; skin < OpenWorldCharacterVisual.AsterSkins.Length; skin++) {
     hero.EquipSkin(skin);
     foreach (var def in OpenWorldWeaponCatalog.All) {
      hero.EquipWeapon(def.Id); yield return null;
      var hand = hero.Visual.RightHandBone;
      var root = Enumerable.Range(0,hand.childCount).Select(i => hand.GetChild(i)).FirstOrDefault(t => t.gameObject.activeSelf && t.name.StartsWith("OW_Equipped_"));
      Check(root,"Weapon missing on skin " + skin + ": " + def.Name);
      var renderer = root.GetComponentInChildren<MeshRenderer>();
      if (!renderer) renderer = root.GetComponentInChildren<Renderer>() as MeshRenderer;
      var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(root);
      float extent = Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
      Check(!float.IsNaN(extent) && extent > .1f && extent < 3.5f,"Invalid equipped weapon scale: " + def.Name);
      checks++;
     }
     EquipCase(skin + " 1"); yield return new WaitForSeconds(.4f);
     ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/verdant_skin_" + skin + ".png");
     yield return new WaitForSeconds(.2f);
    }
    File.AppendAllText("Docs/verdant-slice-validation.txt","PASS: " + checks + " weapon/skin combinations loaded with finite plausible bounds. Captured all six skins with Dueling Blade for visual review; this bounds check is not proof of every grip pose.\n");
   } finally { Busy=false; }
  }
  public static void Cleanup() {
   if(snapshot == null || !OpenWorldManager.I) return;
   var gm=OpenWorldManager.I;
   gm.RestoreAdventure(snapshot); gm.Hero.WarpTo(oldPosition); gm.Hero.Health=oldHealth;
   if(OpenWorldAtmosphere.Instance) OpenWorldAtmosphere.Instance.SetHour(oldHour,false,false);
   gm.CameraRig.Distance=oldCameraDistance; Set(gm.Hero,"animHoldUntil",0f);
   Set(gm,"saveBlocked",oldBlocked); Set(gm,"saveDirty",oldDirty); snapshot=null;
  }
 }
}
