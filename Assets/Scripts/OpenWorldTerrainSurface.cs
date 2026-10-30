using UnityEngine;

namespace LostRealmsOW {
 public static class OpenWorldTerrainSurface {
  // Four layers keep the terrain in one URP splat pass on the mobile baseline.
  public static bool Apply(TerrainData data) {
   var layers = new TerrainLayer[4];
   string[] names = { "Grass", "Dirt", "Rock", "Snow" };
   for (int i = 0; i < names.Length; i++) {
    layers[i] = Resources.Load<TerrainLayer>("World/Terrain/" + names[i]);
    if (!layers[i] || !layers[i].diffuseTexture) return false;
   }
   data.terrainLayers = layers;
   data.alphamapResolution = 256;
   int resolution = data.alphamapResolution;
   var paint = new float[resolution, resolution, 4];
   for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++) {
    float u = x / (float)(resolution - 1), v = z / (float)(resolution - 1);
    var p = new Vector2((u - .5f) * data.size.x, (v - .5f) * data.size.z);
    float noise = Mathf.PerlinNoise(p.x * .035f + 42, p.y * .035f + 17);
    float snow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(108, 145, p.y + (noise - .5f) * 18));
    float rock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(20, 42, data.GetSteepness(u, v)));
    float roadDistance = Mathf.Min(OpenWorldTrails.DistanceToTrail(new Vector3(p.x, 0, p.y)), Mathf.Abs(p.magnitude - 8.5f));
    float road = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.4f, 3.5f, roadDistance));
    float village = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 23, Vector2.Distance(p, new Vector2(-85, -95))));
    float dry = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(95, 145, p.x)) * (1 - snow);
    float dirt = Mathf.Max(dry, Mathf.Max(road, village * .7f));
    paint[z, x, 3] = snow * (1 - rock * .65f);
    paint[z, x, 2] = rock;
    float remaining = 1 - paint[z, x, 3] - rock;
    // Rock and snow overlap: normalize rather than allowing negative weights.
    remaining = Mathf.Max(0, remaining);
    paint[z, x, 1] = dirt * remaining;
    paint[z, x, 0] = (1 - dirt) * remaining;
    float total = paint[z, x, 0] + paint[z, x, 1] + paint[z, x, 2] + paint[z, x, 3];
    for (int i = 0; i < 4; i++) paint[z, x, i] /= Mathf.Max(.001f, total);
   }
   data.SetAlphamaps(0, 0, paint);
   return true;
  }
 }
}
