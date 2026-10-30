using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace LostRealmsOW {
 // Exact reduction of redundant keys in our generated bear clips. Source packs, Aster and horses excluded.
 public static class PreparedAnimalPayloadOptimization {
  public static int CollapseConstants(AnimationClip clip){
   var bindings=new List<EditorCurveBinding>();var curves=new List<AnimationCurve>();int removed=0;
   foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
    if(binding.type!=typeof(Transform))continue;
    var curve=AnimationUtility.GetEditorCurve(clip,binding);if(curve==null||curve.length<3)continue;
    var keys=curve.keys;float value=keys[0].value;
    if(keys.Any(k=>k.value!=value||k.inTangent!=0||k.outTangent!=0))continue;
    var reduced=new AnimationCurve(new Keyframe(keys[0].time,value,0,0),new Keyframe(keys[keys.Length-1].time,value,0,0)){preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
    // Includes interval interiors and both endpoints; strict zero error, not a tolerance-based decimator.
    for(int i=0;i<=128;i++){float t=Mathf.Lerp(keys[0].time,keys[keys.Length-1].time,i/128f);if(curve.Evaluate(t)!=reduced.Evaluate(t))throw new Exception("Constant reduction changed a sample: "+binding.path);}
    bindings.Add(binding);curves.Add(reduced);removed+=keys.Length-2;
   }
   if(bindings.Count>0)AnimationUtility.SetEditorCurves(clip,bindings.ToArray(),curves.ToArray());return removed;
  }
  public static void Run(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Payload preparation requires Edit mode");
   string report="Docs/Performance/Bear_payload_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".md";Directory.CreateDirectory("Docs/Performance");
   File.WriteAllText(report,"# Generated bear payload reduction\n\nExact constant Transform curves only; keys with changing values/tangents are untouched. No vendor, Aster or horse clips. Each modified clip verified against its in-memory original before saving.\n\n| Clip | bytes before | bytes after | removed keys | max sampled error |\n|---|---:|---:|---:|---:|\n");
   foreach(var path in Directory.GetFiles("Assets/GameContent/Imported/Animations","expansion_bear_c_*.anim")){
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);var original=UnityEngine.Object.Instantiate(clip);long bytes=new FileInfo(path).Length;
    try{
     int removed=CollapseConstants(clip);float error=0;
     if(clip.length!=original.length||clip.frameRate!=original.frameRate)throw new Exception("Clip timing changed: "+path);
     foreach(var binding in AnimationUtility.GetCurveBindings(original)){
      var a=AnimationUtility.GetEditorCurve(original,binding);var b=AnimationUtility.GetEditorCurve(clip,binding);
      for(int i=0;i<=128;i++){float t=clip.length*i/128f;error=Mathf.Max(error,Mathf.Abs(a.Evaluate(t)-b.Evaluate(t)));}
     }
     if(error!=0)throw new Exception("Curve samples differ: "+path+" error "+error);
     if(removed>0){File.ReadAllText("Docs/DEVELOPMENT_LOG.md");EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);}
     File.AppendAllText(report,$"| {clip.name} | {bytes} | {new FileInfo(path).Length} | {removed} | {error} |\n");
    }catch{EditorUtility.CopySerialized(original,clip);EditorUtility.SetDirty(clip);File.ReadAllText("Docs/DEVELOPMENT_LOG.md");AssetDatabase.SaveAssetIfDirty(clip);throw;}
    finally{UnityEngine.Object.DestroyImmediate(original);}
   }
   Debug.Log("[PayloadPrep] exact bear curve reduction complete: "+report);
  }
 }
}
