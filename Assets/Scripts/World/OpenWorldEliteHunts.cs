using UnityEngine;

namespace LostRealmsOW {
 // Region elite hunts (GLM, shortlist item 3): four named elites — one per non-Verdant region —
 // registered in the population table (OpenWorldPopulation) so they roam their biome like any enemy.
 // Their telegraphed signature moves come free from the existing combat branches: the Matriarch
 // (role "Elite", no beast flag) casts the purple 3.2 m ARCANE ERUPTION, the beast elites lunge with
 // the model-matched variants (HORN RUSH / VENOM POUNCE / FANG LEAP). This file adds the hunt layer:
 // a WANTED poster at the hub quest board with live SLAIN/AT LARGE status, one-time rewards per kill
 // (gold + gems + a herb/ore bundle that feeds the gathering loop), and a watcher that despawns
 // elite instances whose flag is already set so they stop returning after a reload.
 public static class GlmEliteHunts {
  public struct Elite {
   public string Name, Region, Flag, RewardText;
   public int Gold, Gems, Herbs, Ore;
  }

  public static readonly Elite[] Elites = {
   new Elite { Name = "Rimeclaw Matriarch", Region = "Frozen Peaks", Flag = "glm-elite:rimeclaw", Gold = 100, Gems = 5, Herbs = 3, Ore = 0, RewardText = "100 gold • 5 gems • 3 wild herbs" },
   new Elite { Name = "Sunscar Ravager", Region = "Burning Dunes", Flag = "glm-elite:sunscar", Gold = 120, Gems = 6, Herbs = 0, Ore = 2, RewardText = "120 gold • 6 gems • 2 ore" },
   new Elite { Name = "Marsh Spider Queen", Region = "Hollow Marches", Flag = "glm-elite:marshqueen", Gold = 110, Gems = 5, Herbs = 3, Ore = 0, RewardText = "110 gold • 5 gems • 3 wild herbs" },
   new Elite { Name = "Stormmane Alpha", Region = "Stormveil", Flag = "glm-elite:stormmane", Gold = 100, Gems = 5, Herbs = 0, Ore = 2, RewardText = "100 gold • 5 gems • 2 ore" },
  };

  public static bool IsSlain(OpenWorldManager gm, string name) {
   foreach (var elite in Elites) if (string.Equals(name, elite.Name, System.StringComparison.OrdinalIgnoreCase)) return gm && gm.CollectedWorldIds.Contains(elite.Flag);
   return false;
  }
  static GameObject poster;
  static UnityEngine.UI.Text posterText;
  static string lastPosterState = "";
  static float nextScan, nextPoster;

  // Called every frame from GlmQuests.Tick.
  public static void Tick(OpenWorldManager gm) {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   BuildPoster(gm);
   if (Time.unscaledTime >= nextPoster) {
    nextPoster = Time.unscaledTime + 2f;
    RefreshPoster(gm);
   }
   if (Time.unscaledTime >= nextScan) {
    nextScan = Time.unscaledTime + 10f;
    DespawnSlain(gm);
   }
  }

  // Defeat hook — called from GlmQuests.Defeat (which the manager's OnEnemyDefeated already reaches)
  // with the defeated enemy's display name.
  public static void OnDefeat(OpenWorldManager gm, string displayName) {
   if (!gm || string.IsNullOrEmpty(displayName)) return;
   foreach (var elite in Elites) {
    if (!string.Equals(displayName, elite.Name, System.StringComparison.OrdinalIgnoreCase)) continue;
    if (!gm.CollectedWorldIds.Add(elite.Flag)) continue; // already claimed
    gm.Gold += elite.Gold;
    gm.Gems += elite.Gems;
    gm.Herbs = Mathf.Min(999, gm.Herbs + elite.Herbs);
    gm.Ore = Mathf.Min(999, gm.Ore + elite.Ore);
    gm.MarkProgressDirty();
    gm.ShowBanner("ELITE SLAIN: " + elite.Name.ToUpperInvariant() + " • " + elite.RewardText);
    OpenWorldQuestJournal.Instance?.Record("ELITE SLAIN — " + elite.Name, "The bounty on " + elite.Name + " (" + elite.Region + ") is paid: " + elite.RewardText + ".");
    OpenWorldAudio.Play("sfx_complete", .6f);
    Debug.Log("[Elites] reward paid: " + elite.Name + " (+" + elite.Gold + "g +" + elite.Gems + " gems)");
    nextPoster = 0f; // refresh the poster on the next Tick
   }
  }

  static void BuildPoster(OpenWorldManager gm) {
   if (poster) return;
   Vector3 anchor=new Vector3(-9.2f,0,29f);gm.ReserveSpot(anchor,1.8f);
   poster=new GameObject("Wanted poster");poster.transform.SetParent(gm.WorldRoot,false);poster.transform.position=gm.HuntGround(anchor);
   posterText=OpenWorldNoticeBoard.Build(poster.transform,"WANTED BY THE WARDENS","");posterText.fontSize=52;lastPosterState="";
  }

  static void RefreshPoster(OpenWorldManager gm) {
   if (!posterText) return;
   string state = "";
   foreach (var elite in Elites) state += (gm.CollectedWorldIds.Contains(elite.Flag) ? "1" : "0");
   if (state == lastPosterState) return;
   lastPosterState = state;
   string text = "";
   for (int i = 0; i < Elites.Length; i++) {
    bool slain = i < state.Length && state[i] == '1';
    text += (i == 0 ? "" : "\n") + Elites[i].Region + "\n" + Elites[i].Name + (slain ? " — SLAIN" : "");
   }
   posterText.text = text;
  }

  // Slain elites must not haunt the world after a reload: population rebuilds every session, so
  // destroy instances whose bounty flag is already claimed. Scanned on a 10 s cadence.
  static void DespawnSlain(OpenWorldManager gm) {
   var enemies = Object.FindObjectsByType<OpenWorldEnemy>();
   foreach (var enemy in enemies) {
    if (!enemy || string.IsNullOrEmpty(enemy.DisplayName)) continue;
    foreach (var elite in Elites) {
     if (!string.Equals(enemy.DisplayName, elite.Name, System.StringComparison.OrdinalIgnoreCase)) continue;
     if (gm.CollectedWorldIds.Contains(elite.Flag)) {
      Object.Destroy(enemy.gameObject);
      Debug.Log("[Elites] despawned slain elite: " + elite.Name);
     }
    }
   }
  }
 }

 public static class OpenWorldEliteDebug {
  // Bridge entry points (static only): count live elites, force-pay a bounty, read poster state.
  public static void Scan(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   var enemies = Object.FindObjectsByType<OpenWorldEnemy>();
   int found = 0;
   foreach (var enemy in enemies) {
    if (!enemy) continue;
    foreach (var elite in GlmEliteHunts.Elites)
     if (string.Equals(enemy.DisplayName, elite.Name, System.StringComparison.OrdinalIgnoreCase)) {
      found++;
      Debug.Log("[Elites] live: " + enemy.DisplayName + " at " + enemy.transform.position.ToString("0.0") + " hp " + enemy.Health.ToString("0.0"));
     }
   }
   Debug.Log("[Elites] scan: " + found + " elites alive");
  }
  public static void Pay(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Debug.LogError("[Elites] world missing"); return; }
   foreach (var elite in GlmEliteHunts.Elites) {
    if (!string.IsNullOrEmpty(arg) && !elite.Name.ToLowerInvariant().Contains(arg.ToLowerInvariant())) continue;
    Debug.Log("[Elites] force-pay " + elite.Name + " (flag was " + gm.CollectedWorldIds.Contains(elite.Flag) + ")");
    GlmEliteHunts.OnDefeat(gm, elite.Name);
    return;
   }
   Debug.LogError("[Elites] no elite matches: " + arg);
  }
 }
}
