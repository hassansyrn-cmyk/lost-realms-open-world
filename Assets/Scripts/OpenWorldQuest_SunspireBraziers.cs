using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // "Trial of the Sunspire" (Burning Dunes side quest, GLM Phase 2): Sahra, Sunspire's flamekeeper, asks
 // Aster to rekindle three high watch braziers. Each brazier stands on a rock pillar above a Gale
 // Updraft vent — ride the wind, glide, and light the bowl with an Ember cast.
 public sealed partial class OpenWorldManager {
  internal const string BrazierPrefix = "glm-brazier:";
  public static readonly Vector3[] BrazierSpots = {
   new Vector3(338f, 0f, -16f),
   new Vector3(366f, 0f, 18f),
   new Vector3(330f, 0f, 60f),
  };
  public int BrazierStage => CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "reward") ? 3 : CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit3") ? 2 : CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "accepted") ? 1 : 0;
  public int BraziersLit {
   get {
    int n = 0;
    if (CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit1")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit2")) n++;
    if (CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit3")) n++;
    return n;
   }
  }
  public Vector3 BrazierTarget {
   get {
    if (BrazierStage == 0 || BrazierStage >= 2) { var npc = NPCs.Find(n => n && n.NpcId == "glm-q-brazier"); return npc ? npc.transform.position : BrazierSpots[0]; }
    // Next unlit brazier, or the first one while everything still stands unlit.
    var runtime = GetComponent<OpenWorldQuestBrazierRuntime>();
    return runtime ? runtime.NextUnlit(Hero ? Hero.transform.position : BrazierSpots[0]) : BrazierSpots[0];
   }
  }
  public string BrazierObjective => BrazierStage == 0 ? "Speak to Sahra at Sunspire" : BrazierStage == 1 ? $"Light the watch braziers with Ember ({BraziersLit}/3) — ride the updrafts" : BrazierStage == 2 ? "Return to Sahra at Sunspire" : "The Sunspire fires burn again";
  public bool ConverseBrazier() {
   if (BrazierStage == 0) { SetBrazierFlag("accepted"); ShowBanner("SAHRA: The watch braziers have gone cold, and the dunes forget their keepers. Ride the gale vents, glide to the bowls, and wake them with Ember fire."); }
   else if (BrazierStage == 2) {
    SetBrazierFlag("reward"); Gold += 120; Gems += 6;
    ShowBanner("TRIAL OF THE SUNSPIRE COMPLETE • 120 gold, 6 gems • The Sunspire remembers your name"); OpenWorldAudio.Play("sfx_complete");
   } else if (BrazierStage == 3) ShowBanner("SAHRA: The flames hold. The dunes are yours to wander, flame-keeper.");
   else ShowBanner("SAHRA: " + BrazierObjective + ".");
   return true;
  }
  public void LightBrazier(int index) {
   if (BrazierStage != 1 || index < 1 || index > 3 || CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit" + index)) return;
   SetBrazierFlag("lit" + index);
   ShowBanner($"BRAZIER LIT ({BraziersLit}/3) • " + (BraziersLit == 3 ? "Return to Sahra at Sunspire." : "Two bowls more, or rest and try again."));
   OpenWorldAudio.Play("sfx_complete", .5f);
  }
  void SetBrazierFlag(string flag) { CollectedWorldIds.Add(BrazierPrefix + flag); MarkProgressDirty(); }
  internal void TickBraziers() { if (Hero && ActiveTerrain && !GetComponent<OpenWorldQuestBrazierRuntime>()) gameObject.AddComponent<OpenWorldQuestBrazierRuntime>(); }
 }
 public sealed class OpenWorldQuestBrazierRuntime : MonoBehaviour {
  OpenWorldManager gm;
  readonly List<GameObject> braziers = new List<GameObject>();
  readonly List<OpenWorldUpdraft> vents = new List<OpenWorldUpdraft>();
  bool built;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (!gm.NPCs.Exists(n => n && n.NpcId == "glm-q-brazier")) {
    var npc = OpenWorldNPC.Spawn("glm-q-brazier", "Sahra", "Sunspire Flamekeeper", "The dunes respect those who keep their fires.", 4, -1, new Vector3(322f, 0f, -44f), new Vector3(324f, 0f, -41f), gm.WorldRoot);
    if (npc) gm.NPCs.Add(npc);
   }
   if (gm.IsModalOpen || gm.Hero.Dead) return;
   int stage = gm.BrazierStage;
   if (stage == 1) {
    if (!built) Build();
    for (int i = 0; i < braziers.Count; i++) braziers[i].SetActive(!gm.CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit" + (i + 1)));
    // Ember cast near an unlit bowl lights it: the hero's cast state is the detection, the element
    // decides the outcome (frost/gale casts leave the bowl cold).
    if (gm.Hero.ActiveElement == 0 && gm.Hero.Visual && gm.Hero.Visual.CurrentState == "cast_ember") {
     for (int i = 0; i < braziers.Count; i++) {
      if (!braziers[i].activeSelf) continue;
      if (Vector3.Distance(gm.Hero.transform.position, braziers[i].transform.position) < 4.5f) { gm.LightBrazier(i + 1); OpenWorldVfx.Attach("fire_bonfire", braziers[i].transform, Vector3.up * .4f, 1.1f, null); break; }
     }
    }
   } else if (built) {
    foreach (var b in braziers) if (b) b.SetActive(false);
    foreach (var v in vents) if (v) v.gameObject.SetActive(false);
   }
  }
  void Build() {
   built = true;
   for (int i = 0; i < 3; i++) {
    Vector3 spot = gm.HuntGround(OpenWorldManager.BrazierSpots[i]);
    if (OpenWorldTerrainSculptor.WaterAt(spot.x, spot.z, out _, out _)) spot = gm.HuntGround(spot + new Vector3(6f, 0f, 6f));
    var root = new GameObject("Sunspire brazier " + (i + 1)); root.transform.SetParent(gm.WorldRoot, false); root.transform.position = spot;
    // Stone pillar so the bowl needs the vent + glider to reach.
    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pillar.transform.SetParent(root.transform, false);
    pillar.transform.localPosition = Vector3.up * 3.25f; pillar.transform.localScale = new Vector3(1.7f, 3.25f, 1.7f);
    pillar.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Brazier pillar", new Color(.42f, .3f, .22f));
    var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bowl.transform.SetParent(root.transform, false);
    bowl.transform.localPosition = Vector3.up * 6.6f; bowl.transform.localScale = new Vector3(1.9f, .35f, 1.9f);
    bowl.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Brazier bowl", new Color(.16f, .13f, .12f));
    var label = new GameObject("Brazier label"); label.transform.SetParent(root.transform, false); label.transform.localPosition = Vector3.up * 7.6f;
    var text = label.AddComponent<TextMesh>(); text.text = "WATCH BRAZIER " + (i + 1) + "\n[LIGHT WITH EMBER]"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .05f; text.fontSize = 30; text.color = new Color(1f, .72f, .3f);
    braziers.Add(root);
    var vent = OpenWorldUpdraft.Spawn("SUNSPIRE UPDRAFT", spot + new Vector3(2.4f, 0f, 2.4f), gm.WorldRoot, 23.5f);
    vents.Add(vent);
   }
  }
  public Vector3 NextUnlit(Vector3 from) {
   Vector3 best = from; float bestD = float.MaxValue;
   for (int i = 0; i < 3; i++) {
    if (gm.CollectedWorldIds.Contains(OpenWorldManager.BrazierPrefix + "lit" + (i + 1))) continue;
    float d = Vector3.Distance(from, gm.HuntGround(OpenWorldManager.BrazierSpots[i]));
    if (d < bestD) { bestD = d; best = gm.HuntGround(OpenWorldManager.BrazierSpots[i]); }
   }
   return best;
  }
  void OnDestroy() {
   foreach (var b in braziers) if (b) Destroy(b);
   foreach (var v in vents) if (v) Destroy(v.gameObject);
  }
 }
}
