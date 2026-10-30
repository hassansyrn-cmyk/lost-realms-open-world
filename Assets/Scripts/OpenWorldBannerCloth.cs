using UnityEngine;
namespace LostRealmsOW {
 // Dedicated atlas shader: never multiply a capture tint over gold trim or hardware.
 public sealed class OpenWorldBannerCloth:MonoBehaviour {
  Material material;Renderer[] renderers;bool imported;
  public static void Apply(GameObject root,bool captured){
   if(!root)return;var owner=root.GetComponent<OpenWorldBannerCloth>()??root.AddComponent<OpenWorldBannerCloth>();owner.SetCaptured(captured);
  }
  void SetCaptured(bool captured){
   if(renderers==null){
    renderers=GetComponentsInChildren<Renderer>(true);var atlas=Resources.Load<Texture2D>("Props/Expansion/Textures/RallyBanner_basecolor");
    imported=GetComponentInChildren<MeshFilter>()&&gameObject.name!="ProceduralBanner";
    if(imported){
     var shader=Resources.Load<Shader>("World/Presentation/BannerCloth");
     if(!shader){Debug.LogError("Banner cloth shader missing; preserving original artwork.");return;}
     material=new Material(shader){name="Selective banner cloth"};material.SetTexture("_BaseMap",atlas);material.SetTexture("_BumpMap",Resources.Load<Texture2D>("Props/Expansion/Textures/RallyBanner_normal"));
     foreach(var r in renderers){var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=material;r.sharedMaterials=slots;r.SetPropertyBlock(null);}
    }
   }
   if(imported){if(material)material.SetFloat("_Captured",captured?1:0);}
   else foreach(var r in renderers)if(r.name=="Banner fabric")r.sharedMaterial=OpenWorldMaterialHelper.CreateLit("Fallback banner fabric",captured?new Color(.08f,.42f,.18f):new Color(.55f,.06f,.06f));
  }
  void OnDestroy(){if(material)Destroy(material);}
 }
}
