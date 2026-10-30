using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Live review helpers for bridge jobs (Claude). They run inside Play mode through the bridge's "call" command,
 // so the game's own Update loops do the work.
 //  call LostRealmsOW.ClaudeReviewChecks.VentAhead 12      - moves the first Gale Updraft vent 12 m ahead of Aster
 //                                                           (ahead of her horse when mounted), onto the ground.
 //  call LostRealmsOW.ClaudeReviewChecks.GlideState tag    - logs mounted / gliding / height above the ground.
 //  call LostRealmsOW.ClaudeReviewChecks.Ride x y sprint   - holds the touch stick (and sprint) while riding.
 //  call LostRealmsOW.ClaudeReviewChecks.SkateStart        - starts sampling the ridden horse's hoof bones;
 //  call LostRealmsOW.ClaudeReviewChecks.SkateReport tag   - logs body speed and how fast the planted hooves
 //                                                           still move over the ground (foot skate), then
 //                                                           keeps sampling for the next segment.
 public static class ClaudeReviewChecks {
  public static void VentAhead(string arg) {
   var gm = OpenWorldManager.I;
   var hero = gm ? gm.Hero : null;
   var vent = Object.FindFirstObjectByType<OpenWorldUpdraft>();
   if (!hero || !vent) { Debug.LogError("[Review] no hero or no updraft in the scene"); return; }
   float distance = F(arg, 10f);
   Transform body = hero.IsMounted ? hero.Mount.transform : hero.transform;
   Vector3 forward = body.forward; forward.y = 0f; forward.Normalize();
   Vector3 p = body.position + forward * distance;
   p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
   vent.transform.position = p;
   Debug.Log("[Review] vent '" + vent.UpdraftTitle + "' moved " + distance.ToString("0.0", CultureInfo.InvariantCulture) + " m ahead to " + p + "; mounted=" + hero.IsMounted);
  }

  public static void GlideState(string tag) {
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (!hero) { Debug.LogError("[Review] no hero"); return; }
   float ground = OpenWorldMaterialHelper.SampleGroundHeight(hero.transform.position);
   Debug.Log("[Review] " + tag + ": mounted=" + hero.IsMounted + " gliding=" + hero.IsGliding + " height above ground " + (hero.transform.position.y - ground).ToString("0.00", CultureInfo.InvariantCulture) + " m");
  }

  public static void Ride(string arg) {
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (!hero) { Debug.LogError("[Review] no hero"); return; }
   var p = (arg ?? "").Trim().Split(' ');
   float x = p.Length > 0 ? F(p[0], 0f) : 0f, y = p.Length > 1 ? F(p[1], 0f) : 0f, s = p.Length > 2 ? F(p[2], 0f) : 0f;
   bool idle = x == 0f && y == 0f && s == 0f;
   OpenWorldHero.DebugAlwaysFocused = !idle;
   hero.TouchMoveInput = new Vector2(x, y);
   hero.TouchSprintHeld = s > 0.5f;
   Debug.Log("[Review] ride stick (" + x.ToString("0.00", CultureInfo.InvariantCulture) + ", " + y.ToString("0.00", CultureInfo.InvariantCulture) + ") sprint " + (s > 0.5f));
  }

  static readonly List<Transform> hooves = new List<Transform>();
  static readonly List<List<Vector3>> samples = new List<List<Vector3>>();   // (height above ground, horizontal speed, speed along the body's forward)
  static readonly List<Vector3> lastPos = new List<Vector3>();
  static Transform horse;
  static OpenWorldImportedModel motion;
  static Vector3 bodyLast;
  static float bodyDist, bodyTime, lastTime;
  static int lastFrame = -1;
  static bool sampling;
  static readonly Dictionary<string, int> stateFrames = new Dictionary<string, int>();

  public static void SkateStart(string arg) {
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   horse = hero && hero.IsMounted ? hero.Mount.transform : null;
   if (!horse) { Debug.LogError("[Review] skate: Aster is not mounted"); return; }
   motion = horse.GetComponentInChildren<OpenWorldImportedModel>(true);
   hooves.Clear(); samples.Clear(); lastPos.Clear(); stateFrames.Clear();
   foreach (var t in horse.GetComponentsInChildren<Transform>(true)) {
    if (t.name.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) < 0 || t.childCount != 0) continue;
    hooves.Add(t); samples.Add(new List<Vector3>()); lastPos.Add(t.position);
   }
   bodyLast = horse.position; bodyDist = 0f; bodyTime = 0f; lastTime = Time.time; lastFrame = Time.frameCount;
   if (!sampling) { EditorApplication.update += Sample; sampling = true; }
   string secondary = "";
   if (motion) {
    var f = typeof(OpenWorldImportedModel).GetField("tailBones", BindingFlags.Instance | BindingFlags.NonPublic);
    var n = typeof(OpenWorldImportedModel).GetField("neckBone", BindingFlags.Instance | BindingFlags.NonPublic);
    var tails = f != null ? f.GetValue(motion) as Transform[] : null;
    var neck = n != null ? n.GetValue(motion) as Transform : null;
    secondary = "; secondary motion found " + (tails != null ? tails.Length : 0) + " tail bones, neck " + (neck ? neck.name : "none");
   }
   Debug.Log("[Review] skate: sampling " + hooves.Count + " hoof bones (" + string.Join(", ", hooves.Select(h => h.name)) + ")" + secondary);
  }

  static void Sample() {
   if (!Application.isPlaying || !horse) { EditorApplication.update -= Sample; sampling = false; return; }
   if (Time.frameCount == lastFrame) return;
   float dt = Time.time - lastTime;
   lastFrame = Time.frameCount; lastTime = Time.time;
   if (dt <= 0.0001f || dt > 0.25f) { RefreshLast(); return; }
   Vector3 bp = horse.position, bd = bp - bodyLast; bd.y = 0f;
   bodyDist += bd.magnitude; bodyTime += dt; bodyLast = bp;
   for (int i = 0; i < hooves.Count; i++) {
    var h = hooves[i];
    if (!h) continue;
    Vector3 p = h.position, d = p - lastPos[i]; d.y = 0f; lastPos[i] = p;
    float ground = OpenWorldMaterialHelper.SampleGroundHeight(p, horse);
    Vector3 fwd = horse.forward; fwd.y = 0f; fwd.Normalize();
    samples[i].Add(new Vector3(p.y - ground, d.magnitude / dt, Vector3.Dot(d, fwd) / dt));
   }
   if (motion) { string s = motion.State ?? "?"; stateFrames[s] = stateFrames.TryGetValue(s, out int c) ? c + 1 : 1; }
  }

  static void RefreshLast() {
   if (horse) bodyLast = horse.position;
   for (int i = 0; i < hooves.Count; i++) if (hooves[i]) lastPos[i] = hooves[i].position;
  }

  public static void SkateReport(string tag) {
   if (bodyTime <= 0f) { Debug.LogError("[Review] skate " + tag + ": no samples"); return; }
   float bodySpeed = bodyDist / bodyTime;
   float sum30 = 0f, fwd30 = 0f, sumLow = 0f, fwdLow = 0f;
   int n = 0;
   var parts = new List<string>();
   for (int i = 0; i < hooves.Count; i++) {
    var s = samples[i];
    if (s.Count < 10) continue;
    var heights = s.Select(v => v.x).OrderBy(v => v).ToList();
    float t30 = heights[Mathf.Clamp((int)(heights.Count * 0.3f), 0, heights.Count - 1)];   // lowest 30% of the cycle
    float tLow = heights[0] + 0.03f;                                                        // within 3 cm of the lowest point
    var a = s.Where(v => v.x <= t30).ToList();
    var b = s.Where(v => v.x <= tLow).ToList();
    float m30 = a.Count > 0 ? a.Average(v => v.y) : 0f, f30 = a.Count > 0 ? a.Average(v => v.z) : 0f;
    float mLow = b.Count > 0 ? b.Average(v => v.y) : 0f, fLow = b.Count > 0 ? b.Average(v => v.z) : 0f;
    parts.Add(hooves[i].name + " " + mLow.ToString("0.00", CultureInfo.InvariantCulture) + "/" + fLow.ToString("+0.00;-0.00", CultureInfo.InvariantCulture));
    sum30 += m30; fwd30 += f30; sumLow += mLow; fwdLow += fLow; n++;
   }
   if (n > 0) { sum30 /= n; fwd30 /= n; sumLow /= n; fwdLow /= n; }
   string natural = "";
   if (motion) {
    string st = motion.State ?? "walk";
    float nat = motion.NaturalSpeed(st == "idle" ? "walk" : st);
    var rf = typeof(OpenWorldImportedModel).GetField("rate", BindingFlags.Instance | BindingFlags.NonPublic);
    float rate = rf != null ? (float)rf.GetValue(motion) : -1f;
    natural = " | clip natural " + nat.ToString("0.00", CultureInfo.InvariantCulture) + " m/s, playback x" + rate.ToString("0.00", CultureInfo.InvariantCulture);
   }
   string states = string.Join(" ", stateFrames.OrderByDescending(k => k.Value).Select(k => k.Key + ":" + k.Value));
   string Pct(float v) => (bodySpeed > 0.05f ? v / bodySpeed * 100f : 0f).ToString("0", CultureInfo.InvariantCulture) + "%";
   Debug.Log("[Review] skate " + tag + ": body " + bodySpeed.ToString("0.00", CultureInfo.InvariantCulture) + " m/s" +
    " | hoof at its lowest (3 cm): moves " + sumLow.ToString("0.00", CultureInfo.InvariantCulture) + " m/s (" + Pct(sumLow) + "), along travel " + fwdLow.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) +
    " | lowest 30%: moves " + sum30.ToString("0.00", CultureInfo.InvariantCulture) + " m/s (" + Pct(sum30) + "), along travel " + fwd30.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) +
    natural + " | " + string.Join(", ", parts) + " | states " + states);
   foreach (var l in samples) l.Clear();
   stateFrames.Clear(); bodyDist = 0f; bodyTime = 0f;
  }

  static float F(string s, float fallback) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
 }
}
