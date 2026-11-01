using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LostRealmsOW {
 [Serializable] public sealed class ImportedContentEntry {
  public string key = "", source = "", address = "", category = "", region = "", alias = "";
  public long triangles;
  public Vector3 position;
  public float height = 1, yaw;
  public int weaponId = -1;
 }
 [Serializable] public sealed class ImportedContentManifest { public ImportedContentEntry[] entries; }

 // Only metadata lives in Resources. Prefabs/audio remain in regional Addressables groups.
 public sealed class OpenWorldImportedContent : MonoBehaviour {
  public static OpenWorldImportedContent Instance { get; private set; }
  readonly Dictionary<string, AsyncOperationHandle<GameObject>> models = new Dictionary<string, AsyncOperationHandle<GameObject>>();
  readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
  readonly List<AsyncOperationHandle<AudioClip>> audioHandles = new List<AsyncOperationHandle<AudioClip>>();
  readonly Dictionary<string, GameObject> instances = new Dictionary<string, GameObject>();
  readonly HashSet<string> loading = new HashSet<string>();
  readonly HashSet<string> failed = new HashSet<string>();
  readonly Dictionary<string, Queue<GameObject>> effectPools = new Dictionary<string, Queue<GameObject>>();
  readonly Dictionary<string, int> effectCounts = new Dictionary<string, int>();
  int activeEffects;
  ImportedContentEntry[] entries = Array.Empty<ImportedContentEntry>();
  readonly HashSet<string> desired = new HashSet<string>();
  bool disposed;
  // Placed scenery from the legacy catalog is superseded by the baked realm's own layout.
  // The realm builds its own architecture and nature, and OpenWorldPopulation streams its creatures and
  // enemies by biome, so those legacy small-world placements are skipped there. Placed decorative
  // effects go too (a low-poly fire over the hub hearth, a lone fire on open grass): the realm's
  // campfires and camps carry their own fires.
  static bool SupersededByRealm(ImportedContentEntry e) => OpenWorldRealm.Instance && (e.category == "building" || e.category == "nature" || e.category == "mountain" || e.category == "prop" || e.category == "cloud" || e.category == "enemy" || e.category == "wildlife" || (e.category == "vfx" && string.IsNullOrEmpty(e.alias)));

  static string EnemyName(ImportedContentEntry e) {
   string s = e.source ?? "";
   if (s.Contains("Skeleton")) return "Restless Skeleton";
   if (s.Contains("Wolf")) return "Grey Wolf";
   if (s.Contains("pider")) return "Cave Spider";
   return e.alias == "brute" ? "Stone Brute" : "Wild Beast";
  }
  static bool Permanent(ImportedContentEntry e) => e.category == "audio" || e.category == "weapon" || e.category == "npc" || e.alias.StartsWith("impact_") || e.alias.StartsWith("fx_");
  public int ActiveCount => instances.Count;
  public int CatalogCount => entries.Length;
  public int FailedCount => failed.Count;
  public int PendingCount => loading.Count;
  public int ActiveEffectCount => activeEffects;
  public bool HasInstance(string key) => instances.TryGetValue(key, out var value) && value;

  public static void Attach(Transform parent) {
   var go = new GameObject("Imported Regional Content");
   go.transform.SetParent(parent, false);
   go.AddComponent<OpenWorldImportedContent>();
  }
  IEnumerator Start() {
   Instance = this;
   var data = Resources.Load<TextAsset>("World/ImportedContent");
   if (!data) yield break;
   var manifest = JsonUtility.FromJson<ImportedContentManifest>(data.text);
   entries = manifest.entries ?? Array.Empty<ImportedContentEntry>();
   while (!disposed) {
    var gm = OpenWorldManager.I;
    if (gm && gm.Hero) {
     int budget = OpenWorldGraphics.Instance && OpenWorldGraphics.Instance.High ? 48 : 28;
     desired.Clear();
     var candidates = new List<ImportedContentEntry>();
     foreach (var entry in entries) {
      if (failed.Contains(entry.key) || SupersededByRealm(entry)) continue;
      if (Permanent(entry)) { desired.Add(entry.key); continue; }
      float range = entry.category == "cloud" ? 500 : entry.category == "mountain" ? 380 : entry.category == "wildlife" || entry.category == "enemy" ? 85 : 150;
      if (Vector3.Distance(gm.Hero.transform.position, entry.position) < range) candidates.Add(entry);
     }
     candidates.Sort((a, b) => (a.position - gm.Hero.transform.position).sqrMagnitude.CompareTo((b.position - gm.Hero.transform.position).sqrMagnitude));
     long triangles = 0, triangleBudget = budget == 48 ? 650000 : 350000;
     int selected = 0;
     foreach (var entry in candidates) {
      if (selected >= budget || triangles + entry.triangles > triangleBudget) continue;
      desired.Add(entry.key); triangles += entry.triangles; selected++;
     }
     foreach (var key in new List<string>(models.Keys)) if (!desired.Contains(key) && !loading.Contains(key)) Release(key);
     foreach (var entry in entries) {
      if (desired.Contains(entry.key) && !loading.Contains(entry.key) && !models.ContainsKey(entry.key) && !sounds.ContainsKey(entry.key) && loading.Count < 2) {
       loading.Add(entry.key); StartCoroutine(Load(entry));
      }
     }
    }
    yield return new WaitForSecondsRealtime(.5f);
   }
  }
  IEnumerator Load(ImportedContentEntry entry) {
   if (entry.category == "audio") {
    var audio = Addressables.LoadAssetAsync<AudioClip>(entry.address);
    audioHandles.Add(audio);
    yield return audio;
    if (disposed || !audio.IsValid()) { loading.Remove(entry.key); yield break; }
    if (audio.Status == AsyncOperationStatus.Succeeded) sounds[entry.key] = audio.Result;
    else failed.Add(entry.key);
   } else {
    var handle = Addressables.LoadAssetAsync<GameObject>(entry.address);
    models[entry.key] = handle;
    yield return handle;
    if (disposed || !handle.IsValid()) { loading.Remove(entry.key); models.Remove(entry.key); yield break; }
    if (handle.Status != AsyncOperationStatus.Succeeded) {
     failed.Add(entry.key); Release(entry.key);
    } else if (desired.Contains(entry.key)) Spawn(entry, handle.Result);
    else Release(entry.key);
   }
   loading.Remove(entry.key);
  }
  void Spawn(ImportedContentEntry entry, GameObject prefab) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || !prefab) return;
   if (entry.alias.StartsWith("impact_") || entry.alias.StartsWith("fx_")) return;
   if (entry.category == "weapon" && !OpenWorldWeaponCatalog.Available(entry.weaponId)) return;
   if (entry.category == "weapon" && gm.Hero.EquippedWeaponId == entry.weaponId) gm.Hero.EquipWeapon(entry.weaponId);
   if (entry.category == "chest" && gm.CollectedWorldIds.Contains("chest:imported_" + entry.key)) return;
   if (entry.category == "weapon" && gm.CollectedWorldIds.Contains("imported-weapon:" + entry.key)) return;
   if (entry.category == "wildlife" && gm.CollectedWorldIds.Contains("hunted:imported:" + entry.key)) return;
   Vector3 position = entry.position;
   position.y = gm.ActiveTerrain.SampleHeight(position) + gm.ActiveTerrain.transform.position.y;
   if (entry.category == "cloud") position.y += 85;
   if (entry.category == "enemy") {
    var enemy = OpenWorldEnemy.Spawn(entry.alias == "brute" ? "StoneBrute" : "Goblin", EnemyName(entry), position, transform);
    enemy.Visual.AdoptImportedModel(prefab, entry.height);
    enemy.FitCombatSize();
    enemy.Controller.height = Mathf.Max(.95f, entry.height);
    enemy.Controller.center = Vector3.up * enemy.Controller.height * .5f;
    gm.Enemies.Add(enemy);
    instances[entry.key] = enemy.gameObject;
    return;
   }
   if (entry.category == "npc") {
    if (entry.alias == "brom" && Resources.Load<GameObject>("World/NPCs/Brom_AccuRig")) return; // Preserve the user-rigged smith.
    var npc = gm.NPCs.Find(n => n && n.NpcId == entry.alias);
    if (npc) { npc.Visual.AdoptImportedModel(prefab, entry.height); return; }
   }
   GameObject root;
   if (entry.category == "chest") {
    var chest = OpenWorldCollectible.Spawn(OWCollectibleKind.SecretChest, position, transform, 1, "EXPLORER CACHE", "chest:imported_" + entry.key);
    foreach (Transform child in chest.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    gm.Collectibles.Add(chest);
    root = chest.gameObject;
   } else root = new GameObject("Imported " + entry.key);
   root.transform.SetParent(transform, false);
   root.transform.position = position;
   root.transform.rotation = Quaternion.Euler(0, entry.yaw, 0);
   var model = Instantiate(prefab, root.transform, false);
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   if (bounds.size.y > .001f) {
    float maxWidth = entry.category == "cloud" ? 70 : entry.category == "mountain" ? 140 : entry.category == "building" ? 22 : entry.category == "nature" ? 12 : 5;
    float scale = Mathf.Min(entry.height / bounds.size.y, maxWidth / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.z)));
    if (entry.category == "weapon") scale = entry.height / Mathf.Max(.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
    model.transform.localScale *= scale;
   }
   bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
   if ((entry.category == "expansion_prop" || (entry.category == "weapon" && entry.key.StartsWith("expansion_"))) && !PlaceExpansion(root, model, entry, gm)) {
    root.SetActive(false); Destroy(root); return;
   }
   if (entry.category == "building") OpenWorldGrounding.SupportBuilding(model, gm.ActiveTerrain);
   if (entry.category == "cloud") foreach (var renderer in model.GetComponentsInChildren<Renderer>()) {
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;
   }
   if (entry.category == "wildlife") {
    root.AddComponent<OpenWorldWildlife>().Flying = entry.alias == "bird";
    if (entry.alias != "bird") OpenWorldAnimalInteraction.Attach(root, entry.source, "hunted:imported:" + entry.key);
    if (entry.alias == "bird") root.transform.position += Vector3.up * 3;
   }
   if (entry.category == "weapon") {
    var pickup = root.AddComponent<OpenWorldImportedWeaponPickup>();
    pickup.Entry = entry;
   }
   instances[entry.key] = root;
  }
  static bool PlaceExpansion(GameObject root, GameObject model, ImportedContentEntry entry, OpenWorldManager gm) {
   // Small settlement dressing only: keep doors/roads clear and reject water, steep terrain,
   // and existing colliders. Never bulldoze scenery to make an imported object fit.
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   Vector3 half = b.extents + new Vector3(.3f, 0f, .3f);
   for (int i = 0; i < 33; i++) {
    float angle = i * 2.399963f;
    float radius = i == 0 ? 0f : 1.5f + (i / 4) * 1.4f;
    Vector3 p = entry.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    p.y = gm.ActiveTerrain.SampleHeight(p) + gm.ActiveTerrain.transform.position.y;
    if (OpenWorldRealm.Instance && (OpenWorldTerrainSculptor.RoadDistance(p.x, p.z) < Mathf.Max(half.x, half.z) + 1.5f || OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _))) continue;
    if (!OpenWorldGrounding.TryWalk(gm.ActiveTerrain, p, p + Vector3.right * .2f, out float ground)) continue;
    p.y = ground;
    Vector3 center = p + Vector3.up * (half.y + .18f);
    bool blocked = false;
    foreach (var c in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) {
     if (c is TerrainCollider || c.transform.IsChildOf(root.transform)) continue;
     blocked = true; break;
    }
    if (blocked) continue;
    root.transform.position = p;
    if (entry.category == "weapon") model.transform.position += Vector3.up * .7f;
    else OpenWorldGrounding.SupportBuilding(model, gm.ActiveTerrain);
    return true;
   }
   Debug.Log("[Expansion] No clear placement for " + entry.key + "; skipped.");
   return false;
  }
  public static GameObject Prefab(string key) {
   if (Instance && Instance.models.TryGetValue(key, out var handle) && handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded) return handle.Result;
   return null;
  }
  public static AudioClip Sound(string alias) {
   if (!Instance) return null;
   if ((alias == "sfx_step" || alias == "sfx_step2") && OpenWorldManager.I && OpenWorldManager.I.Hero && OpenWorldManager.I.Hero.transform.position.z > 110) alias = "sfx_step_snow";
   foreach (var entry in Instance.entries) if (entry.category == "audio" && entry.alias == alias && Instance.sounds.TryGetValue(entry.key, out var clip)) return clip;
   return null;
  }
  public static bool Emit(string alias, Vector3 position) => Emit(alias, position, Quaternion.identity, 1f);
  public static bool Emit(string alias, Vector3 position, Quaternion rotation, float scale) {
   if (!Instance) return false;
   if (alias == "fx_dust" && Instance.activeEffects >= 1) return true;
   foreach (var entry in Instance.entries) {
    if (entry.category != "vfx" || entry.alias != alias) continue;
    var prefab = Prefab(entry.key);
    if (!prefab) return false;
    // Suppress fallback effects when the visual budget is full as well.
    if (Instance.activeEffects < 4) Instance.StartCoroutine(Instance.Effect(entry.key, prefab, position, rotation, scale));
    return true;
   }
   return false;
  }
  IEnumerator Effect(string key, GameObject prefab, Vector3 position, Quaternion rotation, float scale) {
   if (!effectPools.TryGetValue(key, out var pool)) { pool = new Queue<GameObject>(); effectPools[key] = pool; }
   GameObject effect;
   if (pool.Count > 0) effect = pool.Dequeue();
   else {
    effectCounts.TryGetValue(key, out int count);
    if (count >= 4) yield break;
    effect = Instantiate(prefab, transform);
    effectCounts[key] = count + 1;
   }
   activeEffects++;
   effect.transform.SetPositionAndRotation(position, rotation);
   effect.transform.localScale = prefab.transform.localScale * Mathf.Clamp(scale, .3f, 2f);
   effect.SetActive(true);
   float lifetime = .1f;
   foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true)) {
    var main = system.main;
    lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    system.Play(false);
   }
   yield return new WaitForSeconds(Mathf.Clamp(lifetime, .2f, 6f));
   if (effect) { effect.SetActive(false); pool.Enqueue(effect); }
   activeEffects--;
  }
  public static void ReserveSites(OpenWorldManager gm) {
   var data = Resources.Load<TextAsset>("World/ImportedContent");
   if (!data) return;
   foreach (var entry in JsonUtility.FromJson<ImportedContentManifest>(data.text).entries) {
    if (SupersededByRealm(entry)) continue;
    if (entry.category == "building" || entry.category == "chest" || entry.category == "weapon") gm.ReserveSpot(entry.position, entry.category == "building" ? 12 : 3);
   }
  }
  void Release(string key) {
   if (instances.TryGetValue(key, out var instance)) {
    if (instance) {
     var gm = OpenWorldManager.I;
     if (gm) { gm.Enemies.Remove(instance.GetComponent<OpenWorldEnemy>()); gm.Collectibles.Remove(instance.GetComponent<OpenWorldCollectible>()); }
     instance.SetActive(false); Destroy(instance);
    }
    instances.Remove(key);
   }
   if (models.TryGetValue(key, out var handle)) { if (handle.IsValid()) Addressables.Release(handle); models.Remove(key); }
  }
  void OnDestroy() {
   disposed = true;
   OpenWorldGrounding.ReleaseMaterials();
   if (Instance == this) Instance = null;
   foreach (var handle in models.Values) if (handle.IsValid()) Addressables.Release(handle);
   foreach (var handle in audioHandles) if (handle.IsValid()) Addressables.Release(handle);
   models.Clear(); instances.Clear(); sounds.Clear();
  }
 }
 public sealed class OpenWorldImportedWeaponPickup : MonoBehaviour {
  static readonly List<OpenWorldImportedWeaponPickup> pickups = new List<OpenWorldImportedWeaponPickup>();
  public ImportedContentEntry Entry;
  TextMesh label;
  void OnEnable() { pickups.Add(this); }
  void OnDisable() { pickups.Remove(this); }
  void Start() {
   if (Entry == null || !Entry.key.StartsWith("expansion_")) return;
   var go = new GameObject("Discoverable weapon label"); go.transform.SetParent(transform, false);
   go.transform.localPosition = Vector3.up * 1.3f;
   label = go.AddComponent<TextMesh>(); label.text = OpenWorldWeaponCatalog.Get(Entry.weaponId).Name.Replace("CARAVAN ", "");
   label.anchor = TextAnchor.MiddleCenter; label.fontSize = 32; label.characterSize = .035f;
   label.color = new Color(1f, .85f, .35f);
  }
  void Update() {
   var gm = OpenWorldManager.I;
   if (label && gm && gm.Hero) {
    float distance = Vector3.Distance(gm.Hero.transform.position, transform.position);
    bool nearest = distance < 12f;
    foreach (var other in pickups) {
     if (other && other != this && Vector3.Distance(gm.Hero.transform.position, other.transform.position) < distance) { nearest = false; break; }
    }
    label.gameObject.SetActive(nearest);
    if (Camera.main) label.transform.rotation = Camera.main.transform.rotation;
   }
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen || Vector3.Distance(gm.Hero.transform.position, transform.position) > 2) return;
   if (gm.CollectedWorldIds.Add("imported-weapon:" + Entry.key)) {
    gm.UnlockedWeapons.Add(Entry.weaponId);
    gm.Hero.EquipWeapon(Entry.weaponId);
    gm.MarkProgressDirty();
    gm.ShowBanner("Weapon discovered: " + OpenWorldWeaponCatalog.Get(Entry.weaponId).Name);
    OpenWorldAudio.Play("sfx_weapon_pickup");
   }
   gameObject.SetActive(false);
  }
 }
}
