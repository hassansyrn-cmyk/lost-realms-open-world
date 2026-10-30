using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 // Kept separate from the horse/prey HUD and from enemy combat logic.
 public sealed partial class OpenWorldMobileHud {
  sealed class AttackBearing {
   public RectTransform Root;public Text Mark;public CanvasGroup Group;
  }
  readonly AttackBearing[] attackBearings=new AttackBearing[3];
  readonly OpenWorldEnemy[] bearingTargets=new OpenWorldEnemy[3];
  readonly float[] bearingDistances=new float[3];
  internal int VisibleAttackBearings {get;private set;}
  // A camera-relative ground-plane direction is stable even when the enemy is behind the lens.
  public static bool TryAttackBearing(Camera camera,Vector3 hero,Vector3 target,out Vector2 direction){
   direction=Vector2.zero;if(!camera)return false;
   Vector3 viewport=camera.WorldToViewportPoint(target);
   if(viewport.z>0&&viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1)return false;
   Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up);
   if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(camera.transform.up,Vector3.up);
   forward.Normalize();Vector3 right=Vector3.Cross(Vector3.up,forward),delta=target-hero;delta.y=0;
   if(delta.sqrMagnitude<.01f)return false;
   direction=new Vector2(Vector3.Dot(delta,right),Vector3.Dot(delta,forward)).normalized;return true;
  }
  internal void UpdateCombatAwareness(){
   if(!safeRoot)return;
   if(attackBearings[0]==null){
    for(int i=0;i<attackBearings.Length;i++){
     var root=Rect("Off-camera attack "+i,safeRoot,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(42,42));
     var group=root.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
     theme.Shape(root.gameObject,"circle",new Color(.045f,.065f,.10f,.95f)).raycastTarget=false;
     var mark=Label(root,"!",Vector2.zero,new Vector2(28,32),Vector2.one*.5f,26);mark.color=new Color(1,.68f,.25f);mark.raycastTarget=false;
     foreach(float sign in new[]{-1f,1f}){var chevron=Rect("Direction",root,Vector2.one*.5f,Vector2.one*.5f,new Vector2(sign*5,25),new Vector2(15,4));chevron.localRotation=Quaternion.Euler(0,0,sign*-45);var image=chevron.gameObject.AddComponent<Image>();image.color=new Color(1,.68f,.25f);image.raycastTarget=false;}
     root.gameObject.SetActive(false);attackBearings[i]=new AttackBearing{Root=root,Mark=mark,Group=group};
    }
   }
   var gm=OpenWorldManager.I;var camera=Camera.main;VisibleAttackBearings=0;
   bool allowed=gm&&gm.Hero&&!gm.Hero.Dead&&!gm.IsModalOpen&&(!titleRoot||!titleRoot.activeSelf)&&camera;
   for(int i=0;i<3;i++){bearingTargets[i]=null;bearingDistances[i]=float.PositiveInfinity;}
   if(allowed){
    Vector3 hero=gm.Hero.transform.position;
    foreach(var enemy in gm.Enemies){
     if(!enemy||!enemy.isActiveAndEnabled||enemy.Dead||!enemy.Telegraphing)continue;
     float distance=(enemy.transform.position-hero).sqrMagnitude,range=Mathf.Min(24,Mathf.Max(12,enemy.AttackRange+4));
     if(distance>range*range||!TryAttackBearing(camera,hero,enemy.transform.position+Vector3.up*enemy.CombatHeight*.5f,out _))continue;
     for(int slot=0;slot<3;slot++)if(distance<bearingDistances[slot]){for(int j=2;j>slot;j--){bearingDistances[j]=bearingDistances[j-1];bearingTargets[j]=bearingTargets[j-1];}bearingDistances[slot]=distance;bearingTargets[slot]=enemy;break;}
    }
   }
   for(int i=0;i<3;i++){
    var marker=attackBearings[i];var enemy=bearingTargets[i];bool show=allowed&&enemy;
    marker.Root.gameObject.SetActive(show);if(!show)continue;
    TryAttackBearing(camera,gm.Hero.transform.position,enemy.transform.position+Vector3.up*enemy.CombatHeight*.5f,out var direction);
    float angle=Mathf.Atan2(-direction.x,direction.y)*Mathf.Rad2Deg;
    // Central ellipse avoids joystick, action controls, navigation and the bottom health bar.
    float radiusX=Mathf.Min(190,safeRoot.rect.width*.22f),radiusY=Mathf.Min(112,safeRoot.rect.height*.19f);
    Vector2 position=new Vector2(direction.x*radiusX,direction.y*radiusY);bool crowded=false;
    for(int prior=0;prior<i;prior++)if(attackBearings[prior].Root.gameObject.activeSelf&&(attackBearings[prior].Root.anchoredPosition-position).sqrMagnitude<52*52){crowded=true;break;}
    if(crowded){marker.Root.gameObject.SetActive(false);continue;}
    marker.Root.anchoredPosition=position;
    marker.Root.localRotation=Quaternion.Euler(0,0,angle);marker.Mark.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);
    marker.Group.alpha=.82f+.18f*Mathf.Sin(Time.unscaledTime*12);VisibleAttackBearings++;
   }
  }
 }
 public sealed class OpenWorldCombatAwarenessDriver:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(!FindAnyObjectByType<OpenWorldCombatAwarenessDriver>())new GameObject("Combat awareness HUD adapter").AddComponent<OpenWorldCombatAwarenessDriver>();}
  void LateUpdate(){if(OpenWorldMobileHud.Instance)OpenWorldMobileHud.Instance.UpdateCombatAwareness();}
 }
}
