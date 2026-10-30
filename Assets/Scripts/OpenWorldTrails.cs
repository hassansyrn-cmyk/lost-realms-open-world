using UnityEngine;

namespace LostRealmsOW {
 // 4-Layer Terrain Splatting (Genshin Impact / Breath of the Wild / Witcher 3 style):
 // - Layer 0: Lush Emerald Moss (forest canopy & valleys)
 // - Layer 1: Worn Soil & Cobble Trail (5 main routes + Hub Sanctuary circle)
 // - Layer 2: Sunlit Highland Meadow (golden-emerald hilltops & glades)
 // - Layer 3: Weathered Cliff Stone (steep slopes & perimeter mountain ridges)
 public sealed class OpenWorldTrails : MonoBehaviour {
  public static readonly Vector3[][] Routes = {
   new[] { new Vector3(0,0,-6), new Vector3(-24,0,20), new Vector3(-44,0,42), new Vector3(-65,0,65) },
   new[] { new Vector3(0,0,-6), new Vector3(25,0,16), new Vector3(46,0,40), new Vector3(68,0,58) },
   new[] { new Vector3(0,0,-6), new Vector3(24,0,-26), new Vector3(45,0,-44), new Vector3(62,0,-62) },
   new[] { new Vector3(0,0,-6), new Vector3(-22,0,-24), new Vector3(-44,0,-40), new Vector3(-60,0,-58) },
   new[] { new Vector3(0,0,18), new Vector3(8,0,44), new Vector3(-6,0,72), new Vector3(0,0,115) },
   new[] { new Vector3(0,0,-6), new Vector3(-30,0,-32), new Vector3(-60,0,-70), new Vector3(-90,0,-100) }
  };

  TerrainData ownedTerrain;
  Material ownedMaterial;
  Texture2D soilTex, highlandTex, cliffTex;
  TerrainLayer[] ownedLayers;

  public void Build(Terrain terrain) {
   ownedTerrain = terrain.terrainData;
   ownedMaterial = terrain.materialTemplate;
   // Imported layers are shared assets; only the generated initial moss layer is owned here.
   var initialLayers = ownedTerrain.terrainLayers;
   if (OpenWorldTerrainSurface.Apply(ownedTerrain)) {
    ownedLayers = initialLayers;
    return;
   }

   soilTex = CreateProceduralTexture("VerdantTrailSoil",
    new Color(0.20f, 0.14f, 0.085f), new Color(0.42f, 0.32f, 0.20f), 8f, 0.22f);
   highlandTex = CreateProceduralTexture("VerdantHighlandMeadow",
    new Color(0.14f, 0.36f, 0.16f), new Color(0.32f, 0.54f, 0.22f), 6f, 0.18f);
   cliffTex = CreateProceduralTexture("VerdantCliffStone",
    new Color(0.24f, 0.27f, 0.30f), new Color(0.46f, 0.49f, 0.52f), 10f, 0.28f);

   var existing = ownedTerrain.terrainLayers;
   var moss = existing.Length > 0 ? existing[0] : new TerrainLayer { diffuseTexture = highlandTex, tileSize = Vector2.one * 14f };
   var dirt = new TerrainLayer { diffuseTexture = soilTex, tileSize = Vector2.one * 3.8f, smoothness = 0.06f, metallic = 0f };
   var highland = new TerrainLayer { diffuseTexture = highlandTex, tileSize = Vector2.one * 11f, smoothness = 0.10f, metallic = 0f };
   var cliff = new TerrainLayer { diffuseTexture = cliffTex, tileSize = Vector2.one * 9f, smoothness = 0.16f, metallic = 0.05f };

   ownedLayers = new[] { moss, dirt, highland, cliff };
   ownedTerrain.terrainLayers = ownedLayers;

   const int resolution = 512;
   ownedTerrain.alphamapResolution = resolution;
   var weights = new float[resolution, resolution, 4];
   Vector3 terrainOrigin = terrain.transform.position;
   Vector3 size = ownedTerrain.size;

   for (int z = 0; z < resolution; z++) {
    float normZ = (z + 0.5f) / resolution;
    for (int x = 0; x < resolution; x++) {
     float normX = (x + 0.5f) / resolution;
     Vector3 p = terrainOrigin + new Vector3(normX * size.x, 0f, normZ * size.z);

     float distToTrail = DistanceToTrail(p);
     float hubRing = Mathf.Abs(new Vector2(p.x, p.z).magnitude - 8.5f);
     float minPathDist = Mathf.Min(distToTrail, hubRing * 1.15f);

     float width = 1.95f + Mathf.PerlinNoise(p.x * 0.18f + 100f, p.z * 0.18f + 100f) * 0.65f;
     float trailWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width - 1f, width + 0.65f, minPathDist));

     float steepness = ownedTerrain.GetSteepness(normX, normZ);
     float altitude = ownedTerrain.GetInterpolatedHeight(normX, normZ) / Mathf.Max(1f, size.y);
     float cliffWeight = Mathf.Clamp01(Mathf.InverseLerp(18f, 34f, steepness) + Mathf.InverseLerp(0.22f, 0.45f, altitude) * 0.75f);
     cliffWeight *= (1f - trailWeight);

     float meadowNoise = Mathf.PerlinNoise(p.x * 0.032f + 42f, p.z * 0.032f + 42f);
     float highlandWeight = Mathf.Clamp01((meadowNoise - 0.36f) * 1.45f) * (1f - trailWeight) * (1f - cliffWeight);

     float mossWeight = Mathf.Max(0f, 1f - (trailWeight + cliffWeight + highlandWeight));

     float sum = mossWeight + trailWeight + highlandWeight + cliffWeight;
     if (sum > 0.0001f) {
      weights[z, x, 0] = mossWeight / sum;
      weights[z, x, 1] = trailWeight / sum;
      weights[z, x, 2] = highlandWeight / sum;
      weights[z, x, 3] = cliffWeight / sum;
     } else {
      weights[z, x, 0] = 1f;
     }
    }
   }
   ownedTerrain.SetAlphamaps(0, 0, weights);
  }

  static Texture2D CreateProceduralTexture(string name, Color dark, Color light, float scale, float grainWeight) {
   var tex = new Texture2D(128, 128, TextureFormat.RGB24, true) { name = name, wrapMode = TextureWrapMode.Repeat };
   var colors = new Color[128 * 128];
   for (int y = 0; y < 128; y++) {
    for (int x = 0; x < 128; x++) {
     float u = x / 128f, v = y / 128f;
     float n = Mathf.Lerp(
      Mathf.Lerp(Mathf.PerlinNoise(u * scale, v * scale), Mathf.PerlinNoise(u * scale - scale, v * scale), u),
      Mathf.Lerp(Mathf.PerlinNoise(u * scale, v * scale - scale), Mathf.PerlinNoise(u * scale - scale, v * scale - scale), u), v);
     float grain = ((x * 1973 + y * 9277 + x * y * 13) & 255) / 255f;
     colors[y * 128 + x] = Color.Lerp(dark, light, n * (1f - grainWeight) + grain * grainWeight);
    }
   }
   tex.SetPixels(colors);
   tex.Apply(true, true);
   return tex;
  }

  public static bool NearTrail(Vector3 point, float clearance) => DistanceToTrail(point) < clearance;

  public static float DistanceToTrail(Vector3 point) {
   float nearest = float.MaxValue;
   Vector2 p = new Vector2(point.x, point.z);
   foreach (var route in Routes) {
    for (int i = 0; i < route.Length - 1; i++) {
     Vector2 a = new Vector2(route[i].x, route[i].z);
     Vector2 b = new Vector2(route[i + 1].x, route[i + 1].z);
     Vector2 d = b - a;
     float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
     nearest = Mathf.Min(nearest, (p - (a + d * t)).sqrMagnitude);
    }
   }
   return Mathf.Sqrt(nearest);
  }

  void OnDestroy() {
   if (ownedLayers != null) foreach (var layer in ownedLayers) if (layer) Destroy(layer);
   if (soilTex) Destroy(soilTex);
   if (highlandTex) Destroy(highlandTex);
   if (cliffTex) Destroy(cliffTex);
   if (ownedTerrain) Destroy(ownedTerrain);
   if (ownedMaterial) Destroy(ownedMaterial);
  }
 }
}
