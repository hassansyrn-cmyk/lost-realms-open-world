using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Motion review for humanoid clips on the Aster rig, without opening the editor: contact sheets of
 // evenly spaced, time-stamped frames (played exactly as the Phase 1 baker bakes them, see
 // OpenWorldMotionLab.BakeCopy) plus a timing report per clip: length, strike frames, stride speed,
 // left-foot plant, body height offset and the lowest body point. Source packs and baked clips are
 // only read.
 //   call LostRealmsOW.OpenWorldAnimPreview.RenderList ClaudeBridge/anim-candidates.txt
 // List lines: label | asset path (.fbx or .anim) | clip name (optional) | options (optional)
 //   options: start=<s> end=<s> (crop), ref=first|last|mean|idle=<asset path, last option> (height reference),
 //            height (render with the body height offset applied), keepY (legacy: raw RootT.y)
 //   directives: @frames <n>, @tile <px>, @rows <n>
 public static class OpenWorldAnimPreview {
  static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots"));

  sealed class Entry {
   public string label, path, clip, reference = "first";
   public float start, end = -1f;
   public bool keepY, applyHeight;
  }

  static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

  public static void RenderList(string listPath) {
   string full = Path.IsPathRooted(listPath) ? listPath : Path.GetFullPath(Path.Combine(Application.dataPath, "..", listPath));
   if (!File.Exists(full)) { Debug.LogError("[AnimPreview] list missing: " + full); return; }
   int frames = 8, tile = 150, rows = 6;
   var entries = new List<Entry>();
   foreach (var raw in File.ReadAllLines(full)) {
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith("#")) continue;
    if (line.StartsWith("@")) {
     var d = line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
     if (d.Length > 1 && d[0] == "frames") frames = Mathf.Clamp(int.Parse(d[1]), 2, 24);
     if (d.Length > 1 && d[0] == "tile") tile = Mathf.Clamp(int.Parse(d[1]), 64, 256);
     if (d.Length > 1 && d[0] == "rows") rows = Mathf.Clamp(int.Parse(d[1]), 1, 12);
     continue;
    }
    var p = line.Split('|').Select(s => s.Trim()).ToArray();
    var e = new Entry { label = p[0], path = p.Length > 1 ? p[1] : "", clip = p.Length > 2 ? p[2] : "" };
    string opts = p.Length > 3 ? p[3] : "";
    int idleAt = opts.IndexOf("ref=idle=", StringComparison.Ordinal);
    if (idleAt >= 0) { e.reference = "idle=" + opts.Substring(idleAt + 9).Trim(); opts = opts.Substring(0, idleAt); }
    foreach (var o in opts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
     if (o == "keepY") e.keepY = true;
     else if (o == "height") e.applyHeight = true;
     else if (o.StartsWith("start=")) e.start = F(o.Substring(6));
     else if (o.StartsWith("end=")) e.end = F(o.Substring(4));
     else if (o.StartsWith("ref=") && idleAt < 0) e.reference = o.Substring(4);
    }
    entries.Add(e);
   }
   Directory.CreateDirectory(Shots);
   string tag = Path.GetFileNameWithoutExtension(full);
   var report = new StringBuilder();
   report.AppendLine("row | label | length | loop | strikes (s) | stride m/s | left plant | height offset m | lowest m | hips | feet | source");
   using (var probe = new OpenWorldMotionLab.Probe()) {
    var idleRefs = new Dictionary<string, float>();
    for (int page = 0; page * rows < entries.Count; page++) {
     var sheet = new Texture2D(frames * tile, rows * tile, TextureFormat.RGB24, false);
     var fill = new Color32[sheet.width * sheet.height];
     for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(30, 34, 40, 255);
     sheet.SetPixels32(fill);
     for (int row = 0; row < rows; row++) {
      int index = page * rows + row;
      if (index >= entries.Count) break;
      var e = entries[index];
      string line;
      try { line = RenderEntry(probe, e, sheet, rows - 1 - row, frames, tile, idleRefs); } catch (Exception ex) { line = "FAILED " + ex.Message; }
      report.AppendLine($"p{page}r{row} #{index} | {e.label} | {line} | {e.path} {e.clip}");
      DrawText(sheet, 4, (rows - 1 - row) * tile + tile - 18, index.ToString(), Color.yellow, 3);
     }
     sheet.Apply();
     File.WriteAllBytes(Path.Combine(Shots, $"anim_{tag}_{page}.png"), sheet.EncodeToPNG());
     UnityEngine.Object.DestroyImmediate(sheet);
    }
   }
   EditorUtility.UnloadUnusedAssetsImmediate();
   File.WriteAllText(Path.Combine(Shots, $"anim_{tag}.txt"), report.ToString());
   Debug.Log($"[AnimPreview] rendered {entries.Count} clips from {tag}");
  }

  static string RenderEntry(OpenWorldMotionLab.Probe probe, Entry e, Texture2D sheet, int tileRow, int frames, int tile, Dictionary<string, float> idleRefs) {
   var source = OpenWorldMotionLab.LoadClip(e.path, e.clip);
   string takes = "";
   if (e.path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) {
    var names = AssetDatabase.LoadAllAssetsAtPath(e.path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).Select(c => c.name).ToArray();
    if (names.Length > 1) takes = " | takes: " + string.Join(",", names);
   }
   if (!source) return "MISSING clip" + takes;
   if (!source.humanMotion) return "NOT HUMANOID";
   var flat = OpenWorldMotionLab.BakeCopy(source, e.keepY, e.start, e.end);
   var keep = OpenWorldMotionLab.BakeCopy(source, true, e.start, e.end);
   try {
    bool loop = source.isLooping;
    var m = OpenWorldMotionLab.Analyse(probe, flat, keep, loop);
    float reference = m.height[0];
    if (e.reference == "last") reference = m.height[m.height.Length - 1];
    else if (e.reference == "mean") reference = m.height.Average();
    else if (e.reference.StartsWith("idle=")) {
     string idlePath = e.reference.Substring(5);
     if (!idleRefs.TryGetValue(idlePath, out reference)) {
      var idle = OpenWorldMotionLab.LoadClip(idlePath, "");
      reference = idle ? OpenWorldMotionLab.RawHeightMean(probe, idle) : m.height[0];
      idleRefs[idlePath] = reference;
     }
    }
    for (int i = 0; i < m.height.Length; i++) m.height[i] -= reference;

    var pru = probe.Pru;
    var cam = pru.camera;
    cam.orthographic = true;
    cam.orthographicSize = 1.32f;
    cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.3f, 0.35f, 0.42f);
    cam.transform.position = new Vector3(4.2f, 2.1f, 5.2f);
    cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
    pru.lights[0].intensity = 1.25f;
    pru.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
    pru.lights[1].intensity = 0.55f;
    pru.ambientColor = new Color(0.5f, 0.52f, 0.56f);
    float len = Mathf.Max(0.05f, flat.length);
    for (int f = 0; f < frames; f++) {
     float t = loop ? len * f / frames : len * f / (frames - 1);
     t = Mathf.Min(t, len - 0.001f);
     probe.Pose(flat, t);
     float offset = e.applyHeight ? SampleHeight(m.height, t) : 0f;
     probe.Go.transform.position = new Vector3(0f, offset, 0f);
     var tex = probe.RenderFrame(tile);
     probe.Go.transform.position = Vector3.zero;
     var px = tex.GetPixels32();
     int ox = f * tile, oy = tileRow * tile;
     for (int y = 0; y < tile; y++) for (int x = 0; x < tile; x++) sheet.SetPixel(ox + x, oy + y, px[Mathf.Min(px.Length - 1, y * tex.width + x)]);
     var gp = cam.WorldToViewportPoint(Vector3.zero);
     int gy = Mathf.Clamp(Mathf.RoundToInt(gp.y * tile), 0, tile - 1);
     for (int x = 0; x < tile; x += 2) sheet.SetPixel(ox + x, oy + gy, new Color(0.9f, 0.9f, 0.4f));
     for (int y = 0; y < tile; y++) sheet.SetPixel(ox, oy + y, new Color(0.1f, 0.1f, 0.12f));
     DrawText(sheet, ox + 4, oy + 3, t.ToString("0.00", CultureInfo.InvariantCulture), new Color(0.75f, 0.95f, 1f), 2);
     UnityEngine.Object.DestroyImmediate(tex);
    }
    string strikes = m.strikes.Length == 0 ? "-" : string.Join(" ", m.strikes.Select(s => s.ToString("0.00", CultureInfo.InvariantCulture)));
    return $"{len:0.00}s | {(loop ? "loop" : "once")} | {strikes} (RH {m.rhPeakSpeed:0} m/s @{m.rhPeak:0.00}, LH {m.lhPeakSpeed:0} @{m.lhPeak:0.00}) | {m.strideSpeed:0.00} | {m.leftPlant:0.00} | {m.height.Min():0.00}..{m.height.Max():0.00} ({e.reference}) | {m.lowest.Min():0.00}..{m.lowest.Max():0.00} | {m.hipsMin:0.00}..{m.hipsMax:0.00} | {m.feetMin:0.00}..{m.feetMax:0.00}{takes}";
   } finally {
    probe.Forget();
    UnityEngine.Object.DestroyImmediate(flat);
    UnityEngine.Object.DestroyImmediate(keep);
   }
  }

  // Diagnostic: "asset path|clip name" renders the clip through each copy step with a fresh probe and
  // logs hand positions, to find which step changes a pose.
  public static void CompareMethods(string arg) {
   var parts = (arg ?? "").Split('|');
   var source = OpenWorldMotionLab.LoadClip(parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "");
   if (!source) { Debug.LogError("[AnimPreview] compare: clip missing " + arg); return; }
   string[] names = { "raw", "instantiate", "flatten", "settings", "bakecopy" };
   const int frames = 8, tile = 150;
   var sheet = new Texture2D(frames * tile, names.Length * tile, TextureFormat.RGB24, false);
   var log = new StringBuilder();
   for (int m = 0; m < names.Length; m++) {
    AnimationClip clip;
    if (m == 0) clip = source;
    else if (m == 4) clip = OpenWorldMotionLab.BakeCopy(source, false);
    else {
     clip = UnityEngine.Object.Instantiate(source);
     if (m == 2) foreach (var b in AnimationUtility.GetCurveBindings(clip)) if (b.propertyName.StartsWith("RootT", StringComparison.Ordinal)) AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.Constant(0f, clip.length, 0f));
     if (m == 3) {
      var st = AnimationUtility.GetAnimationClipSettings(clip);
      st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
      st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
      st.heightFromFeet = false; st.level = 0f; st.orientationOffsetY = 0f;
      AnimationUtility.SetAnimationClipSettings(clip, st);
     }
    }
    using (var probe = new OpenWorldMotionLab.Probe()) {
     var cam = probe.Pru.camera;
     cam.orthographic = true; cam.orthographicSize = 1.32f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
     cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.35f, 0.42f);
     cam.transform.position = new Vector3(4.2f, 2.1f, 5.2f); cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
     probe.Pru.lights[0].intensity = 1.25f; probe.Pru.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
     probe.Pru.lights[1].intensity = 0.55f; probe.Pru.ambientColor = new Color(0.5f, 0.52f, 0.56f);
     log.Append(names[m]).Append(':');
     for (int f = 0; f < frames; f++) {
      float t = Mathf.Min(clip.length - 0.001f, clip.length * f / (frames - 1));
      probe.Pose(clip, t);
      var rh = probe.Local(probe.RightHand); var lh = probe.Local(probe.LeftHand);
      log.Append($" t{t:0.00} R({rh.x:0.00},{rh.y:0.00},{rh.z:0.00}) L({lh.x:0.00},{lh.y:0.00},{lh.z:0.00})");
      var tex = probe.RenderFrame(tile);
      var px = tex.GetPixels32();
      int oy = (names.Length - 1 - m) * tile;
      for (int y = 0; y < tile; y++) for (int x = 0; x < tile; x++) sheet.SetPixel(f * tile + x, oy + y, px[Mathf.Min(px.Length - 1, y * tex.width + x)]);
      UnityEngine.Object.DestroyImmediate(tex);
     }
     log.AppendLine();
    }
    DrawText(sheet, 4, (names.Length - 1 - m) * tile + tile - 18, m.ToString(), Color.yellow, 3);
    if (m > 0) UnityEngine.Object.DestroyImmediate(clip);
   }
   sheet.Apply();
   Directory.CreateDirectory(Shots);
   File.WriteAllBytes(Path.Combine(Shots, "anim_compare.png"), sheet.EncodeToPNG());
   File.WriteAllText(Path.Combine(Shots, "anim_compare.txt"), arg + "\n" + log);
   UnityEngine.Object.DestroyImmediate(sheet);
   Debug.Log("[AnimPreview] compare written");
  }

  static float SampleHeight(float[] h, float t) {
   float f = t * OpenWorldMotionLab.MetaRate;
   int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, h.Length - 1), j = Mathf.Min(h.Length - 1, i + 1);
   return Mathf.Lerp(h[i], h[j], f - i);
  }

  // 3x5 pixel font (digits, '.', '-'), scaled.
  static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string> {
   { '0', "111101101101111" }, { '1', "010110010010111" }, { '2', "111001111100111" }, { '3', "111001111001111" }, { '4', "101101111001001" },
   { '5', "111100111001111" }, { '6', "111100111101111" }, { '7', "111001001001001" }, { '8', "111101111101111" }, { '9', "111101111001111" },
   { '.', "000000000000010" }, { '-', "000000111000000" }
  };
  static void DrawText(Texture2D tex, int x, int y, string s, Color color, int scale) {
   int cx = x;
   foreach (char c in s) {
    if (!Glyphs.TryGetValue(c, out var pattern)) { cx += 4 * scale; continue; }
    for (int py = 0; py < 5; py++) for (int px = 0; px < 3; px++) {
     if (pattern[py * 3 + px] != '1') continue;
     for (int sy = 0; sy < scale; sy++) for (int sx = 0; sx < scale; sx++) {
      int tx = cx + px * scale + sx, ty = y + (4 - py) * scale + sy;
      if (tx >= 0 && ty >= 0 && tx < tex.width && ty < tex.height) tex.SetPixel(tx, ty, color);
     }
    }
    cx += (c == '.' ? 2 : 4) * scale;
   }
  }
 }
}
