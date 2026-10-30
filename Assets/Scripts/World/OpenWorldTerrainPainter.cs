using UnityEngine;

namespace LostRealmsOW {
 // Paints the eight-layer splat map from biome, slope, altitude, roads, pads and water.
 // Layer order: Grass, Forest floor, Dirt, Cobblestone, Rock, Cliff, Sand, Snow.
 public static class OpenWorldTerrainPainter {
  public const int AlphaRes = 1024;
  public static readonly string[] LayerNames = { "Grass", "Forest", "Dirt", "Cobble", "Rock", "Cliff", "Sand", "Snow" };
  public const int Grass = 0, Forest = 1, Dirt = 2, Cobble = 3, Rock = 4, Cliff = 5, Sand = 6, Snow = 7;
  public static bool UsingFullPalette { get; private set; }
  public static float[,,] LastMap { get; private set; }   // kept for the ecosystem's detail pass, then released

  static float S(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

  // Shared with the ecosystem so trees grow where the forest floor is painted.
  public static float ForestMask(float x, float z) {
   float n = OpenWorldRegions.Fbm(x * 0.0085f + 71.3f, z * 0.0085f + 18.6f, 3);
   float n2 = OpenWorldRegions.Noise(x * 0.035f + 5.5f, z * 0.035f + 9.1f);
   return S(0.48f, 0.62f, n + (n2 - 0.5f) * 0.12f);
  }

  public static float MeadowFlowers(float x, float z) {
   return S(0.58f, 0.72f, OpenWorldRegions.Fbm(x * 0.02f + 12.1f, z * 0.02f + 33.3f, 2));
  }

  public static float Slope(float x, float z) {
   float hx = OpenWorldTerrainSculptor.HeightAt(x + 1f, z) - OpenWorldTerrainSculptor.HeightAt(x - 1f, z);
   float hz = OpenWorldTerrainSculptor.HeightAt(x, z + 1f) - OpenWorldTerrainSculptor.HeightAt(x, z - 1f);
   return Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) * 0.5f) * Mathf.Rad2Deg;
  }

  // Slope of the natural landform. Banks cut by village pads, roads and terraces are packed earth
  // rather than bedrock, so rock and cliff follow the natural slope (plus a little tolerance).
  public static float NaturalSlope(float x, float z) {
   float hx = OpenWorldTerrainSculptor.NaturalHeightAt(x + 1f, z) - OpenWorldTerrainSculptor.NaturalHeightAt(x - 1f, z);
   float hz = OpenWorldTerrainSculptor.NaturalHeightAt(x, z + 1f) - OpenWorldTerrainSculptor.NaturalHeightAt(x, z - 1f);
   return Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) * 0.5f) * Mathf.Rad2Deg;
  }

  public static void ReleaseMap() { LastMap = null; }

  public static bool Apply(TerrainData data) {
   var layers = new TerrainLayer[LayerNames.Length];
   for (int i = 0; i < LayerNames.Length; i++) {
    layers[i] = Resources.Load<TerrainLayer>("World/Terrain/" + LayerNames[i]);
    if (!layers[i] || !layers[i].diffuseTexture) { UsingFullPalette = false; return false; }
   }
   UsingFullPalette = true;
   data.terrainLayers = layers;
   data.alphamapResolution = AlphaRes;
   int r = AlphaRes;
   var map = new float[r, r, layers.Length];
   var bw = new float[5];
   var wv = new float[8];
   float px = OpenWorldRegions.WorldHalf * 2f / r;
   for (int zi = 0; zi < r; zi++) {
    float z = -OpenWorldRegions.WorldHalf + (zi + 0.5f) * px;
    for (int xi = 0; xi < r; xi++) {
     float x = -OpenWorldRegions.WorldHalf + (xi + 0.5f) * px;
     for (int i = 0; i < 8; i++) wv[i] = 0f;
     OpenWorldRegions.BiomeWeights(x, z, bw);
     float h = OpenWorldTerrainSculptor.HeightAt(x, z);
     float slope = Slope(x, z);
     float n1 = OpenWorldRegions.Noise(x * 0.045f + 3.3f, z * 0.045f + 1.9f);
     float n2 = OpenWorldRegions.Noise(x * 0.13f + 8.1f, z * 0.13f + 4.4f);
     float forest = ForestMask(x, z);
     // Verdant meadows and woods
     float v = bw[0];
     if (v > 0.001f) {
      wv[Forest] += v * forest * 0.85f;
      wv[Grass] += v * (1f - forest * 0.85f);
      wv[Dirt] += v * S(0.74f, 0.86f, n1) * 0.55f * (1f - forest);
     }
     // Frozen peaks
     float f = bw[1];
     if (f > 0.001f) {
      float lowForest = forest * (1f - S(70f, 120f, h));
      wv[Snow] += f * (1f - lowForest * 0.35f);
      wv[Forest] += f * lowForest * 0.35f;
      wv[Rock] += f * S(0.78f, 0.9f, n2); // outcrops through the snow (see Separate)
     }
     // Burning dunes
     float d = bw[2];
     if (d > 0.001f) {
      wv[Sand] += d;
      wv[Dirt] += d * S(0.7f, 0.85f, n1) * 0.35f;
     }
     // Hollow marches
     float g = bw[3];
     if (g > 0.001f) {
      wv[Forest] += g * 0.6f;
      wv[Dirt] += g * (0.25f + S(0.6f, 0.8f, n1) * 0.4f);
      wv[Grass] += g * 0.25f * (1f - n2);
     }
     // Stormveil cliffs
     float s = bw[4];
     if (s > 0.001f) {
      wv[Rock] += s * 0.65f;
      wv[Grass] += s * S(0.45f, 0.7f, n1) * 0.55f;
      wv[Dirt] += s * 0.12f;
     }
     // Slopes become rock then cliff regardless of biome; snow clings to gentler rock. Sculpted banks
     // (pads, roads) keep the ground of their biome: only natural steepness exposes bedrock.
     float rockSlope = Mathf.Min(slope, NaturalSlope(x, z) + 6f);
     // Transitions break into patches (outcrops, drifts) instead of a uniform cross-fade: two very
     // different textures blended 50/50 read as veined marble.
     float patch = OpenWorldRegions.Noise(x * 0.11f + 21.7f, z * 0.11f + 4.9f) - 0.5f;
     float rockT = S(0.25f, 0.75f, S(24f, 34f, rockSlope) + patch * 0.6f), cliffT = S(36f, 48f, rockSlope);
     if (rockT > 0f) {
      float sum = Sum(wv);
      for (int i = 0; i < 8; i++) wv[i] *= 1f - rockT;
      // Faces band between broken rock and sheer cliff so a mountainside is not one repeated texture.
      float band = S(0.35f, 0.65f, OpenWorldRegions.Noise(x * 0.021f + h * 0.012f + 3.7f, z * 0.021f + 6.1f));
      float cliffShare = cliffT * Mathf.Lerp(0.55f, 1f, band);
      wv[Rock] += sum * rockT * (1f - cliffShare);
      wv[Cliff] += sum * rockT * cliffShare;
      // Snow lodges on gentler frozen rock and wind-blown sand on desert rock; Separate() below turns
      // these shares into drifts with broken edges rather than an even veil.
      if (f > 0.2f) wv[Snow] += sum * rockT * (1f - cliffT) * 0.75f * f;
      if (d > 0.2f) wv[Sand] += sum * rockT * (1f - cliffT) * 0.6f * d;
     }
     // High summits stay snow-capped everywhere. The snow line sits lower in the north and higher over
     // the dunes, lower again on shaded north faces, and sheer faces stay bare rock because snow cannot
     // settle on them.
     var nrm = OpenWorldTerrainSculptor.NormalAt(x, z);
     float shade = Mathf.Clamp01(nrm.z * 2.5f); // facing north, away from the sun's arc
     float snowLine = 150f - 36f * f + 60f * d - 18f * shade;
     // Sheer faces shed snow, so mid-mountain cliffs stay dark between white shoulders, keeping it only
     // on ledges that follow the strata (contour-aligned noise); the highest walls ice over.
     float sheerShed = Mathf.Lerp(0.85f, 0.35f, S(snowLine + 40f, snowLine + 120f, h));
     float ledge = S(0.56f, 0.68f, OpenWorldRegions.Noise(x * 0.035f + 13.1f, h * 0.05f + z * 0.035f + 5.3f));
     float cap = S(0.2f, 0.8f, S(snowLine, snowLine + 36f, h + n2 * 14f) + patch * 0.5f) * (1f - cliffT * sheerShed * (1f - ledge * 0.75f));
     if (cap > 0f) { Scale(wv, 1f - cap); wv[Snow] += cap; }
     // Shorelines and river banks
     float water = OpenWorldTerrainSculptor.DistanceToWater(x, z);
     if (water < 6f) {
      float bank = 1f - S(0.5f, 6f, water);
      Scale(wv, 1f - bank * 0.8f);
      if (d > 0.5f) { wv[Grass] += bank * 0.5f; wv[Sand] += bank * 0.3f; }
      else if (f > 0.5f) { wv[Rock] += bank * 0.5f; wv[Snow] += bank * 0.3f; }
      else { wv[Sand] += bank * 0.35f * (1f - g); wv[Dirt] += bank * 0.3f; wv[Rock] += bank * 0.15f; }
     }
     // Settlement plazas and yards
     float plaza = PlazaWeight(x, z, out int plazaLayer);
     if (plaza > 0f) { Scale(wv, 1f - plaza); wv[plazaLayer] += plaza; }
     // Trodden ground around every building so it reads as lived-in, not dropped on grass.
     float yard = YardWeight(x, z);
     if (yard > 0f) {
      Scale(wv, 1f - yard * 0.85f);
      if (d > 0.5f) { wv[Sand] += yard * 0.5f; wv[Dirt] += yard * 0.35f; }
      else if (f > 0.5f) { wv[Dirt] += yard * 0.6f; wv[Snow] += yard * 0.25f; }
      else { wv[Dirt] += yard * 0.65f; wv[Grass] += yard * 0.2f; }
     }
     // Roads with soft, noisy verges.
     float rd = OpenWorldTerrainSculptor.RoadDistance(x, z) + (n2 - 0.5f) * 0.9f;
     if (rd < 2.4f) {
      float road = 1f - S(-0.4f, 2.4f, rd);
      var kind = OpenWorldTerrainSculptor.RoadKindAt(x, z);
      Scale(wv, 1f - road);
      switch (kind) {
       case OWRoadKind.Cobble: wv[Cobble] += road * S(1.4f, -0.2f, rd); wv[Dirt] += road * (1f - S(1.4f, -0.2f, rd)); break;
       case OWRoadKind.Sand: wv[Dirt] += road * 0.55f; wv[Sand] += road * 0.45f; break;
       case OWRoadKind.Snow: wv[Dirt] += road * 0.62f; wv[Snow] += road * 0.38f; break;
       default: wv[Dirt] += road * 0.85f; wv[Grass] += road * 0.15f * S(0f, 2.4f, rd); break;
      }
     }
     float edge = (OpenWorldRegions.Noise(x * 0.09f + 51.3f, z * 0.09f + 17.9f) - 0.5f) * 0.8f
                + (OpenWorldRegions.Noise(x * 0.29f + 7.7f, z * 0.29f + 63.1f) - 0.5f) * 0.4f;
     Separate(wv, Snow, edge);
     Separate(wv, Sand, edge);
     float total = Sum(wv);
     if (total < 0.0001f) { wv[Grass] = 1f; total = 1f; }
     for (int i = 0; i < 8; i++) map[zi, xi, i] = wv[i] / total;
    }
   }
   data.SetAlphamaps(0, 0, map);
   LastMap = map;
   return true;
  }

  static float Sum(float[] a) { float s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }

  // Snow or sand mixed thinly into dark, cracked rock reads as veined marble (and white-washes whole
  // mountainsides). Where a cover layer meets stone, the mix is pushed to one side of a noisy
  // threshold: a thin share becomes scattered drifts, an even share half-and-half patches, and every
  // edge is crisp and broken (a few metres wide) instead of a translucent veil.
  static void Separate(float[] wv, int cover, float edge) {
   float c = wv[cover], stone = wv[Rock] + wv[Cliff];
   if (c < 0.0001f || stone < 0.0001f) return;
   float pair = c + stone;
   float t = S(0.45f, 0.55f, c / pair + edge);
   wv[cover] = pair * t;
   float k = pair * (1f - t) / stone;
   wv[Rock] *= k;
   wv[Cliff] *= k;
  }
  static void Scale(float[] a, float k) { for (int i = 0; i < a.Length; i++) a[i] *= k; }

  static float YardWeight(float x, float z) {
   var p = new Vector2(x, z);
   float best = 0f;
   foreach (var f in OpenWorldSettlementPlans.BuildingFootprints) {
    if ((p - f.Center).sqrMagnitude > (f.Half.magnitude + 5f) * (f.Half.magnitude + 5f)) continue;
    for (int i = 4; i >= 1; i--) if (f.Contains(p, i * 0.9f)) best = Mathf.Max(best, 1f - (i - 1) / 4f);
   }
   return best * 0.9f;
  }

  // Paved settlement ground: the hub plaza, Sunspire streets, Frostpine square and similar.
  public static float PlazaWeight(float x, float z, out int layer) {
   layer = Cobble;
   Vector2 p = new Vector2(x, z);
   float hub = 1f - S(15f, 19f, p.magnitude);
   if (hub > 0f) return hub * 0.92f;
   float best = 0f;
   foreach (var plaza in OpenWorldSettlementPlans.Plazas) {
    float dd = Vector2.Distance(p, plaza.Center);
    float wgt = 1f - S(plaza.Radius * 0.75f, plaza.Radius, dd);
    if (wgt > best) { best = wgt; layer = plaza.Layer; }
   }
   return best;
  }
 }

 public struct OWPlaza { public Vector2 Center; public float Radius; public int Layer; }
}
