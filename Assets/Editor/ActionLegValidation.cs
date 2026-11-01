using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace LostRealmsOW {
 public static class ActionLegValidation {
  public static void Run() {
   var hero = OpenWorldManager.I.Hero;
   var visual = hero.Visual;
   var type = visual.GetType();
   const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
   var lines = new System.Collections.Generic.List<string>();
   foreach (var state in new[] { "block", "attack", "attack_2", "heavy" }) {
    visual.Play(state, true);
    var active = type.GetField("activeSlot", flags).GetValue(visual);
    if (active == null) { lines.Add("SKIP unavailable action " + state); continue; }
    foreach (var name in new[] { "footIkWeight", "footIkTarget" }) type.GetField(name, flags).SetValue(visual, 1f);
    foreach (var name in new[] { "lFootOffset", "rFootOffset", "pelvisOffset" }) type.GetField(name, flags).SetValue(visual, -.2f);
    var bones = new Transform[7]; var rotations = new Quaternion[7]; var positions = new Vector3[7];
    var names = new[] { "hips", "lThigh", "lKnee", "lFoot", "rThigh", "rKnee", "rFoot" };
    for(int i=0;i<7;i++){bones[i]=(Transform)type.GetField(names[i],flags).GetValue(visual);rotations[i]=bones[i].localRotation;positions[i]=bones[i].localPosition;}
    type.GetMethod("FootIk",flags).Invoke(visual,new object[]{1f/60f,false});
    float angle=0, displacement=0;
    for(int i=0;i<7;i++){angle=Mathf.Max(angle,Quaternion.Angle(rotations[i],bones[i].localRotation));displacement=Mathf.Max(displacement,Vector3.Distance(positions[i],bones[i].localPosition));bones[i].localRotation=rotations[i];bones[i].localPosition=positions[i];}
    lines.Add(state+": procedural action change degrees="+angle+", metres="+displacement+"; "+(angle<.01f&&displacement<.0001f?"PASS":"FAIL"));
   }
   visual.Play("idle",true);
   File.WriteAllLines("Docs/QA/ACTION_LEG_VALIDATION.txt",lines);
   Debug.Log("[ActionLegValidation] "+string.Join("; ",lines));
  }
 }
}
