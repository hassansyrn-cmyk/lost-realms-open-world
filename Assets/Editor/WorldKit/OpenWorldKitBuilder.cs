using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LostRealmsOW {
 // Catalog row (Docs/AssetAudit/world-kit.json). Editing the catalog only needs a kit rebuild,
 // never a script recompile.
 [Serializable] public sealed class KitSpec {
  public string key = "", source = "", role = "", biomes = "", tags = "", collider = "auto", mode = "", tint = "", layer = "";
  public float height, scale = 1f, scaleMin = 0.85f, scaleMax = 1.15f, sink, align, weight = 1f, cull = -1f, footprint;
  public Vector3 rot;
  public bool detail, noShadow, keepColliders, keepAnim, loop = true, keepLights;
  public float life, fxScale = 1f;
  public string coverage = "", coverageTex = "";
  public float coverageLevel = float.NaN;
  public int maxParticles = 0;
 }
 [Serializable] public sealed class KitCatalog { public KitSpec[] entries; }

 // Builds the World Kit: URP-correct, normalised, collider-ready copies of curated vendor
 // prefabs under Resources/WorldKit, plus Resources/World/WorldKit.json describing them.
 public static class OpenWorldKitBuilder {
  public const string CatalogPath = "Docs/AssetAudit/world-kit.json";
  public const string PrefabFolder = "Assets/Resources/WorldKit";
  public const string ManifestPath = "Assets/Resources/World/WorldKit.json";
  public const string ReportPath = "Docs/AssetAudit/WORLDKIT_REPORT.md";
  static readonly List<string> report = new List<string>();

  [MenuItem("Lost Realms OW/World Kit/Build World Kit")]
  public static void Build() { BuildFiltered(null); }

  // Rebuild only rows whose key starts with the prefix (keeps the rest of the manifest).
  public static void BuildPrefix(string prefix) { BuildFiltered(prefix); }

  // Rebuild only the listed keys (comma or space separated); the rest of the manifest is kept.
  public static void BuildKeys(string keys) {
   var set = new HashSet<string>((keys ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries));
   if (set.Count == 0) { Debug.LogWarning("[Kit] BuildKeys: no keys given"); return; }
   BuildFiltered(null, set);
  }

  static void BuildFiltered(string prefix, HashSet<string> only = null) {
   var sw = System.Diagnostics.Stopwatch.StartNew();
   if (!File.Exists(CatalogPath)) { Debug.LogError("[Kit] catalog missing: " + CatalogPath); return; }
   var catalog = JsonUtility.FromJson<KitCatalog>(File.ReadAllText(CatalogPath));
   OpenWorldMaterialConverter.Begin();
   Directory.CreateDirectory(PrefabFolder);
   Directory.CreateDirectory(PrefabFolder + "/VFX");
   Directory.CreateDirectory(PrefabFolder + "/Detail");
   Directory.CreateDirectory("Assets/Resources/World");
   EnsureLayers();
   AssetDatabase.Refresh();
   report.Clear();
   report.Add("# World Kit build — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
   report.Add("");
   var previous = File.Exists(ManifestPath) ? JsonUtility.FromJson<WorldKitManifest>(File.ReadAllText(ManifestPath)) : null;
   var entries = new List<WorldKitEntry>();
   if (prefix != null && previous != null && previous.entries != null) entries.AddRange(previous.entries.Where(e => !e.key.StartsWith(prefix, StringComparison.Ordinal)));
   if (only != null && previous != null && previous.entries != null) entries.AddRange(previous.entries.Where(e => !only.Contains(e.key)));
   var keys = new HashSet<string>();
   var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
   var active = SceneManager.GetActiveScene();
   SceneManager.SetActiveScene(scene);
   int done = 0, failed = 0;
   try {
    foreach (var spec in catalog.entries) {
     if (spec == null || string.IsNullOrEmpty(spec.key)) continue;
     if (!keys.Add(spec.key)) { report.Add("- DUPLICATE key " + spec.key + " skipped"); continue; }
     if (prefix != null && !spec.key.StartsWith(prefix, StringComparison.Ordinal)) continue;
     if (only != null && !only.Contains(spec.key)) continue;
     try {
      var entry = Prepare(spec);
      if (entry != null) { entries.Add(entry); done++; } else failed++;
     } catch (Exception e) { failed++; report.Add("- FAILED " + spec.key + ": " + e.Message); Debug.LogWarning("[Kit] " + spec.key + ": " + e); }
     if ((done + failed) % 20 == 0) {
      AssetDatabase.SaveAssets();
      EditorUtility.UnloadUnusedAssetsImmediate();
      GC.Collect();
     }
    }
   } finally {
    if (active.IsValid()) SceneManager.SetActiveScene(active);
    EditorSceneManager.CloseScene(scene, true);
   }
   var manifest = new WorldKitManifest { entries = entries.OrderBy(e => e.role).ThenBy(e => e.key).ToArray(), generated = DateTime.Now.ToString("s") };
   File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
   PrepareTerrainLayers();
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   report.Add("");
   report.Add($"Prepared {done}, failed {failed}. Materials: converted {OpenWorldMaterialConverter.Converted} (upgraded {OpenWorldMaterialConverter.Upgraded}, custom {OpenWorldMaterialConverter.Custom}, generic {OpenWorldMaterialConverter.Generic}, baked {OpenWorldMaterialConverter.Baked}), kept {OpenWorldMaterialConverter.Kept}.");
   report.AddRange(OpenWorldMaterialConverter.Report.Distinct());
   Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
   File.WriteAllLines(ReportPath, report);
   Debug.Log($"[Kit] built {done} entries ({failed} failed) in {sw.Elapsed.TotalSeconds:0.0}s");
  }

  static readonly HashSet<string> FoliageRoles = new HashSet<string> { "tree", "bush", "grass", "flower", "fern", "reed", "lily", "plant", "mushroom", "deadtree", "palm", "shrub" };
  static readonly HashSet<string> RockRoles = new HashSet<string> { "rock", "rock_small", "rock_big", "cliff", "boulder", "stone", "mountain" };

  static WorldKitEntry Prepare(KitSpec spec) {
   var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.source);
   if (!source) { report.Add("- MISSING " + spec.key + ": " + spec.source); return null; }
   bool vfx = spec.role == "vfx";
   var root = new GameObject(spec.key);
   try {
    var model = UnityEngine.Object.Instantiate(source, root.transform, false);
    model.name = source.name;
    if (spec.rot != Vector3.zero) model.transform.localRotation = Quaternion.Euler(spec.rot) * model.transform.localRotation;
    Clean(model, spec, vfx);
    // Materials
    var mode = ParseMode(spec.mode);
    Color? tint = null;
    if (!string.IsNullOrEmpty(spec.tint) && ColorUtility.TryParseHtmlString(spec.tint.StartsWith("#") ? spec.tint : "#" + spec.tint, out var tc)) tint = tc;
    var coverage = default(OpenWorldMaterialConverter.Coverage);
    if (!string.IsNullOrEmpty(spec.coverage) && ColorUtility.TryParseHtmlString(spec.coverage.StartsWith("#") ? spec.coverage : "#" + spec.coverage, out var cc))
     coverage = new OpenWorldMaterialConverter.Coverage { color = new Color(cc.r, cc.g, cc.b, 1f), texture = spec.coverageTex, level = spec.coverageLevel };
    foreach (var r in model.GetComponentsInChildren<Renderer>(true)) {
     bool particle = r is ParticleSystemRenderer;
     var mats = r.sharedMaterials;
     for (int i = 0; i < mats.Length; i++) mats[i] = OpenWorldMaterialConverter.Convert(mats[i], particle, particle ? KitMaterialMode.Auto : mode, particle ? null : tint, particle ? default : coverage);
     r.sharedMaterials = mats;
     if (r is ParticleSystemRenderer psr && psr.trailMaterial) psr.trailMaterial = OpenWorldMaterialConverter.Convert(psr.trailMaterial, true);
     if (!particle) {
      r.shadowCastingMode = spec.noShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
      r.receiveShadows = true;
      r.lightProbeUsage = LightProbeUsage.BlendProbes;
      r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
      r.allowOcclusionWhenDynamic = true;
     } else r.shadowCastingMode = ShadowCastingMode.Off;
    }
    if (vfx) return PrepareVfx(spec, root, model);
    // Normalise scale and pivot (bottom centre).
    var bounds = RendererBounds(root.transform, model);
    if (bounds.size.y < 0.001f) { report.Add("- EMPTY " + spec.key + ": no renderable bounds"); return null; }
    float scale = spec.height > 0f ? spec.height / bounds.size.y : Mathf.Max(0.001f, spec.scale);
    model.transform.localScale *= scale;
    bounds = RendererBounds(root.transform, model);
    model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    bounds = RendererBounds(root.transform, model);
    if (spec.detail) return PrepareDetail(spec, root, model, bounds);
    // LODs
    int lodCount = SetupLods(spec, root, model, bounds);
    // Colliders
    string collider = spec.collider;
    if (collider == "auto" || string.IsNullOrEmpty(collider)) {
     if (spec.role == "tree" || spec.role == "palm" || spec.role == "deadtree") collider = "trunk";
     else if (RockRoles.Contains(spec.role) || spec.role == "building" || spec.role == "wall" || spec.role == "tower" || spec.role == "ruin" || spec.role == "gate" || spec.role == "bridge") collider = "mesh";
     else if (FoliageRoles.Contains(spec.role)) collider = "none";
     else collider = "box";
    }
    if (!spec.keepColliders) foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
    AddColliders(root, model, bounds, collider);
    // Layer
    string layerName = !string.IsNullOrEmpty(spec.layer) ? spec.layer : FoliageRoles.Contains(spec.role) ? "Foliage" : RockRoles.Contains(spec.role) || spec.role == "building" || spec.role == "wall" || spec.role == "tower" || spec.role == "ruin" ? "WorldStatic" : "SmallProps";
    int layer = LayerMask.NameToLayer(layerName);
    if (layer >= 0) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    string path = PrefabFolder + "/" + spec.key + ".prefab";
    PrefabUtility.SaveAsPrefabAsset(root, path);
    var entry = Entry(spec, bounds, root, model, lodCount, "WorldKit/" + spec.key);
    entry.collider = collider;
    report.Add($"- {spec.key} [{spec.role}] {bounds.size.x:0.0}x{bounds.size.y:0.0}x{bounds.size.z:0.0} m, {entry.triangles} tris LOD0, lods {lodCount}, collider {collider}");
    return entry;
   } finally { UnityEngine.Object.DestroyImmediate(root); }
  }

  static KitMaterialMode ParseMode(string m) {
   switch ((m ?? "").ToLowerInvariant()) {
    case "opaque": return KitMaterialMode.Opaque;
    case "cutout": return KitMaterialMode.Cutout;
    case "cutout2": case "twosided": return KitMaterialMode.CutoutTwoSided;
    case "fade": return KitMaterialMode.Fade;
    default: return KitMaterialMode.Auto;
   }
  }

  static void Clean(GameObject model, KitSpec spec, bool vfx) {
   foreach (var t in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
   var scripts = model.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x).ToList();
   int guard = 0;
   while (scripts.Count > 0 && guard++ < 500) {
    var removable = scripts.Find(candidate => !scripts.Any(other => other != candidate && other.GetType().GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>().Any(req =>
     new[] { req.m_Type0, req.m_Type1, req.m_Type2 }.Any(type => type != null && type.IsAssignableFrom(candidate.GetType())))));
    if (!removable) break;
    scripts.Remove(removable);
    UnityEngine.Object.DestroyImmediate(removable);
   }
   foreach (var c in model.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c);
   foreach (var a in model.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(a);
   foreach (var a in model.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);
   foreach (var j in model.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
   foreach (var b in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(b);
   foreach (var l in model.GetComponentsInChildren<Light>(true)) {
    if (vfx || spec.keepLights) { l.shadows = LightShadows.None; l.renderMode = LightRenderMode.Auto; }
    else UnityEngine.Object.DestroyImmediate(l);
   }
   foreach (var p in model.GetComponentsInChildren<ReflectionProbe>(true)) UnityEngine.Object.DestroyImmediate(p);
   foreach (var p in model.GetComponentsInChildren<LightProbeGroup>(true)) UnityEngine.Object.DestroyImmediate(p);
   if (!spec.keepAnim && !vfx) {
    foreach (var a in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
   } else foreach (var a in model.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.CullCompletely; }
   if (vfx) foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
  }

  // ---------- LODs ----------
  static int SetupLods(KitSpec spec, GameObject root, GameObject model, Bounds bounds) {
   float cull = spec.cull > 0f ? spec.cull : DefaultCull(spec.role, bounds.size.y);
   var groups = model.GetComponentsInChildren<LODGroup>(true);
   LOD[] lods;
   if (groups.Length > 0) {
    var g = groups[0];
    lods = g.GetLODs();
    foreach (var other in groups) UnityEngine.Object.DestroyImmediate(other);
    // Renderers outside the vendor LOD arrays (e.g. collision-only meshes) stay visible in LOD0.
    var inLods = new HashSet<Renderer>(lods.SelectMany(l => l.renderers).Where(r => r));
    var extra = model.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !inLods.Contains(r)).ToArray();
    if (extra.Length > 0 && lods.Length > 0) lods[0].renderers = lods[0].renderers.Concat(extra).ToArray();
    // Respect vendor transition ratios but end at our cull height.
    for (int i = 0; i < lods.Length; i++) {
     float vendor = lods[i].screenRelativeTransitionHeight;
     lods[i].screenRelativeTransitionHeight = i == lods.Length - 1 ? cull : Mathf.Max(vendor, cull + 0.01f * (lods.Length - i));
     lods[i].fadeTransitionWidth = 0.15f;
    }
   } else {
    var renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
    lods = new[] { new LOD(cull, renderers) { fadeTransitionWidth = 0.15f } };
   }
   var group = root.AddComponent<LODGroup>();
   group.SetLODs(lods);
   group.fadeMode = LODFadeMode.CrossFade;
   group.animateCrossFading = true;
   group.RecalculateBounds();
   return lods.Length;
  }

  static float DefaultCull(string role, float height) {
   switch (role) {
    case "tree": case "palm": case "deadtree": return 0.008f;
    case "bush": case "shrub": return 0.016f;
    case "cliff": case "mountain": return 0.004f;
    case "rock_big": case "boulder": return 0.008f;
    case "rock": return 0.012f;
    case "rock_small": return 0.022f;
    case "stone": return 0.03f;
    case "building": case "tower": case "wall": case "gate": case "ruin": return 0.005f;
    case "lamp": case "banner": case "shrine": return 0.014f;
    case "log": case "mushroom": case "fern": case "reed": case "lily": return 0.03f;
    default: return height > 3f ? 0.012f : 0.03f;
   }
  }

  // ---------- colliders ----------
  static void AddColliders(GameObject root, GameObject model, Bounds bounds, string mode) {
   switch (mode) {
    case "trunk": {
     float radius = TrunkRadius(root.transform, model, bounds);
     var cap = root.AddComponent<CapsuleCollider>();
     float h = Mathf.Clamp(bounds.size.y * 0.55f, 1.8f, 9f);
     cap.center = new Vector3(0f, h * 0.5f, 0f);
     cap.height = h; cap.radius = radius; cap.direction = 1;
     break;
    }
    case "mesh":
    case "convex": {
     var lod = root.GetComponent<LODGroup>();
     var lod0 = lod ? new HashSet<Renderer>(lod.GetLODs()[0].renderers.Where(r => r)) : null;
     int added = 0;
     foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) {
      var r = mf.GetComponent<MeshRenderer>();
      if (!mf.sharedMesh || !r || (lod0 != null && !lod0.Contains(r))) continue;
      if (mf.sharedMesh.vertexCount < 3) continue;
      var mc = mf.gameObject.AddComponent<MeshCollider>();
      mc.sharedMesh = mf.sharedMesh;
      mc.convex = mode == "convex";
      added++;
     }
     if (added == 0) goto case "box";
     break;
    }
    case "box": {
     var box = root.AddComponent<BoxCollider>();
     box.center = bounds.center; box.size = bounds.size;
     break;
    }
    case "capsule": {
     var cap = root.AddComponent<CapsuleCollider>();
     cap.center = bounds.center; cap.height = bounds.size.y; cap.radius = Mathf.Min(bounds.size.x, bounds.size.z) * 0.45f;
     break;
    }
   }
  }

  // Median horizontal distance of the lowest vertices from the trunk axis.
  static float TrunkRadius(Transform root, GameObject model, Bounds bounds) {
   var samples = new List<float>();
   float limit = bounds.min.y + Mathf.Clamp(bounds.size.y * 0.12f, 0.4f, 1.6f);
   var lod = root.GetComponent<LODGroup>();
   var lod0 = lod ? new HashSet<Renderer>(lod.GetLODs()[0].renderers.Where(r => r)) : null;
   foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) {
    var r = mf.GetComponent<MeshRenderer>();
    if (!mf.sharedMesh || !r || (lod0 != null && !lod0.Contains(r))) continue;
    if (!mf.sharedMesh.isReadable) continue;
    var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
    var verts = mf.sharedMesh.vertices;
    for (int i = 0; i < verts.Length; i += Mathf.Max(1, verts.Length / 4000)) {
     var p = m.MultiplyPoint3x4(verts[i]);
     if (p.y <= limit) samples.Add(new Vector2(p.x, p.z).magnitude);
    }
   }
   float fallback = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.06f, 0.15f, 0.7f);
   if (samples.Count < 8) return fallback;
   samples.Sort();
   float median = samples[samples.Count / 2];
   return Mathf.Clamp(median * 0.9f, 0.12f, Mathf.Max(0.2f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.25f));
  }

  // ---------- details (terrain detail meshes need the mesh on the prefab root) ----------
  static WorldKitEntry PrepareDetail(KitSpec spec, GameObject root, GameObject model, Bounds bounds) {
   var lodGroup = model.GetComponentInChildren<LODGroup>(true);
   Renderer[] candidates = lodGroup ? lodGroup.GetLODs()[0].renderers.Where(r => r is MeshRenderer).ToArray() : model.GetComponentsInChildren<MeshRenderer>(true);
   var parts = new List<CombineInstance>();
   var materials = new List<Material>();
   foreach (var r in candidates) {
    var mf = r ? r.GetComponent<MeshFilter>() : null;
    if (!mf || !mf.sharedMesh) continue;
    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) {
     parts.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix });
     materials.Add(r.sharedMaterials[Mathf.Min(s, r.sharedMaterials.Length - 1)]);
    }
   }
   if (parts.Count == 0) { report.Add("- DETAIL " + spec.key + ": no mesh"); return null; }
   // Detail prototypes support one material: keep the first and merge geometry that shares it.
   var mat = materials[0];
   var same = parts.Where((p, i) => materials[i] == mat).ToArray();
   var mesh = new Mesh { name = spec.key + "_detail" };
   mesh.indexFormat = IndexFormat.UInt32;
   mesh.CombineMeshes(same, true, true);
   mesh.RecalculateBounds();
   string meshPath = PrefabFolder + "/Detail/" + spec.key + "_mesh.asset";
   var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
   if (existingMesh) { EditorUtility.CopySerialized(mesh, existingMesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = existingMesh; }
   else AssetDatabase.CreateAsset(mesh, meshPath);
   var go = new GameObject(spec.key);
   try {
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var mr = go.AddComponent<MeshRenderer>();
    mr.sharedMaterial = mat;
    mr.shadowCastingMode = spec.noShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
    int layer = LayerMask.NameToLayer("Foliage");
    if (layer >= 0) go.layer = layer;
    string path = PrefabFolder + "/Detail/" + spec.key + ".prefab";
    PrefabUtility.SaveAsPrefabAsset(go, path);
    var entry = Entry(spec, mesh.bounds, go, go, 1, "WorldKit/Detail/" + spec.key);
    entry.collider = "none";
    report.Add($"- {spec.key} [detail {spec.role}] {mesh.bounds.size.x:0.00}x{mesh.bounds.size.y:0.00} m, {mesh.triangles.Length / 3} tris");
    return entry;
   } finally { UnityEngine.Object.DestroyImmediate(go); }
  }

  // ---------- VFX ----------
  static WorldKitEntry PrepareVfx(KitSpec spec, GameObject root, GameObject model) {
   var systems = model.GetComponentsInChildren<ParticleSystem>(true);
   float life = 0.1f;
   int cap = spec.maxParticles > 0 ? spec.maxParticles : 0;
   foreach (var s in systems) {
    var main = s.main;
    // Hierarchy scaling lets the runtime scale an effect from its root; only switch when the parent
    // chain is unit-scaled so the authored look is unchanged.
    if (main.scalingMode != ParticleSystemScalingMode.Hierarchy && UnitParentChain(s.transform, root.transform)) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
    main.playOnAwake = true;
    main.stopAction = ParticleSystemStopAction.None;
    main.cullingMode = spec.loop ? ParticleSystemCullingMode.Pause : ParticleSystemCullingMode.AlwaysSimulate;
    if (!spec.loop) main.loop = false;
    if (cap > 0) main.maxParticles = Mathf.Min(main.maxParticles, cap);
    var collision = s.collision;
    if (collision.enabled) { collision.quality = ParticleSystemCollisionQuality.Low; collision.maxCollisionShapes = 32; }
    var lights = s.lights;
    if (lights.enabled) lights.maxLights = Mathf.Min(lights.maxLights, 2);
    float dur = main.duration + main.startDelay.constantMax + main.startLifetime.constantMax;
    if (!main.loop) life = Mathf.Max(life, dur);
    else life = Mathf.Max(life, Mathf.Min(dur, 4f));
   }
   if (spec.fxScale > 0f && Mathf.Abs(spec.fxScale - 1f) > 0.001f) model.transform.localScale *= spec.fxScale;
   if (spec.life > 0f) life = spec.life;
   if (float.IsInfinity(life) || float.IsNaN(life) || life > 30f) life = 6f;
   string path = PrefabFolder + "/VFX/" + spec.key + ".prefab";
   PrefabUtility.SaveAsPrefabAsset(root, path);
   var entry = new WorldKitEntry {
    key = spec.key, source = spec.source, role = "vfx", biomes = spec.biomes, tags = spec.tags, collider = "none",
    weight = spec.weight, life = life, loop = spec.loop, resource = "WorldKit/VFX/" + spec.key,
    triangles = systems.Length
   };
   report.Add($"- {spec.key} [vfx] {systems.Length} systems, life {life:0.0}s, loop {spec.loop}");
   return entry;
  }

  static bool UnitParentChain(Transform t, Transform stop) {
   for (var p = t.parent; p && p != stop; p = p.parent) {
    var ls = p.localScale;
    if (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f || Mathf.Abs(ls.z - 1f) > 0.01f) return false;
   }
   return true;
  }

  // ---------- manifest helpers ----------
  static WorldKitEntry Entry(KitSpec spec, Bounds bounds, GameObject root, GameObject model, int lods, string resource) {
   long tris = 0;
   var lod = root.GetComponent<LODGroup>();
   IEnumerable<Renderer> lod0 = lod ? lod.GetLODs()[0].renderers.Where(r => r) : model.GetComponentsInChildren<Renderer>(true);
   foreach (var r in lod0) {
    Mesh m = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
    if (!m) continue;
    for (int s = 0; s < m.subMeshCount; s++) tris += m.GetIndexCount(s) / 3;
   }
   return new WorldKitEntry {
    key = spec.key, source = spec.source, role = spec.role, biomes = spec.biomes, tags = spec.tags,
    minHeight = spec.scaleMin, maxHeight = spec.scaleMax, weight = spec.weight, align = spec.align, sink = spec.sink,
    footprint = spec.footprint > 0f ? spec.footprint : Mathf.Max(bounds.extents.x, bounds.extents.z),
    size = bounds.size, center = bounds.center, minY = bounds.min.y, triangles = tris, lods = lods, resource = resource,
    detail = spec.detail
   };
  }

  public static Bounds RendererBounds(Transform root, GameObject model) {
   bool has = false; Bounds b = new Bounds();
   foreach (var r in model.GetComponentsInChildren<Renderer>(true)) {
    if (r is ParticleSystemRenderer) continue;
    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
    if (!mesh) continue;
    var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
    var lb = mesh.bounds;
    for (int i = 0; i < 8; i++) {
     var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
     var p = m.MultiplyPoint3x4(corner);
     if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
    }
   }
   return b;
  }

  // ---------- layers ----------
  static void EnsureLayers() {
   var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
   var layers = tagManager.FindProperty("layers");
   string[] wanted = { "WorldStatic", "Foliage", "SmallProps", "Actors", "Water" };
   int[] slots = { 8, 9, 10, 11, 4 };
   bool changed = false;
   for (int i = 0; i < wanted.Length; i++) {
    if (LayerMask.NameToLayer(wanted[i]) >= 0) continue;
    var p = layers.GetArrayElementAtIndex(slots[i]);
    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = wanted[i]; changed = true; }
   }
   if (changed) tagManager.ApplyModifiedProperties();
  }

  // ---------- terrain layers ----------
  // Albedos are the colour-graded copies in GameContent/Terrain/Textures (the Idyllic ground textures
  // ship pre-tint: yellow grass, orange dirt, grey rock); the vendor originals are the fallback.
  const string Graded = "Assets/GameContent/Terrain/Textures/";
  static readonly (string name, string albedo, string fallback, string normal, float tile, float smooth, float normalScale)[] TerrainLayers = {
   ("Grass", Graded + "Realm_Grass_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Grass/Grass_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Grass/Grass_Normal.png", 9f, 0.06f, 0.8f),
   ("Forest", Graded + "Realm_Forest_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Forest/Forest_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Forest/Forest_Normal.png", 8f, 0.08f, 1f),
   ("Dirt", Graded + "Realm_Dirt_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Dirt/Dirt_01_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Dirt & Stone/Dirt_Stone_Normal.png", 6f, 0.1f, 1f),
   ("Cobble", Graded + "Realm_Cobble_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Cobblestone/Cobblestone_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Cobblestone/Cobblestone_Normal.png", 4.5f, 0.2f, 1f),
   ("Rock", Graded + "Realm_Rock_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Rock/Rock_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Rock/Rock_Normal.png", 11f, 0.14f, 1f),
   // Cliff faces: the Terrain Toolkit's photographic layered rock, graded, with a normal map from its relief.
   ("Cliff", Graded + "Realm_Cliff_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Cliff/Cliff_Albedo.png", Graded + "Realm_Cliff_Normal.png", 16f, 0.12f, 0.8f),
   ("Sand", Graded + "Realm_Sand_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Sand/Sand_Albedo.png", "Assets/Idyllic Fantasy Nature/Textures/Ground/Sand/Sand_Normal.png", 10f, 0.05f, 0.7f),
   ("Snow", Graded + "Realm_Snow_Albedo.png", "Assets/com/heparo/terrain/toolkit/textures/Snow.jpg", "Assets/Idyllic Fantasy Nature/Textures/Ground/Sand/Sand_Normal.png", 12f, 0.34f, 0.6f)
  };

  public static void PrepareTerrainLayers() {
   const string folder = "Assets/Resources/World/Terrain";
   Directory.CreateDirectory(folder);
   foreach (var spec in TerrainLayers) {
    string path = folder + "/" + spec.name + ".terrainlayer";
    var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
    if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
    string albedoPath = spec.albedo;
    if (PrepareGradedTexture(albedoPath) == null) { report.Add("- NOTE graded terrain albedo missing, using vendor original: " + spec.albedo); albedoPath = spec.fallback; }
    var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
    if (!albedo && spec.name == "Snow") albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Symphonie/StylizedForestKit Samples/Shader_URP/Shader/T_snow_D.png");
    if (!string.IsNullOrEmpty(spec.normal) && spec.normal.StartsWith(Graded)) PrepareGradedNormal(spec.normal);
    var normal = string.IsNullOrEmpty(spec.normal) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(spec.normal);
    if (normal && !(AssetImporter.GetAtPath(spec.normal) is TextureImporter ni && ni.textureType == TextureImporterType.NormalMap)) normal = null;
    if (!albedo) { Debug.LogWarning("[Kit] terrain albedo missing for " + spec.name + ": " + spec.albedo); continue; }
    EnsureTextureSettings(albedoPath, false);
    if (normal) EnsureTextureSettings(spec.normal, true);
    layer.diffuseTexture = albedo;
    layer.normalMapTexture = normal;
    layer.normalScale = normal ? spec.normalScale : 0f;
    layer.tileSize = new Vector2(spec.tile, spec.tile);
    layer.tileOffset = Vector2.zero;
    layer.metallic = 0f;
    layer.smoothness = spec.smooth;
    // Unity 6 defaults new layers to "diffuse alpha" smoothness; our albedos are opaque RGB, which made
    // every layer mirror-smooth (the ground reflected the sky and read as white).
    layer.smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly;
    layer.diffuseRemapMin = Vector4.zero; layer.diffuseRemapMax = Vector4.one;
    layer.name = spec.name; // matches the asset file name (Unity warns otherwise)
    EditorUtility.SetDirty(layer);
   }
   AssetDatabase.SaveAssets();
  }

  // Our own graded copies get terrain-friendly import settings (vendor textures are never touched).
  static Texture2D PrepareGradedTexture(string path) {
   if (!File.Exists(path)) return null;
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   if (!importer) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
   if (importer) {
    bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                   importer.anisoLevel != 4 || importer.maxTextureSize != 2048 || importer.textureCompression != TextureImporterCompression.CompressedHQ;
    var android = importer.GetPlatformTextureSettings("Android");
    if (!android.overridden || android.maxTextureSize != 1024) { android.overridden = true; android.maxTextureSize = 1024; android.format = TextureImporterFormat.ASTC_6x6; importer.SetPlatformTextureSettings(android); changed = true; }
    if (changed) {
     importer.textureType = TextureImporterType.Default;
     importer.sRGBTexture = true;
     importer.mipmapEnabled = true;
     importer.streamingMipmaps = true;
     importer.wrapMode = TextureWrapMode.Repeat;
     importer.filterMode = FilterMode.Trilinear;
     importer.anisoLevel = 4;
     importer.maxTextureSize = 2048;
     importer.textureCompression = TextureImporterCompression.CompressedHQ;
     importer.SaveAndReimport();
    }
   }
   return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }

  // Normal maps we generate (GameContent) get normal-map import settings; vendor normals are only checked.
  static void PrepareGradedNormal(string path) {
   if (!File.Exists(path)) return;
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   if (!importer) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
   if (!importer) return;
   bool changed = importer.textureType != TextureImporterType.NormalMap || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                  importer.anisoLevel != 4 || importer.maxTextureSize != 2048 || importer.textureCompression != TextureImporterCompression.CompressedHQ;
   var android = importer.GetPlatformTextureSettings("Android");
   if (!android.overridden || android.maxTextureSize != 1024) { android.overridden = true; android.maxTextureSize = 1024; android.format = TextureImporterFormat.ASTC_6x6; importer.SetPlatformTextureSettings(android); changed = true; }
   if (!changed) return;
   importer.textureType = TextureImporterType.NormalMap;
   importer.mipmapEnabled = true;
   importer.streamingMipmaps = true;
   importer.wrapMode = TextureWrapMode.Repeat;
   importer.filterMode = FilterMode.Trilinear;
   importer.anisoLevel = 4;
   importer.maxTextureSize = 2048;
   importer.textureCompression = TextureImporterCompression.CompressedHQ;
   importer.SaveAndReimport();
  }

  // Vendor textures are never re-imported from here; mismatches are reported for review.
  static void EnsureTextureSettings(string path, bool normal) {
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   if (!importer) return;
   if (normal && importer.textureType != TextureImporterType.NormalMap) report.Add("- NOTE terrain normal not imported as NormalMap: " + path);
   if (importer.wrapMode == TextureWrapMode.Clamp) report.Add("- NOTE terrain texture clamps (tiling seams possible): " + path);
  }
 }
}
