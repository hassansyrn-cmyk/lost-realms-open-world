using UnityEngine;

namespace LostRealmsOW {
 // Paints an illustrated top-down atlas of the sculpted realm: biome palette, hill shading,
 // water, roads and contour hints. Used by the in-game map and by editor previews.
 public static class OpenWorldMapRenderer {
  static readonly float[] w = new float[5];
  static readonly Color[] BiomeLow = {
   new Color(0.36f, 0.55f, 0.27f), new Color(0.80f, 0.86f, 0.92f), new Color(0.86f, 0.72f, 0.46f),
   new Color(0.30f, 0.34f, 0.27f), new Color(0.47f, 0.47f, 0.50f)
  };
  static readonly Color[] BiomeHigh = {
   new Color(0.24f, 0.42f, 0.22f), new Color(0.96f, 0.97f, 1.00f), new Color(0.72f, 0.52f, 0.33f),
   new Color(0.22f, 0.25f, 0.21f), new Color(0.36f, 0.36f, 0.40f)
  };

  public static Texture2D Build(int size) {
   var tex = new Texture2D(size, size, TextureFormat.RGB24, false) { name = "OW_RealmAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
   var cols = new Color[size * size];
   float world = OpenWorldRegions.WorldHalf * 2f;
   float px = world / size;
   Vector3 light = new Vector3(-0.55f, 0.75f, 0.38f).normalized;
   for (int y = 0; y < size; y++) {
    float z = -OpenWorldRegions.WorldHalf + (y + 0.5f) * px;
    for (int x = 0; x < size; x++) {
     float wx = -OpenWorldRegions.WorldHalf + (x + 0.5f) * px;
     float h = OpenWorldTerrainSculptor.HeightAt(wx, z);
     float hx = OpenWorldTerrainSculptor.HeightAt(wx + px, z) - OpenWorldTerrainSculptor.HeightAt(wx - px, z);
     float hz = OpenWorldTerrainSculptor.HeightAt(wx, z + px) - OpenWorldTerrainSculptor.HeightAt(wx, z - px);
     Vector3 n = new Vector3(-hx, 2f * px, -hz).normalized;
     float shade = Mathf.Clamp01(Vector3.Dot(n, light) * 0.8f + 0.35f);
     OpenWorldRegions.BiomeWeights(wx, z, w);
     Color low = Color.black, high = Color.black;
     for (int i = 0; i < 5; i++) { low += BiomeLow[i] * w[i]; high += BiomeHigh[i] * w[i]; }
     float t = Mathf.InverseLerp(0f, 120f, h);
     Color c = Color.Lerp(low, high, t);
     if (h > 95f) c = Color.Lerp(c, new Color(0.94f, 0.95f, 0.98f), Mathf.InverseLerp(95f, 140f, h));
     c *= shade;
     // Contour hint every 12 m.
     float contour = Mathf.Abs(Mathf.Repeat(h, 12f) - 6f);
     if (contour > 5.6f) c *= 0.9f;
     if (OpenWorldTerrainSculptor.RoadDistance(wx, z) < 0.6f) c = Color.Lerp(c, new Color(0.62f, 0.5f, 0.32f), 0.85f);
     if (OpenWorldTerrainSculptor.WaterAt(wx, z, out _, out float depth)) {
      c = Color.Lerp(new Color(0.42f, 0.70f, 0.80f), new Color(0.16f, 0.38f, 0.58f), Mathf.Clamp01(depth));
      if (IsFrozen(wx, z)) c = new Color(0.78f, 0.9f, 0.97f);
     }
     c.a = 1f;
     cols[y * size + x] = c;
    }
   }
   // Parchment-style vignette at the realm edge.
   for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
    float v = Mathf.Clamp01(1.15f - Mathf.Sqrt(dx * dx + dy * dy) * 1.25f);
    cols[y * size + x] = Color.Lerp(new Color(0.18f, 0.14f, 0.10f), cols[y * size + x], 0.35f + 0.65f * v);
   }
   tex.SetPixels(cols);
   tex.Apply(false, false);
   return tex;
  }

  static bool IsFrozen(float x, float z) {
   foreach (var lake in OpenWorldRegions.Lakes) if (lake.Frozen && Vector2.Distance(new Vector2(x, z), lake.Center) < lake.Radius * 1.4f) return true;
   return false;
  }

  // World position to normalised [-1,1] map coordinates.
  public static Vector2 WorldToNormalised(Vector3 world) =>
   new Vector2(Mathf.Clamp(world.x / OpenWorldRegions.WorldHalf, -1f, 1f), Mathf.Clamp(world.z / OpenWorldRegions.WorldHalf, -1f, 1f));
 }
}
