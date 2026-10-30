using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LostRealmsOW {
 public static class RegionDiscoveryValidation {
  public static bool Busy{get;private set;}
  public static string Report{get;private set;}
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);File.AppendAllText(Report,"PASS: "+message+"\n");}
  public static void Begin(){
   Report="Docs/QA/Regional_discoveries_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt";Directory.CreateDirectory("Docs/QA");
   File.WriteAllText(Report,"Regional discoveries — "+DateTime.Now+"\n");VerdantExperienceValidation.Begin();Busy=true;OpenWorldManager.I.StartCoroutine(Run());
  }
  static Vector3 Approach(OpenWorldManager gm,RegionDiscoveryTag tag){
   for(int i=0;i<16;i++){
    float a=i*Mathf.PI/8;var p=gm.HuntGround(tag.transform.position+new Vector3(Mathf.Sin(a),0,-Mathf.Cos(a))*2.6f);
    if(OpenWorldTerrainSculptor.WaterAt(p.x,p.z,out _,out _)||Physics.OverlapCapsule(p+Vector3.up*.5f,p+Vector3.up*1.35f,.35f,~0,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&!c.GetComponentInParent<OpenWorldHero>()))continue;
    if(Physics.Linecast(p+Vector3.up,tag.transform.position+Vector3.up*.9f,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(gm.Hero.transform))continue;
    return p;
   }
   throw new Exception("No accessible approach to "+tag.Id);
  }
  static IEnumerator Run(){
   var gm=OpenWorldManager.I;var hero=gm.Hero;var camera=Camera.main;float fov=camera.fieldOfView;bool rig=gm.CameraRig.enabled;GameObject wall=null;
   try{
    gm.CollectedWorldIds.RemoveWhere(id=>id.StartsWith("glm-disc:"));OpenWorldHero.DebugAction("god");OpenWorldMobileHud.DebugUI("hud");
    var data=OpenWorldMobileHud.RegionalDiscoveries;Check(data.Count==8&&data.Select(d=>d.Id).Distinct().Count()==8,"eight unique discoveries across four regions");
    foreach(var disc in data){
     hero.WarpTo(gm.HuntGround(disc.Position));gm.CameraRig.enabled=true;gm.CameraRig.SnapToTarget();
     RegionDiscoveryTag tag=null;float until=Time.realtimeSinceStartup+25;
     while(Time.realtimeSinceStartup<until){tag=UnityEngine.Object.FindObjectsByType<RegionDiscoveryTag>(FindObjectsInactive.Include).FirstOrDefault(t=>t.Id==disc.Id);if(tag&&tag.Ready)break;yield return null;}
     Check(tag&&tag.Ready,"prepared prop ready: "+disc.Id+" ("+(tag?tag.PreparationState:"no tag")+")");yield return new WaitForSecondsRealtime(2);
     var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(tag.Model.transform);float ground=gm.ActiveTerrain.SampleHeight(bounds.center)+gm.ActiveTerrain.transform.position.y;
     File.AppendAllText(Report,$"SITE {disc.Id} anchor {disc.Position}, placed {tag.transform.position}, prop bottom {bounds.min.y:F3}, centre ground {ground:F3}\n");
     Check(!OpenWorldTerrainSculptor.WaterAt(tag.transform.position.x,tag.transform.position.z,out _,out _)&&bounds.min.y-ground>=-.08f&&bounds.min.y-ground<.22f,"dry grounded prop: "+disc.Id);
     Check(tag.Model.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial&&r.sharedMaterial.shader&&!r.sharedMaterial.shader.name.Contains("InternalError")),"valid prop materials: "+disc.Id);
     Check(disc.Cache||tag.GetComponentInChildren<OpenWorldTrailNote>(),"note has physical raised parchment: "+disc.Id);
     hero.WarpTo(Approach(gm,tag));yield return new WaitForSecondsRealtime(.2f);
     Check(gm.InteractionPrompt==(disc.Cache?"OPEN CACHE":"READ NOTE"),"normal interaction prompt available: "+disc.Id);
     gm.CameraRig.enabled=false;var focus=tag.transform.position+Vector3.up;camera.fieldOfView=48;Vector3 facing=Vector3.ProjectOnPlane(hero.transform.position-tag.transform.position,Vector3.up).normalized;Vector3 eye=hero.transform.position+Vector3.up*1.8f;
     for(int v=0;v<8;v++){var direction=Quaternion.Euler(0,v*45,0)*facing;var candidate=focus+direction*4.8f+Vector3.up*1.7f;var ray=candidate-focus;if(Physics.RaycastAll(focus,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(tag.transform)&&!h.transform.IsChildOf(hero.transform)))continue;eye=candidate;break;}
     camera.transform.position=eye;camera.transform.LookAt(focus);
     yield return new WaitForSecondsRealtime(.2f);ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/discovery_"+disc.Id.Substring(9)+".png");yield return null;
     int gold=gm.Gold,gems=gm.Gems;
     if(disc.Id==data[0].Id){
      wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary discovery cover check";wall.transform.position=(hero.transform.position+tag.transform.position)*.5f+Vector3.up*1.4f;wall.transform.rotation=Quaternion.LookRotation(tag.transform.position-hero.transform.position);wall.transform.localScale=new Vector3(2.5f,3,.25f);Physics.SyncTransforms();
      Check(!OpenWorldMobileHud.Instance.TryDiscover(gm)&&gm.Gold==gold&&gm.Gems==gems,"solid cover blocks discovery interaction");wall.SetActive(false);UnityEngine.Object.Destroy(wall);wall=null;Physics.SyncTransforms();
     }
     gm.Interact();
     Check(gm.CollectedWorldIds.Contains(disc.Id)&&gm.Gold==gold+disc.Gold&&gm.Gems==gems+disc.Gems,"normal interaction pays exact reward once: "+disc.Id);
     Check(gm.CurrentBanner.Contains(disc.Note)&&OpenWorldQuestJournal.Instance.Dialogue().Any(line=>line.Contains(disc.Note)),"lore displayed and retained in journal: "+disc.Id);
     Check(!OpenWorldMobileHud.Instance.TryDiscover(gm)&&gm.Gold==gold+disc.Gold&&gm.Gems==gems+disc.Gems,"repeat reward refused: "+disc.Id);
    }
    var store=new OpenWorldSaveStore(Path.GetFullPath("Temp/DiscoveryValidation/"+DateTime.UtcNow.Ticks));store.Write(gm.CaptureAdventure());var saved=store.Load();
    Check(data.All(d=>saved.collected.Contains(d.Id)),"all eight flags survive disk save/load");gm.RestoreAdventure(saved);yield return null;
    Check(!OpenWorldMobileHud.Instance.TryDiscover(gm),"restored save refuses duplicate collection");
    hero.WarpTo(gm.HuntGround(Vector3.zero));yield return new WaitForSecondsRealtime(1);
    Check(UnityEngine.Object.FindObjectsByType<RegionDiscoveryTag>(FindObjectsInactive.Include).All(t=>!t.gameObject.activeInHierarchy),"distant discovery props deactivate");
    File.AppendAllText(Report,"COMPLETE: all eight discoveries, access, grounding, lore, rewards, persistence and distance checks passed. Scripted editor checks; no Android build.\n");
   }finally{
    if(wall)UnityEngine.Object.Destroy(wall);gm.CameraRig.enabled=rig;camera.fieldOfView=fov;Busy=false;
   }
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
