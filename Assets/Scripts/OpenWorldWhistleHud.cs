using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Separate partial to avoid editing the HUD and prey labels owned by Antigravity.
 public sealed partial class OpenWorldMobileHud {
  Button whistleControl;
  float nextHorseScan;
  bool horseKnown;
  internal void UpdateWhistleControl() {
   if (!safeRoot) return;
   if (!whistleControl) {
    whistleControl = PillButton(safeRoot, "WHISTLE", new Vector2(105, 300), new Vector2(130, 48), Vector2.zero, 17);
    whistleControl.gameObject.name = "Horse whistle";
    whistleControl.onClick.AddListener(() => {
     var gm = OpenWorldManager.I;
     if (gm && gm.Hero && !gm.IsModalOpen && !gm.Hero.Dead && !gm.Hero.IsMounted) OpenWorldAnimalInteraction.CallHorse(gm.Hero);
    });
   }
   if (Time.unscaledTime >= nextHorseScan) {
    nextHorseScan = Time.unscaledTime + .5f;
    var last = OpenWorldAnimalInteraction.LastRidden;
    horseKnown = last && last.isActiveAndEnabled && !last.Dead;
    var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
    if (!horseKnown && hero) foreach (var a in Object.FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsSortMode.None)) {
     if (a.Horse && !a.Dead && !a.Rider && (a.transform.position - hero.transform.position).sqrMagnitude < 8100f) { horseKnown = true; break; }
    }
   }
   var manager = OpenWorldManager.I;
   whistleControl.gameObject.SetActive(horseKnown && manager && manager.Hero && !manager.Hero.Dead && !manager.Hero.IsMounted && !manager.IsModalOpen && (!titleRoot || !titleRoot.activeSelf));
  }
 }
 public sealed class OpenWorldWhistleHudDriver : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { new GameObject("Horse whistle HUD adapter").AddComponent<OpenWorldWhistleHudDriver>(); }
  void LateUpdate() { if (OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateWhistleControl(); }
 }
}
