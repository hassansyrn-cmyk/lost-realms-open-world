using System;using System.Linq;using System.Collections;using System.IO;using UnityEngine;
namespace LostRealmsOW {
 public static class StableRepairValidation {
  public static void Focus(){var gm=UnityEngine.Object.FindObjectsByType<OpenWorldManager>().First(g=>g.Hero);var p=OpenWorldHorseProgression.Instance;gm.Hero.WarpTo(p.Stablekeeper.transform.position+Vector3.forward*2);gm.CameraRig.enabled=false;var at=GameObject.Find("Verdant stables").transform.position;Camera.main.transform.position=at+new Vector3(7,6,12);Camera.main.transform.LookAt(at+new Vector3(-3,1,1));}
  public static void ShowShop(){OpenWorldHorseProgression.Instance.Stablekeeper.Converse(UnityEngine.Object.FindObjectsByType<OpenWorldManager>().First(g=>g.Hero));}
  public static void Run(){OpenWorldHorseProgression.Instance.StartCoroutine(Check());}
  static IEnumerator Check(){
   var gm=UnityEngine.Object.FindObjectsByType<OpenWorldManager>().First(g=>g.Hero);var p=OpenWorldHorseProgression.Instance;
   var flags=gm.CollectedWorldIds.ToArray();int gold=gm.Gold;var pos=gm.Hero.transform.position;
   var save=typeof(OpenWorldManager).GetField("saveBlocked",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);bool old=(bool)save.GetValue(gm);save.SetValue(gm,true);
   var report=new System.Text.StringBuilder();void Assert(bool v,string name){report.AppendLine((v?"PASS ":"FAIL ")+name);}
   try{
    Assert(!p.ShopOpen,"Shop closed on approach");Assert(p.Stablekeeper,"Labeled stablekeeper spawned");
    Assert(GameObject.Find("Verdant stables").transform.Cast<Transform>().Count(t=>t.name.StartsWith("Stable display"))==3,"Three breed models displayed");
    gm.Hero.WarpTo(p.Stablekeeper.transform.position+Vector3.forward*2);yield return null;
    p.Stablekeeper.Converse(gm);Assert(p.ShopOpen,"Speaking opens stable list");p.CloseShop();Assert(!p.ShopOpen,"Close dismisses list");
    gm.Gold=5000;Assert(p.Buy(1),"Purchase prepared horse");yield return null;
    var horse=UnityEngine.Object.FindObjectsByType<OpenWorldAnimalInteraction>().First(a=>a.Owned&&a.Breed==1);
    var motion=horse.GetComponentInChildren<OpenWorldImportedModel>();Assert(motion&&motion.Idle&&motion.Walk&&motion.Trot&&motion.Canter&&motion.Run,"Owned horse has all native gait clips");
    var bones=horse.GetComponentInChildren<SkinnedMeshRenderer>().bones;motion.State="walk";yield return new WaitForSeconds(.2f);var rotations=bones.Select(b=>b.localRotation).ToArray();yield return new WaitForSeconds(.25f);
    Assert(bones.Where((b,i)=>Quaternion.Angle(rotations[i],b.localRotation)>.5f).Count()>4,"Walk actually moves skeleton");
    horse.transform.position=new Vector3(-65,gm.ActiveTerrain.SampleHeight(new Vector3(-65,0,-5))+gm.ActiveTerrain.transform.position.y,-5);gm.Hero.WarpTo(horse.transform.position+Vector3.right*2);gm.Hero.EnterMount(horse);Assert(gm.Hero.IsMounted,"Owned horse mounts");yield return new WaitForSeconds(1);
    gm.Hero.TouchMoveInput=Vector2.up;yield return new WaitForSeconds(3);Assert(horse.Speed>1,"Mounted horse moves under held stick input");rotations=bones.Select(b=>b.localRotation).ToArray();yield return new WaitForSeconds(.25f);
    Assert(bones.Where((b,i)=>Quaternion.Angle(rotations[i],b.localRotation)>.5f).Count()>4,"Mounted horse skeleton animates");
   }finally{if(gm.Hero.IsMounted)gm.Hero.LeaveMount(true);gm.Hero.TouchMoveInput=Vector2.zero;p.CloseShop();gm.CollectedWorldIds.Clear();gm.CollectedWorldIds.UnionWith(flags);gm.Gold=gold;gm.Hero.WarpTo(pos);save.SetValue(gm,old);File.WriteAllText("Docs/QA/STABLE_REPAIR.txt",report.ToString());Debug.Log(report.ToString());}
  }
 }
}
