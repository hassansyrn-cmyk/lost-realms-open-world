using UnityEngine.Playables;using UnityEngine.Animations;
using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;
namespace LostRealmsOW {public static class CasterRigValidation {
 static OpenWorldEnemy testEnemy;
 public static void LiveSpawn(){var gm=OpenWorldManager.I??UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();testEnemy=OpenWorldEnemy.Spawn("Caster","Caster grounding test",gm.Hero.transform.position+gm.Hero.transform.forward*4,gm.WorldRoot);testEnemy.enabled=false;LiveReport();}
 public static void LiveReport(){var e=testEnemy;if(!e)return;var lines=new System.Collections.Generic.List<string>();lines.Add("enemy="+e.transform.position+" visualScale="+e.Visual.transform.localScale+" modelPos="+e.Visual.ModelInstance.transform.localPosition+" modelScale="+e.Visual.ModelInstance.transform.localScale);foreach(var r in e.Visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>()){var m=new Mesh();r.BakeMesh(m);var b=m.bounds;lines.Add("renderer="+r.bounds+" bakedLocal="+b+" worldMin="+r.transform.TransformPoint(b.min)+" worldMax="+r.transform.TransformPoint(b.max));UnityEngine.Object.DestroyImmediate(m);}File.AppendAllLines("Docs/QA/CASTER_GROUNDING.txt",lines);Debug.Log(string.Join("\n",lines));}
 public static void Render(){
  foreach(var role in new[]{"Caster_Rigged","Aster"})foreach(var state in role=="Aster"?new[]{"hit","hit_2","hit_3","hit_4"}:new[]{"idle","walk","attack"}){
   var p=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/"+role));p.transform.position=new Vector3(0,1000,0);
   var a=p.GetComponentInChildren<Animator>();a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();
   var clip=Resources.Load<AnimationClip>("Animations/"+(role=="Aster"?"Aster":"Caster")+"/"+state);
   var graph=PlayableGraph.Create("render");var player=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"pose",a);output.SetSourcePlayable(player);graph.Play();player.SetTime(clip.length*.4);graph.Evaluate(.001f);
   var go=new GameObject("qa camera");var cam=go.AddComponent<Camera>();cam.transform.position=p.transform.position+new Vector3(3,2.3f,4);cam.transform.LookAt(p.transform.position+Vector3.up*1.2f);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.gray;
   var rt=new RenderTexture(512,512,24);cam.targetTexture=rt;cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(512,512,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes("Docs/QA/"+role+"_"+state+".png",tex.EncodeToPNG());RenderTexture.active=old;
   graph.Destroy();UnityEngine.Object.DestroyImmediate(p);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
  }
 }
 public static void BuildRig(){
  var source=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/Caster"));
  var rig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/Aster"));
  try {
   rig.name="Caster_Rigged";var a=rig.GetComponentInChildren<Animator>();
   var rb=OpenWorldMaterialHelper.ComputeRendererBounds(rig.transform);rig.transform.localScale*=2f/rb.size.y;
   rb=OpenWorldMaterialHelper.ComputeRendererBounds(rig.transform);rig.transform.position-=new Vector3(rb.center.x,rb.min.y,rb.center.z);
   var ids=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
   var bones=ids.Select(id=>a.GetBoneTransform(id)).Where(t=>t).ToArray();
   foreach(var r in rig.GetComponentsInChildren<Renderer>())UnityEngine.Object.DestroyImmediate(r);
   foreach(var lod in rig.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(lod);
   var mf=source.GetComponentInChildren<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);mesh.name="Caster weighted mesh";
   var v=mesh.vertices;var weights=new BoneWeight[v.Length];
   for(int i=0;i<v.Length;i++){
    Vector3 world=mf.transform.TransformPoint(v[i]);v[i]=a.transform.InverseTransformPoint(world);
    var distances=bones.Select((b,j)=>new {j,d=(world-b.position).sqrMagnitude}).OrderBy(x=>x.d).Take(4).ToArray();
    float[] w=distances.Select(x=>1f/Mathf.Pow(.005f+x.d,3)).ToArray();float sum=w.Sum();
    weights[i]=new BoneWeight{boneIndex0=distances[0].j,boneIndex1=distances[1].j,boneIndex2=distances[2].j,boneIndex3=distances[3].j,weight0=w[0]/sum,weight1=w[1]/sum,weight2=w[2]/sum,weight3=w[3]/sum};
   }
   mesh.vertices=v;mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*a.transform.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
   var folder="Assets/GameContent/GeneratedCaster";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   string path=folder+"/CasterSkin.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
   var renderer=a.gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterials=mf.GetComponent<Renderer>().sharedMaterials;renderer.bones=bones;renderer.rootBone=a.GetBoneTransform(HumanBodyBones.Hips);renderer.updateWhenOffscreen=true;
   PrefabUtility.SaveAsPrefabAsset(rig,"Assets/Resources/Characters/Caster_Rigged.prefab");
   Directory.CreateDirectory("Assets/Resources/Animations/Caster");
   foreach(var state in new[]{"idle","walk","run","attack","death"}){string dest="Assets/Resources/Animations/Caster/"+state+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Animations/Aster/"+(state=="attack"?"attack_1":state)+".anim");var previous=AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);if(previous)EditorUtility.CopySerialized(clip,previous);else AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(clip),dest);}
   AssetDatabase.SaveAssets();Debug.Log("Caster rig generated: "+v.Length+" vertices, "+bones.Length+" bones");
  }finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(rig);}
 }
 public static void Inspect(){var lines=new System.Collections.Generic.List<string>();
 foreach(var role in new[]{"Caster","Elite","Aster"}){var p=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/"+role));try{var a=p.GetComponentInChildren<Animator>();lines.Add(role+" bounds="+OpenWorldMaterialHelper.ComputeRendererBounds(p.transform)+" animator="+(a?a.isHuman.ToString():"none"));foreach(var m in p.GetComponentsInChildren<MeshFilter>())lines.Add("mesh "+m.name+" vertices="+m.sharedMesh.vertexCount);if(role=="Aster"&&a&&a.isHuman){foreach(var state in new[]{"idle","hit","hit_2","hit_3","hit_4","hit_5"}){var clip=Resources.Load<AnimationClip>("Animations/Aster/"+state);if(!clip)continue;float min=99;for(int i=0;i<=10;i++){var graph=UnityEngine.Playables.PlayableGraph.Create("pose probe");var player=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"pose",a);output.SetSourcePlayable(player);player.SetTime(clip.length*i/10);graph.Play();graph.Evaluate(.001f);graph.Destroy();var hip=a.GetBoneTransform(HumanBodyBones.Hips);var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);min=Mathf.Min(min,hip.position.y-foot.position.y);}lines.Add(state+" minimum hip-foot="+min);}}}finally{UnityEngine.Object.DestroyImmediate(p);}}
 File.WriteAllLines("Docs/QA/CASTER_HIT_INSPECTION.txt",lines);Debug.Log(string.Join("\n",lines));}
}}
