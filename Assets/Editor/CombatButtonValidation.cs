using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 public static class CombatButtonValidation {
  const string Report="Docs/UI_BUTTONS_2026-10-30.txt";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  public static bool Busy{get;private set;}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  public static void Begin(){VerdantExperienceValidation.Begin();File.WriteAllText(Report,"Combat controls validation "+DateTime.Now+"\n");OpenWorldMobileHud.DebugUI("hud");Busy=true;OpenWorldManager.I.StartCoroutine(Run());}
  static IEnumerator Run(){
   var gm=OpenWorldManager.I;var hud=OpenWorldMobileHud.Instance;
   try{
    yield return null;Canvas.ForceUpdateCanvases();
    var theme=(OpenWorldUITheme)typeof(OpenWorldMobileHud).GetField("theme",Private).GetValue(hud);
    foreach(string name in new[]{"ELEMENT","GRAPPLE","LOCK ON","SUMMON"}){
     var b=hud.GetComponentsInChildren<Button>(true).First(x=>x.name==name);
     Check(b.GetComponent<Image>().sprite==theme.Kit("circle"),name+" uses icon-free background");
     int glyphs=b.GetComponentsInChildren<Image>().Count(i=>i.name.EndsWith("Glyph"));Check(glyphs==1,name+" has one live glyph");
    }
    foreach(var caption in hud.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Action caption")){
     var parent=(RectTransform)caption.transform.parent;var corners=new Vector3[4];caption.rectTransform.GetWorldCorners(corners);
     Check(corners.All(c=>((Vector2)parent.InverseTransformPoint(c)).magnitude<=parent.rect.width*.49f),caption.text+" caption rectangle remains inside circle");
    }
    Check(!hud.GetComponentsInChildren<Button>(true).Any(b=>b.name=="ECHO"),"Echo control renamed to Summon");
    int element=gm.Hero.ActiveElement;hud.GetComponentsInChildren<Button>().First(b=>b.name=="ELEMENT").onClick.Invoke();
    yield return null;Check(gm.Hero.ActiveElement==(element+1)%3,"Element button still switches element");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/button_cleanup_hud.png");
    gm.Hero.WarpTo(new Vector3(60,0,28));gm.Hero.transform.rotation=Quaternion.identity;gm.CameraRig.Yaw=25;gm.CameraRig.Pitch=14;gm.CameraRig.SnapToTarget();
    hud.GetComponentsInChildren<Button>().First(b=>b.name=="SUMMON").onClick.Invoke();yield return null;yield return null;
    var spirit=UnityEngine.Object.FindAnyObjectByType<OpenWorldEchoSpirit>();Check(spirit,"Summon button creates guardian");
    var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(spirit.transform);Check(bounds.size.y>2.5f&&bounds.size.y<5,"guardian has enlarged world height "+bounds.size.y.ToString("F2")+" m");
    float scale=spirit.transform.localScale.x;yield return new WaitForSeconds(.25f);
    Check(spirit&&Mathf.Abs(spirit.transform.localScale.x-scale)<.001f,"guardian update preserves enlarged size before fade");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/button_cleanup_summon.png");
    yield return new WaitForSeconds(1.1f);Check(!spirit,"summon still despawns after its effect");
   }finally{Busy=false;}
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
