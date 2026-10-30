using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
namespace LostRealmsOW {
 // Explicitly started by QA. Fixed arrays and once-a-second memory probes keep measurement overhead bounded.
 public sealed class OpenWorldPerformanceProbe:MonoBehaviour {
  public bool Busy{get;private set;}
  readonly double[] frames=new double[8192],main=new double[8192],alloc=new double[8192];
  int count,mainCount,allocCount,startGc;string label,path;double started,previous,until,nextMemory;
  long usedStart,monoStart,peakUsed,peakReserved,monoEnd;ProfilerRecorder mainThread,gc;
  public void Begin(string name,float seconds,string destination){
   if(Busy)throw new InvalidOperationException("Performance sample already running");
   label=name;path=destination;count=mainCount=allocCount=0;
   started=previous=Time.realtimeSinceStartupAsDouble;until=started+seconds;nextMemory=started;
   usedStart=peakUsed=Profiler.GetTotalAllocatedMemoryLong();peakReserved=Profiler.GetTotalReservedMemoryLong();monoStart=Profiler.GetMonoUsedSizeLong();startGc=GC.CollectionCount(0);
   mainThread=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
   gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);Busy=true;
  }
  void Update(){
   if(!Busy)return;double now=Time.realtimeSinceStartupAsDouble;
   if(count<frames.Length)frames[count++]=(now-previous)*1000;previous=now;
   if(mainThread.Valid&&mainThread.LastValue>0&&mainCount<main.Length)main[mainCount++]=mainThread.LastValue/1000000.0;
   if(gc.Valid&&allocCount<alloc.Length)alloc[allocCount++]=gc.LastValue/1024.0;
   if(now>=nextMemory){nextMemory=now+1;peakUsed=Math.Max(peakUsed,Profiler.GetTotalAllocatedMemoryLong());peakReserved=Math.Max(peakReserved,Profiler.GetTotalReservedMemoryLong());}
   if(now>=until)Finish(now);
  }
  static double Quantile(double[] values,int n,double q){return n>0?values[Math.Min(n-1,(int)Math.Ceiling((n-1)*q))]:double.NaN;}
  void Finish(double now){
   long end=Profiler.GetTotalAllocatedMemoryLong();peakUsed=Math.Max(peakUsed,end);peakReserved=Math.Max(peakReserved,Profiler.GetTotalReservedMemoryLong());monoEnd=Profiler.GetMonoUsedSizeLong();int collections=GC.CollectionCount(0)-startGc;
   double sum=0;int over=0;for(int i=0;i<count;i++){sum+=frames[i];if(frames[i]>50)over++;}
   Array.Sort(frames,0,count);Array.Sort(main,0,mainCount);Array.Sort(alloc,0,allocCount);
   string row=$"| {label} | {now-started:F1} | {count} | {sum/Math.Max(1,count):F2} | {Quantile(frames,count,.5):F2} | {Quantile(frames,count,.95):F2} | {Quantile(frames,count,.99):F2} | {Quantile(frames,count,1):F2} | {over} | {usedStart/1048576.0:F1} → {end/1048576.0:F1} | {peakUsed/1048576.0:F1} | {peakReserved/1048576.0:F1} | {monoStart/1048576.0:F1} → {monoEnd/1048576.0:F1} | {Quantile(main,mainCount,.95):F2} | {Quantile(alloc,allocCount,.95):F1} | {collections} |\n";
   Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,row);Debug.Log("[CodexPerformance] "+row.Trim());
   Busy=false;Dispose();
  }
  void Dispose(){if(mainThread.Valid)mainThread.Dispose();if(gc.Valid)gc.Dispose();}
  void OnDestroy(){if(Busy)Debug.LogWarning("[CodexPerformance] Incomplete sample discarded: "+label);Busy=false;Dispose();}
 }
}

