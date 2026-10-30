using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Professional Genshin Impact / Wuthering Waves / Zelda (BotW/TotK) inspired HUD & World UI,
 // keeping the Legends of the Lost Realms navy/gold identity and Cinzel/Crimson typography.
 public sealed partial class OpenWorldMobileHud : MonoBehaviour {
  OpenWorldUITheme theme;
  public static OpenWorldMobileHud Instance { get; private set; }
  RectTransform safeRoot, worldUiRoot;
  GameObject controls, heroStatusPanel, topRightHudGroup;
  Text status, aetherStatus, levelBadge, goldLabel, gemLabel, meatLabel, dashCaptionLabel;
  Text objectiveTitle, objective, objectiveDist, notice, bossLabel, bossSubLabel, compassLabel, compassSubLabel, minimapFooter;
  Text qualityLabel;
  Button interact;
  Text interactLabel;
  Image health, healthTrail, aether, stamina, bossHealthFill, bossTrailFill;
  Image heroElemDot, burstReadyRing, burstGlowImg, spellCostRing, echoCdRing, parryReadyGlow, elementCycleGlyph, lockOnGlyph, attackChargeRing;
  Text spellCostLabel, echoCdLabel;
  GameObject objectivePanel, noticePanel, bossPanel, minimapPanel, compassPanel, clockButtonObject, titleRoot;
  CanvasGroup noticeGroup, bossGroup, interactGroup;
  RectTransform minimapHeroDot, minimapGoalDot, minimapCone, lockOnReticle;
  RawImage minimapRaw;
  const float MinimapRadius = 150f;
  readonly List<RectTransform> minimapEnemyDots = new List<RectTransform>();
  Font font;
  Rect lastSafe;
  int lastWidth, lastHeight;
  readonly Color panel = new Color(0.035f, 0.065f, 0.11f, 0.86f);
  readonly Color gold = new Color(1f, 0.84f, 0.52f);
  static readonly string[] Bearings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

  // Smooth bar trails & cooldown tracking
  float hpTrailVal = 1f, bossTrailVal = 1f;
  float echoCdUntil;
  bool wasEchoPressed;

  // Loot / Pickup feed state
  struct FeedItem {
   public RectTransform root;
   public CanvasGroup group;
   public Image icon;
   public Text label;
   public float hideAt;
  }
  readonly List<FeedItem> feedPool = new List<FeedItem>();
  int lastGold = -1, lastGems = -1, lastKeys = -1, lastRelics = -1, lastBounties = -1, lastMeat = -1;
  bool wasMealBuffed;
  string lastChestSeen = "";

  static readonly System.Reflection.FieldInfo DeerHealthField =
   typeof(OpenWorldAnimalInteraction).GetField("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

  // Screen-space World Landmark / NPC / Wildlife Markers (replaces raw 3D TextMesh labels)
  struct WorldMarkerUI {
   public RectTransform root;
   public CanvasGroup group;
   public Image icon;
   public Text titleLabel;
   public Text subLabel;
  }
  readonly List<WorldMarkerUI> worldMarkers = new List<WorldMarkerUI>();
  TextMesh[] cachedWorldLabels = Array.Empty<TextMesh>();
  OpenWorldAnimalInteraction[] cachedAnimals = Array.Empty<OpenWorldAnimalInteraction>();
  float nextLabelScanAt;

  // Screen-space Enemy & Wildlife Health Bars & Floating Combat Popups
  struct EnemyBarUI {
   public RectTransform root;
   public CanvasGroup group;
   public Text nameLabel;
   public Image fill, trail;
   public Component target;
   public float trailVal;
  }
  readonly List<EnemyBarUI> enemyBars = new List<EnemyBarUI>();

  struct ScreenPopupUI {
   public RectTransform root;
   public CanvasGroup group;
   public Text text;
   public Vector3 worldPos;
   public Vector2 screenVel;
   public float elapsed, duration, baseScale;
  }
  readonly List<ScreenPopupUI> screenPopups = new List<ScreenPopupUI>();

  void Awake() {
   Instance = this;
   theme = new OpenWorldUITheme();
   font = theme.ButtonFont ? theme.ButtonFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   var canvas = gameObject.AddComponent<Canvas>();
   canvas.renderMode = RenderMode.ScreenSpaceOverlay;
   canvas.sortingOrder = 20;
   var scaler = gameObject.AddComponent<CanvasScaler>();
   scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
   scaler.referenceResolution = new Vector2(1280, 720);
   scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
   gameObject.AddComponent<GraphicRaycaster>();
   if (!FindAnyObjectByType<EventSystem>()) {
    var events = new GameObject("World UI Events", typeof(EventSystem), typeof(StandaloneInputModule));
    events.transform.SetParent(transform, false);
   }

   // World-space screen overlay layer behind HUD panels (enemy bars, lock-on reticle, damage popups)
   worldUiRoot = Rect("World Space UI", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   worldUiRoot.offsetMin = worldUiRoot.offsetMax = Vector2.zero;
   BuildWorldSpaceUI();

   safeRoot = Rect("Safe Area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   ApplySafeArea();
   BuildStatus();
   OpenWorldStaminaWheel.Create(transform);
   BuildNavigation();
   BuildControls();
   BuildMenus();
   BuildClock();
   BuildTitleScreen();
  }

  void BuildWorldSpaceUI() {
   // Pool of 6 sleek screen-space landmark, NPC, campfire & wildlife markers
   for (int i = 0; i < 6; i++) {
    var m = Rect("WorldMarker_" + i, worldUiRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(216, 38));
    var cg = m.gameObject.AddComponent<CanvasGroup>();
    cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
    theme.Shape(m.gameObject, "fadeH", new Color(0.03f, 0.055f, 0.10f, 0.84f)).raycastTarget = false;

    var topRule = Rect("TopRule", m, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(156, 1f));
    theme.Shape(topRule.gameObject, "fadeH", OpenWorldUITheme.GoldLine).raycastTarget = false;

    var ic = Rect("Diamond", m, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 1f), new Vector2(11, 11));
    var icImg = theme.Shape(ic.gameObject, "diamond", gold);
    icImg.raycastTarget = false;

    var t1 = Label(m, "", new Vector2(0, 6), new Vector2(204, 17), new Vector2(0.5f, 0.5f), 12);
    t1.font = theme.ButtonFont ? theme.ButtonFont : font;
    t1.color = gold;
    AddShadow(t1.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1f, -1f));

    var t2 = Label(m, "", new Vector2(0, -9), new Vector2(204, 15), new Vector2(0.5f, 0.5f), 11);
    t2.font = theme.Body ? theme.Body : font;
    t2.color = OpenWorldUITheme.TextMain;
    AddShadow(t2.gameObject, new Color(0, 0, 0, 0.85f), new Vector2(1f, -1f));

    m.gameObject.SetActive(false);
    worldMarkers.Add(new WorldMarkerUI { root = m, group = cg, icon = icImg, titleLabel = t1, subLabel = t2 });
   }

   for (int i = 0; i < 6; i++) {
    var r = Rect("EnemyBar_" + i, worldUiRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(148, 32));
    var cg = r.gameObject.AddComponent<CanvasGroup>();
    cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

    var nameT = Label(r, "", new Vector2(0, 8), new Vector2(168, 16), new Vector2(0.5f, 0.5f), 12);
    nameT.font = theme.ButtonFont ? theme.ButtonFont : font;
    nameT.color = OpenWorldUITheme.TextMain;
    AddShadow(nameT.gameObject, new Color(0, 0, 0, 0.85f), new Vector2(1f, -1f));

    var barBg = Rect("BarBg", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(118, 7));
    theme.Round(barBg.gameObject, new Color(0.02f, 0.04f, 0.08f, 0.88f), 3.5f);

    var trailR = Rect("Trail", barBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    trailR.offsetMin = new Vector2(1, 1); trailR.offsetMax = new Vector2(-1, -1);
    var trailImg = trailR.gameObject.AddComponent<Image>();
    trailImg.color = new Color(1f, 0.9f, 0.65f, 0.8f);
    trailImg.raycastTarget = false;

    var fillR = Rect("Fill", barBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    fillR.offsetMin = new Vector2(1, 1); fillR.offsetMax = new Vector2(-1, -1);
    var fillImg = fillR.gameObject.AddComponent<Image>();
    fillImg.color = new Color(0.95f, 0.28f, 0.24f, 0.96f);
    fillImg.raycastTarget = false;

    var border = Rect("Border", barBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    border.offsetMin = border.offsetMax = Vector2.zero;
    theme.Round(border.gameObject, OpenWorldUITheme.GoldLine, 3.5f, true).raycastTarget = false;

    r.gameObject.SetActive(false);
    enemyBars.Add(new EnemyBarUI { root = r, group = cg, nameLabel = nameT, fill = fillImg, trail = trailImg, trailVal = 1f });
   }

   // Lock-on diamond reticle
   lockOnReticle = Rect("LockOnReticle", worldUiRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
   theme.Shape(lockOnReticle.gameObject, "diamondOutline", OpenWorldUITheme.Gold).raycastTarget = false;
   var innerLock = Rect("Inner", lockOnReticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
   theme.Shape(innerLock.gameObject, "g_lock", new Color(1f, 0.92f, 0.55f, 0.9f)).raycastTarget = false;
   lockOnReticle.gameObject.SetActive(false);

   // Pool of 14 crisp screen-space combat popups
   for (int i = 0; i < 14; i++) {
    var p = Rect("CombatPopup_" + i, worldUiRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 44));
    var cg = p.gameObject.AddComponent<CanvasGroup>();
    cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
    var t = Label(p, "", Vector2.zero, new Vector2(260, 44), new Vector2(0.5f, 0.5f), 22);
    t.font = theme.ButtonFont ? theme.ButtonFont : font;
    t.fontStyle = FontStyle.Bold;
    AddShadow(t.gameObject, new Color(0f, 0f, 0f, 0.92f), new Vector2(1.5f, -1.5f));
    p.gameObject.SetActive(false);
    screenPopups.Add(new ScreenPopupUI { root = p, group = cg, text = t, elapsed = 99f, duration = 0.9f, baseScale = 1f });
   }
  }

  public static void SpawnCombatPopup(Vector3 worldPos, string text, Color color, float scale = 1f) {
   var hud = Instance;
   if (!hud || hud.screenPopups.Count == 0) return;
   int pick = 0;
   float bestElapsed = -1f;
   for (int i = 0; i < hud.screenPopups.Count; i++) {
    if (hud.screenPopups[i].elapsed >= hud.screenPopups[i].duration) { pick = i; break; }
    if (hud.screenPopups[i].elapsed > bestElapsed) { bestElapsed = hud.screenPopups[i].elapsed; pick = i; }
   }
   var item = hud.screenPopups[pick];
   item.worldPos = worldPos + new Vector3(UnityEngine.Random.Range(-0.28f, 0.28f), 0.2f, UnityEngine.Random.Range(-0.28f, 0.28f));
   item.screenVel = new Vector2(UnityEngine.Random.Range(-18f, 18f), 54f * Mathf.Clamp(scale, 0.85f, 1.45f));
   item.elapsed = 0f;
   item.duration = 0.92f;
   item.baseScale = Mathf.Clamp(scale, 0.82f, 1.5f);
   item.text.text = text;
   item.text.color = color;
   item.root.gameObject.SetActive(true);
   item.group.alpha = 1f;
   hud.screenPopups[pick] = item;
  }

  void BuildStatus() {
   // 1. TOP-LEFT: Circular Genshin / Wuthering Waves Radar Minimap
   var mini = Rect("Live Radar Minimap", safeRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -92), new Vector2(156, 156));
   minimapPanel = mini.gameObject;
   var miniGlow = Rect("MiniShadow", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(168, 168));
   theme.Shape(miniGlow.gameObject, "glow", new Color(0f, 0.02f, 0.06f, 0.65f)).raycastTarget = false;
   var miniBack = Rect("MiniBack", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(150, 150));
   theme.Shape(miniBack.gameObject, "circle", OpenWorldUITheme.InkStrong).raycastTarget = false;

   var miniCanvas = Rect("RadarMap", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(134, 134));
   miniCanvas.gameObject.AddComponent<RectMask2D>();
   var realmAtlas = OpenWorldRealm.Instance ? Resources.Load<Texture2D>("World/Realm/RealmAtlas") : null;
   if (realmAtlas) {
    minimapRaw = miniCanvas.gameObject.AddComponent<RawImage>();
    minimapRaw.texture = realmAtlas;
    minimapRaw.raycastTarget = false;
   } else {
    var miniImg = miniCanvas.gameObject.AddComponent<Image>();
    miniImg.sprite = GetOrCreateMapSprite();
    miniImg.raycastTarget = false;
   }

   // Camera view cone inside radar
   minimapCone = Rect("ViewCone", miniCanvas, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(88, 88));
   theme.Shape(minimapCone.gameObject, "g_cone", new Color(1f, 0.9f, 0.55f, 0.16f)).raycastTarget = false;

   var miniRing = Rect("MiniRing", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(152, 152));
   theme.Shape(miniRing.gameObject, "ring", OpenWorldUITheme.Gold).raycastTarget = false;
   var miniInnerRing = Rect("MiniInnerRing", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(138, 138));
   theme.Shape(miniInnerRing.gameObject, "ring", new Color(1f, 0.84f, 0.52f, 0.28f)).raycastTarget = false;

   // Cardinal bearings on radar rim
   var nBadge = Rect("NorthBadge", mini, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 72), new Vector2(20, 20));
   theme.Shape(nBadge.gameObject, "diamond", new Color(0.06f, 0.1f, 0.18f, 0.95f)).raycastTarget = false;
   Label(nBadge, "N", Vector2.zero, new Vector2(20, 18), Vector2.one * 0.5f, 11).color = gold;
   Label(mini, "S", new Vector2(0, -68), new Vector2(20, 16), Vector2.one * 0.5f, 10).color = OpenWorldUITheme.TextDim;
   Label(mini, "E", new Vector2(68, 0), new Vector2(20, 16), Vector2.one * 0.5f, 10).color = OpenWorldUITheme.TextDim;
   Label(mini, "W", new Vector2(-68, 0), new Vector2(20, 16), Vector2.one * 0.5f, 10).color = OpenWorldUITheme.TextDim;

   for (int i = 0; i < 6; i++) {
    var ed = Rect("EnemyBlip_" + i, miniCanvas, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(7, 7));
    theme.Shape(ed.gameObject, "circle", new Color(1f, 0.28f, 0.25f)).raycastTarget = false;
    ed.gameObject.SetActive(false);
    minimapEnemyDots.Add(ed);
   }
   minimapGoalDot = Rect("GoalBlip", miniCanvas, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(11, 11));
   theme.Shape(minimapGoalDot.gameObject, "diamond", new Color(0.32f, 0.96f, 1f)).raycastTarget = false;

   minimapHeroDot = Rect("HeroBlip", miniCanvas, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(13, 13));
   theme.Shape(minimapHeroDot.gameObject, "g_arrow", new Color(1f, 0.92f, 0.32f)).raycastTarget = false;

   // Time & Weather Pill directly below radar
   var timePill = Rect("TimePill", mini, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -14), new Vector2(148, 22));
   theme.Round(timePill.gameObject, OpenWorldUITheme.InkStrong, 11f).raycastTarget = false;
   var timePillOutline = Rect("Outline", timePill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   timePillOutline.offsetMin = timePillOutline.offsetMax = Vector2.zero;
   theme.Round(timePillOutline.gameObject, OpenWorldUITheme.GoldLine, 11f, true).raycastTarget = false;
   minimapFooter = Label(timePill, "09:00 • CLEAR", Vector2.zero, new Vector2(138, 18), Vector2.one * 0.5f, 11);
   minimapFooter.color = gold;

   // Compact circular Realm Clock button on the lower-right rim of the minimap
   var clockButton = IconCircleButton(safeRoot, "TIME", "g_sun", new Vector2(182, -148), new Vector2(0, 1), 40f, true);
   clockButton.onClick.AddListener(() => {
    var gm = OpenWorldManager.I;
    if (!gm) return;
    bool open = !gm.ShowClockModal;
    gm.CloseMenus();
    gm.ShowClockModal = open;
    OpenWorldAudio.Play("sfx_menu", 0.5f);
   });
   clockButtonObject = clockButton.gameObject;

   // 2. TOP-LEFT BELOW MINIMAP: Genshin / WuWa Quest Tracker Banner
   var quest = Rect("Quest tracker", safeRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(198, -228), new Vector2(360, 68));
   theme.Shape(quest.gameObject, "fadeR", new Color(0.03f, 0.06f, 0.11f, 0.85f)).raycastTarget = false;
   objectivePanel = quest.gameObject;
   var qBar = Rect("QuestAccent", quest, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, 0), new Vector2(3, 52));
   qBar.gameObject.AddComponent<Image>().color = gold;
   var qDiamond = Rect("QuestIcon", quest, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 13), new Vector2(14, 14));
   theme.Shape(qDiamond.gameObject, "diamond", gold).raycastTarget = false;
   var qRing = Rect("QuestIconRing", qDiamond, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(22, 22));
   theme.Shape(qRing.gameObject, "diamondOutline", new Color(1f, 0.84f, 0.52f, 0.5f)).raycastTarget = false;

   objectiveTitle = Label(quest, "", new Vector2(-14, 13), new Vector2(256, 22), new Vector2(0.5f, 0.5f), 14);
   objectiveTitle.alignment = TextAnchor.MiddleLeft;
   objectiveTitle.color = gold;
   AddShadow(objectiveTitle.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));

   objectiveDist = Label(quest, "", new Vector2(118, 13), new Vector2(92, 20), new Vector2(0.5f, 0.5f), 12);
   objectiveDist.alignment = TextAnchor.MiddleRight;
   objectiveDist.color = new Color(0.38f, 0.96f, 1f);
   AddShadow(objectiveDist.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));

   objective = Label(quest, "", new Vector2(14, -12), new Vector2(316, 26), new Vector2(0.5f, 0.5f), 15);
   objective.font = theme.Body ? theme.Body : font;
   objective.alignment = TextAnchor.MiddleLeft;
   objective.color = OpenWorldUITheme.TextMain;
   AddShadow(objective.gameObject, new Color(0, 0, 0, 0.85f), new Vector2(1, -1));

   // 3. LEFT-SIDE PICKUP & LOOT FEED (Zelda / Genshin toast stack below Quest Tracker)
   for (int i = 0; i < 4; i++) {
    var row = Rect("LootToast_" + i, safeRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(148, -284 - i * 34), new Vector2(260, 28));
    var cg = row.gameObject.AddComponent<CanvasGroup>();
    cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
    theme.Shape(row.gameObject, "fadeR", new Color(0.04f, 0.08f, 0.14f, 0.86f)).raycastTarget = false;
    var accent = Rect("Accent", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(2.5f, 22));
    accent.gameObject.AddComponent<Image>().color = gold;
    var ic = Rect("Icon", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(20, 20));
    var icImg = theme.Image(ic.gameObject, "Icons/UI_Icon_Coin");
    icImg.preserveAspect = true; icImg.raycastTarget = false;
    var txt = Label(row, "", new Vector2(16, 0), new Vector2(212, 24), new Vector2(0.5f, 0.5f), 13);
    txt.alignment = TextAnchor.MiddleLeft;
    txt.color = OpenWorldUITheme.TextMain;
    row.gameObject.SetActive(false);
    feedPool.Add(new FeedItem { root = row, group = cg, icon = icImg, label = txt, hideAt = -1f });
   }

   // 4. TOP-CENTER: Minimalist Cardinal Compass & Exploration Ribbon
   var comp = Rect("Compass Ribbon", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(360, 38));
   theme.Shape(comp.gameObject, "fadeH", new Color(0.03f, 0.055f, 0.1f, 0.72f)).raycastTarget = false;
   var compTopLine = Rect("TopLine", comp, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(280, 1));
   theme.Shape(compTopLine.gameObject, "fadeH", OpenWorldUITheme.GoldSoft).raycastTarget = false;
   var compBotLine = Rect("BotLine", comp, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(280, 1));
   theme.Shape(compBotLine.gameObject, "fadeH", OpenWorldUITheme.GoldLine).raycastTarget = false;
   compassPanel = comp.gameObject;
   compassLabel = Label(comp, "", new Vector2(0, 6), new Vector2(340, 18), Vector2.one * 0.5f, 13);
   compassLabel.color = gold;
   compassSubLabel = Label(comp, "", new Vector2(0, -9), new Vector2(340, 16), Vector2.one * 0.5f, 11);
   compassSubLabel.font = theme.Body ? theme.Body : font;
   compassSubLabel.color = OpenWorldUITheme.TextDim;

   // 5. TOP-CENTER: Boss Health Bar (Wuthering Waves / Genshin style)
   var bossRect = Rect("Boss Health Panel", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(520, 52));
   theme.Shape(bossRect.gameObject, "fadeH", new Color(0.03f, 0.04f, 0.08f, 0.84f)).raycastTarget = false;
   bossPanel = bossRect.gameObject;
   bossGroup = bossRect.gameObject.AddComponent<CanvasGroup>();
   bossGroup.blocksRaycasts = false;
   var bossDiamond = Rect("BossIcon", bossRect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -3), new Vector2(10, 10));
   theme.Shape(bossDiamond.gameObject, "diamond", new Color(1f, 0.35f, 0.3f)).raycastTarget = false;
   bossLabel = Label(bossRect, "", new Vector2(-48, 11), new Vector2(360, 22), Vector2.one * 0.5f, 16);
   bossLabel.font = theme.Heading ? theme.Heading : font;
   bossLabel.alignment = TextAnchor.MiddleLeft;
   bossLabel.color = gold;
   bossSubLabel = Label(bossRect, "", new Vector2(158, 11), new Vector2(160, 20), Vector2.one * 0.5f, 13);
   bossSubLabel.alignment = TextAnchor.MiddleRight;
   bossSubLabel.color = OpenWorldUITheme.TextDim;

   var bossBarBack = Rect("BossBarBack", bossRect, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -10), new Vector2(476, 10));
   theme.Round(bossBarBack.gameObject, new Color(0.02f, 0.03f, 0.06f, 0.92f), 5f);
   var bossTrailRect = Rect("BossBarTrail", bossBarBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   bossTrailRect.offsetMin = new Vector2(1.5f, 1.5f); bossTrailRect.offsetMax = new Vector2(-1.5f, -1.5f);
   bossTrailFill = theme.Image(bossTrailRect.gameObject, "Frames/UI_ProgressBar_Fill");
   bossTrailFill.color = new Color(1f, 0.9f, 0.65f, 0.85f);
   bossTrailFill.raycastTarget = false;
   var bossBarFill = Rect("BossBarFill", bossBarBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   bossBarFill.offsetMin = new Vector2(1.5f, 1.5f); bossBarFill.offsetMax = new Vector2(-1.5f, -1.5f);
   bossHealthFill = theme.Image(bossBarFill.gameObject, "Frames/UI_ProgressBar_Fill");
   bossHealthFill.color = new Color(0.95f, 0.24f, 0.22f);
   bossHealthFill.raycastTarget = false;
   var bossBarOutline = Rect("BossBarOutline", bossBarBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   bossBarOutline.offsetMin = bossBarOutline.offsetMax = Vector2.zero;
   theme.Round(bossBarOutline.gameObject, OpenWorldUITheme.GoldSoft, 5f, true).raycastTarget = false;
   bossPanel.SetActive(false);

   // 6. TOP-CENTER: Zelda / Genshin Notification & Discovery Banner
   var banner = Rect("Notification", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(680, 44));
   theme.Shape(banner.gameObject, "fadeH", new Color(0.03f, 0.055f, 0.1f, 0.88f)).raycastTarget = false;
   var bTop = Rect("TopRule", banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(520, 1.5f));
   theme.Shape(bTop.gameObject, "fadeH", gold).raycastTarget = false;
   var bBot = Rect("BotRule", banner, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(520, 1.5f));
   theme.Shape(bBot.gameObject, "fadeH", OpenWorldUITheme.GoldSoft).raycastTarget = false;
   var bGem = Rect("CenterDiamond", banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(8, 8));
   theme.Shape(bGem.gameObject, "diamond", gold).raycastTarget = false;
   noticePanel = banner.gameObject;
   noticeGroup = banner.gameObject.AddComponent<CanvasGroup>();
   noticeGroup.blocksRaycasts = false;
   notice = Label(banner, "", Vector2.zero, new Vector2(640, 38), Vector2.one * 0.5f, 17);
   notice.color = gold;
   AddShadow(notice.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));

   // 7. BOTTOM-CENTER: Genshin Impact / Wuthering Waves Hero Status Bar
   var box = Rect("Hero status", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(436, 64));
   heroStatusPanel = box.gameObject;
   theme.Round(box.gameObject, new Color(0.03f, 0.055f, 0.1f, 0.82f), 16f).raycastTarget = false;
   var boxBorder = Rect("Border", box, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   boxBorder.offsetMin = boxBorder.offsetMax = Vector2.zero;
   theme.Round(boxBorder.gameObject, OpenWorldUITheme.GoldLine, 16f, true).raycastTarget = false;

   // Circular Aster Portrait with golden rim & Element badge on the left
   var portraitRing = Rect("PortraitRing", box, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 2), new Vector2(54, 54));
   theme.Shape(portraitRing.gameObject, "circle", new Color(0.05f, 0.09f, 0.16f, 0.98f)).raycastTarget = false;
   var portraitMask = Rect("PortraitMask", portraitRing, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(48, 48));
   portraitMask.gameObject.AddComponent<RectMask2D>();
   var portrait = Rect("Aster portrait", portraitMask, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(50, 50));
   theme.Image(portrait.gameObject, "Portraits/UI_Avatar_Aster_HP_Headshot").preserveAspect = true;
   portrait.GetComponent<Image>().raycastTarget = false;
   var portraitRim = Rect("Rim", portraitRing, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(54, 54));
   theme.Shape(portraitRim.gameObject, "ring", gold).raycastTarget = false;

   var elemBadgeBg = Rect("ElemBadge", portraitRing, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), new Vector2(20, 20));
   theme.Shape(elemBadgeBg.gameObject, "circle", new Color(0.04f, 0.07f, 0.12f, 0.96f)).raycastTarget = false;
   var elemGlyphR = Rect("Glyph", elemBadgeBg, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(14, 14));
   heroElemDot = theme.Shape(elemGlyphR.gameObject, "g_flame", OpenWorldUITheme.ElementColors[0]);
   heroElemDot.raycastTarget = false;

   // Level & State header row above HP bar (anchored to center of box so bounds are exact)
   levelBadge = Label(box, "LV.1 WARDEN", new Vector2(-28, 17), new Vector2(228, 18), new Vector2(0.5f, 0.5f), 12);
   levelBadge.alignment = TextAnchor.MiddleLeft;
   levelBadge.color = gold;

   status = Label(box, "HP 5 / 5", new Vector2(138, 17), new Vector2(124, 18), new Vector2(0.5f, 0.5f), 12);
   status.font = theme.ButtonFont ? theme.ButtonFont : font;
   status.alignment = TextAnchor.MiddleRight;
   status.color = OpenWorldUITheme.TextMain;

   // HP Bar with delayed damage trail
   health = BarWithTrail(box, new Vector2(28, 0), new Vector2(344, 11), OpenWorldUITheme.Health, out healthTrail);
   // Aether / Resonance Bar below HP
   aether = BarSimple(box, new Vector2(-18, -14), new Vector2(252, 7), OpenWorldUITheme.AetherColor);
   aetherStatus = Label(box, "AE 100/100", new Vector2(156, -14), new Vector2(88, 15), new Vector2(0.5f, 0.5f), 11);
   aetherStatus.alignment = TextAnchor.MiddleRight;
   aetherStatus.color = new Color(0.82f, 0.74f, 1f);

   // Subtle micro stamina bar at bottom edge of status pill (primary wheel floats by shoulder)
   stamina = BarSimple(box, new Vector2(28, -25), new Vector2(344, 3), new Color(0.38f, 0.96f, 0.58f, 0.75f));
  }

  Image BarWithTrail(Transform parent, Vector2 pos, Vector2 size, Color color, out Image trailImage) {
   var back = Rect("Meter", parent, Vector2.one * 0.5f, Vector2.one * 0.5f, pos, size);
   theme.Round(back.gameObject, new Color(0.015f, 0.03f, 0.06f, 0.92f), size.y * 0.5f);
   var tr = Rect("Trail", back, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   tr.offsetMin = new Vector2(1.5f, 1.5f); tr.offsetMax = new Vector2(-1.5f, -1.5f);
   trailImage = theme.Image(tr.gameObject, "Frames/UI_ProgressBar_Fill");
   trailImage.color = new Color(1f, 0.92f, 0.68f, 0.82f);
   trailImage.raycastTarget = false;
   var fill = Rect("Value", back, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   fill.offsetMin = new Vector2(1.5f, 1.5f); fill.offsetMax = new Vector2(-1.5f, -1.5f);
   var image = theme.Image(fill.gameObject, "Frames/UI_ProgressBar_Fill");
   image.color = color; image.raycastTarget = false;
   var rim = Rect("Rim", back, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   rim.offsetMin = rim.offsetMax = Vector2.zero;
   theme.Round(rim.gameObject, OpenWorldUITheme.GoldLine, size.y * 0.5f, true).raycastTarget = false;
   return image;
  }

  Image BarSimple(Transform parent, Vector2 pos, Vector2 size, Color color) {
   var back = Rect("Meter", parent, Vector2.one * 0.5f, Vector2.one * 0.5f, pos, size);
   theme.Round(back.gameObject, new Color(0.015f, 0.03f, 0.06f, 0.88f), size.y * 0.5f);
   var fill = Rect("Value", back, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   fill.offsetMin = new Vector2(1f, 1f); fill.offsetMax = new Vector2(-1f, -1f);
   var image = theme.Image(fill.gameObject, "Frames/UI_ProgressBar_Fill");
   image.color = color; image.raycastTarget = false;
   return image;
  }

  void BuildNavigation() {
   var trRoot = Rect("TopRightHUD", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   trRoot.offsetMin = trRoot.offsetMax = Vector2.zero;
   topRightHudGroup = trRoot.gameObject;

   // Top-Right Currency, Hunting Meat & Graphics Pill Bar
   var resPill = Rect("ResourceBar", trRoot, Vector2.one, Vector2.one, new Vector2(-282, -26), new Vector2(264, 30));
   theme.Round(resPill.gameObject, OpenWorldUITheme.InkStrong, 15f).raycastTarget = false;
   var resOutline = Rect("Outline", resPill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   resOutline.offsetMin = resOutline.offsetMax = Vector2.zero;
   theme.Round(resOutline.gameObject, OpenWorldUITheme.GoldLine, 15f, true).raycastTarget = false;

   var coinIcon = Rect("CoinIcon", resPill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(18, 18));
   theme.Image(coinIcon.gameObject, "Icons/UI_Icon_Coin").preserveAspect = true;
   coinIcon.GetComponent<Image>().raycastTarget = false;
   goldLabel = Label(resPill, "120", new Vector2(-68, 0), new Vector2(64, 22), new Vector2(0.5f, 0.5f), 13);
   goldLabel.alignment = TextAnchor.MiddleLeft;
   goldLabel.color = gold;

   var gemIcon = Rect("GemIcon", resPill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(108, 0), new Vector2(18, 18));
   theme.Image(gemIcon.gameObject, "Icons/UI_Icon_Gem").preserveAspect = true;
   gemIcon.GetComponent<Image>().raycastTarget = false;
   gemLabel = Label(resPill, "10", new Vector2(20, 0), new Vector2(50, 22), new Vector2(0.5f, 0.5f), 13);
   gemLabel.alignment = TextAnchor.MiddleLeft;
   gemLabel.color = new Color(0.58f, 0.94f, 1f);

   var meatIcon = Rect("MeatIcon", resPill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(188, 0), new Vector2(17, 17));
   theme.Shape(meatIcon.gameObject, "g_meat", new Color(1f, 0.68f, 0.45f)).raycastTarget = false;
   meatLabel = Label(resPill, "0", new Vector2(98, 0), new Vector2(48, 22), new Vector2(0.5f, 0.5f), 13);
   meatLabel.alignment = TextAnchor.MiddleLeft;
   meatLabel.color = new Color(1f, 0.76f, 0.55f);

   // Compact Graphics tier pill button next to resources
   var quality = PillButton(trRoot, "HIGH • 60 FPS", new Vector2(-76, -26), new Vector2(124, 30), Vector2.one, 11);
   qualityLabel = quality.GetComponentInChildren<Text>();
   quality.onClick.AddListener(() => {
    var graphics = OpenWorldGraphics.Instance;
    if (!graphics) return;
    graphics.Cycle();
    OpenWorldManager.I?.ShowBanner(graphics.Label + " graphics • " + graphics.TargetFrameRate + " fps target");
   });

   // Top-Right Genshin / WuWa Circular Icon Row: MAP, CHESTS, GEAR, SKINS, TITLE, PAUSE
   (string title, string icon, bool kitGlyph)[] navItems = {
    ("MAP", "Icons/UI_Icon_Atlas", false),
    ("CHESTS", "Icons/UI_Icon_Vault", false),
    ("GEAR", "Icons/UI_Icon_Arsenal", false),
    ("SKINS", "Icons/UI_Icon_Wardrobe", false),
    ("TITLE", "Icons/UI_Icon_MainMenu", false),
    ("PAUSE", "g_pause", true)
   };

   for (int i = 0; i < navItems.Length; i++) {
    int index = i;
    float x = -38f - (navItems.Length - 1 - i) * 58f;
    var btn = IconCircleButton(trRoot, navItems[i].title, navItems[i].icon, new Vector2(x, -76f), Vector2.one, 46f, navItems[i].kitGlyph);
    btn.onClick.AddListener(() => {
     var gm = OpenWorldManager.I;
     if (!gm) return;
     if (index == 4) {
      gm.CloseMenus();
      SetTitleScreenVisible(true);
      OpenWorldAudio.Play("sfx_menu", 0.5f);
      return;
     }
     if (index == 5) {
      bool open = !gm.ShowPauseModal;
      gm.CloseMenus();
      gm.ShowPauseModal = open;
      OpenWorldAudio.Play("sfx_menu", 0.5f);
      return;
     }
     bool wasOpen = index == 0 ? gm.ShowMapModal : index == 1 ? gm.ShowVaultModal : index == 2 ? gm.ShowArsenalModal : gm.ShowWardrobeModal;
     gm.CloseMenus();
     if (index == 0) gm.ShowMapModal = !wasOpen;
     if (index == 1) gm.ShowVaultModal = !wasOpen;
     if (index == 2) gm.ShowArsenalModal = !wasOpen;
     if (index == 3) gm.ShowWardrobeModal = !wasOpen;
     OpenWorldAudio.Play("sfx_menu", 0.5f);
    });
   }
  }

  void BuildControls() {
   controls = new GameObject("Adventure Controls", typeof(RectTransform));
   controls.transform.SetParent(safeRoot, false);
   var cr = (RectTransform)controls.transform;
   cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
   cr.offsetMin = cr.offsetMax = Vector2.zero;

   // Behind the buttons: each gesture owns one finger, leaving others free.
   var look = Rect("Swipe to look", cr, new Vector2(0.38f, 0), Vector2.one, Vector2.zero, Vector2.zero);
   look.offsetMin = new Vector2(0, 16); look.offsetMax = new Vector2(-16, -110);
   look.gameObject.AddComponent<Image>().color = Color.clear;
   var lookInput = look.gameObject.AddComponent<OpenWorldTouchSurface>();
   lookInput.Look = true;

   // Bottom-Left Virtual Joystick
   var stick = Rect("Movement", cr, Vector2.zero, Vector2.zero, new Vector2(118, 118), new Vector2(164, 164));
   var stickImg = theme.Image(stick.gameObject, "Buttons/UI_Move_Joystick_OuterBase");
   stickImg.color = new Color(1f, 1f, 1f, 0.88f);
   var knob = Rect("Thumb", stick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
   theme.Image(knob.gameObject, "Buttons/UI_Move_Joystick_InnerAnalog");
   knob.GetComponent<Image>().raycastTarget = false;
   var joystick = stick.gameObject.AddComponent<OpenWorldTouchSurface>();
   joystick.Knob = knob;

   // Bottom-Right Radial Combat Cluster (Genshin Impact / Wuthering Waves layout)
   ActionButton(cr, "ATTACK\nHold to charge", -96, 96, 128, null, true);

   // Inner arc around Attack: Dash (bottom-left), Parry (mid-left), Jump (top-right)
   ActionButton(cr, "DASH", -218, 56, 86, h => h.TouchDodge = true);
   ActionButton(cr, "PARRY", -188, 158, 86, h => h.TouchParry = true);
   ActionButton(cr, "JUMP", -78, 212, 86, h => h.TouchJump = true);

   // Outer Resonance / Elemental Skill Arc: Spell, Burst (Ultimate), Echo, Element Switch
   ActionButton(cr, "SPELL", -318, 56, 78, h => h.TouchSpell = true);
   ActionButton(cr, "BURST", -292, 152, 84, h => h.TouchBurst = true);
   ActionButton(cr, "SUMMON", -410, 56, 74, h => { h.TouchEcho = true; wasEchoPressed = true; });
   ActionButton(cr, "ELEMENT", -392, 146, 72, h => h.CycleElement());

   // Upper Tactical Utility Buttons: Lock-On & Grapple
   ActionButton(cr, "LOCK ON", -160, 256, 76, h => h.ToggleLockOn());
   ActionButton(cr, "GRAPPLE", -240, 240, 76, h => OpenWorldManager.I?.TryGrappleToNearestAnchor());

   // Center-Right Contextual Interact Prompt Pill (Genshin / Zelda F-prompt style)
   interact = PillButton(cr, "INTERACT", new Vector2(220, 210), new Vector2(236, 48), new Vector2(0.5f, 0), 16);
   interactGroup = interact.gameObject.AddComponent<CanvasGroup>();
   var iDiamond = Rect("PromptBadge", interact.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(24, 24));
   theme.Shape(iDiamond.gameObject, "diamond", gold).raycastTarget = false;
   var fKey = Label(iDiamond, "F", Vector2.zero, new Vector2(20, 20), Vector2.one * 0.5f, 12);
   fKey.color = new Color(0.04f, 0.08f, 0.14f);
   var iChev = Rect("Chevron", interact.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(14, 14));
   theme.Shape(iChev.gameObject, "g_chevron", gold).raycastTarget = false;
   interactLabel = interact.GetComponentInChildren<Text>();
   interactLabel.rectTransform.anchoredPosition = new Vector2(6, 0);
   interactLabel.rectTransform.sizeDelta = new Vector2(164, 36);
   interact.onClick.AddListener(() => OpenWorldManager.I?.Interact());
  }

  void ActionButton(Transform parent, string title, float x, float y, float diameter, Action<OpenWorldHero> action, bool attack = false) {
   string art = attack ? "UI_Action_Blade_Attack_HoldToCharge" : title == "DASH" ? "UI_Action_Dodge_Evade" :
    title == "JUMP" ? "UI_Action_Jump_DoubleJump_x2" : title == "PARRY" ? "UI_Action_Parry_Defense" :
    (title == "ELEMENT" || title == "BURST") ? "UI_Action_ElementalPower" : "UI_Action_Spell_LockOn_HoldToLock";

   var r = Rect(attack ? "BLADE\nCHARGE" : title, parent, new Vector2(1, 0), new Vector2(1, 0), new Vector2(x, y), Vector2.one * diameter);

   // Subtle shadow glow behind every circular combat button
   var shadow = Rect("Shadow", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.16f));
   theme.Shape(shadow.gameObject, "glow", new Color(0f, 0.02f, 0.06f, 0.55f)).raycastTarget = false;

   bool liveGlyph = title == "ELEMENT" || title == "SUMMON" || title == "LOCK ON" || title == "GRAPPLE";
   // These controls own live glyphs; the old painted art includes a second, static symbol.
   var img = liveGlyph ? theme.Shape(r.gameObject, "circle", new Color(.015f, .055f, .12f, .96f)) : theme.Image(r.gameObject, "Buttons/" + art);
   if (liveGlyph) {
    var rim = Rect("Utility rim", r, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * (diameter * .98f));
    theme.Shape(rim.gameObject, "ring", gold).raycastTarget = false;
   }
   img.preserveAspect = true;
   var b = r.gameObject.AddComponent<Button>();
   b.transition = Selectable.Transition.ColorTint;
   var colors = b.colors;
   colors.normalColor = new Color(1f, 1f, 1f, 0.94f);
   colors.highlightedColor = new Color(1f, 0.96f, 0.85f, 1f);
   colors.pressedColor = new Color(0.72f, 0.92f, 1f, 1f);
   b.colors = colors;
   b.navigation = new Navigation { mode = Navigation.Mode.None };

   // Custom radial rings, glyphs & cooldown overlays per skill
   if (attack) {
    var ringR = Rect("ChargeRing", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.06f));
    attackChargeRing = theme.Shape(ringR.gameObject, "ringThick", new Color(1f, 0.88f, 0.42f, 0.92f));
    attackChargeRing.type = Image.Type.Filled;
    attackChargeRing.fillMethod = Image.FillMethod.Radial360;
    attackChargeRing.fillOrigin = (int)Image.Origin360.Top;
    attackChargeRing.fillAmount = 0f;
    attackChargeRing.raycastTarget = false;
   } else if (title == "BURST") {
    var glowR = Rect("BurstReadyGlow", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.28f));
    burstGlowImg = theme.Shape(glowR.gameObject, "glow", new Color(1f, 0.65f, 0.25f, 0f));
    burstGlowImg.raycastTarget = false;
    var ringR = Rect("BurstGauge", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.08f));
    burstReadyRing = theme.Shape(ringR.gameObject, "ringThick", OpenWorldUITheme.ElementColors[0]);
    burstReadyRing.type = Image.Type.Filled;
    burstReadyRing.fillMethod = Image.FillMethod.Radial360;
    burstReadyRing.fillOrigin = (int)Image.Origin360.Top;
    burstReadyRing.raycastTarget = false;
   } else if (title == "SPELL") {
    var ringR = Rect("SpellRing", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.06f));
    spellCostRing = theme.Shape(ringR.gameObject, "ring", OpenWorldUITheme.AetherColor);
    spellCostRing.type = Image.Type.Filled;
    spellCostRing.fillMethod = Image.FillMethod.Radial360;
    spellCostRing.fillOrigin = (int)Image.Origin360.Top;
    spellCostRing.raycastTarget = false;
    var costBadge = Rect("CostPill", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(40, 15));
    theme.Round(costBadge.gameObject, OpenWorldUITheme.InkStrong, 7.5f).raycastTarget = false;
    spellCostLabel = Label(costBadge, "25 AE", Vector2.zero, new Vector2(38, 14), Vector2.one * 0.5f, 9);
    spellCostLabel.color = new Color(0.84f, 0.76f, 1f);
   } else if (title == "SUMMON") {
    var gRect = Rect("EchoGlyph", r, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 6), Vector2.one * (diameter * 0.42f));
    theme.Shape(gRect.gameObject, "g_echo", new Color(0.55f, 0.95f, 1f, 0.92f)).raycastTarget = false;
    var cdR = Rect("EchoCd", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 0.94f));
    echoCdRing = theme.Shape(cdR.gameObject, "circle", new Color(0.01f, 0.02f, 0.05f, 0.72f));
    echoCdRing.type = Image.Type.Filled;
    echoCdRing.fillMethod = Image.FillMethod.Radial360;
    echoCdRing.fillOrigin = (int)Image.Origin360.Top;
    echoCdRing.fillAmount = 0f;
    echoCdRing.raycastTarget = false;
    echoCdLabel = Label(r, "", Vector2.zero, new Vector2(56, 22), Vector2.one * 0.5f, 14);
    echoCdLabel.color = Color.white;
   } else if (title == "PARRY") {
    var glowR = Rect("CounterGlow", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * (diameter * 1.28f));
    parryReadyGlow = theme.Shape(glowR.gameObject, "ringThick", new Color(1f, 0.9f, 0.35f, 0f));
    parryReadyGlow.raycastTarget = false;
   } else if (title == "ELEMENT") {
    var gRect = Rect("ElemGlyph", r, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 5), Vector2.one * (diameter * 0.44f));
    elementCycleGlyph = theme.Shape(gRect.gameObject, "g_flame", OpenWorldUITheme.ElementColors[0]);
    elementCycleGlyph.raycastTarget = false;
   } else if (title == "LOCK ON") {
    var gRect = Rect("LockGlyph", r, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 5), Vector2.one * (diameter * 0.44f));
    lockOnGlyph = theme.Shape(gRect.gameObject, "g_lock", gold);
    lockOnGlyph.raycastTarget = false;
   } else if (title == "GRAPPLE") {
    var gRect = Rect("HookGlyph", r, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 5), Vector2.one * (diameter * 0.44f));
    theme.Shape(gRect.gameObject, "g_hook", new Color(0.55f, 0.98f, 0.82f)).raycastTarget = false;
   }

   string captionText = attack ? "ATTACK" : title == "DASH" ? "DASH" : title == "LOCK ON" ? "LOCK" : title;
   var caption = Label(r, captionText, new Vector2(0, -diameter * 0.24f), new Vector2(diameter * 0.62f, diameter * .18f), Vector2.one * 0.5f, attack ? 13 : 11);
   caption.color = gold;
   caption.gameObject.name = "Action caption";
   caption.resizeTextForBestFit = true;
   caption.resizeTextMinSize = 8; caption.resizeTextMaxSize = attack ? 13 : 11;
   caption.horizontalOverflow = HorizontalWrapMode.Wrap; caption.verticalOverflow = VerticalWrapMode.Truncate;
   AddShadow(caption.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));
   if (title == "DASH") dashCaptionLabel = caption;

   if (attack) {
    var atk = b.gameObject.AddComponent<OpenWorldAttackButton>();
    atk.ChargeRing = attackChargeRing;
   } else if (title == "DASH") {
    b.gameObject.AddComponent<OpenWorldDashButton>();
   } else {
    b.onClick.AddListener(() => {
     var gm = OpenWorldManager.I;
     if (gm && gm.Hero && !gm.Hero.Dead && !gm.IsModalOpen) action(gm.Hero);
    });
   }
  }

  Button IconCircleButton(Transform parent, string caption, string iconOrGlyph, Vector2 pos, Vector2 anchor, float diameter, bool kitGlyph) {
   var r = Rect(caption, parent, anchor, anchor, pos, Vector2.one * diameter);
   theme.Shape(r.gameObject, "circle", OpenWorldUITheme.InkStrong);
   var rim = Rect("Rim", r, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * diameter);
   theme.Shape(rim.gameObject, "ring", OpenWorldUITheme.GoldSoft).raycastTarget = false;

   var ic = Rect("Icon", r, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 2), Vector2.one * (diameter * (kitGlyph ? 0.46f : 0.62f)));
   if (kitGlyph) {
    theme.Shape(ic.gameObject, iconOrGlyph, gold).raycastTarget = false;
   } else {
    var im = theme.Image(ic.gameObject, iconOrGlyph);
    im.preserveAspect = true; im.raycastTarget = false;
   }

   var btn = r.gameObject.AddComponent<Button>();
   var colors = btn.colors;
   colors.normalColor = new Color(1f, 1f, 1f, 0.92f);
   colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
   colors.pressedColor = new Color(0.65f, 0.9f, 1f, 1f);
   btn.colors = colors;
   btn.navigation = new Navigation { mode = Navigation.Mode.None };

   var lbl = Label(r, caption, new Vector2(0, -diameter * 0.64f), new Vector2(diameter + 22f, 14f), Vector2.one * 0.5f, 10);
   lbl.color = OpenWorldUITheme.TextMain;
   AddShadow(lbl.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1, -1));
   return btn;
  }

  Button PillButton(Transform parent, string title, Vector2 pos, Vector2 size, Vector2 anchor, int fontSize = 15) {
   var r = Rect(title, parent, anchor, anchor, pos, size);
   theme.Round(r.gameObject, OpenWorldUITheme.Card, size.y * 0.5f);
   var rim = Rect("Outline", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   rim.offsetMin = rim.offsetMax = Vector2.zero;
   theme.Round(rim.gameObject, OpenWorldUITheme.GoldSoft, size.y * 0.5f, true).raycastTarget = false;

   var button = r.gameObject.AddComponent<Button>();
   var colors = button.colors;
   colors.normalColor = Color.white;
   colors.highlightedColor = new Color(1f, 0.96f, 0.84f, 1f);
   colors.pressedColor = new Color(0.7f, 0.9f, 1f, 1f);
   colors.disabledColor = new Color(0.45f, 0.5f, 0.58f, 0.65f);
   button.colors = colors;
   button.navigation = new Navigation { mode = Navigation.Mode.None };

   var lbl = Label(r, title, Vector2.zero, new Vector2(size.x - 16f, size.y - 6f), Vector2.one * 0.5f, fontSize);
   lbl.color = gold;
   return button;
  }

  Button Button(Transform parent, string title, Vector2 pos, Vector2 size, Vector2 anchor) {
   var r = Rect(title, parent, anchor, anchor, pos, size);
   theme.Image(r.gameObject, "Buttons/UI_Button_Standard_Normal");
   var button = r.gameObject.AddComponent<Button>();
   var colors = button.colors;
   colors.highlightedColor = new Color(0.8f, 1f, 0.9f);
   colors.pressedColor = new Color(0.4f, 0.85f, 0.7f);
   button.colors = colors;
   button.transition = Selectable.Transition.SpriteSwap;
   var state = button.spriteState;
   state.highlightedSprite = state.pressedSprite = theme.Sprite("Buttons/UI_Button_Standard_Highlighted");
   button.spriteState = state;
   button.navigation = new Navigation { mode = Navigation.Mode.None };
   Label(r, title, Vector2.zero, new Vector2(size.x * .76f, size.y * .64f), Vector2.one * 0.5f, 20).color = gold;
   return button;
  }

  Text Label(Transform parent, string value, Vector2 pos, Vector2 size, Vector2 anchor, int fontSize) {
   var r = Rect(value.Length == 0 ? "Label" : value, parent, anchor, anchor, pos, size);
   var text = r.gameObject.AddComponent<Text>();
   text.font = font; text.text = value; text.fontSize = fontSize;
   text.alignment = TextAnchor.MiddleCenter;
   text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = fontSize;
   text.color = Color.white; text.raycastTarget = false;
   return text;
  }

  static void AddShadow(GameObject go, Color c, Vector2 dist) {
   var s = go.AddComponent<Shadow>();
   s.effectColor = c;
   s.effectDistance = dist;
  }

  static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size) {
   var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
   r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max;
   r.anchoredPosition = pos; r.sizeDelta = size;
   return r;
  }

  void ApplySafeArea() {
   lastSafe = Screen.safeArea; lastWidth = Screen.width; lastHeight = Screen.height;
   safeRoot.anchorMin = new Vector2(lastSafe.xMin / Mathf.Max(1, lastWidth), lastSafe.yMin / Mathf.Max(1, lastHeight));
   safeRoot.anchorMax = new Vector2(lastSafe.xMax / Mathf.Max(1, lastWidth), lastSafe.yMax / Mathf.Max(1, lastHeight));
   safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
  }

  void BuildTitleScreen() {
   var root = Rect("Title Screen Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   titleRoot = root.gameObject;

   // Fullscreen key art background
   var bg = theme.Image(root.gameObject, "Backgrounds/BG_MainMenu_Fullscreen_Landscape_Primary");
   bg.color = new Color(0.92f, 0.94f, 1f, 1f);

   // Cinematic vignette & bottom dark gradient
   var vig = Rect("Vignette", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   vig.offsetMin = vig.offsetMax = Vector2.zero;
   theme.Shape(vig.gameObject, "vignette", new Color(0.01f, 0.03f, 0.07f, 0.72f)).raycastTarget = false;

   var botFade = Rect("BottomFade", root, new Vector2(0, 0), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
   botFade.offsetMin = botFade.offsetMax = Vector2.zero;
   theme.Shape(botFade.gameObject, "fadeUp", new Color(0.01f, 0.025f, 0.06f, 0.92f)).raycastTarget = false;

   var card = Rect("TitleCenter", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 36), new Vector2(820, 320));
   var ornament = Rect("HeaderOrnament", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(460, 56));
   theme.Image(ornament.gameObject, "Frames/UI_Header_Ornament").preserveAspect = true;
   ornament.GetComponent<Image>().raycastTarget = false;

   var mainTitle = Label(card, "LEGENDS OF THE LOST REALMS", new Vector2(0, 48), new Vector2(800, 56), new Vector2(0.5f, 0.5f), 40);
   mainTitle.font = theme.Heading ? theme.Heading : font;
   mainTitle.color = gold;
   AddShadow(mainTitle.gameObject, new Color(0, 0, 0, 0.95f), new Vector2(2, -2));

   var subTitle = Label(card, "O P E N   W O R L D", new Vector2(0, 4), new Vector2(520, 30), new Vector2(0.5f, 0.5f), 20);
   subTitle.color = new Color(0.86f, 0.92f, 1f);
   AddShadow(subTitle.gameObject, new Color(0, 0, 0, 0.9f), new Vector2(1.5f, -1.5f));

   var rule = Rect("Divider", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(440, 20));
   theme.Image(rule.gameObject, "Frames/UI_Divider_Line").preserveAspect = true;
   rule.GetComponent<Image>().raycastTarget = false;

   var enterBtn = PillButton(root, "ENTER THE REALM", new Vector2(0, 132), new Vector2(310, 54), new Vector2(0.5f, 0), 20);
   enterBtn.onClick.AddListener(() => {
    SetTitleScreenVisible(false);
    OpenWorldAudio.Play("sfx_power_select", 0.55f);
   });

   var rowBtns = Rect("QuickRow", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 68), new Vector2(560, 40));
   var mapBtn = PillButton(rowBtns, "REALM ATLAS", new Vector2(-176, 0), new Vector2(160, 36), new Vector2(0.5f, 0.5f), 13);
   mapBtn.onClick.AddListener(() => { SetTitleScreenVisible(false); if (OpenWorldManager.I) OpenWorldManager.I.ShowMapModal = true; });
   var clockBtn = PillButton(rowBtns, "TIME OF DAY", Vector2.zero, new Vector2(160, 36), new Vector2(0.5f, 0.5f), 13);
   clockBtn.onClick.AddListener(() => { SetTitleScreenVisible(false); if (OpenWorldManager.I) OpenWorldManager.I.ShowClockModal = true; });
   var settingsBtn = PillButton(rowBtns, "SETTINGS", new Vector2(176, 0), new Vector2(160, 36), new Vector2(0.5f, 0.5f), 13);
   settingsBtn.onClick.AddListener(() => { SetTitleScreenVisible(false); if (OpenWorldManager.I) OpenWorldManager.I.ShowPauseModal = true; });

   var footer = Label(root, "Unity 6000.6 URP  •  Press ESC or tap Enter to resume your journey", new Vector2(0, 22), new Vector2(680, 22), new Vector2(0.5f, 0), 13);
   footer.font = theme.Body ? theme.Body : font;
   footer.color = OpenWorldUITheme.TextDim;

   titleRoot.SetActive(false);
  }

  public void SetTitleScreenVisible(bool visible) {
   if (titleRoot) titleRoot.SetActive(visible);
  }

  void PushLootFeed(string iconPath, string message, Color textColor) {
   int slot = 0;
   float earliest = float.MaxValue;
   for (int i = 0; i < feedPool.Count; i++) {
    if (Time.unscaledTime >= feedPool[i].hideAt) { slot = i; break; }
    if (feedPool[i].hideAt < earliest) { earliest = feedPool[i].hideAt; slot = i; }
   }
   var item = feedPool[slot];
   bool isGlyph = iconPath.StartsWith("g_");
   item.icon.sprite = isGlyph ? theme.Kit(iconPath) : theme.Sprite(iconPath);
   item.icon.color = isGlyph ? textColor : Color.white;
   item.label.text = message;
   item.label.color = textColor;
   item.hideAt = Time.unscaledTime + 3.4f;
   item.root.gameObject.SetActive(true);
   item.group.alpha = 1f;
   feedPool[slot] = item;
  }

  void UpdateLootFeed(OpenWorldManager gm, bool overlayOpen) {
   bool mealBuff = gm.Hero && gm.Hero.IsMealBuffActive;
   if (lastGold < 0) {
    lastGold = gm.Gold; lastGems = gm.Gems; lastKeys = gm.KeysCollected;
    lastRelics = gm.RelicsCount; lastBounties = gm.BountiesCount;
    lastMeat = gm.RawMeat; wasMealBuffed = mealBuff;
    lastChestSeen = gm.LastChestResult ?? "";
    return;
   }
   if (gm.Gold > lastGold) PushLootFeed("Icons/UI_Icon_Coin", $"+{gm.Gold - lastGold} Gold", gold);
   if (gm.Gems > lastGems) PushLootFeed("Icons/UI_Icon_Gem", $"+{gm.Gems - lastGems} Aether Gems", new Color(0.55f, 0.95f, 1f));
   if (gm.RawMeat > lastMeat) PushLootFeed("g_meat", $"+{gm.RawMeat - lastMeat} Raw Meat (Cook at Campfire)", new Color(1f, 0.68f, 0.45f));
   if (mealBuff && !wasMealBuffed) PushLootFeed("g_flame", "Hearty Campfire Meal (+ATK & Stamina)", new Color(1f, 0.78f, 0.36f));
   if (gm.KeysCollected > lastKeys) PushLootFeed("Icons/UI_Icon_Star", $"Sanctum Key ({gm.KeysCollected}/{OpenWorldManager.TotalKeys})", new Color(1f, 0.92f, 0.45f));
   if (gm.RelicsCount > lastRelics) PushLootFeed("Icons/UI_Icon_Sanctuary", $"Ancient Relic ({gm.RelicsCount}/4)", new Color(0.55f, 1f, 0.78f));
   if (gm.BountiesCount > lastBounties) PushLootFeed("Icons/UI_Icon_Achievements", $"Bounty Slain ({gm.BountiesCount}/3)", new Color(1f, 0.65f, 0.32f));
   if (!string.IsNullOrEmpty(gm.LastChestResult) && gm.LastChestResult != lastChestSeen) {
    PushLootFeed("Icons/UI_Icon_Vault", gm.LastChestResult, gold);
   }
   lastGold = gm.Gold; lastGems = gm.Gems; lastKeys = gm.KeysCollected;
   lastRelics = gm.RelicsCount; lastBounties = gm.BountiesCount;
   lastMeat = gm.RawMeat; wasMealBuffed = mealBuff;
   lastChestSeen = gm.LastChestResult ?? "";

   float now = Time.unscaledTime;
   for (int i = 0; i < feedPool.Count; i++) {
    var f = feedPool[i];
    if (!f.root.gameObject.activeSelf) continue;
    float rem = f.hideAt - now;
    if (rem <= 0f || overlayOpen) { f.root.gameObject.SetActive(false); continue; }
    f.group.alpha = Mathf.Clamp01(rem * 2.5f);
   }
  }

  void UpdateWorldSpaceUI(OpenWorldManager gm, OpenWorldHero h) {
   var cam = Camera.main;
   bool hideAll = !cam || gm.IsModalOpen || (titleRoot && titleRoot.activeSelf);
   if (worldUiRoot && worldUiRoot.gameObject.activeSelf == hideAll) worldUiRoot.gameObject.SetActive(!hideAll);
   if (hideAll) return;

   float dt = Time.unscaledDeltaTime;
   Vector3 hp = h.transform.position;

   // 0. World Landmark, Shrine, Campfire, NPC & Wildlife Screen-Space Markers
   if (Time.unscaledTime >= nextLabelScanAt) {
    nextLabelScanAt = Time.unscaledTime + 0.8f;
    cachedWorldLabels = FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude);
    cachedAnimals = FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsInactive.Exclude);
    for (int i = 0; i < cachedWorldLabels.Length; i++) {
     var tm = cachedWorldLabels[i];
     if (!tm) continue;
     var mr = tm.GetComponent<MeshRenderer>();
     if (mr && mr.enabled) mr.enabled = false;
    }
   }

   int markerIdx = 0;
   for (int i = 0; i < cachedWorldLabels.Length && markerIdx < worldMarkers.Count; i++) {
    var tm = cachedWorldLabels[i];
    if (!tm || !tm.gameObject.activeInHierarchy || tm.name == "EnemyStatusLabel") continue;
    string rawText = (tm.text ?? "").Trim();
    if (string.IsNullOrEmpty(rawText)) continue;
    float d2 = (tm.transform.position - hp).sqrMagnitude;
    if (d2 > 22f * 22f) continue;

    Vector3 sp = cam.WorldToScreenPoint(tm.transform.position);
    if (sp.z <= 0.1f) continue;
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) continue;

    var m = worldMarkers[markerIdx];
    if (!m.root.gameObject.activeSelf) m.root.gameObject.SetActive(true);
    m.root.anchoredPosition = lp;
    float distFade = Mathf.Clamp01(1.35f - Mathf.Sqrt(d2) / 22f);
    m.group.alpha = distFade;

    int nl = rawText.IndexOf('\n');
    if (nl >= 0) {
     m.titleLabel.text = rawText.Substring(0, nl).Trim();
     m.subLabel.text = rawText.Substring(nl + 1).Replace('\n', ' ').Trim();
    } else {
     m.titleLabel.text = rawText;
     m.subLabel.text = "";
    }
    Color tint = Color.Lerp( new Color(tm.color.r, tm.color.g, tm.color.b, 1f), gold, 0.35f);
    m.titleLabel.color = tint;
    m.icon.sprite = theme.Kit("diamond");
    m.icon.color = tint;
    worldMarkers[markerIdx] = m;
    markerIdx++;
   }

   // Nearby Wildlife Markers (Horses & Hunted Deer ready for meat collection)
   for (int i = 0; i < cachedAnimals.Length && markerIdx < worldMarkers.Count; i++) {
    var a = cachedAnimals[i];
    if (!a || !a.isActiveAndEnabled || a.Rider != null) continue;
    if (!a.Horse && !(a.Deer && a.Dead)) continue;
    float d2 = (a.transform.position - hp).sqrMagnitude;
    if (d2 > 18f * 18f) continue;

    Vector3 wPos = a.transform.position + Vector3.up * (a.Horse ? 2.15f : 1.25f);
    Vector3 sp = cam.WorldToScreenPoint(wPos);
    if (sp.z <= 0.1f) continue;
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) continue;

    var m = worldMarkers[markerIdx];
    if (!m.root.gameObject.activeSelf) m.root.gameObject.SetActive(true);
    m.root.anchoredPosition = lp;
    m.group.alpha = Mathf.Clamp01(1.3f - Mathf.Sqrt(d2) / 18f);
    if (a.Horse) {
     string horseTitle = !string.IsNullOrEmpty(a.SpeciesName) ? a.SpeciesName.ToUpperInvariant() : "STEED";
     m.titleLabel.text = horseTitle;
     m.subLabel.text = "Mount & Ride [F]";
     m.titleLabel.color = gold;
     m.icon.sprite = theme.Kit("g_horse");
     m.icon.color = gold;
    } else {
     string preyTitle = !string.IsNullOrEmpty(a.SpeciesName) ? a.SpeciesName.ToUpperInvariant() : "DEER";
     m.titleLabel.text = $"HUNTED {preyTitle}";
     m.subLabel.text = $"Collect {a.Meat} Raw Meat [F]";
     Color meatCol = new Color(1f, 0.68f, 0.45f);
     m.titleLabel.color = meatCol;
     m.icon.sprite = theme.Kit("g_meat");
     m.icon.color = meatCol;
    }
    worldMarkers[markerIdx] = m;
    markerIdx++;
   }
   for (int i = markerIdx; i < worldMarkers.Count; i++) {
    if (worldMarkers[i].root.gameObject.activeSelf) worldMarkers[i].root.gameObject.SetActive(false);
   }

   // 1. Lock-on reticle
   var locked = h.LockOnTarget;
   if (lockOnReticle) {
    bool showLock = locked && !locked.Dead && locked.gameObject.activeSelf;
    if (lockOnReticle.gameObject.activeSelf != showLock) lockOnReticle.gameObject.SetActive(showLock);
    if (showLock) {
     Vector3 wPos = locked.transform.position + Vector3.up * (locked.CombatHeight * 0.58f);
     Vector3 sp = cam.WorldToScreenPoint(wPos);
     if (sp.z > 0.1f && RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) {
      lockOnReticle.anchoredPosition = lp;
      lockOnReticle.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * 75f);
     } else {
      lockOnReticle.gameObject.SetActive(false);
     }
    }
   }

   // 2. Floating Enemy & Wildlife Health Bars
   int barIdx = 0;
   for (int i = 0; i < gm.Enemies.Count && barIdx < enemyBars.Count; i++) {
    var e = gm.Enemies[i];
    if (!e || e.Dead || !e.gameObject.activeSelf || e.IsBoss) continue;
    float d2 = (e.transform.position - hp).sqrMagnitude;
    bool relevant = e == locked || e.Health < e.MaxHealth - 0.01f || e.Telegraphing || d2 <= 15f * 15f;
    if (!relevant || d2 > 28f * 28f) continue;

    var oldLabel = e.transform.Find("EnemyStatusLabel");
    if (oldLabel && oldLabel.gameObject.activeSelf) oldLabel.gameObject.SetActive(false);

    Vector3 headWorld = e.transform.position + Vector3.up * (e.CombatHeight + 0.32f);
    Vector3 sp = cam.WorldToScreenPoint(headWorld);
    if (sp.z <= 0.1f) continue;
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) continue;

    var bar = enemyBars[barIdx];
    if (!bar.root.gameObject.activeSelf) bar.root.gameObject.SetActive(true);
    bar.group.alpha = 1f;
    bar.root.anchoredPosition = lp;
    string prefix = e == locked ? "◆ " : e.Telegraphing ? " ! " : "";
    bar.nameLabel.text = $"{prefix}{e.DisplayName}";
    bar.nameLabel.color = e == locked ? gold : e.IsBountyElite ? new Color(1f, 0.68f, 0.34f) : OpenWorldUITheme.TextMain;

    float ratio = Mathf.Clamp01(e.Health / Mathf.Max(1f, e.MaxHealth));
    if (bar.target != e) { bar.target = e; bar.trailVal = ratio; }
    else if (ratio < bar.trailVal) bar.trailVal = Mathf.MoveTowards(bar.trailVal, ratio, dt * 0.55f);
    else bar.trailVal = ratio;

    bar.fill.color = new Color(0.95f, 0.28f, 0.24f, 0.96f);
    bar.fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
    bar.trail.rectTransform.anchorMax = new Vector2(bar.trailVal, 1f);
    enemyBars[barIdx] = bar;
    barIdx++;
   }

   // Damaged or close-range Wild Deer health bars
   for (int i = 0; i < cachedAnimals.Length && barIdx < enemyBars.Count; i++) {
    var a = cachedAnimals[i];
    if (!a || !a.isActiveAndEnabled || !a.Deer || a.Dead) continue;
    float d2 = (a.transform.position - hp).sqrMagnitude;
    float deerHp = DeerHealthField != null ? (float)DeerHealthField.GetValue(a) : 3f;
    bool relevant = (deerHp < 2.99f && d2 <= 28f * 28f) || d2 <= 11f * 11f;
    if (!relevant) continue;

    float barOffsetY = a.Meat == 1 ? 0.65f : 1.65f;
    Vector3 headWorld = a.transform.position + Vector3.up * barOffsetY;
    Vector3 sp = cam.WorldToScreenPoint(headWorld);
    if (sp.z <= 0.1f) continue;
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) continue;

    var bar = enemyBars[barIdx];
    if (!bar.root.gameObject.activeSelf) bar.root.gameObject.SetActive(true);
    bar.group.alpha = 1f;
    bar.root.anchoredPosition = lp;
    bar.nameLabel.text = !string.IsNullOrEmpty(a.SpeciesName) ? a.SpeciesName : "Wild Deer";
    bar.nameLabel.color = new Color(1f, 0.86f, 0.62f);

    float ratio = Mathf.Clamp01(deerHp / 3f);
    if (bar.target != a) { bar.target = a; bar.trailVal = ratio; }
    else if (ratio < bar.trailVal) bar.trailVal = Mathf.MoveTowards(bar.trailVal, ratio, dt * 0.55f);
    else bar.trailVal = ratio;

    bar.fill.color = new Color(0.95f, 0.58f, 0.24f, 0.96f);
    bar.fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
    bar.trail.rectTransform.anchorMax = new Vector2(bar.trailVal, 1f);
    enemyBars[barIdx] = bar;
    barIdx++;
   }

   for (int i = barIdx; i < enemyBars.Count; i++) {
    if (enemyBars[i].root.gameObject.activeSelf) enemyBars[i].root.gameObject.SetActive(false);
   }

   // 3. Screen-space Floating Damage Numbers & Combat Callouts
   for (int i = 0; i < screenPopups.Count; i++) {
    var p = screenPopups[i];
    if (!p.root.gameObject.activeSelf) continue;
    p.elapsed += dt;
    if (p.elapsed >= p.duration) {
     p.root.gameObject.SetActive(false);
     screenPopups[i] = p;
     continue;
    }
    Vector3 sp = cam.WorldToScreenPoint(p.worldPos);
    if (sp.z <= 0.1f) { p.group.alpha = 0f; screenPopups[i] = p; continue; }
    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(worldUiRoot, sp, null, out var lp)) {
     float t = p.elapsed / p.duration;
     p.root.anchoredPosition = lp + p.screenVel * p.elapsed + new Vector2(0, 18f * Mathf.Sin(t * Mathf.PI * 0.5f));
     float punch = p.elapsed < 0.12f ? Mathf.Lerp(1.38f, 1f, p.elapsed / 0.12f) : 1f;
     p.root.localScale = Vector3.one * (p.baseScale * punch);
     p.group.alpha = 1f - Mathf.SmoothStep(0.52f, 1f, t);
    }
    screenPopups[i] = p;
   }
  }

  void Update() {
   if (Screen.safeArea != lastSafe || Screen.width != lastWidth || Screen.height != lastHeight) ApplySafeArea();
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) return;
   if (gm.IsModalOpen && titleRoot && titleRoot.activeSelf) titleRoot.SetActive(false);
   var h = gm.Hero;
   float dt = Time.unscaledDeltaTime;

   if (OpenWorldGraphics.Instance && qualityLabel) {
    qualityLabel.text = $"{OpenWorldGraphics.Instance.Label} • {OpenWorldGraphics.Instance.TargetFrameRate} FPS";
   }
   bool titleOpen = titleRoot && titleRoot.activeSelf;
   bool overlayOpen = gm.IsModalOpen || titleOpen;
   controls.SetActive(!overlayOpen && !h.Dead && (Application.isFocused || OpenWorldHero.DebugAlwaysFocused));
   if (heroStatusPanel) heroStatusPanel.SetActive(!overlayOpen);
   if (topRightHudGroup) topRightHudGroup.SetActive(!overlayOpen);

   int elemIdx = Mathf.Clamp(h.ActiveElement, 0, 2);
   Color elemCol = OpenWorldUITheme.ElementColors[elemIdx];
   string elemName = OpenWorldUITheme.ElementNames[elemIdx];
   bool galloping = h.IsMounted && (h.TouchSprintHeld || Input.GetKey(KeyCode.LeftShift));
   string stateTag = h.IsCounterReady ? " • COUNTER!" : h.IsMounted ? (galloping ? " • GALLOPING" : " • MOUNTED") : h.IsGliding ? " • GLIDING" : h.IsClimbing ? " • CLIMBING" : h.IsSwimming ? " • SWIMMING" : h.IsMealBuffActive ? " • BUFFED" : "";

   if (levelBadge) levelBadge.text = $"LV.{gm.AdventurerRank} {gm.AdventurerTitle.ToUpperInvariant()} • {elemName}{stateTag}";
   status.text = $"HP {Mathf.CeilToInt(h.Health)}/{Mathf.CeilToInt(h.MaxHealth)}  •  {gm.Gold}G";
   if (aetherStatus) aetherStatus.text = $"AE {Mathf.FloorToInt(h.Aether)}/{Mathf.FloorToInt(h.MaxAether)}";
   if (goldLabel) goldLabel.text = gm.Gold.ToString();
   if (gemLabel) gemLabel.text = gm.Gems.ToString();
   if (meatLabel) meatLabel.text = gm.RawMeat.ToString();
   if (dashCaptionLabel) dashCaptionLabel.text = h.IsMounted ? "GALLOP" : "DASH";

   if (heroElemDot) {
    heroElemDot.sprite = theme.Kit(OpenWorldUITheme.ElementGlyphs[elemIdx]);
    heroElemDot.color = elemCol;
   }
   if (elementCycleGlyph) {
    elementCycleGlyph.sprite = theme.Kit(OpenWorldUITheme.ElementGlyphs[elemIdx]);
    elementCycleGlyph.color = elemCol;
   }

   float hpRatio = Mathf.Clamp01(h.Health / Mathf.Max(1f, h.MaxHealth));
   if (hpRatio < hpTrailVal) hpTrailVal = Mathf.MoveTowards(hpTrailVal, hpRatio, dt * 0.45f);
   else hpTrailVal = hpRatio;
   health.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
   health.color = hpRatio <= 0.28f ? OpenWorldUITheme.HealthLow : OpenWorldUITheme.Health;
   if (healthTrail) healthTrail.rectTransform.anchorMax = new Vector2(hpTrailVal, 1f);

   aether.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(h.Aether / Mathf.Max(1f, h.MaxAether)), 1f);
   aether.color = Color.Lerp(OpenWorldUITheme.AetherColor, elemCol, 0.35f);
   if (stamina) stamina.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(h.Stamina / Mathf.Max(1f, h.MaxStamina)), 1f);

   // Skill gauges & states (Spell 25 AE, Burst 35 AE, Echo 3.5s CD, Parry counter glow, Lock-on highlight)
   if (spellCostRing) {
    float spellFill = Mathf.Clamp01(h.Aether / 25f);
    spellCostRing.fillAmount = spellFill;
    spellCostRing.color = spellFill >= 1f ? elemCol : new Color(0.45f, 0.5f, 0.6f, 0.55f);
   }
   if (spellCostLabel) spellCostLabel.color = h.Aether >= 25f ? elemCol : OpenWorldUITheme.TextMuted;

   if (burstReadyRing) {
    float burstFill = Mathf.Clamp01(h.Aether / 35f);
    burstReadyRing.fillAmount = burstFill;
    burstReadyRing.color = burstFill >= 1f ? elemCol : new Color(elemCol.r, elemCol.g, elemCol.b, 0.45f);
    if (burstGlowImg) {
     float pulse = burstFill >= 1f ? 0.26f + 0.16f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
     burstGlowImg.color = new Color(elemCol.r, elemCol.g, elemCol.b, pulse);
    }
   }

   if (wasEchoPressed) {
    wasEchoPressed = false;
    if (Time.time >= echoCdUntil) echoCdUntil = Time.time + 3.5f;
   }
   if (echoCdRing) {
    float rem = Mathf.Max(0f, echoCdUntil - Time.time);
    echoCdRing.fillAmount = rem / 3.5f;
    if (echoCdLabel) echoCdLabel.text = rem > 0.05f ? $"{rem:0.0}s" : "";
   }

   if (parryReadyGlow) {
    float a = h.IsCounterReady ? 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
    parryReadyGlow.color = new Color(1f, 0.9f, 0.35f, a);
   }
   if (lockOnGlyph) {
    lockOnGlyph.color = h.LockOnTarget ? new Color(0.38f, 0.98f, 1f) : gold;
   }

   // Boss Health Bar
   var boss = gm.ActiveBoss;
   bool showBoss = boss && !overlayOpen && Vector3.Distance(h.transform.position, boss.transform.position) <= 38f;
   if (bossPanel && bossPanel.activeSelf != showBoss) bossPanel.SetActive(showBoss);
   if (showBoss) {
    bossLabel.text = boss.DisplayName.ToUpperInvariant();
    if (bossSubLabel) bossSubLabel.text = $"PHASE {boss.BossPhase}  •  {Mathf.CeilToInt(boss.Health)}/{Mathf.CeilToInt(boss.MaxHealth)}";
    float bRatio = Mathf.Clamp01(boss.Health / Mathf.Max(1f, boss.MaxHealth));
    if (bRatio < bossTrailVal) bossTrailVal = Mathf.MoveTowards(bossTrailVal, bRatio, dt * 0.4f);
    else bossTrailVal = bRatio;
    bossHealthFill.rectTransform.anchorMax = new Vector2(bRatio, 1f);
    if (bossTrailFill) bossTrailFill.rectTransform.anchorMax = new Vector2(bossTrailVal, 1f);
   }

   // Quest Tracker
   Vector3 toObjective = gm.QuestTarget - h.transform.position;
   toObjective.y = 0;
   int bearing = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(toObjective.x, toObjective.z) * Mathf.Rad2Deg, 360f) / 45f) % 8;
   if (objectiveTitle) objectiveTitle.text = gm.SaveUnavailable ? "SAVE UNAVAILABLE" : gm.QuestHeading;
   if (objectiveDist) objectiveDist.text = gm.QuestCompleted ? "COMPLETE" : $"{toObjective.magnitude:0}m {Bearings[bearing]}";
   objective.text = gm.SaveUnavailable ? "Progress is not being saved • check storage" : gm.QuestDetail;

   // Interact Prompt
   string prompt = gm.InteractionPrompt;
   interact.gameObject.SetActive(!string.IsNullOrEmpty(prompt) && !overlayOpen);
   interactLabel.text = prompt;
   objectivePanel.SetActive(!overlayOpen);
   if (clockButtonObject) clockButtonObject.SetActive(!overlayOpen);

   // Radar Minimap
   if (minimapPanel) {
    minimapPanel.SetActive(!overlayOpen);
    if (!overlayOpen) {
     Vector3 hp = h.transform.position;
     if (minimapRaw) {
      float span = MinimapRadius * 2f / (OpenWorldRegions.WorldHalf * 2f);
      float u = (hp.x + OpenWorldRegions.WorldHalf) / (OpenWorldRegions.WorldHalf * 2f), v = (hp.z + OpenWorldRegions.WorldHalf) / (OpenWorldRegions.WorldHalf * 2f);
      minimapRaw.uvRect = new Rect(u - span * 0.5f, v - span * 0.5f, span, span);
     }
     float camYaw = gm.CameraRig ? gm.CameraRig.Yaw : h.transform.eulerAngles.y;
     if (minimapCone) minimapCone.localEulerAngles = new Vector3(0, 0, -camYaw);
     if (minimapHeroDot) {
      minimapHeroDot.anchoredPosition = minimapRaw ? Vector2.zero : WorldToMapCanvas(hp) * 0.45f;
      minimapHeroDot.localEulerAngles = new Vector3(0, 0, -h.transform.eulerAngles.y);
     }
     if (minimapGoalDot) {
      minimapGoalDot.gameObject.SetActive(!gm.QuestCompleted);
      minimapGoalDot.anchoredPosition = minimapRaw ? LocalBlip(gm.QuestTarget - hp, true) : WorldToMapCanvas(gm.QuestTarget) * 0.45f;
     }
     int eIdx = 0;
     for (int i = 0; i < gm.Enemies.Count && eIdx < minimapEnemyDots.Count; i++) {
      var e = gm.Enemies[i];
      if (!e || e.Dead || !e.gameObject.activeSelf) continue;
      if (minimapRaw && (e.transform.position - hp).sqrMagnitude > MinimapRadius * MinimapRadius) continue;
      minimapEnemyDots[eIdx].gameObject.SetActive(true);
      minimapEnemyDots[eIdx].anchoredPosition = minimapRaw ? LocalBlip(e.transform.position - hp, false) : WorldToMapCanvas(e.transform.position) * 0.45f;
      eIdx++;
     }
     for (; eIdx < minimapEnemyDots.Count; eIdx++) minimapEnemyDots[eIdx].gameObject.SetActive(false);
     if (minimapFooter && OpenWorldAtmosphere.Instance) {
      var atm = OpenWorldAtmosphere.Instance;
      minimapFooter.text = $"{OpenWorldAtmosphere.ClockText(atm.Hour)} • {atm.WeatherLabel}";
     }
    }
   }

   // Top-Center Compass Ribbon
   if (compassPanel) {
    compassPanel.SetActive(!overlayOpen);
    if (!overlayOpen && compassLabel) {
     float camYaw = gm.CameraRig ? gm.CameraRig.Yaw : h.transform.eulerAngles.y;
     int camDir = Mathf.RoundToInt(Mathf.Repeat(camYaw, 360f) / 45f) % 8;
     string leftB = Bearings[(camDir + 7) % 8];
     string midB = Bearings[camDir];
     string rightB = Bearings[(camDir + 1) % 8];
     string biomeName = OpenWorldRealm.Instance ? OpenWorldRegions.BiomeTitle(OpenWorldRegions.DominantBiome(h.transform.position.x, h.transform.position.z)).ToUpperInvariant() : "VERDANT KINGDOM";
     compassLabel.text = $"◄ {leftB}   •   [ {midB} ]   •   {rightB} ►";
     if (compassSubLabel) compassSubLabel.text = $"{biomeName}  •  Relics {gm.RelicsCount}/4  •  Crypt {(gm.CryptCompleted ? "Awakened" : "Sealed")}";
    }
   }

   notice.text = overlayOpen ? "" : gm.CurrentBanner;
   noticePanel.SetActive(!string.IsNullOrEmpty(notice.text));

   UpdateLootFeed(gm, overlayOpen);
   UpdateWorldSpaceUI(gm, h);
   UpdateMenus();
   UpdateClock();
  }

  void OnApplicationFocus(bool focused) {
   if (!focused && !OpenWorldHero.DebugAlwaysFocused && controls) controls.SetActive(false);
  }
  void OnApplicationPause(bool paused) {
   if (paused && controls) controls.SetActive(false);
  }
  void OnDestroy() {
   DestroyClockTextures();
   theme?.Dispose();
   if (mapSprite) Destroy(mapSprite);
   if (mapTexture) Destroy(mapTexture);
   if (Instance == this) Instance = null;
  }
 }

 public sealed class OpenWorldTouchSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
  public bool Look;
  public RectTransform Knob;
  int? pointer;
  public void OnPointerDown(PointerEventData e) {
   if (pointer.HasValue) return;
   pointer = e.pointerId;
   if (!Look) OnDrag(e);
  }
  public void OnDrag(PointerEventData e) {
   if (pointer != e.pointerId) return;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) { ResetInput(); return; }
   if (Look) {
    if (gm.CameraRig) gm.CameraRig.AddLookDelta(e.delta * (180f / Mathf.Max(1, Screen.height)));
   } else {
    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out Vector2 local);
    Vector2 value = Vector2.ClampMagnitude(local / 64f, 1f);
    gm.Hero.TouchMoveInput = value.magnitude < 0.12f ? Vector2.zero : value;
    if (Knob) Knob.anchoredPosition = value * 52f;
   }
  }
  public void OnPointerUp(PointerEventData e) { if (pointer == e.pointerId) ResetInput(); }
  void ResetInput() {
   pointer = null;
   if (!Look && OpenWorldManager.I && OpenWorldManager.I.Hero) OpenWorldManager.I.Hero.TouchMoveInput = Vector2.zero;
   if (Knob) Knob.anchoredPosition = Vector2.zero;
  }
  void OnDisable() { ResetInput(); }
 }

 public sealed class OpenWorldDashButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
  int? pointer;
  public void OnPointerDown(PointerEventData e) {
   if (pointer.HasValue) return;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;
   pointer = e.pointerId;
   gm.Hero.TouchDodge = true;
   gm.Hero.TouchSprintHeld = true;
  }
  public void OnPointerUp(PointerEventData e) {
   if (pointer != e.pointerId) return;
   Release();
  }
  void Release() {
   pointer = null;
   var gm = OpenWorldManager.I;
   if (gm && gm.Hero) gm.Hero.TouchSprintHeld = false;
  }
  void OnDisable() { if (pointer.HasValue) Release(); }
 }

 public sealed class OpenWorldAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
  public Image ChargeRing;
  int? pointer;
  float pressedAt;
  void Update() {
   if (!ChargeRing) return;
   if (pointer.HasValue) {
    ChargeRing.fillAmount = Mathf.Clamp01((Time.unscaledTime - pressedAt) / 0.42f);
   } else if (ChargeRing.fillAmount > 0f) {
    ChargeRing.fillAmount = 0f;
   }
  }
  public void OnPointerDown(PointerEventData e) {
   if (pointer.HasValue) return;
   pointer = e.pointerId; pressedAt = Time.unscaledTime;
  }
  public void OnPointerUp(PointerEventData e) {
   if (pointer != e.pointerId) return;
   pointer = null;
   if (ChargeRing) ChargeRing.fillAmount = 0f;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;
   if (Time.unscaledTime - pressedAt >= 0.42f) gm.Hero.TouchCharged = true;
   else gm.Hero.TouchAttack = true;
  }
  public void OnPointerExit(PointerEventData e) {
   if (pointer == e.pointerId) {
    pointer = null;
    if (ChargeRing) ChargeRing.fillAmount = 0f;
   }
  }
  void OnDisable() {
   pointer = null;
   if (ChargeRing) ChargeRing.fillAmount = 0f;
  }
 }
}

