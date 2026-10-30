using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 public sealed partial class OpenWorldMobileHud {
  internal void UpdateVerdantHuntPanel(OpenWorldManager gm) {
   if (OpenWorldQuestJournal.Instance) return; // The journal owns explicit main/side tracking.
   if (!objectiveTitle || gm.VerdantHuntStage < 1 || gm.VerdantHuntStage >= 5 || gm.IsModalOpen || (titleRoot && titleRoot.activeSelf)) return;
   objectiveTitle.text = "A HUNTER'S PROMISE";
   objective.text = gm.VerdantHuntObjective;
   objectiveDist.text = gm.SaveUnavailable ? "Save unavailable • check storage" : Mathf.RoundToInt(Vector3.Distance(gm.Hero.transform.position, gm.VerdantHuntTarget)) + " m";
  }
 }
}

