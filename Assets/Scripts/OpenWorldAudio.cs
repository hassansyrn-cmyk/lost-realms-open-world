using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // A fixed voice budget prevents rapid combat events from creating unbounded sources.
 public sealed class OpenWorldAudio : MonoBehaviour {
  static OpenWorldAudio instance;
  public static OpenWorldAudio Instance => instance;
  const string VolumePref = "LostRealms.AudioVolume";
  public int VolumeTier { get; private set; } // 0 = 100%, 1 = 50%, 2 = Muted
  public float MasterVolume => VolumeTier == 1 ? 0.5f : VolumeTier == 2 ? 0f : 1f;
  public string VolumeLabel => VolumeTier == 1 ? "50%" : VolumeTier == 2 ? "MUTED" : "100%";
  readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
  readonly AudioSource[] voices = new AudioSource[8];
  readonly int[] priority = new int[8];
  float lastImpact = -10;
  void Awake() {
   instance = this;
   VolumeTier = Mathf.Clamp(PlayerPrefs.GetInt(VolumePref, 0), 0, 2);
   AudioListener.volume = MasterVolume;
   for (int i = 0; i < voices.Length; i++) {
    voices[i] = gameObject.AddComponent<AudioSource>();
    voices[i].playOnAwake = false;
    voices[i].spatialBlend = 0f;
   }
  }
  public void CycleVolume() {
   VolumeTier = (VolumeTier + 1) % 3;
   AudioListener.volume = MasterVolume;
   PlayerPrefs.SetInt(VolumePref, VolumeTier);
   PlayerPrefs.Save();
  }
  // Runtime-generated clips (procedural thunder and similar) join the named lookup.
  public static void Register(string name, AudioClip clip) {
   if (!instance || !clip || string.IsNullOrEmpty(name)) return;
   instance.clips[name] = clip;
  }

  public static void Play(string name, float volume = 0.7f, float pitch = 1f, int importance = 0) {
   if (!instance || instance.VolumeTier == 2) return;
   if (!instance.clips.TryGetValue(name, out var clip)) {
    clip = Resources.Load<AudioClip>("Audio/" + name);
    instance.clips[name] = clip;
   }
   clip = OpenWorldImportedContent.Sound(name) ?? clip;
   if (!clip) return;
   int slot=-1, weakest=int.MaxValue;
   for(int i=0;i<instance.voices.Length;i++) {
    if(!instance.voices[i].isPlaying){slot=i;break;}
    if(instance.priority[i]<importance&&instance.priority[i]<weakest){slot=i;weakest=instance.priority[i];}
   }
   if(slot<0)return;
   var voice=instance.voices[slot];voice.Stop();instance.priority[slot]=importance;
   voice.clip=clip;voice.volume=Mathf.Clamp01(volume);voice.pitch=Mathf.Clamp(pitch,.75f,1.25f);voice.Play();
  }
  public static void PlayCombatImpact(bool heavy){if(!instance||Time.unscaledTime-instance.lastImpact<.045f)return;instance.lastImpact=Time.unscaledTime;Play("sfx_impact",heavy?.42f:.27f,Random.Range(heavy?.89f:.97f,heavy?.96f:1.06f),2);}
  void OnDestroy() { if (instance == this) instance = null; }
 }
}

