using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace LostRealmsOW {
 public static class HorseExtraClipsValidation {
  public static bool Busy{get;private set;}
  static bool diagnostic;
  static string Report=>diagnostic?"Docs/HORSE_EXTRA_CONTACT_DIAG.txt":"Docs/HORSE_EXTRAS_2026-10-30.txt";
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  static void LogSkate(string message,string trace,LogType type){if(message.StartsWith("[Review] skate "))File.AppendAllText(Report,message+"\n");}
  public static void Begin(){Start(false);}public static void Diagnose(){Start(true);}
  static void Start(bool diag){diagnostic=diag;VerdantExperienceValidation.Begin();OpenWorldMobileHud.DebugUI("hud");File.WriteAllText(Report,"Horse extras + discovery polish "+DateTime.Now+"\n");Busy=true;OpenWorldManager.I.StartCoroutine(Run());}
  static Vector3 Arena(OpenWorldManager gm){
   Vector3 best=Vector3.zero;float score=float.MaxValue;
   for(int x=-150;x<=-20;x+=10)for(int z=-240;z<=-110;z+=10){
    var p=gm.HuntGround(new Vector3(x,0,z));float lo=p.y,hi=p.y;bool clear=true;
    for(int dx=-18;dx<=18&&clear;dx+=3)for(int dz=-18;dz<=18;dz+=3){
     var q=gm.HuntGround(p+new Vector3(dx,0,dz));lo=Mathf.Min(lo,q.y);hi=Mathf.Max(hi,q.y);
     if(OpenWorldTerrainSculptor.WaterAt(q.x,q.z,out _,out _)||Physics.CheckSphere(q+Vector3.up*1.15f,1f,~0,QueryTriggerInteraction.Ignore)){clear=false;break;}
    }
    if(clear&&hi-lo<score){score=hi-lo;best=p;}
   }
   Check(best!=Vector3.zero,"found dry unobstructed riding circuit at "+best+", ground height range "+score.ToString("F2"));return best;
  }
  static void Steer(OpenWorldAnimalInteraction horse,float magnitude,bool gallop,float turn){
   var gm=OpenWorldManager.I;var hero=gm.Hero;gm.CameraRig.enabled=false;Camera.main.transform.position=horse.transform.position+new Vector3(0,4,-8);Camera.main.transform.LookAt(horse.transform.position+Vector3.up*1.3f);
   Vector3 f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized,r=Vector3.Cross(Vector3.up,f),direction=Quaternion.Euler(0,turn*Time.deltaTime,0)*horse.transform.forward;
   hero.TouchMoveInput=new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f))*magnitude;hero.TouchSprintHeld=gallop;OpenWorldHero.DebugAlwaysFocused=true;
  }
  static IEnumerator Circle(OpenWorldAnimalInteraction horse,bool gallop,int sign){
   var gm=OpenWorldManager.I;var motion=horse.GetComponentInChildren<OpenWorldImportedModel>();string label=(gallop?"gallop":"walk")+(sign>0?"_r":"_l");
   float until=Time.time+3;while(Time.time<until){Steer(horse,gallop?1:.45f,gallop,sign*45);yield return null;}
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var toes=(Transform[])typeof(OpenWorldImportedModel).GetField("legHooves",flags).GetValue(motion);var plants=(bool[])typeof(OpenWorldImportedModel).GetField("isPlanted",flags).GetValue(motion);
   var low=Enumerable.Repeat(float.PositiveInfinity,4).ToArray();var high=Enumerable.Repeat(float.NegativeInfinity,4).ToArray();var contacts=new int[4];
   if(diagnostic){var ankles=(Transform[])typeof(OpenWorldImportedModel).GetField("legAnkles",flags).GetValue(motion);File.AppendAllText(Report,"Leg chains: "+string.Join("; ",ankles.Select(a=>a.name+" / "+a.parent.name+" / "+a.parent.parent.name))+"\n");}
   ClaudeReviewChecks.SkateStart("");if(!diagnostic)OpenWorldMotionCapture.Sequence("extra_"+label+" 30 6 left");int selected=0,frames=0;float peak=0;until=Time.time+(diagnostic?3:5);
   while(Time.time<until){Steer(horse,gallop?1:.45f,gallop,sign*45);if(motion.PlayingState==label)selected++;peak=Mathf.Max(peak,horse.Speed);frames++;yield return new WaitForEndOfFrame();for(int f=0;f<4;f++){float y=toes[f].position.y-OpenWorldMaterialHelper.SampleGroundHeight(toes[f].position,horse.transform);low[f]=Mathf.Min(low[f],y);high[f]=Mathf.Max(high[f],y);if(plants[f])contacts[f]++;}}
   File.AppendAllText(Report,"Contacts "+label+": "+string.Join("; ",Enumerable.Range(0,4).Select(i=>$"{toes[i].name} y {low[i]:F3}..{high[i]:F3}, planted {contacts[i]}/{frames}"))+"\n");
   ClaudeReviewChecks.SkateReport("extra_"+label);File.AppendAllText(Report,label+": active "+selected+"/"+frames+", peak "+peak.ToString("F2")+" m/s\n");Check(selected>frames*.7f,"turning circle selects "+label);while(OpenWorldMotionCapture.Busy){Steer(horse,gallop?1:.45f,gallop,sign*45);yield return null;}
  }
  static IEnumerator Run(){
   var gm=OpenWorldManager.I;GameObject fixture=null;var camera=Camera.main;bool rig=gm.CameraRig.enabled;float fov=camera.fieldOfView;Application.logMessageReceived+=LogSkate;
   try{
    Check(OpenWorldImportedModel.SelectTurnBank(29,0)==1&&OpenWorldImportedModel.SelectTurnBank(20,1)==1&&OpenWorldImportedModel.SelectTurnBank(13,1)==0&&OpenWorldImportedModel.SelectTurnBank(-29,1)==-1,"28/14 degree turn hysteresis and direction reversal");
    gm.Hero.WarpTo(OpenWorldHuntingDiscoveries.NoteAnchor);yield return new WaitForSeconds(1);gm.CameraRig.enabled=false;
    var sites=UnityEngine.Object.FindObjectsByType<OpenWorldDiscoverySite>();var note=sites.First(s=>s.Kind==0);var cache=sites.First(s=>s.Kind==1);Check(note.GetComponentInChildren<OpenWorldTrailNote>(),"raised parchment replaces ground square without new UI");
    Check(cache.GetComponentsInChildren<MeshFilter>().Count(m=>m.transform.parent&&m.transform.parent.name.StartsWith("Split stone"))>=2||cache.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Split stone"))==2,"two prepared world-kit rocks frame the cache");
    for(int i=0;i<2;i++){var site=i==0?note:cache;gm.Hero.WarpTo(site.transform.position+Vector3.back*2.5f);var focus=site.transform.position+Vector3.up*(i==0?1.15f:.7f);camera.fieldOfView=48;camera.transform.position=focus+new Vector3(1.8f,1.15f,-4.2f);camera.transform.LookAt(focus);yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/discovery_polish_"+i+".png");yield return null;}
    gm.Hero.WarpTo(note.transform.position+Vector3.back*2);OpenWorldHuntingDiscoveries.Instance.Interact(note);Check(gm.CollectedWorldIds.Contains(OpenWorldHuntingDiscoveries.NoteId),"raised note retains original interaction");
    gm.CollectedWorldIds.Remove(OpenWorldHuntingDiscoveries.CacheId);cache.Refresh(gm);gm.Hero.WarpTo(cache.transform.position+Vector3.back*1.5f);int gold=gm.Gold;OpenWorldHuntingDiscoveries.Instance.Interact(cache);Check(gm.Gold==gold+35,"cache remains accessible from southern opening");
    Vector3 arena=Arena(gm);gm.Hero.WarpTo(arena);gm.CameraRig.enabled=true;gm.CameraRig.SnapToTarget();yield return new WaitForSeconds(1);
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/wildlife_a868c1dd18.prefab");Check(prefab,"prepared horse exists");fixture=new GameObject("Extra clips wild horse fixture");fixture.transform.position=arena;var model=UnityEngine.Object.Instantiate(prefab,fixture.transform);var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);model.transform.localScale*=2.2f/bounds.size.y;bounds=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);model.transform.position+=Vector3.up*(arena.y-bounds.min.y);
    var wildlife=fixture.AddComponent<OpenWorldWildlife>();wildlife.Nature=OpenWorldWildlife.Temper.Skittish;var horse=OpenWorldAnimalInteraction.Attach(fixture,"Wild Horse","qa:horse-extras");yield return null;wildlife.enabled=false;
    var motion=fixture.GetComponentInChildren<OpenWorldImportedModel>();fixture.GetComponentInChildren<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;Check(motion.WalkLeft&&motion.WalkRight&&motion.GallopLeft&&motion.GallopRight&&motion.RunStop&&motion.Buck&&motion.Swim&&motion.Jump,"all eight additional source clips prepared");Check(motion.NaturalSpeed("walk_l")==motion.NaturalSpeed("walk")&&motion.NaturalSpeed("gallop_r")==motion.NaturalSpeed("gallop"),"banked clips share straight gait natural speed");
    gm.Hero.WarpTo(arena+Vector3.back*2);yield return new WaitForSeconds(.3f);gm.Hero.EnterMount(horse);Check(gm.Hero.IsMounted&&horse.JustTamed,"first wild mount accepted");OpenWorldMotionCapture.Sequence("extra_buck 40 3 left");Vector3 held=fixture.transform.position;bool sawBuck=false,stayedMounted=true;float timeout=Time.time+10;
    while(horse.ControlsLocked&&Time.time<timeout){OpenWorldHero.DebugAlwaysFocused=true;gm.Hero.TouchMoveInput=Vector2.up;if(motion.PlayingState=="buck")sawBuck=true;gm.Hero.LeaveMount();if(!gm.Hero.IsMounted){stayedMounted=false;break;}yield return null;}
    gm.Hero.TouchMoveInput=Vector2.zero;Check(stayedMounted,"dismount blocked during initial mount/buck");Check(sawBuck&&Vector2.Distance(new Vector2(held.x,held.z),new Vector2(fixture.transform.position.x,fixture.transform.position.z))<.06f&&!horse.ControlsLocked,"buck plays once and movement stays locked until complete");while(OpenWorldMotionCapture.Busy)yield return null;
    foreach(bool gallop in new[]{false,true})foreach(int sign in (diagnostic?new[]{-1}:new[]{-1,1})){
     gm.Hero.TouchMoveInput=Vector2.zero;gm.Hero.TouchSprintHeld=false;yield return new WaitForSeconds(2);var capsule=fixture.GetComponent<CharacterController>();capsule.enabled=false;var start=gm.HuntGround(arena+new Vector3(-sign*(gallop?8.5f:2),0,gallop?6.5f:0));fixture.transform.SetPositionAndRotation(start,Quaternion.identity);capsule.enabled=true;yield return null;
     yield return Circle(horse,gallop,sign);
    }
    if(diagnostic)yield break;
    Steer(horse,0,false,0);yield return null;Check(motion.State=="runstop","release from gallop starts authored stop");OpenWorldMotionCapture.Sequence("extra_stop 36 2 left");while(OpenWorldMotionCapture.Busy)yield return null;yield return new WaitForSeconds(.1f);Check(horse.Speed<.15f&&motion.State=="idle","stop finishes into stationary idle");
    gm.Hero.LeaveMount(true);yield return null;gm.Hero.WarpTo(fixture.transform.position+Vector3.back*2);yield return new WaitForSeconds(.3f);gm.Hero.EnterMount(horse);yield return new WaitForSeconds(.8f);Check(!horse.JustTamed&&!horse.IsBucking,"remount of tamed horse does not replay buck");
   }finally{
    gm.Hero.TouchMoveInput=Vector2.zero;gm.Hero.TouchSprintHeld=false;if(gm.Hero.IsMounted)gm.Hero.LeaveMount(true);if(fixture)UnityEngine.Object.Destroy(fixture);gm.CameraRig.enabled=rig;camera.fieldOfView=fov;Application.logMessageReceived-=LogSkate;Busy=false;
   }
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
