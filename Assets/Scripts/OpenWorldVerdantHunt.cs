using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LostRealmsOW {
 public sealed partial class OpenWorldManager {
  const string HuntPrefix = "verdant-hunt:";
  public int VerdantHuntStage => CollectedWorldIds.Contains(HuntPrefix + "reward") ? 5 : CollectedWorldIds.Contains(HuntPrefix + "cooked") ? 4 : CollectedWorldIds.Contains(HuntPrefix + "slain") ? 3 : CollectedWorldIds.Contains(HuntPrefix + "trail2") ? 2 : CollectedWorldIds.Contains(HuntPrefix + "accepted") ? 1 : 0;
  public Vector3 VerdantHuntTarget {
   get {
    if (VerdantHuntStage == 0 || VerdantHuntStage >= 4) { var npc = NPCs.Find(n => n && n.NpcId == "kael"); return npc ? npc.transform.position : new Vector3(44, 0, 38.5f); }
    if (VerdantHuntStage == 3) { var fire = Campfires.Find(f => f && f.CampfireTitle == "OAKVALE CAMPFIRE"); return fire ? fire.transform.position : new Vector3(41.5f, 0, 40.5f); }
    return HuntGround(VerdantHuntStage == 2 ? new Vector3(118, 0, 64) : CollectedWorldIds.Contains(HuntPrefix + "trail1") ? new Vector3(90, 0, 44) : new Vector3(63, 0, 33));
   }
  }
  public string VerdantHuntObjective => VerdantHuntStage == 0 ? "Speak to Warden Kael in Oakvale" : VerdantHuntStage == 1 ? (CollectedWorldIds.Contains(HuntPrefix + "trail1") ? "Follow the tracks into the woodland" : "Inspect tracks beyond Oakvale") : VerdantHuntStage == 2 ? "Defeat Thornhide at the woodland clearing" : VerdantHuntStage == 3 ? "Cook a meal at Oakvale's campfire" : VerdantHuntStage == 4 ? "Return to Warden Kael for your reward" : "Oakvale's trail is safe again";
  public Vector3 HuntGround(Vector3 p) { p.y = ActiveTerrain ? ActiveTerrain.SampleHeight(p) + ActiveTerrain.transform.position.y : OpenWorldMaterialHelper.SampleGroundHeight(p); return p; }
  void SetHuntFlag(string flag) { CollectedWorldIds.Add(HuntPrefix + flag); MarkProgressDirty(); }
  public bool ConverseVerdantHunt() {
   if (VerdantHuntStage == 0) {
    SetHuntFlag("accepted"); ShowBanner("KAEL: Thornhide has driven the hunters from our trail. Follow its tracks, bring it down, and prepare a meal for the village.");
   } else if (VerdantHuntStage == 4) {
    SetHuntFlag("reward"); Gold += 120; Gems += 5; UnlockedWeapons.Add(4);
    ShowBanner("A HUNTER'S PROMISE COMPLETE • 120 gold, 5 gems • Ranger Axe unlocked"); OpenWorldAudio.Play("sfx_complete");
   } else if (VerdantHuntStage == 5) ShowBanner("KAEL: Our hunters are back on the trail. Oakvale will remember your help, Aster.");
   else ShowBanner("KAEL: " + VerdantHuntObjective + ". Watch its warning, dodge the sweep, then strike while it recovers.");
   return true;
  }
  public bool NearVerdantTracks => VerdantHuntStage == 1 && Hero && Vector3.Distance(Hero.transform.position, VerdantHuntTarget) < 3f;
  public bool InspectVerdantTracks() {
   if (!NearVerdantTracks) return false;
   if (!CollectedWorldIds.Contains(HuntPrefix + "trail1")) { SetHuntFlag("trail1"); ShowBanner("Deep claw marks, fresh tracks… Thornhide went farther into the woods."); }
   else { SetHuntFlag("trail2"); ShowBanner("Broken branches and a low growl • Thornhide is near. Prepare for a fight."); }
   OpenWorldAudio.Play("sfx_power_select", .35f); return true;
  }
  public void RecordVerdantQuarry(OpenWorldEnemy enemy) {
   if (VerdantHuntStage != 2 || !enemy || !enemy.GetComponent<VerdantHuntQuarry>()) return;
   SetHuntFlag("slain"); ShowBanner("THORNHIDE DEFEATED • Take the fresh meat to Oakvale's campfire.");
  }
  public void RecordVerdantMeal(OpenWorldCampfire fire) {
   if (VerdantHuntStage == 3 && fire && fire.CampfireTitle == "OAKVALE CAMPFIRE") { SetHuntFlag("cooked"); ShowBanner("A meal for Oakvale • Return to Warden Kael."); }
  }
  public bool NearVerdantCampfire {
   get { var fire = Campfires.Find(f => f && f.CampfireTitle == "OAKVALE CAMPFIRE"); return VerdantHuntStage == 3 && Hero && fire && Vector3.Distance(Hero.transform.position,fire.transform.position) < 3f; }
  }
  public bool TryCookVerdantMeal() {
   if (!NearVerdantCampfire) return false;
   Campfires.Find(f => f && f.CampfireTitle == "OAKVALE CAMPFIRE").CookMeal(this);
   SaveAdventure(); return true;
  }
  void TickVerdantHunt() {
   if (!Hero || !ActiveTerrain) return;
   if (!GetComponent<OpenWorldVerdantHuntRuntime>()) gameObject.AddComponent<OpenWorldVerdantHuntRuntime>();
  }
 }
 public sealed class VerdantHuntQuarry : MonoBehaviour { }
 public sealed class OpenWorldVerdantHuntRuntime : MonoBehaviour {
  OpenWorldManager gm;
  OpenWorldEnemy quarry;
  GameObject track;
  Material trackMaterial;
  AsyncOperationHandle<GameObject> bear;
  bool loading;
  float nextAttempt;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (gm.IsModalOpen || gm.Hero.Dead) return;
   if (gm.VerdantHuntStage == 1) {
    if (!track) BuildTracks();
    track.SetActive(true); track.transform.position = gm.VerdantHuntTarget;
   } else if (track) track.SetActive(false);
   if (gm.VerdantHuntStage != 2) { if (quarry && !quarry.Dead) { gm.Enemies.Remove(quarry); Destroy(quarry.gameObject); } return; }
   float d = Vector3.Distance(gm.Hero.transform.position, gm.VerdantHuntTarget);
   if (d > 120f && quarry) { gm.Enemies.Remove(quarry); Destroy(quarry.gameObject); }
   if (d < 85f && !quarry && !loading && Time.time > nextAttempt) StartCoroutine(SpawnQuarry());
  }
  IEnumerator SpawnQuarry() {
   loading = true; nextAttempt = Time.time + 15f;
   if (!bear.IsValid()) bear = Addressables.LoadAssetAsync<GameObject>("ow.imported.expansion_bear");
   yield return bear;
   loading = false;
   if (!gm || gm.VerdantHuntStage != 2) yield break;
   if (bear.Status != AsyncOperationStatus.Succeeded || !bear.Result) {
    Debug.LogWarning("[VerdantHunt] Bear content unavailable; rebuild matching Addressables content.");
    if (bear.IsValid()) Addressables.Release(bear); bear = default; yield break;
   }
   Vector3 site = gm.VerdantHuntTarget; bool found = false;
   for (int i = 0; i < 20; i++) {
    Vector3 p = gm.HuntGround(site + Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * (i == 0 ? 0 : 2 + i * .5f));
    if (OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _) || OpenWorldPopulation.Blocked(p, 5f, 3.5f)) continue;
    if (!OpenWorldGrounding.TryWalk(gm.ActiveTerrain, p, p + Vector3.right * .2f, out _)) continue;
    site = p; found = true; break;
   }
   if (!found) { Debug.LogWarning("[VerdantHunt] Clearing blocked; retrying later."); yield break; }
   quarry = OpenWorldEnemy.Spawn("StoneBrute", "Thornhide, Terror of Oakvale", site, gm.WorldRoot, importedPrefab: bear.Result, importedHeight: 1.8f);

   quarry.Beast = quarry.Territorial = true; quarry.BodyHeight = 1.8f;
   quarry.SpecialName = "MAULING SWEEP"; quarry.MeatDrop = 3;
   quarry.MaxHealth = quarry.Health = 24f; quarry.AttackDamage = 1.2f;
   quarry.MoveSpeed = 4.3f; quarry.AttackRange = 2.5f; quarry.WarnRadius = 18f; quarry.AggroRadius = 10f; quarry.LeashRadius = 28f;
   quarry.Controller.height = 1.8f; quarry.Controller.center = Vector3.up * .9f; quarry.Controller.radius = .7f;
   quarry.gameObject.AddComponent<VerdantHuntQuarry>(); gm.Enemies.Add(quarry);
  }
  void BuildTracks() {
   track = new GameObject("Thornhide tracks"); track.transform.SetParent(gm.WorldRoot, false);
   trackMaterial = OpenWorldMaterialHelper.CreateLit("Hunt tracks", new Color(.12f, .08f, .035f));
   for (int i = 0; i < 6; i++) {
    var print = GameObject.CreatePrimitive(PrimitiveType.Cylinder); print.transform.SetParent(track.transform, false);
    print.transform.localPosition = new Vector3((i % 2 == 0 ? -.25f : .25f), .035f, i * .35f - .8f);
    print.transform.localScale = new Vector3(.22f, .015f, .32f); Destroy(print.GetComponent<Collider>());
    print.GetComponent<Renderer>().sharedMaterial = trackMaterial;
   }
   var label = new GameObject("Fresh claw tracks"); label.transform.SetParent(track.transform, false); label.transform.localPosition = Vector3.up * 1.2f;
   var text = label.AddComponent<TextMesh>(); text.text = "FRESH TRACKS"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .045f; text.fontSize = 32; text.color = new Color(1f, .8f, .35f);
  }
  void LateUpdate() {
   if (gm && gm.Hero && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateVerdantHuntPanel(gm);
   if (!track || !track.activeSelf) return;
   var label = track.transform.Find("Fresh claw tracks");
   label.gameObject.SetActive(Vector3.Distance(gm.Hero.transform.position, track.transform.position) < 18f);
   if (Camera.main) label.rotation = Camera.main.transform.rotation;
  }
  void OnDestroy() {
   if (quarry) Destroy(quarry.gameObject); if (track) Destroy(track); if (trackMaterial) Destroy(trackMaterial);
   if (bear.IsValid()) Addressables.Release(bear);
  }
 }
}

