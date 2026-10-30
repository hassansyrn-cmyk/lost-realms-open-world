using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Authored settlement layouts built from World Kit pieces. Phase 1 (before sculpting) decides
 // every building footprint and registers level yard pads; phase 2 (after sculpting) resolves
 // heights and adds the props, fences and lamps that make each place feel lived in.
 public static class OpenWorldSettlementPlans {
  public static readonly List<OWPlaza> Plazas = new List<OWPlaza>();
  public static readonly List<OWFootprint> Footprints = new List<OWFootprint>();
  public static readonly List<(Vector2 c, float r)> ClearZones = new List<(Vector2, float)>();
  static readonly List<Planned> planned = new List<Planned>();
  static readonly OWOccupancy occupancy = new OWOccupancy();
  static System.Random rng;

  sealed class Planned {
   public string Key, Zone; public Vector2 Pos; public float Yaw, Scale = 1f; public OWPlaceKind Kind;
   public Vector2 Half; public float Sink = 0.06f; public bool Props;
  }

  public static IReadOnlyList<OWFootprint> BuildingFootprints => Footprints;

  // ---------- phase 1 ----------
  public static void Plan() {
   OpenWorldRegions.Initialise();
   OpenWorldWorldKit.Load();
   Plazas.Clear(); Footprints.Clear(); planned.Clear(); occupancy.Clear(); ClearZones.Clear();
   rng = new System.Random(77031);
   RegisterClearZones();
   PlanHubOuterRing();
   PlanOakvale();
   PlanMillbrook();
   PlanCaravanPost();
   PlanFrostpine();
   PlanSunspire();
   PlanOasis();
   PlanDuneRuins();
   PlanGraveyard();
   PlanStormOverlook();
   foreach (var p in planned) {
    if (p.Kind != OWPlaceKind.Building && p.Kind != OWPlaceKind.Landmark) continue;
    OpenWorldTerrainSculptor.AddPad(new OWPad { Center = p.Pos, HalfSize = p.Half + new Vector2(1.6f, 1.6f), Yaw = p.Yaw, Falloff = 6f, Height = float.NaN, Late = true });
    Footprints.Add(new OWFootprint { Center = p.Pos, Half = p.Half, Yaw = p.Yaw });
   }
  }

  // Gameplay spots that must stay open (quest objects, gates, NPC stands, sky-island shadows).
  static void RegisterClearZones() {
   void Z(float x, float z, float r) => ClearZones.Add((new Vector2(x, z), r));
   Z(0, 0, 36); Z(0, 25, 9); Z(-13.5f, 30, 6); Z(13.5f, 30, 6); Z(20, 16, 5); Z(0, 115, 30); Z(0, 95, 8); Z(-60, -58, 24); Z(-60, -43, 6);
   Z(-65, 65, 20); Z(68, 58, 20); Z(62, -62, 20); Z(-65, 54, 6); Z(68, 47, 6); Z(62, -73, 6);
   Z(30, -24, 10); Z(26, -18, 11); Z(-22, 84, 11); Z(26, -8, 5); Z(-18, 88, 5); Z(-78, 82, 6); Z(-84, 92, 10);
   Z(-58, 72, 4); Z(74, 64, 4); Z(-10, -14, 4); Z(-72, 58, 4); Z(60, 68, 4); Z(68, -56, 4);
   Z(-28, 24, 4); Z(29, 20, 4); Z(28, -28, 4); Z(-26, -26, 4); Z(5, 52, 4);
   Z(OpenWorldRegions.FrostRift.x, OpenWorldRegions.FrostRift.z, 16); Z(OpenWorldRegions.DesertRift.x, OpenWorldRegions.DesertRift.z, 16);
   Z(OpenWorldRegions.StormRift.x, OpenWorldRegions.StormRift.z, 16); Z(OpenWorldRegions.StormRelay.x, OpenWorldRegions.StormRelay.z, 18);
   Z(OpenWorldRegions.DragonPeak.x, OpenWorldRegions.DragonPeak.z, 30); Z(OpenWorldRegions.WolfDen.x, OpenWorldRegions.WolfDen.z, 14);
  }

  public static bool InClearZone(Vector2 p, float r) {
   foreach (var z in ClearZones) if ((p - z.c).sqrMagnitude < (z.r + r) * (z.r + r)) return true;
   return false;
  }

  // Same test but ignoring the zone centred on 'except' (a camp's own clearing).
  static bool InClearZoneExcept(Vector2 p, float r, Vector2 except) {
   foreach (var z in ClearZones) {
    if ((z.c - except).sqrMagnitude < 4f) continue;
    if ((p - z.c).sqrMagnitude < (z.r + r) * (z.r + r)) return true;
   }
   return false;
  }

  static float RoadDistance(Vector2 p) {
   float best = 999f;
   foreach (var road in OpenWorldRegions.Roads) {
    float d = OpenWorldRegions.DistanceToPath(road.Path, p, out _, out _) - road.Width * 0.5f;
    if (d < best) best = d;
   }
   return best;
  }

  static float WaterDistance(Vector2 p) {
   float best = 999f;
   foreach (var lake in OpenWorldRegions.Lakes) best = Mathf.Min(best, Vector2.Distance(p, lake.Center) - lake.Radius * 1.3f);
   foreach (var river in OpenWorldRegions.Rivers) best = Mathf.Min(best, OpenWorldRegions.DistanceToPath(river.Path, p, out _, out _) - Mathf.Max(river.WidthStart, river.WidthEnd) * 0.6f);
   return best;
  }

  static Vector2 Half(string key, float scale) {
   var e = OpenWorldWorldKit.Entry(key);
   if (e == null) return Vector2.zero;
   return new Vector2(e.size.x * 0.5f, e.size.z * 0.5f) * scale;
  }

  // Facing yaw so the building's +Z front looks at a target point.
  static float Face(Vector2 from, Vector2 to) { var d = to - from; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

  static bool TryPlace(string zone, string key, Vector2 pos, float yaw, float scale, OWPlaceKind kind, float roadClear = 2.5f, bool ignoreClear = false, bool props = true) {
   var e = OpenWorldWorldKit.Entry(key);
   if (e == null) return false;
   var half = new Vector2(e.size.x * 0.5f, e.size.z * 0.5f) * scale;
   float radius = Mathf.Max(half.x, half.y);
   if (!occupancy.Free(pos, radius * 0.92f, 40f)) return false;
   if (RoadDistance(pos) < radius * 0.85f + roadClear) return false;
   if (WaterDistance(pos) < radius + 2f) return false;
   if (!ignoreClear && InClearZone(pos, radius * 0.8f)) return false;
   if (Mathf.Max(Mathf.Abs(pos.x), Mathf.Abs(pos.y)) > OpenWorldRegions.PlayableHalf - 10f) return false;
   occupancy.Add(pos, radius * 0.92f);
   planned.Add(new Planned { Key = key, Zone = zone, Pos = pos, Yaw = yaw, Scale = scale, Kind = kind, Half = half, Props = props });
   return true;
  }

  static string Pick(string[] keys) => keys[rng.Next(keys.Length)];

  // Buildings around a centre, fronts facing it, skipping arcs blocked by roads or other pieces.
  static int Ring(string zone, Vector2 c, float radius, float spread, string[] keys, int count, float scale = 1f, float startDeg = 0f, float roadClear = 2.5f) {
   int placed = 0, attempts = 0;
   float step = 360f / Mathf.Max(6, count * 2);
   float angle = startDeg;
   while (placed < count && attempts++ < count * 18) {
    angle += step * (0.55f + (float)rng.NextDouble() * 0.6f);
    string key = Pick(keys);
    var half = Half(key, scale);
    float r = radius + half.y + (float)(rng.NextDouble() * spread);
    var dir = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
    var pos = c + dir * r;
    if (TryPlace(zone, key, pos, Face(pos, c), scale, OWPlaceKind.Building, roadClear)) placed++;
   }
   return placed;
  }

  // Houses lining both sides of a straight street, fronts facing the street.
  static void Street(string zone, Vector2 a, Vector2 b, float setback, string[] keys, float scale, float gap, params float[] skipAt) {
   var d = (b - a); float len = d.magnitude; d /= len;
   var n = new Vector2(-d.y, d.x);
   for (int side = -1; side <= 1; side += 2) {
    float t = 4f;
    while (t < len - 4f) {
     bool skip = false;
     foreach (var s in skipAt) if (Mathf.Abs(t - s) < 9f) { skip = true; break; }
     if (skip) { t += 6f; continue; }
     string key = Pick(keys);
     var half = Half(key, scale);
     float width = half.x * 2f;
     var along = a + d * (t + width * 0.5f);
     var pos = along + n * side * (setback + half.y);
     float yaw = Face(pos, along);
     if (TryPlace(zone, key, pos, yaw, scale, OWPlaceKind.Building, -6f, true)) t += width + gap + (float)rng.NextDouble() * 1.5f;
     else t += 3f;
    }
   }
  }

  static void Plaza(Vector2 c, float r, int layer) => Plazas.Add(new OWPlaza { Center = c, Radius = r, Layer = layer });

  // ---------- settlements ----------
  static readonly string[] VerdantHouses = { "house_house1", "house_house2", "house_house3", "house_woodenhouse_1", "house_woodenhouse_2", "house_woodenhouse_3", "house_big_village", "house_old_village" };
  static readonly string[] HamletHouses = { "rpg_house_01", "rpg_house_02", "rpg_house_03", "rpg_house_04", "rpg_house_05", "house_old_village" };
  static readonly string[] FrostHouses = { "house_woodenhouse_1", "house_woodenhouse_2", "house_woodenhouse_3", "house_old_village", "rpg_house_02", "rpg_house_04" };
  static readonly string[] DesertCity = { "desert_house_30_a", "desert_house_30_c", "desert_house_30_d", "desert_house_30_f", "desert_house_31_a", "desert_house_31_c", "desert_house_31_d", "desert_house_31_g",
   "desert_house_32_a", "desert_house_32_c", "desert_house_32_e", "desert_house_33_a", "desert_house_33_c", "desert_house_33_f", "desert_house_33_h", "desert_house_34_a", "desert_house_34_c", "desert_house_34_g",
   "desert_house_38_a", "desert_house_38_d", "desert_house_41_a", "desert_house_41_b", "desert_house_20", "desert_house_26", "desert_house_28" };
  // house_40_* and house_42 are open-backed corner modules meant to abut other buildings, so they stay out.
  static readonly string[] DesertHuts = { "desert_house_39_a", "desert_house_39_c", "desert_house_39_e", "desert_house_35_a", "desert_house_35_b", "desert_house_35_d", "desert_house_43", "desert_house_44" };
  static readonly string[] OasisHouses = { "oasis_house_1", "oasis_house_2", "oasis_house_3", "oasis_house_4", "oasis_house_5", "oasis_house_6" };

  static void PlanHubOuterRing() {
   var c = Vector2.zero;
   Plaza(c, 19f, OpenWorldTerrainPainter.Cobble);
   TryPlace("hub", "tavern_grand", new Vector2(-51f, 8f), Face(new Vector2(-51f, 8f), c), 0.85f, OWPlaceKind.Landmark);
   TryPlace("hub", "armory_hall", new Vector2(47f, 15f), Face(new Vector2(47f, 15f), c), 0.9f, OWPlaceKind.Landmark);
   Ring("hub", c, 40f, 10f, VerdantHouses, 9, 1f, 15f);
  }

  static void PlanOakvale() {
   var c = new Vector2(OpenWorldRegions.Oakvale.x, OpenWorldRegions.Oakvale.z);
   Plaza(c, 9f, OpenWorldTerrainPainter.Dirt);
   TryPlace("oakvale", "windmill_grand", c + new Vector2(24f, -16f), Face(c + new Vector2(24f, -16f), c), 0.62f, OWPlaceKind.Landmark);
   Ring("oakvale", c, 22f, 6f, HamletHouses, 4, 1f, 200f);
   Ring("oakvale", c, 30f, 8f, new[] { "rpg_shed_01", "rpg_shed_02" }, 3, 1f, 40f);
  }

  static void PlanMillbrook() {
   var c = new Vector2(OpenWorldRegions.Millbrook.x, OpenWorldRegions.Millbrook.z);
   Plaza(c, 11f, OpenWorldTerrainPainter.Dirt);
   TryPlace("millbrook", "windmill_small", c + new Vector2(-26f, 14f), Face(c + new Vector2(-26f, 14f), c), 1f, OWPlaceKind.Landmark);
   TryPlace("millbrook", "camp_well", c + new Vector2(3f, 3f), 0f, 0.7f, OWPlaceKind.Prop, 1f, true, false);
   Ring("millbrook", c, 16f, 6f, VerdantHouses, 7, 1f, 30f);
   Ring("millbrook", c, 30f, 6f, new[] { "rpg_shed_01", "rpg_shed_02" }, 3, 1f, 120f);
  }

  static void PlanCaravanPost() {
   var c = new Vector2(OpenWorldRegions.CaravanPost.x, OpenWorldRegions.CaravanPost.z);
   Plaza(c, 10f, OpenWorldTerrainPainter.Dirt);
   Ring("caravan", c, 10f, 3f, new[] { "desert_tent_a", "desert_tent_b", "desert_tent_c" }, 4, 1f, 60f, 1.5f);
  }

  static void PlanFrostpine() {
   var c = new Vector2(OpenWorldRegions.Frostpine.x, OpenWorldRegions.Frostpine.z);
   Plaza(c, 12f, OpenWorldTerrainPainter.Dirt);
   TryPlace("frostpine", "camp_well", c + new Vector2(0f, 4f), 0f, 0.7f, OWPlaceKind.Prop, 1f, true, false);
   Ring("frostpine", c, 15f, 7f, FrostHouses, 8, 1f, 10f);
  }

  static void PlanSunspire() {
   var c = new Vector2(OpenWorldRegions.Sunspire.x, OpenWorldRegions.Sunspire.z);
   Plaza(c, 15f, OpenWorldTerrainPainter.Cobble);
   Plaza(c + new Vector2(-30f, 0f), 9f, OpenWorldTerrainPainter.Cobble);
   // Landmarks first so streets flow around them.
   TryPlace("sunspire", "desert_citadel_main", c + new Vector2(38f, -30f), Face(c + new Vector2(38f, -30f), c), 0.42f, OWPlaceKind.Landmark, 2f, true, false);
   TryPlace("sunspire", "desert_church_b", c + new Vector2(-34f, -30f), Face(c + new Vector2(-34f, -30f), c), 0.8f, OWPlaceKind.Landmark, 2f, true, false);
   TryPlace("sunspire", "desert_city_tower_a", c + new Vector2(30f, 26f), 0f, 0.7f, OWPlaceKind.Landmark, 2f, true, false);
   TryPlace("sunspire", "desert_fountain_b", c, 0f, 1f, OWPlaceKind.Prop, -10f, true, false);
   // Main east-west avenue (the Kings Road) and the northern avenue (Oasis Track).
   Street("sunspire", c + new Vector2(-58f, 0f), c + new Vector2(-17f, 0f), 6f, DesertCity, 0.9f, 1.2f, 28f);
   Street("sunspire", c + new Vector2(17f, 0f), c + new Vector2(56f, 0f), 6f, DesertCity, 0.9f, 1.2f);
   Street("sunspire", c + new Vector2(0f, 17f), c + new Vector2(0f, 46f), 5.5f, DesertCity, 0.9f, 1.2f);
   Street("sunspire", c + new Vector2(0f, -17f), c + new Vector2(0f, -44f), 5.5f, DesertCity, 0.9f, 1.2f);
   Ring("sunspire", c + new Vector2(-30f, 26f), 6f, 6f, DesertHuts, 4, 1f, 0f, 1f);
   Ring("sunspire", c + new Vector2(26f, -14f), 6f, 6f, DesertHuts, 3, 1f, 0f, 1f);
   // Market tents around the central plaza.
   Ring("sunspire", c, 11f, 2f, new[] { "desert_tent_a", "desert_tent_b", "desert_tent_c" }, 5, 1f, 35f, -8f);
   // City wall: corner towers and wall runs along the pad edge, gates where roads enter.
   float hx = 62f, hz = 48f, wallScale = 0.55f;
   var corners = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
   foreach (var k in corners) TryPlace("sunspire", "desert_castle_tower_round", c + k, 0f, wallScale, OWPlaceKind.Building, -20f, true, false);
   WallRun(c + corners[0], c + corners[1], wallScale, null);
   WallRun(c + corners[1], c + corners[2], wallScale, null);
   WallRun(c + corners[2], c + corners[3], wallScale, new[] { c + new Vector2(0f, hz) });
   WallRun(c + corners[3], c + corners[0], wallScale, new[] { c + new Vector2(-hx, 0f) });
  }

  static void WallRun(Vector2 a, Vector2 b, float scale, Vector2[] gates) {
   var e = OpenWorldWorldKit.Entry("desert_castle_wall_10m");
   if (e == null) return;
   float seg = Mathf.Max(e.size.x, e.size.z) * scale;
   var d = b - a; float len = d.magnitude; d /= len;
   float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 90f;
   for (float t = seg * 0.5f + 6f; t < len - 6f; t += seg) {
    var p = a + d * t;
    bool gap = false;
    if (gates != null) foreach (var g in gates) if (Vector2.Distance(p, g) < 9f) gap = true;
    if (gap) continue;
    TryPlace("sunspire", "desert_castle_wall_10m", p, yaw, scale, OWPlaceKind.Building, -20f, true, false);
   }
   if (gates != null) foreach (var g in gates) TryPlace("sunspire", "desert_fortification_gate", g, yaw, 0.8f, OWPlaceKind.Building, -20f, true, false);
  }

  static void PlanOasis() {
   var c = new Vector2(OpenWorldRegions.Oasis.x, OpenWorldRegions.Oasis.z);
   Ring("oasis", c, 30f, 8f, OasisHouses, 7, 1f, 120f);
   Ring("oasis", c, 26f, 4f, new[] { "desert_tent_a", "desert_tent_c" }, 3, 1f, 300f);
  }

  static void PlanDuneRuins() {
   var c = new Vector2(OpenWorldRegions.DuneRuins.x, OpenWorldRegions.DuneRuins.z);
   Ring("duneruins", c, 6f, 14f, new[] { "desert_ruin_a", "desert_ruin_b", "desert_ruin_c", "desert_ruin_d", "desert_ruin_e", "desert_ruin_f" }, 8, 1.1f, 0f, 1f);
   TryPlace("duneruins", "desert_sentinel_tower_b", c + new Vector2(0f, 14f), 180f, 0.6f, OWPlaceKind.Landmark, 1f, true, false);
  }

  static void PlanGraveyard() {
   var c = new Vector2(OpenWorldRegions.Graveyard.x, OpenWorldRegions.Graveyard.z);
   Plaza(c, 10f, OpenWorldTerrainPainter.Dirt);
   TryPlace("graveyard", "grave_crypt_arch", c + new Vector2(0f, -20f), 0f, 1f, OWPlaceKind.Landmark, 1f, true, false);
  }

  static void PlanStormOverlook() {
   var c = new Vector2(OpenWorldRegions.StormOverlook.x, OpenWorldRegions.StormOverlook.z);
   Plaza(c, 9f, OpenWorldTerrainPainter.Dirt);
   TryPlace("storm", "desert_sentinel_tower_a", c + new Vector2(12f, 8f), Face(c + new Vector2(12f, 8f), c), 0.7f, OWPlaceKind.Landmark, 1f, true, false);
   TryPlace("storm", "desert_fortification_tower_small", c + new Vector2(-14f, 6f), Face(c + new Vector2(-14f, 6f), c), 0.6f, OWPlaceKind.Building, 1f, true, false);
   Ring("storm", c, 16f, 4f, new[] { "palisade_1", "palisade_2", "palisade_corner" }, 5, 0.8f, 100f, 1f);
  }

  // ---------- phase 2: heights, props, lamps ----------
  public static void Resolve(List<OWPlacement> output) {
   var rng2 = new System.Random(9917);
   foreach (var p in planned) {
    float y = GroundUnder(p.Pos, p.Half, p.Yaw) - p.Sink;
    output.Add(new OWPlacement { key = p.Key, pos = new Vector3(p.Pos.x, y, p.Pos.y), euler = new Vector3(0f, p.Yaw, 0f), scale = p.Scale, kind = p.Kind, zone = p.Zone });
    if (p.Props && p.Kind == OWPlaceKind.Building) DressBuilding(p, output, rng2);
   }
   DressCamps(output, rng2);
   DressGraveyard(output, rng2);
   DressRoadsides(output, rng2);
  }

  // Lowest ground under the footprint so no corner ever floats.
  static float GroundUnder(Vector2 c, Vector2 half, float yaw) {
   float min = OpenWorldTerrainSculptor.HeightAt(c.x, c.y);
   var rot = Quaternion.Euler(0f, yaw, 0f);
   for (int i = 0; i < 4; i++) {
    var local = new Vector3((i & 1) == 0 ? -half.x : half.x, 0f, (i & 2) == 0 ? -half.y : half.y) * 0.95f;
    var w = rot * local;
    min = Mathf.Min(min, OpenWorldTerrainSculptor.HeightAt(c.x + w.x, c.y + w.z));
   }
   return min;
  }

  static void Prop(List<OWPlacement> output, string key, Vector2 pos, float yaw, float scale, string zone, OWPlaceKind kind = OWPlaceKind.Prop) {
   if (OpenWorldWorldKit.Entry(key) == null) return;
   float y = OpenWorldTerrainSculptor.HeightAt(pos.x, pos.y) - 0.03f;
   output.Add(new OWPlacement { key = key, pos = new Vector3(pos.x, y, pos.y), euler = new Vector3(0f, yaw, 0f), scale = scale, kind = kind, zone = zone });
  }

  static readonly string[] VerdantClutter = { "town_storage_barrel", "town_storage_barrel_small", "town_storage_basket", "town_storage_bag_potato", "town_storage_jug", "village_crate_01", "village_crate_02", "village_barrel_01", "smith_box" };
  static readonly string[] DesertClutter = { "desert_barrel", "desert_box", "oasis_jar_1", "oasis_jar_2", "oasis_jar_3" };

  // A few barrels, crates and sacks against the side walls of each house.
  static void DressBuilding(Planned p, List<OWPlacement> output, System.Random r) {
   bool desert = p.Key.StartsWith("desert") || p.Key.StartsWith("oasis");
   var clutter = desert ? DesertClutter : VerdantClutter;
   var rot = Quaternion.Euler(0f, p.Yaw, 0f);
   int n = 1 + r.Next(3);
   for (int i = 0; i < n; i++) {
    float side = r.Next(2) == 0 ? -1f : 1f;
    var local = new Vector3(side * (p.Half.x + 0.7f), 0f, (float)(r.NextDouble() * 2 - 1) * p.Half.y * 0.6f);
    var w = rot * local;
    var pos = p.Pos + new Vector2(w.x, w.z);
    if (RoadDistance(pos) < 0.8f) continue;
    Prop(output, clutter[r.Next(clutter.Length)], pos, p.Yaw + (float)r.NextDouble() * 40f - 20f, 1f, p.Zone);
   }
  }

  // Camp supplies sit at fixed offsets (firewood, barrels, crate); the pit keeps clear of them too.
  static readonly Vector2[] CampSupplies = { new Vector2(5.5f, -3f), new Vector2(-8f, 6f), new Vector2(-6.5f, -6f) };

  static bool FindPitSpot(Vector2 centre, out Vector2 pit) {
   for (int ring = 0; ring < 3; ring++) {
    float radius = 5.5f + ring * 2f;
    for (int i = 0; i < 12; i++) {
     float a = (i * 30f + ring * 15f) * Mathf.Deg2Rad;
     var p = centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
     if (RoadDistance(p) < 2.4f || InClearZoneExcept(p, 1.6f, centre)) continue;
     bool crowded = false;
     foreach (var s in CampSupplies) if ((p - (centre + s)).sqrMagnitude < 3.2f * 3.2f) { crowded = true; break; }
     if (crowded || OpenWorldTerrainSculptor.DistanceToWater(p.x, p.y) < 3f) continue;
     var n = OpenWorldTerrainSculptor.NormalAt(p.x, p.y);
     if (Vector3.Angle(n, Vector3.up) > 14f) continue;
     pit = p;
     return true;
    }
   }
   pit = centre;
   return false;
  }

  static void DressCamps(List<OWPlacement> output, System.Random r) {
   (Vector2 c, string zone)[] camps = {
    (new Vector2(-65f, 65f), "woods"), (new Vector2(68f, 58f), "ruins"), (new Vector2(62f, -62f), "ridge"),
    (new Vector2(OpenWorldRegions.StormOverlook.x, OpenWorldRegions.StormOverlook.z), "storm"), (new Vector2(OpenWorldRegions.WolfDen.x, OpenWorldRegions.WolfDen.z), "wolfden")
   };
   foreach (var camp in camps) {
    // Defensive stakes and banners ring the camp; firewood and barrels sit inside.
    int stakes = 9;
    for (int i = 0; i < stakes; i++) {
     float a = (i / (float)stakes) * Mathf.PI * 2f + (float)r.NextDouble() * 0.2f;
     var pos = camp.c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (14.5f + (float)r.NextDouble() * 1.5f);
     if (RoadDistance(pos) < 1.2f || InClearZoneExcept(pos, 0.5f, camp.c)) continue;
     Prop(output, i % 3 == 0 ? "war_banner_a" : (i % 2 == 0 ? "stake_a" : "stake_b"), pos, Mathf.Atan2(pos.x - camp.c.x, pos.y - camp.c.y) * Mathf.Rad2Deg, 1f, camp.zone);
    }
    // A fire pit near the heart of the camp (stone ring, crossed logs and a live fire), on the first
    // spot around the centre that is clear of roads, the camp's own landmark and its supplies.
    if (FindPitSpot(camp.c, out var pit)) {
     Prop(output, "bonfire_c", pit, 0f, 0.8f, camp.zone);
     Prop(output, "bonfire_b", pit, 35f, 0.55f, camp.zone);
     Prop(output, "fire_bonfire", pit, 0f, 1f, camp.zone, OWPlaceKind.Fx);
    }
    Prop(output, "firewood_" + "abc"[r.Next(3)], camp.c + new Vector2(5.5f, -3f), (float)r.NextDouble() * 360f, 1f, camp.zone);
    Prop(output, "barrels_mounted", camp.c + new Vector2(-8f, 6f), (float)r.NextDouble() * 360f, 0.8f, camp.zone);
    Prop(output, "village_crate_01", camp.c + new Vector2(-6.5f, -6f), (float)r.NextDouble() * 360f, 1f, camp.zone);
   }
  }

  static void DressGraveyard(List<OWPlacement> output, System.Random r) {
   var c = new Vector2(OpenWorldRegions.Graveyard.x, OpenWorldRegions.Graveyard.z);
   float yaw = 18f;
   var rot = Quaternion.Euler(0f, yaw, 0f);
   string[] tombs = { "tomb_001", "tomb_002", "tomb_003", "tomb_004", "tomb_005", "tomb_plain" };
   for (int row = -5; row <= 5; row++) for (int col = -6; col <= 6; col++) {
    if (Mathf.Abs(col) <= 1) continue; // central path
    if (r.NextDouble() < 0.18) continue;
    var local = new Vector3(col * 4.2f + (float)(r.NextDouble() - 0.5) * 0.6f, 0f, row * 4.6f + (float)(r.NextDouble() - 0.5) * 0.6f);
    var w = rot * local;
    var pos = c + new Vector2(w.x, w.z);
    if (InClearZone(pos, 0.5f)) continue;
    Prop(output, tombs[r.Next(tombs.Length)], pos, yaw + 180f + (float)(r.NextDouble() - 0.5) * 14f, 0.9f + (float)r.NextDouble() * 0.25f, "graveyard", OWPlaceKind.Prop);
   }
   // Continuous iron fence (panels butt together) around the plot, gate arch on the north side.
   float hx = 32f, hz = 27f;
   var panel = OpenWorldWorldKit.Entry("grave_fence_1");
   float step = panel != null ? Mathf.Max(1.2f, Mathf.Max(panel.size.x, panel.size.z) * 0.98f) : 2.05f;
   for (float t = -hx; t <= hx; t += step) {
    foreach (float zz in new[] { -hz, hz }) {
     var local = new Vector3(t, 0f, zz);
     if (zz > 0 && Mathf.Abs(t) < 4f) continue;
     var w = rot * local;
     Prop(output, (int)(t * 7) % 2 == 0 ? "grave_fence_1" : "grave_fence_2", c + new Vector2(w.x, w.z), yaw + 90f, 1f, "graveyard", OWPlaceKind.Fence);
    }
   }
   for (float t = -hz; t <= hz; t += step) {
    foreach (float xx in new[] { -hx, hx }) {
     var w = rot * new Vector3(xx, 0f, t);
     Prop(output, "grave_fence_1", c + new Vector2(w.x, w.z), yaw, 1f, "graveyard", OWPlaceKind.Fence);
    }
   }
   var gate = rot * new Vector3(0f, 0f, hz);
   Prop(output, "grave_gate_arch", c + new Vector2(gate.x, gate.z), yaw, 1f, "graveyard", OWPlaceKind.Landmark);
   foreach (var lp in new[] { new Vector3(-4f, 0, hz + 1.5f), new Vector3(4f, 0, hz + 1.5f), new Vector3(-hx + 2f, 0, 0), new Vector3(hx - 2f, 0, 0) }) {
    var w = rot * lp;
    Prop(output, "grave_lamp", c + new Vector2(w.x, w.z), yaw, 1f, "graveyard", OWPlaceKind.Lamp);
   }
   for (int i = 0; i < 6; i++) {
    var w = rot * new Vector3((i % 2 == 0 ? -1 : 1) * 6f, 0f, -18f + i * 2.5f);
    Prop(output, "grave_column", c + new Vector2(w.x, w.z), yaw, 0.9f, "graveyard", OWPlaceKind.Building);
   }
  }

  // Lantern posts at the edges of towns where roads arrive.
  static void DressRoadsides(List<OWPlacement> output, System.Random r) {
   (Vector2 c, float radius, string key, string zone)[] towns = {
    (Vector2.zero, 24f, "lantern_post", "hub"), (new Vector2(OpenWorldRegions.Frostpine.x, OpenWorldRegions.Frostpine.z), 20f, "lantern_post", "frostpine"),
    (new Vector2(OpenWorldRegions.Millbrook.x, OpenWorldRegions.Millbrook.z), 20f, "lantern_post", "millbrook"),
    (new Vector2(OpenWorldRegions.Sunspire.x, OpenWorldRegions.Sunspire.z), 18f, "desert_street_oil_light", "sunspire")
   };
   foreach (var t in towns) {
    foreach (var road in OpenWorldRegions.Roads) {
     for (int i = 1; i < road.Path.Length; i++) {
      var a = road.Path[i - 1]; var b = road.Path[i];
      float da = Vector2.Distance(a, t.c), db = Vector2.Distance(b, t.c);
      if ((da - t.radius) * (db - t.radius) > 0f) continue; // the segment crosses the town edge
      var dir = (b - a).normalized; var n = new Vector2(-dir.y, dir.x);
      var at = Vector2.Lerp(a, b, Mathf.InverseLerp(da, db, t.radius));
      foreach (float side in new[] { -1f, 1f }) {
       var pos = at + n * side * (road.Width * 0.5f + 1.6f);
       if (InClearZone(pos, 0.4f) && t.zone != "hub") continue;
       Prop(output, t.key, pos, Mathf.Atan2(-n.x * side, -n.y * side) * Mathf.Rad2Deg, 1f, t.zone, OWPlaceKind.Lamp);
      }
     }
    }
   }
  }
 }
}
