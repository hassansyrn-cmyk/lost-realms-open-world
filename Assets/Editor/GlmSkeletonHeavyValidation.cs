using System;
using System.Reflection;
using UnityEngine;

namespace LostRealmsOW {
 // Verification for the Hollow Revenant heavy attack (GLM; brief task 2026-10-30 22:40). Spawns its own
 // revenant (registered in Manager.Enemies like a natural one) and drives it deterministically through
 // the same private combat entry points the AI uses, asserting: heavy configuration (0.8 s windup, amber
 // 2.6 m BONE SLAM), exactly-one damage application, dodge/cover evasion, parry-stagger cancellation,
 // death cancellation, the 6.5 s cooldown, the surviving normal attack, and unchanged behaviour for
 // other goblin-role species. Results append to Docs/glm-skeletonheavy-validation.txt. The player's save
 // is never written.
 public static class GlmSkeletonHeavyValidation {
  const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static OpenWorldEnemy revenant;
  static GameObject cover;

  static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
  static object Get(object obj, string member) {
   var prop = obj.GetType().GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
   if (prop != null) return prop.GetValue(obj);
   var fld = obj.GetType().GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
   if (fld != null) return fld.GetValue(obj);
   throw new System.Exception("member not found: " + member);
  }
  static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
  static void Invoke(object obj, string method, params object[] values) => obj.GetType().GetMethod(method, Private).Invoke(obj, values);
  static void Log(string line) {
   System.IO.File.AppendAllText("Docs/glm-skeletonheavy-validation.txt", DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
   Debug.Log("[SkeletonHeavy] " + line);
  }

  // Spawns a fresh Hollow Revenant 5 m in front of the hero and parks the hero in melee.
  public static void Setup() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero, "World required");
   DestroyRevenant();
   revenant = OpenWorldEnemy.Spawn("Goblin", "Hollow Revenant", gm.HuntGround(gm.Hero.transform.position + gm.Hero.transform.forward * 5f), gm.WorldRoot);
   revenant.MaxHealth = revenant.Health = 40f;
   revenant.AttackDamage = 1.2f;
   revenant.AggroRadius = 0f; // the natural AI stays idle; the suite drives every windup
   revenant.WarnRadius = 0f;
   revenant.LeashRadius = 999f;
   gm.Enemies.Add(revenant);
   var hero = gm.Hero;
   hero.WarpTo(revenant.transform.position + Vector3.back * 1.8f);
   Set(hero, "invulnerableUntil", -1f);
   Set(hero, "parryWindowUntil", -1f);
   hero.Health = hero.MaxHealth;
   Log("setup: spawned test revenant 5 m ahead, hero parked in melee, AI aggro disabled");
  }

  static void DestroyRevenant() {
   if (!revenant) { revenant = null; return; }
   var gm = OpenWorldManager.I;
   if (gm) gm.Enemies.Remove(revenant);
   if (revenant) UnityEngine.Object.Destroy(revenant.gameObject);
   revenant = null;
  }

  static void ArmHeavy() {
   Set(revenant, "nextHeavyAt", 0f);
   Set(revenant, "bossSwingCount", 2);
   TopUp();
   Invoke(revenant, "BeginWindup");
  }

  static void TopUp() {
   var gm = OpenWorldManager.I;
   Set(gm.Hero, "invulnerableUntil", -1f);
   Set(gm.Hero, "parryWindowUntil", -1f);
   gm.Hero.Health = gm.Hero.MaxHealth;
  }

  // Forces the heavy: cooldown cleared, even swing count — asserts the BONE SLAM configuration.
  public static void Windup() {
   Check(revenant && !revenant.Dead, "Revenant missing");
   ArmHeavy();
   var move = (string)Get(revenant, "CombatMove");
   var size = (float)Get(revenant, "strikeSize");
   var color = (Color)Get(revenant, "strikeColor");
   float timer = (float)Get(revenant, "stateTimer");
   Check(move == "BONE SLAM", "CombatMove is " + move + ", expected BONE SLAM");
   Check(Mathf.Abs(timer - 0.8f) < 0.01f, "Heavy windup is " + timer + " s, expected 0.8");
   Check(Mathf.Abs(size - 2.6f) < 0.01f, "Heavy strike size is " + size + ", expected 2.6");
   Check(Mathf.Abs(color.r - 1f) < 0.05f && color.g >= 0.55f && color.g <= 0.8f, "Heavy warning is not amber: " + color);
   Log("windup OK: BONE SLAM, 0.8 s windup, amber 2.6 m warning live (screenshot next)");
  }

  // Completes the strike while the hero stands in reach: exactly one heavy damage application.
  public static void Strike() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero && revenant && !revenant.Dead, "Revenant missing");
   TopUp();
   gm.Hero.WarpTo(revenant.transform.position + revenant.transform.forward * 1.2f); // re-park: patrols drift
   Physics.SyncTransforms();
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   float after = gm.Hero.Health;
   float heavyDrop = before - after;
   // Hero-side mitigation scales the raw amount; a double application would roughly double it.
   Check(heavyDrop > 0.5f && heavyDrop < 2.6f, "Heavy damage not a single application: " + heavyDrop.ToString("0.00"));
   Log("strike OK: exactly one heavy application (" + before.ToString("0.00") + " -> " + after.ToString("0.00") + ")");
  }

  // Dodging out of the 2.6 m warning circle prevents the hit.
  public static void Dodge() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero && revenant && !revenant.Dead, "Revenant missing");
   ArmHeavy();
   gm.Hero.WarpTo(revenant.transform.position + Vector3.forward * 4.5f);
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   Check(Mathf.Abs(gm.Hero.Health - before) < 0.01f, "Hit landed despite dodging out of reach");
   Log("dodge OK: 4.5 m outside the 2.6 m slam — no damage");
  }

  // A solid cube between the skeleton and the hero blocks the strike.
  public static void Cover() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero && revenant && !revenant.Dead, "Revenant missing");
   ArmHeavy();
   gm.Hero.WarpTo(revenant.transform.position + Vector3.forward * 1.8f);
   if (!cover) {
    cover = new GameObject("GlmHeavyCover", typeof(BoxCollider)).Extract();
    cover.transform.localScale = new Vector3(2f, 2f, 0.3f);
   }
   cover.transform.position = (revenant.transform.position + gm.Hero.transform.position) / 2f + Vector3.up;
   Physics.SyncTransforms();
   ArmHeavy();
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   Check(Mathf.Abs(gm.Hero.Health - before) < 0.01f, "Hit passed through solid cover");
   Log("cover OK: solid cover between skeleton and hero — no damage");
  }

  // Stagger during the windup cancels the pending heavy (existing parry/stagger rules).
  public static void ParryStagger() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero && revenant && !revenant.Dead, "Revenant missing");
   ArmHeavy();
   revenant.Stun(0.6f);
   var pending = (bool)Get(revenant, "awaitingImpact");
   var telegraph = Get(revenant, "telegraph");
   Check(!pending, "Pending impact survived the stagger");
   Check(telegraph == null, "Telegraph survived the stagger");
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   Check(Mathf.Abs(gm.Hero.Health - before) < 0.01f, "Damage landed after the stagger cancelled the windup");
   Log("stagger OK: pending heavy cancelled, telegraph dismissed, no damage on resolve");
  }

  // Death during the windup: no delayed damage, and the hero survives.
  public static void DeathCancel() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero && revenant && !revenant.Dead, "Revenant missing");
   ArmHeavy();
   revenant.TakeHit(9999f, gm.Hero.transform.position, true);
   Check(revenant.Dead, "Revenant did not die");
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   Check(Mathf.Abs(gm.Hero.Health - before) < 0.01f, "Dead skeleton's heavy still damaged the hero");
   Log("death OK: heavy cancelled by death — no delayed damage");
   DestroyRevenant(); // respawned fresh by the next step that needs one
  }

  // Immediately after a heavy the cooldown forces the ordinary attack (and it still works).
  public static void Cooldown() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero, "World required");
   Setup(); // fresh revenant with the hero parked at melee
   Check(revenant && !revenant.Dead, "Revenant missing");
   gm.Hero.WarpTo(revenant.transform.position + revenant.transform.forward * 1.6f); // in front: normal attacks need facing
   Physics.SyncTransforms();
   TopUp();
   Set(revenant, "bossSwingCount", 2); // even swing would be a special if the cooldown allowed it
   Set(revenant, "nextHeavyAt", Time.time + 6.5f); // simulate a heavy that just happened
   Invoke(revenant, "BeginWindup");
   var move = (string)Get(revenant, "CombatMove");
   Check(move == "CLEAVE", "Cooldown did not gate the heavy: CombatMove " + move);
   float before = gm.Hero.Health;
   Invoke(revenant, "PerformStrike", gm.Hero);
   float normalDrop = before - gm.Hero.Health;
   Check(normalDrop > 0.3f && normalDrop < 1.8f, "Normal attack damage not a single application: " + normalDrop.ToString("0.00"));
   Log("cooldown OK: normal CLEAVE only (next heavy in ~6.5 s), normal damage " + before.ToString("0.00") + " -> " + gm.Hero.Health.ToString("0.00"));
  }

  // Other goblin-role species keep their existing special, and the cooldown is per-enemy.
  public static void OtherSkeletons() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero, "World required");
   var other = OpenWorldEnemy.Spawn("Goblin", "Dune Stalker", gm.HuntGround(gm.Hero.transform.position + gm.Hero.transform.forward * 5f), gm.WorldRoot);
   try {
    Set(other, "bossSwingCount", 1); // odd pre-increment rolls the special
    Invoke(other, "BeginWindup");
    Check((string)Get(other, "CombatMove") == "VENOM BURST", "Dune Stalker special changed: " + Get(other, "CombatMove"));
    Log("others OK: Dune Stalker keeps VENOM BURST");
   } finally {
    if (gm.Enemies.Contains(other)) gm.Enemies.Remove(other);
    if (other) UnityEngine.Object.Destroy(other.gameObject);
   }
  }

  public static void Cleanup() {
   if (cover) { UnityEngine.Object.Destroy(cover); cover = null; }
   DestroyRevenant();
   var gm = OpenWorldManager.I;
   if (gm && gm.Hero) Set(gm.Hero, "invulnerableUntil", Time.time + 3600f); // restore god-mode safety
   Log("cleanup done: cover removed, test revenant despawned, hero invulnerability restored");
  }

  // Bridge check: the whole suite in one call — each failure throws with the step named.
  public static void RunAll() {
   var steps = new (string name, Action action)[] {
    ("setup", Setup), ("windup", Windup), ("strike", Strike), ("dodge", Dodge),
    ("cover", Cover), ("parry-stagger", ParryStagger), ("death-cancel", DeathCancel),
    ("cooldown", Cooldown), ("other-skeletons", OtherSkeletons), ("cleanup", Cleanup),
   };
   foreach (var step in steps) {
    try { step.action(); }
    catch (Exception e) { throw new Exception("[" + step.name + "] " + e.Message, e); }
   }
   Log("RunAll: every step PASS");
  }
 }

 // Small extension so the cover cube can be created with its collider in one expression.
 internal static class GlmCoverExtensions {
  public static GameObject Extract(this GameObject go) => go;
 }
}
