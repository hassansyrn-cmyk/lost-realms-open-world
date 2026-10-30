using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Zelda (BotW / TotK) & Genshin-style world-space stamina wheel beside Aster's shoulder:
 // - Appears smoothly when stamina is spent or refilling (sprint, dash, climb, swim, glide)
 // - Outer dark ring with subtle cardinal ticks, white/gold delayed depletion trail ring, and
 //   main radial fill (emerald -> amber -> pulsing crimson when exhausted)
 // - Fades away cleanly once full.
 public sealed class OpenWorldStaminaWheel : MonoBehaviour {
  Image plate, back, trail, fill, rim, glow;
  CanvasGroup group;
  RectTransform rect, parentRect;
  float shownUntil, lastStamina = -1f, trailAmount = 1f;
  static Sprite ringMain, ringThin, ringTicks, softDisc;

  public static OpenWorldStaminaWheel Create(Transform canvasRoot) {
   var go = new GameObject("Stamina Wheel", typeof(RectTransform));
   go.transform.SetParent(canvasRoot, false);
   var w = go.AddComponent<OpenWorldStaminaWheel>();
   w.Build();
   return w;
  }

  void Build() {
   rect = (RectTransform)transform;
   parentRect = transform.parent as RectTransform;
   rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
   rect.sizeDelta = new Vector2(54f, 54f);
   group = gameObject.AddComponent<CanvasGroup>();
   group.alpha = 0f;
   group.blocksRaycasts = false;
   group.interactable = false;

   if (!ringMain) BuildSprites();

   plate = Layer("Plate", softDisc, new Color(0.02f, 0.04f, 0.08f, 0.52f), 1.08f);
   glow = Layer("ExhaustGlow", softDisc, new Color(1f, 0.25f, 0.2f, 0f), 1.32f);
   back = Layer("Track", ringMain, new Color(0.04f, 0.07f, 0.12f, 0.78f), 1f);

   trail = Layer("Trail", ringMain, new Color(1f, 0.92f, 0.68f, 0.65f), 1f);
   trail.type = Image.Type.Filled;
   trail.fillMethod = Image.FillMethod.Radial360;
   trail.fillOrigin = (int)Image.Origin360.Top;
   trail.fillClockwise = false;

   fill = Layer("Fill", ringMain, new Color(0.42f, 0.98f, 0.58f, 0.96f), 1f);
   fill.type = Image.Type.Filled;
   fill.fillMethod = Image.FillMethod.Radial360;
   fill.fillOrigin = (int)Image.Origin360.Top;
   fill.fillClockwise = false;

   rim = Layer("RimTicks", ringTicks, new Color(1f, 0.86f, 0.54f, 0.52f), 1.04f);
  }

  Image Layer(string name, Sprite sprite, Color color, float scale) {
   var go = new GameObject(name, typeof(RectTransform));
   go.transform.SetParent(transform, false);
   var r = (RectTransform)go.transform;
   r.anchorMin = Vector2.zero;
   r.anchorMax = Vector2.one;
   r.offsetMin = r.offsetMax = Vector2.zero;
   r.localScale = Vector3.one * scale;
   var img = go.AddComponent<Image>();
   img.sprite = sprite;
   img.color = color;
   img.raycastTarget = false;
   return img;
  }

  static void BuildSprites() {
   const int size = 128;
   var tMain = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OW_StaminaMain" };
   var tThin = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OW_StaminaThin" };
   var tTicks = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OW_StaminaTicks" };
   var tDisc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OW_StaminaDisc" };

   var pxMain = new Color32[size * size];
   var pxThin = new Color32[size * size];
   var pxTicks = new Color32[size * size];
   var pxDisc = new Color32[size * size];

   float c = (size - 1) * 0.5f;
   float outer = size * 0.45f, inner = size * 0.30f;
   float rimOuter = size * 0.485f, rimInner = size * 0.46f;
   float innerLineO = size * 0.285f, innerLineI = size * 0.265f;

   for (int y = 0; y < size; y++) {
    for (int x = 0; x < size; x++) {
     float dx = x - c, dy = y - c;
     float d = Mathf.Sqrt(dx * dx + dy * dy);
     float ang = Mathf.Atan2(dy, dx);

     // Main gauge band with soft bevel shading
     float aMain = Mathf.Clamp01(outer - d + 0.6f) * Mathf.Clamp01(d - inner + 0.6f);
     float bandT = Mathf.Clamp01((d - inner) / Mathf.Max(1f, outer - inner));
     float bevel = 0.88f + 0.12f * Mathf.Sin(bandT * Mathf.PI);
     byte bVal = (byte)Mathf.RoundToInt(bevel * 255f);
     pxMain[y * size + x] = new Color32(bVal, bVal, bVal, (byte)Mathf.RoundToInt(aMain * 255f));

     // Thin ring
     float aThin = Mathf.Clamp01(rimOuter - d + 0.6f) * Mathf.Clamp01(d - rimInner + 0.6f);
     pxThin[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(aThin * 255f));

     // Outer + inner gold hairline rings + 4 cardinal ticks
     float aInner = Mathf.Clamp01(innerLineO - d + 0.6f) * Mathf.Clamp01(d - innerLineI + 0.6f) * 0.7f;
     float cardinal = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 28f);
     float tickBand = Mathf.Clamp01(rimOuter - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f) * cardinal * 0.45f;
     float aTick = Mathf.Clamp01(Mathf.Max(aThin, Mathf.Max(aInner, tickBand)));
     pxTicks[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(aTick * 255f));

     // Soft backing disc
     float rNorm = d / (size * 0.5f);
     float aDisc = Mathf.Clamp01(1f - Mathf.SmoothStep(0.65f, 1.0f, rNorm));
     pxDisc[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(aDisc * 255f));
    }
   }

   tMain.SetPixels32(pxMain); tMain.Apply(false, true);
   tThin.SetPixels32(pxThin); tThin.Apply(false, true);
   tTicks.SetPixels32(pxTicks); tTicks.Apply(false, true);
   tDisc.SetPixels32(pxDisc); tDisc.Apply(false, true);

   ringMain = Sprite.Create(tMain, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
   ringThin = Sprite.Create(tThin, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
   ringTicks = Sprite.Create(tTicks, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
   softDisc = Sprite.Create(tDisc, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
  }

  void LateUpdate() {
   var gm = OpenWorldManager.I;
   var hero = gm ? gm.Hero : null;
   var cam = Camera.main;
   if (!hero || !cam || !parentRect) { group.alpha = 0f; return; }

   float now = Time.unscaledTime;
   float dt = Time.unscaledDeltaTime;
   float k = Mathf.Clamp01(hero.Stamina / Mathf.Max(1f, hero.MaxStamina));
   bool spending = hero.IsSprinting || hero.IsDashing || hero.IsClimbing || hero.IsSwimming || hero.IsGliding;
   if (spending || k < 0.995f || (lastStamina >= 0f && Mathf.Abs(hero.Stamina - lastStamina) > 0.01f)) {
    shownUntil = now + 1.25f;
   }
   lastStamina = hero.Stamina;

   bool show = now < shownUntil && !hero.Dead && !gm.IsModalOpen;
   group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * (show ? 7f : 2.8f));
   if (group.alpha <= 0.001f) {
    trailAmount = k;
    return;
   }

   // Track beside Aster's right shoulder in screen space.
   Vector3 world = hero.transform.position + Vector3.up * 1.38f + cam.transform.right * 0.76f;
   Vector3 screen = cam.WorldToScreenPoint(world);
   if (screen.z < 0.1f) { group.alpha = 0f; return; }
   if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, null, out var local)) {
    rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, local, Mathf.Clamp01(dt * 22f));
   }

   fill.fillAmount = k;
   if (k < trailAmount) {
    trailAmount = Mathf.MoveTowards(trailAmount, k, dt * 0.55f);
   } else {
    trailAmount = k;
   }
   trail.fillAmount = trailAmount;

   bool exhausted = hero.Stamina <= 0.5f;
   Color c = k > 0.45f
    ? new Color(0.38f, 0.98f, 0.56f)
    : k > 0.22f
     ? new Color(1f, 0.82f, 0.28f)
     : new Color(1f, 0.34f, 0.26f);

   if (exhausted) {
    float pulse = Mathf.PingPong(now * 5f, 1f);
    c = Color.Lerp(new Color(1f, 0.18f, 0.16f), new Color(1f, 0.64f, 0.48f), pulse);
    if (glow) glow.color = new Color(1f, 0.24f, 0.18f, 0.32f * pulse);
   } else if (glow) {
    glow.color = new Color(1f, 0.24f, 0.18f, 0f);
   }

   fill.color = new Color(c.r, c.g, c.b, 0.96f);
  }
 }
}
