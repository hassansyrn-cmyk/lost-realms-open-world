using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 [Serializable] public sealed class WorldKitEntry {
  public string key = "", source = "", role = "", biomes = "", tags = "", collider = "";
  public float minHeight = 1f, maxHeight = 1f, weight = 1f, align = 0f, sink = 0f, footprint = 0f;
  public Vector3 size;          // native renderer bounds of the prepared prefab (metres)
  public Vector3 center;        // native bounds centre relative to the prefab root
  public float minY;            // native bounds bottom relative to the prefab root
  public long triangles;
  public int lods;
  public string resource = "";  // Resources path of the prepared prefab
  public float life;            // vfx: seconds until the pooled instance can be recycled
  public bool loop;             // vfx: looping effect (attach/detach rather than fire-and-forget)
  public bool detail;           // terrain detail mesh (grass, flowers) rather than a placed object
 }
 [Serializable] public sealed class WorldKitManifest { public WorldKitEntry[] entries; public string generated = ""; }

 // The environment kit: prepared vendor prefabs grouped by role and biome, loaded once per world build.
 public static class OpenWorldWorldKit {
  static WorldKitManifest manifest;
  static readonly Dictionary<string, WorldKitEntry> byKey = new Dictionary<string, WorldKitEntry>();
  static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
  static readonly Dictionary<string, List<WorldKitEntry>> byRole = new Dictionary<string, List<WorldKitEntry>>();
  public static bool Loaded => manifest != null && manifest.entries != null && manifest.entries.Length > 0;
  public static int Count => Loaded ? manifest.entries.Length : 0;
  public static int LoadedPrefabCount { get; private set; }
  public static readonly List<string> Missing = new List<string>();

  public static void Load() {
   if (manifest != null) return;
   var text = Resources.Load<TextAsset>("World/WorldKit");
   manifest = text ? JsonUtility.FromJson<WorldKitManifest>(text.text) : new WorldKitManifest { entries = Array.Empty<WorldKitEntry>() };
   if (manifest.entries == null) manifest.entries = Array.Empty<WorldKitEntry>();
   foreach (var e in manifest.entries) {
    byKey[e.key] = e;
    if (!byRole.TryGetValue(e.role, out var list)) byRole[e.role] = list = new List<WorldKitEntry>();
    list.Add(e);
   }
  }

  public static void Unload() {
   prefabs.Clear();
   LoadedPrefabCount = 0;
  }

  // Forget the cached manifest (after an editor kit rebuild in the same domain).
  public static void Reload() {
   manifest = null; byKey.Clear(); byRole.Clear(); prefabs.Clear(); Missing.Clear(); LoadedPrefabCount = 0;
   Load();
  }

  public static WorldKitEntry Entry(string key) { Load(); return key != null && byKey.TryGetValue(key, out var e) ? e : null; }

  public static GameObject Prefab(string key) {
   Load();
   if (string.IsNullOrEmpty(key)) return null;
   if (prefabs.TryGetValue(key, out var go)) return go;
   var e = Entry(key);
   go = e != null && !string.IsNullOrEmpty(e.resource) ? Resources.Load<GameObject>(e.resource) : null;
   if (!go) { if (!Missing.Contains(key)) Missing.Add(key); }
   else LoadedPrefabCount++;
   prefabs[key] = go;
   return go;
  }

  public static IReadOnlyList<WorldKitEntry> Role(string role) {
   Load();
   return byRole.TryGetValue(role, out var list) ? (IReadOnlyList<WorldKitEntry>)list : Array.Empty<WorldKitEntry>();
  }

  // Entries of a role that suit a biome (and optionally carry a tag).
  public static List<WorldKitEntry> Select(string role, OWBiome biome, string tag = null) {
   var result = new List<WorldKitEntry>();
   string b = BiomeKey(biome);
   foreach (var e in Role(role)) {
    if (!string.IsNullOrEmpty(e.biomes) && e.biomes.IndexOf(b, StringComparison.Ordinal) < 0 && e.biomes.IndexOf("any", StringComparison.Ordinal) < 0) continue;
    if (tag != null && (e.tags == null || e.tags.IndexOf(tag, StringComparison.Ordinal) < 0)) continue;
    result.Add(e);
   }
   return result;
  }

  public static string BiomeKey(OWBiome biome) {
   switch (biome) {
    case OWBiome.Frozen: return "frozen";
    case OWBiome.Dunes: return "dunes";
    case OWBiome.Graveyard: return "grave";
    case OWBiome.Storm: return "storm";
    default: return "verdant";
   }
  }

  public static WorldKitEntry Pick(List<WorldKitEntry> list, System.Random rng) {
   if (list == null || list.Count == 0) return null;
   float total = 0; foreach (var e in list) total += Mathf.Max(0.001f, e.weight);
   float roll = (float)rng.NextDouble() * total;
   foreach (var e in list) { roll -= Mathf.Max(0.001f, e.weight); if (roll <= 0) return e; }
   return list[list.Count - 1];
  }

  // Uniform scale that brings the prepared prefab to a target height.
  public static float ScaleFor(WorldKitEntry e, float targetHeight) {
   if (e == null || e.size.y < 0.001f) return 1f;
   return targetHeight / e.size.y;
  }
 }
}
