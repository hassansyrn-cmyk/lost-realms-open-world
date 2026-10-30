using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Dash afterimages (Wuthering Waves / Genshin style): a snapshot of the character's skinned meshes drawn
 // with an additive rim shader where the character was, fading out over a fraction of a second.
 // Snapshot meshes are pooled, and at most a handful are alive at once.
 public sealed class OpenWorldAfterimage : MonoBehaviour {
  const int MaxAlive = 6;
  static Material material;
  static bool materialMissing;
  static readonly Stack<Mesh> pool = new Stack<Mesh>();
  static readonly List<SkinnedMeshRenderer> scratch = new List<SkinnedMeshRenderer>();
  static readonly List<OpenWorldAfterimage> alive = new List<OpenWorldAfterimage>();
  static readonly int ColorId = Shader.PropertyToID("_Color"), FadeId = Shader.PropertyToID("_Fade"), BaseColorId = Shader.PropertyToID("_BaseColor");

  readonly List<Mesh> meshes = new List<Mesh>();
  readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
  MaterialPropertyBlock block;
  float born, life;
  Color tint;

  static Material Mat() {
   if (material || materialMissing) return material;
   var shader = Resources.Load<Shader>("Shaders/Afterimage") ?? Shader.Find("LostRealms/Afterimage");
   if (!shader || !shader.isSupported) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
   if (!shader) { materialMissing = true; return null; }
   material = new Material(shader) { name = "Afterimage", renderQueue = 3100 };
   if (shader.name.Contains("Particles")) {
    // Additive particle fallback.
    material.SetFloat("_Surface", 1f);
    material.SetFloat("_Blend", 2f);
    material.SetOverrideTag("RenderType", "Transparent");
    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
    material.SetInt("_ZWrite", 0);
   }
   return material;
  }

  public static void Spawn(GameObject model, Color tint, float life) {
   var mat = Mat();
   if (!mat || !model) return;
   while (alive.Count >= MaxAlive) { var oldest = alive[0]; alive.RemoveAt(0); if (oldest) oldest.Release(); }
   model.GetComponentsInChildren(false, scratch);
   if (scratch.Count == 0) return;
   var go = new GameObject("Afterimage");
   var a = go.AddComponent<OpenWorldAfterimage>();
   a.born = Time.time; a.life = Mathf.Max(0.05f, life); a.tint = tint;
   a.block = new MaterialPropertyBlock();
   foreach (var skin in scratch) {
    if (!skin || !skin.enabled || !skin.sharedMesh || skin.forceRenderingOff) continue;
    var mesh = pool.Count > 0 ? pool.Pop() : new Mesh { name = "AfterimageSnapshot" };
    skin.BakeMesh(mesh, true);
    a.meshes.Add(mesh);
    a.matrices.Add(Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one));
   }
   scratch.Clear();
   alive.Add(a);
  }

  void Update() {
   float age = (Time.time - born) / life;
   if (age >= 1f || !material) { Release(); return; }
   float fade = (1f - age) * (1f - age);
   block.SetColor(ColorId, tint);
   block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, fade * 0.55f));
   block.SetFloat(FadeId, fade);
   for (int i = 0; i < meshes.Count; i++) {
    var mesh = meshes[i];
    for (int sub = 0; sub < mesh.subMeshCount; sub++)
     Graphics.DrawMesh(mesh, matrices[i], material, 0, null, sub, block, false, false, false);
   }
  }

  void Release() {
   foreach (var mesh in meshes) { if (!mesh) continue; if (pool.Count < 16) pool.Push(mesh); else Destroy(mesh); }
   meshes.Clear();
   alive.Remove(this);
   if (this) Destroy(gameObject);
  }

  void OnDestroy() {
   alive.Remove(this);
   foreach (var mesh in meshes) if (mesh) { if (pool.Count < 16) pool.Push(mesh); else Destroy(mesh); }
   meshes.Clear();
  }
 }
}
