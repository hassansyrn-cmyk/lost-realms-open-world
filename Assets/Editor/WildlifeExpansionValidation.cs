using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 public static class WildlifeExpansionValidation {
  static GameObject preview;
  static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
  static OpenWorldAnimalInteraction Create(string key, string species, Vector3 position, string id) {
   var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/" + key + ".prefab");
   Check(prefab, "Prepared animal missing: " + key);
   var root = new GameObject("Validation " + species);
   root.transform.position = position;
   var model = UnityEngine.Object.Instantiate(prefab, root.transform, false);
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.localScale *= (species == "Horse" ? 1.75f : 1.45f) / b.size.y;
   b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.position += new Vector3(position.x - b.center.x, position.y - b.min.y, position.z - b.center.z);
   root.AddComponent<OpenWorldWildlife>();
   OpenWorldAnimalInteraction.Attach(root, species, id);
   return root.GetComponent<OpenWorldAnimalInteraction>();
  }
  public static void Verify() {
   var gm = OpenWorldManager.I;
   Check(Application.isPlaying && gm && gm.Hero && gm.ActiveTerrain, "Play mode/world required");
   var hero = gm.Hero;
   int meat = gm.RawMeat; Vector3 oldPosition = hero.transform.position;
   float health = hero.Health, aether = hero.Aether;
   float stamina = hero.Stamina; int gold = gm.Gold;
   var mealField = typeof(OpenWorldHero).GetField("mealBuffUntil", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
   object meal = mealField.GetValue(hero);
   var oldCollected = new System.Collections.Generic.HashSet<string>(gm.CollectedWorldIds);
   OpenWorldAnimalInteraction deer = null, horse = null;
   try {
    var p = oldPosition + hero.transform.forward * 1.3f;
    p.y = gm.ActiveTerrain.SampleHeight(p) + gm.ActiveTerrain.transform.position.y + .03f;
    deer = Create("wildlife_83c3ea98a8", "Meadow Deer", p, "hunted:validation");
    int hit = OpenWorldAnimalInteraction.Strike(oldPosition, hero.transform.forward, 2.5f, 100f, false);
    Check(hit >= 1 && deer.Dead, "Melee hunting did not defeat the deer");
    deer.Interact(gm);
    Check(gm.RawMeat == meat + 2, "Meat collection failed");
    deer.Interact(gm);
    Check(gm.RawMeat == meat + 2, "Duplicate meat collection");
    var data = JsonUtility.FromJson<OpenWorldSaveData>(JsonUtility.ToJson(gm.CaptureAdventure()));
    Check(data.rawMeat == meat + 2 && Array.IndexOf(data.collected, "hunted:validation") >= 0, "Hunting snapshot missing");
    Check(JsonUtility.FromJson<OpenWorldSaveData>("{}").rawMeat == 0, "Legacy meat default changed");
    Check(gm.Campfires.Count > 0, "No campfire");
    gm.RawMeat = 0; hero.Health = 2f;
    gm.Campfires[0].CookMeal(gm);
    Check(gm.RawMeat == 0 && hero.Health == 2f, "Cooking without meat gave a meal");
    gm.RawMeat = 1; gm.Campfires[0].CookMeal(gm);
    Check(gm.RawMeat == 0 && hero.Health == hero.MaxHealth, "Cooking did not consume meat and heal");
    horse = Create("wildlife_a868c1dd18", "Horse", p, "horse:validation");
    hero.EnterMount(horse);
    Check(hero.IsMounted && horse.Rider == hero && !hero.Controller.enabled, "Mount state failed");
    hero.WarpTo(oldPosition);
    Check(!hero.IsMounted && !horse.Rider && hero.Controller.enabled, "Warp failed to release mount");
    File.WriteAllText("Docs/wildlife-expansion-validation.txt", "PASS: melee deer hunt; collect exactly once; meat/hunted ID JSON snapshot; legacy zero meat; cooking requires/consumes meat; horse mounting; warp releases mount and restores hero controller. Visual riding, terrain collision and long-distance streaming need separate review.\n");
    Debug.Log("[WildlifeValidation] PASS");
   } catch (Exception e) {
    File.WriteAllText("Docs/wildlife-expansion-validation.txt", "FAIL: " + e + "\n"); throw;
   } finally {
    hero.LeaveMount(true); hero.WarpTo(oldPosition);
    gm.RawMeat = meat; hero.Health = health; hero.Aether = aether;
    gm.Gold = gold; hero.Stamina = stamina; mealField.SetValue(hero, meal);
    gm.CollectedWorldIds.Clear(); gm.CollectedWorldIds.UnionWith(oldCollected);
    if (deer) UnityEngine.Object.Destroy(deer.gameObject);
    if (horse) UnityEngine.Object.Destroy(horse.gameObject);
   }
  }
  public static void PreviewHorse() {
   var gm = OpenWorldManager.I; if (!gm || !gm.Hero) return;
   gm.CloseMenus();
   foreach (var animal in UnityEngine.Object.FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsSortMode.None)) {
    if (!animal.Horse || Vector3.Distance(animal.transform.position, gm.Hero.transform.position) > 25f) continue;
    gm.Hero.WarpTo(animal.transform.position + animal.transform.right * 1.6f);
    gm.Hero.EnterMount(animal);
    return;
   }
   var p = gm.Hero.transform.position + gm.Hero.transform.forward * 1.5f;
   p.y = gm.ActiveTerrain.SampleHeight(p) + gm.ActiveTerrain.transform.position.y;
   var horse = Create("wildlife_a868c1dd18", "Horse", p, "horse:preview");
   preview = horse.gameObject;
   gm.Hero.EnterMount(horse);
  }
  public static void Ride() {
   var hero = OpenWorldManager.I.Hero;
   Check(hero && hero.Mount, "Mount before running the riding probe");
   hero.Mount.StartCoroutine(RideFrames(hero));
  }
  static System.Collections.IEnumerator RideFrames(OpenWorldHero hero) {
   bool focused = OpenWorldHero.DebugAlwaysFocused;
   OpenWorldHero.DebugAlwaysFocused = true;
   Vector3 start = hero.Mount.transform.position;
   try {
    for (float t = 0f; t < 2f && hero && hero.Mount; t += Time.deltaTime) {
     hero.TouchMoveInput = Vector2.up; hero.TouchSprintHeld = true;
     yield return null;
    }
    hero.TouchMoveInput = Vector2.zero; hero.TouchSprintHeld = false;
    float distance = hero.Mount ? Vector3.Distance(start, hero.Mount.transform.position) : 0f;
    Check(hero.Mount && distance > 1f, "Riding did not move through terrain");
    File.AppendAllText("Docs/wildlife-expansion-validation.txt", "PASS: mounted gallop input moved horse " + distance.ToString("F2") + " metres in 2 seconds with rider attached.\n");
    var gm = OpenWorldManager.I;
    gm.CameraRig.Yaw = hero.transform.eulerAngles.y + 75f; gm.CameraRig.Pitch = 10f; gm.CameraRig.SnapToTarget();
   } finally { OpenWorldHero.DebugAlwaysFocused = focused; }
  }
  public static void Cleanup() {
   if (OpenWorldManager.I && OpenWorldManager.I.Hero) OpenWorldManager.I.Hero.LeaveMount(true);
   if (preview) UnityEngine.Object.Destroy(preview);
  }
  public static void VisitExpansion(string key) {
   var data = Resources.Load<TextAsset>("World/ImportedContent");
   var gm = OpenWorldManager.I;
   if (!data || !gm || !gm.Hero || !gm.CameraRig) { Debug.LogWarning("[ExpansionPlacement] visit " + key + ": needs Play mode with the world loaded"); return; }
   var manifest = JsonUtility.FromJson<ImportedContentManifest>(data.text);
   var entry = Array.Find(manifest.entries, e => e.key == key);
   if (entry == null) { Debug.LogWarning("[ExpansionPlacement] visit: unknown or unprepared entry " + key); return; }
   gm.Hero.WarpTo(entry.position + new Vector3(0, 0, -8));
   gm.CameraRig.Yaw = 0f; gm.CameraRig.Pitch = 15f; gm.CameraRig.SnapToTarget();
  }
  public static void ReportExpansion() {
   var text = new System.Text.StringBuilder();
   foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
    if (!t.name.StartsWith("Imported expansion_")) continue;
    var b = OpenWorldMaterialHelper.ComputeRendererBounds(t);
    text.AppendLine(t.name + " at " + t.position.ToString("F2") + " size " + b.size.ToString("F2"));
   }
   File.AppendAllText("Docs/AssetAudit/EXPANSION_PLACEMENT_REPORT.txt", text.ToString());
   Debug.Log("[ExpansionPlacement] " + text);
  }
 }
}
