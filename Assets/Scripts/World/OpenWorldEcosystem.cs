using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Grows the realm: terrain trees and bushes (GPU-instanced with LOD), detail grass and flowers,
 // and placed rocks, cliffs, forest litter and water plants. Every rule keeps things grounded:
 // nothing in roads, yards, quest clearings or water (except water plants), trunks sunk into
 // slopes, rocks aligned and buried, cliffs facing downhill on steep ground.
 public static class OpenWorldEcosystem {
  public struct Stats { public int trees, bushes, rocks, cliffs, litter, waterPlants, detailInstances; public string summary; }
  public static Stats Last;

  static System.Random rng;
  static readonly float[] bw = new float[5];
  static readonly OWOccupancy trunks = new OWOccupancy();
  static readonly OWOccupancy stones = new OWOccupancy();
  static float R() => (float)rng.NextDouble();
  static float S(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

  // ---------- context ----------
  struct Site {
   public float x, z, h, slope, forest, meadow, road, water;
   public bool wet; public OWBiome biome; public float village;
  }

  static readonly Vector2[] Settlements = {
   Vector2.zero, new Vector2(44, 42), new Vector2(-96, -104), new Vector2(84, -8), new Vector2(-150, 262), new Vector2(318, -48),
   new Vector2(292, 150), new Vector2(-212, -222)
  };

  static bool Sample(float x, float z, out Site s) {
   s = new Site { x = x, z = z };
   if (Mathf.Abs(x) > OpenWorldRegions.WorldHalf - 6f || Mathf.Abs(z) > OpenWorldRegions.WorldHalf - 6f) return false;
   s.h = OpenWorldTerrainSculptor.HeightAt(x, z);
   s.slope = OpenWorldTerrainPainter.Slope(x, z);
   s.forest = OpenWorldTerrainPainter.ForestMask(x, z);
   s.meadow = OpenWorldTerrainPainter.MeadowFlowers(x, z);
   s.road = OpenWorldTerrainSculptor.RoadDistance(x, z);
   s.wet = OpenWorldTerrainSculptor.WaterAt(x, z, out _, out _);
   s.water = s.wet ? 0f : OpenWorldTerrainSculptor.DistanceToWater(x, z);
   OpenWorldRegions.BiomeWeights(x, z, bw);
   // Pick the biome stochastically by weight so borders interleave instead of switching.
   float roll = R(), acc = 0f; int bi = 0;
   for (int i = 0; i < 5; i++) { acc += bw[i]; if (roll <= acc) { bi = i; break; } bi = i; }
   s.biome = (OWBiome)bi;
   float v = 999f;
   foreach (var c in Settlements) v = Mathf.Min(v, Vector2.Distance(new Vector2(x, z), c));
   s.village = v;
   return true;
  }

  static bool Blocked(Vector2 p, float radius) {
   if (OpenWorldSettlementPlans.InClearZone(p, radius)) return true;
   foreach (var f in OpenWorldSettlementPlans.BuildingFootprints) if (f.Contains(p, radius + 2.2f)) return true;
   if (OpenWorldTerrainPainter.PlazaWeight(p.x, p.y, out _) > 0.05f) return true;
   foreach (var pad in OpenWorldTerrainSculptor.Pads) {
    if (pad.Late) continue;
    float d = Vector2.Distance(pad.Center, p);
    if (pad.Round && d < pad.HalfSize.x * 0.75f && pad.HalfSize.x >= 12f) return true;
   }
   return false;
  }

  // ---------- species ----------
  static readonly Dictionary<string, List<WorldKitEntry>> cache = new Dictionary<string, List<WorldKitEntry>>();
  static List<WorldKitEntry> Kit(string role, OWBiome biome, string tag) {
   string key = role + "|" + biome + "|" + tag;
   if (!cache.TryGetValue(key, out var list)) {
    list = OpenWorldWorldKit.Select(role, biome, tag);
    list.RemoveAll(e => e.detail);
    cache[key] = list;
   }
   return list;
  }
  static WorldKitEntry PickKit(string role, OWBiome biome, string tag = null) {
   var list = Kit(role, biome, tag);
   if (list.Count == 0 && tag != null) list = Kit(role, biome, null);
   return OpenWorldWorldKit.Pick(list, rng);
  }

  static WorldKitEntry ChooseTree(in Site s) {
   switch (s.biome) {
    case OWBiome.Frozen:
     if (s.h < 60f && R() < 0.18f) return PickKit("tree", OWBiome.Frozen, "snowy");
     return PickKit("tree", OWBiome.Frozen, "conifer");
    case OWBiome.Dunes:
     if (s.water < 34f) return PickKit("palm", OWBiome.Dunes);
     if (Vector2.Distance(new Vector2(s.x, s.z), new Vector2(OpenWorldRegions.Sunspire.x, OpenWorldRegions.Sunspire.z)) < 110f && R() < 0.6f) return PickKit("tree", OWBiome.Dunes, "pine");
     return PickKit("deadtree", OWBiome.Dunes);
    case OWBiome.Graveyard:
     if (s.water < 16f && R() < 0.7f) return PickKit("tree", OWBiome.Graveyard, "willow");
     if (R() < 0.35f) return PickKit("deadtree", OWBiome.Graveyard);
     return PickKit("tree", OWBiome.Graveyard, "eerie");
    case OWBiome.Storm:
     float r = R();
     if (r < 0.5f) return PickKit("tree", OWBiome.Storm, "conifer");
     if (r < 0.75f) return PickKit("tree", OWBiome.Storm, "stormtouched");
     return PickKit("deadtree", OWBiome.Storm);
    default:
     if (s.water < 14f && R() < 0.55f) return PickKit("tree", OWBiome.Verdant, "willow");
     if (s.h > 30f) return R() < 0.7f ? PickKit("tree", OWBiome.Verdant, "conifer") : PickKit("tree", OWBiome.Verdant, "autumn");
     if (s.village < 80f && R() < 0.3f) return PickKit("tree", OWBiome.Verdant, "blossom");
     float v = R();
     if (v < 0.78f) return PickKit("tree", OWBiome.Verdant, "broadleaf");
     if (v < 0.9f) return PickKit("tree", OWBiome.Verdant, "conifer");
     return PickKit("tree", OWBiome.Verdant, "autumn");
   }
  }

  static float TreeDensity(in Site s) {
   switch (s.biome) {
    case OWBiome.Frozen: return (0.12f + 0.82f * Mathf.Pow(s.forest, 1.2f)) * S(150f, 108f, s.h) * (s.slope > 30f ? 0.45f : 1f);
    case OWBiome.Dunes: return s.water < 34f ? 0.5f * (1f - s.water / 34f) + 0.05f : 0.006f;
    case OWBiome.Graveyard: return 0.1f + 0.62f * s.forest;
    case OWBiome.Storm: return 0.04f + 0.42f * s.forest;
    default:
     // Dense, layered woods with clearings; open meadows keep scattered specimen trees.
     float d = 0.03f + 1.05f * Mathf.Pow(s.forest, 1.4f);
     d *= 1f - 0.8f * s.meadow;
     if (s.village < 60f) d *= 0.4f;
     return Mathf.Min(d, 0.9f);
   }
  }

  // ---------- entry point ----------
  public static void Plan(TerrainData td, List<OWPlacement> output, float[,,] splat, int seed = 4242) {
   rng = new System.Random(seed);
   cache.Clear(); trunks.Clear(); stones.Clear();
   OpenWorldWorldKit.Load();
   Last = new Stats();
   // Buildings occupy space for everything that follows.
   foreach (var f in OpenWorldSettlementPlans.BuildingFootprints) trunks.Add(f.Center, Mathf.Max(f.Half.x, f.Half.y));
   var prototypes = new List<TreePrototype>();
   var protoIndex = new Dictionary<string, int>();
   var instances = new List<TreeInstance>();
   PlantTrees(td, prototypes, protoIndex, instances);
   PlantBushes(td, prototypes, protoIndex, instances);
   td.treePrototypes = prototypes.ToArray();
   td.SetTreeInstances(instances.ToArray(), false);
   PlaceCliffs(output);
   PlaceRocks(output);
   PlaceLitter(output);
   PlaceWaterPlants(output);
   PaintDetails(td, splat);
   Last.summary = $"trees {Last.trees}, bushes {Last.bushes}, cliffs {Last.cliffs}, rocks {Last.rocks}, litter {Last.litter}, water plants {Last.waterPlants}, detail instances {Last.detailInstances}";
  }

  static int Proto(WorldKitEntry e, List<TreePrototype> prototypes, Dictionary<string, int> index) {
   if (index.TryGetValue(e.key, out int i)) return i;
   var prefab = OpenWorldWorldKit.Prefab(e.key);
   if (!prefab) return -1;
   prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
   index[e.key] = prototypes.Count - 1;
   return prototypes.Count - 1;
  }

  static TreeInstance Instance(WorldKitEntry e, int proto, float x, float z, float h, float scale) {
   float size = OpenWorldRegions.WorldHalf * 2f;
   float sink = e.sink * scale;
   return new TreeInstance {
    position = new Vector3((x + OpenWorldRegions.WorldHalf) / size, Mathf.Clamp01((h - sink - OpenWorldRegions.TerrainBase) / (OpenWorldRegions.TerrainHeight - OpenWorldRegions.TerrainBase)), (z + OpenWorldRegions.WorldHalf) / size),
    widthScale = scale, heightScale = scale * (0.95f + R() * 0.1f), rotation = R() * Mathf.PI * 2f,
    color = Color.Lerp(Color.white, new Color(0.86f, 0.9f, 0.82f), R() * 0.6f), lightmapColor = Color.white, prototypeIndex = proto
   };
  }

  static void PlantTrees(TerrainData td, List<TreePrototype> prototypes, Dictionary<string, int> index, List<TreeInstance> instances) {
   const float cell = 5.2f;
   float lim = OpenWorldRegions.WorldHalf - 10f;
   for (float z = -lim; z < lim; z += cell) for (float x = -lim; x < lim; x += cell) {
    int tries = 1;
    for (int t = 0; t < tries; t++) {
     float px = x + R() * cell, pz = z + R() * cell;
     if (!Sample(px, pz, out var s)) continue;
     if (t == 0 && s.forest > 0.65f && (s.biome == OWBiome.Verdant || s.biome == OWBiome.Frozen)) tries = 2;
     bool edge = OpenWorldRegions.EdgeMetric(px, pz) > OpenWorldRegions.PlayableHalf - 8f;
     if (s.wet || s.water < 1.6f || s.road < 2.6f || s.slope > (s.biome == OWBiome.Frozen ? 38f : 34f)) continue;
     float density = TreeDensity(s);
     if (edge) density = Mathf.Max(density, 0.18f) * (s.h < 135f ? 1f : 0.2f); // perimeter slopes stay forested up to the treeline
     if (R() > density) continue;
     var e = ChooseTree(s);
     if (e == null) continue;
     float scale = Mathf.Lerp(e.minHeight, e.maxHeight, R());
     float trunkR = Mathf.Max(0.6f, e.footprint * scale * 0.28f);
     var p = new Vector2(px, pz);
     if (!trunks.Free(p, trunkR, 6f) || Blocked(p, trunkR + 1f)) continue;
     int proto = Proto(e, prototypes, index);
     if (proto < 0) continue;
     // Sink trunks a little more on slopes so the downhill side never floats.
     float sinkExtra = Mathf.Tan(s.slope * Mathf.Deg2Rad) * trunkR * 0.6f;
     instances.Add(Instance(e, proto, px, pz, s.h - sinkExtra, scale));
     trunks.Add(p, trunkR);
     Last.trees++;
    }
   }
  }

  static void PlantBushes(TerrainData td, List<TreePrototype> prototypes, Dictionary<string, int> index, List<TreeInstance> instances) {
   const float cell = 5f;
   float lim = OpenWorldRegions.PlayableHalf;
   for (float z = -lim; z < lim; z += cell) for (float x = -lim; x < lim; x += cell) {
    float px = x + R() * cell, pz = z + R() * cell;
    if (!Sample(px, pz, out var s)) continue;
    if (s.wet || s.water < 1.2f || s.road < 1.8f || s.slope > 32f) continue;
    float d;
    switch (s.biome) {
     case OWBiome.Frozen: d = s.h < 90f ? 0.05f + 0.12f * s.forest : 0f; break;
     case OWBiome.Dunes: d = s.water < 20f ? 0.12f : 0f; break;
     case OWBiome.Graveyard: d = 0.06f + 0.14f * s.forest; break;
     case OWBiome.Storm: d = 0.04f + 0.08f * s.forest; break;
     default: d = 0.03f + 0.22f * s.forest * (1f - s.forest * 0.5f) + 0.05f * s.meadow; break; // forest edges
    }
    if (s.village < 40f) d *= 0.4f;
    if (R() > d) continue;
    var e = PickKit("bush", s.biome == OWBiome.Dunes ? OWBiome.Verdant : s.biome);
    if (e == null) continue;
    var p = new Vector2(px, pz);
    float scale = Mathf.Lerp(e.minHeight, e.maxHeight, R());
    if (!trunks.Free(p, 0.6f, 6f) || Blocked(p, 1.2f)) continue;
    int proto = Proto(e, prototypes, index);
    if (proto < 0) continue;
    instances.Add(Instance(e, proto, px, pz, s.h, scale));
    trunks.Add(p, 0.5f);
    Last.bushes++;
   }
  }

  // ---------- placed geology ----------
  // Every placed piece is grounded against its whole footprint, not just its pivot: the rotated base
  // is sampled on a 5x5 grid and the pivot drops by the worst gap, so on a slope the downhill edge
  // touches the ground and the uphill side beds into it instead of the piece hovering.
  static float GroundedPivotY(WorldKitEntry e, float x, float z, Quaternion rot, float scale, float inset) {
   float pivot = OpenWorldTerrainSculptor.HeightAt(x, z);
   float hx = Mathf.Max(0.05f, e.size.x * scale * 0.5f * inset), hz = Mathf.Max(0.05f, e.size.z * scale * 0.5f * inset);
   float worst = 0f;
   for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++) {
    var w = rot * new Vector3(hx * i * 0.5f, 0f, hz * j * 0.5f);
    float gap = pivot + w.y - OpenWorldTerrainSculptor.HeightAt(x + w.x, z + w.z);
    if (gap > worst) worst = gap;
   }
   return pivot - worst;
  }

  // How much of the bounds footprint the base actually covers: rounded boulders touch the ground
  // with a smaller patch than their widest extent; blocky cliffs and props with most of it.
  static float BaseInset(WorldKitEntry e) {
   switch (e.role) {
    case "cliff": case "mountain": return 0.78f;
    case "rock": case "rock_big": return 0.62f;
    case "rock_small": case "stone": return 0.55f;
    case "log": return 0.7f;
    default: return 0.6f;
   }
  }

  static void Place(List<OWPlacement> output, WorldKitEntry e, float x, float z, float scale, OWPlaceKind kind, float yaw, string zone, float extraSink = 0f, float alignOverride = -1f) {
   var n = OpenWorldTerrainSculptor.NormalAt(x, z);
   var up = Vector3.Slerp(Vector3.up, n, Mathf.Clamp01(alignOverride >= 0f ? alignOverride : e.align));
   var rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
   float sink = e.sink * e.size.y * scale + extraSink;
   float y = GroundedPivotY(e, x, z, rot, scale, BaseInset(e)) - sink;
   output.Add(new OWPlacement { key = e.key, pos = new Vector3(x, y, z), euler = rot.eulerAngles, scale = scale, kind = kind, zone = zone });
  }

  static void PlaceCliffs(List<OWPlacement> output) {
   // Exposed bedrock only where the ground is genuinely steep; the painted rock/cliff layers carry
   // the rest of the mountainside so outcrops read as features rather than scattered blocks.
   const float cell = 13f;
   float lim = OpenWorldRegions.WorldHalf - 12f;
   int budget = 210;
   for (float z = -lim; z < lim && budget > 0; z += cell) for (float x = -lim; x < lim && budget > 0; x += cell) {
    float px = x + R() * cell, pz = z + R() * cell;
    if (!Sample(px, pz, out var s)) continue;
    if (s.slope < 41f || s.road < 4f || s.wet || s.water < 4f) continue;
    if (OpenWorldTerrainPainter.NaturalSlope(px, pz) < 34f) continue; // cut banks are earth, not bedrock
    if (R() > 0.42f) continue;
    var p = new Vector2(px, pz);
    if (Blocked(p, 3f)) continue;
    var e = PickKit("cliff", s.biome);
    if (e == null) continue;
    float scale = Mathf.Lerp(e.minHeight, e.maxHeight, R()) * Mathf.Lerp(1.05f, 1.55f, Mathf.InverseLerp(41f, 62f, s.slope));
    float radius = e.footprint * scale * 0.62f;
    if (!stones.Free(p, radius, 18f)) continue;
    var n = OpenWorldTerrainSculptor.NormalAt(px, pz);
    float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + (R() - 0.5f) * 40f; // face downhill
    // Tilted with the slope; the footprint grounding plus the entry's own sink beds it into the hillside.
    Place(output, e, px, pz, scale, OWPlaceKind.Cliff, yaw, "wild", e.size.y * scale * 0.05f, 0.82f);
    stones.Add(p, radius);
    budget--; Last.cliffs++;
   }
   // A few great crags crown the frozen and storm summits.
   int crags = 0;
   for (int i = 0; i < 4000 && crags < 8; i++) {
    float px = (R() * 2f - 1f) * (OpenWorldRegions.WorldHalf - 30f), pz = (R() * 2f - 1f) * (OpenWorldRegions.WorldHalf - 30f);
    if (!Sample(px, pz, out var s)) continue;
    if (s.h < 105f || s.slope > 24f || (s.biome != OWBiome.Frozen && s.biome != OWBiome.Storm)) continue;
    var e = PickKit("mountain", s.biome) ?? PickKit("mountain", OWBiome.Frozen);
    if (e == null) break;
    var p = new Vector2(px, pz);
    if (!stones.Free(p, 34f, 34f) || Blocked(p, 20f)) continue;
    Place(output, e, px, pz, Mathf.Lerp(e.minHeight, e.maxHeight, R()) * 0.85f, OWPlaceKind.Cliff, R() * 360f, "wild", 0f, 0.35f);
    stones.Add(p, 30f);
    crags++;
   }
  }

  static void PlaceRocks(List<OWPlacement> output) {
   const float cell = 11f;
   float lim = OpenWorldRegions.WorldHalf - 12f;
   for (float z = -lim; z < lim; z += cell) for (float x = -lim; x < lim; x += cell) {
    float px = x + R() * cell, pz = z + R() * cell;
    if (!Sample(px, pz, out var s)) continue;
    if (s.wet || s.road < 2.2f || s.slope > 44f) continue;
    float d = 0.02f + S(16f, 34f, s.slope) * 0.13f;
    if (s.biome == OWBiome.Storm) d += 0.1f;
    if (s.biome == OWBiome.Frozen) d += 0.04f;
    if (s.biome == OWBiome.Dunes) d = d * 0.5f + (s.slope > 12f ? 0.06f : 0f);
    if (s.water < 10f) d += 0.06f;
    if (s.village < 45f) d *= 0.25f;
    if (R() > d) continue;
    var p = new Vector2(px, pz);
    if (Blocked(p, 2.5f)) continue;
    bool sand = s.biome == OWBiome.Dunes;
    string bigTag = sand ? "sandstone" : null;
    var big = R() < 0.35f ? PickKit("rock_big", s.biome, bigTag) : PickKit("rock", s.biome, bigTag);
    if (big == null) continue;
    float scale = Mathf.Lerp(big.minHeight, big.maxHeight, R());
    float radius = big.footprint * scale * 0.7f;
    if (!stones.Free(p, radius, 14f) || !trunks.Free(p, radius * 0.6f, 6f)) continue;
    Place(output, big, px, pz, scale, OWPlaceKind.Rock, R() * 360f, "wild");
    stones.Add(p, radius);
    Last.rocks++;
    // Satellite stones make the cluster read as weathered outcrop rather than a dropped prop.
    int sat = rng.Next(3);
    for (int k = 0; k < sat; k++) {
     float a = R() * Mathf.PI * 2f, dist = radius * (1.05f + R() * 0.9f);
     float sx = px + Mathf.Cos(a) * dist, sz = pz + Mathf.Sin(a) * dist;
     var sp = new Vector2(sx, sz);
     if (OpenWorldTerrainSculptor.RoadDistance(sx, sz) < 1.5f || OpenWorldTerrainSculptor.WaterAt(sx, sz, out _, out _) || Blocked(sp, 0.8f)) continue;
     var small = PickKit("rock_small", sand ? OWBiome.Storm : s.biome);
     if (sand) small = PickKit("rock", OWBiome.Dunes, "sandstone");
     if (small == null) continue;
     float ss = Mathf.Lerp(small.minHeight, small.maxHeight, R()) * (sand ? 0.45f : 1f);
     Place(output, small, sx, sz, ss, OWPlaceKind.Rock, R() * 360f, "wild");
     stones.Add(sp, small.footprint * ss * 0.6f);
     Last.rocks++;
    }
   }
  }

  static void PlaceLitter(List<OWPlacement> output) {
   const float cell = 8f;
   float lim = OpenWorldRegions.PlayableHalf;
   for (float z = -lim; z < lim; z += cell) for (float x = -lim; x < lim; x += cell) {
    float px = x + R() * cell, pz = z + R() * cell;
    if (!Sample(px, pz, out var s)) continue;
    if (s.wet || s.road < 1.5f || s.slope > 26f) continue;
    if (s.biome == OWBiome.Dunes) continue;
    float d = s.forest > 0.45f ? 0.22f : 0.02f;
    if (R() > d) continue;
    var p = new Vector2(px, pz);
    if (Blocked(p, 1.2f) || !trunks.Free(p, 0.8f, 6f)) continue;
    WorldKitEntry e;
    float r = R();
    if (r < 0.32f) e = PickKit("log", s.biome, "stump");
    else if (r < 0.5f) e = PickKit("log", s.biome, "forest");
    else if (r < 0.75f) e = PickKit("mushroom", s.biome);
    else if (r < 0.88f) e = PickKit("fern", s.biome);
    else e = PickKit("log", s.biome, "small");
    if (e == null) continue;
    Place(output, e, px, pz, Mathf.Lerp(e.minHeight, e.maxHeight, R()), OWPlaceKind.Foliage, R() * 360f, "wild");
    Last.litter++;
   }
   // Runestones on quiet hilltops mark ancient ways.
   int runes = 0;
   for (int i = 0; i < 3000 && runes < 24; i++) {
    float px = (R() * 2f - 1f) * OpenWorldRegions.PlayableHalf, pz = (R() * 2f - 1f) * OpenWorldRegions.PlayableHalf;
    if (!Sample(px, pz, out var s) || s.wet || s.slope > 12f || s.road < 3f || s.road > 26f || s.biome == OWBiome.Dunes) continue;
    var p = new Vector2(px, pz);
    if (Blocked(p, 3f) || !stones.Free(p, 6f, 14f)) continue;
    var e = PickKit("shrine", s.biome);
    if (e == null) break;
    Place(output, e, px, pz, Mathf.Lerp(e.minHeight, e.maxHeight, R()), OWPlaceKind.Landmark, R() * 360f, "wild");
    stones.Add(p, 6f);
    runes++;
   }
  }

  static void PlaceWaterPlants(List<OWPlacement> output) {
   foreach (var lake in OpenWorldRegions.Lakes) {
    if (lake.Frozen) continue;
    var b = OpenWorldRegions.DominantBiome(lake.Center.x, lake.Center.y);
    if (b == OWBiome.Dunes) continue;
    int pads = Mathf.RoundToInt(lake.Radius * 1.4f);
    for (int i = 0; i < pads * 3 && pads > 0; i++) {
     float a = R() * Mathf.PI * 2f;
     float rr = lake.Radius * (0.45f + R() * 0.55f);
     float x = lake.Center.x + Mathf.Cos(a) * rr, z = lake.Center.y + Mathf.Sin(a) * rr;
     if (!OpenWorldTerrainSculptor.WaterAt(x, z, out float surf, out float depth) || depth < 0.25f) continue;
     var e = PickKit("lily", b == OWBiome.Graveyard ? OWBiome.Graveyard : OWBiome.Verdant);
     if (e == null) break;
     float scale = Mathf.Lerp(e.minHeight, e.maxHeight, R());
     // Pads float: their pivot (bottom) sits just under the surface.
     float y = surf - Mathf.Min(e.size.y * scale * 0.94f, e.size.y * scale - 0.02f);
     output.Add(new OWPlacement { key = e.key, pos = new Vector3(x, y, z), euler = new Vector3(0f, R() * 360f, 0f), scale = scale, kind = OWPlaceKind.WaterPlant, zone = lake.Name });
     pads--; Last.waterPlants++;
    }
   }
   // Cattails and pebbles along every shoreline and river bank.
   const float cell = 3.5f;
   float lim = OpenWorldRegions.PlayableHalf;
   for (float z = -lim; z < lim; z += cell) for (float x = -lim; x < lim; x += cell) {
    float px = x + R() * cell, pz = z + R() * cell;
    float wd = OpenWorldTerrainSculptor.DistanceToWater(px, pz);
    if (wd > 3.2f || wd < 0.3f) continue;
    if (OpenWorldTerrainSculptor.RoadDistance(px, pz) < 2f) continue;
    var b = OpenWorldRegions.DominantBiome(px, pz);
    var p = new Vector2(px, pz);
    if (Blocked(p, 0.8f)) continue;
    if (b != OWBiome.Dunes && b != OWBiome.Frozen && R() < 0.16f) {
     var e = PickKit("reed", b == OWBiome.Graveyard ? OWBiome.Graveyard : OWBiome.Verdant);
     if (e == null) continue;
     Place(output, e, px, pz, Mathf.Lerp(e.minHeight, e.maxHeight, R()), OWPlaceKind.WaterPlant, R() * 360f, "shore");
     Last.waterPlants++;
    } else if (R() < 0.08f) {
     var e = PickKit("stone", b == OWBiome.Frozen ? OWBiome.Frozen : OWBiome.Verdant, "water");
     if (e == null) continue;
     Place(output, e, px, pz, Mathf.Lerp(e.minHeight, e.maxHeight, R()), OWPlaceKind.Rock, R() * 360f, "shore");
     Last.waterPlants++;
    }
   }
  }

  // ---------- details ----------
  public const int DetailRes = 1024;

  static void PaintDetails(TerrainData td, float[,,] splat) {
   var entries = new List<WorldKitEntry>();
   foreach (var e in OpenWorldWorldKit.Role("grass")) if (e.detail) entries.Add(e);
   foreach (var e in OpenWorldWorldKit.Role("plant")) if (e.detail) entries.Add(e);
   foreach (var e in OpenWorldWorldKit.Role("reed")) if (e.detail) entries.Add(e);
   foreach (var e in OpenWorldWorldKit.Role("flower")) if (e.detail) entries.Add(e);
   foreach (var e in OpenWorldWorldKit.Role("fern")) if (e.detail) entries.Add(e);
   var protos = new List<DetailPrototype>();
   var kinds = new List<WorldKitEntry>();
   var dry = new List<bool>(); var frost = new List<bool>(); var meadowTag = new List<bool>();
   foreach (var e in entries) {
    var prefab = Resources.Load<GameObject>(e.resource);
    if (!prefab) continue;
    bool flower = e.role == "flower";
    protos.Add(new DetailPrototype {
     prototype = prefab, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
     minWidth = flower ? 0.8f : 0.8f, maxWidth = flower ? 1.2f : 1.25f, minHeight = flower ? 0.8f : 0.55f, maxHeight = flower ? 1.2f : 1.05f,
     noiseSpread = 0.35f, density = 1f, alignToGround = 0.65f, positionJitter = 0.95f, healthyColor = Color.white, dryColor = Color.white
    });
    kinds.Add(e);
    dry.Add(e.tags.Contains("dry")); frost.Add(e.tags.Contains("frost")); meadowTag.Add(e.tags.Contains("meadow"));
   }
   td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
   td.SetDetailResolution(DetailRes, 32);
   td.detailPrototypes = protos.ToArray();
   if (protos.Count == 0) return;
   int res = DetailRes;
   var layers = new int[protos.Count][,];
   for (int i = 0; i < layers.Length; i++) layers[i] = new int[res, res];
   int alphaRes = splat != null ? splat.GetLength(0) : 0;
   float px = OpenWorldRegions.WorldHalf * 2f / res;
   var weights = new float[8];
   for (int zi = 0; zi < res; zi++) {
    float z = -OpenWorldRegions.WorldHalf + (zi + 0.5f) * px;
    for (int xi = 0; xi < res; xi++) {
     float x = -OpenWorldRegions.WorldHalf + (xi + 0.5f) * px;
     if (OpenWorldTerrainSculptor.WaterAt(x, z, out _, out _)) continue;
     float road = OpenWorldTerrainSculptor.RoadDistance(x, z);
     if (road < 0.4f) continue;
     if (alphaRes > 0) {
      int ax = Mathf.Clamp(xi * alphaRes / res, 0, alphaRes - 1), az = Mathf.Clamp(zi * alphaRes / res, 0, alphaRes - 1);
      for (int l = 0; l < 8; l++) weights[l] = splat[az, ax, l];
     } else { for (int l = 0; l < 8; l++) weights[l] = 0f; weights[0] = 1f; }
     float grassy = weights[OpenWorldTerrainPainter.Grass] + weights[OpenWorldTerrainPainter.Forest] * 0.55f;
     float snow = weights[OpenWorldTerrainPainter.Snow], sand = weights[OpenWorldTerrainPainter.Sand];
     float rocky = weights[OpenWorldTerrainPainter.Rock] + weights[OpenWorldTerrainPainter.Cliff] + weights[OpenWorldTerrainPainter.Cobble];
     float slope = OpenWorldTerrainPainter.Slope(x, z);
     if (slope > 38f) continue;
     float slopeK = 1f - S(24f, 38f, slope);
     float wd = OpenWorldTerrainSculptor.DistanceToWater(x, z);
     float verge = S(0.4f, 2.2f, road);
     OpenWorldRegions.BiomeWeights(x, z, bw);
     float meadow = OpenWorldTerrainPainter.MeadowFlowers(x, z);
     float forest = OpenWorldTerrainPainter.ForestMask(x, z);
     float n = OpenWorldRegions.Noise(x * 0.15f + 3.1f, z * 0.15f + 7.7f);
     // Trodden ground: settlements keep their grass short and patchy.
     float vd = 999f;
     foreach (var c in Settlements) vd = Mathf.Min(vd, Vector2.Distance(new Vector2(x, z), c));
     float villageK = Mathf.Lerp(0.3f, 1f, S(22f, 55f, vd));
     for (int k = 0; k < kinds.Count; k++) {
      var e = kinds[k];
      float d = 0f;
      switch (e.role) {
       case "grass":
        if (dry[k]) d = (sand * (wd < 30f ? 0.6f : 0.08f) + bw[1] * (1f - snow) * 0.4f) * 1.2f;
        else if (frost[k]) d = bw[1] * (1f - snow) * 1.1f;
        else d = grassy * (1f - snow) * (1f - sand) * (1f - rocky) * (bw[0] + bw[3] * 0.85f + bw[4] * 0.5f) * 0.75f;
        break;
       case "plant": d = forest * grassy * (bw[0] + bw[3]) * 0.35f; break;
       case "fern": d = forest * (bw[0] + bw[1] * (1f - snow)) * 0.22f; break;
       case "reed": d = wd < 3.5f && wd > 0.2f ? (1f - wd / 3.5f) * (bw[0] + bw[3]) * 0.55f : 0f; break;
       case "flower":
        float meadowK = meadowTag[k] ? meadow : meadow * 0.35f + 0.02f;
        d = meadowK * grassy * bw[0] * (1f - forest) * 0.18f;
        break;
      }
      d *= slopeK * verge * villageK;
      if (d <= 0.001f) continue;
      // Stochastic rounding keeps low densities natural instead of banding.
      float count = d * (e.role == "grass" ? 2.1f : 1.2f) * (0.6f + n * 0.8f);
      int c = (int)count;
      if (R() < count - c) c++;
      if (c > 0) { layers[k][zi, xi] = Mathf.Min(c, 6); Last.detailInstances += c; }
     }
    }
   }
   for (int k = 0; k < layers.Length; k++) td.SetDetailLayer(0, 0, k, layers[k]);
  }
 }
}
