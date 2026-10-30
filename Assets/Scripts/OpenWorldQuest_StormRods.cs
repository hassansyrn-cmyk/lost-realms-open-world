using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // "The Conductor" (Stormveil side quest, GLM Phase 2): Rhega, the last stormwarden, keeps three lightning
 // rods that tame the tempest over the cliffs. During a thunderstorm, reach each fallen rod — two need a
 // gale vent or a grapple anchor — and set it upright.
 public sealed partial class OpenWorldManager {
  internal const string RodPrefix = "glm-rod:";
  public static readonly Vector3[] RodSpots = {
   new Vector3(240f, 0f, -274f),
   new Vector3(252f, 0f, -286f),
   new Vector3(234f, 0f, -288f),
  };
  public int RodStage => CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "reward") ? 3 : CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r3") ? 2 : CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "accepted") ? 1 : 0;
  public int RodsSet {
   get {
    int n = 0;
    if (CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r1")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r2")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r3")) n++;
    return n;
   }
  }
  public Vector3 RodTarget {
   get {
    if (RodStage == 0 || RodStage >= 2) { var npc = NPCs.Find(n => n && n.NpcId == "glm-q-rod"); return npc ? npc.transform.position : RodSpots[0]; }
    var runtime = GetComponent<OpenWorldQuestRodRuntime>();
    return runtime ? runtime.NextRod(Hero ? Hero.transform.position : RodSpots[0]) : RodSpots[0];
   }
  }
  public string RodObjective => RodStage == 0 ? "Speak to Rhega at the Stormveil overlook" : RodStage == 1 ? $"Raise the lightning rods ({RodsSet}/3) — a thunderstorm must crown the cliffs" : RodStage == 2 ? "Return to Rhega at the overlook" : "The tempest keeps its bargain";
  public bool ConverseRod() {
   if (RodStage == 0) {
    SetRodFlag("accepted"); ShowBanner("RHEGA: Three lightning rods fell, and the tempest forgets its bargain. Raise them under a living storm — call the thunder, ride the wind, and set each rod upright.");
    if (OpenWorldAtmosphere.Instance) OpenWorldAtmosphere.Instance.ApplyWeather(OpenWorldAtmosphere.WeatherPreset.Thunderstorm);
   } else if (RodStage == 2) {
    SetRodFlag("reward"); Gold += 120; Gems += 5; UnlockedWeapons.Add(5);
    ShowBanner("THE CONDUCTOR COMPLETE • 120 gold, 5 gems • Reaper's Spear unlocked"); OpenWorldAudio.Play("sfx_complete");
   } else if (RodStage == 3) ShowBanner("RHEGA: The storm sings to your spear-hand now. Stormveil bows to no one — but it will remember you.");
   else ShowBanner("RHEGA: " + RodObjective + ".");
   return true;
  }
  public bool InteractRod() {
   var runtime = GetComponent<OpenWorldQuestRodRuntime>();
   return runtime && runtime.TryRaise();
  }
  internal void SetRodFlag(string flag) { CollectedWorldIds.Add(RodPrefix + flag); MarkProgressDirty(); }
  internal void TickRods() { if (Hero && ActiveTerrain && !GetComponent<OpenWorldQuestRodRuntime>()) gameObject.AddComponent<OpenWorldQuestRodRuntime>(); }
 }
 public sealed class OpenWorldQuestRodRuntime : MonoBehaviour {
  OpenWorldManager gm;
  readonly List<GameObject> rods = new List<GameObject>();
  readonly List<OpenWorldUpdraft> vents = new List<OpenWorldUpdraft>();
  readonly List<OpenWorldGrappleAnchor> anchors = new List<OpenWorldGrappleAnchor>();
  bool built;
  public bool Built => built;
  float weatherUntil;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (!gm.NPCs.Exists(n => n && n.NpcId == "glm-q-rod")) {
    var npc = OpenWorldNPC.Spawn("glm-q-rod", "Rhega", "Stormwarden", "The cliffs test everyone. Some they keep.", 2, 5, new Vector3(228f, 0f, -258f), new Vector3(230f, 0f, -255f), gm.WorldRoot);
    if (npc) gm.NPCs.Add(npc);
   }
   if (gm.IsModalOpen || gm.Hero.Dead) return;
   int stage = gm.RodStage;
   if (stage == 1) {
    if (!built) Build();
    // Keep the storm alive while the work is out: the rods only take a raising under a living thunderstorm.
    var atm = OpenWorldAtmosphere.Instance;
    if (atm && atm.CurrentWeather != OpenWorldAtmosphere.WeatherPreset.Thunderstorm && Time.time > weatherUntil) {
     atm.ApplyWeather(OpenWorldAtmosphere.WeatherPreset.Thunderstorm);
     weatherUntil = Time.time + 8f;
    }
   } else if (built) {
    foreach (var r in rods) if (r) r.SetActive(false);
    foreach (var v in vents) if (v) v.gameObject.SetActive(false);
    foreach (var a in anchors) if (a) a.gameObject.SetActive(false);
   }
  }
  void Build() {
   built = true;
   float[] heights = { 0f, 7.5f, 5.5f };
   for (int i = 0; i < 3; i++) {
    Vector3 spot = gm.HuntGround(OpenWorldManager.RodSpots[i]);
    var root = new GameObject("Lightning rod " + (i + 1)); root.transform.SetParent(gm.WorldRoot, false); root.transform.position = spot;
    // Fallen mast: tilted until raised, then the runtime swaps the tilt and lights the crown.
    var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder); mast.transform.SetParent(root.transform, false);
    mast.transform.localPosition = Quaternion.Euler(0f, i * 40f, 62f) * Vector3.up * (1.6f + heights[i] * .4f);
    mast.transform.localScale = new Vector3(.22f, 2.6f, .22f); mast.transform.localRotation = Quaternion.Euler(0f, i * 40f, 62f);
    mast.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Rod mast", new Color(.35f, .38f, .45f));
    var label = new GameObject("Rod label"); label.transform.SetParent(root.transform, false); label.transform.localPosition = Vector3.up * 2.6f;
    var text = label.AddComponent<TextMesh>(); text.text = "FALLEN LIGHTNING ROD\n[RAISE DURING THE STORM]"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .05f; text.fontSize = 30; text.color = new Color(.75f, .85f, 1f);
    if (heights[i] > 0f) {
     // Elevated rods stand on rock pillars; a vent and a grapple anchor give two ways up.
     var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pillar.transform.SetParent(root.transform, false);
     pillar.transform.localPosition = Vector3.up * (heights[i] * .5f - .1f); pillar.transform.localScale = new Vector3(2.2f, heights[i] * .5f, 2.2f);
     pillar.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Storm pillar", new Color(.3f, .32f, .4f));
     mast.transform.localPosition = Vector3.up * (heights[i] + 1.2f);
     mast.transform.localRotation = Quaternion.Euler(0f, i * 40f, 62f);
     var vent = OpenWorldUpdraft.Spawn("STORMVEIL UPDRAFT", spot + new Vector3(2.6f, 0f, 2.6f), gm.WorldRoot, 24f);
     vents.Add(vent);
     var anchor = OpenWorldGrappleAnchor.Spawn("ROD " + (i + 1) + " MAST", spot + Vector3.up * (heights[i] + 2.4f), gm.WorldRoot);
     anchors.Add(anchor);
    }
    rods.Add(root);
   }
  }
  public bool TryRaise() {
   if (gm.RodStage != 1) return false;
   var atm = OpenWorldAtmosphere.Instance;
   if (!atm || atm.CurrentWeather != OpenWorldAtmosphere.WeatherPreset.Thunderstorm) { gm.ShowBanner("The rods take only a living storm • wait for the thunder."); return true; }
   for (int i = 0; i < rods.Count; i++) {
    if (!rods[i].activeSelf || gm.CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r" + (i + 1))) continue;
    if (Vector2.Distance(new Vector2(gm.Hero.transform.position.x, gm.Hero.transform.position.z), new Vector2(rods[i].transform.position.x, rods[i].transform.position.z)) > 4.5f) continue;
    gm.SetRodFlag("r" + (i + 1));
    var mast = rods[i].transform.GetChild(0);
    mast.localRotation = Quaternion.identity;
    mast.localPosition = Vector3.up * 1.6f;
    OpenWorldVfx.Attach("circle_magic", rods[i].transform, Vector3.up * 2.8f, 1.4f, new Color(.6f, .8f, 1f));
    gm.ShowBanner($"LIGHTNING ROD RAISED ({gm.RodsSet}/3) • " + (gm.RodsSet == 3 ? "Return to Rhega." : "The tempest snarls. Two rods stand."));
    OpenWorldAudio.Play("sfx_gale_cast", .6f);
    return true;
   }
   return false;
  }
  public Vector3 NextRod(Vector3 from) {
   Vector3 best = from; float bestD = float.MaxValue;
   for (int i = 0; i < 3; i++) {
    if (gm.CollectedWorldIds.Contains(OpenWorldManager.RodPrefix + "r" + (i + 1))) continue;
    float d = Vector3.Distance(from, gm.HuntGround(OpenWorldManager.RodSpots[i]));
    if (d < bestD) { bestD = d; best = gm.HuntGround(OpenWorldManager.RodSpots[i]); }
   }
   return best;
  }
  void LateUpdate() {
   if (gm && gm.Hero && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateGlmQuestPanel(gm, "rod");
  }
  void OnDestroy() {
   foreach (var r in rods) if (r) Destroy(r);
   foreach (var v in vents) if (v) Destroy(v.gameObject);
   foreach (var a in anchors) if (a) Destroy(a.gameObject);
  }
 }
}
