using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // Uses the original Legends UI textures without changing their import settings.
 // Also generates the modern UI kit at runtime (rounded panels, pills, rings, gradients and line
 // glyphs), so the HUD can look clean and translucent (Genshin / Wuthering Waves / Zelda) while
 // keeping the Legends navy/gold palette and Cinzel/Crimson fonts. Nothing is written to disk.
 public sealed class OpenWorldUITheme {
  readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
  readonly List<Texture2D> generated = new List<Texture2D>();
  public readonly Font Heading = Resources.Load<Font>("UI/Fonts/CinzelDecorative-Bold");
  public readonly Font ButtonFont = Resources.Load<Font>("UI/Fonts/Cinzel-Bold");
  public readonly Font Body = Resources.Load<Font>("UI/Fonts/CrimsonText-Regular");

  // Palette: Legends navy and gold, toned for translucent panels over the world.
  public static readonly Color Ink = new Color(0.035f, 0.06f, 0.11f, 0.62f);
  public static readonly Color InkStrong = new Color(0.03f, 0.05f, 0.09f, 0.9f);
  public static readonly Color Card = new Color(0.07f, 0.11f, 0.18f, 0.88f);
  public static readonly Color CardHover = new Color(0.11f, 0.16f, 0.25f, 0.95f);
  public static readonly Color Gold = new Color(1f, 0.84f, 0.52f);
  public static readonly Color GoldSoft = new Color(0.95f, 0.8f, 0.52f, 0.55f);
  public static readonly Color GoldLine = new Color(0.92f, 0.76f, 0.46f, 0.35f);
  public static readonly Color TextMain = new Color(0.97f, 0.96f, 0.93f);
  public static readonly Color TextDim = new Color(0.72f, 0.77f, 0.84f);
  public static readonly Color TextMuted = new Color(0.52f, 0.57f, 0.64f);
  public static readonly Color Health = new Color(0.5f, 0.9f, 0.42f);
  public static readonly Color HealthLow = new Color(1f, 0.34f, 0.3f);
  public static readonly Color AetherColor = new Color(0.66f, 0.52f, 1f);
  public static readonly Color Danger = new Color(0.95f, 0.3f, 0.26f);
  public static readonly Color[] ElementColors = { new Color(1f, 0.52f, 0.22f), new Color(0.48f, 0.86f, 1f), new Color(0.42f, 1f, 0.76f) };
  public static readonly string[] ElementNames = { "EMBER", "FROST", "GALE" };
  public static readonly string[] ElementGlyphs = { "g_flame", "g_frost", "g_gale" };

  public Sprite Sprite(string path, bool frame = false) {
   string key = path + (frame ? ":frame" : "");
   if (sprites.TryGetValue(key, out var sprite)) return sprite;
   var texture = Resources.Load<Texture2D>("UI/" + path);
   if (!texture) return null;
   var border = frame ? new Vector4(texture.width * .12f, texture.height * .24f, texture.width * .12f, texture.height * .24f) : Vector4.zero;
   sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, border);
   sprite.name = path;
   sprites.Add(key, sprite);
   return sprite;
  }
  public Image Image(GameObject target, string path, bool frame = false) {
   var image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
   image.sprite = Sprite(path, frame);
   image.color = Color.white;
   image.type = frame ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
   image.pixelsPerUnitMultiplier = frame ? 4f : 1f;
   return image;
  }

  // ---- Generated kit ----------------------------------------------------------------------

  const float RoundRadius = 30f; // corner radius (px) of the 64 px "round" sprites

  // Rounded rectangle / pill. radius is in canvas units; a radius of height/2 makes a pill.
  public Image Round(GameObject target, Color color, float radius, bool outline = false) {
   var image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
   image.sprite = Kit(outline ? "roundOutline" : "round");
   image.type = UnityEngine.UI.Image.Type.Sliced;
   image.pixelsPerUnitMultiplier = RoundRadius / Mathf.Max(1f, radius);
   image.color = color;
   return image;
  }

  public Image Shape(GameObject target, string kind, Color color) {
   var image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
   image.sprite = Kit(kind);
   image.type = UnityEngine.UI.Image.Type.Simple;
   image.preserveAspect = kind.StartsWith("g_") || kind == "circle" || kind.StartsWith("ring") || kind.StartsWith("diamond");
   image.color = color;
   return image;
  }

  public Sprite Kit(string kind) {
   if (sprites.TryGetValue("kit:" + kind, out var cached)) return cached;
   Sprite sprite;
   switch (kind) {
    case "round": sprite = BuildRound(false); break;
    case "roundOutline": sprite = BuildRound(true); break;
    case "fadeH": sprite = BuildFade(128, 4, (u, v) => Mathf.Sin(Mathf.PI * u)); break;
    case "fadeR": sprite = BuildFade(128, 4, (u, v) => Mathf.SmoothStep(1f, 0f, u)); break;
    case "fadeL": sprite = BuildFade(128, 4, (u, v) => Mathf.SmoothStep(0f, 1f, u)); break;
    case "fadeUp": sprite = BuildFade(4, 128, (u, v) => Mathf.SmoothStep(1f, 0f, v)); break;
    case "fadeDown": sprite = BuildFade(4, 128, (u, v) => Mathf.SmoothStep(0f, 1f, v)); break;
    case "vignette": sprite = BuildFade(128, 128, (u, v) => {
      float x = u * 2f - 1f, y = v * 2f - 1f;
      return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, Mathf.Sqrt(x * x + y * y)));
     }); break;
    case "glow": sprite = BuildFade(128, 128, (u, v) => {
      float x = u * 2f - 1f, y = v * 2f - 1f;
      float r = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
      return r * r;
     }); break;
    default: sprite = BuildShape(kind); break;
   }
   sprite.name = "OW_UI_" + kind;
   sprites["kit:" + kind] = sprite;
   return sprite;
  }

  Texture2D NewTexture(int w, int h, string name) {
   var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "OW_UI_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
   generated.Add(t);
   return t;
  }

  Sprite BuildRound(bool outline) {
   const int size = 64;
   var tex = NewTexture(size, size, outline ? "RoundOutline" : "Round");
   var px = new Color32[size * size];
   float half = size * 0.5f, r = RoundRadius;
   for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++) {
     float qx = Mathf.Abs(x + 0.5f - half) - (half - r), qy = Mathf.Abs(y + 0.5f - half) - (half - r);
     float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r + 1f;
     float a = Mathf.Clamp01(0.5f - d);
     if (outline) a *= Mathf.Clamp01(d + 3.5f);
     px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
    }
   tex.SetPixels32(px); tex.Apply(false, true);
   float b = RoundRadius + 1f;
   return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
  }

  Sprite BuildFade(int w, int h, Func<float, float, float> alpha) {
   var tex = NewTexture(w, h, "Fade");
   var px = new Color32[w * h];
   for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
     px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h)) * 255f));
   tex.SetPixels32(px); tex.Apply(false, true);
   return UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * .5f, 100);
  }

  // Signed-distance shapes and line glyphs, drawn white so Image.color tints them.
  Sprite BuildShape(string kind) {
   int size = kind.StartsWith("g_") ? 96 : 128;
   var tex = NewTexture(size, size, kind);
   var px = new Color32[size * size];
   float scale = size * 0.5f;
   for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++) {
     var p = new Vector2((x + 0.5f) / scale - 1f, (y + 0.5f) / scale - 1f);
     float d = Distance(kind, p);
     float a = Mathf.Clamp01(0.5f - d * scale);
     px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
    }
   tex.SetPixels32(px); tex.Apply(false, true);
   return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
  }

  static float Distance(string kind, Vector2 p) {
   float ang = Mathf.Atan2(p.y, p.x), len = p.magnitude;
   switch (kind) {
    case "circle": return len - 0.98f;
    case "ring": return Mathf.Abs(len - 0.93f) - 0.05f;
    case "ringThick": return Mathf.Abs(len - 0.88f) - 0.1f;
    case "diamond": return (Mathf.Abs(p.x) + Mathf.Abs(p.y) - 0.92f) * 0.7071f;
    case "diamondOutline": return Mathf.Abs((Mathf.Abs(p.x) + Mathf.Abs(p.y) - 0.84f) * 0.7071f) - 0.06f;
    case "g_pause": return Mathf.Min(Seg(p, new Vector2(-.3f, -.55f), new Vector2(-.3f, .55f), .12f), Seg(p, new Vector2(.3f, -.55f), new Vector2(.3f, .55f), .12f));
    case "g_close": return Mathf.Min(Seg(p, new Vector2(-.55f, -.55f), new Vector2(.55f, .55f), .09f), Seg(p, new Vector2(-.55f, .55f), new Vector2(.55f, -.55f), .09f));
    case "g_lock": {
      float d = Mathf.Abs(len - .5f) - .06f;
      d = Mathf.Min(d, len - .1f);
      d = Mathf.Min(d, Seg(p, new Vector2(0, .62f), new Vector2(0, .92f), .06f));
      d = Mathf.Min(d, Seg(p, new Vector2(0, -.62f), new Vector2(0, -.92f), .06f));
      d = Mathf.Min(d, Seg(p, new Vector2(.62f, 0), new Vector2(.92f, 0), .06f));
      return Mathf.Min(d, Seg(p, new Vector2(-.62f, 0), new Vector2(-.92f, 0), .06f));
     }
    case "g_hook": {
      float d = Seg(p, new Vector2(0, .62f), new Vector2(0, -.18f), .075f);
      d = Mathf.Min(d, Mathf.Abs((p - new Vector2(0, .76f)).magnitude - .14f) - .06f);
      d = Mathf.Min(d, Arc(p, new Vector2(-.3f, -.18f), .3f, .075f, 180f, 360f));
      d = Mathf.Min(d, Arc(p, new Vector2(.3f, -.18f), .3f, .075f, 180f, 360f));
      d = Mathf.Min(d, Seg(p, new Vector2(-.6f, -.18f), new Vector2(-.44f, .04f), .07f));
      return Mathf.Min(d, Seg(p, new Vector2(.6f, -.18f), new Vector2(.44f, .04f), .07f));
     }
    case "g_echo": {
      var c = new Vector2(-.62f, 0f);
      float d = (p - c).magnitude - .15f;
      d = Mathf.Min(d, Arc(p, c, .48f, .07f, -48f, 48f));
      d = Mathf.Min(d, Arc(p, c, .86f, .07f, -44f, 44f));
      return Mathf.Min(d, Arc(p, c, 1.24f, .07f, -38f, 38f));
     }
    case "g_burst": {
      float k = Mathf.Cos(4f * ang);
      float r = .26f + .66f * Mathf.Pow(Mathf.Max(0f, k), 10f) + .36f * Mathf.Pow(Mathf.Max(0f, -k), 10f);
      return (len - r) * 0.6f;
     }
    case "g_flame": {
      float body = (p - new Vector2(0f, -.32f)).magnitude - .44f;
      float tip = Poly(p, new[] { new Vector2(-.4f, -.16f), new Vector2(.42f, -.16f), new Vector2(.08f, .9f) });
      float d = Mathf.Min(body, tip);
      float inner = Mathf.Min((p - new Vector2(.02f, -.42f)).magnitude - .17f,
       Poly(p, new[] { new Vector2(-.15f, -.38f), new Vector2(.17f, -.38f), new Vector2(.04f, .02f) }));
      return Mathf.Max(d, -inner);
     }
    case "g_frost": {
      float d = 9f;
      for (int i = 0; i < 6; i++) {
       float a = i * 60f * Mathf.Deg2Rad + Mathf.PI * 0.5f;
       var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
       d = Mathf.Min(d, Seg(p, Vector2.zero, dir * .86f, .065f));
       var m = dir * .52f;
       var l = new Vector2(Mathf.Cos(a + .75f), Mathf.Sin(a + .75f)) * .26f;
       var r = new Vector2(Mathf.Cos(a - .75f), Mathf.Sin(a - .75f)) * .26f;
       d = Mathf.Min(d, Mathf.Min(Seg(p, m, m + l, .055f), Seg(p, m, m + r, .055f)));
      }
      return d;
     }
    case "g_gale": {
      float d = Seg(p, new Vector2(-.82f, .36f), new Vector2(.22f, .36f), .07f);
      d = Mathf.Min(d, Arc(p, new Vector2(.22f, .56f), .2f, .07f, -90f, 180f));
      d = Mathf.Min(d, Seg(p, new Vector2(-.62f, 0f), new Vector2(.7f, 0f), .07f));
      d = Mathf.Min(d, Seg(p, new Vector2(-.82f, -.36f), new Vector2(.12f, -.36f), .07f));
      return Mathf.Min(d, Arc(p, new Vector2(.12f, -.56f), .2f, .07f, -180f, 90f));
     }
    case "g_sun": {
      float d = len - .3f;
      for (int i = 0; i < 8; i++) {
       float a = i * 45f * Mathf.Deg2Rad;
       var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
       d = Mathf.Min(d, Seg(p, dir * .48f, dir * .78f, .07f));
      }
      return d;
     }
    case "g_arrow": return Poly(p, new[] { new Vector2(0f, .9f), new Vector2(.62f, -.66f), new Vector2(0f, -.32f), new Vector2(-.62f, -.66f) });
    case "g_chevron": return Mathf.Min(Seg(p, new Vector2(-.22f, .52f), new Vector2(.26f, 0f), .11f), Seg(p, new Vector2(.26f, 0f), new Vector2(-.22f, -.52f), .11f));
    case "g_gear": {
      float teeth = Mathf.Clamp(Mathf.Cos(8f * ang) * 3f, -1f, 1f);
      float d = (len - (.66f + .14f * teeth)) * 0.8f;
      return Mathf.Max(d, -(len - .28f));
     }
    case "g_cone": {
      // 90° view cone pointing up, used on the minimap.
      float a = Mathf.Abs(Mathf.DeltaAngle(90f, ang * Mathf.Rad2Deg));
      return a < 45f ? len - .98f : 1f;
     }
    case "g_meat": {
      // Drumstick / hunted meat icon
      float lobe = Mathf.Min((p - new Vector2(-.18f, .18f)).magnitude - .40f,
       Seg(p, new Vector2(-.18f, .18f), new Vector2(.22f, -.20f), .26f));
      float bone = Seg(p, new Vector2(.16f, -.14f), new Vector2(.60f, -.58f), .095f);
      float k1 = (p - new Vector2(.67f, -.50f)).magnitude - .13f;
      float k2 = (p - new Vector2(.52f, -.65f)).magnitude - .13f;
      return Mathf.Min(Mathf.Min(lobe, bone), Mathf.Min(k1, k2));
     }
    case "g_horse": {
      // Horseshoe / mount emblem
      float d = Arc(p, new Vector2(0f, .08f), .54f, .13f, -40f, 220f);
      d = Mathf.Min(d, Seg(p, new Vector2(-.48f, -.28f), new Vector2(-.28f, -.28f), .10f));
      return Mathf.Min(d, Seg(p, new Vector2(.28f, -.28f), new Vector2(.48f, -.28f), .10f));
     }
    default: return 1f;
   }
  }

  static float Seg(Vector2 p, Vector2 a, Vector2 b, float w) {
   Vector2 pa = p - a, ba = b - a;
   float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
   return (pa - ba * h).magnitude - w;
  }

  static float Arc(Vector2 p, Vector2 c, float r, float w, float a0, float a1) {
   var q = p - c;
   float a = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
   float mid = (a0 + a1) * 0.5f, half = (a1 - a0) * 0.5f;
   if (Mathf.Abs(Mathf.DeltaAngle(mid, a)) <= half) return Mathf.Abs(q.magnitude - r) - w;
   var e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
   var e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
   return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - w;
  }

  // Signed distance to a simple polygon (Inigo Quilez).
  static float Poly(Vector2 p, Vector2[] v) {
   float d = Vector2.Dot(p - v[0], p - v[0]);
   float s = 1f;
   for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++) {
    Vector2 e = v[j] - v[i], w = p - v[i];
    Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
    d = Mathf.Min(d, Vector2.Dot(b, b));
    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
   }
   return s * Mathf.Sqrt(d);
  }

  public void Dispose() {
   foreach (var sprite in sprites.Values) if (sprite) UnityEngine.Object.Destroy(sprite);
   sprites.Clear();
   foreach (var t in generated) if (t) UnityEngine.Object.Destroy(t);
   generated.Clear();
  }
 }
}
