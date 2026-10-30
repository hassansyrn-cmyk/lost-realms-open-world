using System.Collections.Generic;
using UnityEngine;
namespace LostRealmsOW {
 public sealed class OpenWorldTrailPresentation:MonoBehaviour {
  readonly List<Mesh> meshes=new List<Mesh>();Material dirt;Texture2D texture;
  readonly List<Material> decorations=new List<Material>();
  static readonly int Clearings=Shader.PropertyToID("_LR_Clearings"),Count=Shader.PropertyToID("_LR_ClearingCount");
  readonly Vector4[] points=new Vector4[4];
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Reset(){Shader.SetGlobalFloat(Count,0);}
  public void Build(OpenWorldDiscoverySite[] sites,OpenWorldManager gm){
   texture=new Texture2D(32,32,TextureFormat.RGB24,false){name="Trail soil (runtime)",wrapMode=TextureWrapMode.Repeat};
   var colors=new Color[1024];for(int i=0;i<colors.Length;i++){float x=i%32,y=i/32;float n=.65f*Mathf.PerlinNoise(x*.23f+11,y*.23f+7)+.35f*Mathf.PerlinNoise(x*.71f+3,y*.71f+19);colors[i]=Color.Lerp(new Color(.25f,.20f,.12f),new Color(.38f,.30f,.18f),n);}texture.SetPixels(colors);texture.Apply(false,true);
   dirt=new Material(OpenWorldMaterialHelper.LitShader){name="Worn hunter trail"};OpenWorldMaterialHelper.SetLitProperties(dirt,Color.white,texture,null,.04f,0);
   int count=0;
   foreach(var site in sites){if(!site)continue;float radius=site.Kind==2?3.5f:site.Kind==1?2.1f:2.4f;points[count++]=new Vector4(site.transform.position.x,site.transform.position.y,site.transform.position.z,radius);Patch(site.transform.position,radius,gm);}
   Shader.SetGlobalVectorArray(Clearings,points);Shader.SetGlobalFloat(Count,count);
   foreach(var anchor in new[]{new Vector3(58,0,35),new Vector3(70,0,37),new Vector3(92,0,48)}){
    var p=gm.HuntGround(anchor);if(OpenWorldTerrainSculptor.WaterAt(p.x,p.z,out _,out _)||OpenWorldPopulation.Blocked(p,1.4f,.35f))continue;
    var stake=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stake.name="Amber hunter trail ribbon";stake.transform.SetParent(transform,false);stake.transform.position=p+Vector3.up*.65f;stake.transform.localScale=new Vector3(.09f,.65f,.09f);Destroy(stake.GetComponent<Collider>());stake.GetComponent<Renderer>().sharedMaterial=Decoration("Trail stake",new Color(.25f,.17f,.09f));
    var cloth=GameObject.CreatePrimitive(PrimitiveType.Cube);cloth.name="Oakvale trail ribbon";cloth.transform.SetParent(stake.transform,false);cloth.transform.localPosition=new Vector3(1.8f,.65f,0);cloth.transform.localScale=new Vector3(3.5f,.18f,.7f);cloth.transform.localRotation=Quaternion.Euler(0,0,-14);Destroy(cloth.GetComponent<Collider>());cloth.GetComponent<Renderer>().sharedMaterial=Decoration("Oakvale amber ribbon",new Color(.87f,.51f,.14f));
   }
   Strip(new[]{new Vector3(52,0,37),new Vector3(63,0,33),new Vector3(90,0,44),new Vector3(111,0,59)},gm);
  }
  void Patch(Vector3 center,float radius,OpenWorldManager gm){
   const int n=24;var vertices=new Vector3[n+1];var uv=new Vector2[n+1];var triangles=new int[n*3];
   vertices[0]=center+Vector3.up*.035f;uv[0]=new Vector2(center.x,center.z)*.5f;
   for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;float r=radius*(.92f+.08f*Mathf.Sin(i*2.7f));var p=center+new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);p=gm.HuntGround(p);vertices[i+1]=p+Vector3.up*.035f;uv[i+1]=new Vector2(p.x,p.z)*.5f;triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%n+1;}
   AddMesh("Worn clearing",vertices,uv,triangles);
  }
  void Strip(Vector3[] route,OpenWorldManager gm){
   var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
   for(int s=0;s<route.Length-1;s++){int steps=Mathf.CeilToInt(Vector3.Distance(route[s],route[s+1]));var right=Vector3.Cross(Vector3.up,(route[s+1]-route[s]).normalized);
    for(int k=0;k<steps;k++){Vector3 a=Vector3.Lerp(route[s],route[s+1],k/(float)steps),b=Vector3.Lerp(route[s],route[s+1],(k+1)/(float)steps);if(OpenWorldTerrainSculptor.WaterAt(a.x,a.z,out _,out _)||OpenWorldPopulation.Blocked(gm.HuntGround(a),1,.45f))continue;int first=v.Count;foreach(var p in new[]{a-right*.5f,a+right*.5f,b-right*.5f,b+right*.5f}){v.Add(gm.HuntGround(p)+Vector3.up*.027f);uv.Add(new Vector2(p.x,p.z)*.5f);}tri.AddRange(new[]{first,first+2,first+1,first+1,first+2,first+3});}}
   AddMesh("Worn hunter footpath",v.ToArray(),uv.ToArray(),tri.ToArray());
  }
  void AddMesh(string name,Vector3[] vertices,Vector2[] uv,int[] triangles){var m=new Mesh{name=name};m.vertices=vertices;m.uv=uv;m.triangles=triangles;m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=m;obj.AddComponent<MeshRenderer>().sharedMaterial=dirt;}
  Material Decoration(string name,Color color){var material=OpenWorldMaterialHelper.CreateLit(name,color);decorations.Add(material);return material;}
  void OnDestroy(){Shader.SetGlobalFloat(Count,0);foreach(var m in meshes)if(m)Destroy(m);foreach(var m in decorations)if(m)Destroy(m);if(dirt)Destroy(dirt);if(texture)Destroy(texture);}
 }
}
