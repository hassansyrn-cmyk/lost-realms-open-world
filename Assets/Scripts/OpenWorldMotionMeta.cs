using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Side-car data written by the Phase 1 motion baker next to the baked clips
 // (Resources/Animations/<role>/motion_meta.json). The clips keep every RootT curve at 0; these numbers
 // let the animation driver match strides, keep feet in phase, time impacts and put crouches, rolls and
 // landings back on the ground without any root curves.
 [Serializable] public sealed class OWMotionClip {
  public string state;
  public string source;
  public float length;
  public bool loop;
  public float stride;          // planted-foot speed (m/s) of a locomotion cycle at playback rate 1
  public float plant = -1f;     // normalized time the left foot plants (locomotion phase sync)
  public float[] strikes;       // impact frames, seconds
  public float[] height;        // body height offset (m) sampled at OpenWorldMotionMeta.Rate; empty = none
 }

 [Serializable] public sealed class OWMotionMetaFile {
  public int version;
  public OWMotionClip[] clips;
 }

 public static class OpenWorldMotionMeta {
  public const float Rate = 30f;
  static readonly Dictionary<string, Dictionary<string, OWMotionClip>> cache = new Dictionary<string, Dictionary<string, OWMotionClip>>();

  public static Dictionary<string, OWMotionClip> For(string role) {
   if (string.IsNullOrEmpty(role)) role = "Aster";
   if (cache.TryGetValue(role, out var found)) return found;
   var map = new Dictionary<string, OWMotionClip>();
   var text = Resources.Load<TextAsset>("Animations/" + role + "/motion_meta");
   if (text) {
    try {
     var file = JsonUtility.FromJson<OWMotionMetaFile>(text.text);
     if (file != null && file.clips != null)
      foreach (var c in file.clips) if (c != null && !string.IsNullOrEmpty(c.state)) map[c.state] = c;
    } catch (Exception e) { Debug.LogWarning("[Motion] meta for " + role + " unreadable: " + e.Message); }
   }
   cache[role] = map;
   return map;
  }

  public static float Height(OWMotionClip c, float t, bool loop) {
   if (c == null || c.height == null || c.height.Length == 0) return 0f;
   float len = Mathf.Max(0.01f, c.length);
   if (loop) t = Mathf.Repeat(t, len); else t = Mathf.Clamp(t, 0f, len);
   float f = t * Rate;
   int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, c.height.Length - 1);
   int j = loop ? (i + 1) % c.height.Length : Mathf.Min(c.height.Length - 1, i + 1);
   return Mathf.Lerp(c.height[i], c.height[j], f - Mathf.Floor(f));
  }
 }
}
