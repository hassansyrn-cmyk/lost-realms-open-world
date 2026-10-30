using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // One-time import settings for the realm themes (GLM Part B, mobile memory): the long music clips
 // load as Streaming so a phone keeps them out of memory. These are the project's own audio files in
 // Assets/Resources/Audio, not vendor packs. Run once via the bridge:
 //   exec LostRealmsOW.GlmMusicImport.SetStreaming
 public static class GlmMusicImport {
  static readonly string[] Clips = {
   "Assets/Resources/Audio/verdant_realm_theme.mp3",
   "Assets/Resources/Audio/whiteout_realm_theme.mp3",
   "Assets/Resources/Audio/sunscar_realm_theme.mp3",
   "Assets/Resources/Audio/stormveil_theme.wav",
   "Assets/Resources/Audio/astral_theme.wav",
   "Assets/Resources/Audio/lava_realm_theme.mp3",
   "Assets/Resources/Audio/boss_fight_theme.mp3",
   "Assets/Resources/Audio/storm_guardian_theme.wav",
   "Assets/Resources/Audio/astral_guardian_theme.wav",
  };
  public static void SetStreaming() {
   int changed = 0;
   foreach (var path in Clips) {
    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
    if (!importer) { Debug.LogWarning("[GlmMusicImport] no importer for " + path); continue; }
    var settings = importer.defaultSampleSettings;
    if (settings.loadType != AudioClipLoadType.Streaming) {
     settings.loadType = AudioClipLoadType.Streaming;
     importer.defaultSampleSettings = settings;
     importer.SaveAndReimport();
     changed++;
    }
   }
   Debug.Log($"[GlmMusicImport] streaming load type set on {changed} of {Clips.Length} realm themes");
  }
 }
}
