using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Named groups of vendor audio clips (footsteps per surface, birds, fire loops, magic) referenced
 // from one Resources asset so player builds include exactly the clips the soundscape uses.
 public sealed class OpenWorldSoundBank : ScriptableObject {
  [Serializable] public sealed class Group { public string name = ""; public AudioClip[] clips = Array.Empty<AudioClip>(); }
  public Group[] groups = Array.Empty<Group>();

  [NonSerialized] Dictionary<string, Group> lookup;
  static OpenWorldSoundBank loaded;
  static bool attempted;

  public static OpenWorldSoundBank Instance {
   get {
    if (!loaded && !attempted) { attempted = true; loaded = Resources.Load<OpenWorldSoundBank>("World/SoundBank"); }
    return loaded;
   }
  }

  public AudioClip[] Clips(string name) {
   if (lookup == null) {
    lookup = new Dictionary<string, Group>();
    foreach (var g in groups) if (g != null && !string.IsNullOrEmpty(g.name)) lookup[g.name] = g;
   }
   return lookup.TryGetValue(name, out var group) ? group.clips : Array.Empty<AudioClip>();
  }

  public AudioClip Random(string name) {
   var clips = Clips(name);
   return clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];
  }
 }
}
