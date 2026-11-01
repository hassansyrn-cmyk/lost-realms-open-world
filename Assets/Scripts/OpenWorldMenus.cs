using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 public sealed partial class OpenWorldMobileHud {
  GameObject menuRoot;
  RectTransform menuViewport;
  RectTransform menuContent;
  RectTransform regionMapBox;
  RectTransform heroPin, targetPin;
  Texture2D mapTexture;
  Sprite mapSprite;
  Text menuTitle, menuSubtitle, menuFooter, menuGoldText, menuGemText, menuMeatText, devStatusLabel;
  InputField devPinInput;
  Button devToggleBtn;
  Text devToggleLabel;
  GameObject devBarObj;
  readonly Image[] menuTabBgs = new Image[5];
  readonly Text[] menuTabLabels = new Text[5];
  ScrollRect menuScroll;
  int displayedMenu = -1;
  string menuFeedback = "";
  float newJourneyConfirmUntil;
  readonly List<Action> refreshRows = new List<Action>();
  public bool MenuVisible => menuRoot && menuRoot.activeSelf;
  public int MenuRowCount { get; private set; }
  public bool RegionMapVisible => regionMapBox && regionMapBox.gameObject.activeSelf;

  public static void DebugUI(string arg) {
   var gm = OpenWorldManager.I ?? FindAnyObjectByType<OpenWorldManager>();
   var hud = Instance ?? FindAnyObjectByType<OpenWorldMobileHud>();
   if (!gm || !hud) { Debug.LogError("[DebugUI] gm or hud missing"); return; }
   string cmd = (arg ?? "").Trim().ToLowerInvariant();
   hud.SetTitleScreenVisible(false);
   gm.CloseMenus();
   switch (cmd) {
    case "hud":
     OpenWorldHero.DebugAlwaysFocused = true;
     gm.ShowBanner("VERDANT SANCTUARY DISCOVERED  •  REALM GATE ATTUNED");
     if (gm.Hero) SpawnCombatPopup(gm.Hero.transform.position + Vector3.up * 2.1f + Vector3.right * 0.6f, "CRIT 148", new Color(1f, 0.88f, 0.32f), 1.25f);
     break;
    case "atlas": gm.ShowMapModal = true; break;
    case "vault": gm.ShowVaultModal = true; break;
    case "arsenal": gm.ShowArsenalModal = true; break;
    case "wardrobe": gm.ShowWardrobeModal = true; break;
    case "pause": gm.ShowPauseModal = true; break;
    case "title": hud.SetTitleScreenVisible(true); break;
   }
   hud.Update();
  }

  void BuildMenus() {
   var root = Rect("Realm Menus", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   menuRoot = root.gameObject;
   root.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.025f, 0.055f, 0.88f);

   var frame = Rect("Legends Menu Frame", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1140, 656));
   theme.Round(frame.gameObject, new Color(0.035f, 0.065f, 0.12f, 0.96f), 20f);
   var frameOutline = Rect("FrameOutline", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   frameOutline.offsetMin = frameOutline.offsetMax = Vector2.zero;
   theme.Round(frameOutline.gameObject, OpenWorldUITheme.GoldSoft, 20f, true).raycastTarget = false;

   // Top Header Bar inside modal
   var headerBar = Rect("HeaderBar", frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(0, 88));
   theme.Shape(headerBar.gameObject, "fadeDown", new Color(0.06f, 0.11f, 0.2f, 0.65f)).raycastTarget = false;
   var headerLine = Rect("HeaderLine", frame, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(1060, 1.5f));
   theme.Shape(headerLine.gameObject, "fadeH", OpenWorldUITheme.GoldSoft).raycastTarget = false;

   // Title & Subtitle on left of Header (anchor (0,1), center X = leftMargin + width*0.5)
   menuTitle = Label(frame, "REALM ATLAS", new Vector2(216, -32), new Vector2(360, 34), new Vector2(0, 1), 24);
   menuTitle.font = theme.Heading ? theme.Heading : font;
   menuTitle.alignment = TextAnchor.MiddleLeft;
   menuTitle.color = gold;

   menuSubtitle = Label(frame, "WORLD NAVIGATION & DISCOVERIES", new Vector2(216, -60), new Vector2(360, 18), new Vector2(0, 1), 11);
   menuSubtitle.alignment = TextAnchor.MiddleLeft;
   menuSubtitle.color = OpenWorldUITheme.TextDim;

   // Center Navigation Tab Strip (switch menus directly in-place)
   string[] tabNames = { "ATLAS", "VAULT", "ARSENAL", "WARDROBE", "SYSTEM" };
   for (int i = 0; i < tabNames.Length; i++) {
    int tabKind = i + 1;
    var tabRect = Rect("Tab_" + tabNames[i], frame, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-132 + i * 94, -44), new Vector2(86, 36));
    menuTabBgs[i] = theme.Round(tabRect.gameObject, new Color(0.07f, 0.12f, 0.2f, 0.85f), 18f);
    var tabBtn = tabRect.gameObject.AddComponent<Button>();
    tabBtn.navigation = new Navigation { mode = Navigation.Mode.None };
    menuTabLabels[i] = Label(tabRect, tabNames[i], Vector2.zero, new Vector2(80, 28), Vector2.one * 0.5f, 12);
    menuTabLabels[i].color = OpenWorldUITheme.TextMain;
    tabBtn.onClick.AddListener(() => {
     var gm = OpenWorldManager.I;
     if (!gm) return;
     gm.CloseMenus();
     if (tabKind == 1) gm.ShowMapModal = true;
     else if (tabKind == 2) gm.ShowVaultModal = true;
     else if (tabKind == 3) gm.ShowArsenalModal = true;
     else if (tabKind == 4) gm.ShowWardrobeModal = true;
     else if (tabKind == 5) gm.ShowPauseModal = true;
     OpenWorldAudio.Play("sfx_menu", 0.45f);
    });
   }

   // Right Header: Gold, Gems & Meat Pill + Circular Close Button
   var currBox = Rect("MenuCurrency", frame, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-173, -44), new Vector2(210, 34));
   theme.Round(currBox.gameObject, new Color(0.02f, 0.04f, 0.08f, 0.88f), 17f).raycastTarget = false;
   var cIcon = Rect("Coin", currBox, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(17, 17));
   theme.Image(cIcon.gameObject, "Icons/UI_Icon_Coin").preserveAspect = true;
   cIcon.GetComponent<Image>().raycastTarget = false;
   menuGoldText = Label(currBox, "120", new Vector2(-48, 0), new Vector2(54, 22), new Vector2(0.5f, 0.5f), 13);
   menuGoldText.alignment = TextAnchor.MiddleLeft;
   menuGoldText.color = gold;

   var gIcon = Rect("Gem", currBox, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(92, 0), new Vector2(17, 17));
   theme.Image(gIcon.gameObject, "Icons/UI_Icon_Gem").preserveAspect = true;
   gIcon.GetComponent<Image>().raycastTarget = false;
   menuGemText = Label(currBox, "10", new Vector2(24, 0), new Vector2(44, 22), new Vector2(0.5f, 0.5f), 13);
   menuGemText.alignment = TextAnchor.MiddleLeft;
   menuGemText.color = new Color(0.58f, 0.94f, 1f);

   var mIcon = Rect("Meat", currBox, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(158, 0), new Vector2(16, 16));
   theme.Shape(mIcon.gameObject, "g_meat", new Color(1f, 0.68f, 0.45f)).raycastTarget = false;
   menuMeatText = Label(currBox, "0", new Vector2(84, 0), new Vector2(38, 22), new Vector2(0.5f, 0.5f), 13);
   menuMeatText.alignment = TextAnchor.MiddleLeft;
   menuMeatText.color = new Color(1f, 0.76f, 0.55f);

   var close = Rect("CloseButton", frame, Vector2.one, Vector2.one, new Vector2(-42, -44), new Vector2(44, 44));
   theme.Shape(close.gameObject, "circle", new Color(0.12f, 0.16f, 0.24f, 0.95f));
   var closeRim = Rect("Rim", close, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(44, 44));
   theme.Shape(closeRim.gameObject, "ring", gold).raycastTarget = false;
   var closeGlyph = Rect("X", close, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(18, 18));
   theme.Shape(closeGlyph.gameObject, "g_close", gold).raycastTarget = false;
   var closeBtn = close.gameObject.AddComponent<Button>();
   closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
   closeBtn.onClick.AddListener(() => { OpenWorldManager.I?.CloseMenus(); OpenWorldAudio.Play("sfx_menu", 0.45f); });

   BuildRegionMapPanel(frame);
   BuildDeveloperTestingBar(frame);

   var viewport = Rect("Scrollable menu", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   viewport.offsetMin = new Vector2(36, 76);
   viewport.offsetMax = new Vector2(-36, -100);
   viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.02f);
   viewport.gameObject.AddComponent<RectMask2D>();
   menuViewport = viewport;
   menuContent = Rect("Rows", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
   menuContent.pivot = new Vector2(.5f, 1);
   menuScroll = viewport.gameObject.AddComponent<ScrollRect>();
   menuScroll.viewport = viewport; menuScroll.content = menuContent;
   menuScroll.horizontal = false;
   menuScroll.movementType = ScrollRect.MovementType.Clamped;
   menuScroll.scrollSensitivity = 38;

   var footerLine = Rect("FooterLine", frame, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(1060, 1f));
   theme.Shape(footerLine.gameObject, "fadeH", OpenWorldUITheme.GoldLine).raycastTarget = false;
   menuFooter = Label(frame, "", new Vector2(0, 32), new Vector2(1020, 44), new Vector2(.5f, 0), 16);
   menuFooter.font = theme.Body ? theme.Body : font;
   menuFooter.color = OpenWorldUITheme.TextDim;
   menuRoot.SetActive(false);
  }

  void BuildDeveloperTestingBar(RectTransform frame) {
   var bar = Rect("DeveloperTestingBar", frame, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(1040, 44));
   theme.Round(bar.gameObject, new Color(0.05f, 0.09f, 0.16f, 0.94f), 12f);
   var rim = Rect("Outline", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   rim.offsetMin = rim.offsetMax = Vector2.zero;
   theme.Round(rim.gameObject, OpenWorldUITheme.GoldLine, 12f, true).raycastTarget = false;
   devBarObj = bar.gameObject;

   // Left status message clearly separated from buttons
   devStatusLabel = Label(bar, "EQUIPMENT TESTING MODE  •  Enter 4-digit PIN to test all weapons & skins", new Vector2(328, 0), new Vector2(616, 32), new Vector2(0, 0.5f), 13);
   devStatusLabel.alignment = TextAnchor.MiddleLeft;
   devStatusLabel.color = OpenWorldUITheme.TextDim;

   // CREDITS button placed to the left of the PIN box with clear gap
   var creditsBtn = PillButton(bar, "CREDITS", new Vector2(-332, 0), new Vector2(108, 34), new Vector2(1, 0.5f), 13);
   creditsBtn.onClick.AddListener(() => {
    var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
    if (!gm) return;
    gm.CloseMenus();
    gm.ShowCreditsModal = true;
    OpenWorldAudio.Play("sfx_menu", 0.45f);
   });

   // 4-digit PIN InputField clearly framed with gold outline and number pad for mobile
   var pinBox = Rect("Developer password", bar, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-199, 0), new Vector2(130, 34));
   theme.Round(pinBox.gameObject, new Color(0.02f, 0.04f, 0.08f, 0.96f), 8f);
   var pinRim = Rect("Outline", pinBox, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   pinRim.offsetMin = pinRim.offsetMax = Vector2.zero;
   theme.Round(pinRim.gameObject, OpenWorldUITheme.GoldLine, 8f, true).raycastTarget = false;

   var pinText = Label(pinBox, "", Vector2.zero, new Vector2(110, 26), new Vector2(.5f, .5f), 15);
   pinText.color = OpenWorldUITheme.Gold;
   pinText.alignment = TextAnchor.MiddleCenter;
   devPinInput = pinBox.gameObject.AddComponent<InputField>();
   devPinInput.textComponent = pinText;
   devPinInput.contentType = InputField.ContentType.Pin;
   devPinInput.characterLimit = 4;
   devPinInput.keyboardType = TouchScreenKeyboardType.NumberPad;
   var ph = Label(pinBox, "PIN CODE", Vector2.zero, new Vector2(110, 26), new Vector2(.5f, .5f), 12);
   ph.color = OpenWorldUITheme.TextMuted;
   ph.alignment = TextAnchor.MiddleCenter;
   devPinInput.placeholder = ph;

   // UNLOCK / DISABLE toggle button
   devToggleBtn = PillButton(bar, "UNLOCK", new Vector2(-68, 0), new Vector2(108, 34), new Vector2(1, 0.5f), 13);
   devToggleLabel = devToggleBtn.GetComponentInChildren<Text>();
   devToggleBtn.onClick.AddListener(() => {
    var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
    if (!gm) return;
    bool accepted = gm.SetDeveloperMode(devPinInput ? devPinInput.text : "");
    menuFeedback = accepted
     ? (gm.DeveloperMode ? "Developer mode enabled • All equipment available" : "Developer mode disabled • Original equipment restored")
     : "Incorrect password";
    if (devPinInput) devPinInput.text = "";
    OpenWorldAudio.Play("sfx_menu", 0.45f);
   });
   devBarObj.SetActive(false);
  }

  void BuildRegionMapPanel(RectTransform frame) {
   regionMapBox = Rect("Region Map Panel", frame, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(216, -12), new Vector2(364, 468));
   theme.Round(regionMapBox.gameObject, new Color(0.025f, 0.045f, 0.085f, 0.95f), 16f);
   var outline = Rect("Outline", regionMapBox, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   outline.offsetMin = outline.offsetMax = Vector2.zero;
   theme.Round(outline.gameObject, OpenWorldUITheme.GoldSoft, 16f, true).raycastTarget = false;

   var mapHeader = Label(regionMapBox, OpenWorldRealm.Instance ? "THE LOST REALMS  •  1 KM WORLD" : "VERDANT KINGDOM MAP", new Vector2(0, -22), new Vector2(336, 24), new Vector2(.5f, 1), 14);
   mapHeader.color = gold;

   var canvasFrame = Rect("MapBorder", regionMapBox, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(332, 332));
   theme.Round(canvasFrame.gameObject, OpenWorldUITheme.GoldLine, 10f, true).raycastTarget = false;

   var canvasRect = Rect("Map Canvas", regionMapBox, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(324, 324));
   var mapImg = canvasRect.gameObject.AddComponent<Image>();
   mapImg.sprite = GetOrCreateMapSprite();
   mapImg.raycastTarget = false;

   var legend = Label(regionMapBox, "▲ Gold: Aster   •   ◆ Cyan: Objective   •   ● Green/Red: Camps\n◆ Violet: Sanctum & Crypt   •   ◆ Amber: Biome Rifts", new Vector2(0, 26), new Vector2(340, 38), new Vector2(.5f, 0), 12);
   legend.font = theme.Body ? theme.Body : font;
   legend.color = OpenWorldUITheme.TextDim;
   regionMapBox.gameObject.SetActive(false);
  }

  Sprite GetOrCreateMapSprite() {
   if (mapSprite) return mapSprite;
   var atlas = OpenWorldRealm.Instance ? Resources.Load<Texture2D>("World/Realm/RealmAtlas") : null;
   if (atlas) {
    mapSprite = Sprite.Create(atlas, new Rect(0, 0, atlas.width, atlas.height), Vector2.one * 0.5f);
    return mapSprite;
   }
   const int sz = 128;
   mapTexture = new Texture2D(sz, sz, TextureFormat.RGB24, false) { name = "OW_RegionMapTex", wrapMode = TextureWrapMode.Clamp };
   var cols = new Color[sz * sz];
   for (int y = 0; y < sz; y++) {
    for (int x = 0; x < sz; x++) {
     float wx = ((x / (float)(sz - 1)) - 0.5f) * 240f;
     float wz = ((y / (float)(sz - 1)) - 0.5f) * 240f;
     float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
     Color c = Color.Lerp(new Color(0.09f, 0.22f, 0.14f), new Color(0.18f, 0.36f, 0.22f), n);
     if (OpenWorldTrails.NearTrail(new Vector3(wx, 0f, wz), 4.2f)) {
      c = new Color(0.46f, 0.36f, 0.22f);
     }
     if ((x % 32 == 0) || (y % 32 == 0)) c = Color.Lerp(c, new Color(0.3f, 0.5f, 0.4f), 0.18f);
     cols[y * sz + x] = c;
    }
   }
   mapTexture.SetPixels(cols);
   mapTexture.Apply(false, true);
   mapSprite = Sprite.Create(mapTexture, new Rect(0, 0, sz, sz), Vector2.one * 0.5f);
   return mapSprite;
  }

  static Vector2 LocalBlip(Vector3 offset, bool clampToRim) {
   var v = new Vector2(offset.x, offset.z) / MinimapRadius * 58f;
   if (clampToRim && v.magnitude > 56f) v = v.normalized * 56f;
   return v;
  }

  static Vector2 WorldToMapCanvas(Vector3 worldPos) {
   if (OpenWorldRealm.Instance) {
    float rx = Mathf.Clamp(worldPos.x / OpenWorldRegions.WorldHalf, -0.96f, 0.96f);
    float rz = Mathf.Clamp(worldPos.z / OpenWorldRegions.WorldHalf, -0.96f, 0.96f);
    return new Vector2(rx * 154f, rz * 154f);
   }
   float nx = Mathf.Clamp(worldPos.x / 125f, -0.92f, 0.92f);
   float nz = Mathf.Clamp(worldPos.z / 125f, -0.92f, 0.92f);
   return new Vector2(nx * 148f, nz * 148f);
  }

  void RebuildRegionMapPins(OpenWorldManager gm) {
   if (!regionMapBox) return;
   var canvasRect = (RectTransform)regionMapBox.Find("Map Canvas");
   if (!canvasRect) return;
   for (int i = canvasRect.childCount - 1; i >= 0; i--) {
    var ch = canvasRect.GetChild(i);
    ch.gameObject.SetActive(false);
    Destroy(ch.gameObject);
   }

   foreach (var camp in gm.Camps) {
    if (!camp) continue;
    Color cc = camp.Captured ? new Color(0.28f, 0.95f, 0.48f) : new Color(0.95f, 0.3f, 0.28f);
    SpawnMapPin(canvasRect, camp.CampTitle.Split(' ')[0].ToUpperInvariant(), camp.transform.position, cc, 10f, "circle");
   }

   foreach (var updraft in gm.Updrafts) {
    if (!updraft) continue;
    SpawnMapPin(canvasRect, "", updraft.transform.position, new Color(0.35f, 1f, 0.78f), 7f, "diamond");
   }

   Color sc = gm.SanctumCompleted ? new Color(0.45f, 0.95f, 1f) : new Color(0.76f, 0.48f, 1f);
   SpawnMapPin(canvasRect, "SANCTUM", OpenWorldManager.SanctumAltarPosition, sc, 11f, "diamond");
   Color cryptCol = gm.CryptCompleted ? new Color(0.45f, 1f, 0.78f) : new Color(0.84f, 0.58f, 1f);
   SpawnMapPin(canvasRect, "CRYPT", OpenWorldCryptDungeon.EntrancePos, cryptCol, 11f, "diamond");

   Color gc = gm.GuardianDefeated ? new Color(0.35f, 1f, 0.6f) : new Color(1f, 0.65f, 0.25f);
   SpawnMapPin(canvasRect, "GROVE", new Vector3(0f, 0f, 115f), gc, 12f, "diamond");

   SpawnMapPin(canvasRect, "HUB", Vector3.zero, new Color(1f, 0.92f, 0.65f), 11f, "circle", -11f);
   SpawnMapPin(canvasRect, "OAKVALE", new Vector3(44f, 0f, 42f), new Color(0.95f, 0.88f, 0.55f), 8f, "circle");
   bool realm = OpenWorldRealm.Instance;
   SpawnMapPin(canvasRect, "FROSTPINE", realm ? OpenWorldRegions.Frostpine : new Vector3(-82f, 0f, 14f), new Color(0.68f, 0.92f, 1f), 8f, "circle");
   if (realm) {
    SpawnMapPin(canvasRect, "SUNSPIRE", OpenWorldRegions.Sunspire, new Color(1f, 0.82f, 0.5f), 9f, "circle");
    SpawnMapPin(canvasRect, "OASIS", OpenWorldRegions.Oasis, new Color(0.55f, 0.9f, 1f), 8f, "circle");
    SpawnMapPin(canvasRect, "MILLBROOK", OpenWorldRegions.Millbrook, new Color(0.95f, 0.88f, 0.55f), 8f, "circle");
    SpawnMapPin(canvasRect, "GRAVEYARD", OpenWorldRegions.Graveyard, new Color(0.7f, 0.9f, 0.75f), 8f, "circle");
    SpawnMapPin(canvasRect, "DRAGON PEAK", OpenWorldRegions.DragonPeak, new Color(0.9f, 0.95f, 1f), 8f, "diamond");
    SpawnMapPin(canvasRect, "RUINS", OpenWorldRegions.DuneRuins, new Color(1f, 0.75f, 0.45f), 8f, "diamond");
    SpawnMapPin(canvasRect, "RELAY", OpenWorldStormRelay.ConductorPos, new Color(0.58f, 0.78f, 1f), 8f, "diamond");
   }

   Vector3[] riftSpots = { OpenWorldFrontierBuilder.RiftCenter(0), OpenWorldFrontierBuilder.RiftCenter(1), OpenWorldFrontierBuilder.RiftCenter(2) };
   string[] riftTags = { "SUNSCAR", "FROSTPEAK", "STORMVEIL" };
   for (int i = 0; i < riftSpots.Length; i++) {
    bool slain = (gm.BountiesMask & (1 << i)) != 0;
    Color rc = slain ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.52f, 0.18f);
    SpawnMapPin(canvasRect, riftTags[i], riftSpots[i], rc, 9f, "diamond");
   }

   if (!gm.QuestCompleted) {
    targetPin = SpawnMapPin(canvasRect, "GOAL", gm.QuestTarget, new Color(0.3f, 0.96f, 1f), 13f, "diamond", 12f);
   } else {
    targetPin = null;
   }
   heroPin = SpawnMapPin(canvasRect, "YOU", gm.Hero ? gm.Hero.transform.position : Vector3.zero, new Color(1f, 0.88f, 0.25f), 14f, "g_arrow", 12f);
  }

  RectTransform SpawnMapPin(RectTransform mapCanvas, string tagText, Vector3 worldPos, Color color, float size, string shapeKind = "circle", float labelOffsetY = 10f) {
   var pin = Rect("Pin_" + tagText, mapCanvas, new Vector2(.5f, .5f), new Vector2(.5f, .5f), WorldToMapCanvas(worldPos), new Vector2(size, size));
   theme.Shape(pin.gameObject, shapeKind, color).raycastTarget = false;
   if (!string.IsNullOrEmpty(tagText)) {
    var lbl = Label(pin, tagText, new Vector2(0, labelOffsetY), new Vector2(84, 14), new Vector2(.5f, .5f), 10);
    lbl.color = color;
    AddShadow(lbl.gameObject, new Color(0, 0, 0, 0.92f), new Vector2(1, -1));
   }
   return pin;
  }

  int CurrentMenu(OpenWorldManager gm) => gm.ShowPauseModal ? 5 : gm.ShowMapModal ? 1 : gm.ShowVaultModal ? 2 :
   gm.ShowArsenalModal ? 3 : gm.ShowWardrobeModal ? 4 : 0;

  void UpdateMenus() {
   var gm = OpenWorldManager.I ?? FindAnyObjectByType<OpenWorldManager>();
   if (!gm) return;
   int kind = CurrentMenu(gm);
   menuRoot.SetActive(kind != 0);
   if (kind == 0) { displayedMenu = 0; return; }
   if (menuGoldText) menuGoldText.text = gm.Gold.ToString();
   if (menuGemText) menuGemText.text = gm.Gems.ToString();
   if (menuMeatText) menuMeatText.text = gm.RawMeat.ToString();
   if (kind != displayedMenu) {
    displayedMenu = kind;
    menuFeedback = "";
    RebuildMenu(gm, kind);
   }
   if (kind == 1 && gm.Hero) {
    if (heroPin) {
     heroPin.anchoredPosition = WorldToMapCanvas(gm.Hero.transform.position);
     heroPin.localEulerAngles = new Vector3(0, 0, -gm.Hero.transform.eulerAngles.y);
    }
    if (targetPin) targetPin.anchoredPosition = WorldToMapCanvas(gm.QuestTarget);
   }
   if (kind == 5 && devBarObj) {
    if (devToggleLabel) devToggleLabel.text = gm.DeveloperMode ? "DISABLE" : "UNLOCK";
    if (devStatusLabel) {
     devStatusLabel.text = !string.IsNullOrEmpty(menuFeedback)
      ? menuFeedback
      : gm.DeveloperMode
       ? "DEVELOPER MODE ACTIVE  •  All catalog weapons & skins unlocked for this session"
       : "EQUIPMENT TESTING MODE  •  Enter 4-digit PIN to test all weapons & skins";
     devStatusLabel.color = gm.DeveloperMode ? gold : OpenWorldUITheme.TextDim;
    }
   }
   foreach (var refresh in refreshRows) refresh();
   menuFooter.text = kind == 2 ? $"{gm.Gold} Gold  •  {gm.Gems} Aether Gems   |   {(string.IsNullOrEmpty(gm.LastChestResult) ? "Open chests to discover weapons and skins" : gm.LastChestResult)}" :
    kind == 5 ? "" :
    kind == 1 ? $"LV.{gm.AdventurerRank} {gm.AdventurerTitle} (+{(gm.AdventurerRank - 1) * 6}% ATK)  •  Crypt {(gm.CryptCompleted ? "Awakened" : "Sealed")}  •  Relics {gm.RelicsCount}/4  •  Bounties {gm.BountiesCount}/3" :
    gm.DeveloperMode ? "DEVELOPER MODE • All equipment available • Test selections are not saved" :
    "Swipe or scroll to browse  •  Equipment and unlocks are saved automatically";
  }

  void RebuildMenu(OpenWorldManager gm, int kind) {
   foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
   refreshRows.Clear(); MenuRowCount = 0;
   bool showMap = kind == 1;
   bool showDevBar = kind == 5;
   if (regionMapBox) regionMapBox.gameObject.SetActive(showMap);
   if (devBarObj) devBarObj.SetActive(showDevBar);
   if (menuViewport) {
    menuViewport.offsetMin = new Vector2(showMap ? 412 : 36, showDevBar ? 88 : 76);
    menuViewport.offsetMax = new Vector2(-36, -100);
   }
   if (showMap) RebuildRegionMapPins(gm);

   for (int i = 0; i < 5; i++) {
    bool active = (i + 1) == kind;
    if (menuTabBgs[i]) menuTabBgs[i].color = active ? new Color(0.18f, 0.26f, 0.4f, 0.98f) : new Color(0.06f, 0.1f, 0.17f, 0.78f);
    if (menuTabLabels[i]) menuTabLabels[i].color = active ? gold : OpenWorldUITheme.TextDim;
   }

   string[] titles = { "", "REALM ATLAS", "TREASURE VAULT", "ASTER'S ARSENAL", "WARDROBE", "JOURNEY PAUSED" };
   string[] subtitles = {
    "",
    "FAST-TRAVEL WAYPOINTS & LIVE GEOGRAPHIC OVERVIEW",
    "EXCHANGE GOLD & AETHER GEMS FOR WEAPONS AND SKINS",
    $"BLADES, AXES & POLEARMS  •  {gm.UnlockedWeapons.Count}/{OpenWorldWeaponCatalog.All.Length} UNLOCKED",
    $"HERO ATTIRE & APPEARANCES  •  {gm.UnlockedSkins.Count}/{OpenWorldCharacterVisual.AsterSkins.Length} UNLOCKED",
    "ADVENTURE CONTROLS, GRAPHICS, ATMOSPHERE & SAVE"
   };
   menuTitle.text = titles[kind];
   if (menuSubtitle) menuSubtitle.text = subtitles[kind];

   if (kind == 1) {
    foreach (var gate in gm.Gates) {
     var destination = gate;
     AddMenuRow(destination.WaypointTitle,
      destination.Discovered ? "Discovered Realm Gate  •  Instant fast-travel ready" : "Uncharted waypoint  •  Explore the region to attune this gate",
      "Icons/UI_Icon_Atlas", () => gm.FastTravelTo(destination),
      () => destination.Discovered && !gm.Hero.Dead, () => destination.Discovered ? "TRAVEL" : "LOCKED", true);
    }
   } else if (kind == 2) {
    string[] names = { "Wanderer's Supply Chest", "Golden Reliquary Chest", "Epic Sovereign Vault" };
    string[] descriptions = {
     "Cost: 50 Gold  •  Common blades, bonus gold and Aether gems",
     "Cost: 140 Gold  •  Higher odds for rare forged weapons and gems",
     "Cost: 8 Aether Gems  •  Guaranteed high-tier roll for epic weapons & skins"
    };
    string[] icons = { "Icons/UI_Chest_Normal", "Icons/UI_Chest_Golden", "Icons/UI_Chest_Epic" };
    for (int i = 0; i < 3; i++) {
     int tier = i;
     AddMenuRow(names[i], descriptions[i], icons[i], () => gm.OpenGachaChest(tier),
      () => tier == 2 ? gm.Gems >= 8 : gm.Gold >= (tier == 0 ? 50 : 140), () => "OPEN");
    }
   } else if (kind == 3) {
    foreach (var weapon in OpenWorldWeaponCatalog.All) {
     if(!OpenWorldWeaponCatalog.Available(weapon.Id))continue;
     var item = weapon;
     string styleTag = item.Style == "chop" ? "HEAVY CLEAVE" : item.Style == "spear" ? "POLEARM" : "SWIFT BLADE";
     string elemTag = item.ElementAffinity == 0 ? " • EMBER" : item.ElementAffinity == 1 ? " • FROST" : item.ElementAffinity == 2 ? " • GALE" : "";
     string detail = $"[{styleTag}{elemTag}]  {item.Summary}";
     AddMenuRow(item.Name, detail, "Icons/UI_Icon_Arsenal", () => gm.Hero.EquipWeapon(item.Id),
      () => (gm.DeveloperMode || gm.UnlockedWeapons.Contains(item.Id)) && gm.Hero.EquippedWeaponId != item.Id,
      () => gm.Hero.EquippedWeaponId == item.Id ? "EQUIPPED" : (gm.DeveloperMode || gm.UnlockedWeapons.Contains(item.Id)) ? "EQUIP" : "LOCKED");
    }
   } else if (kind == 4) {
    for (int i = 0; i < OpenWorldCharacterVisual.AsterSkins.Length; i++) {
     int skin = i;
     AddMenuRow(OpenWorldCharacterVisual.AsterSkinTitles[i],
      i == 0 ? "Aster's signature Warden attire from the Verdant Kingdom" : "Rare realm appearance discovered in Epic Sovereign Vaults",
      "Icons/UI_Icon_Wardrobe", () => gm.Hero.EquipSkin(skin),
      () => (gm.DeveloperMode || gm.UnlockedSkins.Contains(skin)) && gm.Hero.EquippedSkinId != skin,
      () => gm.Hero.EquippedSkinId == skin ? "EQUIPPED" : (gm.DeveloperMode || gm.UnlockedSkins.Contains(skin)) ? "EQUIP" : "LOCKED");
    }
   } else {
    AddMenuRow("Continue your journey", gm.QuestHeading, "Icons/UI_Icon_Play_Resume", gm.CloseMenus, () => true, () => "RESUME");
    AddMenuRow("Start New Journey", "Start unarmed with no weapons. Previous adventure backed up. Tap twice to confirm.", "Icons/UI_Icon_Play_Resume",
     () => {
      if (Time.unscaledTime < newJourneyConfirmUntil) {
       newJourneyConfirmUntil = 0;
       if (!gm.StartNewJourney()) menuFeedback = "Could not start • Previous adventure preserved";
      } else { newJourneyConfirmUntil = Time.unscaledTime + 5f; menuFeedback = "Tap New Journey again within 5 seconds to restart"; }
     }, () => true, () => Time.unscaledTime < newJourneyConfirmUntil ? "CONFIRM" : "NEW");
    AddMenuRow("Save adventure", "Save progress at your last sanctuary or Realm Gate", "Icons/UI_Icon_Sanctuary",
     () => menuFeedback = gm.SaveAdventure() ? "Adventure saved" : "Save unavailable • Existing save files are preserved",
     () => true, () => "SAVE");
    AddMenuRow("Graphics quality", "Balanced (30 fps mobile) • High (60 fps) • Ultra", "Icons/UI_Icon_Star",
     () => { var g = OpenWorldGraphics.Instance; if (g) g.Cycle(); },
     () => OpenWorldGraphics.Instance, () => OpenWorldGraphics.Instance ? OpenWorldGraphics.Instance.Label : "HIGH");
    AddMenuRow("Time of day", "Open the realm clock: pick any hour, fast-forward the sky, lock the day cycle", "Icons/UI_Icon_Star",
     () => { gm.CloseMenus(); gm.ShowClockModal = true; },
     () => OpenWorldAtmosphere.Instance, () => OpenWorldAtmosphere.Instance ? OpenWorldAtmosphere.ClockText(OpenWorldAtmosphere.Instance.Hour) : "CLOCK");
    AddMenuRow("Weather", "Cycle between Clear Skies, Spring Rain, Thunderstorm and Mystic Mist", "Icons/UI_Icon_Star",
     () => { var atm = OpenWorldAtmosphere.Instance; if (atm) atm.CycleWeather(); },
     () => OpenWorldAtmosphere.Instance, () => OpenWorldAtmosphere.Instance ? OpenWorldAtmosphere.Instance.WeatherLabel : "CLEAR");
    AddMenuRow("Sound volume", "Adjust realm music, ambience and combat sound effects", "Icons/UI_Icon_Star",
     () => { var a = OpenWorldAudio.Instance; if (a) a.CycleVolume(); },
     () => OpenWorldAudio.Instance, () => OpenWorldAudio.Instance ? OpenWorldAudio.Instance.VolumeLabel : "100%");
    AddMenuRow("Camera sensitivity", "Adjust touch swipe and mouse orbit look speed", "Icons/UI_Icon_Atlas",
     () => { if (gm.CameraRig) gm.CameraRig.CycleSensitivity(); },
     () => gm.CameraRig, () => gm.CameraRig ? gm.CameraRig.SensitivityLabel : "NORMAL");
   }
   menuContent.sizeDelta = new Vector2(0, MenuRowCount * 112 + 12);
   menuScroll.StopMovement();
   menuContent.anchoredPosition = Vector2.zero;
   menuScroll.verticalNormalizedPosition = 1;
  }

  void AddMenuRow(string title, string description, string icon, Action action, Func<bool> enabled, Func<string> caption, bool compact = false) {
   int row = MenuRowCount++;
   var box = Rect("Row " + title, menuContent, new Vector2(0, 1), Vector2.one, new Vector2(0, -row * 112 - 52), new Vector2(0, 100));
   var cardBg = theme.Round(box.gameObject, OpenWorldUITheme.Card, 14f);
   var cardRim = Rect("Outline", box, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   cardRim.offsetMin = cardRim.offsetMax = Vector2.zero;
   var rimImg = theme.Round(cardRim.gameObject, OpenWorldUITheme.GoldLine, 14f, true);
   rimImg.raycastTarget = false;

   // Left status accent bar
   var accent = Rect("StatusBar", box, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(4, 68));
   var accentImg = theme.Round(accent.gameObject, gold, 2f);
   accentImg.raycastTarget = false;

   // Circular Icon Badge
   var badge = Rect("IconBadge", box, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(compact ? 46 : 56, 0), new Vector2(62, 62));
   theme.Shape(badge.gameObject, "circle", new Color(0.03f, 0.055f, 0.1f, 0.95f)).raycastTarget = false;
   var badgeRim = Rect("Rim", badge, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(62, 62));
   theme.Shape(badgeRim.gameObject, "ring", OpenWorldUITheme.GoldSoft).raycastTarget = false;
   var art = Rect("Icon", badge, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(44, 44));
   theme.Image(art.gameObject, icon).preserveAspect = true;
   art.GetComponent<Image>().raycastTarget = false;

   float textWidth = compact ? 340f : 640f;
   float textCenterX = compact ? 262f : 424f;
   var heading = Label(box, title, new Vector2(textCenterX, 18), new Vector2(textWidth, 30), new Vector2(0, .5f), compact ? 18 : 20);
   heading.alignment = TextAnchor.MiddleLeft; heading.color = gold;
   var body = Label(box, description, new Vector2(textCenterX, -16), new Vector2(textWidth, 38), new Vector2(0, .5f), compact ? 15 : 17);
   body.font = theme.Body ? theme.Body : font;
   body.alignment = TextAnchor.MiddleLeft;
   body.color = OpenWorldUITheme.TextDim;

   var button = PillButton(box, caption(), new Vector2(compact ? -102 : -124, 0), new Vector2(compact ? 168 : 204, 54), new Vector2(1, .5f), 16);
   var btnBg = button.GetComponent<Image>();
   var label = button.GetComponentInChildren<Text>();
   button.onClick.AddListener(() => { if (enabled()) { action(); OpenWorldAudio.Play("sfx_menu", .45f); } });

   refreshRows.Add(() => {
    bool canClick = enabled();
    string cap = caption();
    bool isEquipped = cap == "EQUIPPED";
    bool isLocked = cap == "LOCKED";
    button.interactable = canClick;
    label.text = cap;
    if (isEquipped) {
     label.color = new Color(0.45f, 1f, 0.68f);
     accentImg.color = new Color(0.38f, 0.96f, 0.62f);
     cardBg.color = new Color(0.08f, 0.16f, 0.22f, 0.94f);
     if (btnBg) btnBg.color = new Color(0.08f, 0.22f, 0.18f, 0.95f);
    } else if (isLocked) {
     label.color = OpenWorldUITheme.TextMuted;
     accentImg.color = new Color(0.35f, 0.4f, 0.48f, 0.5f);
     cardBg.color = new Color(0.045f, 0.075f, 0.13f, 0.78f);
     if (btnBg) btnBg.color = new Color(0.05f, 0.08f, 0.13f, 0.7f);
    } else {
     label.color = canClick ? gold : OpenWorldUITheme.TextMuted;
     accentImg.color = canClick ? gold : OpenWorldUITheme.GoldLine;
     cardBg.color = OpenWorldUITheme.Card;
     if (btnBg) btnBg.color = new Color(0.09f, 0.15f, 0.25f, 0.95f);
    }
   });
  }
 }
}
