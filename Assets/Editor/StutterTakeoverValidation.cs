using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
namespace LostRealmsOW {
 public static class StutterTakeoverValidation {
  public static bool Busy{get;private set;}
  public static string Report{get;private set;}
  static string phase;static int errors;
  static void OnLog(string message,string trace,LogType type){
   if(type!=LogType.Exception&&type!=LogType.Error)return;
   if(message.Contains("EnvironmentDependencyValues")||message.Contains("graphicsApiMask")||message.Contains("[FusionEditor/Import]"))return;
   errors++;File.AppendAllText(Report,"ERROR "+phase+": "+message+"\n"+trace+"\n");
  }
  public static void Begin(string label){
   if(Busy)throw new Exception("Takeover capture already running");
   Report="Docs/Performance/Takeover_"+label+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".md";
   Directory.CreateDirectory("Docs/Performance");VerdantExperienceValidation.Begin();Busy=true;errors=0;
   OpenWorldManager.I.StartCoroutine(Run(label));
  }
  static IEnumerator Run(string label){
   var gm=OpenWorldManager.I;var hero=gm.Hero;var old=OpenWorldGraphics.Instance.Current;
   bool profiling=ProfilerDriver.enabled,profileEditor=ProfilerDriver.profileEditor,engineProfiling=Profiler.enabled;
   bool diagnostics=OpenWorldStreamingDiagnostics.Enabled;OpenWorldStreamingDiagnostics.Enabled=true;
   var probe=gm.gameObject.AddComponent<OpenWorldPerformanceProbe>();Application.logMessageReceived+=OnLog;
   try{
    File.WriteAllText(Report,"# Stutter takeover "+label+" — "+DateTime.Now+"\n\nDesktop Unity Editor, AssetDatabase Addressables backend; first visit is not guaranteed cold. Arrival windows include streaming from the moment of warp. Profiler is enabled for attribution, so compare like-for-like and do not infer device frame rates. No screenshots in measured windows.\n\n| Scenario | s | frames | mean ms | p50 | p95 | p99 | max | >50 ms | used MiB start/end | peak | reserved | Mono start/end | main p95 | alloc p95 KiB | gc0 |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|\n");
    OpenWorldHero.DebugAction("god");OpenWorldMobileHud.DebugUI("hud");OpenWorldGraphics.Instance.Apply(OpenWorldGraphics.Tier.Balanced,false);
    gm.Hero.WarpTo(gm.HuntGround(new Vector3(44,0,42)));gm.CameraRig.SnapToTarget();yield return new WaitForSecondsRealtime(4);
    foreach(int visit in new[]{0,1,2,3}){
     phase=(visit%2==0?"frozen":"storm")+(visit<2?"_first":"_repeat");string tag="takeover_"+label+"_"+phase;
     ClaudeSpikeProfiler.Begin(tag);yield return null;yield return null;probe.Begin(phase,7,Report);
     hero.WarpTo(gm.HuntGround(visit%2==0?new Vector3(78,0,318):new Vector3(232,0,-262)));gm.CameraRig.Yaw=0;gm.CameraRig.Pitch=10;gm.CameraRig.SnapToTarget();
     while(probe.Busy)yield return null;
     yield return null;ClaudeSpikeProfiler.End(tag);yield return null;
    }
    OpenWorldGraphics.Instance.Apply(OpenWorldGraphics.Tier.High,false);hero.WarpTo(gm.HuntGround(new Vector3(44,0,42)));gm.CameraRig.SnapToTarget();yield return new WaitForSecondsRealtime(4);
    phase="Verdant route";probe.Begin(phase,35,Report);bool outbound=true;float travel=0;var previous=hero.transform.position;
    while(probe.Busy){
     var target=outbound?new Vector3(128,0,64):new Vector3(44,0,42);var to=target-hero.transform.position;to.y=0;if(to.magnitude<3)outbound=!outbound;
     var f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);
     hero.TouchMoveInput=new Vector2(Vector3.Dot(to.normalized,r),Vector3.Dot(to.normalized,f))*.8f;
     travel+=Vector3.Distance(previous,hero.transform.position);previous=hero.transform.position;yield return null;
    }
    hero.TouchMoveInput=Vector2.zero;
    File.AppendAllText(Report,"\nRoute travelled "+travel.ToString("F1")+" m. Tagged errors: "+errors+".\n");
    phase="handle recovery";
    var type=typeof(OpenWorldPopulation);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var speciesType=type.GetNestedType("Species",System.Reflection.BindingFlags.NonPublic);var groupType=type.GetNestedType("Group",System.Reflection.BindingFlags.NonPublic);
    var sp=Activator.CreateInstance(speciesType,true);var group=Activator.CreateInstance(groupType,true);
    speciesType.GetField("source").SetValue(sp,"QA recovery crow");speciesType.GetField("address").SetValue(sp,"ow.imported.expansion_crow");speciesType.GetField("display").SetValue(sp,"Recovery Crow");speciesType.GetField("height").SetValue(sp,.3f);
    speciesType.GetField("kind").SetValue(sp,Enum.Parse(speciesType.GetField("kind").FieldType,"Bird"));
    speciesType.GetField("requested").SetValue(sp,true); // Deliberate stale requested flag with a default, invalid handle.
    groupType.GetField("species").SetValue(group,sp);groupType.GetField("spawning").SetValue(group,true);groupType.GetField("active").SetValue(group,true);groupType.GetField("size").SetValue(group,1);groupType.GetField("radius").SetValue(group,3f);groupType.GetField("center").SetValue(group,gm.HuntGround(hero.transform.position+Vector3.right*8));
    var population=OpenWorldPopulation.Instance;var spawn=type.GetMethod("Spawn",flags);float timeout=Time.realtimeSinceStartup+15;
    try{
     while((bool)groupType.GetField("spawning").GetValue(group)&&Time.realtimeSinceStartup<timeout){spawn.Invoke(population,new[]{group,(object)gm});yield return null;}
     var handle=(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject>)speciesType.GetField("handle").GetValue(sp);
     bool ok=handle.IsValid()&&handle.IsDone&&handle.Status==UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded&&!(bool)groupType.GetField("spawning").GetValue(group);
     File.AppendAllText(Report,(ok?"PASS":"FAIL")+": pending spawn recovered a deliberately invalid handle without dereferencing it.\n");if(!ok)errors++;
    }finally{
     type.GetMethod("Despawn",flags).Invoke(population,new[]{group,(object)gm});
     var handle=(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject>)speciesType.GetField("handle").GetValue(sp);if(handle.IsValid())UnityEngine.AddressableAssets.Addressables.Release(handle);
    }
    File.AppendAllText(Report,"\nStreaming scopes (nested; do not sum):\n");
    foreach(OpenWorldStreamingDiagnostics.Work work in Enum.GetValues(typeof(OpenWorldStreamingDiagnostics.Work))){var rows=Enumerable.Range(0,OpenWorldStreamingDiagnostics.Count).Select(OpenWorldStreamingDiagnostics.Get).Where(r=>r.Kind==work).ToArray();File.AppendAllText(Report,$"- {work}: {rows.Length} calls, max {(rows.Length>0?rows.Max(r=>r.Milliseconds):0):F2} ms.\n");}
    for(int i=0;i<3;i++){
     hero.WarpTo(gm.HuntGround(i%2==0?new Vector3(232,0,-262):new Vector3(-150,0,262)));gm.CameraRig.SnapToTarget();yield return new WaitForSecondsRealtime(3);
    }
    File.AppendAllText(Report,"\nThree additional return-warps completed. Tagged errors including returns: "+errors+".\n");
   }finally{
    hero.TouchMoveInput=Vector2.zero;hero.TouchSprintHeld=false;
    ProfilerDriver.enabled=profiling;ProfilerDriver.profileEditor=profileEditor;Profiler.enabled=engineProfiling;
    OpenWorldStreamingDiagnostics.Enabled=diagnostics;
    Application.logMessageReceived-=OnLog;OpenWorldGraphics.Instance.Apply(old,false);UnityEngine.Object.Destroy(probe);Busy=false;
   }
  }
  static Action restoreRoute;static string routeTag;
  static bool cancelRoute;
  public static void RouteTimeline(string label){
   if(Busy)throw new Exception("Validation already running");
   Report="Docs/Performance/RouteTimeline_"+label+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".md";routeTag="takeover_route_"+label;
   Directory.CreateDirectory("Docs/Performance");VerdantExperienceValidation.Begin();Busy=true;cancelRoute=false;
   OpenWorldManager.I.StartCoroutine(RouteRun());
  }
  static IEnumerator RouteRun(){
   var gm=OpenWorldManager.I;var hero=gm.Hero;int oldCap=Application.targetFrameRate;
   bool driver=ProfilerDriver.enabled,editor=ProfilerDriver.profileEditor,engine=Profiler.enabled,binary=Profiler.enableBinaryLog;string log=Profiler.logFile;
   var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
   System.Reflection.PropertyInfo history=null;object oldHistory=null;
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
    Type[] types;try{types=assembly.GetTypes();}catch{continue;}
    var type=types.FirstOrDefault(t=>t.Name=="ProfilerUserSettings");if(type==null)continue;
    var property=type.GetProperty("frameCount",flags);if(property==null||!property.CanWrite||property.PropertyType!=typeof(int))continue;
    history=property;oldHistory=property.GetValue(null);property.SetValue(null,2000);break;
   }
   var probe=gm.gameObject.AddComponent<OpenWorldPerformanceProbe>();
   string raw=Path.GetFullPath("Temp/StutterRoute_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".raw");
   restoreRoute=()=>{
    hero.TouchMoveInput=Vector2.zero;hero.TouchSprintHeld=false;ProfilerDriver.enabled=false;Profiler.enabled=false;Profiler.enableBinaryLog=false;
    Profiler.logFile=log;Profiler.enableBinaryLog=binary;ProfilerDriver.profileEditor=editor;ProfilerDriver.enabled=driver;Profiler.enabled=engine;
    if(history!=null)history.SetValue(null,oldHistory);Application.targetFrameRate=oldCap;if(probe)UnityEngine.Object.Destroy(probe);
   };
   try{
    File.WriteAllText(Report,"# Focused route CPU timeline — "+DateTime.Now+"\n\nEditor; graphics tier retained ("+OpenWorldGraphics.Instance.Current+"), temporary 30 fps diagnostic cap. Focus at start: "+Application.isFocused+"; resolution "+Screen.width+"x"+Screen.height+"; InteractionMode preference "+UnityEditor.EditorPrefs.GetInt("InteractionMode",-1)+". Profiler history "+(history!=null?"2000 frames; previous "+oldHistory:"installed setter unavailable; raw recording retains full route")+". CPU includes Editor profiling overhead; not a phone measurement. Raw: "+raw+"\n\n| Scenario | s | frames | mean ms | p50 | p95 | p99 | max | >50 ms | used MiB start/end | peak | reserved | Mono start/end | main p95 | alloc p95 KiB | gc0 |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|\n");
    OpenWorldHero.DebugAction("god");Application.targetFrameRate=30;hero.WarpTo(gm.HuntGround(new Vector3(44,0,42)));gm.CameraRig.SnapToTarget();yield return new WaitForSecondsRealtime(6);
    ClaudeSpikeProfiler.Begin("route_prime");yield return new WaitForSecondsRealtime(1);ProfilerDriver.enabled=false;
    Profiler.logFile=raw;Profiler.enableBinaryLog=true;ClaudeSpikeProfiler.Begin(routeTag);ProfilerDriver.profileEditor=true;yield return null;yield return null;
    probe.Begin("Focused hunt route",32,Report);bool outbound=true;float travel=0;var previous=hero.transform.position;
    while(probe.Busy&&!cancelRoute){
     var target=outbound?new Vector3(128,0,64):new Vector3(44,0,42);var to=target-hero.transform.position;to.y=0;if(to.magnitude<3)outbound=!outbound;
     var f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);
     hero.TouchMoveInput=new Vector2(Vector3.Dot(to.normalized,r),Vector3.Dot(to.normalized,f))*.8f;
     travel+=Vector3.Distance(previous,hero.transform.position);previous=hero.transform.position;yield return null;
    }
    hero.TouchMoveInput=Vector2.zero;yield return null;
    if(!cancelRoute){ClaudeSpikeProfiler.End(routeTag);SummarizeBridge();File.AppendAllText(Report,"\nTravelled "+travel.ToString("F1")+" m. Raw capture exists: "+File.Exists(raw)+"; bytes "+(File.Exists(raw)?new FileInfo(raw).Length:0)+". CPU breakdown: ClaudeBridge/out/spikes_"+routeTag+".txt\n");}
   }finally{restoreRoute?.Invoke();restoreRoute=null;Busy=false;}
  }
  static void SummarizeBridge(){
   var times=new System.Collections.Generic.List<float>();
   for(int f=ProfilerDriver.firstFrameIndex;f>=0&&f<=ProfilerDriver.lastFrameIndex;f++){
    using(var view=ProfilerDriver.GetHierarchyFrameDataView(f,0,UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime,false)){
     if(view==null||!view.valid)continue;var stack=new System.Collections.Generic.Stack<int>();var children=new System.Collections.Generic.List<int>();stack.Push(view.GetRootItemID());
     while(stack.Count>0){int id=stack.Pop();if(view.GetItemName(id).Contains("ClaudeBridge.Poll")){times.Add(view.GetItemColumnDataAsFloat(id,UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime));continue;}children.Clear();view.GetItemChildren(id,children);foreach(int child in children)stack.Push(child);}
    }
   }
   if(times.Count>0){times.Sort();File.AppendAllText(Report,$"\nBridge Poll: {times.Count} frame samples, mean {times.Average():F3} ms, p95 {times[(int)((times.Count-1)*.95f)]:F3} ms, max {times[times.Count-1]:F3} ms.\n");}
  }
  public static void Cleanup(){cancelRoute=true;restoreRoute?.Invoke();restoreRoute=null;Busy=false;VerdantExperienceValidation.Cleanup();}
 }
}
