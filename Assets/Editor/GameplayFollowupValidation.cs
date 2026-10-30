using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LostRealmsOW {
 public static class GameplayFollowupValidation {
  static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
  public static void Verify() {
   var gm = OpenWorldManager.I; var hero = gm.Hero;
   WildlifeExpansionValidation.PreviewHorse();
   Check(hero.IsMounted, "Mount fixture failed");
   var vent = UnityEngine.Object.FindFirstObjectByType<OpenWorldUpdraft>();
   Check(vent, "Updraft missing");
   var old = vent.transform.position;
   var velocity = typeof(OpenWorldHero).GetField("velocity", Private);
   var setGlide = typeof(OpenWorldHero).GetMethod("SetGliding", Private);
   try {
    vent.transform.position = hero.transform.position;
    typeof(OpenWorldUpdraft).GetField("nextLaunchTime", Private).SetValue(vent, 0f);
    Vector3 before = (Vector3)velocity.GetValue(hero);
    typeof(OpenWorldUpdraft).GetMethod("Update", Private).Invoke(vent, null);
    Check(!hero.IsGliding && (Vector3)velocity.GetValue(hero) == before, "Mounted vent launched or opened glider");
    hero.LaunchIntoGlide();
    setGlide.Invoke(hero, new object[] { true });
    Check(!hero.IsGliding && (Vector3)velocity.GetValue(hero) == before, "Direct mounted launch/glide bypass");
    bool focus = OpenWorldHero.DebugAlwaysFocused;
    try {
     OpenWorldHero.DebugAlwaysFocused = true;
     hero.TouchMoveInput = new Vector2(0f, .8f);
     for (int i = 0; i < 5; i++) {
      typeof(OpenWorldHero).GetMethod("Update", Private).Invoke(hero, null);
      Check(hero.TouchMoveInput.y == .8f, "Mounted update consumed held touch input");
     }
    } finally { OpenWorldHero.DebugAlwaysFocused = focus; hero.TouchMoveInput = Vector2.zero; }
    var hud = OpenWorldMobileHud.Instance;
    var updateWhistle = typeof(OpenWorldMobileHud).GetMethod("UpdateWhistleControl", Private | BindingFlags.Public);
    typeof(OpenWorldMobileHud).GetField("nextHorseScan", Private).SetValue(hud, 0f);
    updateWhistle.Invoke(hud, null);
    var button = (UnityEngine.UI.Button)typeof(OpenWorldMobileHud).GetField("whistleControl", Private).GetValue(hud);
    Check(button && !button.gameObject.activeSelf, "Whistle shown while mounted");
    hero.LeaveMount(true);
    Check(!hero.IsGliding, "Dismount left glider open");
    updateWhistle.Invoke(hud, null);
    Check(button.gameObject.activeSelf, "Known-horse whistle hidden after dismount");
    button.onClick.Invoke();
    vent.transform.position = hero.transform.position;
    typeof(OpenWorldUpdraft).GetMethod("Update", Private).Invoke(vent, null);
    Check(hero.IsGliding && ((Vector3)velocity.GetValue(hero)).y > 0f, "On-foot vent failed");
    File.WriteAllText("Docs/gameplay-followup-validation.txt", "PASS: mounted vent causes no launch/glider; direct mounted launch and SetGliding(true) refused; dismount folded; on-foot vent launches into glide. These are live handler checks, not a physical touch ride-through.\n");
    File.AppendAllText("Docs/gameplay-followup-validation.txt", "PASS: mounted Update preserves a single held touch-stick value across five handler calls; whistle hidden mounted, visible with known horse after dismount, button callback invoked. Not physical Android tests or a whistle-arrival test.\n");
   } finally { vent.transform.position = old; setGlide.Invoke(hero, new object[] { false }); WildlifeExpansionValidation.Cleanup(); }
  }
 }
}
