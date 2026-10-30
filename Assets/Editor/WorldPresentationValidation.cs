using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace LostRealmsOW {
 public static class WorldPresentationValidation {
  const string Report="Docs/WORLD_PRESENTATION_2026-10-30.txt";
  public static bool Busy{get;private set;}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  public static void Begin(){VerdantExperienceValidation.Begin();OpenWorldMobileHud.DebugUI("hud");Busy=true;OpenWorldManager.I.StartCoroutine(Run());}
  static IEnumerator Run(){
   var gm=OpenWorldManager.I;GameObject root=null;var camera=Camera.main;var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;float fov=camera.fieldOfView;bool rig=gm.CameraRig.enabled;int culling=camera.cullingMask;var clear=camera.clearFlags;var background=camera.backgroundColor;var canvas=OpenWorldMobileHud.Instance.GetComponent<Canvas>();bool canvasEnabled=canvas.enabled;
   try{
    File.WriteAllText(Report,"World presentation "+DateTime.Now+"\n");var shader=Resources.Load<Shader>("World/Presentation/BannerCloth");Check(shader&&!ShaderUtil.ShaderHasError(shader),"selective cloth shader compiles and is included through Resources");
    gm.Hero.WarpTo(new Vector3(60,0,28));gm.CameraRig.enabled=false;
    root=new GameObject("Presentation fixtures");var p=gm.HuntGround(new Vector3(61,0,29));
    var hostile=OpenWorldOutpostCamp.Create("Hostile cloth",p,root.transform,new[]{"Goblin"});var captured=OpenWorldOutpostCamp.Create("Captured cloth",p+Vector3.right*3,root.transform,new[]{"Goblin"});captured.transform.position=hostile.transform.position+Vector3.right*3;foreach(var e in hostile.Guards.Concat(captured.Guards))e.gameObject.SetActive(false);captured.RestoreCaptured();
    var red=hostile.GetComponentInChildren<OpenWorldBannerCloth>();var green=captured.GetComponentInChildren<OpenWorldBannerCloth>();var redMat=red.GetComponentInChildren<MeshRenderer>().sharedMaterial;var greenMat=green.GetComponentInChildren<MeshRenderer>().sharedMaterial;
    Check(redMat.shader==shader&&greenMat.shader==shader&&redMat.GetFloat("_Captured")==0&&greenMat.GetFloat("_Captured")==1,"hostile and restored-captured banners use separate correct states");Check(redMat.GetTexture("_BaseMap")==greenMat.GetTexture("_BaseMap"),"both states retain the original embroidered texture");
    Check(!ShaderUtil.ShaderHasError(shader),"banner shader has no compile error after first use");
    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;foreach(var text in root.GetComponentsInChildren<TextMesh>(true))text.gameObject.SetActive(false);hostile.enabled=captured.enabled=false;
    camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.07f);canvas.enabled=false;
    camera.fieldOfView=42;var focus=p+new Vector3(1.5f,1.15f,0);camera.transform.position=focus+new Vector3(.3f,.65f,-6.5f);camera.transform.LookAt(focus);yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/banner_fabric_comparison.png");yield return null;
    hostile.Capture();Check(hostile.Captured&&redMat.GetFloat("_Captured")==1,"live camp capture switches the same selective cloth state");
    var anchor=OpenWorldGrappleAnchor.Spawn("ASTROLABE REVIEW",p+new Vector3(0,3,0),root.transform);var visual=anchor.GetComponentInChildren<OpenWorldGrappleVisual>();Check(visual&&visual.GetComponentsInChildren<MeshRenderer>().Length==4,"grapple uses four combined mesh assemblies");Check(anchor.GetComponentsInChildren<Collider>().Length==0,"astrolabe creates no traversal-blocking colliders");Check(anchor.GetComponentsInChildren<Light>().Length==0,"astrolabe requires no extra realtime lights");
    var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(visual.transform);Check(bounds.size.magnitude<5,"anchor silhouette remains bounded: "+bounds.size);int triangles=visual.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3);Check(triangles<8000,"bounded mesh complexity: "+triangles+" triangles");
    var pivot=anchor.transform.position;var ring=visual.transform.Find("Rotating solar arcs");var before=ring.localRotation;yield return new WaitForSeconds(.3f);Check(Quaternion.Angle(before,ring.localRotation)>1&&anchor.transform.position==pivot,"ring animation leaves gameplay anchor pivot unchanged");
    hostile.gameObject.SetActive(false);captured.gameObject.SetActive(false);foreach(var t in anchor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;anchor.enabled=false;
    camera.fieldOfView=38;focus=p+Vector3.up*3;camera.transform.position=focus+new Vector3(1.7f,.6f,-5);camera.transform.LookAt(focus);yield return new WaitForSeconds(.2f);ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/grapple_astrolabe.png");yield return null;
    gm.GrappleAnchors.Add(anchor);gm.Hero.WarpTo(p);Check(gm.TryGrappleToNearestAnchor(5),"existing grapple action still accepts redesigned anchor");Check(Vector2.Distance(new Vector2(gm.Hero.transform.position.x,gm.Hero.transform.position.z),new Vector2(pivot.x,pivot.z))<.1f&&gm.Hero.IsGliding,"grapple arrival and glider deployment remain functional");gm.GrappleAnchors.Remove(anchor);
   }finally{if(root){foreach(var anchor in root.GetComponentsInChildren<OpenWorldGrappleAnchor>())gm.GrappleAnchors.Remove(anchor);UnityEngine.Object.Destroy(root);}camera.transform.SetPositionAndRotation(oldPos,oldRot);camera.fieldOfView=fov;camera.cullingMask=culling;camera.clearFlags=clear;camera.backgroundColor=background;canvas.enabled=canvasEnabled;gm.CameraRig.enabled=rig;Busy=false;}
  }
  public static void Cleanup(){VerdantExperienceValidation.Cleanup();}
 }
}
