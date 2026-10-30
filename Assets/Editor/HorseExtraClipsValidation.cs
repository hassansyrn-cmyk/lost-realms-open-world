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
  static bool diagnostic,ridingOnly;
  static string reportPath;
  static string Report=>reportPath;
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  static void LogSkate(string message,string trace,LogType type){if(message.StartsWith("[Review] skate "))File.AppendAllText(Report,message+"\n");}
  public static void Begin(){Start(false);}public static void Diagnose(){Start(true);}public static void Riding(){Start(false,true);}
  static void Start(bool diag,bool ride=false){diagnostic=diag;ridingOnly=ride;reportPath="Docs/HORSE_EXTRAS_"+(diag?"DIAG_":"")+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt";VerdantExperienceValidation.Begin();OpenWorldMobileHud.DebugUI("hud");File.WriteAllText(Report,"Horse extras + discovery polish "+DateTime.Now+"\n");Busy=true;OpenWorldManager.I.StartCoroutine(Guard(Run()));}
  // Flatten nested iterators so a failed child check still disposes the outer fixture and releases Busy.
  static IEnumerator Guard(IEnumerator routine){
   var stack=new Stack<IEnumerator>();stack.Push(routine);
   try{
    while(stack.Count>0){
     object next=null;bool more=false;Exception failure=null;
     try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){failure=e;}
     if(failure!=null){File.AppendAllText(Report,"FAIL: "+failure.Message+"\n");Debug.LogException(failure);yield break;}
     if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
     if(next is IEnumerator nested){stack.Push(nested);continue;}
     yield return next;
    }
   }finally{while(stack.Count>0){try{(stack.Pop() as IDisposable)?.Dispose();}catch(Exception e){Debug.LogException(e);}}Busy=false;}
  }
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
   var gm=OpenWorldManager.I;var hero=gm.Hero;Follow(horse);
   Vector3 f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized,r=Vector3.Cross(Vector3.up,f),direction=Quaternion.Euler(0,turn*Time.deltaTime,0)*horse.transform.forward;
   hero.TouchMoveInput=new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f))*magnitude;hero.TouchSprintHeld=gallop;OpenWorldHero.DebugAlwaysFocused=true;
  }
  static IEnumerator Circle(OpenWorldAnimalInteraction horse,bool gallop,int sign){
   var gm=OpenWorldManager.I;var motion=horse.GetComponentInChildren<OpenWorldImportedModel>();string label=(gallop?"gallop":"walk")+(sign>0?"_r":"_l");
   float until=Time.time+3;while(Time.time<until){Steer(horse,gallop?1:.45f,gallop,sign*45);yield return null;}
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var toes=(Transform[])typeof(OpenWorldImportedModel).GetField("legHooves",flags).GetValue(motion);var plants=(bool[])typeof(OpenWorldImportedModel).GetField("isPlanted",flags).GetValue(motion);
   var previous=toes.Select(t=>t.position).ToArray();float previousTime=Time.time;var trace=new System.Text.StringBuilder("time,foot,phase,height,speed,planted,x,y,z\n");var phaseMethod=typeof(OpenWorldImportedModel).GetMethod("StridePhase",flags);var slotField=typeof(OpenWorldImportedModel).GetField("current",flags);
   var low=Enumerable.Repeat(float.PositiveInfinity,4).ToArray();var high=Enumerable.Repeat(float.NegativeInfinity,4).ToArray();var contacts=new int[4];
   if(diagnostic){var ankles=(Transform[])typeof(OpenWorldImportedModel).GetField("legAnkles",flags).GetValue(motion);File.AppendAllText(Report,"Leg chains: "+string.Join("; ",ankles.Select(a=>a.name+" / "+a.parent.name+" / "+a.parent.parent.name))+"\n");}
   ClaudeReviewChecks.SkateStart("");if(!diagnostic)OpenWorldMotionCapture.Sequence("extra_"+label+" 30 6 left");int selected=0,frames=0;float peak=0;until=Time.time+(diagnostic?3:5);
   while(Time.time<until){Steer(horse,gallop?1:.45f,gallop,sign*45);if(motion.PlayingState==label)selected++;peak=Mathf.Max(peak,horse.Speed);frames++;yield return new WaitForEndOfFrame();for(int f=0;f<4;f++){float y=toes[f].position.y-OpenWorldMaterialHelper.SampleGroundHeight(toes[f].position,horse.transform);low[f]=Mathf.Min(low[f],y);high[f]=Mathf.Max(high[f],y);if(plants[f])contacts[f]++;var point=toes[f].position;var delta=point-previous[f];delta.y=0;float velocity=Time.time>previousTime+.0001f?delta.magnitude/(Time.time-previousTime):0;trace.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0:F4},{1},{2:F4},{3:F4},{4:F4},{5},{6:F4},{7:F4},{8:F4}\n",Time.time,f,phaseMethod.Invoke(motion,new object[]{slotField.GetValue(motion)}),y,velocity,plants[f]?1:0,point.x,point.y,point.z);previous[f]=point;}previousTime=Time.time;}File.WriteAllText(Report+"."+label+".csv",trace.ToString());
   File.AppendAllText(Report,"Contacts "+label+": "+string.Join("; ",Enumerable.Range(0,4).Select(i=>$"{toes[i].name} y {low[i]:F3}..{high[i]:F3}, planted {contacts[i]}/{frames}"))+"\n");
   ClaudeReviewChecks.SkateReport("extra_"+label);File.AppendAllText(Report,label+": active "+selected+"/"+frames+", peak "+peak.ToString("F2")+" m/s\n");Check(selected>frames*.7f,"turning circle selects "+label);while(OpenWorldMotionCapture.Busy){Steer(horse,gallop?1:.45f,gallop,sign*45);yield return null;}
  }
  static void Follow(OpenWorldAnimalInteraction horse){
   OpenWorldManager.I.CameraRig.enabled=false;
   Camera.main.transform.position=horse.transform.position+new Vector3(0,4,-8);
   Camera.main.transform.LookAt(horse.transform.position+Vector3.up*1.3f);
  }
  // Set once, then only follow the camera: this exercises a still thumb rather than repeated drag events.
  static void Drive(OpenWorldAnimalInteraction horse,Vector3 direction,float magnitude,bool sprint){
   Follow(horse);var hero=OpenWorldManager.I.Hero;
   Vector3 forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized,right=Vector3.Cross(Vector3.up,forward);
   hero.TouchMoveInput=new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,forward))*magnitude;
   hero.TouchSprintHeld=sprint;
  }
  static IEnumerator Hold(OpenWorldAnimalInteraction horse,float seconds){
   float until=Time.time+seconds;while(Time.time<until){Follow(horse);yield return null;}
  }
  static IEnumerator Place(OpenWorldAnimalInteraction horse,Vector3 point,Vector3 direction){
   var hero=OpenWorldManager.I.Hero;hero.TouchMoveInput=Vector2.zero;hero.TouchSprintHeld=false;
   yield return Hold(horse,2.1f);
   var capsule=horse.GetComponent<CharacterController>();capsule.enabled=false;
   horse.transform.SetPositionAndRotation(OpenWorldManager.I.HuntGround(point),Quaternion.LookRotation(direction));capsule.enabled=true;
   yield return Hold(horse,.2f);
  }
  static IEnumerator RidingChecks(OpenWorldAnimalInteraction horse,Vector3 arena){
   var gm=OpenWorldManager.I;var hero=gm.Hero;GameObject wall=null;
   try{
    yield return Place(horse,arena+Vector3.back*10,Vector3.forward);
    Drive(horse,Vector3.forward,.45f,false);Vector2 stick=hero.TouchMoveInput;Vector3 begin=horse.transform.position;
    yield return Hold(horse,2);
    Check(hero.TouchMoveInput==stick&&horse.Speed>1&&Vector3.Distance(begin,horse.transform.position)>2,"still touch stick holds walking gait without repeated input events");
    yield return Place(horse,arena+Vector3.back*12,Vector3.forward);
    Drive(horse,Vector3.forward,1,true);yield return Hold(horse,3);
    Check(horse.Speed>7,"held sprint reaches gallop");
    float peak=horse.Speed,minimum=peak;Drive(horse,Vector3.back,1,true);float until=Time.time+2.8f;
    while(Time.time<until){Follow(horse);minimum=Mathf.Min(minimum,horse.Speed);yield return null;}
    File.AppendAllText(Report,$"Sharp reverse: start {peak:F2} m/s, minimum {minimum:F2}, final heading {horse.transform.eulerAngles.y:F1} degrees\n");
    Check(minimum<5&&Vector3.Angle(horse.transform.forward,Vector3.back)<35,"sharp gallop reversal brakes before completing turn");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/horse_polish_reverse.png");yield return null;
    yield return Place(horse,arena,Vector3.forward);
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary riding collision check";
    wall.transform.position=gm.HuntGround(arena+Vector3.forward*2.5f)+Vector3.up*1.5f;wall.transform.localScale=new Vector3(6,3,.4f);Physics.SyncTransforms();
    Drive(horse,Vector3.forward,.45f,false);yield return Hold(horse,3);
    Check(horse.transform.position.z<arena.z+2.1f&&horse.Speed<.5f,"solid obstacle blocks horse and brakes movement");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/horse_polish_obstacle.png");yield return null;wall.SetActive(false);UnityEngine.Object.Destroy(wall);wall=null;
    Physics.SyncTransforms();Vector3 low=Vector3.zero,high=Vector3.zero;float rise=0;
    for(int cx=-8;cx<=8;cx+=8)for(int cz=-8;cz<=8;cz+=8)for(int angle=0;angle<360;angle+=30){
     Vector3 centre=arena+new Vector3(cx,0,cz),direction=Quaternion.Euler(0,angle,0)*Vector3.forward,a=gm.HuntGround(centre-direction*7.5f),b=gm.HuntGround(centre+direction*7.5f);
     bool clear=true;for(int n=0;n<=15;n++){
      var q=gm.HuntGround(Vector3.Lerp(a,b,n/15f));
      if(OpenWorldTerrainSculptor.WaterAt(q.x,q.z,out _,out _)||Physics.OverlapSphere(q+Vector3.up*1.2f,.7f,~0,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(horse.transform)&&!c.GetComponentInParent<OpenWorldHero>())){clear=false;break;}
     }
     if(clear&&b.y-a.y>rise){rise=b.y-a.y;low=a;high=b;}
    }
    File.AppendAllText(Report,$"Slope lane: {low} -> {high}, rise {rise:F2} m\n");Check(rise>.25f,"dry unobstructed slope found for riding check");
    foreach(bool uphill in new[]{true,false}){
     Vector3 from=uphill?low:high,to=uphill?high:low,direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;
     yield return Place(horse,from,direction);begin=horse.transform.position;Drive(horse,direction,.75f,false);float deadline=Time.time+8;
     while(Vector3.Dot(horse.transform.position-begin,direction)<8&&Time.time<deadline){Follow(horse);yield return null;}
     float travelled=Vector3.Dot(horse.transform.position-begin,direction),dy=horse.transform.position.y-begin.y;
     File.AppendAllText(Report,$"Slope {(uphill?"up":"down")}: {travelled:F2} m, rise {dy:F2} m, end speed {horse.Speed:F2} m/s\n");
     Check(travelled>=8&&hero.IsMounted&&(uphill?dy>.15f:dy<-.15f),"rider traverses "+(uphill?"uphill":"downhill")+" on real terrain");
     ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/horse_polish_"+(uphill?"uphill":"downhill")+".png");yield return null;
    }
    yield return Place(horse,arena,Vector3.forward);hero.LeaveMount();yield return Hold(horse,1);
    Check(!hero.IsMounted&&!hero.IsDismounting&&hero.Controller.enabled&&!hero.IsGliding,"normal dismount restores foot control without glider");
    hero.WarpTo(gm.HuntGround(arena+Vector3.right*10));yield return null;begin=horse.transform.position;
    OpenWorldAnimalInteraction.CallHorse(hero);Check(horse.GetComponent<OpenWorldWildlife>().ModeName=="Summoned","whistle uses real horse summon behaviour");
    float limit=Time.time+12;while(horse.GetComponent<OpenWorldWildlife>().ModeName=="Summoned"&&Time.time<limit){Follow(horse);yield return null;}
    yield return Hold(horse,1);File.AppendAllText(Report,$"Whistle final distance {Vector3.Distance(hero.transform.position,horse.transform.position):F2} m; mode {horse.GetComponent<OpenWorldWildlife>().ModeName}\n");
    Check(horse.GetComponent<OpenWorldWildlife>().ModeName=="Idle"&&OpenWorldAnimalInteraction.Nearest(hero)==horse&&Vector3.Distance(begin,horse.transform.position)>5,"whistled horse approaches Aster and stops within mounting reach");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/horse_polish_whistle.png");yield return null;
    horse.Interact(gm);yield return Hold(horse,.8f);Check(hero.IsMounted&&!horse.JustTamed&&!horse.IsBucking,"arrived horse mounts through the normal interaction without another buck");
   }finally{hero.TouchMoveInput=Vector2.zero;hero.TouchSprintHeld=false;if(wall)UnityEngine.Object.Destroy(wall);}
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
    if(ridingOnly){yield return RidingChecks(horse,arena);yield break;}
    foreach(bool gallop in new[]{false,true})foreach(int sign in (diagnostic?new[]{-1}:new[]{-1,1})){
     gm.Hero.TouchMoveInput=Vector2.zero;gm.Hero.TouchSprintHeld=false;yield return new WaitForSeconds(2);var capsule=fixture.GetComponent<CharacterController>();capsule.enabled=false;var start=gm.HuntGround(arena+new Vector3(-sign*(gallop?8.5f:2),0,gallop?6.5f:0));fixture.transform.SetPositionAndRotation(start,Quaternion.identity);capsule.enabled=true;yield return null;
     yield return Circle(horse,gallop,sign);
    }
    if(diagnostic)yield break;
    Steer(horse,0,false,0);yield return null;Check(motion.State=="runstop","release from gallop starts authored stop");OpenWorldMotionCapture.Sequence("extra_stop 36 2 left");while(OpenWorldMotionCapture.Busy)yield return null;yield return new WaitForSeconds(.1f);Check(horse.Speed<.15f&&motion.State=="idle","stop finishes into stationary idle");
    gm.Hero.LeaveMount(true);yield return null;gm.Hero.WarpTo(fixture.transform.position+Vector3.back*2);yield return new WaitForSeconds(.3f);gm.Hero.EnterMount(horse);yield return new WaitForSeconds(.8f);Check(!horse.JustTamed&&!horse.IsBucking,"remount of tamed horse does not replay buck");
    yield return RidingChecks(horse,arena);
   }finally{
    gm.Hero.TouchMoveInput=Vector2.zero;gm.Hero.TouchSprintHeld=false;if(gm.Hero.IsMounted)gm.Hero.LeaveMount(true);if(fixture)UnityEngine.Object.Destroy(fixture);gm.CameraRig.enabled=rig;camera.fieldOfView=fov;Application.logMessageReceived-=LogSkate;Busy=false;
   }
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
