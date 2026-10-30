using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 public static class CombatAwarenessValidation {
  public static bool Busy{get;private set;}
  const string Report="Docs/COMBAT_AWARENESS_2026-10-30.txt";
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  static void Refresh(OpenWorldMobileHud hud)=>typeof(OpenWorldMobileHud).GetMethod("UpdateCombatAwareness",Private).Invoke(hud,null);
  static int Count(OpenWorldMobileHud hud)=>(int)typeof(OpenWorldMobileHud).GetProperty("VisibleAttackBearings",Private).GetValue(hud);
  static void Windup(OpenWorldEnemy enemy){var field=typeof(OpenWorldEnemy).GetField("state",Private);field.SetValue(enemy,Enum.Parse(field.FieldType,"Windup"));typeof(OpenWorldEnemy).GetField("stateTimer",Private).SetValue(enemy,30f);}
  public static void Begin(){VerdantExperienceValidation.Begin();OpenWorldMobileHud.DebugUI("hud");Busy=true;OpenWorldManager.I.StartCoroutine(Run());}
  static IEnumerator Run(){
   var gm=OpenWorldManager.I;var hud=OpenWorldMobileHud.Instance;var hero=gm.Hero;var camera=Camera.main;var fixtures=new List<OpenWorldEnemy>();var original=new List<OpenWorldEnemy>(gm.Enemies);bool rigEnabled=gm.CameraRig.enabled;
   var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;float hp=hero.Health;
   try{
    File.WriteAllText(Report,"Combat awareness validation "+DateTime.Now+"; deterministic windup fixture, desktop Editor.\n");
    hero.WarpTo(gm.HuntGround(new Vector3(60,0,28)));gm.CameraRig.enabled=false;var center=hero.transform.position;
    camera.transform.position=center+new Vector3(0,3,-6);camera.transform.LookAt(center+Vector3.up);
    Check(!OpenWorldMobileHud.TryAttackBearing(camera,center,center+Vector3.forward*4+Vector3.up,out _),"on-camera attack has no duplicate bearing");
    Check(OpenWorldMobileHud.TryAttackBearing(camera,center,center+Vector3.back*10,out var rear)&&rear.y<-.9f,"behind-camera enemy points down without mirrored direction");
    Check(OpenWorldMobileHud.TryAttackBearing(camera,center,center+Vector3.right*10,out var right)&&right.x>.9f,"right-hand threat points right");
    camera.transform.rotation=Quaternion.Euler(15,90,0);Check(OpenWorldMobileHud.TryAttackBearing(camera,center,center+Vector3.back*10,out var turned)&&turned.x>.9f,"direction follows camera yaw");camera.transform.LookAt(center+Vector3.up);
    gm.Enemies.Clear();
    var offsets=new[]{Vector3.back*9,Vector3.left*9,Vector3.right*9,Vector3.back*11};
    for(int i=0;i<4;i++){var enemy=OpenWorldEnemy.Spawn("IronGoblin","Awareness fixture",center+offsets[i],gm.WorldRoot);enemy.Controller.enabled=false;Windup(enemy);fixtures.Add(enemy);gm.Enemies.Add(enemy);}
    Refresh(hud);Check(Count(hud)==3,"four simultaneous windups bounded to three nearest warnings");
    var roots=hud.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name.StartsWith("Off-camera attack ")).ToArray();Check(roots.Length==3,"fixed reusable pool of three markers");
    Check(roots.All(r=>!r.GetComponent<CanvasGroup>().blocksRaycasts&&!r.GetComponent<CanvasGroup>().interactable&&r.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget)),"warnings cannot steal touch input");
    foreach(var enemy in fixtures)enemy.transform.position=center+Vector3.back*10;Refresh(hud);Check(Count(hud)==1,"near-identical directions share one warning without overlapping icons");for(int i=0;i<4;i++)fixtures[i].transform.position=center+offsets[i];
    var savedEnemies=new List<OpenWorldEnemy>(gm.Enemies);gm.Enemies.Clear();gm.Enemies.Add(fixtures[0]);var actor=fixtures[0];
    actor.transform.position=center+Vector3.forward*4;Refresh(hud);Check(Count(hud)==0,"visible winding-up actor suppressed by HUD");
    actor.transform.position=center+Vector3.back*10;Windup(actor);Refresh(hud);Check(Count(hud)==1,"off-camera live windup displayed");
    gm.ShowMapModal=true;Refresh(hud);Check(Count(hud)==0,"map hides attack cues");gm.ShowMapModal=false;
    hero.Health=0;Refresh(hud);Check(Count(hud)==0,"dead hero hides attack cues");hero.Health=hp;
    actor.Health=0;Refresh(hud);Check(Count(hud)==0,"dead attackers cannot leave stale warning");actor.Health=actor.MaxHealth;
    actor.transform.position=center+Vector3.back*30;Refresh(hud);Check(Count(hud)==0,"distant windups ignored");
    actor.transform.position=center+Vector3.back*10;actor.Stun(10);Refresh(hud);Check(Count(hud)==0,"interrupted attack removes warning");
    gm.Enemies.Clear();gm.Enemies.AddRange(savedEnemies);foreach(var enemy in fixtures)Windup(enemy);Refresh(hud);
    yield return null;ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/combat_awareness.png");yield return null;
   }finally{
    foreach(var enemy in fixtures){gm.Enemies.Remove(enemy);if(enemy)UnityEngine.Object.Destroy(enemy.gameObject);}foreach(var enemy in original)if(enemy&&!gm.Enemies.Contains(enemy))gm.Enemies.Add(enemy);
    hero.Health=hp;gm.ShowMapModal=false;camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);gm.CameraRig.enabled=rigEnabled;Busy=false;
   }
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
