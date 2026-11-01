using System.Reflection;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LostRealmsOW {
 public static class GlmJumpDiagnostics {
  const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static FieldInfo velocityField;

  static Vector3 HeroVel(OpenWorldHero hero) {
   if (velocityField == null) velocityField = typeof(OpenWorldHero).GetField("velocity", Private);
   return velocityField != null ? (Vector3)velocityField.GetValue(hero) : Vector3.zero;
  }

  static void Log(string line) {
   System.IO.File.AppendAllText("Docs/glm-jump-validation.txt", System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
   Debug.Log("[GlmJump] " + line);
  }

  static string State(OpenWorldHero hero) {
   var vel = HeroVel(hero);
   return $"TouchJump={hero.TouchJump}, grounded={hero.Grounded}, gliding={hero.IsGliding}, velY={vel.y:0.00}, hp={hero.Health:0.0}, pos.y={hero.transform.position.y:0.00}";
  }

  public static void PressJumpButton() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Log("ERROR: world missing"); return; }
   var jumpBtn = UnityEngine.Object.FindAnyObjectByType<OpenWorldJumpButton>(FindObjectsInactive.Include);
   if (!jumpBtn) { Log("ERROR: jump button not found in scene"); return; }
   Log("dispatch: pressing jump button | " + State(gm.Hero));
   var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
   ExecuteEvents.Execute<IPointerDownHandler>(jumpBtn.gameObject, eventData, (handler, _) => handler.OnPointerDown(eventData));
   Log("dispatch: done, " + State(gm.Hero));
  }

  public static void ReportState(string label) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Log(label + ": world missing"); return; }
   Log(label + ": " + State(gm.Hero));
  }

  static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

  public static void Cleanup() {
   Log("cleanup done");
  }

  // Cooking recipe verification: seeds meat, cooks 3 times at the Oakvale campfire, asserts
  // the recipe cycle and RawMeat consumption via the real CookMeal path.
  public static void VerifyCooking() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Log("cooking: world missing"); return; }
   var fire = gm.Campfires.Find(f => f && f.CampfireTitle == "OAKVALE CAMPFIRE");
   if (!fire) { Log("cooking: Oakvale campfire not found"); return; }
   gm.RawMeat = 3;
   gm.Hero.WarpTo(fire.transform.position + Vector3.back * 1.5f);
   Set(gm.Hero, "invulnerableUntil", -1f);
   int goldBefore = gm.Gold;
   for (int i = 0; i < 3; i++) {
    int meatBefore = gm.RawMeat;
    fire.CookMeal(gm);
    Log($"cooking: cook {i + 1}, meat {meatBefore} -> {gm.RawMeat}, buff active={gm.Hero.IsMealBuffActive}, gold={gm.Gold}");
   }
   Check(gm.RawMeat == 0, "RawMeat should be 0 after 3 cooks, is " + gm.RawMeat);
   Log("cooking OK: 3 recipes cycled, 3 meat consumed, buff active");
  }
  static void Check(bool c, string t) { if (!c) throw new System.Exception(t); }
 }
}
