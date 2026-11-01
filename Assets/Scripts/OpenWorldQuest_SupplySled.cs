using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LostRealmsOW {
 // "The Lost Supply Sled" (Frozen Peaks side quest, GLM Phase 2): Matthias, Frostpine's provisioner,
 // sends Aster after a supply sled that never arrived. Follow the tracks north, drive off the wolf pack
 // that claimed it, collect the supplies and bring word back to Frostpine.
 public sealed partial class OpenWorldManager {
  internal const string SledPrefix = "glm-sled:";
  public static readonly Vector3 SledSite = new Vector3(-184f, 0f, 290f);
  public static readonly Vector3 SledTracks = new Vector3(-178f, 0f, 282f);
  public int SledStage => CollectedWorldIds.Contains(SledPrefix + "reward") ? 5 : CollectedWorldIds.Contains(SledPrefix + "collected") ? 4 : CollectedWorldIds.Contains(SledPrefix + "pack") ? 3 : CollectedWorldIds.Contains(SledPrefix + "tracks") ? 2 : CollectedWorldIds.Contains(SledPrefix + "accepted") ? 1 : 0;
  public Vector3 SledTarget {
   get {
    if (SledStage == 0 || SledStage >= 4) { var npc = NPCs.Find(n => n && n.NpcId == "glm-q-sled"); return npc ? npc.transform.position : SledSite; }
    if (SledStage == 3) return HuntGround(SledSite);
    return HuntGround(SledStage == 2 ? SledSite : SledTracks);
   }
  }
  public string SledObjective => SledStage == 0 ? "Speak to Matthias in Frostpine" : SledStage == 1 ? (CollectedWorldIds.Contains(SledPrefix + "tracks") ? "Follow the tracks up the ridge" : "Inspect the sled tracks north of Frostpine") : SledStage == 2 ? "Drive the wolf pack off the supply sled" : SledStage == 3 ? "Collect the scattered supplies" : SledStage == 4 ? "Bring word back to Matthias in Frostpine" : "The supply sled is home";
  public bool ConverseSled() {
   if (SledStage == 0) { SetSledFlag("accepted"); ShowBanner("MATTHIAS: A supply sled never reached Frostpine. Follow the tracks north — and mind the wolves, they have claimed worse cargoes than ours."); }
   else if (SledStage == 4) {
    SetSledFlag("reward"); Gold += 110; Gems += 4; UnlockedWeapons.Add(4);
    ShowBanner("THE LOST SUPPLY SLED COMPLETE • 110 gold, 4 gems • Ranger Axe unlocked"); OpenWorldAudio.Play("sfx_complete");
   } else if (SledStage == 5) ShowBanner("MATTHIAS: Frostpine eats well tonight thanks to you, Aster.");
   else ShowBanner("MATTHIAS: " + SledObjective + ".");
   return true;
  }
  public bool NearSledTracks => SledStage == 1 && Hero && Vector3.Distance(Hero.transform.position, SledTarget) < 3f;
  public bool InteractSledTracks() {
   if (!NearSledTracks) return false;
   SetSledFlag("tracks"); ShowBanner("Deep paw prints and a dragged runner • the pack dragged the sled up the ridge.");
   OpenWorldAudio.Play("sfx_power_select", .35f); return true;
  }
  public bool NearSledSupplies => SledStage == 3 && Hero && Vector3.Distance(Hero.transform.position, SledTarget) < 3.5f;
  public bool InteractSledSupplies() {
   if (!NearSledSupplies) return false;
   SetSledFlag("collected"); ShowBanner("Supplies gathered • return to Matthias in Frostpine.");
   OpenWorldAudio.Play("sfx_complete", .5f); return true;
  }
  public void DefeatSledWolf(OpenWorldEnemy enemy) {
   if (SledStage != 2 || !enemy || !enemy.GetComponent<SledWolf>()) return;
   var wolves = FindObjectsByType<SledWolf>(FindObjectsInactive.Exclude);
   int left = 0; foreach (var w in wolves) if (w && w.GetComponent<OpenWorldEnemy>() && !w.GetComponent<OpenWorldEnemy>().Dead) left++;
   if (left == 0) { SetSledFlag("pack"); ShowBanner("PACK SCATTERED • The sled is yours. Gather the supplies."); }
   else ShowBanner($"Frostfang wolves scattered • {left} still hold the sled.");
  }
  void SetSledFlag(string flag) { CollectedWorldIds.Add(SledPrefix + flag); MarkProgressDirty(); }
  internal void TickSled() { if (Hero && ActiveTerrain && !GetComponent<OpenWorldQuestSledRuntime>()) gameObject.AddComponent<OpenWorldQuestSledRuntime>(); }
 }
 public sealed class SledWolf : MonoBehaviour { }
 public sealed class OpenWorldQuestSledRuntime : MonoBehaviour {
  OpenWorldManager gm;
  GameObject track, sled;
  Material trackMaterial;
  AsyncOperationHandle<GameObject> crate, wolfAsset;
  readonly List<OpenWorldEnemy> wolves = new List<OpenWorldEnemy>();
  bool loading;
  float nextAttempt;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (!gm.NPCs.Exists(n => n && n.NpcId == "glm-q-sled")) {
    var npc = OpenWorldNPC.Spawn("glm-q-sled", "Matthias", "Frostpine Provisioner", "Winter is long, and the sled is late.", 3, -1, new Vector3(-146f, 0f, 258f), new Vector3(-143f, 0f, 261f), gm.WorldRoot);
    if (npc) gm.NPCs.Add(npc);
   }
   if (gm.IsModalOpen || gm.Hero.Dead) return;
   int stage = gm.SledStage;
   if (stage == 1) {
    if (!track) BuildTracks();
    track.SetActive(true); track.transform.position = gm.SledTarget;
   } else if (track) track.SetActive(false);
   float d = Vector3.Distance(gm.Hero.transform.position, gm.SledTarget);
   if (stage >= 2 && stage <= 3) {
    if (!sled && !loading && Time.time > nextAttempt && d < 120f) StartCoroutine(SpawnSled());
    if (sled) sled.SetActive(true);
   } else if (sled) sled.SetActive(false);
   if (stage == 2) {
    if (d > 130f) ReleaseWolves();
    else if (wolves.Count == 0 && sled && !loading && Time.time > nextAttempt) StartCoroutine(SpawnPack());
   } else ReleaseWolves();
  }
  IEnumerator SpawnSled() {
   loading = true; nextAttempt = Time.time + 15f;
   if (!crate.IsValid()) crate = Addressables.LoadAssetAsync<GameObject>("ow.imported.expansion_caravan_crate");
   yield return crate;
   loading = false;
   if (!gm || gm.SledStage < 2 || gm.SledStage > 3) yield break;
   if (crate.Status != AsyncOperationStatus.Succeeded || !crate.Result) {
    Debug.LogWarning("[GlmSled] Sled content unavailable; rebuild matching Addressables content.");
    if (crate.IsValid()) Addressables.Release(crate); crate = default; yield break;
   }
   Vector3 site = gm.SledTarget; bool found = false;
   for (int i = 0; i < 20; i++) {
    Vector3 p = gm.HuntGround(site + Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * (i == 0 ? 0 : 2 + i * .5f));
    if (OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _) || OpenWorldPopulation.Blocked(p, 4f, 3f)) continue;
    if (!OpenWorldGrounding.TryWalk(gm.ActiveTerrain, p, p + Vector3.right * .2f, out _)) continue;
    site = p; found = true; break;
   }
   if (!found) { Debug.LogWarning("[GlmSled] Sled site blocked; retrying later."); yield break; }
   sled = Instantiate(crate.Result, site, Quaternion.Euler(0f, 40f, 0f), gm.WorldRoot);
   sled.name = "Lost supply sled";
   var label = new GameObject("Sled label"); label.transform.SetParent(sled.transform, false); label.transform.localPosition = Vector3.up * 1.6f;
   var text = label.AddComponent<TextMesh>(); text.text = "LOST SUPPLY SLED"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .05f; text.fontSize = 32; text.color = new Color(1f, .8f, .35f);
  }
  IEnumerator SpawnPack() {
   loading = true; nextAttempt = Time.time + 15f;
   if (!wolfAsset.IsValid()) wolfAsset = Addressables.LoadAssetAsync<GameObject>("ow.imported.enemy_10b1fc0ab2");
   yield return wolfAsset;
   loading = false;
   if (!gm || gm.SledStage != 2) yield break;
   if (wolfAsset.Status != AsyncOperationStatus.Succeeded || !wolfAsset.Result) { if (wolfAsset.IsValid()) Addressables.Release(wolfAsset); wolfAsset = default; yield break; }
   Vector3 site = gm.SledTarget;
   for (int i = 0; i < 3; i++) {
    Vector3 p = gm.HuntGround(site + Quaternion.Euler(0, i * 120f + 30f, 0) * Vector3.forward * 4.5f);
    var wolf = OpenWorldEnemy.Spawn("Goblin", "Frostfang Wolf", p, gm.WorldRoot);
    wolf.Visual.AdoptImportedModel(wolfAsset.Result, 1.15f);
    wolf.Beast = wolf.Territorial = true; wolf.BodyHeight = 1.15f;
    wolf.MaxHealth = wolf.Health = 8f; wolf.AttackDamage = .9f;
    wolf.MoveSpeed = 5.4f; wolf.AttackRange = 2.2f; wolf.WarnRadius = 16f; wolf.AggroRadius = 11f; wolf.LeashRadius = 30f;
    wolf.Controller.height = 1.15f; wolf.Controller.center = Vector3.up * .58f; wolf.Controller.radius = .38f;
    wolf.gameObject.AddComponent<SledWolf>();
    gm.Enemies.Add(wolf); wolves.Add(wolf);
   }
  }
  void ReleaseWolves() {
   for (int i = wolves.Count - 1; i >= 0; i--) {
    var w = wolves[i];
    if (w) { gm.Enemies.Remove(w); if (!w.Dead) Destroy(w.gameObject); }
    wolves.RemoveAt(i);
   }
  }
  void BuildTracks() {
   track = new GameObject("Sled tracks"); track.transform.SetParent(gm.WorldRoot, false);
   trackMaterial = OpenWorldMaterialHelper.CreateLit("Sled tracks", new Color(.35f, .32f, .3f));
   for (int i = 0; i < 6; i++) {
    var print = GameObject.CreatePrimitive(PrimitiveType.Cylinder); print.transform.SetParent(track.transform, false);
    print.transform.localPosition = new Vector3((i % 2 == 0 ? -.3f : .3f), .035f, i * .38f - .9f);
    print.transform.localScale = new Vector3(.2f, .015f, .34f); Destroy(print.GetComponent<Collider>());
    print.GetComponent<Renderer>().sharedMaterial = trackMaterial;
   }
   var runner = GameObject.CreatePrimitive(PrimitiveType.Cube); runner.transform.SetParent(track.transform, false);
   runner.transform.localPosition = new Vector3(.1f, .04f, .4f); runner.transform.localScale = new Vector3(.5f, .05f, 2.6f);
   Destroy(runner.GetComponent<Collider>()); runner.GetComponent<Renderer>().sharedMaterial = trackMaterial;
   var label = new GameObject("Sled track label"); label.transform.SetParent(track.transform, false); label.transform.localPosition = Vector3.up * 1.2f;
   var text = label.AddComponent<TextMesh>(); text.text = "SLED TRACKS"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .045f; text.fontSize = 32; text.color = new Color(1f, .8f, .35f);
  }
  void LateUpdate() {
   if (gm && gm.Hero && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateGlmQuestPanel(gm, "sled");
   if (!track || !track.activeSelf) return;
   var label = track.transform.Find("Sled track label");
   if (!label) return;
   label.gameObject.SetActive(Vector3.Distance(gm.Hero.transform.position, track.transform.position) < 18f);
   if (Camera.main) label.rotation = Camera.main.transform.rotation;
  }
  void OnDestroy() {
   ReleaseWolves();
   if (sled) Destroy(sled); if (track) Destroy(track); if (trackMaterial) Destroy(trackMaterial);
   if (crate.IsValid()) Addressables.Release(crate);
   if (wolfAsset.IsValid()) Addressables.Release(wolfAsset);
  }
 }
}
