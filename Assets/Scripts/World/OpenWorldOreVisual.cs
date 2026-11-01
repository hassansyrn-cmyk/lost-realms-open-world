using System.Collections.Generic;
using UnityEngine;
namespace LostRealmsOW {
 // Shared meshes/materials per node; no lights, particles or colliders on the ore detail.
 public sealed class OpenWorldOreVisual : MonoBehaviour {
  readonly List<Material> materials=new List<Material>(); Mesh crystal;
  public static GameObject Build(Transform parent){var root=new GameObject("Iron and copper vein");root.transform.SetParent(parent,false);root.AddComponent<OpenWorldOreVisual>().Construct();root.transform.localPosition=Vector3.down*.425f;return root;}
  void Construct(){
   var prefab=OpenWorldWorldKit.Prefab("rock_medium_01");
   if(prefab){var rock=Instantiate(prefab,transform);rock.name="Textured host stone";rock.transform.localRotation=Quaternion.Euler(0,23,0);var b=OpenWorldMaterialHelper.ComputeRendererBounds(rock.transform);rock.transform.localScale*=.85f/Mathf.Max(.01f,b.size.y);b=OpenWorldMaterialHelper.ComputeRendererBounds(rock.transform);rock.transform.position+=transform.position-new Vector3(b.center.x,b.min.y,b.center.z);}
   else {var stone=GameObject.CreatePrimitive(PrimitiveType.Sphere);stone.transform.SetParent(transform,false);stone.transform.localPosition=Vector3.up*.32f;stone.transform.localScale=new Vector3(1.25f,.7f,1);Destroy(stone.GetComponent<Collider>());var mat=OpenWorldMaterialHelper.CreateLit("Dark ore host",new Color(.18f,.22f,.24f));materials.Add(mat);stone.GetComponent<Renderer>().sharedMaterial=mat;}
   var copper=OpenWorldMaterialHelper.CreateLit("Copper mineral faces",new Color(.69f,.36f,.14f));var iron=OpenWorldMaterialHelper.CreateLit("Steel blue mineral faces",new Color(.28f,.49f,.57f));
   foreach(var mat in new[]{copper,iron}){if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",.7f);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.48f);materials.Add(mat);}
   crystal=Crystal();
   for(int i=0;i<8;i++){var go=new GameObject("Exposed mineral "+i);go.transform.SetParent(transform,false);float a=i*2.39996f;go.transform.localPosition=new Vector3(Mathf.Cos(a)*.39f,.44f+(i%3)*.085f,Mathf.Sin(a)*.32f);go.transform.localRotation=Quaternion.Euler(18+8*i,i*47,12-6*i);go.transform.localScale=new Vector3(.13f+(i%2)*.06f,.26f+(i%3)*.08f,.13f);go.AddComponent<MeshFilter>().sharedMesh=crystal;go.AddComponent<MeshRenderer>().sharedMaterial=i%3==0?copper:iron;}
  }
  static Mesh Crystal(){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<6;i++){float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));int k=v.Count;v.AddRange(new[]{p,q,q+Vector3.up*.7f,p+Vector3.up*.7f,p+Vector3.up*.7f,q+Vector3.up*.7f,Vector3.up*1.2f});t.AddRange(new[]{k,k+2,k+1,k,k+3,k+2,k+4,k+6,k+5});}var m=new Mesh{name="Faceted mineral prism"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
  void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);if(crystal)Destroy(crystal);}
 }
}
