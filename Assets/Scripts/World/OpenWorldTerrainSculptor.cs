using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace LostRealmsOW {
 // Sculpts the 1 km realm heightmap: biome landforms, perimeter ranges, quest and settlement pads,
 // graded roads, lake basins and river channels. Also bakes 1 m road and water fields that the
 // painter, ecosystem and gameplay systems query (no physics needed). The editor bakes the result
 // into a TerrainData asset; at runtime the compact fields are restored from RealmFields.bytes.
 public static class OpenWorldTerrainSculptor {
  public const int HeightRes = 1025;
  public const int FieldRes = 1024;              // 1 m cells across the world
  const float Size = OpenWorldRegions.WorldHalf * 2f;
  const float CellToWorld = Size / FieldRes;

  public static float[,] Natural { get; private set; }     // pre-feature heights (metres)
  public static float[,] Final { get; private set; }       // final heights (metres)
  static float[] roadDist, roadHeight; static byte[] roadKind;
  static float[] waterSurface; static float[] waterDepth;  // NaN surface = dry land
  static float[] riverDist;
  static readonly List<OWPad> pads = new List<OWPad>();
  public static IReadOnlyList<OWPad> Pads => pads;
  public static bool Ready { get; private set; }
  static Terrain runtimeTerrain;

  public static void ResetPads() { pads.Clear(); Ready = false; }
  public static void AddPad(OWPad pad) { pads.Add(pad); }
  public static void AddRoundPad(Vector3 c, float radius, float falloff, float offset = 0f, float height = float.NaN) {
   pads.Add(new OWPad { Center = new Vector2(c.x, c.z), HalfSize = new Vector2(radius, radius), Falloff = falloff, Height = height, Offset = offset, Round = true });
  }

  static float S(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

  // ---------- natural landform ----------
  static readonly float[] bw = new float[5];
  static float[] valleyGrid; const int ValleyRes = 256;

  public static float NaturalHeight(float x, float z) {
   OpenWorldRegions.BiomeWeights(x, z, bw);
   float dCore = Vector2.Distance(new Vector2(x, z), new Vector2(0f, 30f));
   float fine = (OpenWorldRegions.Fbm(x * 0.026f + 1.7f, z * 0.026f + 8.8f, 3) - 0.5f) * 3.2f;
   float h = 0f;
   // Verdant: broad rolling meadows, calmer around the quest core.
   if (bw[0] > 0.0005f) {
    float broad = (OpenWorldRegions.Fbm(x * 0.0052f + 11.3f, z * 0.0052f + 4.1f, 4) - 0.5f) * 26f;
    float coreCalm = S(70f, 185f, dCore);
    h += bw[0] * (7f + Mathf.Lerp(broad * 0.32f, broad, coreCalm) + fine);
   }
   // Frozen: ridged ranges climbing north, with carved valleys where travel happens.
   if (bw[1] > 0.0005f) {
    float wx = x + (OpenWorldRegions.Noise(x * 0.004f + 5f, z * 0.004f + 2f) - 0.5f) * 90f;
    float wz = z + (OpenWorldRegions.Noise(x * 0.004f + 13f, z * 0.004f + 7f) - 0.5f) * 90f;
    float ridge = OpenWorldRegions.Ridged(wx * 0.0046f + 2.2f, wz * 0.0046f + 6.6f, 5);
    float rise = S(190f, 330f, z);
    float valley = ValleyMask(x, z);
    float frozen = 26f + rise * 18f + Mathf.Pow(ridge, 1.45f) * 150f * rise * valley + fine * 2.2f;
    frozen = Mathf.Lerp(Mathf.Min(frozen, 34f + rise * 26f), frozen, valley);
    h += bw[1] * frozen;
   }
   // Dunes: long wind-blown dunes with occasional sandstone mesas.
   if (bw[2] > 0.0005f) {
    float warp = OpenWorldRegions.Fbm(x * 0.006f, z * 0.006f, 2) * 9f;
    float dune = Mathf.Pow(Mathf.Sin(x * 0.045f + z * 0.018f + warp) * 0.5f + 0.5f, 2.2f);
    float duneSmall = Mathf.Sin(x * 0.11f - z * 0.07f + warp * 1.7f) * 0.5f + 0.5f;
    float mesaNoise = OpenWorldRegions.Fbm(x * 0.0065f + 31f, z * 0.0065f + 17f, 3);
    float mesa = S(0.6f, 0.66f, mesaNoise) * (20f + (mesaNoise - 0.66f) * 40f);
    h += bw[2] * (5f + dune * 9f + duneSmall * 1.6f + mesa * DesertMesaMask(x, z));
   }
   // Hollow marches: low wet ground with barrows.
   if (bw[3] > 0.0005f) {
    float barrow = OpenWorldRegions.Fbm(x * 0.012f + 77f, z * 0.012f + 3f, 4);
    h += bw[3] * (2.5f + (barrow - 0.5f) * 12f + fine);
   }
   // Stormveil: a shattered plateau with ravines.
   if (bw[4] > 0.0005f) {
    float plate = OpenWorldRegions.Ridged(x * 0.0062f + 9f, z * 0.0062f + 21f, 4);
    h += bw[4] * (14f + plate * 34f + fine * 1.5f);
   }
   // Perimeter ranges keep the realm enclosed without invisible walls. Domain-warped ridgelines carry
   // sharper summits, and a slow massif term raises some stretches into high ranges and lets others
   // fall to saddles, so the skyline reads as a mountain chain rather than an even wall.
   float border = S(392f, 500f, OpenWorldRegions.EdgeMetric(x, z));
   if (border > 0f) {
    float wx = x + (OpenWorldRegions.Noise(x * 0.0031f + 71f, z * 0.0031f + 19f) - 0.5f) * 140f;
    float wz = z + (OpenWorldRegions.Noise(x * 0.0031f + 23f, z * 0.0031f + 57f) - 0.5f) * 140f;
    float ridges = OpenWorldRegions.Ridged(wx * 0.0058f + 4f, wz * 0.0058f + 40f, 5);
    float peaks = Mathf.Pow(OpenWorldRegions.Ridged(wx * 0.0088f + 17f, wz * 0.0088f + 3f, 4), 1.9f);
    float massif = 0.55f + 0.9f * OpenWorldRegions.Fbm(x * 0.0021f + 90f, z * 0.0021f + 12f, 3);
    h += Mathf.Pow(border, 1.35f) * (48f + (ridges * 115f + peaks * 78f) * massif);
   }
   return Mathf.Clamp(SoftCeiling(h, 205f, OpenWorldRegions.TerrainHeight - 8f), 0.5f, OpenWorldRegions.TerrainHeight - 4f);
  }

  // Heights above the knee are compressed smoothly towards the ceiling instead of being clipped, so the
  // highest summits stay pointed (a hard clamp shaved every tall range into a flat-topped plateau).
  static float SoftCeiling(float h, float knee, float ceiling) {
   if (h <= knee) return h;
   float range = ceiling - knee;
   return knee + range * (1f - Mathf.Exp(-(h - knee) / range));
  }

  // 1 = full mountains, 0 = a travelled valley floor (roads, villages, tarn, arena basin).
  static float FrozenValleyMask(float x, float z) {
   Vector2 p = new Vector2(x, z);
   float m = 1f;
   foreach (var road in OpenWorldRegions.Roads) {
    if (road.Kind != OWRoadKind.Snow && road.Name != "Northern Pass") continue;
    float d = OpenWorldRegions.DistanceToPath(road.Path, p, out _, out _);
    m = Mathf.Min(m, S(22f, 78f, d));
   }
   m = Mathf.Min(m, S(55f, 120f, Vector2.Distance(p, new Vector2(OpenWorldRegions.Frostpine.x, OpenWorldRegions.Frostpine.z))));
   m = Mathf.Min(m, S(45f, 105f, Vector2.Distance(p, new Vector2(OpenWorldRegions.FrozenLake.x, OpenWorldRegions.FrozenLake.z))));
   m = Mathf.Min(m, S(36f, 90f, Vector2.Distance(p, new Vector2(OpenWorldRegions.DragonPeak.x, OpenWorldRegions.DragonPeak.z))));
   m = Mathf.Min(m, S(28f, 70f, Vector2.Distance(p, new Vector2(OpenWorldRegions.WolfDen.x, OpenWorldRegions.WolfDen.z))));
   m = Mathf.Min(m, S(24f, 60f, Vector2.Distance(p, new Vector2(OpenWorldRegions.FrostRift.x, OpenWorldRegions.FrostRift.z))));
   return m;
  }

  // The valley mask is smooth, so a 4 m grid bilinearly sampled is indistinguishable and ~40x cheaper.
  static float ValleyMask(float x, float z) {
   if (valleyGrid == null) {
    valleyGrid = new float[ValleyRes * ValleyRes];
    float step = Size / (ValleyRes - 1);
    for (int zi = 0; zi < ValleyRes; zi++) for (int xi = 0; xi < ValleyRes; xi++) {
     float wx = -OpenWorldRegions.WorldHalf + xi * step, wz = -OpenWorldRegions.WorldHalf + zi * step;
     valleyGrid[zi * ValleyRes + xi] = wz < 120f ? 1f : FrozenValleyMask(wx, wz);
    }
   }
   float fx = Mathf.Clamp((x + OpenWorldRegions.WorldHalf) / Size * (ValleyRes - 1), 0, ValleyRes - 1.001f);
   float fz = Mathf.Clamp((z + OpenWorldRegions.WorldHalf) / Size * (ValleyRes - 1), 0, ValleyRes - 1.001f);
   int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
   float a = Mathf.Lerp(valleyGrid[iz * ValleyRes + ix], valleyGrid[iz * ValleyRes + ix + 1], tx);
   float b = Mathf.Lerp(valleyGrid[(iz + 1) * ValleyRes + ix], valleyGrid[(iz + 1) * ValleyRes + ix + 1], tx);
   return Mathf.Lerp(a, b, tz);
  }

  static float DesertMesaMask(float x, float z) {
   Vector2 p = new Vector2(x, z);
   float m = 1f;
   m = Mathf.Min(m, S(70f, 110f, Vector2.Distance(p, new Vector2(OpenWorldRegions.Sunspire.x, OpenWorldRegions.Sunspire.z))));
   m = Mathf.Min(m, S(40f, 70f, Vector2.Distance(p, new Vector2(OpenWorldRegions.Oasis.x, OpenWorldRegions.Oasis.z))));
   m = Mathf.Min(m, S(30f, 60f, Vector2.Distance(p, new Vector2(OpenWorldRegions.DuneRuins.x, OpenWorldRegions.DuneRuins.z))));
   m = Mathf.Min(m, S(24f, 50f, Vector2.Distance(p, new Vector2(OpenWorldRegions.DesertRift.x, OpenWorldRegions.DesertRift.z))));
   foreach (var road in OpenWorldRegions.Roads) {
    if (road.Kind != OWRoadKind.Sand && road.Name != "Kings Road East") continue;
    m = Mathf.Min(m, S(10f, 34f, OpenWorldRegions.DistanceToPath(road.Path, p, out _, out _)));
    if (m <= 0f) break;
   }
   return m;
  }

  // ---------- build ----------
  public static TerrainData Build() {
   OpenWorldRegions.Initialise();
   valleyGrid = null;
   int n = HeightRes;
   float step = Size / (n - 1);
   var natural = new float[n, n];
   for (int zi = 0; zi < n; zi++) {
    float z = -OpenWorldRegions.WorldHalf + zi * step;
    for (int xi = 0; xi < n; xi++) {
     float x = -OpenWorldRegions.WorldHalf + xi * step;
     natural[zi, xi] = NaturalHeight(x, z);
    }
   }
   Natural = natural;
   var h = (float[,])natural.Clone();
   ApplyPads(h, step, false);
   BuildRoadField(h, step);
   ApplyRoads(h, step);
   ApplyPads(h, step, true);
   ResolveLakes(h, step);
   BuildRiverAndWaterField(h, step);
   ApplyWater(h, step);
   Final = h;
   runtimeTerrain = null;

   // Heights are stored from TerrainBase up, so beds below 0 survive (the terrain object is placed at
   // TerrainBase; see OpenWorldRealm). TerrainData.size.y records the range for older bakes.
   var td = new TerrainData { heightmapResolution = n };
   float range = OpenWorldRegions.TerrainHeight - OpenWorldRegions.TerrainBase;
   td.size = new Vector3(Size, range, Size);
   var norm = new float[n, n];
   float inv = 1f / range;
   for (int zi = 0; zi < n; zi++) for (int xi = 0; xi < n; xi++) norm[zi, xi] = Mathf.Clamp01((h[zi, xi] - OpenWorldRegions.TerrainBase) * inv);
   td.SetHeights(0, 0, norm);
   Ready = true;
   return td;
  }

  static void ApplyPads(float[,] h, float step, bool late) {
   int n = HeightRes;
   foreach (var pad in pads) {
    if (pad.Late != late) continue;
    float baseH;
    if (float.IsNaN(pad.Height)) {
     // Average a small neighbourhood so pads sit at a believable mean level.
     float sum = 0; int count = 0;
     for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++) {
      sum += SampleGrid(late ? h : Natural, pad.Center.x + i * pad.HalfSize.x * 0.4f, pad.Center.y + j * pad.HalfSize.y * 0.4f, step); count++;
     }
     baseH = sum / count;
    } else baseH = pad.Height;
    baseH += pad.Offset;
    float reach = Mathf.Max(pad.HalfSize.x, pad.HalfSize.y) * 1.5f + pad.Falloff;
    int x0 = Mathf.Max(0, Mathf.FloorToInt((pad.Center.x - reach + OpenWorldRegions.WorldHalf) / step));
    int x1 = Mathf.Min(n - 1, Mathf.CeilToInt((pad.Center.x + reach + OpenWorldRegions.WorldHalf) / step));
    int z0 = Mathf.Max(0, Mathf.FloorToInt((pad.Center.y - reach + OpenWorldRegions.WorldHalf) / step));
    int z1 = Mathf.Min(n - 1, Mathf.CeilToInt((pad.Center.y + reach + OpenWorldRegions.WorldHalf) / step));
    // World -> pad-local for a Unity yaw (matches Quaternion.Euler(0, Yaw, 0) on the building).
    float c = Mathf.Cos(pad.Yaw * Mathf.Deg2Rad), s = Mathf.Sin(pad.Yaw * Mathf.Deg2Rad);
    for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) {
     float wx = -OpenWorldRegions.WorldHalf + xi * step - pad.Center.x;
     float wz = -OpenWorldRegions.WorldHalf + zi * step - pad.Center.y;
     float d;
     if (pad.Round) d = Mathf.Max(0f, Mathf.Sqrt(wx * wx + wz * wz) - pad.HalfSize.x);
     else {
      float lx = wx * c - wz * s, lz = wx * s + wz * c;
      float dx = Mathf.Max(0f, Mathf.Abs(lx) - pad.HalfSize.x), dz = Mathf.Max(0f, Mathf.Abs(lz) - pad.HalfSize.y);
      d = Mathf.Sqrt(dx * dx + dz * dz);
     }
     if (d > pad.Falloff) continue;
     float t = Mathf.SmoothStep(0f, 1f, d / Mathf.Max(0.01f, pad.Falloff));
     h[zi, xi] = Mathf.Lerp(baseH, h[zi, xi], t);
    }
   }
  }

  // ---------- roads ----------
  static void BuildRoadField(float[,] h, float step) {
   int f = FieldRes;
   roadDist = new float[f * f];
   roadHeight = new float[f * f];
   roadKind = new byte[f * f];
   for (int i = 0; i < roadDist.Length; i++) { roadDist[i] = 999f; roadHeight[i] = float.NaN; }
   foreach (var road in OpenWorldRegions.Roads) {
    // Road grade: the pad-adjusted terrain, smoothed along the line so it climbs gently.
    var path = road.Path;
    var grade = new float[path.Length];
    for (int i = 0; i < path.Length; i++) grade[i] = SampleGrid(h, path[i].x, path[i].y, step);
    for (int pass = 0; pass < 6; pass++) {
     var g2 = (float[])grade.Clone();
     for (int i = 1; i < path.Length - 1; i++) g2[i] = (grade[i - 1] + grade[i] * 2f + grade[i + 1]) * 0.25f;
     grade = g2;
    }
    float reach = road.Width * 0.5f + 10f;
    for (int i = 0; i < path.Length - 1; i++) {
     Vector2 a = path[i], b = path[i + 1];
     int x0 = Cell(Mathf.Min(a.x, b.x) - reach), x1 = Cell(Mathf.Max(a.x, b.x) + reach);
     int z0 = Cell(Mathf.Min(a.y, b.y) - reach), z1 = Cell(Mathf.Max(a.y, b.y) + reach);
     Vector2 d = b - a; float len2 = Mathf.Max(0.0001f, d.sqrMagnitude);
     for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) {
      Vector2 p = new Vector2(CellX(xi), CellX(zi));
      float u = Mathf.Clamp01(Vector2.Dot(p - a, d) / len2);
      float dist = (p - (a + d * u)).magnitude - road.Width * 0.5f;
      int k = zi * f + xi;
      if (dist < roadDist[k]) {
       roadDist[k] = dist;
       roadHeight[k] = Mathf.Lerp(grade[i], grade[i + 1], u);
       roadKind[k] = (byte)road.Kind;
      }
     }
    }
   }
  }

  static void ApplyRoads(float[,] h, float step) {
   int n = HeightRes;
   for (int zi = 0; zi < n; zi++) for (int xi = 0; xi < n; xi++) {
    float x = -OpenWorldRegions.WorldHalf + xi * step, z = -OpenWorldRegions.WorldHalf + zi * step;
    int k = FieldIndex(x, z);
    float d = roadDist[k];
    if (d > 7f || float.IsNaN(roadHeight[k])) continue;
    float t = S(0.2f, 7f, d);
    // Slight camber: the road surface sits a few centimetres below the verge.
    h[zi, xi] = Mathf.Lerp(roadHeight[k] - 0.04f, h[zi, xi], t);
   }
  }

  public static float RoadDistance(float x, float z) {
   if (roadDist == null) return 999f;
   return roadDist[FieldIndex(x, z)];
  }
  public static OWRoadKind RoadKindAt(float x, float z) => roadKind == null ? OWRoadKind.Dirt : (OWRoadKind)roadKind[FieldIndex(x, z)];

  // ---------- water ----------
  static void ResolveLakes(float[,] h, float step) {
   foreach (var lake in OpenWorldRegions.Lakes) {
    // Surface sits a little below the lowest shoreline sample so the basin always holds water.
    float lowest = float.MaxValue;
    for (int a = 0; a < 48; a++) {
     float ang = a / 48f * Mathf.PI * 2f;
     Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
     Vector2 p = lake.Center + dir * lake.ShoreRadius(lake.Center + dir * lake.Radius) * 1.05f;
     lowest = Mathf.Min(lowest, SampleGrid(h, p.x, p.y, step));
    }
    float centre = SampleGrid(h, lake.Center.x, lake.Center.y, step);
    lake.SurfaceY = Mathf.Min(lowest, centre + 0.5f) - 0.25f;
   }
  }

  static readonly List<float[]> riverSurfaces = new List<float[]>();

  static void BuildRiverAndWaterField(float[,] h, float step) {
   int f = FieldRes;
   waterSurface = new float[f * f];
   waterDepth = new float[f * f];
   riverDist = new float[f * f];
   for (int i = 0; i < waterSurface.Length; i++) { waterSurface[i] = float.NaN; riverDist[i] = 999f; }
   // Lakes
   foreach (var lake in OpenWorldRegions.Lakes) {
    float reach = lake.Radius * 1.8f;
    int x0 = Cell(lake.Center.x - reach), x1 = Cell(lake.Center.x + reach), z0 = Cell(lake.Center.y - reach), z1 = Cell(lake.Center.y + reach);
    for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) {
     Vector2 p = new Vector2(CellX(xi), CellX(zi));
     float r = lake.ShoreRadius(p);
     float d = Vector2.Distance(p, lake.Center);
     if (d > r) continue;
     int k = zi * f + xi;
     waterSurface[k] = lake.SurfaceY;
     waterDepth[k] = lake.Depth * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r * 0.55f, d));
    }
   }
   // Rivers: monotonic surface descending toward the mouth, matched to lakes they feed.
   riverSurfaces.Clear();
   foreach (var river in OpenWorldRegions.Rivers) {
    var path = river.Path;
    var surface = new float[path.Length];
    for (int i = 0; i < path.Length; i++) surface[i] = SampleGrid(Natural, path[i].x, path[i].y, step) - 0.4f;
    // Join the lake at the mouth and source when they touch one.
    foreach (var lake in OpenWorldRegions.Lakes) {
     if (Vector2.Distance(path[path.Length - 1], lake.Center) < lake.Radius * 1.6f) surface[path.Length - 1] = lake.SurfaceY;
     if (Vector2.Distance(path[0], lake.Center) < lake.Radius * 1.6f) surface[0] = Mathf.Max(surface[0], lake.SurfaceY);
    }
    for (int i = 1; i < path.Length; i++) surface[i] = Mathf.Min(surface[i], surface[i - 1] - 0.005f);
    float mouth = surface[path.Length - 1];
    for (int i = path.Length - 2; i >= 0; i--) surface[i] = Mathf.Max(surface[i], mouth);
    for (int pass = 0; pass < 8; pass++) {
     var s2 = (float[])surface.Clone();
     for (int i = 1; i < path.Length - 1; i++) s2[i] = (surface[i - 1] + surface[i] + surface[i + 1]) / 3f;
     surface = s2;
    }
    for (int i = 1; i < path.Length; i++) surface[i] = Mathf.Min(surface[i], surface[i - 1]);
    riverSurfaces.Add(surface);
    float total = 0; var cum = new float[path.Length];
    for (int i = 1; i < path.Length; i++) { total += Vector2.Distance(path[i - 1], path[i]); cum[i] = total; }
    float maxHalf = Mathf.Max(river.WidthStart, river.WidthEnd) * 0.5f;
    for (int i = 0; i < path.Length - 1; i++) {
     Vector2 a = path[i], b = path[i + 1];
     float reach = maxHalf * 1.2f + 2f;
     int x0 = Cell(Mathf.Min(a.x, b.x) - reach), x1 = Cell(Mathf.Max(a.x, b.x) + reach);
     int z0 = Cell(Mathf.Min(a.y, b.y) - reach), z1 = Cell(Mathf.Max(a.y, b.y) + reach);
     Vector2 d = b - a; float len2 = Mathf.Max(0.0001f, d.sqrMagnitude);
     for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) {
      Vector2 p = new Vector2(CellX(xi), CellX(zi));
      float u = Mathf.Clamp01(Vector2.Dot(p - a, d) / len2);
      float t = total > 0 ? Mathf.Lerp(cum[i], cum[i + 1], u) / total : 0;
      float half = river.WidthAt(t) * 0.5f * (1f + (OpenWorldRegions.Noise(p.x * 0.05f, p.y * 0.05f) - 0.5f) * 0.3f);
      float dist = (p - (a + d * u)).magnitude;
      if (dist > half) continue;
      int k = zi * f + xi;
      // The nearest centre-line segment owns the cell, so depth falls off smoothly (no beading).
      if (dist >= riverDist[k]) continue;
      bool lakeCell = !float.IsNaN(waterSurface[k]) && riverDist[k] >= 999f;
      float surf = Mathf.Lerp(surface[i], surface[i + 1], u);
      float depth = river.Depth * (1f - Mathf.Pow(dist / half, 2f));
      // Fords where roads cross: shallow enough to wade and paint as gravel, ramping down to the
      // swimmable channel a few metres either side.
      if (roadDist[k] < 3f) depth = Mathf.Min(depth, 0.32f);
      else if (roadDist[k] < 8f) depth = Mathf.Min(depth, Mathf.Lerp(0.32f, river.Depth, Mathf.InverseLerp(3f, 8f, roadDist[k])));
      if (lakeCell && waterSurface[k] >= surf - 0.02f) { riverDist[k] = dist; continue; }
      riverDist[k] = dist;
      waterSurface[k] = surf; waterDepth[k] = Mathf.Max(0.05f, depth);
     }
    }
   }
  }

  static float[] waterDist; static float[] waterNear;

  // Two-pass chamfer distance transform: metres to the nearest water cell and that cell's surface.
  static void BuildWaterDistance() {
   int f = FieldRes;
   waterDist = new float[f * f];
   waterNear = new float[f * f];
   const float inf = 1e6f, d1 = CellToWorld, d2 = CellToWorld * 1.41421356f;
   for (int i = 0; i < waterDist.Length; i++) {
    bool wet = !float.IsNaN(waterSurface[i]);
    waterDist[i] = wet ? 0f : inf;
    waterNear[i] = wet ? waterSurface[i] : float.NaN;
   }
   for (int z = 0; z < f; z++) for (int x = 0; x < f; x++) {
    int k = z * f + x;
    if (x > 0) Relax(k, k - 1, d1);
    if (z > 0) {
     Relax(k, k - f, d1);
     if (x > 0) Relax(k, k - f - 1, d2);
     if (x < f - 1) Relax(k, k - f + 1, d2);
    }
   }
   for (int z = f - 1; z >= 0; z--) for (int x = f - 1; x >= 0; x--) {
    int k = z * f + x;
    if (x < f - 1) Relax(k, k + 1, d1);
    if (z < f - 1) {
     Relax(k, k + f, d1);
     if (x < f - 1) Relax(k, k + f + 1, d2);
     if (x > 0) Relax(k, k + f - 1, d2);
    }
   }
  }

  static void Relax(int k, int from, float cost) {
   float c = waterDist[from] + cost;
   if (c < waterDist[k]) { waterDist[k] = c; waterNear[k] = waterNear[from]; }
  }

  static void ApplyWater(float[,] h, float step) {
   BuildWaterDistance();
   int n = HeightRes;
   for (int zi = 0; zi < n; zi++) for (int xi = 0; xi < n; xi++) {
    float x = -OpenWorldRegions.WorldHalf + xi * step, z = -OpenWorldRegions.WorldHalf + zi * step;
    int k = FieldIndex(x, z);
    if (!float.IsNaN(waterSurface[k])) {
     h[zi, xi] = Mathf.Min(h[zi, xi], waterSurface[k] - waterDepth[k]);
     continue;
    }
    float r = waterDist[k];
    if (r > 14f || float.IsNaN(waterNear[k])) continue;
    float surf = waterNear[k];
    // Banks rise gently from the waterline; low ground is filled so water never spills,
    // high ground is graded down into a natural valley.
    float target = surf + 0.16f + r * 0.32f;
    if (h[zi, xi] > target) h[zi, xi] = Mathf.Lerp(target, h[zi, xi], S(1.5f, 14f, r));
    else h[zi, xi] = Mathf.Max(h[zi, xi], surf + 0.14f);
   }
  }

  public static bool WaterAt(float x, float z, out float surface, out float depth) {
   surface = 0; depth = 0;
   if (waterSurface == null) return false;
   int k = FieldIndex(x, z);
   if (float.IsNaN(waterSurface[k])) return false;
   surface = waterSurface[k]; depth = waterDepth[k];
   return true;
  }

  public static float DistanceToWater(float x, float z) {
   if (waterDist == null) return 999f;
   return Mathf.Min(999f, waterDist[FieldIndex(x, z)]);
  }

  public static float NearestWaterSurface(float x, float z) {
   if (waterNear == null) return float.NaN;
   return waterNear[FieldIndex(x, z)];
  }

  public static IReadOnlyList<float[]> RiverSurfaces => riverSurfaces;

  // ---------- helpers ----------
  static int Cell(float w) => Mathf.Clamp(Mathf.FloorToInt((w + OpenWorldRegions.WorldHalf) / CellToWorld), 0, FieldRes - 1);
  static float CellX(int i) => -OpenWorldRegions.WorldHalf + (i + 0.5f) * CellToWorld;
  static int FieldIndex(float x, float z) => Cell(z) * FieldRes + Cell(x);

  public static float SampleGrid(float[,] grid, float x, float z, float step) {
   int n = grid.GetLength(0);
   float fx = Mathf.Clamp((x + OpenWorldRegions.WorldHalf) / step, 0, n - 1.001f);
   float fz = Mathf.Clamp((z + OpenWorldRegions.WorldHalf) / step, 0, n - 1.001f);
   int ix = (int)fx, iz = (int)fz;
   float tx = fx - ix, tz = fz - iz;
   float a = Mathf.Lerp(grid[iz, ix], grid[iz, ix + 1], tx);
   float b = Mathf.Lerp(grid[iz + 1, ix], grid[iz + 1, ix + 1], tx);
   return Mathf.Lerp(a, b, tz);
  }

  public static float HeightAt(float x, float z) {
   if (Final != null) return SampleGrid(Final, x, z, Size / (Final.GetLength(0) - 1));
   if (runtimeTerrain) return runtimeTerrain.SampleHeight(new Vector3(x, 0f, z)) + runtimeTerrain.transform.position.y;
   return 0f;
  }

  // Height of the natural landform before pads, roads and water were cut in (bake time only).
  public static float NaturalHeightAt(float x, float z) {
   if (Natural != null) return SampleGrid(Natural, x, z, Size / (Natural.GetLength(0) - 1));
   return HeightAt(x, z);
  }

  // Normal of the sculpted surface (central differences over ~1 m).
  public static Vector3 NormalAt(float x, float z) {
   float hx = HeightAt(x + 1f, z) - HeightAt(x - 1f, z);
   float hz = HeightAt(x, z + 1f) - HeightAt(x, z - 1f);
   return new Vector3(-hx, 2f, -hz).normalized;
  }

  // ---------- baked field persistence ----------
  // Layout: magic, version, res, lakes (surface) then gzip-compressed per-cell bytes:
  // road distance (0.1 m), road kind, water surface (cm, 0xFFFF dry), water depth (cm), water distance (0.25 m).
  public static byte[] SerializeFields() {
   int f = FieldRes, cells = f * f;
   using (var ms = new MemoryStream()) {
    using (var bwr = new BinaryWriter(ms)) {
     bwr.Write(0x4C52464Cu); bwr.Write(2); bwr.Write(f);
     bwr.Write(OpenWorldRegions.Lakes.Count);
     foreach (var lake in OpenWorldRegions.Lakes) bwr.Write(lake.SurfaceY);
     bwr.Write(riverSurfaces.Count);
     foreach (var rs in riverSurfaces) { bwr.Write(rs.Length); foreach (var v in rs) bwr.Write(v); }
     var raw = new byte[cells * 7];
     for (int k = 0; k < cells; k++) {
      int o = k * 7;
      raw[o] = (byte)Mathf.Clamp(Mathf.RoundToInt((roadDist[k] + 2f) * 10f), 0, 255);
      raw[o + 1] = roadKind[k];
      ushort surf = float.IsNaN(waterSurface[k]) ? (ushort)0xFFFF : (ushort)Mathf.Clamp(Mathf.RoundToInt((waterSurface[k] + 10f) * 100f), 0, 0xFFFE);
      raw[o + 2] = (byte)(surf & 0xFF); raw[o + 3] = (byte)(surf >> 8);
      raw[o + 4] = (byte)Mathf.Clamp(Mathf.RoundToInt(waterDepth[k] * 100f), 0, 255);
      raw[o + 5] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(waterDist[k], 63f) * 4f), 0, 255);
      ushort near = float.IsNaN(waterNear[k]) ? (ushort)0xFFFF : (ushort)Mathf.Clamp(Mathf.RoundToInt((waterNear[k] + 10f) * 100f), 0, 0xFFFE);
      raw[o + 6] = (byte)(near >> 8);
     }
     using (var packed = new MemoryStream()) {
      using (var gz = new GZipStream(packed, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
      var bytes = packed.ToArray();
      bwr.Write(bytes.Length); bwr.Write(bytes);
     }
    }
    return ms.ToArray();
   }
  }

  public static bool LoadFields(byte[] data, Terrain terrain) {
   try {
    OpenWorldRegions.Initialise();
    using (var ms = new MemoryStream(data))
    using (var br = new BinaryReader(ms)) {
     if (br.ReadUInt32() != 0x4C52464Cu) return false;
     int version = br.ReadInt32(); int f = br.ReadInt32();
     if (version != 2 || f != FieldRes) return false;
     int lakes = br.ReadInt32();
     for (int i = 0; i < lakes; i++) { float s = br.ReadSingle(); if (i < OpenWorldRegions.Lakes.Count) OpenWorldRegions.Lakes[i].SurfaceY = s; }
     riverSurfaces.Clear();
     int rivers = br.ReadInt32();
     for (int i = 0; i < rivers; i++) { int len = br.ReadInt32(); var rs = new float[len]; for (int j = 0; j < len; j++) rs[j] = br.ReadSingle(); riverSurfaces.Add(rs); }
     int packedLen = br.ReadInt32();
     var packed = br.ReadBytes(packedLen);
     int cells = f * f;
     var raw = new byte[cells * 7];
     using (var pms = new MemoryStream(packed))
     using (var gz = new GZipStream(pms, CompressionMode.Decompress)) {
      int read = 0;
      while (read < raw.Length) { int r = gz.Read(raw, read, raw.Length - read); if (r <= 0) break; read += r; }
     }
     roadDist = new float[cells]; roadKind = new byte[cells]; roadHeight = null;
     waterSurface = new float[cells]; waterDepth = new float[cells]; waterDist = new float[cells]; waterNear = new float[cells];
     for (int k = 0; k < cells; k++) {
      int o = k * 7;
      roadDist[k] = raw[o] >= 255 ? 999f : raw[o] / 10f - 2f;
      roadKind[k] = raw[o + 1];
      int surf = raw[o + 2] | (raw[o + 3] << 8);
      waterSurface[k] = surf == 0xFFFF ? float.NaN : surf / 100f - 10f;
      waterDepth[k] = raw[o + 4] / 100f;
      waterDist[k] = raw[o + 5] >= 252 ? 999f : raw[o + 5] / 4f;
      waterNear[k] = float.NaN;
     }
     // Nearest-surface is only needed close to water: reuse the wet neighbours' surface.
     for (int k = 0; k < cells; k++) if (!float.IsNaN(waterSurface[k])) waterNear[k] = waterSurface[k];
    }
    Natural = null; Final = null;
    runtimeTerrain = terrain;
    Ready = true;
    return true;
   } catch (Exception e) {
    Debug.LogWarning("[Realm] baked fields unreadable: " + e.Message);
    return false;
   }
  }
 }
}
