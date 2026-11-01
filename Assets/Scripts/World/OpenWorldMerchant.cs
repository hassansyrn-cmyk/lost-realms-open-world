using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Wandering Trader Fenn (GLM): the open world's first gold sink and source. Fenn's stall stands by the
 // hunter's rest camp; he sells ready-made supplies for gold and buys hunted raw meat. The player has no
 // potion inventory (potions heal instantly on pickup), so the shop sells services and a hot meal backed
 // by exactly the APIs cooking uses (Heal / ActivateMealBuff / RestoreAether / RefillStamina), and pays
 // 8 gold per raw meat — hunts become income and the gold from quests/discoveries/bounties finally
 // matters. Talk to Fenn through the existing NPC interact chain; the uGUI modal follows the quest board
 // pattern (partial of OpenWorldMobileHud) so no HUD file is edited.
 public static class GlmMerchant {
  public const string NpcId = "glm-merchant";
  public const int MeatPrice = 8, MealPrice = 25, DraughtPrice = 15, TonicPrice = 10, HerbPrice = 5, OrePrice = 12;
  // NE of the hunter's rest camp anchor (55, 58) — on the same built plateau, clear of camp props.
  static readonly Vector3 StallSpot = new Vector3(58.6f, 0f, 60.8f);

  // Runs every frame from GlmQuests.Tick (manager Update): spawns the stall once, then only ticks the modal.
  public static void Tick(OpenWorldManager gm) {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   var hud = OpenWorldMobileHud.Instance ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldMobileHud>();
   if (hud) hud.TickMerchantShop(gm);
   if (!gm.NPCs.Exists(n => n && n.NpcId == NpcId)) SpawnStall(gm);
  }

  static void SpawnStall(OpenWorldManager gm) {
   if (!FindSpot(gm, out var spot)) return; // retry on later frames if the ground reads badly
   gm.ReserveSpot(StallSpot, 3.2f);
   var stall = new GameObject("Trader stall");
   stall.transform.SetParent(gm.WorldRoot, false);
   stall.transform.position = spot;
   var awning = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_awning_standing_01a", "Props/Textures/rpgpp_lt_tex_a", stall.transform, spot, 2.9f, Color.white, true);
   if (awning) awning.transform.rotation = Quaternion.Euler(0f, 218f, 0f); // face the camp centre
   var crate = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_crate_01", "Props/Textures/rpgpp_lt_tex_a", stall.transform, spot + new Vector3(1.1f, 0f, .3f), .9f, Color.white, true);
   if (crate) crate.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
   var barrel = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_barrel_01", "Props/Textures/rpgpp_lt_tex_a", stall.transform, spot + new Vector3(-1f, 0f, .4f), .95f, Color.white, true);
   var label = new GameObject("Stall label");
   label.transform.SetParent(stall.transform, false);
   label.transform.localPosition = Vector3.up * 2.6f;
   var text = label.AddComponent<TextMesh>();
   text.text = "TRADER FENN\nsupplies • meat wanted";
   text.anchor = TextAnchor.MiddleCenter; text.characterSize = .06f; text.fontSize = 32;
   text.color = new Color(1f, .84f, .52f);
   var npc = OpenWorldNPC.Spawn(NpcId, "Wandering Trader Fenn", "Travelling Merchant",
    "Coin for meat, supplies for coin — have a look!", 3, -1, spot + Vector3.back * 1.2f, spot + Vector3.back * 1.2f, gm.WorldRoot);
   gm.NPCs.Add(npc);
   Debug.Log("[Merchant] stall + Fenn spawned at " + spot.ToString("0.0"));
  }

  // The camp plateau is flat, but verify before building: try the primary spot, then two fallbacks.
  static bool FindSpot(OpenWorldManager gm, out Vector3 best) {
   Vector3[] candidates = {
    StallSpot,
    StallSpot + new Vector3(-1.4f, 0f, -1.4f),
    StallSpot + new Vector3(1.4f, 0f, -1.4f),
   };
   foreach (var c in candidates) {
    var p = gm.HuntGround(c);
    float lo = p.y, hi = p.y;
    for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) {
     var q = p + new Vector3(x, 0, z) * 1.1f;
     float y = gm.ActiveTerrain.SampleHeight(q) + gm.ActiveTerrain.transform.position.y;
     lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
    }
    if (hi - lo <= 1.1f) { best = new Vector3(p.x, hi + .02f, p.z); return true; }
   }
   best = default;
   return false;
  }
 }

 public sealed partial class OpenWorldManager {
  public bool ShowMerchantModal;

  // Opened from OpenWorldNPC.Converse via the shared interact chain — replaces the free-heal default
  // for this NPC so Fenn's services stay paid.
  public bool GlmMerchantConverse() {
   if (!Hero || Hero.Dead || IsModalOpen) return false;
   ShowMerchantModal = true;
   OpenWorldAudio.Play("sfx_upgrade", .5f);
   return true;
  }

  public void MerchantBuyMeal() {
   if (!MerchantGate(GlmMerchant.MealPrice, "hot meal")) return;
   Hero.Heal(Hero.MaxHealth);
   Hero.ActivateMealBuff(180f);
   ShowBanner("BOUGHT A HOT MEAL • Full HP + meal buff 180 s (−" + GlmMerchant.MealPrice + " gold)");
   RefreshShop();
  }

  public void MerchantBuyDraught() {
   if (!MerchantGate(GlmMerchant.DraughtPrice, "aether draught")) return;
   Hero.RestoreAether(Hero.MaxAether);
   ShowBanner("BOUGHT AN AETHER DRAUGHT • Aether restored (−" + GlmMerchant.DraughtPrice + " gold)");
   RefreshShop();
  }

  public void MerchantBuyTonic() {
   if (!MerchantGate(GlmMerchant.TonicPrice, "stamina tonic")) return;
   Hero.RefillStamina();
   ShowBanner("BOUGHT A STAMINA TONIC • Stamina refilled (−" + GlmMerchant.TonicPrice + " gold)");
   RefreshShop();
  }

  public void MerchantSellMeat(int count) {
   int n = Mathf.Min(count, RawMeat);
   if (n <= 0) { ShowBanner("FENN: \"No meat to sell — the deer are that way, hunter.\""); return; }
   RawMeat -= n;
   AddGold(n * GlmMerchant.MeatPrice);
   ShowBanner("SOLD " + n + " RAW MEAT • +" + (n * GlmMerchant.MeatPrice) + " gold");
   OpenWorldAudio.Play("sfx_upgrade", .45f);
   MarkProgressDirty();
   RefreshShop();
  }

  public void MerchantSellHerbs() {
   if (Herbs <= 0) { ShowBanner("FENN: \"No herbs? The flowers grow right along the paths.\""); return; }
   int earned = Herbs * GlmMerchant.HerbPrice;
   ShowBanner("SOLD " + Herbs + " WILD HERB" + (Herbs > 1 ? "S" : "") + " • +" + earned + " gold");
   Herbs = 0;
   AddGold(earned);
   OpenWorldAudio.Play("sfx_upgrade", .45f);
   MarkProgressDirty();
   RefreshShop();
  }

  public void MerchantSellOre() {
   if (Ore <= 0) { ShowBanner("FENN: \"No ore? The grey veins hide near the rocks — bring a sharp eye.\""); return; }
   int earned = Ore * GlmMerchant.OrePrice;
   ShowBanner("SOLD " + Ore + " ORE • +" + earned + " gold");
   Ore = 0;
   AddGold(earned);
   OpenWorldAudio.Play("sfx_upgrade", .45f);
   MarkProgressDirty();
   RefreshShop();
  }

  bool MerchantGate(int price, string what) {
   if (!Hero || Hero.Dead) return false;
   if (Gold < price) { ShowBanner("FENN: \"No coin, no " + what + ". Sell me some meat first.\""); RefreshShop(); return false; }
   AddGold(-price);
   OpenWorldAudio.Play("sfx_upgrade", .5f);
   return true;
  }

  void RefreshShop() { if (OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.RefreshMerchantShop(this); }
 }

 public static class OpenWorldMerchantDebug {
  // Bridge entry points (static only): seed, open the shop, buy, sell, close.
  public static void Seed(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) { Debug.LogError("[Merchant] world missing"); return; }
   gm.AddGold(150);
   gm.RawMeat = Mathf.Min(999, gm.RawMeat + 6);
   Debug.Log("[Merchant] seeded: gold " + gm.Gold + ", raw meat " + gm.RawMeat);
  }
  public static void Open(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) { Debug.LogError("[Merchant] world missing"); return; }
   gm.ShowMerchantModal = true;
   Debug.Log("[Merchant] converse done, modal=" + gm.ShowMerchantModal);
  }
  public static void BuyMeal(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) return;
   int gold = gm.Gold;
   gm.MerchantBuyMeal();
   Debug.Log("[Merchant] buy meal: gold " + gold + " -> " + gm.Gold + ", hp " + (gm.Hero ? gm.Hero.Health.ToString("0.0") : "?") + "/" + (gm.Hero ? gm.Hero.MaxHealth.ToString("0.0") : "?"));
  }
  public static void SellAll(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) return;
   int gold = gm.Gold, meat = gm.RawMeat;
   gm.MerchantSellMeat(int.MaxValue);
   Debug.Log("[Merchant] sell all: meat " + meat + " -> " + gm.RawMeat + ", gold " + gold + " -> " + gm.Gold);
  }
  public static void SellHerbs(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) return;
   int gold = gm.Gold, herbs = gm.Herbs;
   gm.MerchantSellHerbs();
   Debug.Log("[Merchant] sell herbs: herbs " + herbs + " -> " + gm.Herbs + ", gold " + gold + " -> " + gm.Gold);
  }
  public static void SellOre(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!gm) return;
   int gold = gm.Gold, ore = gm.Ore;
   gm.MerchantSellOre();
   Debug.Log("[Merchant] sell ore: ore " + ore + " -> " + gm.Ore + ", gold " + gold + " -> " + gm.Gold);
  }
  public static void Close(string arg) {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (gm) gm.ShowMerchantModal = false;
  }
 }

 public sealed partial class OpenWorldMobileHud {
  GameObject merchantRoot;
  RectTransform merchantRows;
  Text merchantGold;

  internal void TickMerchantShop(OpenWorldManager gm) {
   if (merchantRoot && merchantRoot.activeSelf != gm.ShowMerchantModal) {
    merchantRoot.SetActive(gm.ShowMerchantModal);
    if (gm.ShowMerchantModal) FillMerchantShop(gm);
   } else if (!merchantRoot && gm.ShowMerchantModal) {
    BuildMerchantShop();
    merchantRoot.SetActive(true);
    FillMerchantShop(gm);
   }
  }

  internal void RefreshMerchantShop(OpenWorldManager gm) {
   if (merchantRoot && merchantRoot.activeSelf) FillMerchantShop(gm);
  }

  void BuildMerchantShop() {
   theme ??= new OpenWorldUITheme();
   var parent = safeRoot != null ? safeRoot : (transform as RectTransform);
   var root = Rect("Merchant Shop", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   merchantRoot = root.gameObject;
   root.gameObject.AddComponent<Image>().color = new Color(0f, 0.015f, 0.04f, 0.65f);

   // Tapping backdrop dismisses modal
   var rootBtn = root.gameObject.AddComponent<Button>();
   rootBtn.transition = Selectable.Transition.None;
   rootBtn.onClick.AddListener(() => {
    var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
    if (gm) { gm.ShowMerchantModal = false; OpenWorldAudio.Play("sfx_click", 0.5f); }
   });

   // Card frame sized to fit comfortably inside 720p canvas
   var frame = Rect("Merchant Frame", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(740, 630));
   theme.Round(frame.gameObject, new Color(0.05f, 0.075f, 0.13f, 0.95f), 20f, true);

   // Title
   var title = Label(frame, "WANDERING TRADER FENN", new Vector2(0, -28), new Vector2(640, 32), new Vector2(.5f, 1f), 24);
   title.font = theme.Heading ? theme.Heading : title.font;
   title.color = OpenWorldUITheme.Gold;

   // Gold / Currency Status Bar
   merchantGold = Label(frame, "", new Vector2(0, -56), new Vector2(640, 22), new Vector2(.5f, 1f), 15);
   merchantGold.color = OpenWorldUITheme.TextMain;

   // Top-Right X Close Button
   var close = Rect("Merchant Close", frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(36, 36));
   theme.Round(close.gameObject, new Color(0.12f, 0.16f, 0.25f, 0.95f), 18f, true);
   var closeLabel = Label(close, "X", Vector2.zero, new Vector2(36, 36), new Vector2(.5f, .5f), 18);
   closeLabel.color = OpenWorldUITheme.Gold;
   var closeButton = close.gameObject.AddComponent<Button>();
   closeButton.transition = Selectable.Transition.ColorTint;
   closeButton.onClick.AddListener(() => {
    var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
    if (gm) { gm.ShowMerchantModal = false; OpenWorldAudio.Play("sfx_click", 0.5f); }
   });

   // Bottom Prominent CLOSE Button
   var btmClose = Rect("Merchant Bottom Close", frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(160, 36));
   theme.Round(btmClose.gameObject, new Color(0.12f, 0.16f, 0.25f, 0.95f), 12f, true);
   var btmCloseLabel = Label(btmClose, "CLOSE", Vector2.zero, new Vector2(160, 36), new Vector2(.5f, .5f), 15);
   btmCloseLabel.font = theme.ButtonFont ? theme.ButtonFont : (theme.Heading ? theme.Heading : btmCloseLabel.font);
   btmCloseLabel.color = OpenWorldUITheme.Gold;
   var btmCloseBtn = btmClose.gameObject.AddComponent<Button>();
   btmCloseBtn.transition = Selectable.Transition.ColorTint;
   btmCloseBtn.onClick.AddListener(() => {
    var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
    if (gm) { gm.ShowMerchantModal = false; OpenWorldAudio.Play("sfx_click", 0.5f); }
   });

   // Viewport + Scroll for merchant rows
   var viewport = Rect("Merchant Viewport", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   viewport.offsetMin = new Vector2(24, 56);
   viewport.offsetMax = new Vector2(-24, -76);
   viewport.gameObject.AddComponent<RectMask2D>();
   var scroll = viewport.gameObject.AddComponent<ScrollRect>();

   merchantRows = Rect("Merchant Rows", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
   merchantRows.pivot = new Vector2(0.5f, 1f);
   scroll.viewport = viewport;
   scroll.content = merchantRows;
   scroll.horizontal = false;
   scroll.vertical = true;
   scroll.movementType = ScrollRect.MovementType.Clamped;
  }

  void FillMerchantShop(OpenWorldManager gm) {
   if (!merchantRows) return;
   for (int i = merchantRows.childCount - 1; i >= 0; i--) Destroy(merchantRows.GetChild(i).gameObject);
   merchantGold.text = "Gold: <color=#FFD54F>" + gm.Gold + " G</color>   •   Meat: <color=#FF8A80>" + gm.RawMeat + "</color>   •   Herbs: <color=#81C784>" + gm.Herbs + "</color>   •   Ore: <color=#90CAF9>" + gm.Ore + "</color>";
   float y = 4f;
   Row("HOT MEAL — " + GlmMerchant.MealPrice + " GOLD", "Full HP + meal buff 180 s. Cooked while you wait.", gm.Gold >= GlmMerchant.MealPrice, gm => gm.MerchantBuyMeal(), ref y);
   Row("AETHER DRAUGHT — " + GlmMerchant.DraughtPrice + " GOLD", "Restores Aether to full. Bottled at the Sunspire springs.", gm.Gold >= GlmMerchant.DraughtPrice, gm => gm.MerchantBuyDraught(), ref y);
   Row("STAMINA TONIC — " + GlmMerchant.TonicPrice + " GOLD", "Refills stamina for sprints and climbs.", gm.Gold >= GlmMerchant.TonicPrice, gm => gm.MerchantBuyTonic(), ref y);
   Row("SELL RAW MEAT (1) — +" + GlmMerchant.MeatPrice + " GOLD", "You carry " + gm.RawMeat + " raw meat.", gm.RawMeat >= 1, gm => gm.MerchantSellMeat(1), ref y);
   Row("SELL ALL RAW MEAT — +" + gm.RawMeat * GlmMerchant.MeatPrice + " GOLD", "Honest prices for honest hunts.", gm.RawMeat >= 1, gm => gm.MerchantSellMeat(int.MaxValue), ref y);
   Row("SELL ALL HERBS — +" + gm.Herbs * GlmMerchant.HerbPrice + " GOLD", "You carry " + gm.Herbs + " wild herbs. Fenn dries them for teas.", gm.Herbs >= 1, gm => gm.MerchantSellHerbs(), ref y);
   Row("SELL ALL ORE — +" + gm.Ore * GlmMerchant.OrePrice + " GOLD", "You carry " + gm.Ore + " ore. The smiths up north pay double — someday.", gm.Ore >= 1, gm => gm.MerchantSellOre(), ref y);
   merchantRows.sizeDelta = new Vector2(0f, y);
  }

  void Row(string title, string desc, bool enabled, System.Action<OpenWorldManager> action, ref float y) {
   // Stretched anchors (insets 0) keep the row inside the frame regardless of pivot/anchor subtleties.
   var row = Rect("Shop row " + title, merchantRows, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 58f));
   row.anchoredPosition = new Vector2(0f, -y - 29f);
   theme.Round(row.gameObject, new Color(0.09f, 0.12f, 0.2f, 0.92f), 12f);
   // Centered label boxes (anchor .5,1): the shared Label helper centers the text box on anchoredPosition,
   // so a top-left anchor would hang half the box outside the row.
   var t = Label(row, title, new Vector2(0f, -17f), new Vector2(640f, 22f), new Vector2(.5f, 1f), 16);
   t.color = enabled ? OpenWorldUITheme.Gold : OpenWorldUITheme.TextMuted;
   t.font = theme.ButtonFont ? theme.ButtonFont : (theme.Heading ? theme.Heading : t.font);
   var d = Label(row, desc, new Vector2(0f, -41f), new Vector2(640f, 20f), new Vector2(.5f, 1f), 12);
   d.color = OpenWorldUITheme.TextDim;
   d.font = theme.Body ? theme.Body : d.font;
   var btn = row.gameObject.AddComponent<Button>();
   btn.transition = Selectable.Transition.ColorTint;
   btn.interactable = enabled;
   btn.onClick.AddListener(() => { if (OpenWorldManager.I) action(OpenWorldManager.I); });
   y += 64f;
  }
 }
}
