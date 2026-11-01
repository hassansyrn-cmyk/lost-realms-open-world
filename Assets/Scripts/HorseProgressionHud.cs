using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Stable panel (buy / select a horse) and the wild-horse taming overlay.
 //
 // The taming overlay is the visual half of the shrinking-ring challenge in
 // OpenWorldHorseProgression: a ring fades in at a random point, shrinks to nothing, and the
 // player taps it before it vanishes. Hits bloom gold and settle the horse; misses flash red
 // and shake the card; a tamed horse gets a short result card before the saddled owned horse
 // replaces it.
 public sealed partial class OpenWorldMobileHud {
  // While a round is live the horse world marker and the centre-right interact prompt would
  // only tell the player to do what the overlay is already doing, so both are suppressed.
  internal static bool TamingQuiet;

  GameObject horsePanel, tamingRoot, tamingCard, tamingBarRow, tamingCircle, tamingVisual, tamingBurst, tamingFlash;
  Text horseHint, tameTitle, tameSubtitle, tameBottom, staminaHorse, tameFloat;
  Button[] stableBuy;
  Image tameGlow, tameShade, tameOuter, tameRing, tameArc, tameInner, tameCore, tameBurstImg, tameFlashImg, trustFill;
  Image[] missPips, sparks;
  RectTransform tamingRect, tamingCircleRect, tamingBurstRect, tameFloatRect;
  RectTransform[] sparkRects;
  Vector2[] sparkDirs;
  CanvasGroup tamingGroup, ringGroup;

  Vector2 cardBase, sparkOrigin, floatOrigin;
  float roundSeenAt = -1f, trustShown, shake, burstT = 1f, burstSize = 140f, flashAmount, shownAt;
  float ringPop, sparkT = 1f, floatT = 1f;
  Color burstColor = Color.white, flashColor = Color.white, sparkColor = Color.white, floatColor = Color.white;
  int seenBeat, seenHits, seenMisses;
  bool wasCelebrating;

  readonly Color tameGold = new Color(1f, .84f, .52f);
  readonly Color tameDanger = new Color(1f, .34f, .3f);
  readonly Color tameInk = new Color(.028f, .05f, .092f, .95f);

  internal void UpdateHorseProgression() {
   if (!safeRoot) return;
   var p = OpenWorldHorseProgression.Instance;
   var gm = OpenWorldManager.I ? OpenWorldManager.I : UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (!p || !gm || !gm.Hero) return;

   if (!horsePanel) {
    var r = Rect("Horse progression", safeRoot, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 260), new Vector2(620, 225));
    horsePanel = r.gameObject;
    var fill = Rect("Horse panel backing", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    fill.offsetMin = new Vector2(3, 3);
    fill.offsetMax = new Vector2(-3, -3);
    fill.gameObject.AddComponent<Image>().color = new Color(.025f, .045f, .065f, .98f);
    fill.SetAsFirstSibling();
    theme.Round(horsePanel, new Color(.04f, .07f, .10f, .98f), 18, true);
    horseHint = Label(r, "", new Vector2(0, 65), new Vector2(590, 80), Vector2.one * .5f, 20);
    stableBuy = new Button[3];
    for (int i = 0; i < 3; i++) {
     int id = i;
     stableBuy[i] = PillButton(r, OpenWorldHorseProgression.Breeds[i].Name + "\n" + OpenWorldHorseProgression.Breeds[i].Price + " GOLD", new Vector2(-200 + i * 200, -55), new Vector2(190, 70), Vector2.one * .5f, 16);
     stableBuy[i].onClick.AddListener(() => OpenWorldHorseProgression.Instance.Buy(id));
    }
    var close=PillButton(r,"CLOSE",new Vector2(265,90),new Vector2(80,32),Vector2.one*.5f,14);close.onClick.AddListener(()=>OpenWorldHorseProgression.Instance.CloseShop());
    staminaHorse = Label(safeRoot, "", new Vector2(0, 105), new Vector2(430, 30), new Vector2(.5f, 0), 17);
   }

   bool allowed = !gm.Hero.Dead && !gm.IsModalOpen && (!titleRoot || !titleRoot.activeSelf);
   bool taming = p.Target;
   bool stable = allowed && !taming && !gm.Hero.IsMounted && p.ShopOpen;

   horsePanel.SetActive(allowed && stable);
   if (stable) {
    for (int i = 0; i < 3; i++) {
     stableBuy[i].GetComponentInChildren<Text>().text = OpenWorldHorseProgression.Breeds[i].Name + "\n" + (gm.CollectedWorldIds.Contains("horse-owned:" + i) ? "SELECT" : OpenWorldHorseProgression.Breeds[i].Price + " GOLD");
    }
    horseHint.text = "VERDANT STABLES • Handling skill " + p.Skill + "\nRoad: 8.2 speed / 100 stamina • Courser: 11 / 80 • Highlander: 9.2 / 150\nOwned horses can be selected here at no cost";
   }

   staminaHorse.gameObject.SetActive(allowed && gm.Hero.IsMounted);
   if (gm.Hero.IsMounted) staminaHorse.text = gm.Hero.Mount.SpeciesName + " • Stamina " + Mathf.RoundToInt(gm.Hero.Mount.HorseStamina);

   UpdateTamingOverlay(p, taming && allowed);
  }

  // ---------------------------------------------------------------- taming overlay

  // One-line diagnostics for the editor bridge; also useful when a capture looks empty.
  public static string TamingSnapshot() {
   var h = Instance;
   var p = OpenWorldHorseProgression.Instance;
   if (!h || !p) return "[TameUI] missing HUD or progression";
   var r = h.tamingCircle ? h.tamingCircle.GetComponent<RectTransform>() : null;
   return "[TameUI] target=" + (p.Target ? p.Target.name : "null") + " active=" + p.BeatActive
    + " serial=" + p.BeatSerial + " pos=" + p.Beat.Pos.ToString("0.00") + " progress=" + p.BeatProgress.ToString("0.00")
    + " hits=" + p.Successes + " misses=" + p.Mistakes + " state=" + p.TameState
    + " root=" + (h.tamingRoot ? h.tamingRoot.activeSelf.ToString() : "null")
    + " circle=" + (h.tamingCircle ? h.tamingCircle.activeSelf.ToString() : "null")
    + " size=" + (r ? r.sizeDelta.x.ToString("0") : "-")
    + " scale=" + (h.tamingVisual ? h.tamingVisual.transform.localScale.x.ToString("0.00") : "-")
    + " alpha=" + (h.ringGroup ? h.ringGroup.alpha.ToString("0.00") : "-");
  }

  void UpdateTamingOverlay(OpenWorldHorseProgression p, bool visible) {
   TamingQuiet = visible;
   if (visible && !tamingRoot) BuildTamingOverlay();
   if (!tamingRoot) return;
   if (!visible) {
    if (tamingRoot.activeSelf) tamingRoot.SetActive(false);
    return;
   }
   if (!tamingRoot.activeSelf) tamingRoot.SetActive(true);
   tamingRoot.transform.SetAsLastSibling();

   float now = Time.unscaledTime;
   float dt = Time.unscaledDeltaTime;
   bool celebrating = p.TameState == 1;

   // A new round resets every animated value.
   if (roundSeenAt != p.RoundStartedAt) {
    roundSeenAt = p.RoundStartedAt;
    seenBeat = p.BeatSerial;
    seenHits = p.Successes;
    seenMisses = p.Mistakes;
    wasCelebrating = false;
    trustShown = 0f;
    burstT = sparkT = floatT = 1f;
    flashAmount = 0f;
    shake = 0f;
    ringPop = 0f;
    shownAt = now;
   }

   tamingGroup.alpha = Mathf.Clamp01((now - shownAt) / .16f);
   float cardPop = celebrating ? 1f : Mathf.SmoothStep(.94f, 1f, tamingGroup.alpha);

   if (celebrating && !wasCelebrating) {
    Burst(Vector2.zero, tameGold, 210f, 16);
    Float("TAMED", tameGold, Vector2.zero);
    flashColor = new Color(1f, .86f, .55f);
    flashAmount = .17f;
   }
   wasCelebrating = celebrating;

   // Header copy.
   tameTitle.text = celebrating ? "TAMED" : "GENTLE THE WILD";
   tameTitle.color = celebrating ? tameGold : OpenWorldUITheme.TextMain;
   tameTitle.fontSize = tameTitle.resizeTextMaxSize = celebrating ? 32 : 23;
   tameSubtitle.text = p.Target
    ? OpenWorldHorseProgression.Breeds[p.Target.Breed].Name + "  ·  " + (celebrating ? "HANDLING " + p.Skill : "TRUST " + p.Successes + " / " + p.Required + "  ·  HANDLING " + p.Skill)
    : "";
   tameBottom.text = celebrating
    ? "She yields to your hand  •  " + OpenWorldHorseProgression.Breeds[p.Target.Breed].Name + " is yours"
    : "TAP EACH FADING RING BEFORE IT VANISHES   •   THREE MISSES AND SHE BOLTS";
   tamingCard.transform.localScale = Vector3.one * cardPop;
   tamingBarRow.SetActive(!celebrating);

   // Trust bar, eased toward the real value so a hit reads as a soft swell.
   float trust = p.Required > 0 ? Mathf.Clamp01((float)p.Successes / p.Required) : 0f;
   trustShown = Mathf.MoveTowards(trustShown, trust, dt * 1.4f);
   SetFill(trustFill, trustShown);

   for (int i = 0; i < missPips.Length; i++) {
    bool remaining = i < OpenWorldHorseProgression.MistakeLimit - p.Mistakes;
    missPips[i].color = remaining ? new Color(1f, .84f, .52f, .92f) : new Color(.42f, .16f, .16f, .55f);
    missPips[i].rectTransform.localScale = Vector3.one * (remaining ? 1f : .78f);
   }

   // The shrinking ring.
   bool ring = !celebrating && p.BeatActive;
   tamingCircle.SetActive(ring);
   Vector2 field = tamingRect.rect.size;
   Vector2 center = new Vector2((p.Beat.Pos.x - .5f) * field.x, (p.Beat.Pos.y - .5f) * field.y);
   float diameter = Mathf.Max(150f, p.Beat.Diameter + 44f);
   tamingCircleRect.sizeDelta = new Vector2(diameter, diameter);

   if (p.BeatSerial != seenBeat) {
    seenBeat = p.BeatSerial;
    shake = Mathf.Max(shake, .26f); // the horse shies as a new ring appears
    ringPop = 1f;
   }
   ringPop = Mathf.MoveTowards(ringPop, 0f, dt * 5f);

   if (ring) {
    float t = p.BeatProgress;
    float pop = Mathf.SmoothStep(.55f, 1f, Mathf.Clamp01(t / .16f));
    float breathe = 1f + .018f * Mathf.Sin(now * 18f);
    float scale = Mathf.Lerp(1f, .15f, t) * pop * breathe * (1f + ringPop * .12f);
    tamingVisual.transform.localScale = Vector3.one * scale;
    // Mathf.SmoothStep does not remap its edges, so the last-tenth fade needs a normalised t
    // (passing raw t faded the ring to almost nothing halfway through its life).
    ringGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.90f, 1f, t));

    float urgency = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, t));
    Color ringColor = Color.Lerp(tameGold, tameDanger, urgency);
    tameRing.color = ringColor;
    tameOuter.color = new Color(ringColor.r, ringColor.g, ringColor.b, .26f);
    tameArc.color = new Color(1f, .92f, .76f, .92f - .34f * urgency);
    tameArc.fillAmount = 1f - t;
    tameInner.color = new Color(1f, .96f, .87f, .34f + .30f * (1f - t));
    tameCore.color = new Color(1f, .95f, .84f, .34f + .52f * Mathf.Pow(1f - t, 1.4f));
    tameGlow.color = new Color(1f, .78f, .42f, .30f * (1f - urgency * .5f));
    tameShade.color = new Color(.008f, .022f, .05f, .52f);
   }

   // Hits, misses and the finish, all driven by the counters so a tap is acknowledged the
   // moment it lands.
   if (p.Successes > seenHits) {
    seenHits = p.Successes;
    if (!celebrating) { // the finish has its own, larger bloom and the TAMED callout
     Burst(center, tameGold, 130f, 12);
     Float("+1 TRUST", tameGold, center + Vector2.up * 26f);
     flashColor = new Color(1f, .88f, .6f);
     flashAmount = Mathf.Max(flashAmount, .05f);
    }
   }
   if (p.Mistakes > seenMisses) {
    seenMisses = p.Mistakes;
    Burst(center, tameDanger, 150f, 12);
    Float("MISSED", tameDanger, center + Vector2.up * 26f);
    shake = .9f;
    flashColor = new Color(.9f, .22f, .2f);
    flashAmount = Mathf.Max(flashAmount, .12f);
   }

   // Feedback animation.
   shake = Mathf.MoveTowards(shake, 0f, dt * 5.5f);
   Vector2 jitter = new Vector2(UnityEngine.Random.value * 2f - 1f, UnityEngine.Random.value * 2f - 1f) * (shake * 7f);
   tamingCard.GetComponent<RectTransform>().anchoredPosition = cardBase + jitter;
   tamingCircleRect.anchoredPosition = center + jitter;

   AnimateBurst();
   AnimateSparks(dt);
   AnimateFloat(dt);

   flashAmount = Mathf.MoveTowards(flashAmount, 0f, dt * .85f);
   tameFlashImg.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAmount);
   tamingFlash.SetActive(flashAmount > .002f);
  }

  static void SetFill(Image image, float amount) {
   var r = image.rectTransform;
   r.anchorMin = Vector2.zero;
   r.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
   r.offsetMin = Vector2.zero;
   r.offsetMax = Vector2.zero;
  }

  void Burst(Vector2 position, Color color, float size, int count) {
   burstColor = color;
   burstSize = size;
   burstT = 0f;
   tamingBurstRect.anchoredPosition = position;
   sparkColor = color;
   sparkOrigin = position;
   sparkT = 0f;
   for (int i = 0; i < sparks.Length; i++) {
    float a = Mathf.PI * 2f * i / sparks.Length + UnityEngine.Random.value * .6f;
    sparkDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(.7f, 1.25f);
    sparkRects[i].sizeDelta = Vector2.one * Mathf.Lerp(11f, 5f, (float)i / sparks.Length);
   }
  }

  void Float(string text, Color color, Vector2 at) {
   tameFloat.text = text;
   floatColor = color;
   floatOrigin = at;
   floatT = 0f;
   tameFloatRect.anchoredPosition = at;
   tameFloat.gameObject.SetActive(true);
  }

  void AnimateBurst() {
   if (burstT < 1f) {
    burstT = Mathf.Min(1f, burstT + Time.unscaledDeltaTime / .52f);
    float e = 1f - Mathf.Pow(1f - burstT, 3f);
    tamingBurstRect.sizeDelta = Vector2.one * Mathf.Lerp(burstSize, burstSize * 2.7f, e);
    tameBurstImg.color = new Color(burstColor.r, burstColor.g, burstColor.b, Mathf.Pow(1f - burstT, 2f) * .9f);
    tamingBurst.SetActive(true);
   } else if (tamingBurst.activeSelf) {
    tamingBurst.SetActive(false);
   }
  }

  void AnimateSparks(float dt) {
   if (sparkT >= 1f) {
    for (int i = 0; i < sparks.Length; i++) if (sparks[i].gameObject.activeSelf) sparks[i].gameObject.SetActive(false);
    return;
   }
   sparkT = Mathf.Min(1f, sparkT + dt / .58f);
   float e = 1f - Mathf.Pow(1f - sparkT, 3f);
   float alpha = Mathf.Pow(1f - sparkT, 1.4f);
   for (int i = 0; i < sparks.Length; i++) {
    var rect = sparkRects[i];
    Vector2 drift = sparkDirs[i] * Mathf.Lerp(10f, 92f, e) + Vector2.up * (e * e * 16f);
    rect.anchoredPosition = sparkOrigin + drift;
    rect.sizeDelta *= .995f;
    sparks[i].color = new Color(sparkColor.r, sparkColor.g, sparkColor.b, alpha);
    sparks[i].gameObject.SetActive(alpha > .015f);
   }
  }

  void AnimateFloat(float dt) {
   if (floatT >= 1f) {
    if (tameFloat != null && tameFloat.gameObject.activeSelf) tameFloat.gameObject.SetActive(false);
    return;
   }
   floatT = Mathf.Min(1f, floatT + dt / 1.15f);
   float e = 1f - Mathf.Pow(1f - floatT, 2f);
   tameFloatRect.anchoredPosition = floatOrigin + Vector2.up * Mathf.Lerp(8f, 54f, e);
   tameFloat.color = new Color(floatColor.r, floatColor.g, floatColor.b, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, floatT)));
   tameFloatRect.localScale = Vector3.one * (1f + .07f * Mathf.Sin(floatT * Mathf.PI));
   tameFloat.gameObject.SetActive(true);
  }

  void BuildTamingOverlay() {
   var root = Rect("Taming overlay", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   tamingRoot = root.gameObject;
   tamingRect = root;
   tamingGroup = tamingRoot.AddComponent<CanvasGroup>();

   // Dimmed veil: it swallows touches so the stick and action buttons stay out of the way, but
   // it stays light enough that the horse's rearing and stamping still reads underneath.
   var veil = Rect("Veil", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   veil.offsetMin = veil.offsetMax = Vector2.zero;
   var veilImg = veil.gameObject.AddComponent<Image>();
   veilImg.color = new Color(.006f, .016f, .034f, .40f);
   veilImg.raycastTarget = true;

   var vig = Rect("Vignette", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   vig.offsetMin = vig.offsetMax = Vector2.zero;
   theme.Shape(vig.gameObject, "vignette", new Color(.004f, .012f, .032f, .85f)).raycastTarget = false;

   tamingFlash = Rect("Taming flash", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
   tamingFlash.GetComponent<RectTransform>().offsetMin = tamingFlash.GetComponent<RectTransform>().offsetMax = Vector2.zero;
   tameFlashImg = tamingFlash.AddComponent<Image>();
   tameFlashImg.raycastTarget = false;
   tameFlashImg.color = Color.clear;
   tamingFlash.SetActive(false);

   // Header card.
   var card = Rect("Taming card", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(640, 90));
   tamingCard = card.gameObject;
   cardBase = card.anchoredPosition;
   theme.Round(tamingCard, tameInk, 18f);
   var cardRim = Rect("Rim", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   cardRim.offsetMin = new Vector2(1.5f, 1.5f);
   cardRim.offsetMax = new Vector2(-1.5f, -1.5f);
   theme.Round(cardRim.gameObject, OpenWorldUITheme.GoldSoft, 17f, true).raycastTarget = false;

   tameTitle = Label(card, "GENTLE THE WILD", new Vector2(0, 20), new Vector2(600, 28), Vector2.one * .5f, 23);
   tameTitle.font = theme.Heading ? theme.Heading : font;
   tameTitle.color = OpenWorldUITheme.TextMain;
   AddShadow(tameTitle.gameObject, new Color(0, 0, 0, .9f), new Vector2(1.5f, -1.5f));
   tameSubtitle = Label(card, "", new Vector2(0, -9), new Vector2(600, 18), Vector2.one * .5f, 14);
   tameSubtitle.color = tameGold;
   var rule = Rect("Rule", card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -29), new Vector2(250, 1));
   var ruleImg = rule.gameObject.AddComponent<Image>();
   ruleImg.color = new Color(1f, .84f, .52f, .26f);
   ruleImg.raycastTarget = false;
   var gem = Rect("Rule gem", card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -29), new Vector2(7, 7));
   theme.Shape(gem.gameObject, "diamond", new Color(1f, .84f, .52f, .65f)).raycastTarget = false;

   // Trust strip: one centred piece of furniture rather than labels scattered across the screen.
   var row = Rect("Trust row", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -118), new Vector2(600, 30));
   tamingBarRow = row.gameObject;
   theme.Round(tamingBarRow, new Color(.02f, .038f, .07f, .92f), 15f);
   var rowRim = Rect("Rim", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   rowRim.offsetMin = new Vector2(1, 1);
   rowRim.offsetMax = new Vector2(-1, -1);
   theme.Round(rowRim.gameObject, OpenWorldUITheme.GoldSoft, 14f, true).raycastTarget = false;

   var trustLabel = Label(row, "TRUST", new Vector2(-250, 0), new Vector2(58, 16), Vector2.one * .5f, 11);
   trustLabel.color = OpenWorldUITheme.TextMuted;
   trustLabel.alignment = TextAnchor.MiddleRight;

   var trustBarRect = Rect("Trust bar", row, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-40, 0), new Vector2(300, 8));
   theme.Round(trustBarRect.gameObject, new Color(.012f, .025f, .05f, .98f), 4f).raycastTarget = false;
   var barRim = Rect("Bar rim", trustBarRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   barRim.offsetMin = new Vector2(-1, -1);
   barRim.offsetMax = new Vector2(1, 1);
   theme.Round(barRim.gameObject, new Color(1f, .84f, .52f, .18f), 4f, true).raycastTarget = false;

   var fill = Rect("Fill", trustBarRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   fill.offsetMin = fill.offsetMax = Vector2.zero;
   trustFill = theme.Round(fill.gameObject, tameGold, 4f);
   trustFill.raycastTarget = false;
   SetFill(trustFill, 0f);

   missPips = new Image[OpenWorldHorseProgression.MistakeLimit];
   for (int i = 0; i < missPips.Length; i++) {
    var pip = Rect("Miss pip " + i, row, Vector2.one * .5f, Vector2.one * .5f, new Vector2(142 + i * 22, 0), new Vector2(13, 13));
    missPips[i] = theme.Shape(pip.gameObject, "diamond", tameGold);
    missPips[i].raycastTarget = false;
   }

   var pipLabel = Label(row, "MISSES", new Vector2(240, 0), new Vector2(60, 16), Vector2.one * .5f, 10);
   pipLabel.color = OpenWorldUITheme.TextMuted;

   tameBottom = Label(root, "", new Vector2(0, 150), new Vector2(940, 20), new Vector2(.5f, 0), 12);
   tameBottom.color = new Color(.86f, .82f, .72f, .92f);
   AddShadow(tameBottom.gameObject, new Color(0, 0, 0, .95f), new Vector2(1, -1));

   // The ring: a fixed touch area whose visible child shrinks to nothing. The dark shade disc
   // keeps the gold readable over bright grass; the thin outer arc is the time remaining.
   tamingCircle = Rect("Tame ring", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(260, 260)).gameObject;
   tamingCircleRect = tamingCircle.GetComponent<RectTransform>();
   var hit = tamingCircle.AddComponent<Image>();
   hit.color = new Color(1f, 1f, 1f, 0f);
   hit.raycastTarget = true;
   // Touch-down, not release: a press that lands before the ring vanishes counts even if the
   // finger lifts a moment later. The press is rejected outside the visible disc.
   tamingCircle.AddComponent<TameRingPress>();

   tamingVisual = Rect("Visual", tamingCircleRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
   tamingVisual.GetComponent<RectTransform>().offsetMin = tamingVisual.GetComponent<RectTransform>().offsetMax = Vector2.zero;
   ringGroup = tamingVisual.AddComponent<CanvasGroup>();
   ringGroup.blocksRaycasts = false;
   ringGroup.interactable = false;

   var shade = Rect("Shade", tamingVisual.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   shade.offsetMin = new Vector2(-14, -14);
   shade.offsetMax = new Vector2(14, 14);
   tameShade = theme.Shape(shade.gameObject, "circle", new Color(.008f, .022f, .05f, .34f));
   tameShade.raycastTarget = false;

   var glow = Rect("Glow", tamingVisual.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   glow.offsetMin = new Vector2(-34, -34);
   glow.offsetMax = new Vector2(34, 34);
   tameGlow = theme.Shape(glow.gameObject, "glow", new Color(1f, .78f, .42f, .30f));
   tameGlow.raycastTarget = false;

   var arc = Rect("Countdown arc", tamingVisual.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   arc.offsetMin = new Vector2(-10, -10);
   arc.offsetMax = new Vector2(10, 10);
   tameArc = theme.Shape(arc.gameObject, "ring", new Color(1f, .92f, .76f, .92f));
   tameArc.type = Image.Type.Filled;
   tameArc.fillMethod = Image.FillMethod.Radial360;
   tameArc.fillOrigin = (int)Image.Origin360.Top;
   tameArc.fillClockwise = true;
   tameArc.fillAmount = 1f;
   tameArc.raycastTarget = false;

   var outer = Rect("Outer ring", tamingVisual.transform, new Vector2(.02f, .02f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
   tameOuter = theme.Shape(outer.gameObject, "ring", new Color(1f, .84f, .52f, .26f));
   tameOuter.raycastTarget = false;

   var ringRect = Rect("Ring", tamingVisual.transform, new Vector2(.09f, .09f), new Vector2(.91f, .91f), Vector2.zero, Vector2.zero);
   tameRing = theme.Shape(ringRect.gameObject, "ringThick", tameGold);
   tameRing.raycastTarget = false;

   var inner = Rect("Inner ring", tamingVisual.transform, new Vector2(.26f, .26f), new Vector2(.74f, .74f), Vector2.zero, Vector2.zero);
   tameInner = theme.Shape(inner.gameObject, "ring", new Color(1f, .96f, .87f, .5f));
   tameInner.raycastTarget = false;

   var core = Rect("Core", tamingVisual.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(54, 54));
   tameCore = theme.Shape(core.gameObject, "circle", new Color(1f, .95f, .84f, .4f));
   tameCore.raycastTarget = false;

   // Compass ticks just inside the edge, so the ring reads as an object even while tiny.
   for (int i = 0; i < 4; i++) {
    Vector2 anchor = i == 0 ? new Vector2(.5f, 1) : i == 1 ? new Vector2(1, .5f) : i == 2 ? new Vector2(.5f, 0) : new Vector2(0, .5f);
    Vector2 at = i == 0 ? new Vector2(0, -9) : i == 1 ? new Vector2(-9, 0) : i == 2 ? new Vector2(0, 9) : new Vector2(9, 0);
    var tick = Rect("Tick " + i, tamingVisual.transform, anchor, anchor, at, new Vector2(8, 8));
    theme.Shape(tick.gameObject, "diamond", new Color(1f, .9f, .68f, .55f)).raycastTarget = false;
   }

   tamingCircle.SetActive(false);

   // Shared bloom used for hits, misses and the tamable moment.
   var burst = Rect("Tame burst", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(150, 150));
   tamingBurst = burst.gameObject;
   tamingBurstRect = burst;
   tameBurstImg = theme.Shape(tamingBurst, "ringThick", tameGold);
   tameBurstImg.raycastTarget = false;
   tamingBurst.SetActive(false);

   sparks = new Image[12];
   sparkRects = new RectTransform[sparks.Length];
   sparkDirs = new Vector2[sparks.Length];
   for (int i = 0; i < sparks.Length; i++) {
    var spark = Rect("Spark " + i, root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(9, 9));
    sparkRects[i] = spark;
    sparks[i] = theme.Shape(spark.gameObject, "circle", tameGold);
    sparks[i].raycastTarget = false;
    spark.gameObject.SetActive(false);
   }

   tameFloat = Label(root, "", Vector2.zero, new Vector2(300, 28), Vector2.one * .5f, 18);
   tameFloat.font = theme.Heading ? theme.Heading : font;
   tameFloat.color = tameGold;
   AddShadow(tameFloat.gameObject, new Color(0, 0, 0, .95f), new Vector2(1.5f, -1.5f));
   tameFloatRect = tameFloat.rectTransform;
   tameFloat.gameObject.SetActive(false);

   tamingRoot.SetActive(false);
  }
 }

 public sealed class TameRingPress : MonoBehaviour, IPointerDownHandler {
  public void OnPointerDown(PointerEventData eventData) {
   var rect = (RectTransform)transform;
   if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return;
   float radius = rect.rect.width * .5f;
   if (local.sqrMagnitude > radius * radius) return;
   if (OpenWorldHorseProgression.Instance) OpenWorldHorseProgression.Instance.Tap();
  }
 }

 public sealed class HorseProgressionHudDriver : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { new GameObject("Horse progression HUD").AddComponent<HorseProgressionHudDriver>(); }
  void LateUpdate() { if (OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.UpdateHorseProgression(); }
 }
}
