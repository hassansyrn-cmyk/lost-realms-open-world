using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
namespace LostRealmsOW {
 public static class VerdantLivingWorldValidation {
  public static bool Busy{get;private set;}
  const string Dir="Docs/Performance/";
  public static void Arm(){SessionState.SetBool("LostRealms.SpawnTrace",true);}
  public static void Disarm(){SessionState.SetBool("LostRealms.SpawnTrace",false);}
  static float hour;static string weather;static bool hadWeather;
  public static void Begin(){hour=OpenWorldAtmosphere.Instance.Hour;hadWeather=PlayerPrefs.HasKey("LostRealms.RegionWeather");weather=PlayerPrefs.GetString("LostRealms.RegionWeather","");VerdantExperienceValidation.Begin();}
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();OpenWorldAtmosphere.Instance.SetHour(hour,false,false);if(hadWeather)PlayerPrefs.SetString("LostRealms.RegionWeather",weather);else PlayerPrefs.DeleteKey("LostRealms.RegionWeather");}
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Validation="Docs/Performance/Codex_Living_validation.txt";
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Validation,"PASS: "+text+"\n");}
  static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
  public static void Hunt(){Busy=true;OpenWorldManager.I.StartCoroutine(FullHunt());}
  static IEnumerator FullHunt(){
   var gm=OpenWorldManager.I;var h=gm.Hero;int summons=0,warnings=0;float lowest=h.MaxHealth;
   try{
    Directory.CreateDirectory(Dir);File.AppendAllText(Validation,"\nFull hunt "+DateTime.Now+"\n");
    gm.CollectedWorldIds.RemoveWhere(f=>f.StartsWith("verdant-hunt:"));OpenWorldAtmosphere.Instance.SetHour(10,false,false);
    var kael=gm.NPCs.Find(n=>n&&n.NpcId=="kael");h.WarpTo(kael.transform.position+Vector3.back*2);gm.Interact();Check(gm.VerdantHuntStage==1,"accept hunt through NPC interaction");
    h.WarpTo(gm.VerdantHuntTarget);gm.Interact();h.WarpTo(gm.VerdantHuntTarget);gm.Interact();Check(gm.VerdantHuntStage==2,"inspect both trail sites through Interact");
    h.WarpTo(gm.VerdantHuntTarget+Vector3.back*8);h.EquipWeapon(0);h.Health=h.MaxHealth;h.Aether=h.MaxAether;Set(h,"invulnerableUntil",0f);
    OpenWorldEnemy bear=null;float loadUntil=Time.realtimeSinceStartup+15;
    while(Time.realtimeSinceStartup<loadUntil){var q=UnityEngine.Object.FindAnyObjectByType<VerdantHuntQuarry>();if(q){bear=q.GetComponent<OpenWorldEnemy>();break;}yield return null;}
    Check(bear,"prepared quarry streams in");Check(bear.Visual.GetComponentsInChildren<Animator>().Length==1,"imported quarry has one animator and no fallback rig");
    h.LockOnTarget=bear;float end=Time.time+65,nextAttack=0,nextSummon=Time.time+2,warnAt=0;bool was=false,responded=false;float started=Time.time;
    while(Time.time<end&&!bear.Dead&&!h.Dead){
     Vector3 to=bear.transform.position-h.transform.position;to.y=0;var forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
     h.TouchMoveInput=to.magnitude>2?new Vector2(Vector3.Dot(to.normalized,right),Vector3.Dot(to.normalized,forward))*.7f:Vector2.zero;
     if(bear.Telegraphing&&!was){warnings++;warnAt=Time.time;responded=false;}was=bear.Telegraphing;
     if(bear.Telegraphing&&!responded&&Time.time-warnAt>.32f){responded=true;if(warnings%2==0)h.PerformDodge(new Vector2(0,-1));else h.PerformParry();}
     if(to.magnitude<5.2f&&Time.time>nextSummon){float prior=(float)typeof(OpenWorldHero).GetField("echoCooldownUntil",Private).GetValue(h);h.PerformEchoSkill();float after=(float)typeof(OpenWorldHero).GetField("echoCooldownUntil",Private).GetValue(h);if(after>prior){summons++;nextSummon=Time.time+6;}else nextSummon=Time.time+.25f;}
     else if(to.magnitude<3.4f&&!bear.Telegraphing&&Time.time>nextAttack){h.PerformLightAttack();nextAttack=Time.time+.65f;}
     lowest=Mathf.Min(lowest,h.Health);yield return null;
    }
    h.TouchMoveInput=Vector2.zero;h.LockOnTarget=null;
    File.AppendAllText(Validation,$"Fight: elapsed {Time.time-started:F1}s, warnings {warnings}, actual summons {summons}, lowest HP {lowest:F2}, quarry HP {bear.Health:F2}. Scripted real attack/dodge/parry/summon decisions, not human balance certification.\n");
    Check(!h.Dead&&bear.Dead&&summons>=1&&gm.VerdantHuntStage==3,"normal attacks and summon win quarry encounter with real damage");
    int meat=gm.RawMeat;h.WarpTo(gm.VerdantHuntTarget);gm.Interact();Check(gm.VerdantHuntStage==4&&gm.RawMeat==meat-1,"cook hunt meal through Interact");
    int gold=gm.Gold,gems=gm.Gems;h.WarpTo(gm.VerdantHuntTarget+Vector3.back*2);gm.Interact();Check(gm.VerdantHuntStage==5&&gm.Gold==gold+120&&gm.Gems==gems+5&&gm.UnlockedWeapons.Contains(4),"complete hunt with advertised reward");gm.Interact();Check(gm.Gold==gold+120&&gm.Gems==gems+5,"reward cannot repeat");
   }finally{h.TouchMoveInput=Vector2.zero;h.LockOnTarget=null;Busy=false;}
  }
  public static void Routines(){Busy=true;OpenWorldManager.I.StartCoroutine(RoutineTour());}
  static IEnumerator RoutineTour(){
   var gm=OpenWorldManager.I;GameObject fixture=null,wall=null;
   try{
    var smith=gm.NPCs.Find(n=>n&&n.NpcId=="brom");Check(smith&&smith.GetComponent<OpenWorldNpcRoutine>(),"named villagers have routines");
    foreach(float at in new[]{6f,12f,19f,23f}){OpenWorldAtmosphere.Instance.SetHour(at,false,false);gm.Hero.WarpTo(smith.transform.position+Vector3.back*15);yield return new WaitForSeconds(1.2f);var routine=smith.GetComponent<OpenWorldNpcRoutine>();Check(routine.Phase==OpenWorldNpcRoutine.PhaseAt(at),"clock drives NPC phase at "+at);File.AppendAllText(Validation,"Smith "+at+"h: "+routine.Activity+" at "+smith.transform.position+", target="+routine.Destination+", clip="+smith.Visual.CurrentState+"\n");}
    var rowan=gm.NPCs.Find(n=>n&&n.NpcId=="rowan");gm.Hero.WarpTo(rowan.transform.position+Vector3.back*2);rowan.Converse(gm);rowan.Converse(gm);string first=gm.CurrentBanner;rowan.Converse(gm);Check(gm.CurrentBanner!=first,"repeat conversations vary with routine context");
    Vector3 before=rowan.transform.position;yield return new WaitForSeconds(1);Check(Vector3.Distance(before,rowan.transform.position)<.02f,"conversation pauses routine movement");
    OpenWorldAtmosphere.Instance.SetHour(12,false,false);Vector3 p=gm.HuntGround(new Vector3(61,0,25));var npc=OpenWorldNPC.Spawn("marek-caravan","Routine fixture","Testing","Hello",0,-1,p,p+Vector3.forward*3,gm.WorldRoot);fixture=npc.gameObject;
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=p+new Vector3(0,1,1.2f);wall.transform.localScale=new Vector3(5,2,.35f);Physics.SyncTransforms();gm.Hero.WarpTo(p+Vector3.back*12);yield return new WaitForSeconds(2);
    Check(npc.transform.position.z<p.z+.8f,"routine does not walk through solid wall");
    Check(!OpenWorldNpcRoutine.Supports("glm-q-sled"),"regional quest givers keep their authored locations");
   }finally{if(fixture)UnityEngine.Object.Destroy(fixture);if(wall)UnityEngine.Object.Destroy(wall);Busy=false;}
  }
  public static void Trail(){Busy=true;OpenWorldManager.I.StartCoroutine(TrailTour());}
  static IEnumerator TrailTour(){
   var gm=OpenWorldManager.I;
   try{
    gm.Hero.WarpTo(OpenWorldHuntingDiscoveries.NoteAnchor);yield return new WaitForSeconds(1);
    Check(Shader.GetGlobalFloat("_LR_ClearingCount")==3,"three localized foliage clearings active");
    Check(UnityEngine.Object.FindObjectsByType<OpenWorldTrailPresentation>().Length==1,"one streamed trail presentation");
    Check(gm.ActiveTerrain.terrainData==Resources.Load<TerrainData>(OpenWorldRealm.Folder+"RealmTerrain"),"baked terrain data retained without runtime mutation/copy");
    for(int i=0;i<3;i++){VerdantExperienceValidation.ViewDiscovery(i.ToString());var cam=Camera.main;var site=UnityEngine.Object.FindObjectsByType<OpenWorldDiscoverySite>().First(s=>s.Kind==i);if(i==1){var focus=site.transform.position+Vector3.up*.7f;cam.transform.position=focus+new Vector3(2,1.7f,-3.5f);cam.transform.LookAt(focus);}yield return null;ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/living_trail_"+i+".png");yield return null;}
    gm.Hero.WarpTo(new Vector3(-220,0,-180));yield return new WaitForSeconds(2);File.AppendAllText(Validation,"Trail teardown: hero="+gm.Hero.transform.position+", realm="+gm.RealmActive+", presentations="+UnityEngine.Object.FindObjectsByType<OpenWorldTrailPresentation>().Length+", count="+Shader.GetGlobalFloat("_LR_ClearingCount")+"\n");Check(Shader.GetGlobalFloat("_LR_ClearingCount")==0,"local clearing shader state resets on streaming out");
    gm.CameraRig.enabled=true;
   }finally{Busy=false;}
  }
  public static void Trace(string label){Busy=true;OpenWorldManager.I.StartCoroutine(Route(label));}
  static IEnumerator Route(string label){
   var gm=OpenWorldManager.I;var h=gm.Hero;var old=OpenWorldGraphics.Instance.Current;
   var probe=gm.gameObject.AddComponent<OpenWorldPerformanceProbe>();string path=Dir+"Codex_Living_"+label+".md";
   File.WriteAllText(path,"# Living Verdant "+label+" — "+DateTime.Now+"\nDesktop Editor, High target 60; not Android.\n\n| Scene | seconds | frames | mean | p50 | p95 | p99 | max | >50ms | used start/end MiB | peak | reserved | Mono | main p95 | alloc p95 KiB | gc0 |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|\n");
   var markers=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(markers);var native=new List<ProfilerRecorder>();var names=new List<string>();var slow=new List<string>();
   foreach(var handle in markers){var desc=ProfilerRecorderHandle.GetDescription(handle);if(desc.Name=="Gfx.WaitForPresentOnGfxThread"||desc.Name=="GC.Collect"||desc.Name=="Shader.CreateGPUProgram"||desc.Name=="Gfx.WaitForGfxCommandsFromMainThread"||desc.Name=="Mesh.UploadMeshData"){native.Add(ProfilerRecorder.StartNew(desc.Category,desc.Name,1));names.Add(desc.Name);}}
   double lastFrame=Time.realtimeSinceStartupAsDouble;
   try{
    OpenWorldGraphics.Instance.Apply(OpenWorldGraphics.Tier.High,false);h.WarpTo(new Vector3(44,0,42));gm.CameraRig.SnapToTarget();OpenWorldHero.DebugAction("god");yield return new WaitForSeconds(3);
    probe.Begin("Hunt route",35,path);bool outbound=true;float travel=0;Vector3 last=h.transform.position;
    while(probe.Busy){double now=Time.realtimeSinceStartupAsDouble;double frame=(now-lastFrame)*1000;lastFrame=now;if(frame>50&&slow.Count<40){string row="wall "+frame.ToString("F2")+" ms";for(int ni=0;ni<native.Count;ni++)if(native[ni].Valid)row+="; "+names[ni]+"="+(native[ni].LastValue/1000000.0).ToString("F2")+" ms";slow.Add(row);}var target=outbound?new Vector3(128,0,64):new Vector3(44,0,42);var to=target-h.transform.position;to.y=0;if(to.magnitude<3)outbound=!outbound;
     var forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);h.TouchMoveInput=new Vector2(Vector3.Dot(to.normalized,right),Vector3.Dot(to.normalized,forward))*.8f;travel+=Vector3.Distance(last,h.transform.position);last=h.transform.position;yield return null;}
    h.TouchMoveInput=Vector2.zero;File.AppendAllText(path,"\nTravelled "+travel.ToString("F1")+" m.\n\n| Work (nested scopes; do not sum) | Calls | Total ms | Max ms | Items |\n|---|---:|---:|---:|---:|\n");
    foreach(OpenWorldStreamingDiagnostics.Work work in Enum.GetValues(typeof(OpenWorldStreamingDiagnostics.Work))){var rows=Enumerable.Range(0,OpenWorldStreamingDiagnostics.Count).Select(OpenWorldStreamingDiagnostics.Get).Where(r=>r.Kind==work).ToArray();File.AppendAllText(path,$"| {work} | {rows.Length} | {rows.Sum(r=>r.Milliseconds):F2} | {(rows.Length>0?rows.Max(r=>r.Milliseconds):0):F2} | {rows.Sum(r=>r.Items)} |\n");}
    File.AppendAllText(path,"\nNative markers available: "+string.Join(", ",names)+". First wall sample includes settle and is excluded from frame conclusions.\n"+string.Join("\n",slow)+"\n\nSlowest measured work (includes startup):\n");foreach(var row in Enumerable.Range(0,OpenWorldStreamingDiagnostics.Count).Select(OpenWorldStreamingDiagnostics.Get).OrderByDescending(r=>r.Milliseconds).Take(12))File.AppendAllText(path,$"- {row.Kind} {row.Milliseconds:F2} ms, {row.Items} items, realtime {row.At:F2}.\n");
   }finally{foreach(var recorder in native)recorder.Dispose();h.TouchMoveInput=Vector2.zero;OpenWorldGraphics.Instance.Apply(old,false);UnityEngine.Object.Destroy(probe);Busy=false;}
  }
 }
}

