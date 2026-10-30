using UnityEngine;

namespace LostRealmsOW {
 // Terrain pads for every gameplay landmark, registered before the heightmap is sculpted so
 // quest objects, settlements and arenas stand on level, natural-looking ground.
 public static class OpenWorldWorldPlan {
  static void S(Vector3 c, float radius, float falloff, float offset) => OpenWorldTerrainSculptor.AddRoundPad(c, radius, falloff, offset);

  public static void RegisterCorePads() {
   OpenWorldRegions.Initialise();
   // Verdant quest heartland (original coordinates)
   S(OpenWorldRegions.Hub, 27f, 20f, 0f);
   S(OpenWorldRegions.Grove, 26f, 18f, 0f);
   S(OpenWorldRegions.Sanctum, 19f, 14f, 0f);
   S(new Vector3(-65f, 0f, 65f), 15f, 12f, 0f);
   S(new Vector3(68f, 0f, 58f), 15f, 12f, 0f);
   S(new Vector3(62f, 0f, -62f), 15f, 12f, 0f);
   S(OpenWorldRegions.Oakvale, 18f, 12f, 0f);
   S(OpenWorldRegions.CaravanPost, 13f, 10f, 0f);
   S(new Vector3(30f, 0f, -24f), 8f, 7f, 0f);
   S(new Vector3(0f, 0f, 25f), 10f, 8f, 0f);
   // Frozen Peaks
   S(OpenWorldRegions.Frostpine, 36f, 24f, 0f);
   S(OpenWorldRegions.FrostRift, 12f, 12f, 0f);
   S(OpenWorldRegions.DragonPeak, 32f, 34f, 0f);
   S(OpenWorldRegions.WolfDen, 16f, 14f, 0f);
   // Burning Dunes
   OpenWorldTerrainSculptor.AddPad(new OWPad { Center = new Vector2(OpenWorldRegions.Sunspire.x, OpenWorldRegions.Sunspire.z), HalfSize = new Vector2(62f, 48f), Yaw = 0f, Falloff = 26f, Height = float.NaN, Offset = 0f });
   S(OpenWorldRegions.Oasis, 34f, 18f, 0f);
   S(OpenWorldRegions.DesertRift, 13f, 12f, 0f);
   S(OpenWorldRegions.DuneRuins, 26f, 18f, 0f);
   // Hollow Marches
   OpenWorldTerrainSculptor.AddPad(new OWPad { Center = new Vector2(OpenWorldRegions.Graveyard.x, OpenWorldRegions.Graveyard.z), HalfSize = new Vector2(36f, 30f), Yaw = 18f, Falloff = 16f, Height = float.NaN, Offset = 0.4f });
   OpenWorldTerrainSculptor.AddPad(new OWPad { Center = new Vector2(OpenWorldRegions.Millbrook.x, OpenWorldRegions.Millbrook.z), HalfSize = new Vector2(40f, 30f), Yaw = 0f, Falloff = 18f, Height = float.NaN, Offset = 0f });
   // Stormveil Cliffs
   S(OpenWorldRegions.StormOverlook, 24f, 16f, 0f);
   S(OpenWorldRegions.StormRift, 12f, 10f, 0f);
   S(OpenWorldRegions.StormRelay, 14f, 10f, 0f);
  }
 }
}
