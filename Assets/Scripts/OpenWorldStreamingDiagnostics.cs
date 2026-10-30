using System;
using System.Diagnostics;
using UnityEngine;
namespace LostRealmsOW {
 public static class OpenWorldStreamingDiagnostics {
  public enum Work { RealmTick, RealmFill, PopulationTick, PopulationSpawn }
  public struct Sample {public Work Kind;public double At,Milliseconds;public int Items;}
  static readonly Sample[] samples=new Sample[8192];
  public static int Count{get;private set;}
  public static bool Enabled;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Reset(){Count=0;Enabled=false;
#if UNITY_EDITOR
   Enabled=UnityEditor.SessionState.GetBool("LostRealms.SpawnTrace",false);
#endif
  }
  public static long Start()=>Enabled?Stopwatch.GetTimestamp():0;
  public static void End(Work kind,long stamp,int items=0){if(stamp==0||!Enabled||Count>=samples.Length)return;samples[Count++]=new Sample{Kind=kind,At=Time.realtimeSinceStartupAsDouble,Milliseconds=(Stopwatch.GetTimestamp()-stamp)*1000.0/Stopwatch.Frequency,Items=items};}
  public static Sample Get(int i)=>samples[i];
 }
}
