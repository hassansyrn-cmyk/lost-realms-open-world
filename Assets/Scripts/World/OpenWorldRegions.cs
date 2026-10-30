using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 public enum OWBiome { Verdant = 0, Frozen = 1, Dunes = 2, Graveyard = 3, Storm = 4 }
 public enum OWRoadKind { Dirt = 0, Cobble = 1, Sand = 2, Snow = 3 }

 // A flattened building or plaza footprint. Heights blend from the pad back to the sculpted land.
 public struct OWPad {
  public Vector2 Center;
  public Vector2 HalfSize;     // rectangle half extents (x,z) before rotation
  public float Yaw;            // degrees
  public float Falloff;        // metres of smooth blend outside the rectangle
  public float Height;         // NaN = use the natural height at the centre
  public float Offset;         // added to the resolved height (raise or sink a plaza)
  public bool Round;
  public bool Late;            // applied after roads (building yards stay perfectly level)
 }

 public sealed class OWRoad {
  public string Name;
  public OWRoadKind Kind;
  public float Width;
  public Vector2[] Path;       // densely resampled centre line
  public OWRoad(string name, OWRoadKind kind, float width, params Vector2[] controls) {
   Name = name; Kind = kind; Width = width; Path = OpenWorldRegions.Smooth(controls, 3f);
  }
 }

 public sealed class OWRiver {
  public string Name;
  public Vector2[] Path;
  public float WidthStart, WidthEnd, Depth;
  public OWRiver(string name, float widthStart, float widthEnd, float depth, params Vector2[] controls) {
   Name = name; WidthStart = widthStart; WidthEnd = widthEnd; Depth = depth; Path = OpenWorldRegions.Smooth(controls, 3f);
  }
  public float WidthAt(float t) => Mathf.Lerp(WidthStart, WidthEnd, t);
 }

 public sealed class OWLake {
  public string Name;
  public Vector2 Center;
  public float Radius;
  public float Depth;
  public bool Frozen;
  public float Seed;
  public float SurfaceY;       // resolved by the sculptor
  public OWLake(string name, Vector2 center, float radius, float depth, bool frozen, float seed) {
   Name = name; Center = center; Radius = radius; Depth = depth; Frozen = frozen; Seed = seed;
  }
  // Irregular shoreline radius for a direction.
  public float ShoreRadius(Vector2 p) {
   Vector2 d = p - Center;
   float a = Mathf.Atan2(d.y, d.x);
   float wobble = Mathf.PerlinNoise(Mathf.Cos(a) * 1.3f + Seed, Mathf.Sin(a) * 1.3f + Seed * 0.7f) - 0.5f;
   float wobble2 = Mathf.PerlinNoise(Mathf.Cos(a) * 3.1f + Seed * 2.1f, Mathf.Sin(a) * 3.1f + Seed) - 0.5f;
   return Radius * (1f + wobble * 0.55f + wobble2 * 0.18f);
  }
 }

 // The authored macro layout of the Lost Realms: biomes, landmarks, roads, rivers and lakes.
 // Main-quest anchors keep their original coordinates so saves and quests stay valid.
 public static class OpenWorldRegions {
  public const float WorldHalf = 512f;
  public const float PlayableHalf = 466f;
  public const float TerrainHeight = 320f; // vertical range of the heightmap; summits reach ~300 m
  // Lowest height the heightmap can hold. Lake and river beds in the low valleys go below 0 (a valley lake
  // whose surface sits ~1 m up needs its bed at -2 m to be swimmable); the terrain object sits this far
  // down and its heights are stored relative to it.
  public const float TerrainBase = -12f;

  // Verdant heartland (existing quest content)
  public static readonly Vector3 Hub = Vector3.zero;
  public static readonly Vector3 Grove = new Vector3(0f, 0f, 115f);
  public static readonly Vector3 Sanctum = new Vector3(-60f, 0f, -58f);
  public static readonly Vector3 Oakvale = new Vector3(44f, 0f, 42f);
  public static readonly Vector3 Millbrook = new Vector3(-96f, 0f, -104f);
  public static readonly Vector3 CaravanPost = new Vector3(84f, 0f, -8f);
  public static readonly Vector3 MirrorLake = new Vector3(-150f, 0f, 150f);
  // Frozen Peaks (north)
  public static readonly Vector3 Frostpine = new Vector3(-150f, 0f, 262f);
  public static readonly Vector3 FrozenLake = new Vector3(78f, 0f, 318f);
  public static readonly Vector3 FrostRift = new Vector3(-62f, 0f, 304f);
  public static readonly Vector3 DragonPeak = new Vector3(10f, 0f, 400f);
  public static readonly Vector3 WolfDen = new Vector3(150f, 0f, 248f);
  // Burning Dunes (east)
  public static readonly Vector3 Sunspire = new Vector3(318f, 0f, -48f);
  public static readonly Vector3 Oasis = new Vector3(292f, 0f, 150f);
  public static readonly Vector3 DesertRift = new Vector3(236f, 0f, 40f);
  public static readonly Vector3 DuneRuins = new Vector3(402f, 0f, 70f);
  // Hollow Graveyard (south-west)
  public static readonly Vector3 Graveyard = new Vector3(-212f, 0f, -222f);
  public static readonly Vector3 Marsh = new Vector3(-300f, 0f, -120f);
  // Stormveil Cliffs (south-east)
  public static readonly Vector3 StormOverlook = new Vector3(232f, 0f, -262f);
  public static readonly Vector3 StormRift = new Vector3(196f, 0f, -232f);
  public static readonly Vector3 StormRelay = new Vector3(244f, 0f, -282f);

  public static readonly List<OWRoad> Roads = new List<OWRoad>();
  public static readonly List<OWRiver> Rivers = new List<OWRiver>();
  public static readonly List<OWLake> Lakes = new List<OWLake>();
  static bool initialised;

  public static void Initialise() {
   if (initialised) return;
   initialised = true;
   // Existing Verdant trails (kept on their original lines so quest navigation still reads correctly).
   foreach (var route in OpenWorldTrails.Routes) {
    var pts = new Vector2[route.Length];
    for (int i = 0; i < route.Length; i++) pts[i] = new Vector2(route[i].x, route[i].z);
    Roads.Add(new OWRoad("Verdant trail", OWRoadKind.Dirt, 4.6f, pts));
   }
   Roads.Add(new OWRoad("Kings Road East", OWRoadKind.Cobble, 6.2f,
    V(14, -4), V(44, -6), V(84, -19), V(126, -24), V(170, -30), V(214, -38), V(258, -46), V(290, -48)));
   Roads.Add(new OWRoad("Oasis Track", OWRoadKind.Sand, 5f,
    V(318, -24), V(326, 30), V(312, 92), V(296, 128)));
   Roads.Add(new OWRoad("Ruins Track", OWRoadKind.Sand, 4.2f, V(326, 30), V(366, 52), V(390, 66)));
   Roads.Add(new OWRoad("Desert Rift Path", OWRoadKind.Sand, 4f, V(214, -38), V(226, -6), V(234, 28)));
   Roads.Add(new OWRoad("Northern Pass", OWRoadKind.Dirt, 5.2f,
    V(0, 98), V(8, 140), V(26, 182), V(36, 222), V(44, 262), V(60, 296)));
   Roads.Add(new OWRoad("Frostpine Road", OWRoadKind.Snow, 4.8f,
    V(36, 222), V(-10, 236), V(-60, 248), V(-110, 256), V(-140, 260)));
   Roads.Add(new OWRoad("Frost Rift Path", OWRoadKind.Snow, 4f, V(-60, 248), V(-62, 292)));
   Roads.Add(new OWRoad("Wolf Den Trail", OWRoadKind.Snow, 4f, V(44, 262), V(96, 254), V(140, 248)));
   Roads.Add(new OWRoad("Mirror Lake Path", OWRoadKind.Dirt, 4.2f,
    V(-24, 20), V(-70, 46), V(-104, 92), V(-124, 124)));
   Roads.Add(new OWRoad("Gravewalk", OWRoadKind.Dirt, 4.6f,
    V(-96, -116), V(-128, -150), V(-168, -186), V(-198, -210)));
   Roads.Add(new OWRoad("Marsh Causeway", OWRoadKind.Dirt, 3.8f, V(-168, -186), V(-236, -150), V(-288, -126)));
   Roads.Add(new OWRoad("Stormveil Ascent", OWRoadKind.Dirt, 4.6f,
    V(62, -62), V(98, -104), V(140, -150), V(176, -200), V(206, -240), V(226, -256)));
   Roads.Add(new OWRoad("Sunspire Approach", OWRoadKind.Sand, 5f, V(170, -28), V(176, -86), V(196, -150), V(176, -200)));
   Roads.Add(new OWRoad("Sunspire Avenue", OWRoadKind.Cobble, 6f, V(290, -48), V(318, -48), V(346, -48), V(374, -48)));
   Roads.Add(new OWRoad("Sunspire North Way", OWRoadKind.Cobble, 5.5f, V(318, -92), V(318, -64), V(318, -40), V(318, -24)));

   // Depths (physics pass): the big lakes and the Silverrun are deep enough to swim in mid-water (Aster
   // swims above ~1.3 m); ponds, the Duskwater and every road ford stay wadeable.
   Rivers.Add(new OWRiver("Silverrun", 6f, 13f, 1.9f,
    V(64, 356), V(44, 312), V(26, 262), V(-4, 214), V(-48, 186), V(-98, 168), V(-124, 156)));
   Rivers.Add(new OWRiver("Silverrun Lower", 11f, 8f, 1.8f,
    V(-172, 128), V(-186, 76), V(-196, 18), V(-212, -46), V(-238, -96), V(-284, -116)));
   Rivers.Add(new OWRiver("Duskwater", 5f, 8f, 0.9f,
    V(-300, -128), V(-276, -176), V(-246, -232), V(-262, -298), V(-300, -352)));

   Lakes.Add(new OWLake("Mirror Lake", V(-150, 150), 34f, 3.4f, false, 11.7f));
   Lakes.Add(new OWLake("Glacier Tarn", V(78, 318), 30f, 1.1f, true, 3.3f));
   Lakes.Add(new OWLake("Sunwell Oasis", V(292, 150), 20f, 2.6f, false, 7.9f));
   Lakes.Add(new OWLake("Hollow Mere", V(-306, -122), 26f, 2.2f, false, 5.1f));
   Lakes.Add(new OWLake("Sanctuary Pond", V(26, -18), 7.5f, 0.6f, false, 2.4f));
   Lakes.Add(new OWLake("Grove Pond", V(-22, 84), 7.5f, 0.6f, false, 9.2f));
  }

  static Vector2 V(float x, float z) => new Vector2(x, z);

  public static Vector2[] Smooth(Vector2[] controls, float step) {
   if (controls.Length < 2) return controls;
   var result = new List<Vector2>();
   for (int i = 0; i < controls.Length - 1; i++) {
    Vector2 p0 = controls[Mathf.Max(0, i - 1)], p1 = controls[i], p2 = controls[i + 1], p3 = controls[Mathf.Min(controls.Length - 1, i + 2)];
    int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
    for (int s = 0; s < n; s++) {
     float t = s / (float)n;
     result.Add(CatmullRom(p0, p1, p2, p3, t));
    }
   }
   result.Add(controls[controls.Length - 1]);
   return result.ToArray();
  }

  static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
   float t2 = t * t, t3 = t2 * t;
   return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
  }

  public static float Noise(float x, float z) => Mathf.PerlinNoise(x, z);

  public static float Fbm(float x, float z, int octaves, float lacunarity = 2.03f, float gain = 0.5f) {
   float sum = 0, amp = 0.5f, norm = 0;
   for (int i = 0; i < octaves; i++) {
    sum += Mathf.PerlinNoise(x + i * 17.31f, z - i * 9.77f) * amp;
    norm += amp; amp *= gain; x *= lacunarity; z *= lacunarity;
   }
   return sum / norm;
  }

  public static float Ridged(float x, float z, int octaves) {
   float sum = 0, amp = 0.55f, norm = 0, weight = 1f;
   for (int i = 0; i < octaves; i++) {
    float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x + i * 31.7f, z + i * 13.1f) * 2f - 1f);
    n *= n * weight;
    weight = Mathf.Clamp01(n * 1.4f);
    sum += n * amp; norm += amp; amp *= 0.5f; x *= 2.07f; z *= 2.07f;
   }
   return sum / norm;
  }

  static float S(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

  // Biome weights with organic, domain-warped borders. Index order matches OWBiome.
  // Frozen Peaks hold the north, the Burning Dunes the east, the Hollow Marches are a broad
  // south-western basin and the Stormveil Cliffs a shattered south-eastern plateau.
  public static readonly Vector2 MarchesHeart = new Vector2(-280f, -225f);
  public static readonly Vector2 StormHeart = new Vector2(238f, -252f);
  public static void BiomeWeights(float x, float z, float[] w) {
   float wx = x + (Mathf.PerlinNoise(x * 0.0042f + 7.7f, z * 0.0042f + 3.1f) - 0.5f) * 70f;
   float wz = z + (Mathf.PerlinNoise(x * 0.0042f + 19.3f, z * 0.0042f + 11.9f) - 0.5f) * 70f;
   float n1 = (Mathf.PerlinNoise(x * 0.011f + 3.1f, z * 0.011f + 9.4f) - 0.5f) * 36f;
   float n2 = (Mathf.PerlinNoise(x * 0.019f + 41.2f, z * 0.019f + 2.7f) - 0.5f) * 18f;
   float frozen = S(180f, 236f, wz + n1 + n2 - Mathf.Max(0f, wx - 250f) * 0.3f);
   float storm = 1f - S(150f, 195f, Vector2.Distance(new Vector2(wx, wz), StormHeart) + n1 + n2);
   float dunes = S(150f, 214f, wx + n1 * 0.8f + n2);
   float grave = 1f - S(140f, 190f, Vector2.Distance(new Vector2(wx, wz), MarchesHeart) + n1 + n2);
   storm *= 1f - frozen;
   dunes *= (1f - frozen) * (1f - storm);
   grave *= (1f - frozen) * (1f - storm) * (1f - dunes);
   float verdant = Mathf.Max(0f, 1f - frozen - dunes - grave - storm);
   float total = Mathf.Max(0.0001f, verdant + frozen + dunes + grave + storm);
   w[0] = verdant / total; w[1] = frozen / total; w[2] = dunes / total; w[3] = grave / total; w[4] = storm / total;
  }

  // Rounded-square distance used by the perimeter ranges (no straight, artificial walls).
  public static float EdgeMetric(float x, float z) {
   float ax = Mathf.Abs(x) / 512f, az = Mathf.Abs(z) / 512f;
   float m = Mathf.Pow(Mathf.Pow(ax, 6f) + Mathf.Pow(az, 6f), 1f / 6f) * 512f;
   return m + (Mathf.PerlinNoise(x * 0.01f + 50f, z * 0.01f + 60f) - 0.5f) * 46f + (Mathf.PerlinNoise(x * 0.031f + 5f, z * 0.031f + 9f) - 0.5f) * 14f;
  }

  public static OWBiome DominantBiome(float x, float z) {
   var w = scratch;
   BiomeWeights(x, z, w);
   int best = 0;
   for (int i = 1; i < 5; i++) if (w[i] > w[best]) best = i;
   return (OWBiome)best;
  }
  static readonly float[] scratch = new float[5];

  public static string BiomeTitle(OWBiome biome) {
   switch (biome) {
    case OWBiome.Frozen: return "FROZEN PEAKS";
    case OWBiome.Dunes: return "BURNING DUNES";
    case OWBiome.Graveyard: return "HOLLOW MARCHES";
    case OWBiome.Storm: return "STORMVEIL CLIFFS";
    default: return "VERDANT HEARTLANDS";
   }
  }

  // Nearest point on a polyline (x,z) with its parameter along the line (0..1).
  public static float DistanceToPath(Vector2[] path, Vector2 p, out float t, out int segment) {
   float best = float.MaxValue; t = 0; segment = 0;
   float total = 0;
   for (int i = 0; i < path.Length - 1; i++) total += Vector2.Distance(path[i], path[i + 1]);
   float run = 0;
   for (int i = 0; i < path.Length - 1; i++) {
    Vector2 a = path[i], b = path[i + 1], d = b - a;
    float len = d.magnitude;
    float u = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / (len * len)) : 0;
    float dist = (p - (a + d * u)).sqrMagnitude;
    if (dist < best) { best = dist; segment = i; t = total > 0 ? (run + u * len) / total : 0; }
    run += len;
   }
   return Mathf.Sqrt(best);
  }
 }
}
