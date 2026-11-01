using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;
namespace LostRealmsOW {
 // Physical timber frame, shingled cap and pinned parchment. Ink is fixed to the paper plane.
 public sealed class OpenWorldNoticeBoard : MonoBehaviour {
  readonly List<Object> owned=new List<Object>();Material timber,iron,paper;Texture2D grain;
  public static Text Build(Transform parent,string heading,string body){return parent.gameObject.AddComponent<OpenWorldNoticeBoard>().Construct(heading,body);}
  Text Construct(string heading,string body){
   grain=new Texture2D(64,256);grain.name="Notice board wood grain";var colors=new Color[64*256];for(int y=0;y<256;y++)for(int x=0;x<64;x++){float g=Mathf.PerlinNoise(x*.21f,y*.025f)*.65f+Mathf.PerlinNoise(x*.7f,y*.05f)*.35f;colors[y*64+x]=Color.Lerp(new Color(.11f,.055f,.025f),new Color(.36f,.20f,.085f),g);}grain.SetPixels(colors);grain.Apply();owned.Add(grain);
   timber=OpenWorldMaterialHelper.CreateLit("Weathered oak",Color.white,grain);iron=OpenWorldMaterialHelper.CreateLit("Forged iron",new Color(.10f,.12f,.13f));paper=OpenWorldMaterialHelper.CreateLit("Warm parchment",new Color(.84f,.73f,.49f));owned.Add(timber);owned.Add(iron);owned.Add(paper);
   for(int side=-1;side<=1;side+=2){Part("Oak post",new Vector3(side*1.03f,1.22f,0),new Vector3(.17f,2.44f,.21f),timber);Part("Stone shoe",new Vector3(side*1.03f,.12f,0),new Vector3(.30f,.24f,.32f),iron);for(int k=0;k<2;k++)Part("Iron strap",new Vector3(side*1.03f,1.05f+k*1.05f,-.12f),new Vector3(.21f,.13f,.025f),iron);}
   for(int i=0;i<6;i++)Part("Backing plank",new Vector3(-.83f+i*.332f,1.78f,0),new Vector3(.32f,1.36f,.11f),timber);
   Part("Lower carved rail",new Vector3(0,1.07f,-.035f),new Vector3(2.12f,.12f,.18f),timber);Part("Upper carved rail",new Vector3(0,2.47f,-.035f),new Vector3(2.12f,.14f,.18f),timber);
   for(int side=-1;side<=1;side+=2){var roof=Part("Pitched rain cap",new Vector3(side*.56f,2.66f,0),new Vector3(1.21f,.10f,.54f),timber);roof.transform.localRotation=Quaternion.Euler(0,0,-side*13);}
   Part("Pinned parchment",new Vector3(0,1.78f,-.075f),new Vector3(1.72f,1.19f,.018f),paper);
   for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2){var nail=Part("Parchment tack",new Vector3(x*.79f,1.78f+y*.53f,-.097f),Vector3.one*.035f,iron);nail.transform.localRotation=Quaternion.Euler(0,0,45);}
   var canvasGo=new GameObject("Printed parchment ink",typeof(RectTransform),typeof(Canvas));canvasGo.transform.SetParent(transform,false);canvasGo.transform.localPosition=new Vector3(0,1.78f,-.09f);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var rt=canvasGo.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(1000,690);rt.localScale=Vector3.one*.00155f;
   var title=Ink(canvasGo.transform,"Heading",heading,56,260,FontStyle.Bold);title.rectTransform.sizeDelta=new Vector2(960,85);
   var text=Ink(canvasGo.transform,"Notices",body,60,-45,FontStyle.Normal);text.rectTransform.sizeDelta=new Vector2(950,520);
   var rule=Part("Header brass inlay",new Vector3(0,2.095f,-.10f),new Vector3(1.38f,.012f,.009f),iron);
   return text;
  }
  Text Ink(Transform parent,string name,string value,int size,float y,FontStyle style){var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(0,y);var text=go.GetComponent<Text>();text.font=Resources.Load<Font>(name=="Heading"?"UI/Fonts/Cinzel-Bold":"UI/Fonts/CrimsonText-Regular")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.lineSpacing=.92f;text.fontStyle=style;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.18f,.105f,.045f);text.text=value;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
  GameObject Part(string name,Vector3 pos,Vector3 size,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;var col=go.GetComponent<Collider>();if(col)Destroy(col);return go;}
  void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
 }
}
