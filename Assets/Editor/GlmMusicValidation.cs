using System;
using UnityEngine;

namespace LostRealmsOW {
 // Part B validation (brief Docs/TASKS_GLM_2026-10-30.md): called from the bridge at each tour stop.
 // RegionTrack asserts the expected theme is live and logs track/volume/cutoff; Night asserts the
 // low-pass engaged; Combat asserts the boss swap, and CombatReturn the ride back to the region track
 // after the fight. Results append to Docs/glm-music-validation.txt. Throws on failure so the bridge
 // records the reason.
 public static class GlmMusicValidation {
  static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
  static void Log(string line) {
   System.IO.File.AppendAllText("Docs/glm-music-validation.txt", DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
   Debug.Log("[GlmMusic] " + line);
  }

  public static void RegionTrack(string expected) {
   Check(OpenWorldMusic.Instance, "Music system missing (Instance null)");
   var music = OpenWorldMusic.Instance;
   string track = OpenWorldMusic.CurrentTrack;
   Check(track == expected, $"Expected {expected}, playing {track}");
   var atm = OpenWorldAtmosphere.Instance;
   Check(atm, "Atmosphere missing");
   Log($"region OK: {track}, volume {OpenWorldMusic.CurrentVolume:0.000}, cutoff {OpenWorldMusic.CurrentCutoff:0} Hz, night {atm.NightFactor:0.00}");
  }

  public static void Night() {
   Check(OpenWorldMusic.Instance, "Music system missing (Instance null)");
   float cutoff = OpenWorldMusic.CurrentCutoff;
   Check(cutoff > 100f && cutoff < 5000f, $"Night filter not blended: cutoff {cutoff:0} Hz");
   Log($"night OK: cutoff {cutoff:0} Hz, volume {OpenWorldMusic.CurrentVolume:0.000}");
  }

  public static void Combat(string bossTrack) {
   string track = OpenWorldMusic.CurrentTrack;
   Check(track == bossTrack, $"Combat swap failed: playing {track}, expected {bossTrack}");
   Log($"combat OK: swapped to {bossTrack}, volume {OpenWorldMusic.CurrentVolume:0.000}");
  }

  public static void CombatReturn(string regionTrack) {
   string track = OpenWorldMusic.CurrentTrack;
   Check(track == regionTrack, $"Combat return failed: playing {track}, expected {regionTrack}");
   Log($"return OK: back to {regionTrack}, volume {OpenWorldMusic.CurrentVolume:0.000}");
  }

  static OpenWorldEnemy fightEnemy;
  // Spawns an elite beside the hero (Role Elite drives the boss-layer branch of OpenWorldMusic).
  public static void SpawnFight() {
   var gm = OpenWorldManager.I; Check(gm && gm.Hero, "World required");
   if (fightEnemy) UnityEngine.Object.Destroy(fightEnemy.gameObject);
   fightEnemy = OpenWorldEnemy.Spawn("Elite", "Music Validation Wraith", gm.HuntGround(gm.Hero.transform.position + gm.Hero.transform.forward * 6f), gm.WorldRoot);
   fightEnemy.MaxHealth = fightEnemy.Health = 30f;
   fightEnemy.AttackDamage = 0.5f;
   fightEnemy.MoveSpeed = 3.5f;
   fightEnemy.AggroRadius = 40f; fightEnemy.WarnRadius = 40f; fightEnemy.LeashRadius = 60f; fightEnemy.AttackRange = 2.4f;
   gm.Enemies.Add(fightEnemy); // the music layer only scans Manager.Enemies
   Log("fight spawned: elite wraith 6 m ahead (boss-layer branch)");
  }
  public static void KillFight() {
   Check(fightEnemy, "Fight enemy missing");
   fightEnemy.TakeHit(1000f, OpenWorldManager.I.Hero.transform.position, true);
   Log("fight killed — waiting out the 7 s combat return");
  }
 }
}
