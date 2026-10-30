using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // "Restless Lanterns" (Hollow Marches side quest, GLM Phase 2, night only): Sexton Ivo tends graves the
 // revenants keep disturbing. After dusk the four spirit lanterns of the graveyard wake; gather them all
 // past the patrols and the wraith that watches them falls to Aster's blade.
 public sealed partial class OpenWorldManager {
  internal const string LanternPrefix = "glm-lantern:";
  public static readonly Vector3 LanternCircle = new Vector3(-218f, 0f, -230f);
  public int LanternStage => CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "reward") ? 4 : CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "wraith") ? 3 : CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "gathered") ? 2 : CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "accepted") ? 1 : 0;
  public int LanternsHeld {
   get {
    int n = 0;
    if (CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l1")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l2")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l3")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l4")) n++;
    return n;
   }
  }
  public bool NightInMarches {
   get {
    var atm = OpenWorldAtmosphere.Instance;
    if (!atm) return false;
    return atm.Hour >= 20.5f || atm.Hour < 4.5f;
   }
  }
  public Vector3 LanternTarget {
   get {
    if (LanternStage == 0 || LanternStage >= 3) { var npc = NPCs.Find(n => n && n.NpcId == "glm-q-lantern"); return npc ? npc.transform.position : LanternCircle; }
    var runtime = GetComponent<OpenWorldQuestLanternRuntime>();
    return runtime ? runtime.NextLantern(Hero ? Hero.transform.position : LanternCircle) : LanternCircle;
   }
  }
  public string LanternObjective {
   get {
    if (LanternStage == 0) return "Speak to Sexton Ivo at the Hollow Marches graveyard";
    if (LanternStage == 1) return LanternsHeld == 4 ? "The lanterns are gathered — the wraith stirs" : !NightInMarches ? $"Wait for nightfall to gather the spirit lanterns ({LanternsHeld}/4)" : $"Gather the spirit lanterns past the revenants ({LanternsHeld}/4)";
    if (LanternStage == 2) return "Defeat the wraith of the hollow lanterns";
    if (LanternStage == 3) return "Bring the lanterns to Sexton Ivo";
    return "The graveyard rests again";
   }
  }
  public bool ConverseLantern() {
   if (LanternStage == 0) { SetLanternFlag("accepted"); ShowBanner("IVO: Four spirit lanterns keep the restless sleep. After dark they wander, and the revenants follow the light. Gather them, and face what watches them."); }
   else if (LanternStage == 3) {
    SetLanternFlag("reward"); Gold += 140; Gems += 5;
    ShowBanner("RESTLESS LANTERNS COMPLETE • 140 gold, 5 gems • The Hollow Marches sleep soundly"); OpenWorldAudio.Play("sfx_complete");
   } else if (LanternStage == 4) ShowBanner("IVO: The graves are quiet. You have my thanks, and theirs.");
   else ShowBanner("IVO: " + LanternObjective + ".");
   return true;
  }
  public bool InteractLantern() {
   var runtime = GetComponent<OpenWorldQuestLanternRuntime>();
   return runtime && runtime.TryCollect();
  }
  internal void SetLanternFlag(string flag) { CollectedWorldIds.Add(LanternPrefix + flag); MarkProgressDirty(); }
  internal void TickLanterns() { if (Hero && ActiveTerrain && !GetComponent<OpenWorldQuestLanternRuntime>()) gameObject.AddComponent<OpenWorldQuestLanternRuntime>(); }
 }
 public sealed class OpenWorldQuestLanternRuntime : MonoBehaviour {
  OpenWorldManager gm;
  readonly List<GameObject> lanterns = new List<GameObject>();
  OpenWorldEnemy wraith;
  bool built;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (!gm.NPCs.Exists(n => n && n.NpcId == "glm-q-lantern")) {
    var npc = OpenWorldNPC.Spawn("glm-q-lantern", "Sexton Ivo", "Graveyard Keeper", "The dead here do not rest on their own.", 5, -1, new Vector3(-204f, 0f, -214f), new Vector3(-201f, 0f, -212f), gm.WorldRoot);
    if (npc) gm.NPCs.Add(npc);
   }
   if (gm.IsModalOpen || gm.Hero.Dead) return;
   int stage = gm.LanternStage;
   if (stage == 1) {
    if (!built) Build();
    bool night = gm.NightInMarches;
    for (int i = 0; i < lanterns.Count; i++) {
     bool taken = gm.CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l" + (i + 1));
     lanterns[i].SetActive(night && !taken);
    }
    if (night && gm.LanternsHeld == 4 && !gm.CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "gathered")) { gm.SetLanternFlag("gathered"); gm.ShowBanner("The last lantern hums in your hands • something pale rises from the circle."); }
   } else if (stage == 2) {
    foreach (var l in lanterns) if (l) l.SetActive(false);
    if (!wraith && !gm.CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "wraith")) SpawnWraith();
    if (wraith && wraith.Dead && !gm.CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "wraith")) {
     gm.SetLanternFlag("wraith");
     gm.ShowBanner("THE WRAITH FALLS • Return the spirit lanterns to Sexton Ivo.");
     OpenWorldAudio.Play("sfx_complete", .5f);
    }
   } else {
    foreach (var l in lanterns) if (l) l.SetActive(false);
    if (wraith && !wraith.Dead) { gm.Enemies.Remove(wraith); Destroy(wraith.gameObject); wraith = null; }
   }
  }
  public bool TryCollect() {
   if (gm.LanternStage != 1 || !gm.NightInMarches) return false;
   for (int i = 0; i < lanterns.Count; i++) {
    if (!lanterns[i].activeSelf || gm.CollectedWorldIds.Contains(OpenWorldManager.LanternPrefix + "l" + (i + 1))) continue;
    float lanternDist = Vector2.Distance(new Vector2(gm.Hero.transform.position.x, gm.Hero.transform.position.z), new Vector2(lanterns[i].transform.position.x, lanterns[i].transform.position.z));
    if (lanternDist > 4f) continue;
    gm.SetLanternFlag("l" + (i + 1));
    gm.ShowBanner($"SPIRIT LANTERN ({gm.LanternsHeld}/4) • " + (gm.LanternsHeld == 4 ? "The wraith stirs…" : "Its cold light settles into your hands."));
    OpenWorldAudio.Play("sfx_power_select", .4f);
    return true;
   }
   return false;
  }
  public Vector3 NextLantern(Vector3 from) {
   Vector3 best = OpenWorldManager.LanternCircle; float bestD = float.MaxValue;
   for (int i = 0; i < lanterns.Count; i++) {
    if (!lanterns[i] || !lanterns[i].activeSelf) continue;
    float d = Vector3.Distance(from, lanterns[i].transform.position);
    if (d < bestD) { bestD = d; best = lanterns[i].transform.position; }
   }
   return best;
  }
  // Validation accessors (GlmQuestValidation drives the tour through the same public entries).
  public bool HasLanterns => built && lanterns.Count == 4;
  public bool LanternsActive { get { foreach (var l in lanterns) if (l && l.activeSelf) return true; return false; } }
  public Vector3 LanternSpot(int index) => lanterns[index - 1] ? lanterns[index - 1].transform.position - Vector3.up * 1.35f : gm.HuntGround(OpenWorldManager.LanternCircle);
  void SpawnWraith() {
   Vector3 site = gm.HuntGround(OpenWorldManager.LanternCircle);
   wraith = OpenWorldEnemy.Spawn("Elite", "Wraith of the Hollow Lanterns", site, gm.WorldRoot);
   wraith.MaxHealth = wraith.Health = 30f; wraith.AttackDamage = 1.3f;
   wraith.MoveSpeed = 4.6f; wraith.AttackRange = 2.6f; wraith.WarnRadius = 18f; wraith.AggroRadius = 12f; wraith.LeashRadius = 34f;
   wraith.SpecialName = "ARCANE ERUPTION";
   gm.Enemies.Add(wraith);
  }
  void Build() {
   built = true;
   var glow = new Color(.55f, .9f, 1f);
   Vector3[] spots = {
    new Vector3(-222f, 0f, -234f), new Vector3(-214f, 0f, -236f), new Vector3(-222f, 0f, -226f), new Vector3(-213f, 0f, -227f),
   };
   for (int i = 0; i < 4; i++) {
    Vector3 spot = gm.HuntGround(spots[i]);
    var root = new GameObject("Spirit lantern " + (i + 1)); root.transform.SetParent(gm.WorldRoot, false); root.transform.position = spot + Vector3.up * 1.35f;
    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.transform.SetParent(root.transform, false);
    orb.transform.localScale = Vector3.one * .38f; Destroy(orb.GetComponent<Collider>());
    var renderer = orb.GetComponent<Renderer>();
    renderer.sharedMaterial = OpenWorldMaterialHelper.CreateLit("Lantern glow", glow);
    OpenWorldVfx.Attach("motes", root.transform, Vector3.zero, 1.2f, glow);
    OpenWorldVfx.Attach("circle_magic", root.transform, Vector3.down * 1.28f, 1f, glow);
    lanterns.Add(root);
   }
  }
  void LateUpdate() {
   if (gm && gm.Hero && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateGlmQuestPanel(gm, "lantern");
  }
  void OnDestroy() {
   foreach (var l in lanterns) if (l) Destroy(l);
   if (wraith) { gm?.Enemies.Remove(wraith); if (wraith) Destroy(wraith.gameObject); }
  }
 }
}
