using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Credits screen (GLM Phase 5, required before release because of CC-BY assets). A modal in the quest
 // board's style, opened from the pause menu's bottom bar; every third-party attribution lives in the
 // single Entries table below, so a new asset only needs a new line. Sources: Docs/AssetAudit
 // (HORSE_CANDIDATES_2026-10-29.md, DOWNLOADED_ACTORS_2026-10-29.md).
 // The attribution body is a ScrollRect (drag on touch, wheel on desktop) whose content height follows
 // the text's preferred size, and the frame adapts to the safe area so it stays readable on narrow
 // screens. ValidateCredits drives the bridge check.
 public sealed class OpenWorldCreditsRuntime : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldCreditsRuntime>()) new GameObject("Credits screen").AddComponent<OpenWorldCreditsRuntime>(); }
  public static bool HudReady;

  void Update() {
   var gm = OpenWorldManager.I;
   HudReady = gm && OpenWorldMobileHud.Instance;
   if (gm && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.TickCredits(gm);
  }
  // Bridge helpers: open the credits modal, then verify it.
  public static void DebugShow(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   gm.CloseMenus();
   gm.ShowCreditsModal = true;
  }
  public static void Validate(string arg) {
   var hud = OpenWorldMobileHud.Instance;
   if (!hud) throw new System.Exception("HUD missing for credits validation");
   hud.ValidateCredits();
  }
 }
 public sealed partial class OpenWorldMobileHud {
  GameObject creditsRoot;
  float creditsFrameWidth, creditsFrameHeight;
  ScrollRect creditsScroll;
  RectTransform creditsViewport, creditsContent;
  Text creditsBody;
  // The one attribution table. Format: (asset, credit line). CC-BY requires name + author + source
  // link + licence; the dragon entry also carries the required modification notice. One line per
  // imported model that needs attribution (store packs need none).
  static readonly (string asset, string credit, string[] requiredTokens)[] Entries = {
   ("Horse (realistic horses, saddled and wild)",
    "\"Horse\" by Henry S — https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9 — licensed under CC BY 4.0 (http://creativecommons.org/licenses/by/4.0/). Prepared via headless Blender (LODs, gait loops, normal/AO bakes).",
    new[] { "Henry S", "CC BY 4.0" }),
   ("Mountain Dragon (aerial landmark above Dragon Peak)",
    "\"Mountain Dragon\" by Alexey Zaika / lexazaixa25 — https://sketchfab.com/3d-models/mountain-dragon-0a0665396f634b0e9d0872720087f52c — licensed under CC BY 4.0 (http://creativecommons.org/licenses/by/4.0/). Changes: converted GLB to FBX, extracted textures and wingbeat, URP materials, game scale and flight placement.",
    new[] { "Alexey Zaika", "CC BY 4.0", "Changes:" }),
   ("Komodo Dragon Lizard (oasis wildlife, huntable)",
    "\"Komodo Dragon Lizard\" by Nyilonelycompany — https://sketchfab.com/3d-models/komodo-dragon-lizard-f4c8b2accf50495db3d64ca583f2a148 — Sketchfab Standard licence (https://sketchfab.com/licenses). Changes: GLB to FBX, walk loop and texture extraction, URP material, game scale.",
    new[] { "Nyilonelycompany", "Sketchfab Standard" }),
   ("Medieval Civilian (Caravan Provisioner, Caravan Post)",
    "\"Medieval Civilian 3\" by Leonardo Carvalho (sketchfab.com/livrosparacriancas) — https://sketchfab.com/3d-models/medieval-civilian-3-d99ca22f06134e8dab28fdfa75d87f31 — Sketchfab Standard licence, free download. Commercial use allowed; this credit is a courtesy, and the model file is not redistributed on its own. Changes: humanoid avatar setup, URP materials, retargeted motions.",
    new[] { "Leonardo Carvalho", "medieval-civilian-3", "Sketchfab Standard" }),
  };
  internal void TickCredits(OpenWorldManager gm) {
   if (creditsRoot && creditsRoot.activeSelf != gm.ShowCreditsModal) {
    creditsRoot.SetActive(gm.ShowCreditsModal);
   } else if (!creditsRoot && gm.ShowCreditsModal) {
    BuildCredits();
    creditsRoot.SetActive(true);
    Debug.Log("[GlmCredits] modal built (frame " + safeRoot.rect.width.ToString("0") + "x" + safeRoot.rect.height.ToString("0") + ")");
   }
  }
  void BuildCredits() {
   var root = Rect("Credits", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   creditsRoot = root.gameObject;
   root.gameObject.AddComponent<Image>().color = new Color(0f, 0.015f, 0.04f, 0.56f);
   // Adaptive frame: never wider/taller than the safe area allows, so narrow phones keep margins.
   float width = creditsFrameWidth = Mathf.Min(860f, safeRoot.rect.width - 40f);
   float height = creditsFrameHeight = Mathf.Min(720f, safeRoot.rect.height - 40f);
   var frame = Rect("Credits Frame", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
   theme.Round(frame.gameObject, new Color(0.04f, 0.06f, 0.11f, 0.95f), 22f, true);
   var title = Label(frame, "CREDITS", new Vector2(0, -48), new Vector2(700, 54), new Vector2(.5f, 1f), 30);
   title.font = theme.Heading ? theme.Heading : title.font;
   title.color = OpenWorldUITheme.Gold;
   var close = Rect("Credits Close", frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34, -34), new Vector2(46, 46));
   theme.Round(close.gameObject, new Color(0.1f, 0.14f, 0.22f, 0.95f), 23f, true);
   var closeLabel = Label(close, "X", Vector2.zero, new Vector2(46, 46), new Vector2(.5f, .5f), 20);
   closeLabel.color = OpenWorldUITheme.TextMain;
   var closeButton = close.gameObject.AddComponent<Button>();
   closeButton.transition = Selectable.Transition.ColorTint;
   closeButton.onClick.AddListener(() => { if (OpenWorldManager.I) OpenWorldManager.I.ShowCreditsModal = false; });
   var scrollFrame = Rect("Credits Scroll Frame", frame, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-56, -128));
   var panel = theme.Round(scrollFrame.gameObject, new Color(0.07f, 0.1f, 0.17f, 0.85f), 14f);
   panel.raycastTarget = true; // the panel catches drag and wheel for the ScrollRect
   // Viewport: masked, padded inside the panel; catches pointer events for touch scrolling.
   creditsViewport = Rect("Credits Viewport", scrollFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   creditsViewport.offsetMin = new Vector2(18f, 16f);
   creditsViewport.offsetMax = new Vector2(-18f, -16f);
   creditsViewport.gameObject.AddComponent<RectMask2D>();
   var dragCatcher = creditsViewport.gameObject.AddComponent<Image>();
   dragCatcher.color = new Color(0f, 0f, 0f, 0.01f);
   // Content: the body text itself; its preferred height drives the scroll extent. Width follows the
   // viewport, height comes from the ContentSizeFitter — no best-fit shrinking.
   var bodyGo = new GameObject("Credits Body", typeof(RectTransform));
   bodyGo.transform.SetParent(creditsViewport, false);
   creditsContent = (RectTransform)bodyGo.transform;
   creditsContent.anchorMin = new Vector2(0f, 1f);
   creditsContent.anchorMax = new Vector2(1f, 1f);
   creditsContent.pivot = new Vector2(0.5f, 1f);
   creditsContent.anchoredPosition = new Vector2(0f, 0f);
   creditsContent.sizeDelta = new Vector2(0f, 0f);
   creditsBody = bodyGo.AddComponent<Text>();
   creditsBody.font = theme.Body ? theme.Body : font; // Crimson body text; Cinzel is for headings
   creditsBody.fontSize = 17;
   creditsBody.color = OpenWorldUITheme.TextMain;
   creditsBody.alignment = TextAnchor.UpperLeft;
   creditsBody.horizontalOverflow = HorizontalWrapMode.Wrap;
   creditsBody.verticalOverflow = VerticalWrapMode.Overflow;
   creditsBody.resizeTextForBestFit = false;
   creditsBody.raycastTarget = false;
   creditsBody.text = CreditsText();
   var fitter = bodyGo.AddComponent<ContentSizeFitter>();
   fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
   fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
   var scroll = creditsScroll = scrollFrame.gameObject.AddComponent<ScrollRect>();
   scroll.content = creditsContent;
   scroll.viewport = creditsViewport;
   scroll.horizontal = false;
   scroll.vertical = true;
   scroll.movementType = ScrollRect.MovementType.Clamped;
   scroll.scrollSensitivity = 28f;
   var footer = Label(frame, "Store and asset-pack art remains under its original licences. Realm themes and sound beds are original to this project. Drag or scroll the list.", new Vector2(0, 44), new Vector2(780, 34), new Vector2(.5f, 0f), 14);
   footer.color = OpenWorldUITheme.TextMuted;
  }
  static string CreditsText() {
   var sb = new System.Text.StringBuilder();
   foreach (var e in Entries) {
    sb.Append("<b>").Append(e.asset).Append("</b>\n").Append(e.credit).Append("\n\n");
   }
   return sb.ToString();
  }
  // Bridge check (call after DebugShow): asserts the attribution data, the body geometry and that the
  // list actually scrolls. Throws on failure so the bridge records the reason.
  public void ValidateCredits() {
   Canvas.ForceUpdateCanvases();
   if (!creditsRoot || !creditsRoot.activeSelf) throw new System.Exception("Credits modal not open");
   if (!creditsBody) throw new System.Exception("Credits body missing");
   string text = creditsBody.text;
   if (text.Length < 400) throw new System.Exception("Credits text too short: " + text.Length);
   var missing = new System.Text.StringBuilder();
   foreach (var e in Entries)
    foreach (var token in e.requiredTokens)
     if (!text.Contains(token)) missing.Append(token).Append("; ");
   if (missing.Length > 0) throw new System.Exception("Attribution tokens missing: " + missing);
   if (creditsViewport == null || creditsContent == null || !creditsScroll || creditsScroll.content != creditsContent)
    throw new System.Exception("Scroll parts missing or unwired");
   float viewH = creditsViewport.rect.height, contentH = creditsContent.rect.height;
   if (viewH <= 40f) throw new System.Exception("Viewport too small: " + viewH.ToString("0") + " px");
   string scrollResult;
   if (contentH > viewH + 10f) {
    // The list overflows here, so prove the ScrollRect actually moves it.
    creditsScroll.verticalNormalizedPosition = 0f;
    Canvas.ForceUpdateCanvases();
    if (Mathf.Abs(creditsContent.anchoredPosition.y) < 10f) throw new System.Exception("Scroll did not move the content");
    creditsScroll.verticalNormalizedPosition = 1f;
    scrollResult = $"scroll proven (content {contentH:0} px in a {viewH:0} px viewport)";
   } else {
    // Content fits at this size; prove the scroll mechanism by shrinking the viewport briefly —
    // the same geometry a narrow screen produces.
    var min0 = creditsViewport.offsetMin;
    var max0 = creditsViewport.offsetMax;
    creditsViewport.offsetMin = new Vector2(min0.x, min0.y + 300f);
    creditsViewport.offsetMax = new Vector2(max0.x, max0.y - 100f);
    Canvas.ForceUpdateCanvases();
    float tightView = creditsViewport.rect.height;
    if (contentH <= tightView + 10f) throw new System.Exception($"Scroll drill inconclusive: content {contentH:0} px vs viewport {tightView:0} px");
    creditsScroll.verticalNormalizedPosition = 0f;
    Canvas.ForceUpdateCanvases();
    if (Mathf.Abs(creditsContent.anchoredPosition.y) < 10f) throw new System.Exception("Scroll did not move the content");
    creditsScroll.verticalNormalizedPosition = 1f;
    creditsViewport.offsetMin = min0;
    creditsViewport.offsetMax = max0;
    Canvas.ForceUpdateCanvases();
    scrollResult = $"content fits this resolution ({contentH:0} px in a {viewH:0} px viewport); scroll proven in a {tightView:0} px viewport drill";
   }
   if (creditsBody.rectTransform.rect.width <= 100f) throw new System.Exception("Body width wrong: " + creditsBody.rectTransform.rect.width);
   if (safeRoot.rect.width < creditsFrameWidth + 1f || safeRoot.rect.height < creditsFrameHeight + 1f) throw new System.Exception("Frame overflows the safe area");
   FileAppend("PASS: modal open, " + Entries.Length + " imported-model credits present (" + string.Join(", ", System.Linq.Enumerable.Select(Entries, e => e.requiredTokens[0])) + "), all licence tokens found, frame " + creditsFrameWidth.ToString("0") + "x" + creditsFrameHeight.ToString("0") + " inside a " + safeRoot.rect.width.ToString("0") + "x" + safeRoot.rect.height.ToString("0") + " safe area, body " + creditsBody.rectTransform.rect.width.ToString("0") + " px wide — " + scrollResult + ".");
   Debug.Log($"[GlmCredits] validate PASS: {Entries.Length} entries, frame {creditsFrameWidth:0}x{creditsFrameHeight:0} in safe area {safeRoot.rect.width:0}x{safeRoot.rect.height:0}, {scrollResult}, font {creditsBody.font?.name}");
  }
  static void FileAppend(string line) {
   System.IO.File.AppendAllText("Docs/glm-credits-validation.txt", System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
  }
 }
}
