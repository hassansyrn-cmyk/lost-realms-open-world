using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Part C (brief Docs/TASKS_GLM_2026-10-30.md): quiet discoveries for the four regions Codex's Verdant
 // route does not cover — a story note and a hidden cache near each region's quest area, but off its
 // paths. Notes are readable from a few metres (TextMesh markers, like the hunt tracks); caches are kit
 // props holding a one-time reward. Progress is saved in CollectedWorldIds under the glm-disc: prefix,
 // so every reward pays exactly once per save.
 public sealed class OpenWorldRegionDiscoveriesRuntime : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldRegionDiscoveriesRuntime>()) new GameObject("Region discoveries").AddComponent<OpenWorldRegionDiscoveriesRuntime>(); }
  void Update() {
   var gm = OpenWorldManager.I;
   if (gm && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.TickRegionDiscoveries(gm);
  }
 }

 public struct RegionDiscovery {
  public string Id, Title, Note, Region;
  public Vector3 Position;
  public int Gold, Gems;
  public string PropResource, PropTexture;
  public bool Cache; // caches hold the reward inside a prop; notes are free-standing lore
 }

 public sealed partial class OpenWorldMobileHud {
  GameObject discRoot;
  readonly List<GameObject> discMarkers = new List<GameObject>();
  static readonly RegionDiscovery[] Discoveries = {
   // Frozen Peaks — off the sled path, overlooking Frostpine.
   new RegionDiscovery { Id = "glm-disc:frozen-note", Region = "Frozen Peaks", Title = "SCOUT'S LAST LETTER",
    Note = "The sled tracks end here, but the scout's letter went farther: 'The peak remembers every name. Leave yours and it may let you pass.'",
    Position = new Vector3(-160f, 0f, 288f), Gold = 20, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:frozen-cache", Region = "Frozen Peaks", Title = "FROZEN SUPPLY CACHE",
    Note = "A crate buried under the drifts — spare rations and a handful of gems, still frozen solid.",
    Position = new Vector3(-96f, 0f, 300f), Gold = 30, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Burning Dunes — between Sunspire and the ruins, away from the brazier pillars.
   new RegionDiscovery { Id = "glm-disc:dunes-note", Region = "Burning Dunes", Title = "CARAVAN LEDGER PAGE",
    Note = "A ledger page baked half-legible: 'Paid the Sunspire keepers in water and silence. The dunes take both eventually.'",
    Position = new Vector3(296f, 0f, -8f), Gold = 20, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:dunes-cache", Region = "Burning Dunes", Title = "SUNKEN CARAVAN STRONGBOX",
    Note = "A strongbox lost when the caravan cut east. The lock rusted open generations ago.",
    Position = new Vector3(372f, 0f, 88f), Gold = 35, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Hollow Marches — outside the graveyard fence line, away from the lantern circle.
   new RegionDiscovery { Id = "glm-disc:marches-note", Region = "Hollow Marches", Title = "MOURNER'S VOW",
    Note = "Scratched into the marker: 'I lit four lanterns so the fifth could rest. Whoever finds this — let them all rest.'",
    Position = new Vector3(-190f, 0f, -206f), Gold = 25, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:marches-cache", Region = "Hollow Marches", Title = "MOURNER'S OFFERING",
    Note = "An offering box the revenants never touched. Perhaps they approved of what it held.",
    Position = new Vector3(-238f, 0f, -244f), Gold = 35, Gems = 2, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Stormveil — below the overlook, away from the lightning rods.
   new RegionDiscovery { Id = "glm-disc:storm-note", Region = "Stormveil", Title = "STORMWARDEN'S TEST",
    Note = "A weathered plate: 'Rhega's test is not the storm. The storm is honest. The test is whether you climb back up.'",
    Position = new Vector3(222f, 0f, -250f), Gold = 25, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:storm-cache", Region = "Stormveil", Title = "LIGHTNING-SEALED CHEST",
    Note = "A chest struck so many times the bolts fused shut — then burst open one night. The storm got there first and left the best parts.",
    Position = new Vector3(258f, 0f, -296f), Gold = 40, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
  };

  internal void TickRegionDiscoveries(OpenWorldManager gm) {
   if (!gm.Hero || !gm.ActiveTerrain) return;
   if (discRoot == null) {
    discRoot = new GameObject("Region discoveries");
    discRoot.transform.SetParent(gm.WorldRoot, false);
    BuildMarkers(gm);
   }
   Vector3 hp = gm.Hero.transform.position;
   foreach (var marker in discMarkers) {
    if (!marker) continue;
    var disc = marker.GetComponent<RegionDiscoveryTag>();
    if (disc == null || gm.CollectedWorldIds.Contains(disc.Id)) { if (marker.activeSelf) marker.SetActive(false); continue; }
    float d = Vector3.Distance(hp, marker.transform.position);
    bool show = d < 30f;
    if (marker.activeSelf != show) marker.SetActive(show);
   }
  }

  void BuildMarkers(OpenWorldManager gm) {
   foreach (var disc in Discoveries) {
    if (gm.CollectedWorldIds.Contains(disc.Id)) continue;
    Vector3 spot = gm.HuntGround(disc.Position);
    if (OpenWorldTerrainSculptor.WaterAt(spot.x, spot.z, out _, out _)) spot = gm.HuntGround(spot + new Vector3(8f, 0f, 8f));
    var root = new GameObject("Discovery " + disc.Id); root.transform.SetParent(discRoot.transform, false); root.transform.position = spot;
    var tag = root.AddComponent<RegionDiscoveryTag>(); tag.Id = disc.Id; tag.Title = disc.Title; tag.Gold = disc.Gold; tag.Gems = disc.Gems; tag.Region = disc.Region;
    var model = OpenWorldMaterialHelper.SpawnModel(disc.PropResource, disc.PropTexture, root.transform, spot + Vector3.up * 0.2f, disc.Cache ? 0.9f : 1.4f, disc.Cache ? new Color(0.75f, 0.6f, 0.35f) : new Color(0.6f, 0.62f, 0.66f));
    if (model) Destroy(model.GetComponent<Collider>());
    var label = new GameObject("Marker label"); label.transform.SetParent(root.transform, false); label.transform.localPosition = Vector3.up * 1.9f;
    var text = label.AddComponent<TextMesh>(); text.text = disc.Title + "\n[READ — F]"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .05f; text.fontSize = 30;
    text.color = disc.Cache ? new Color(1f, .84f, .52f) : new Color(.8f, .88f, 1f);
    discMarkers.Add(root);
   }
  }

  // Called from the shared GLM interact hook: reads a note or opens a cache once.
  public bool TryDiscover(OpenWorldManager gm) {
   foreach (var marker in discMarkers) {
    if (!marker || !marker.activeSelf) continue;
    var disc = marker.GetComponent<RegionDiscoveryTag>();
    if (disc == null || gm.CollectedWorldIds.Contains(disc.Id)) continue;
    if (Vector3.Distance(gm.Hero.transform.position, marker.transform.position) > 3.5f) continue;
    gm.CollectedWorldIds.Add(disc.Id);
    gm.Gold += disc.Gold; gm.Gems += disc.Gems;
    gm.MarkProgressDirty();
    gm.ShowBanner(disc.Title + " • " + disc.Gold + " gold, " + disc.Gems + " gems • " + disc.Region);
    OpenWorldAudio.Play("sfx_upgrade", .5f);
    System.IO.File.AppendAllText("Docs/glm-discoveries-validation.txt", System.DateTime.Now.ToString("HH:mm:ss ") + "discovered " + disc.Id + " (+" + disc.Gold + "g +" + disc.Gems + " gems)\n");
    return true;
   }
   return false;
  }
  // Bridge check (call after warping within range): the discovery pays exactly once, its flag saves.
  public static void Discover(string id) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) throw new System.Exception("World required");
   var hud = OpenWorldMobileHud.Instance;
   if (!hud) throw new System.Exception("HUD missing");
   int gold = gm.Gold, gems = gm.Gems;
   int before = 0;
   foreach (var f in gm.CollectedWorldIds) if (f.StartsWith("glm-disc:")) before++;
   if (!hud.TryDiscover(gm)) throw new System.Exception("Discovery " + id + " did not pay (not in range or already collected)");
   if (!gm.CollectedWorldIds.Contains(id)) throw new System.Exception("Flag missing for " + id);
   if (gm.Gold - gold <= 0 && gm.Gems - gems <= 0) throw new System.Exception("No reward paid for " + id);
   if (hud.TryDiscover(gm)) throw new System.Exception("Discovery " + id + " paid twice");
   int after = 0;
   foreach (var f in gm.CollectedWorldIds) if (f.StartsWith("glm-disc:")) after++;
   if (after != before + 1) throw new System.Exception("Flag count wrong for " + id);
   FileAppendDisc("PASS: " + id + " paid once (+" + (gm.Gold - gold) + " gold, +" + (gm.Gems - gems) + " gems), flag saved");
  }
  static void FileAppendDisc(string line) {
   System.IO.File.AppendAllText("Docs/glm-discoveries-validation.txt", System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
  }
 }

 public sealed class RegionDiscoveryTag : MonoBehaviour {
  public string Id, Title;
  public int Gold, Gems;
  public string Region;
 }
}
