using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Editor-only previews of the procedural realm, so landform changes can be reviewed without play mode.
 public static class WorldPreviewTools {
  static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots"));

  [MenuItem("Lost Realms OW/Diagnostics/Preview Realm Atlas")]
  public static void PreviewAtlas() {
   var sw = Stopwatch.StartNew();
   OpenWorldTerrainSculptor.ResetPads();
   OpenWorldWorldPlan.RegisterCorePads();
   OpenWorldPlanBridge.RegisterSettlementPads();
   var td = OpenWorldTerrainSculptor.Build();
   long sculpt = sw.ElapsedMilliseconds;
   var tex = OpenWorldMapRenderer.Build(1024);
   Directory.CreateDirectory(Shots);
   File.WriteAllBytes(Path.Combine(Shots, "realm_atlas.png"), tex.EncodeToPNG());
   Object.DestroyImmediate(tex);
   var sb = new StringBuilder();
   sb.AppendLine("sculpt_ms=" + sculpt + " total_ms=" + sw.ElapsedMilliseconds);
   float min = float.MaxValue, max = float.MinValue;
   var h = OpenWorldTerrainSculptor.Final;
   for (int z = 0; z < h.GetLength(0); z++) for (int x = 0; x < h.GetLength(1); x++) { min = Mathf.Min(min, h[z, x]); max = Mathf.Max(max, h[z, x]); }
   sb.AppendLine("height_min=" + min.ToString("F1") + " height_max=" + max.ToString("F1"));
   void Probe(string name, Vector3 p) {
    float y = OpenWorldTerrainSculptor.HeightAt(p.x, p.z);
    bool wet = OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out float s, out float d);
    sb.AppendLine(name + " (" + p.x + "," + p.z + ") h=" + y.ToString("F2") + " road=" + OpenWorldTerrainSculptor.RoadDistance(p.x, p.z).ToString("F1") + (wet ? " WATER s=" + s.ToString("F2") + " d=" + d.ToString("F2") : "") + " biome=" + OpenWorldRegions.DominantBiome(p.x, p.z));
   }
   Probe("hub", OpenWorldRegions.Hub); Probe("grove", OpenWorldRegions.Grove); Probe("sanctum", OpenWorldRegions.Sanctum);
   Probe("woods", new Vector3(-65, 0, 65)); Probe("ruins", new Vector3(68, 0, 58)); Probe("ridge", new Vector3(62, 0, -62));
   Probe("crypt_under", new Vector3(88, 0, -42)); Probe("skyisland_under", new Vector3(-84, 0, 92));
   Probe("frostpine", OpenWorldRegions.Frostpine); Probe("tarn", OpenWorldRegions.FrozenLake); Probe("dragon", OpenWorldRegions.DragonPeak);
   Probe("sunspire", OpenWorldRegions.Sunspire); Probe("oasis", OpenWorldRegions.Oasis); Probe("ruins_desert", OpenWorldRegions.DuneRuins);
   Probe("graveyard", OpenWorldRegions.Graveyard); Probe("marsh", OpenWorldRegions.Marsh); Probe("storm", OpenWorldRegions.StormOverlook);
   Probe("mirror", OpenWorldRegions.MirrorLake); Probe("millbrook", OpenWorldRegions.Millbrook);
   foreach (var lake in OpenWorldRegions.Lakes) sb.AppendLine("lake " + lake.Name + " surface=" + lake.SurfaceY.ToString("F2"));
   File.WriteAllText(Path.Combine(Shots, "realm_atlas.txt"), sb.ToString());
   Object.DestroyImmediate(td);
   UnityEngine.Debug.Log("[Preview] realm atlas written\n" + sb);
  }
 }
}
