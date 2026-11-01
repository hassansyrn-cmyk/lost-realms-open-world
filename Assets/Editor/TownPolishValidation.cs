using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;
namespace LostRealmsOW {public static class TownPolishValidation {
  public static void BakeRowan() {
   string source="Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_NPC/";
   var material=AssetDatabase.LoadAssetAtPath<Material>(source+"Materials/PT_NPC_Mat.mat");
   var mask=new Texture2D(2,2);mask.LoadImage(File.ReadAllBytes(source+"Textures/PT_Modular_NPC_Texture_Mask_01.png"));
   var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(source+"Textures/PT_Modular_NPC_Texture_Base_01.png"));
   string shader=File.ReadAllText(source+"Shaders/PT_Modular_NPC_Shader_PBR.shader");
   shader=shader.Substring(shader.IndexOf("void surf"));shader=shader.Substring(0,shader.IndexOf("o.Albedo"));
   var matches=System.Text.RegularExpressions.Regex.Matches(shader,@"float4 color\d+ = IsGammaSpace\(\) \? float4\(([^)]+)\).*?tex2DNode199 \* (_\w+COLOR)",System.Text.RegularExpressions.RegexOptions.Singleline);
   var pixels=texture.GetPixels();var masks=mask.GetPixels();int colored=0;
   foreach(System.Text.RegularExpressions.Match match in matches) {
    var v=match.Groups[1].Value.Split(',');var key=match.Groups[2].Value;
    var reference=new Vector3(float.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture));
    Color tint=material.GetColor(key); if(key.StartsWith("_CLOTH"))tint=new Color(.19f,.38f,.24f);
    for(int i=0;i<pixels.Length;i++)if(Vector3.Distance(new Vector3(masks[i].r,masks[i].g,masks[i].b),reference)<.1f){pixels[i]=pixels[i]*new Color(tint.r,tint.g,tint.b,1);colored++;}
   }
   texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes("Assets/Resources/World/NPCs/RowanClothing.png",texture.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.Refresh();Debug.Log("[BromBake] color regions="+matches.Count+" pixels="+colored);
  }


 static OpenWorldManager GM=>OpenWorldManager.I??UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
 public static void Review(){var gm=GM;var lines=new System.Collections.Generic.List<string>();
  void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);}
  Check(!UnityEngine.Object.FindObjectsByType<OpenWorldEnemy>().Any(e=>e&&e.Role=="Caster"),"no Caster enemies in world");
  var merchant=UnityEngine.Object.FindObjectsByType<OpenWorldNPC>().FirstOrDefault(n=>n.NpcId=="glm-merchant");
  foreach(var camp in UnityEngine.Object.FindObjectsByType<OpenWorldOutpostCamp>()){float hub=new Vector2(camp.transform.position.x,camp.transform.position.z).magnitude;float dist=merchant?Vector2.Distance(new Vector2(camp.transform.position.x,camp.transform.position.z),new Vector2(merchant.transform.position.x,merchant.transform.position.z)):0;Check(hub>140&&dist>75,camp.CampTitle+" hub="+hub.ToString("F1")+" merchant="+dist.ToString("F1"));Check(!OpenWorldTerrainSculptor.WaterAt(camp.transform.position.x,camp.transform.position.z,out _,out _),camp.CampTitle+" dry ground");}
  var rowan=UnityEngine.Object.FindObjectsByType<OpenWorldNPC>().First(n=>n.NpcId=="rowan");var mats=rowan.Visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.sharedMaterials).ToArray();Check(mats.Length>0&&mats.All(m=>m&&m.mainTexture&&m.mainTexture.name=="RowanClothing"),"Rowan imported clothing has colored texture");
  var capital=GameObject.Find("CapitalOuterBuildings");var shrine=UnityEngine.Object.FindObjectsByType<OpenWorldRelicShrine>().First(r=>r.RelicIndex==0);var structures=new System.Collections.Generic.List<Bounds>();
  foreach(Transform child in capital.transform){if(!child.name.Contains("building"))continue;var b=OpenWorldMaterialHelper.ComputeRendererBounds(child);structures.Add(b);var q=b.ClosestPoint(shrine.transform.position);float clearance=Vector2.Distance(new Vector2(q.x,q.z),new Vector2(shrine.transform.position.x,shrine.transform.position.z));Check(clearance>2.5f,child.name+" shrine clearance="+clearance.ToString("F1"));float roadClear=999;for(float x=b.min.x;x<=b.max.x+.1f;x+=.5f)for(float z=b.min.z;z<=b.max.z+.1f;z+=.5f)foreach(var road in OpenWorldRegions.Roads)roadClear=Mathf.Min(roadClear,OpenWorldRegions.DistanceToPath(road.Path,new Vector2(x,z),out _,out _)-road.Width*.5f);Check(roadClear>.5f,child.name+" road edge clearance="+roadClear.ToString("F1"));lines.Add(child.name+" "+b);}
  for(int i=0;i<structures.Count;i++)for(int j=i+1;j<structures.Count;j++)Check(!structures[i].Intersects(structures[j]),"outer buildings "+i+"/"+j+" do not overlap");
  var ores=UnityEngine.Object.FindObjectsByType<OpenWorldOreVisual>(FindObjectsInactive.Include);Check(ores.Length==6,"six enhanced ore veins");File.WriteAllLines("Docs/QA/TOWN_POLISH.txt",lines);Debug.Log(string.Join("\n",lines));
 }
 public static void FocusRowan(){Focus(UnityEngine.Object.FindObjectsByType<OpenWorldNPC>().First(n=>n.NpcId=="rowan").transform.position,new Vector3(4,2.5f,-5),1.1f);}
 public static void FocusShrine(){Focus(UnityEngine.Object.FindObjectsByType<OpenWorldRelicShrine>().First(r=>r.RelicIndex==0).transform.position,new Vector3(-7,5,-9),1.3f);}
 public static void FocusOre(){Focus(UnityEngine.Object.FindObjectsByType<OpenWorldOreVisual>().OrderBy(o=>o.transform.position.sqrMagnitude).First().transform.position,new Vector3(2,1.8f,-2.4f),.5f);}
 public static void FocusTown(){Focus(Vector3.up*6.2f,new Vector3(58,65,-70),0);}
 public static void FocusCamp(){Focus(UnityEngine.Object.FindObjectsByType<OpenWorldOutpostCamp>().First(c=>c.PersistentId=="ruins").transform.position,new Vector3(20,20,-25),0);}
 static void Focus(Vector3 p,Vector3 offset,float height){GM.CameraRig.enabled=false;var c=Camera.main;c.transform.position=p+offset;c.transform.LookAt(p+Vector3.up*height);}

}}