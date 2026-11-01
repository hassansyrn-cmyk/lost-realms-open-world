using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 public static class TitleScreenValidation {
  public static void Run() {
   var report=new System.Text.StringBuilder(); int fail=0;
   void Check(bool ok,string name){report.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)fail++;}
   var title=UnityEngine.Object.FindAnyObjectByType<OpenWorldTitleScreen>();
   var gm=UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   Check(title && OpenWorldTitleScreen.IsOpen,"Title shown at startup");
   Check(gm && !gm.WorldRoot && !gm.Hero,"World and hero not constructed before selection");
   var canvas=GameObject.Find("Lost Realms • Title");
   Check(canvas!=null,"Title canvas exists");
   if(canvas) {
    foreach(var b in canvas.GetComponentsInChildren<Button>(true))Check(b.targetGraphic && b.targetGraphic.raycastTarget,"Touchable "+b.name);
    var safe=canvas.transform.Find("Safe area") as RectTransform;
    Canvas.ForceUpdateCanvases();var corners=new Vector3[4];
    foreach(var b in canvas.GetComponentsInChildren<Button>()) {
     ((RectTransform)b.transform).GetWorldCorners(corners);
     Check(corners.All(p=>p.x>=Screen.safeArea.xMin-1&&p.x<=Screen.safeArea.xMax+1&&p.y>=Screen.safeArea.yMin-1&&p.y<=Screen.safeArea.yMax+1),"Safe area "+b.name);
    }
   }
   if(title && title.HasJourney) { title.RequestNew(); Check(!gm.WorldRoot && !title.Busy,"New Journey awaits confirmation"); title.Back(); Check(!gm.WorldRoot && !title.Busy,"Cancel keeps existing adventure untouched"); }
   string dir=Path.Combine(Path.GetTempPath(),"LostRealmsTitleTest-"+Guid.NewGuid().ToString("N"));
   try {
    OpenWorldTitleScreen.PrepareNewJourney(dir);var store=new OpenWorldSaveStore(dir);var fresh=store.Load();
    Check(fresh.weapon==-1&&fresh.weapons.Length==0,"Fresh start unarmed");fresh.gold=4321;store.Write(fresh);
    OpenWorldTitleScreen.PrepareNewJourney(dir);
    Check(new OpenWorldSaveStore(dir).Load().gold==120,"New journey replaces active progress");
    var backup=Directory.GetDirectories(dir,"BeforeNewJourney-*").Single();
    Check(new OpenWorldSaveStore(backup).Load().gold==4321,"Existing progress preserved in readable backup");
    File.WriteAllText(Path.Combine(dir,"adventure-0.json"),"broken");File.WriteAllText(Path.Combine(dir,"adventure-1.json"),"broken");
    bool refused=false;try{OpenWorldTitleScreen.PrepareNewJourney(dir);}catch(InvalidDataException){refused=true;}
    Check(refused&&File.ReadAllText(Path.Combine(dir,"adventure-0.json"))=="broken","Unreadable saves preserved");
   } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
   Directory.CreateDirectory("Docs/QA");File.WriteAllText("Docs/QA/TITLE_SCREEN.txt",report.ToString());Debug.Log("Title validation failures="+fail);
  }
  public static void Settings(){var t=UnityEngine.Object.FindAnyObjectByType<OpenWorldTitleScreen>();var c=GameObject.Find("Lost Realms • Title");foreach(var b in c.GetComponentsInChildren<Button>())if(b.name=="SETTINGS")b.onClick.Invoke();}
  public static void Back(){UnityEngine.Object.FindAnyObjectByType<OpenWorldTitleScreen>().Back();}
  public static void Continue(){UnityEngine.Object.FindAnyObjectByType<OpenWorldTitleScreen>().Begin(false);}
 }
}
