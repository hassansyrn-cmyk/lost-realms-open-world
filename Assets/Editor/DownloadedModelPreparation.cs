using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
namespace LostRealmsOW {
 public static class DownloadedModelPreparation {
  const string Root = "Assets/GameContent/DownloadedModels";
  static readonly List<string> report = new List<string>();
  [MenuItem("Lost Realms OW/Content/Prepare Downloaded Actors")]
  public static void Prepare() {
   if (EditorApplication.isPlaying) throw new InvalidOperationException("Prepare in Edit mode only");
   report.Clear(); AssetDatabase.Refresh();
   foreach (string id in new[]{"Civilian","Komodo","MountainDragon"}) PrepareOne(id);
   AssetDatabase.SaveAssets();
   File.WriteAllLines("Docs/AssetAudit/DOWNLOADED_ACTORS_PREPARATION.txt",report);
   Debug.Log("[DownloadedActors] Preparation complete: " + string.Join("; ",report));
  }
  static void PrepareOne(string id) {
   string dir=Root+"/"+id, source=dir+"/Source/"+(id=="Civilian"?"Civilian 3":id)+".fbx";
   Directory.CreateDirectory(dir+"/Prepared");AssetDatabase.Refresh();
   foreach(string path in Directory.GetFiles(dir+"/Source","*.png")) {
    var ti=(TextureImporter)AssetImporter.GetAtPath(path);
    bool normal=path.EndsWith("_normal.png")||id=="MountainDragon"&&path.EndsWith("Image_3.png");
    ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
    ti.maxTextureSize=id=="Civilian"?1024:2048;ti.mipmapEnabled=true;ti.streamingMipmaps=true;ti.isReadable=false;
    ti.textureCompression=TextureImporterCompression.Compressed;
    if(!normal)ti.sRGBTexture=!(path.EndsWith("_gloss.png")||path.EndsWith("_specular.png")||path.EndsWith("Image_1.png"));
    ti.SaveAndReimport();
   }
   var mi=(ModelImporter)AssetImporter.GetAtPath(source);
   mi.importCameras=false;mi.importLights=false;mi.isReadable=false;mi.importBlendShapes=true;
   mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
   mi.animationType=id=="Civilian"?ModelImporterAnimationType.Human:ModelImporterAnimationType.Generic;
   if(id=="Civilian")mi.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
   mi.importAnimation=id!="Civilian";mi.animationCompression=ModelImporterAnimationCompression.Off;
   mi.SaveAndReimport();
   if(id!="Civilian") {
    var take=mi.defaultClipAnimations.First();
    mi.clipAnimations=id=="Komodo"?new[]{Range(take,"Idle",1,2),Range(take,"Walk",1,26)}:new[]{Range(take,"Flight",176,204)};
    mi.SaveAndReimport();
   }
   var sourceObj=AssetDatabase.LoadAssetAtPath<GameObject>(source);
   var stage=new GameObject(id);stage.SetActive(false);
   try {
    var model=(GameObject)PrefabUtility.InstantiatePrefab(sourceObj);model.transform.SetParent(stage.transform,false);
    // Modify our prepared instance only; the downloaded source and its hierarchy remain intact.
    foreach(var r in model.GetComponentsInChildren<Renderer>(true)) {
     var mats=r.sharedMaterials;
     for(int i=0;i<mats.Length;i++)mats[i]=MaterialFor(dir,id,id=="Civilian"?(mats[i]?mats[i].name:""):"Body");
     r.sharedMaterials=mats;
     if(r is SkinnedMeshRenderer sk){sk.updateWhenOffscreen=false;sk.quality=SkinQuality.Bone4;}
    }
    var b=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
    float target=id=="Civilian"?1.78f:id=="Komodo"?2.8f:11f;
    float size=id=="Civilian"?b.size.y:Mathf.Max(b.size.x,b.size.z);
    model.transform.localScale*=target/size;
    b=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
    model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
    if(!model.GetComponentInChildren<Animator>())model.AddComponent<Animator>();
    var driver=stage.AddComponent<OpenWorldImportedModel>();
    var clips=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
    if(id=="Civilian") {
     var animator=model.GetComponentInChildren<Animator>();
     if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Civilian humanoid avatar invalid");
     driver.Idle=Copy(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Animations/Aster/idle.anim"),dir,"Idle");
     driver.Walk=Copy(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Animations/Aster/walk.anim"),dir,"Walk");
     driver.Talk=Copy(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Animations/Aster/talk.anim"),dir,"Talk");
     if(!driver.Idle.humanMotion||!driver.Walk.humanMotion||!driver.Talk.humanMotion)throw new Exception("Humanoid motion required");
     driver.WalkSpeed=2.35f;
    } else if(id=="Komodo") {
     driver.Idle=Copy(clips.First(c=>c.name=="Idle"),dir,"Idle");
     driver.Walk=Copy(clips.First(c=>c.name=="Walk"),dir,"Walk");driver.Run=driver.Walk;driver.WalkSpeed=.9f;driver.RunSpeed=.9f;
     if(!AnimationUtility.GetCurveBindings(driver.Walk).Any(c=>c.propertyName.StartsWith("blendShape.")))throw new Exception("Komodo morph animation lost");
    } else { driver.Fly=driver.Idle=Copy(clips.First(c=>c.name=="Flight"),dir,"Flight"); }
    stage.SetActive(true);
    string prefab=dir+"/Prepared/"+id+".prefab";PrefabUtility.SaveAsPrefabAsset(stage,prefab);
    var settings=AddressableAssetSettingsDefaultObject.Settings;
    var group=settings.FindGroup("DownloadedActors")??settings.CreateGroup("DownloadedActors",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
    group.GetSchema<BundledAssetGroupSchema>().BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
    settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefab),group).address="ow.downloaded."+id;
    report.Add(id+": bounds="+OpenWorldMaterialHelper.ComputeRendererBounds(model.transform).size+", clips="+string.Join(",",new[]{driver.Idle,driver.Walk,driver.Talk,driver.Fly}.Where(c=>c).Select(c=>c.name+" "+c.length.ToString("F2")+"s")));
   } finally{UnityEngine.Object.DestroyImmediate(stage);}
  }
  static ModelImporterClipAnimation Range(ModelImporterClipAnimation take,string name,float first,float last) {
   return new ModelImporterClipAnimation{name=name,takeName=take.takeName,firstFrame=first,lastFrame=last,loopTime=true,loopPose=true,lockRootRotation=true,lockRootHeightY=true,lockRootPositionXZ=true,keepOriginalOrientation=true,keepOriginalPositionY=true,keepOriginalPositionXZ=true};
  }
  static AnimationClip Copy(AnimationClip src,string dir,string name) {
   if(!src)throw new Exception("Missing animation "+name);
   string path=dir+"/Prepared/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
   if(!clip){clip=UnityEngine.Object.Instantiate(src);AssetDatabase.CreateAsset(clip,path);}else EditorUtility.CopySerialized(src,clip);
   clip.name=name;AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
   var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
  }
  static Material MaterialFor(string dir,string id,string slot) {
   // FBX import with material import disabled retains source slot names on this model importer.
   slot=slot.Replace(" (Instance)","");
   string path=dir+"/Prepared/"+slot+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!mat){mat=new Material(OpenWorldMaterialHelper.LitShader);AssetDatabase.CreateAsset(mat,path);}
   string prefix=dir+"/Source/";
   var color=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+(id=="Civilian"?"civilian 3_"+slot+"_diffuse.png":"Image_0.png"));
   var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+(id=="Civilian"?"civilian 3_"+slot+"_normal.png":"Image_3.png"));
   OpenWorldMaterialHelper.SetLitProperties(mat,Color.white,color,normal,.25f,0);
   if(!color)throw new Exception("Missing texture for "+id+" "+slot);
   if(id=="MountainDragon"){mat.SetFloat("_Cull",0);var emission=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"Image_2.png");mat.SetTexture("_EmissionMap",emission);mat.SetColor("_EmissionColor",Color.white*.5f);mat.EnableKeyword("_EMISSION");}
   EditorUtility.SetDirty(mat);return mat;
  }
 }
}


