using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace LostRealmsOW {
 public static class BlacksmithIntegrationValidation {
  public static void BakeClothing() {
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
    Color tint=material.GetColor(key); if(key.StartsWith("_CLOTH"))tint=new Color(.32f,.21f,.13f);
    for(int i=0;i<pixels.Length;i++)if(Vector3.Distance(new Vector3(masks[i].r,masks[i].g,masks[i].b),reference)<.1f){pixels[i]=pixels[i]*new Color(tint.r,tint.g,tint.b,1);colored++;}
   }
   texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes("Assets/Resources/World/BlacksmithShop/Textures/BromClothing.png",texture.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.Refresh();Debug.Log("[BromBake] color regions="+matches.Count+" pixels="+colored);
  }
  public static void Inspect() {
   var gm=OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>(); var brom=gm.NPCs.Find(n=>n&&n.NpcId=="brom");
   var root=GameObject.Find("Blacksmith_RoyalForge_Workshop");
   string text="Workshop bounds: "+OpenWorldMaterialHelper.ComputeRendererBounds(root.transform)+"\nBrom: "+brom.transform.position;
   foreach(var r in brom.Visual.ModelInstance.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)text+="\n"+r.name+" material="+(m?m.name:"NULL")+" texture="+(m&&m.mainTexture?m.mainTexture.name:"NULL");
   File.WriteAllText("Docs/QA/BLACKSMITH_INTEGRATION.txt",text);Debug.Log(text);
  }
  public static void Transactions() {
   var gm=OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   var saved=gm.CaptureAdventure(); int equipped=gm.Hero.EquippedWeaponId;
   var lines=new System.Collections.Generic.List<string>();
   void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
   try {
    gm.CloseMenus(); OpenWorldWeaponShop.OpenShop(gm); OpenWorldWeaponShop.Tick(gm);
    var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
    var rows=(RectTransform)typeof(OpenWorldWeaponShop).GetField("rowsContainer",flags).GetValue(null);
    Check(rows&&rows.childCount==10,"first open contains ten listings");
    Check(rows.GetComponentInParent<UnityEngine.UI.ScrollRect>(),"scroll list present");
    Check(rows.GetChild(0).Find("IconBox").GetComponent<RectTransform>().rect.width>=72,"large weapon icon");
    int id=1;gm.UnlockedWeapons.Remove(id);gm.Gold=0;gm.Ore=0;
    Check(!OpenWorldWeaponShop.TryForge(gm,id)&&gm.Gold==0&&gm.Ore==0,"insufficient funds unchanged");
    gm.Gold=40;gm.Ore=1;
    Check(OpenWorldWeaponShop.TryForge(gm,id)&&gm.Gold==0&&gm.Ore==0,"exact single charge");
    Check(!OpenWorldWeaponShop.TryForge(gm,id)&&gm.Gold==0&&gm.Ore==0,"duplicate purchase refused");
    var oldRoot=GameObject.Find("Blacksmith_RoyalForge_Workshop");UnityEngine.Object.DestroyImmediate(oldRoot);
    OpenWorldWeaponShop.Tick(gm);Check(GameObject.Find("Blacksmith_RoyalForge_Workshop"),"destroyed workshop recreated");
    var elite=GlmEliteHunts.Elites[0];gm.CollectedWorldIds.Add(elite.Flag);
    Check(GlmEliteHunts.IsSlain(gm,elite.Name),"population eligibility blocks completed elite");
   } finally {gm.CloseMenus();gm.RestoreAdventure(saved);gm.Hero.EquipWeapon(equipped);File.WriteAllLines("Docs/QA/BLACKSMITH_TRANSACTIONS.txt",lines);}
  }
 }
}
