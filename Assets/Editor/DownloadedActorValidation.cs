using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace LostRealmsOW {
 public static class DownloadedActorValidation {
  const string Root="Assets/GameContent/DownloadedModels/";
  const string Report="Docs/AssetAudit/DOWNLOADED_ACTORS_VALIDATION.txt";
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static OpenWorldSaveData snapshot;static Vector3 position;static bool blocked,dirty;static float hour;
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  public static void Begin(){
   File.WriteAllText(Report,"Downloaded actor validation "+DateTime.Now+"\n");
   var gm=OpenWorldManager.I;snapshot=gm.CaptureAdventure();position=gm.Hero.transform.position;
   blocked=(bool)typeof(OpenWorldManager).GetField("saveBlocked",Private).GetValue(gm);dirty=(bool)typeof(OpenWorldManager).GetField("saveDirty",Private).GetValue(gm);
   typeof(OpenWorldManager).GetField("saveBlocked",Private).SetValue(gm,true);
   hour=OpenWorldAtmosphere.Instance.Hour;OpenWorldAtmosphere.Instance.SetHour(11,false,false);
   foreach(var id in new[]{"Civilian","Komodo","MountainDragon"})Motion(id);
  }
  static void Motion(string id){
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+id+"/Prepared/"+id+".prefab");Check(prefab,id+" prepared prefab exists");
   var fixture=UnityEngine.Object.Instantiate(prefab);fixture.transform.position=new Vector3(0,1000,0);
   var driver=fixture.GetComponent<OpenWorldImportedModel>();driver.enabled=false;var animator=fixture.GetComponentInChildren<Animator>();
   try{
    Check(animator,id+" animator exists"); animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
    foreach(var clip in new[]{driver.Idle,driver.Walk,driver.Talk,driver.Fly}.Where(c=>c).Distinct()){
     var graph=PlayableGraph.Create("Downloaded validation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
     try{
      var player=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"pose",animator);output.SetSourcePlayable(player);graph.Play();
      player.SetTime(.03);graph.Evaluate(.01f);
      var bones=fixture.GetComponentsInChildren<Transform>();var rotations=bones.Select(t=>t.localRotation).ToArray();
      var smr=fixture.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();Vector3[] vertices=null;
      if(id=="Komodo"){smr.BakeMesh(mesh);vertices=mesh.vertices;}
      player.SetTime(clip.length*.42);graph.Evaluate(.01f);
      float angle=bones.Select((t,i)=>Quaternion.Angle(t.localRotation,rotations[i])).Max();
      if(id=="Komodo"){
       smr.BakeMesh(mesh);float delta=mesh.vertices.Select((v,i)=>(v-vertices[i]).sqrMagnitude).Max();
       Check(clip==driver.Idle||delta>.00001f,id+" "+clip.name+" morph motion delta="+delta);UnityEngine.Object.DestroyImmediate(mesh);
      }else{UnityEngine.Object.DestroyImmediate(mesh);Check(angle>.05f,id+" "+clip.name+" bone motion="+angle);}
      Check(AnimationUtility.GetAnimationEvents(clip).Length==0,id+" "+clip.name+" has no inherited gameplay events");
     }finally{graph.Destroy();}
    }
    Check(fixture.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.name.StartsWith("Universal Render Pipeline"))),id+" URP materials valid");
   }finally{UnityEngine.Object.DestroyImmediate(fixture);}
  }
  public static void Travel(string id){var gm=OpenWorldManager.I;gm.CameraRig.enabled=true;Vector3 p=id=="Civilian"?new Vector3(91,0,-7):id=="Komodo"?new Vector3(319,0,143):new Vector3(10,0,378);gm.Hero.WarpTo(p);gm.CameraRig.SnapToTarget();}
  public static void Review(string id){
   var gm=OpenWorldManager.I;var actor=id=="Civilian"?gm.NPCs.Find(n=>n&&n.NpcId=="marek-caravan")?.gameObject:id=="Komodo"?GameObject.Find("Oasis Komodo"):GameObject.Find("Mountain Dragon • Dragon Peak");
   Check(actor,id+" spawned at intended region");
   var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(actor.transform);
   Check(float.IsFinite(bounds.size.y)&&bounds.size.y>.1f&&bounds.size.y<15,id+" finite world bounds "+bounds.size);
   File.AppendAllText(Report,id+" actual position="+actor.transform.position+" lowest renderer="+bounds.min.y+" terrain="+(gm.ActiveTerrain.SampleHeight(actor.transform.position)+gm.ActiveTerrain.transform.position.y)+"\n");
   var focus=id=="Civilian"?actor.transform.position+Vector3.up*1.05f:bounds.center;
   gm.CameraRig.enabled=false;var cam=Camera.main;cam.transform.position=focus+new Vector3(id=="MountainDragon"?13:3,id=="MountainDragon"?5:1,id=="MountainDragon"?18:4);cam.transform.LookAt(focus);cam.fieldOfView=45;
   var human=actor.GetComponentInChildren<Animator>();if(human&&human.isHuman)File.AppendAllText(Report,"Human Head="+human.GetBoneTransform(HumanBodyBones.Head).position+" feet="+human.GetBoneTransform(HumanBodyBones.LeftFoot).position+","+human.GetBoneTransform(HumanBodyBones.RightFoot).position+" model="+human.transform.position+"\n");
   if(human&&human.isHuman){var left=human.GetBoneTransform(HumanBodyBones.LeftFoot).position;var right=human.GetBoneTransform(HumanBodyBones.RightFoot).position;float sole=Mathf.Min(left.y-gm.ActiveTerrain.SampleHeight(left)-gm.ActiveTerrain.transform.position.y,right.y-gm.ActiveTerrain.SampleHeight(right)-gm.ActiveTerrain.transform.position.y);Check(sole>=.04f&&sole<.15f,"civilian planted foot clearance="+sole);}
   if(id=="Civilian")actor.GetComponent<OpenWorldNPC>().Converse(gm);
  }
  public static void Hunt(){var gm=OpenWorldManager.I;var actor=GameObject.Find("Oasis Komodo");Check(actor,"Komodo available for hunting");var animal=actor.GetComponent<OpenWorldAnimalInteraction>();gm.Hero.WarpTo(actor.transform.position+Vector3.back*.8f);int meat=gm.RawMeat;int hits=OpenWorldAnimalInteraction.Strike(gm.Hero.transform.position,Vector3.forward,1.5f,100,true);Check(hits>0&&animal.Dead,"real animal strike defeats Komodo");animal.Interact(gm);Check(gm.RawMeat==meat+2&&gm.CollectedWorldIds.Contains("downloaded:komodo:oasis"),"Komodo collection adds two meat and saved ID");animal.Interact(gm);Check(gm.RawMeat==meat+2,"Komodo cannot be collected twice");}
  public static void Streaming(){Check(!GameObject.Find("Oasis Komodo"),"Komodo despawns beyond its streaming radius");Check(!OpenWorldManager.I.NPCs.Any(n=>n&&n.NpcId=="marek-caravan"),"civilian unregisters beyond streaming radius");}
  public static void Cleanup(){if(snapshot==null)return;var gm=OpenWorldManager.I;gm.RestoreAdventure(snapshot);gm.Hero.WarpTo(position);gm.CameraRig.enabled=true;gm.CameraRig.SnapToTarget();OpenWorldAtmosphere.Instance.SetHour(hour,false,false);typeof(OpenWorldManager).GetField("saveBlocked",Private).SetValue(gm,blocked);typeof(OpenWorldManager).GetField("saveDirty",Private).SetValue(gm,dirty);snapshot=null;}
 }
}



