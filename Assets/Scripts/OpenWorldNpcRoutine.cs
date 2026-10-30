using UnityEngine;
namespace LostRealmsOW {
 public sealed class OpenWorldNpcRoutine : MonoBehaviour {
  public enum DayPhase { Dawn, Work, Evening, Rest }
  public DayPhase Phase {get;private set;}
  public string Activity {get;private set;} = "At home";
  public Vector3 Destination {get;private set;}
  OpenWorldNPC npc;Vector3 home,patrol;int phase=-1,conversation;float nextPlan,nextWork,workUntil;bool excursion;
  readonly RaycastHit[] hits=new RaycastHit[16];
  public static bool Supports(string id)=>id=="brom"||id=="lyra"||id=="rowan"||id=="vane"||id=="kael"||id=="mira"||id=="marek-caravan";
  public static OpenWorldNpcRoutine Attach(OpenWorldNPC actor,Vector3 a,Vector3 b){if(!Supports(actor.NpcId))return null;var r=actor.gameObject.AddComponent<OpenWorldNpcRoutine>();r.npc=actor;r.home=a;r.patrol=b;r.Destination=a;return r;}
  public static DayPhase PhaseAt(float hour)=>hour>=5&&hour<8?DayPhase.Dawn:hour>=8&&hour<17?DayPhase.Work:hour>=17&&hour<21?DayPhase.Evening:DayPhase.Rest;
  public bool Tick(OpenWorldManager gm,float distance){
   if(!npc||!gm.ActiveTerrain)return false;
   var now=PhaseAt(OpenWorldAtmosphere.Instance?OpenWorldAtmosphere.Instance.Hour:12);Phase=now;
   if(distance>110){npc.Visual?.Play("idle");return true;}
   if(phase!=(int)now){phase=(int)now;excursion=false;nextPlan=0;workUntil=0;}
   Activity=now==DayPhase.Rest?"Resting nearby":now==DayPhase.Dawn?"Starting the day":now==DayPhase.Evening?"Evening rounds":npc.NpcId=="brom"?"At the forge":npc.NpcId=="rowan"?"Tending supplies":npc.NpcId=="lyra"?"Studying the wards":"On duty";
   if(Time.time>=nextPlan){
    nextPlan=Time.time+12;excursion=!excursion;
    Vector3 desired=home;
    if(now==DayPhase.Work)desired=excursion?patrol:home;
    if(now==DayPhase.Dawn)desired=home+new Vector3(2.2f,0,-1);
    if(now==DayPhase.Evening)desired=home+new Vector3(-2,0,1.5f);
    Destination=FindReachable(gm,desired);
   }
   Vector3 delta=Destination-transform.position;delta.y=0;
   if(delta.magnitude>.28f){
    float speed=npc.NpcId=="vane"||npc.NpcId=="kael"||npc.NpcId=="mira"?1.65f:1.15f;
    var step=transform.position+delta.normalized*Mathf.Min(delta.magnitude,speed*Time.deltaTime);
    if(!StepAllowed(gm,transform.position,step)){nextPlan=Time.time+1;Destination=transform.position;npc.Visual?.Play("idle");return true;}
    step.y=gm.ActiveTerrain.SampleHeight(step)+gm.ActiveTerrain.transform.position.y;
    transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*6);transform.position=step;npc.Visual?.Play("walk");return true;
   }
   if(Time.time<workUntil)return true;
   if(now==DayPhase.Work&&npc.NpcId=="brom"&&Time.time>=nextWork){
    nextWork=Time.time+5;workUntil=Time.time+Mathf.Min(2,npc.Visual.GetClipLength("chop_1"));npc.Visual.Play("chop_1",true);return true;
   }
   npc.Visual?.Play("idle");return true;
  }
  Vector3 FindReachable(OpenWorldManager gm,Vector3 desired){
   for(int i=0;i<20;i++){
    Vector3 p=desired+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(i==0?0:.4f+i*.12f);
    if(Vector3.Distance(new Vector3(p.x,home.y,p.z),home)>16)continue;
    Vector3 from=transform.position;Vector3 d=p-from;d.y=0;int steps=Mathf.Max(1,Mathf.CeilToInt(d.magnitude/.4f));bool clear=true;
    for(int k=1;k<=steps;k++){var q=Vector3.Lerp(transform.position,p,k/(float)steps);if(!StepAllowed(gm,from,q)){clear=false;break;}q.y=gm.ActiveTerrain.SampleHeight(q)+gm.ActiveTerrain.transform.position.y;from=q;}
    if(clear){p.y=gm.ActiveTerrain.SampleHeight(p)+gm.ActiveTerrain.transform.position.y;return p;}
   }
   return transform.position;
  }
  bool StepAllowed(OpenWorldManager gm,Vector3 from,Vector3 to){
   if(OpenWorldTerrainSculptor.WaterAt(to.x,to.z,out _,out _)||!OpenWorldGrounding.TryWalk(gm.ActiveTerrain,from,to,out _))return false;
   Vector3 d=to-from;d.y=0;if(d.sqrMagnitude<.000001f)return true;
   int n=Physics.SphereCastNonAlloc(from+Vector3.up*.9f,.32f,d.normalized,hits,d.magnitude+.08f,~0,QueryTriggerInteraction.Ignore);
   for(int i=0;i<n;i++){var h=hits[i];if(!h.collider||h.collider is TerrainCollider||h.transform.IsChildOf(transform)||h.collider.GetComponentInParent<OpenWorldHero>())continue;if(h.rigidbody&&!h.rigidbody.isKinematic)continue;return false;}return true;
  }
  public string Dialogue(OpenWorldManager gm,string introduction){
   int turn=conversation++%2;var p=PhaseAt(OpenWorldAtmosphere.Instance?OpenWorldAtmosphere.Instance.Hour:12);
   if(p==DayPhase.Rest)return turn==0?"Even wardens need a quiet hour. I stay close if a traveller needs help.":"The road is quieter after dark. Watch for movement beyond the lanterns.";
   if(p==DayPhase.Dawn)return turn==0?"A fresh morning. I check the paths before the village wakes.":"Dawn is the best time to notice fresh tracks before the crowds disturb them.";
   if(npc.NpcId=="brom")return turn==0?"A heavy final blow gives you room to breathe. Save it for an enemy's windup.":"A sharp blade helps, but timing keeps you alive. Watch the shoulders before a swing.";
   if(npc.NpcId=="lyra")return turn==0?"Old trail markers tell you who passed here. Read the notes people leave behind.":"A guardian's power is a chance to turn a fight, not a reason to ignore a warning.";
   if(npc.NpcId=="rowan")return turn==0?"Hunters bring meat; we turn it into meals. Cook before you take the longer trail.":"A warm meal and a quiet rest do more good than another hurried fight.";
   if(npc.NpcId=="marek-caravan")return gm.CollectedWorldIds.Contains(OpenWorldHuntingDiscoveries.CampId)?"Fewer supplies have gone missing since that southern camp was cleared. Thank you.":turn==0?"Someone has been lifting the hunters' supplies on the southern spur. Take care.":"Look for an Oakvale ribbon if you find a lost cache. Hunters mark what they leave.";
   if(p==DayPhase.Evening)return "One last round, then home. Stay on the marked trail if the light is fading.";
   return introduction;
  }
 }
}
