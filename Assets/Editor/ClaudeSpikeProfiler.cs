using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace LostRealmsOW {
 // Attributes frame spikes (Claude, stutter investigation 30 Oct 2026). Records the editor profiler while a
 // bridge job plays a scenario, then walks the main-thread hierarchy of every slow frame and writes where the
 // time went. Bridge use:
 //  call LostRealmsOW.ClaudeSpikeProfiler.Begin frozen_lake
 //  ... tp / ride / wait ...
 //  call LostRealmsOW.ClaudeSpikeProfiler.End frozen_lake
 // Output: ClaudeBridge/out/spikes_<tag>.txt (frames over 45 ms: top-level split, the most expensive path and
 // the largest self-time markers; for GPU-wait frames also the render thread). Editor-only diagnostics.
 //  call LostRealmsOW.ClaudeSpikeProfiler.LoadCosts DownloadedActors,Imported_Frozen   (edit mode)
 // times a cold load of every prefab in those Addressables groups and lists what it pulls in
 // (ClaudeBridge/out/loadcosts.txt).
 public static class ClaudeSpikeProfiler {
  const float SpikeMs = 45f;

  public static void Begin(string tag) {
   ProfilerDriver.ClearAllFrames();
   ProfilerDriver.profileEditor = false;
   ProfilerDriver.enabled = true;
   Profiler.enabled = true;
   Debug.Log("[Spike] recording " + tag);
  }

  public static void End(string tag) {
   ProfilerDriver.enabled = false;
   int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
   var sb = new StringBuilder();
   sb.AppendLine("Spike capture '" + tag + "' — frames " + first + ".." + last + " — " + System.DateTime.Now.ToString("HH:mm:ss"));
   int frames = 0, slow = 0; float sum = 0f, worst = 0f;
   var spikeReports = new List<(float ms, string text)>();
   for (int f = first; f <= last && f >= 0; f++) {
    using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false)) {
     if (view == null || !view.valid) continue;
     float ms = view.frameTimeMs;
     frames++; sum += ms; worst = Mathf.Max(worst, ms);
     if (ms < SpikeMs) continue;
     slow++;
     string text = Describe(view, f, ms);
     if (text.Contains("WaitForLastPresentation") || text.Contains("WaitForRenderThread") || text.Contains("WaitForPresent")) text += OtherThreads(f);
     spikeReports.Add((ms, text));
    }
   }
   sb.AppendLine($"{frames} frames, mean {(frames > 0 ? sum / frames : 0f):0.0} ms, worst {worst:0.0} ms, {slow} over {SpikeMs:0} ms");
   foreach (var r in spikeReports.OrderByDescending(r => r.ms).Take(8)) sb.Append(r.text);
   string path = Path.Combine("ClaudeBridge", "out", "spikes_" + tag + ".txt");
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   File.WriteAllText(path, sb.ToString());
   Debug.Log("[Spike] " + tag + ": " + frames + " frames, worst " + worst.ToString("0.0", CultureInfo.InvariantCulture) + " ms, " + slow + " slow; see " + path);
  }

  static string Describe(HierarchyFrameDataView view, int frame, float ms) {
   var sb = new StringBuilder();
   sb.AppendLine();
   sb.AppendLine($"== frame {frame}: {ms:0.0} ms");
   int root = view.GetRootItemID();
   var kids = new List<int>();
   view.GetItemChildren(root, kids);
   sb.AppendLine("  top level: " + string.Join(", ", kids.OrderByDescending(k => Total(view, k)).Take(6).Select(k => view.GetItemName(k) + " " + Total(view, k).ToString("0.0", CultureInfo.InvariantCulture))));
   // Follow the most expensive child down while it holds most of its parent's time.
   var path = new List<string>();
   int item = kids.Count > 0 ? kids.OrderByDescending(k => Total(view, k)).First() : -1;
   for (int depth = 0; item >= 0 && depth < 18; depth++) {
    path.Add(view.GetItemName(item) + " " + Total(view, item).ToString("0.0", CultureInfo.InvariantCulture));
    var ch = new List<int>();
    view.GetItemChildren(item, ch);
    if (ch.Count == 0) break;
    int best = ch.OrderByDescending(k => Total(view, k)).First();
    if (Total(view, best) < Total(view, item) * 0.35f) break;
    item = best;
   }
   sb.AppendLine("  hot path: " + string.Join(" > ", path));
   // Self time per marker over the whole frame.
   var self = new Dictionary<string, float>();
   var stack = new Stack<int>(); stack.Push(root);
   var buf = new List<int>();
   while (stack.Count > 0) {
    int id = stack.Pop();
    if (id != root) {
     string n = view.GetItemName(id);
     float s = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
     self[n] = (self.TryGetValue(n, out float v) ? v : 0f) + s;
    }
    buf.Clear(); view.GetItemChildren(id, buf);
    foreach (var c in buf) stack.Push(c);
   }
   sb.AppendLine("  self time: " + string.Join(", ", self.OrderByDescending(kv => kv.Value).Take(14).Select(kv => kv.Key + " " + kv.Value.ToString("0.0", CultureInfo.InvariantCulture))));
   return sb.ToString();
  }

  static float Total(HierarchyFrameDataView view, int id) => view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);

  // The main thread waiting on the GPU/render thread hides the real work: list the render and job threads'
  // largest self-time markers for that frame.
  static string OtherThreads(int frame) {
   var sb = new StringBuilder();
   for (int t = 1; t < 64; t++) {
    using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, t, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false)) {
     if (view == null || !view.valid) break;
     string name = view.threadName ?? "";
     if (name.IndexOf("Render", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Gfx", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) < 0) continue;
     var self = new Dictionary<string, float>();
     var stack = new Stack<int>(); int root = view.GetRootItemID(); stack.Push(root);
     var buf = new List<int>();
     while (stack.Count > 0) {
      int id = stack.Pop();
      if (id != root) { string n = view.GetItemName(id); self[n] = (self.TryGetValue(n, out float v) ? v : 0f) + view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime); }
      buf.Clear(); view.GetItemChildren(id, buf); foreach (var c in buf) stack.Push(c);
     }
     float total = self.Values.Sum();
     if (total < 5f) continue;
     sb.AppendLine("  " + name + " (" + total.ToString("0.0", CultureInfo.InvariantCulture) + " ms): " + string.Join(", ", self.OrderByDescending(kv => kv.Value).Take(10).Select(kv => kv.Key + " " + kv.Value.ToString("0.0", CultureInfo.InvariantCulture))));
    }
   }
   return sb.ToString();
  }

  // Cold-load cost of every prefab in the named Addressables groups, in edit mode. With the "Use Asset
  // Database" play mode an Addressables load is a synchronous AssetDatabase load on the main thread, so this is
  // the stall a player feels in the editor when that prefab first streams in. Each entry is measured after
  // unloading unused assets, so shared dependencies already in memory (shaders, the open scene's) are free.
  public static void LoadCosts(string arg) {
   if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Spike] LoadCosts runs in edit mode"); return; }
   var settings = AddressableAssetSettingsDefaultObject.Settings;
   if (!settings) { Debug.LogWarning("[Spike] no Addressables settings"); return; }
   var wanted = new HashSet<string>((arg ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
   var rows = new List<(double ms, string line)>();
   var started = DateTime.Now;
   bool capped = false;
   foreach (var group in settings.groups) {
    if (!group || (wanted.Count > 0 && !wanted.Contains(group.Name))) continue;
    foreach (var entry in group.entries.ToArray()) {
     string path = entry.AssetPath;
     if (string.IsNullOrEmpty(path) || !(path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))) continue;
     if ((DateTime.Now - started).TotalSeconds > 150) { capped = true; break; }
     EditorUtility.UnloadUnusedAssetsImmediate(true);
     var sw = System.Diagnostics.Stopwatch.StartNew();
     var main = AssetDatabase.LoadMainAssetAtPath(path);
     var deps = main ? EditorUtility.CollectDependencies(new[] { main }) : new Object[0];
     sw.Stop();
     long texBytes = 0, meshBytes = 0, clipBytes = 0, allBytes = 0; int tex = 0, meshes = 0, clips = 0, mats = 0, maxTex = 0, curves = 0; long tris = 0, verts = 0;
     var shaders = new HashSet<string>();
     var heavy = new List<(long b, string n)>();
     foreach (var d in deps) {
      if (!d) continue;
      long b = Profiler.GetRuntimeMemorySizeLong(d); allBytes += b;
      switch (d) {
       case Texture t: tex++; texBytes += b; maxTex = Mathf.Max(maxTex, Mathf.Max(t.width, t.height)); heavy.Add((b, "tex " + t.name + " " + t.width + "x" + t.height + (t is Texture2D t2 ? " " + t2.format : ""))); break;
       case Mesh m: meshes++; meshBytes += b; verts += m.vertexCount; for (int i = 0; i < m.subMeshCount; i++) tris += m.GetIndexCount(i) / 3; heavy.Add((b, "mesh " + m.name + " " + m.vertexCount + "v" + (m.blendShapeCount > 0 ? " " + m.blendShapeCount + " blendshapes" : ""))); break;
       case AnimationClip c: clips++; clipBytes += b; int n = AnimationUtility.GetCurveBindings(c).Length; curves += n; heavy.Add((b, "clip " + c.name + " " + c.length.ToString("0.0", CultureInfo.InvariantCulture) + "s " + n + " curves")); break;
       case Material mat: mats++; if (mat.shader) shaders.Add(mat.shader.name); break;
      }
     }
     string top = string.Join("; ", heavy.OrderByDescending(h => h.b).Take(4).Select(h => h.n + " " + (h.b / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"));
     rows.Add((sw.Elapsed.TotalMilliseconds, $"{sw.Elapsed.TotalMilliseconds,8:0.0} ms | {entry.address} ({group.Name}) {Path.GetFileName(path)} | {deps.Length} objects, {allBytes / 1048576.0:0.0} MB | tex {tex} {texBytes / 1048576.0:0.0} MB max {maxTex} | mesh {meshes} {tris / 1000}k tris {verts / 1000}k verts {meshBytes / 1048576.0:0.0} MB | clips {clips} {clipBytes / 1048576.0:0.0} MB {curves} curves | mats {mats}, shaders {string.Join("/", shaders)} | heaviest: {top}"));
     main = null; deps = null;
    }
    if (capped) break;
   }
   EditorUtility.UnloadUnusedAssetsImmediate(true);
   var sb = new StringBuilder();
   sb.AppendLine("Cold editor load per Addressables prefab — " + DateTime.Now.ToString("HH:mm:ss") + " — play mode script: " + (settings.ActivePlayModeDataBuilder != null ? settings.ActivePlayModeDataBuilder.Name : "?") + (capped ? " — STOPPED at the 150 s cap" : ""));
   sb.AppendLine("Groups: " + (wanted.Count > 0 ? string.Join(", ", wanted) : "all"));
   foreach (var r in rows.OrderByDescending(r => r.ms)) sb.AppendLine(r.line);
   string outPath = Path.Combine("ClaudeBridge", "out", "loadcosts.txt");
   Directory.CreateDirectory(Path.GetDirectoryName(outPath));
   File.WriteAllText(outPath, sb.ToString());
   Debug.Log("[Spike] load costs for " + rows.Count + " prefabs; slowest " + string.Join(", ", rows.OrderByDescending(r => r.ms).Take(5).Select(r => r.line.Substring(0, Math.Min(90, r.line.Length)).Trim())) + "; see " + outPath);
  }
 }
}
