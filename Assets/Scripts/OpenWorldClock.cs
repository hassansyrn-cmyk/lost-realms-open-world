using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostRealmsOW {
 // The realm clock, Genshin / Wuthering Waves style: drag the sun or moon around a 24-hour dial (or tap
 // a period), confirm, and the sky fast-forwards to that hour. The dial ring is painted with the sky of
 // each hour; the golden sector shows how far time will advance. "Day cycle: locked" freezes time for
 // testing and screenshots, and the weather can be set here as well. Open with T, the TIME button next
 // to the minimap or the pause menu.
 public sealed partial class OpenWorldMobileHud {
  GameObject clockRoot;
  RectTransform clockHandle, clockSector, clockNowMark;
  Image clockSectorImage, clockHandleImage;
  Text clockBig, clockPeriod, clockNow, clockLockLabel, clockConfirmLabel;
  readonly Text[] clockWeatherLabels = new Text[4];
  readonly Image[] clockWeatherBgs = new Image[4];
  Texture2D clockRingTex, clockSectorTex, clockDiscTex, clockSunTex, clockMoonTex;
  Sprite clockSectorSprite, clockDiscSprite, clockSunSprite, clockMoonSprite;
  float clockSelected = -1f;
  bool clockWasOpen, clockAwaitingFastForward;
  const float ClockRadius = 196f;

  static readonly (string title, float hour)[] ClockPresets = {
   ("DAWN", 6.5f), ("MORNING", 9f), ("NOON", 12f), ("GOLDEN HOUR", 17.25f), ("DUSK", 18.75f), ("NIGHT", 22.5f)
  };

  // Bridge helper: "open" / "close" shows or hides the clock.
  public static void DebugClock(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   gm.CloseMenus();
   gm.ShowClockModal = arg == null || !arg.Trim().Equals("close", StringComparison.OrdinalIgnoreCase);
  }

  void BuildClock() {
   var root = Rect("Realm Clock", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   clockRoot = root.gameObject;
   // Light dim only: the sky stays visible behind the clock while it fast-forwards.
   root.gameObject.AddComponent<Image>().color = new Color(0f, 0.015f, 0.04f, 0.46f);

   var frame = Rect("Clock Frame", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1040, 610));
   theme.Round(frame.gameObject, new Color(0.03f, 0.055f, 0.105f, 0.92f), 22f);
   var frameOutline = Rect("Outline", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   frameOutline.offsetMin = frameOutline.offsetMax = Vector2.zero;
   theme.Round(frameOutline.gameObject, OpenWorldUITheme.GoldSoft, 22f, true).raycastTarget = false;

   var headerLine = Rect("HeaderLine", frame, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(960, 1.5f));
   theme.Shape(headerLine.gameObject, "fadeH", OpenWorldUITheme.GoldSoft).raycastTarget = false;

   var title = Label(frame, "REALM CHRONOMETER", new Vector2(310, -30), new Vector2(540, 34), new Vector2(0, 1), 24);
   title.font = theme.Heading ? theme.Heading : font;
   title.alignment = TextAnchor.MiddleLeft;
   title.color = gold;

   var sub = Label(frame, "DRAG THE CELESTIAL DIAL OR SELECT A TIME & WEATHER PRESET", new Vector2(320, -58), new Vector2(560, 18), new Vector2(0, 1), 11);
   sub.alignment = TextAnchor.MiddleLeft;
   sub.color = OpenWorldUITheme.TextDim;

   var close = Rect("CloseButton", frame, Vector2.one, Vector2.one, new Vector2(-42, -42), new Vector2(44, 44));
   theme.Shape(close.gameObject, "circle", new Color(0.12f, 0.16f, 0.24f, 0.95f));
   var closeRim = Rect("Rim", close, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(44, 44));
   theme.Shape(closeRim.gameObject, "ring", gold).raycastTarget = false;
   var closeGlyph = Rect("X", close, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(18, 18));
   theme.Shape(closeGlyph.gameObject, "g_close", gold).raycastTarget = false;
   var closeBtn = close.gameObject.AddComponent<Button>();
   closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
   closeBtn.onClick.AddListener(() => { var gm = OpenWorldManager.I; if (gm) gm.ShowClockModal = false; OpenWorldAudio.Play("sfx_menu", 0.45f); });

   BuildClockTextures();
   var dial = Rect("Dial", frame, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-240, -28), new Vector2(ClockRadius * 2f, ClockRadius * 2f));
   var outerRim = Rect("OuterGoldRim", dial, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(ClockRadius * 2f + 14f, ClockRadius * 2f + 14f));
   theme.Shape(outerRim.gameObject, "ring", OpenWorldUITheme.GoldSoft).raycastTarget = false;

   var ring = dial.gameObject.AddComponent<RawImage>();
   ring.texture = clockRingTex;
   var input = dial.gameObject.AddComponent<OpenWorldClockDial>();
   input.OnPoint = local => {
    var atm = OpenWorldAtmosphere.Instance;
    if (!atm || atm.FastForwarding || local.magnitude < ClockRadius * 0.3f) return;
    float hour = DirToHour(local);
    clockSelected = Mathf.Round(hour * 12f) / 12f; // 5-minute steps
   };

   clockSector = Rect("Advance", dial, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, dial.sizeDelta);
   clockSectorImage = clockSector.gameObject.AddComponent<Image>();
   clockSectorImage.sprite = clockSectorSprite;
   clockSectorImage.type = Image.Type.Filled;
   clockSectorImage.fillMethod = Image.FillMethod.Radial360;
   clockSectorImage.fillOrigin = (int)Image.Origin360.Bottom;
   clockSectorImage.fillClockwise = true;
   clockSectorImage.color = new Color(1f, 0.84f, 0.44f, 0.44f);
   clockSectorImage.raycastTarget = false;

   var disc = Rect("Face", dial, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, dial.sizeDelta * 0.7f);
   var discImage = disc.gameObject.AddComponent<Image>();
   discImage.sprite = clockDiscSprite;
   discImage.raycastTarget = false;
   var innerRim = Rect("InnerGoldRim", dial, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, dial.sizeDelta * 0.71f);
   theme.Shape(innerRim.gameObject, "ring", OpenWorldUITheme.GoldLine).raycastTarget = false;

   for (int h = 0; h < 24; h += 3) {
    var label = Label(dial, h.ToString("00"), HourDir(h) * ClockRadius * 0.59f, new Vector2(40, 22), Vector2.one * .5f, 15);
    label.color = new Color(0.86f, 0.9f, 0.95f, 0.85f);
   }
   clockBig = Label(dial, "", new Vector2(0, 22), new Vector2(210, 58), Vector2.one * .5f, 46);
   clockBig.font = theme.Heading ? theme.Heading : font;
   clockBig.color = Color.white;
   clockPeriod = Label(dial, "", new Vector2(0, -18), new Vector2(210, 26), Vector2.one * .5f, 18);
   clockPeriod.color = gold;
   clockNow = Label(dial, "", new Vector2(0, -48), new Vector2(220, 24), Vector2.one * .5f, 14);
   clockNow.font = theme.Body ? theme.Body : font;
   clockNow.color = OpenWorldUITheme.TextDim;

   clockNowMark = Rect("Now", dial, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(14, 14));
   var nowImage = clockNowMark.gameObject.AddComponent<Image>();
   nowImage.sprite = clockDiscSprite;
   nowImage.color = Color.white;
   nowImage.raycastTarget = false;
   clockHandle = Rect("Sun", dial, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(68, 68));
   clockHandleImage = clockHandle.gameObject.AddComponent<Image>();
   clockHandleImage.raycastTarget = false;

   // Right column: periods, day cycle lock, weather, confirm (sleek rounded pills).
   var presetHeading = Label(frame, "CELESTIAL PERIODS", new Vector2(246, 184), new Vector2(420, 20), Vector2.one * 0.5f, 12);
   presetHeading.alignment = TextAnchor.MiddleLeft;
   presetHeading.color = OpenWorldUITheme.TextDim;

   for (int i = 0; i < ClockPresets.Length; i++) {
    var preset = ClockPresets[i];
    var b = PillButton(frame, $"{preset.title}  •  {OpenWorldAtmosphere.ClockText(preset.hour)}", new Vector2(142 + (i % 2) * 212, 140 - (i / 2) * 62), new Vector2(198, 48), Vector2.one * .5f, 14);
    b.onClick.AddListener(() => { if (OpenWorldAtmosphere.Instance && !OpenWorldAtmosphere.Instance.FastForwarding) clockSelected = preset.hour; OpenWorldAudio.Play("sfx_menu", .4f); });
   }

   var lockButton = PillButton(frame, "", new Vector2(248, -56), new Vector2(410, 46), Vector2.one * .5f, 15);
   clockLockLabel = lockButton.GetComponentInChildren<Text>();
   lockButton.onClick.AddListener(() => { var atm = OpenWorldAtmosphere.Instance; if (atm) atm.SetTimeLocked(!atm.TimeLocked); OpenWorldAudio.Play("sfx_menu", .4f); });

   var weatherHeading = Label(frame, "ATMOSPHERIC WEATHER", new Vector2(246, -104), new Vector2(420, 20), Vector2.one * 0.5f, 12);
   weatherHeading.alignment = TextAnchor.MiddleLeft;
   weatherHeading.color = OpenWorldUITheme.TextDim;

   string[] weather = { "CLEAR", "RAIN", "STORM", "MIST" };
   for (int i = 0; i < 4; i++) {
    int index = i;
    var w = PillButton(frame, weather[i], new Vector2(92 + i * 104, -142), new Vector2(96, 44), Vector2.one * .5f, 14);
    clockWeatherBgs[i] = w.GetComponent<Image>();
    clockWeatherLabels[i] = w.GetComponentInChildren<Text>();
    w.onClick.AddListener(() => { var atm = OpenWorldAtmosphere.Instance; if (atm) atm.ApplyWeather((OpenWorldAtmosphere.WeatherPreset)index); OpenWorldAudio.Play("sfx_menu", .4f); });
   }

   var confirm = PillButton(frame, "ADVANCE TIME", new Vector2(248, -220), new Vector2(320, 56), Vector2.one * .5f, 18);
   clockConfirmLabel = confirm.GetComponentInChildren<Text>();
   confirm.onClick.AddListener(() => {
    var atm = OpenWorldAtmosphere.Instance;
    if (!atm || atm.FastForwarding) return;
    if (OpenWorldAtmosphere.Wrap24(clockSelected - atm.Hour) < 0.02f) return;
    atm.SetHour(clockSelected, true);
    clockAwaitingFastForward = true;
    OpenWorldAudio.Play("sfx_power_select", .5f);
   });
   clockRoot.SetActive(false);
  }

  void UpdateClock() {
   var gm = OpenWorldManager.I;
   var atm = OpenWorldAtmosphere.Instance;
   bool open = gm && atm && gm.ShowClockModal;
   if (clockRoot.activeSelf != open) clockRoot.SetActive(open);
   if (!open) { clockWasOpen = false; clockAwaitingFastForward = false; return; }
   if (!clockWasOpen) { clockWasOpen = true; clockSelected = Mathf.Round(atm.Hour * 12f) / 12f; clockAwaitingFastForward = false; }
   if (clockAwaitingFastForward && !atm.FastForwarding) {
    clockAwaitingFastForward = false;
    gm.ShowClockModal = false;
    gm.ShowBanner("The realm turns to " + OpenWorldAtmosphere.ClockText(atm.Hour) + " • " + OpenWorldAtmosphere.PeriodName(atm.Hour));
    return;
   }
   float now = atm.Hour;
   float span = OpenWorldAtmosphere.Wrap24(clockSelected - now);
   if (span > 23.98f) span = 0f;
   clockSector.localEulerAngles = new Vector3(0f, 0f, -now / 24f * 360f);
   clockSectorImage.fillAmount = span / 24f;
   clockHandle.anchoredPosition = HourDir(clockSelected) * ClockRadius * 0.855f;
   bool sunUp = OpenWorldAtmosphere.Wrap24(clockSelected) >= 5.9f && OpenWorldAtmosphere.Wrap24(clockSelected) < 19f;
   clockHandleImage.sprite = sunUp ? clockSunSprite : clockMoonSprite;
   clockNowMark.anchoredPosition = HourDir(now) * ClockRadius * 1.02f;
   clockBig.text = OpenWorldAtmosphere.ClockText(clockSelected);
   clockPeriod.text = OpenWorldAtmosphere.PeriodName(clockSelected);
   int spanMinutes = Mathf.RoundToInt(span * 60f);
   clockNow.text = atm.FastForwarding ? "Time is turning…" : spanMinutes == 0 ? "Now " + OpenWorldAtmosphere.ClockText(now) :
    $"Now {OpenWorldAtmosphere.ClockText(now)}  •  +{spanMinutes / 60} h {spanMinutes % 60:00} min";
   clockLockLabel.text = atm.TimeLocked ? "DAY CYCLE: LOCKED" : $"DAY CYCLE: FLOWING ({atm.DayLengthMinutes:0} MIN DAY)";
   clockConfirmLabel.text = atm.FastForwarding ? "TURNING…" : spanMinutes == 0 ? "PICK A TIME" : "ADVANCE TIME";
   for (int i = 0; i < 4; i++) {
    bool active = (int)atm.CurrentWeather == i;
    clockWeatherLabels[i].color = active ? gold : OpenWorldUITheme.TextDim;
    if (clockWeatherBgs[i]) clockWeatherBgs[i].color = active ? new Color(0.16f, 0.24f, 0.38f, 0.98f) : OpenWorldUITheme.Card;
   }
  }

  // 00:00 at the bottom, 06:00 left, 12:00 top, 18:00 right: the sun rises on the left and sets on the right.
  static Vector2 HourDir(float hour) {
   float a = (-90f - OpenWorldAtmosphere.Wrap24(hour) / 24f * 360f) * Mathf.Deg2Rad;
   return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
  }

  static float DirToHour(Vector2 local) {
   float phi = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
   float theta = Mathf.Repeat(-90f - phi, 360f);
   return theta / 360f * 24f;
  }

  void BuildClockTextures() {
   const int size = 256;
   clockRingTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_ClockRing", wrapMode = TextureWrapMode.Clamp };
   clockSectorTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_ClockSector", wrapMode = TextureWrapMode.Clamp };
   clockDiscTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OW_ClockFace", wrapMode = TextureWrapMode.Clamp };
   var ring = new Color[size * size];
   var sector = new Color[size * size];
   var disc = new Color[size * size];
   float c = (size - 1) * 0.5f;
   for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
    var d = new Vector2(x - c, y - c) / c;
    float r = d.magnitude;
    float hour = DirToHour(d);
    float edge = Mathf.Clamp01((0.985f - r) * size * 0.5f) * Mathf.Clamp01((r - 0.72f) * size * 0.5f);
    var sky = OpenWorldAtmosphere.DialColor(hour);
    // Hour ticks on the inner rim, longer every three hours.
    float minuteOfHour = Mathf.Abs(hour - Mathf.Round(hour)) * 60f;
    bool major = Mathf.RoundToInt(hour) % 3 == 0;
    float tickReach = major ? 0.86f : 0.79f;
    if (r < tickReach && minuteOfHour < (major ? 5f : 3.5f)) sky = Color.Lerp(sky, Color.white, 0.75f);
    ring[y * size + x] = new Color(sky.r, sky.g, sky.b, edge);
    sector[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1.0f - r) * size * 0.5f) * Mathf.Clamp01((r - 0.7f) * size * 0.5f));
    float face = Mathf.Clamp01((0.995f - r) * size * 0.5f);
    disc[y * size + x] = new Color(0.03f + 0.05f * (1f - r), 0.06f + 0.06f * (1f - r), 0.1f + 0.08f * (1f - r), face * 0.94f);
   }
   clockRingTex.SetPixels(ring); clockRingTex.Apply(false, true);
   clockSectorTex.SetPixels(sector); clockSectorTex.Apply(false, true);
   clockDiscTex.SetPixels(disc); clockDiscTex.Apply(false, true);
   clockSectorSprite = Sprite.Create(clockSectorTex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
   clockDiscSprite = Sprite.Create(clockDiscTex, new Rect(0, 0, size, size), Vector2.one * 0.5f);

   const int icon = 96;
   clockSunTex = new Texture2D(icon, icon, TextureFormat.RGBA32, false) { name = "OW_ClockSun", wrapMode = TextureWrapMode.Clamp };
   clockMoonTex = new Texture2D(icon, icon, TextureFormat.RGBA32, false) { name = "OW_ClockMoon", wrapMode = TextureWrapMode.Clamp };
   var sun = new Color[icon * icon];
   var moon = new Color[icon * icon];
   float ic = (icon - 1) * 0.5f;
   for (int y = 0; y < icon; y++) for (int x = 0; x < icon; x++) {
    var d = new Vector2(x - ic, y - ic) / ic;
    float r = d.magnitude;
    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(d.y, d.x) * 6f)), 6f) * Mathf.Clamp01(1f - r) * 0.8f;
    float core = Mathf.Clamp01((0.42f - r) * icon * 0.5f);
    float glow = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 0.7f;
    float a = Mathf.Clamp01(core + glow + rays * (r > 0.42f ? 1f : 0f));
    sun[y * icon + x] = new Color(1f, Mathf.Lerp(0.78f, 0.97f, core), Mathf.Lerp(0.35f, 0.82f, core), a);
    float lit = Mathf.Clamp01((0.44f - r) * icon * 0.5f);
    float shadow = Mathf.Clamp01((0.4f - (d - new Vector2(0.2f, 0.16f)).magnitude) * icon * 0.5f);
    float halo = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 0.45f;
    moon[y * icon + x] = new Color(0.86f, 0.92f, 1f, Mathf.Clamp01(lit * (1f - shadow) + halo * (1f - shadow * 0.7f)));
   }
   clockSunTex.SetPixels(sun); clockSunTex.Apply(false, true);
   clockMoonTex.SetPixels(moon); clockMoonTex.Apply(false, true);
   clockSunSprite = Sprite.Create(clockSunTex, new Rect(0, 0, icon, icon), Vector2.one * 0.5f);
   clockMoonSprite = Sprite.Create(clockMoonTex, new Rect(0, 0, icon, icon), Vector2.one * 0.5f);
  }

  void DestroyClockTextures() {
   foreach (var s in new[] { clockSectorSprite, clockDiscSprite, clockSunSprite, clockMoonSprite }) if (s) Destroy(s);
   foreach (var t in new[] { clockRingTex, clockSectorTex, clockDiscTex, clockSunTex, clockMoonTex }) if (t) Destroy(t);
  }
 }

 // Pointer input for the clock dial (press or drag anywhere on the ring).
 public sealed class OpenWorldClockDial : MonoBehaviour, IPointerDownHandler, IDragHandler {
  public Action<Vector2> OnPoint;
  public void OnPointerDown(PointerEventData e) => Point(e);
  public void OnDrag(PointerEventData e) => Point(e);
  void Point(PointerEventData e) {
   var rt = (RectTransform)transform;
   if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) OnPoint?.Invoke(local);
  }
 }
}
