using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 public static class LegendsUIValidation {
  static void Check(bool value, string message) { if (!value) throw new Exception("Legends UI: " + message); }
  public static void RunFromBridge() { if (OpenWorldManager.I) Verify(OpenWorldManager.I); }
  public static void Verify(OpenWorldManager gm) {
   var hud = OpenWorldMobileHud.Instance;
   Check(hud.GetComponentsInChildren<Text>(true).Any(t => t.font && t.font.name == "Cinzel-Bold"), "Original button font is not used.");
   Check(hud.GetComponentsInChildren<Image>(true).Any(i => i.sprite && i.sprite.name.Contains("UI_Action_Blade")), "Original attack art is missing.");
   gm.CloseMenus(); gm.ShowMapModal = true; hud.SendMessage("Update");
   Check(hud.MenuVisible && hud.RegionMapVisible && hud.MenuRowCount == gm.Gates.Count, "Atlas does not display region map and every gate.");
   gm.CloseMenus(); gm.ShowArsenalModal = true; hud.SendMessage("Update");
   Check(hud.MenuRowCount == OpenWorldWeaponCatalog.All.Length, "Arsenal omitted weapons.");
   var scroll = hud.GetComponentInChildren<ScrollRect>();
   Canvas.ForceUpdateCanvases();
   Check(scroll && scroll.content.rect.height > scroll.viewport.rect.height, "Long arsenal cannot scroll.");
   scroll.verticalNormalizedPosition = 0;
   Canvas.ForceUpdateCanvases();
   Vector3[] rowCorners = new Vector3[4], viewportCorners = new Vector3[4];
   ((RectTransform)scroll.content.GetChild(scroll.content.childCount - 1)).GetWorldCorners(rowCorners);
   scroll.viewport.GetWorldCorners(viewportCorners);
   Check(rowCorners[0].y >= viewportCorners[0].y - 2, "Last arsenal row is unreachable.");
   int equipped = gm.Hero.EquippedWeaponId;
   var locked = scroll.content.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentInChildren<Text>().text == "LOCKED");
   Check(locked && !locked.interactable, "Locked equipment is not disabled.");
   locked.onClick.Invoke();
   Check(gm.Hero.EquippedWeaponId == equipped, "Locked equipment can be equipped.");
   gm.CloseMenus(); gm.ShowWardrobeModal = true; hud.SendMessage("Update");
   Check(hud.MenuRowCount == OpenWorldCharacterVisual.AsterSkins.Length, "Wardrobe omitted skins.");
   gm.CloseMenus(); gm.ShowVaultModal = true; hud.SendMessage("Update");
   Check(hud.MenuRowCount == 3, "Vault tiers missing.");
   gm.CloseMenus(); gm.ShowPauseModal = true; gm.SendMessage("Update"); hud.SendMessage("Update");
   Check(Time.timeScale == 0 && hud.MenuRowCount == 7, "Pause menu does not stop gameplay or show settings.");
   gm.CloseMenus(); hud.SendMessage("Update");
   Check(!hud.MenuVisible && Mathf.Approximately(Time.timeScale, 1), "Closing pause does not resume gameplay.");
   System.IO.File.WriteAllText("Docs/legends-ui-validation.txt",
    "PASS: Original Cinzel font and combat artwork in runtime HUD; complete atlas/arsenal/wardrobe/vault row counts; final arsenal row reachable by scrolling; locked equipment rejects click; pause stops time and resume restores it. Unity 6000.6.0f1 play-mode validation. Physical Android touch/readability remain unverified.");
  }
 }
}
