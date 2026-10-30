using UnityEngine;
namespace LostRealmsOW {
 public static class OpenWorldCombatFeel {
  static readonly RaycastHit[] coverHits=new RaycastHit[24];
  // Solids block the weapon; actor capsules do not shield nearby actors from a broad swing.
  public static bool ClearHit(Vector3 origin,OpenWorldEnemy foe,Transform hero){
   Vector3 start=origin+Vector3.up*.9f,end=foe.transform.position+Vector3.up*Mathf.Min(1.1f,foe.CombatHeight*.55f);
   Vector3 d=end-start;int n=Physics.RaycastNonAlloc(start,d.normalized,coverHits,d.magnitude,~0,QueryTriggerInteraction.Ignore);
   for(int i=0;i<n;i++){var hit=coverHits[i];if(!hit.collider||hit.transform.IsChildOf(hero)||hit.transform.IsChildOf(foe.transform))continue;if(hit.collider.GetComponentInParent<OpenWorldEnemy>()||hit.collider.GetComponentInParent<OpenWorldAnimalInteraction>())continue;if(hit.rigidbody&&!hit.rigidbody.isKinematic)continue;return false;}return true;
  }
  public static void Confirm(OpenWorldHero hero,bool heavy,int hits){if(hits<=0||!hero)return;hero.Visual?.TriggerHitStop(heavy?.065f:.035f);OpenWorldManager.I?.CameraRig?.AddShake(heavy?.075f:.028f,heavy?.13f:.085f);}
 }
}
