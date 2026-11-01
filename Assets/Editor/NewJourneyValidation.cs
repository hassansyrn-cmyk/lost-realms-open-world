using System;
using System.IO;
using UnityEngine;
namespace LostRealmsOW {
 public static class NewJourneyValidation {
  public static void Run() {
   string dir=Path.Combine(Application.temporaryCachePath,"CodexNewJourney-"+Guid.NewGuid().ToString("N"));
   var lines=new System.Collections.Generic.List<string>();
   void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
   var heroObject=new GameObject("Isolated unarmed test");
   try {
    var fresh=OpenWorldSaveData.NewJourney();
    Check(fresh.weapon==-1&&fresh.weapons.Length==0,"new journey owns no weapons");
    var store=new OpenWorldSaveStore(dir); store.Write(fresh); var loaded=store.Load();
    Check(loaded.weapon==-1&&loaded.weapons.Length==0,"unarmed save round trip");
    loaded.weapon=2;loaded.weapons=new[]{2};store.Write(loaded);
    Check(store.Load().weapon==2,"existing armed save remains supported");
    Check(OpenWorldWeaponCatalog.Get(-1).Style=="unarmed","unarmed combat profile");
    var hand=new GameObject("test hand");hand.transform.SetParent(heroObject.transform);
    var old=new GameObject("OW_Equipped_Test");old.transform.SetParent(hand.transform);
    var result=OpenWorldWeaponCatalog.AttachToHand(hand.transform,-1,out var trail);
    Check(!result&&!trail&&(!old||!old.activeSelf),"unarmed removes old weapon without replacement");
    File.WriteAllLines("Docs/QA/NEW_JOURNEY.txt",lines);Debug.Log(string.Join("\n",lines));
   } finally { UnityEngine.Object.DestroyImmediate(heroObject); }
  }
 }
}
