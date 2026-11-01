using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Shared URP-first material and placement helper for the open-world project.
 // Guarantees every runtime-created material uses Universal Render Pipeline/Lit
 // (with Standard fallback) so nothing ever renders magenta.
 public static class OpenWorldMaterialHelper {
  static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

  public static Shader LitShader =>
   Shader.Find("Universal Render Pipeline/Lit") ??
   Shader.Find("Universal Render Pipeline/Simple Lit") ??
   Shader.Find("Standard");

  public static Shader UnlitShader =>
   Shader.Find("Universal Render Pipeline/Unlit") ??
   Shader.Find("Unlit/Color") ??
   LitShader;

  public static Material CreateLit(string key, Color color, Texture2D albedo = null, Texture2D normal = null, float smoothness = 0.28f, float metallic = 0.08f, Color emission = default) {
   string cacheKey = $"{key}_{color}_{(albedo ? albedo.GetEntityId().ToString() : "none")}_{(normal ? normal.GetEntityId().ToString() : "none")}_{smoothness}_{metallic}_{emission}";
   if (Cache.TryGetValue(cacheKey, out var mat) && mat != null) return mat;

   mat = new Material(LitShader) { name = "OW_" + key };
   SetLitProperties(mat, color, albedo, normal, smoothness, metallic, emission);
   Cache[cacheKey] = mat;
   return mat;
  }

  public static void SetLitProperties(Material mat, Color color, Texture2D albedo = null, Texture2D normal = null, float smoothness = 0.28f, float metallic = 0.08f, Color emission = default) {
   if (!mat) return;
   mat.color = color;
   if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
   if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
   if (albedo) {
    mat.mainTexture = albedo;
    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
   }
   if (normal && mat.HasProperty("_BumpMap")) {
    mat.SetTexture("_BumpMap", normal);
    mat.EnableKeyword("_NORMALMAP");
   }
   if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
   if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
   if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
   if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor")) {
    mat.EnableKeyword("_EMISSION");
    mat.SetColor("_EmissionColor", emission);
   }
  }

  static readonly MaterialPropertyBlock TintBlock = new MaterialPropertyBlock();
  public static void TintHierarchy(GameObject root, Color color, Color emission = default) {
   if (!root) return;
   var owner = root.GetComponent<OpenWorldTintMaterials>();
   if (!owner && emission.maxColorComponent > .01f) owner = root.AddComponent<OpenWorldTintMaterials>();
   foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
    if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer.GetComponent<TextMesh>()) continue;
    if (emission.maxColorComponent > .01f) {
     var materials = renderer.sharedMaterials;
     bool changed = false;
     for (int i = 0; i < materials.Length; i++) {
      var source = materials[i];
      if (source && source.HasProperty("_EmissionColor") && !source.IsKeywordEnabled("_EMISSION")) { materials[i] = owner.Emissive(source); changed = true; }
     }
     if (changed) renderer.sharedMaterials = materials;
    }
    renderer.GetPropertyBlock(TintBlock);
    TintBlock.SetColor("_BaseColor", color);
    TintBlock.SetColor("_Color", color);
    TintBlock.SetColor("_EmissionColor", emission);
    renderer.SetPropertyBlock(TintBlock);
    TintBlock.Clear();
   }
  }

  public static GameObject SpawnModel(string resourcePath, string texturePath, Transform parent, Vector3 worldPos, float targetHeight, Color tint, bool addMeshCollider = true) {
   var prefab = Resources.Load<GameObject>(resourcePath);
   if (!prefab) return null;

   var wrapper = new GameObject("OW_" + System.IO.Path.GetFileNameWithoutExtension(resourcePath));
   if (parent) wrapper.transform.SetParent(parent, false);
   wrapper.transform.position = worldPos;

   var model = Object.Instantiate(prefab, wrapper.transform, false);
   model.transform.localPosition = Vector3.zero;

   var tex = !string.IsNullOrEmpty(texturePath) ? Resources.Load<Texture2D>(texturePath) : null;
   Texture2D nrm = null;
   if (!string.IsNullOrEmpty(texturePath)) {
    if (texturePath.Contains("_basecolor")) nrm = Resources.Load<Texture2D>(texturePath.Replace("_basecolor", "_normal"));
    else if (texturePath.EndsWith("_0")) nrm = Resources.Load<Texture2D>(texturePath.Substring(0, texturePath.Length - 2) + "_1");
   }
   var mat = CreateLit(resourcePath, tint, tex, nrm);

   var renderers = model.GetComponentsInChildren<Renderer>(true);
   foreach (var r in renderers) {
    if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
    if (tex != null || r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("InternalError") || r.sharedMaterial.shader.name == "Standard") {
     r.sharedMaterial = mat;
     var mats = r.sharedMaterials;
     if (mats != null && mats.Length > 1) {
      for (int i = 0; i < mats.Length; i++) mats[i] = mat;
      r.sharedMaterials = mats;
     }
    }
   }

   if (renderers.Length > 0 && targetHeight > 0.05f) {
    Bounds b = ComputeRendererBounds(model.transform);
    if (b.size.y > 0.001f) {
     float s = targetHeight / b.size.y;
     model.transform.localScale *= s;
    }
    b = ComputeRendererBounds(model.transform);
    model.transform.position += new Vector3(worldPos.x - b.center.x, worldPos.y - b.min.y, worldPos.z - b.center.z);
   }

   if (addMeshCollider) {
    foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) {
     if (mf.sharedMesh && !mf.GetComponent<Collider>()) {
      // Imported meshes can lose their CPU data in Android players. Runtime cooking then
      // fails; bounds remain available and give scenery a solid, conservative fallback.
      if (mf.sharedMesh.isReadable) {
       var mc = mf.gameObject.AddComponent<MeshCollider>();
       mc.sharedMesh = mf.sharedMesh;
      } else {
       var bounds = mf.sharedMesh.bounds;
       var box = mf.gameObject.AddComponent<BoxCollider>();
       box.center = bounds.center;
       box.size = bounds.size;
      }
     }
    }
   }
   return wrapper;
  }

  public static Bounds ComputeRendererBounds(Transform root) {
   Bounds result = new Bounds(root.position, Vector3.zero);
   bool first = true;
   foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r.GetComponent<TextMesh>()) continue;
    if (first) { result = r.bounds; first = false; }
    else result.Encapsulate(r.bounds);
   }
   return result;
  }

  public static float SampleGroundHeight(Vector3 pos, Transform ignoreRoot = null) {
   if (Terrain.activeTerrain != null) {
    float th = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
    if (pos.y > th + 6f) {
     var skyHits = Physics.RaycastAll(new Vector3(pos.x, pos.y + 15f, pos.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
     float skyBest = float.MinValue;
     foreach (var h in skyHits) {
      if (ignoreRoot && h.transform.IsChildOf(ignoreRoot)) continue;
      if (h.collider is CharacterController) continue;
      if (h.normal.y > 0.5f && h.point.y >= th + 6f && h.point.y <= pos.y + 1f) {
       if (h.point.y > skyBest) skyBest = h.point.y;
      }
     }
     if (skyBest > float.MinValue) return skyBest;
    }
    // Also check solid non-character colliders (such as stone decks or bridges) above terrain
    var hits = Physics.RaycastAll(new Vector3(pos.x, th + 25f, pos.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
    float best = th;
    foreach (var h in hits) {
     if (ignoreRoot && h.transform.IsChildOf(ignoreRoot)) continue;
     if (h.collider is CharacterController) continue;
     if (h.normal.y > 0.55f && h.point.y >= th - 0.2f && h.point.y <= Mathf.Min(th + 6f, pos.y + 0.3f)) {
      if (h.point.y > best) best = h.point.y;
     }
    }
    return best;
   }
   if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore)) {
    return hit.point.y;
   }
   return pos.y;
  }
 }
 // One variant per source material per tinted object; disposed with that object.
 public sealed class OpenWorldTintMaterials : MonoBehaviour {
  readonly Dictionary<Material, Material> variants = new Dictionary<Material, Material>();
  public Material Emissive(Material source) {
   if (!variants.TryGetValue(source, out var material)) {
    material = new Material(source) { name = source.name + " Runtime Emission" };
    material.EnableKeyword("_EMISSION"); variants[source] = material;
   }
   return material;
  }
  void OnDestroy() { foreach (var material in variants.Values) if (material) Destroy(material); }
 }

}
