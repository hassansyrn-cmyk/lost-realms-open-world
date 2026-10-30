using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace LostRealmsOW {
 // Original Aether astrolabe. Meshes are assembled once, with no physical blockers or point lights.
 public sealed class OpenWorldGrappleVisual:MonoBehaviour {
  readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();
  Transform outer,inner,crystal;float phase;
  public static void Build(Transform parent){var root=new GameObject("Aether astrolabe");root.transform.SetParent(parent,false);root.AddComponent<OpenWorldGrappleVisual>().Construct();}
  Material Metal(string name,Color tint,float metallic,Color emission){var m=new Material(OpenWorldMaterialHelper.LitShader){name=name};OpenWorldMaterialHelper.SetLitProperties(m,tint,null,null,.6f,metallic,emission);materials.Add(m);return m;}
  void Construct(){
   var bronze=Metal("Astrolabe aged bronze",new Color(.16f,.12f,.075f),.78f,Color.black);
   var gold=Metal("Astrolabe brushed gold",new Color(.83f,.56f,.19f),.82f,Color.black);
   var teal=Metal("Astrolabe Aether inlay",new Color(.08f,.7f,.61f),.28f,new Color(.02f,.55f,.43f));
   var crystalMat=Metal("Astrolabe crystal facets",new Color(.04f,.38f,.35f),.4f,new Color(.015f,.28f,.23f));
   var pale=Metal("Astrolabe crystal highlights",new Color(.21f,.88f,.73f),.45f,new Color(.08f,.5f,.35f));
   var frame=new Assembly();frame.Arc(1.03f,.12f,.12f,0,360,96,0,Quaternion.identity);
   for(int k=0;k<4;k++){
    float angle=k*90;var q=Quaternion.Euler(0,0,angle);frame.Gem(q*new Vector3(0,1.13f,0),.105f,.64f,q,1);
    frame.Arc(.9f,.025f,.14f,angle+8,74,20,1,Quaternion.identity);
    for(int j=0;j<3;j++)frame.Arc(1.035f,.04f,.145f,angle+32+j*9,4,2,2,Quaternion.identity);
   }
   frame.Gem(new Vector3(0,-1.46f,0),.09f,.23f,Quaternion.identity,2);
   Render("Wardens' compass frame",frame,transform,new[]{bronze,gold,teal});
   outer=new GameObject("Rotating solar arcs").transform;outer.SetParent(transform,false);outer.localRotation=Quaternion.Euler(25,0,0);
   var solar=new Assembly();
   for(int k=0;k<3;k++){
    solar.Arc(.78f,.085f,.105f,k*120+10,96,28,0,Quaternion.identity);
    solar.Arc(.78f,.025f,.13f,k*120+22,50,18,1,Quaternion.identity);
    solar.Gem(Quaternion.Euler(0,0,k*120+10)*new Vector3(0,.78f,0),.09f,.22f,Quaternion.Euler(0,0,k*120+10),0);
   }
   Render("Broken gilded solar ring",solar,outer,new[]{gold,teal});
   inner=new GameObject("Counter-rotating lunar arcs").transform;inner.SetParent(transform,false);inner.localRotation=Quaternion.Euler(0,64,35);
   var lunar=new Assembly();for(int k=0;k<2;k++){lunar.Arc(.59f,.065f,.09f,k*180,145,36,0,Quaternion.identity);lunar.Arc(.59f,.02f,.12f,k*180+12,30,10,1,Quaternion.identity);}
   Render("Lunar gimbal",lunar,inner,new[]{gold,teal});
   crystal=new GameObject("Suspended Aether heart").transform;crystal.SetParent(transform,false);
   var heart=new Assembly();heart.Gem(Vector3.zero,.23f,1.12f,Quaternion.Euler(0,22,0),0,true);Render("Cut Aether crystal",heart,crystal,new[]{crystalMat,pale});
  }
  void Render(string name,Assembly geometry,Transform parent,Material[] mats){var mesh=geometry.Finish(name,mats.Length);meshes.Add(mesh);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=mats;renderer.shadowCastingMode=ShadowCastingMode.Off;}
  void Update(){var gm=OpenWorldManager.I;if(!gm||!gm.Hero||gm.IsModalOpen||(gm.Hero.transform.position-transform.position).sqrMagnitude>10000)return;phase+=Time.deltaTime;outer.localRotation=Quaternion.Euler(25,0,phase*13);inner.localRotation=Quaternion.Euler(0,64,35-phase*19);crystal.localPosition=Vector3.up*(Mathf.Sin(phase*1.4f)*.065f);crystal.localRotation=Quaternion.Euler(0,phase*22,0);}
  void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var material in materials)if(material)Destroy(material);}
  sealed class Assembly {
   readonly List<Vector3> vertices=new List<Vector3>();readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
   void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material){int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
   public void Arc(float radius,float width,float depth,float start,float sweep,int segments,int mat,Quaternion rotation){
    // Eight-sided bevelled section gives highlights along the ring instead of flat cylinder caps.
    Vector2[] section={new Vector2(-.5f,-.3f),new Vector2(-.3f,-.5f),new Vector2(.3f,-.5f),new Vector2(.5f,-.3f),new Vector2(.5f,.3f),new Vector2(.3f,.5f),new Vector2(-.3f,.5f),new Vector2(-.5f,.3f)};
    Vector3 Point(float angle,int side){var p=section[side];float a=angle*Mathf.Deg2Rad;return rotation*new Vector3(Mathf.Sin(a)*(radius+p.x*width),Mathf.Cos(a)*(radius+p.x*width),p.y*depth);}
    for(int k=0;k<segments;k++){float a=start+sweep*k/segments,b=start+sweep*(k+1)/segments;for(int s=0;s<8;s++){int next=(s+1)%8;Quad(Point(a,s),Point(a,next),Point(b,next),Point(b,s),mat);}}
    if(sweep<359)for(int s=1;s<7;s++){Quad(Point(start,0),Point(start,s+1),Point(start,s),Point(start,0),mat);Quad(Point(start+sweep,0),Point(start+sweep,s),Point(start+sweep,s+1),Point(start+sweep,0),mat);}
   }
   public void Gem(Vector3 center,float radius,float height,Quaternion rotation,int mat,bool alternating=false){
    float[] y={-.5f,-.21f,.22f,.5f},r={0,radius,radius*.83f,0};
    Vector3 Point(int band,int side){float angle=side*Mathf.PI/4;return center+rotation*new Vector3(Mathf.Sin(angle)*r[band],y[band]*height,Mathf.Cos(angle)*r[band]);}
    for(int band=0;band<3;band++)for(int side=0;side<8;side++)Quad(Point(band,side),Point(band,(side+1)%8),Point(band+1,(side+1)%8),Point(band+1,side),alternating&&side%3==0?1:mat);
   }
   public Mesh Finish(string name,int count){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.subMeshCount=count;for(int i=0;i<count;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
  }
 }
}
