using UnityEngine; using UnityEditor; using System.Linq; using System.Collections.Generic; public static class HumanoidRig { public static HumanDescription Description(GameObject prefab){
  var all=prefab.GetComponentsInChildren<Transform>(true);var mapping=new Dictionary<string,string>{{"pelvis","Hips"},{"spine","Spine"},{"chest","Chest"},{"neck","Neck"},{"head","Head"}};
  foreach(string side in new[]{"L","R"}){string full=side=="L"?"Left":"Right";foreach(var pair in new[]{("clavicle","Shoulder"),("upperarm","UpperArm"),("forearm","LowerArm"),("hand","Hand"),("thigh","UpperLeg"),("shin","LowerLeg"),("foot","Foot"),("toe","Toes")})mapping[pair.Item1+side.ToLower()]=full+pair.Item2;}
  var human=new List<HumanBone>();foreach(var t in all){string clean=t.name.Replace("_","").Replace(".","").ToLower();if(mapping.TryGetValue(clean,out var h))human.Add(new HumanBone{boneName=t.name,humanName=h,limit=new HumanLimit{useDefaultValues=true}});}
  return new HumanDescription{human=human.ToArray(),skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false};
 }
}
