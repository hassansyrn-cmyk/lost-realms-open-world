using UnityEngine;
namespace LostRealmsOW {
 // World-space prop only: the existing discovery interaction still supplies the note's words.
 public sealed class OpenWorldTrailNote:MonoBehaviour {
  Mesh paper;Texture2D texture;Material parchment,shine;Transform glint;
  public static void Build(Transform parent){var root=new GameObject("Eren's pinned trail parchment");root.transform.SetParent(parent,false);root.transform.localPosition=new Vector3(.25f,0,.15f);root.AddComponent<OpenWorldTrailNote>().Construct();}
  void Construct(){
   var stake=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stake.name="Weathered note stake";stake.transform.SetParent(transform,false);stake.transform.localPosition=Vector3.up*.72f;stake.transform.localScale=new Vector3(.07f,.72f,.07f);Destroy(stake.GetComponent<Collider>());stake.GetComponent<Renderer>().sharedMaterial=OpenWorldMaterialHelper.CreateLit("Trail note wood",new Color(.25f,.16f,.075f));
   texture=new Texture2D(128,160,TextureFormat.RGB24,true){name="Eren parchment and ink"};var px=new Color[128*160];
   for(int y=0;y<160;y++)for(int x=0;x<128;x++){
    float edge=Mathf.Min(Mathf.Min(x,127-x),Mathf.Min(y,159-y));float noise=Mathf.PerlinNoise(x*.19f,y*.19f);Color color=Color.Lerp(new Color(.48f,.29f,.11f),new Color(.9f,.78f,.49f),Mathf.Clamp01(edge/12f))*(.94f+.1f*noise);
    int row=(y-36)/13;bool ink=y>=36&&y<137&&(y-36)%13<2&&x>15&&x<110-row%3*9&&Mathf.Sin(x*.6f+row*2)>-.55f;
    if(ink)color=new Color(.18f,.12f,.07f);if((new Vector2(x-100,y-22)).sqrMagnitude<81)color=new Color(.5f,.09f,.055f);px[y*128+x]=color;
   }
   texture.SetPixels(px);texture.Apply(true,true);parchment=new Material(OpenWorldMaterialHelper.LitShader){name="Pinned parchment"};OpenWorldMaterialHelper.SetLitProperties(parchment,Color.white,texture,null,.08f,0);parchment.SetFloat("_Cull",0);
   var vertices=new Vector3[20];var uv=new Vector2[20];var triangles=new int[4*3*6];int t=0;
   for(int y=0;y<5;y++)for(int x=0;x<4;x++){int i=y*4+x;float u=x/3f,v=y/4f;vertices[i]=new Vector3((u-.5f)*.57f, .95f+v*.66f,-.075f-Mathf.Sin(u*Mathf.PI)*.035f+(v<.1f?.025f*Mathf.Sin(x*2):0));uv[i]=new Vector2(u,v);}
   for(int y=0;y<4;y++)for(int x=0;x<3;x++){int i=y*4+x;triangles[t++]=i;triangles[t++]=i+4;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+4;triangles[t++]=i+5;}
   paper=new Mesh{name="Curled pinned parchment"};paper.vertices=vertices;paper.uv=uv;paper.triangles=triangles;paper.RecalculateNormals();paper.RecalculateBounds();var page=new GameObject("Parchment sheet");page.transform.SetParent(transform,false);page.AddComponent<MeshFilter>().sharedMesh=paper;page.AddComponent<MeshRenderer>().sharedMaterial=parchment;
   var pin=GameObject.CreatePrimitive(PrimitiveType.Sphere);pin.name="Brass parchment pin";pin.transform.SetParent(transform,false);pin.transform.localPosition=new Vector3(0,1.55f,-.12f);pin.transform.localScale=Vector3.one*.045f;Destroy(pin.GetComponent<Collider>());pin.GetComponent<Renderer>().sharedMaterial=OpenWorldMaterialHelper.CreateLit("Note brass pin",new Color(.8f,.58f,.2f),null,null,.65f,.8f);
   glint=new GameObject("Subtle parchment glint").transform;glint.SetParent(transform,false);glint.localPosition=pin.transform.localPosition+Vector3.back*.015f;shine=new Material(OpenWorldMaterialHelper.UnlitShader){name="Parchment glint"};shine.color=new Color(1,.83f,.4f);
   for(int i=0;i<2;i++){var ray=GameObject.CreatePrimitive(PrimitiveType.Quad);ray.transform.SetParent(glint,false);ray.transform.localScale=i==0?new Vector3(.08f,.009f,1):new Vector3(.009f,.08f,1);Destroy(ray.GetComponent<Collider>());ray.GetComponent<Renderer>().sharedMaterial=shine;}
  }
  void Update(){var gm=OpenWorldManager.I;bool near=gm&&gm.Hero&&!gm.IsModalOpen&&(gm.Hero.transform.position-transform.position).sqrMagnitude<144;float pulse=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Time.time*1.5f)),12);glint.gameObject.SetActive(near&&pulse>.03f);if(near){glint.localScale=Vector3.one*pulse;if(Camera.main)glint.rotation=Camera.main.transform.rotation;}}
  void OnDestroy(){if(paper)Destroy(paper);if(texture)Destroy(texture);if(parchment)Destroy(parchment);if(shine)Destroy(shine);}
 }
}
