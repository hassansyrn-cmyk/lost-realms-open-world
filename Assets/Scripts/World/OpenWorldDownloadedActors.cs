using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace LostRealmsOW {
 // Separate from the horse population: three deliberately placed actors, each streamed independently.
 public sealed class OpenWorldDownloadedActors : MonoBehaviour {
  sealed class Site {
   public string Id; public Vector3 Anchor; public float Range; public GameObject Actor;
   public AsyncOperationHandle<GameObject> Handle; public bool Loading; public float Retry;
  }
  readonly Site[] sites={
   new Site{Id="Civilian",Anchor=new Vector3(91,0,-1),Range=75},
   new Site{Id="Komodo",Anchor=new Vector3(319,0,153),Range=85},
   new Site{Id="MountainDragon",Anchor=new Vector3(10,0,400),Range=165}
  };
  OpenWorldManager gm;Transform world;float nextTick;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(!FindAnyObjectByType<OpenWorldDownloadedActors>())new GameObject("Downloaded regional actors").AddComponent<OpenWorldDownloadedActors>();}
  void Update() {
   if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.5f;
   gm=OpenWorldManager.I;if(!gm||!gm.Hero||!gm.ActiveTerrain)return;
   if(world!=gm.WorldRoot){foreach(var s in sites)Clear(s);world=gm.WorldRoot;}
   if(!gm.RealmActive||gm.IsModalOpen||gm.Hero.Dead)return;
   foreach(var s in sites) {
    if(s.Id=="Komodo"&&gm.CollectedWorldIds.Contains("downloaded:komodo:oasis")){Clear(s);continue;}
    var delta=gm.Hero.transform.position-s.Anchor;delta.y=0;float d=delta.magnitude;
    if(s.Actor&&d>s.Range+35){Clear(s);continue;}
    if(!s.Actor&&!s.Loading&&d<s.Range&&Time.time>s.Retry)StartCoroutine(Spawn(s));
   }
  }
  IEnumerator Spawn(Site s) {
   s.Loading=true;s.Retry=Time.time+20;var expectedWorld=world;
   if(!s.Handle.IsValid())s.Handle=Addressables.LoadAssetAsync<GameObject>("ow.downloaded."+s.Id);
   var handle=s.Handle;yield return handle;
   s.Loading=false;
   if(!this||!gm||!world||world!=expectedWorld||!handle.IsValid()){Clear(s);yield break;}
   if(handle.Status!=AsyncOperationStatus.Succeeded||!handle.Result){Debug.LogWarning("[DownloadedActors] Missing matching content: "+s.Id);Clear(s);yield break;}
   var delta=gm.Hero.transform.position-s.Anchor;delta.y=0;if(delta.magnitude>s.Range+10){Clear(s);yield break;}
   if(s.Id=="MountainDragon") {
    s.Actor=Instantiate(handle.Result,world);s.Actor.name="Mountain Dragon • Dragon Peak";
    var flight=s.Actor.AddComponent<OpenWorldMountainDragonFlight>();flight.Centre=s.Anchor;flight.Terrain=gm.ActiveTerrain;flight.PositionNow();
   } else {
    if(!FindSite(s.Anchor,s.Id=="Civilian"?.55f:1.6f,out var p)){Debug.LogWarning("[DownloadedActors] No safe ground at "+s.Id);Clear(s);yield break;}
    if(s.Id=="Civilian") {
     // A short, clearance-checked market route avoids walking through streamed stalls. Conversation
     // drives the retargeted talk gesture; idle and walk remain available to future schedules.
     var npc=OpenWorldNPC.Spawn("marek-caravan","Marek","Caravan Provisioner","The oasis shelters sun-scaled lizards. Far north, keep an eye on the sky above Dragon Peak.",0,-1,p,PatrolEnd(p),world);
     npc.Visual.AdoptImportedModel(handle.Result,1.78f);
     var feet=npc.gameObject.AddComponent<OpenWorldDownloadedHumanoidGrounding>();feet.Model=npc.Visual.ModelInstance.transform;feet.Animator=npc.Visual.Animator;feet.Terrain=gm.ActiveTerrain;
     gm.NPCs.Add(npc);s.Actor=npc.gameObject;
    } else {
     s.Actor=new GameObject("Oasis Komodo");s.Actor.transform.SetParent(world,false);s.Actor.transform.position=p;s.Actor.transform.rotation=Quaternion.Euler(0,220,0);
     Instantiate(handle.Result,s.Actor.transform,false);
     var ai=s.Actor.AddComponent<OpenWorldWildlife>();ai.Nature=OpenWorldWildlife.Temper.Wary;ai.WalkSpeed=.75f;ai.RunSpeed=1.4f;ai.HomeRadius=6;ai.Awareness=7;ai.FleeDistance=12;ai.TurnRate=65;
     var interaction=OpenWorldAnimalInteraction.Attach(s.Actor,"Oasis Komodo","downloaded:komodo:oasis");interaction.Deer=true;interaction.Meat=2;
    }
   }
   Debug.Log("[DownloadedActors] Spawned "+s.Id+" at "+s.Actor.transform.position);
  }
  Vector3 PatrolEnd(Vector3 p) {
   for(int k=0;k<8;k++){var dir=Quaternion.Euler(0,k*45,0)*Vector3.forward;bool clear=true;for(int i=1;i<=12;i++){var q=p+dir*(i*.25f);q.y=gm.ActiveTerrain.SampleHeight(q)+gm.ActiveTerrain.transform.position.y;if(OpenWorldTerrainSculptor.WaterAt(q.x,q.z,out _,out _)||OpenWorldPopulation.Blocked(q,1.8f,.55f)||Mathf.Abs(q.y-p.y)>.25f){clear=false;break;}}if(clear)return p+dir*3;}return p;
  }
  bool FindSite(Vector3 anchor,float radius,out Vector3 result) {
   for(int i=0;i<48;i++){
    var p=anchor+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(i==0?0:1+i*.32f);
    p.y=gm.ActiveTerrain.SampleHeight(p)+gm.ActiveTerrain.transform.position.y;
    if(OpenWorldTerrainSculptor.WaterAt(p.x,p.z,out _,out _)||OpenWorldPopulation.Blocked(p,2,radius))continue;
    if(!OpenWorldGrounding.TryWalk(gm.ActiveTerrain,p,p+Vector3.right*.25f,out _))continue;
    // Require a flat footprint so the lizard's low belly does not intersect the dune.
    bool level=true;for(int k=0;k<8;k++){var q=p+Quaternion.Euler(0,k*45,0)*Vector3.forward*radius;if(Mathf.Abs(gm.ActiveTerrain.SampleHeight(q)+gm.ActiveTerrain.transform.position.y-p.y)>.3f){level=false;break;}}
    if(!level)continue;result=p;return true;
   }
   result=anchor;return false;
  }
  void Clear(Site s){if(s.Actor){var npc=s.Actor.GetComponent<OpenWorldNPC>();if(npc&&gm)gm.NPCs.Remove(npc);Destroy(s.Actor);}s.Actor=null;if(!s.Loading&&s.Handle.IsValid()){Addressables.Release(s.Handle);s.Handle=default;}}
  void OnDestroy(){StopAllCoroutines();foreach(var s in sites){s.Loading=false;Clear(s);}}
 }
 // Humanoid game motions have flattened body-root curves. The imported civilian needs its
 // own sole correction; do not alter the shared baked clips or the horse animation driver.
 [DefaultExecutionOrder(12000)]
 public sealed class OpenWorldDownloadedHumanoidGrounding : MonoBehaviour {
  public Transform Model;public Animator Animator;public Terrain Terrain;
  void LateUpdate(){if(!Model||!Animator||!Animator.isHuman||!Terrain)return;var l=Animator.GetBoneTransform(HumanBodyBones.LeftFoot);var r=Animator.GetBoneTransform(HumanBodyBones.RightFoot);if(!l||!r)return;float a=Terrain.SampleHeight(l.position)+Terrain.transform.position.y+.075f-l.position.y;float b=Terrain.SampleHeight(r.position)+Terrain.transform.position.y+.075f-r.position.y;Model.position+=Vector3.up*Mathf.Max(a,b);}
 }
 public sealed class OpenWorldMountainDragonFlight : MonoBehaviour {
  public Vector3 Centre;public Terrain Terrain;
  float phase;
  public void PositionNow(){phase=Time.time*.06f;Move();}
  void Update(){phase+=Time.deltaTime*.06f;Move();}
  void Move(){if(!Terrain)return;var p=Centre+new Vector3(Mathf.Cos(phase)*27,0,Mathf.Sin(phase)*27);p.y=Mathf.Max(Terrain.SampleHeight(p),Terrain.SampleHeight(Centre))+Terrain.transform.position.y+26+Mathf.Sin(phase*2)*2;transform.position=p;transform.rotation=Quaternion.LookRotation(new Vector3(-Mathf.Sin(phase),0,Mathf.Cos(phase)))*Quaternion.Euler(0,0,-7);}
 }
}



