using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LostRealmsOW {
 public static class OpenWorldContentPreparation {
  const string Output = "Assets/GameContent/Imported";
  static readonly Dictionary<string, AnimationClip[]> packClips = new Dictionary<string, AnimationClip[]>();
  static readonly List<string> report = new List<string>();
  static bool expansionOnly;
  [MenuItem("Lost Realms OW/Content/Prepare Wildlife Expansion")]
  public static void PrepareExpansion() {
   expansionOnly = true;
   try { Prepare(); PrepareAnimalGaits(); } finally { expansionOnly = false; }
   ClaudeAnimalPrep.Run(); // clip picks, gait speeds, facing and saddles for every prepared animal (Claude)
  }
  static void PrepareAnimalGaits() {
   string[] keys = { "wildlife_a868c1dd18", "wildlife_83c3ea98a8" };
   string[] names = { "Horse", "Deer" };
   for (int i = 0; i < keys.Length; i++) {
    string path = Output + "/Prefabs/" + keys[i] + ".prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    try {
     var driver = root.GetComponent<OpenWorldImportedModel>();
     var run = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Animals_FREE/Animations/Other_Animations/" + names[i] + "_001_run.anim");
     if (!driver || !run) throw new InvalidOperationException("Animal running clip missing: " + names[i]);
     driver.Run = CleanClip(run, keys[i] + "_run");
     PrefabUtility.SaveAsPrefabAsset(root, path);
    } finally { PrefabUtility.UnloadPrefabContents(root); }
   }
   AssetDatabase.SaveAssets();
  }
  [MenuItem("Lost Realms OW/Content/Prepare Imported Regional Content")]
  public static void Prepare() {
   // Batch editors can start with an untitled recovery scene, which cannot host additive preparation.
   if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/VerdantOpenWorld.unity", OpenSceneMode.Single);
   var previousScene = SceneManager.GetActiveScene();
   var workScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
   SceneManager.SetActiveScene(workScene);
   try { PrepareSources(); }
   finally {
    if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
    EditorSceneManager.CloseScene(workScene, true);
   }
   if (!expansionOnly) ClaudeAnimalPrep.Run();
  }
  static void PrepareSources() {
   var plan = JsonUtility.FromJson<ImportedContentManifest>(File.ReadAllText("Docs/AssetAudit/selected-content.json"));
   Directory.CreateDirectory(Output + "/Prefabs");
   Directory.CreateDirectory(Output + "/Materials");
   Directory.CreateDirectory(Output + "/Animations");
   Directory.CreateDirectory("Assets/Resources/World");
   AssetDatabase.Refresh();
   if (!expansionOnly) PrepareTerrainLayers();
   var settings = AddressableAssetSettingsDefaultObject.Settings;
   if (!settings) throw new InvalidOperationException("Addressables settings are required.");
   if (!expansionOnly) AsterPhase1.PrepareImportedMotionSet();
   var accepted = new List<ImportedContentEntry>();
   if (expansionOnly && File.Exists("Assets/Resources/World/ImportedContent.json"))
    accepted.AddRange(JsonUtility.FromJson<ImportedContentManifest>(File.ReadAllText("Assets/Resources/World/ImportedContent.json")).entries.Where(e => !e.key.StartsWith("expansion_")));
   report.Clear(); report.Add("# Imported content preparation — " + DateTime.Now.ToString("yyyy-MM-dd") + "\n");
   foreach (var entry in plan.entries) {
    if (expansionOnly && !entry.key.StartsWith("expansion_")) continue;
    try {
     string assetPath = entry.source;
     if (entry.category != "audio") {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.source);
      if (!source) { report.Add("- DEFERRED " + entry.source + ": model/prefab did not import."); continue; }
      long triangles = TriangleCount(source);
      entry.triangles = triangles;
      long limit = entry.category == "mountain" ? 150000 : entry.category == "enemy" || entry.category == "npc" ? 60000 : 40000;
      if (triangles > limit) { report.Add($"- DEFERRED {entry.source}: {triangles} triangles exceed the {limit} per-asset preparation budget."); continue; }
      var stage = new GameObject("Content preparation"); stage.SetActive(false);
      try {
       var wrapper = new GameObject(Path.GetFileNameWithoutExtension(entry.source));
       wrapper.transform.SetParent(stage.transform, false);
       var model = UnityEngine.Object.Instantiate(source, wrapper.transform, false);
       foreach (var transform in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
       var scripts = model.GetComponentsInChildren<MonoBehaviour>(true);
       var dependencies = scripts.Where(x => x).Select(x => x.GetType().FullName).Distinct().ToArray();
       if (dependencies.Any(x => new[] { "Deform", "Cloth", "InverseKinematics", "DynamicBone", "WaterRenderer" }.Any(term => x.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))) {
        report.Add("- DEFERRED " + entry.source + ": essential deformation/render dependencies need a dedicated adapter: " + string.Join(", ", dependencies)); continue;
       }
       var pendingScripts = scripts.Where(x => x).ToList();
       while (pendingScripts.Count > 0) {
        var removable = pendingScripts.Find(candidate => !pendingScripts.Any(other => other != candidate && other.GetType().GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>().Any(requirement =>
         new[] { requirement.m_Type0, requirement.m_Type1, requirement.m_Type2 }.Any(type => type != null && type.IsAssignableFrom(candidate.GetType())))));
        if (!removable) throw new InvalidOperationException("Cyclic vendor component requirements need an adapter.");
        pendingScripts.Remove(removable); UnityEngine.Object.DestroyImmediate(removable);
       }
       if (dependencies.Length > 0) report.Add("- Copy adapter replaces scripts on " + entry.key + ": " + string.Join(", ", dependencies));
       foreach (var camera in model.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera);
       foreach (var listener in model.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(listener);
       foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
       foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
       // Demo physics must not make streamed buildings/props fall or push the player.
       foreach (var joint in model.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(joint);
       foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
       bool actor = entry.category == "wildlife" || entry.category == "enemy" || entry.category == "npc";
       if (actor || entry.category == "cloud" || entry.category == "weapon" || entry.category == "vfx" || entry.category == "mountain" || entry.category == "nature") {
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
       }
       foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) {
        var materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++) materials[i] = CompatibleMaterial(materials[i], renderer is ParticleSystemRenderer);
        renderer.sharedMaterials = materials;
       }
       var particles = model.GetComponentsInChildren<ParticleSystem>(true);
       foreach (var system in particles) {
        var main = system.main; main.maxParticles = Mathf.Min(main.maxParticles, Mathf.Max(12, 256 / Mathf.Max(1, particles.Length)));
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
        main.stopAction = ParticleSystemStopAction.None;
        if (entry.alias.StartsWith("fx_") || entry.alias.StartsWith("impact_")) { main.loop = false; main.playOnAwake = false; }
       }
       if (actor) {
        var animation = wrapper.AddComponent<OpenWorldImportedModel>();
        var clips = AnimationUtility.GetAnimationClips(source);
        string pack = entry.source.Split('/')[1];
        if (!packClips.TryGetValue(pack, out var library)) {
         library = AssetDatabase.FindAssets("t:AnimationClip t:Model", new[] { "Assets/" + pack })
          .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
          .Where(c => !c.name.StartsWith("__preview__")).ToArray();
         packClips[pack] = library;
        }
        var animatorRoot = model.GetComponentInChildren<Animator>(true);
        if (animatorRoot && !animatorRoot.hasTransformHierarchy) AnimatorUtility.DeoptimizeTransformHierarchy(animatorRoot.gameObject);
        var legacyRoot = model.GetComponentInChildren<Animation>(true);
        Transform bindingRoot = animatorRoot ? animatorRoot.transform : legacyRoot ? legacyRoot.transform : model.transform;
        string modelName = Path.GetFileNameWithoutExtension(entry.source).Replace("Prefab_", "").Replace("prefab_", "");
        // Generic rigs may share bone names across species: only use own clips or a model-named library clip.
        var sourceDependencies = new HashSet<string>(AssetDatabase.GetDependencies(entry.source, true));
        var fallback = library.Where(c => sourceDependencies.Contains(AssetDatabase.GetAssetPath(c)) || (entry.source.Contains("DungeonCharacters/Skeletons_demo/") && c.name.StartsWith("DS_")) || c.humanMotion || c.name.IndexOf(modelName, StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(c)).IndexOf(System.Text.RegularExpressions.Regex.Replace(modelName, @"[ _\d]", ""), StringComparison.OrdinalIgnoreCase) >= 0);
        clips = clips.Concat(fallback).Distinct().Where(c => CompatibleClip(c, bindingRoot, animatorRoot)).ToArray();
        if (animatorRoot && animatorRoot.isHuman) clips = clips.Concat(HumanoidFallbackClips()).Distinct().ToArray();
        animation.Idle = Pick(clips, "idle", "stand", "breath", "watch");
        animation.Walk = Pick(clips, "walk", "run", "fly");
        animation.Run = Pick(clips, "run", "gallop");
        animation.Attack = Pick(clips, "attack", "bite", "slash");
        animation.Hit = Pick(clips, "hit", "hurt", "damage", "damege"); // Wolf_Animated vendor spelling.
        animation.Death = Pick(clips, "death", "dead", "die");
        if (animatorRoot && animatorRoot.isHuman) animation.Talk = StoreClip("Assets/Kevin Iglesias/Human Animations/Animations/Female/Social/Conversation/HumanF@Talk01.fbx");
        if (!animation.Idle) {
         foreach (var diagnostic in library.Take(4)) report.Add("  - Rig diagnostic " + diagnostic.name + ": " + string.Join(", ", AnimationUtility.GetCurveBindings(diagnostic).Where(b => !string.IsNullOrEmpty(b.path) && !bindingRoot.Find(b.path)).Select(b => b.path + "/" + b.propertyName).Distinct().Take(4)));
         report.Add("- DEFERRED " + entry.source + ": no compatible idle clip. Source/library=" + AnimationUtility.GetAnimationClips(source).Length + "/" + library.Length + "; root=" + bindingRoot.name + "; compatible clips=" + string.Join(", ", clips.Select(c => c.name).Take(20)) + ". Rig/animation binding needs review."); continue; }
        // A partial demo rig must not stand idle when taking damage or dying in combat.
        if (entry.category == "enemy") {
         var missing = new List<string>();
         if (!animation.Walk) missing.Add("walk");
         if (!animation.Attack) missing.Add("attack");
         if (!animation.Hit) missing.Add("hit");
         if (!animation.Death) missing.Add("death");
         if (missing.Count > 0) { report.Add("- DEFERRED " + entry.source + ": incomplete compatible combat motion set: " + string.Join(", ", missing) + ". Keep as a source library until retargeting is validated."); continue; }
        }
        animation.Idle = CleanClip(animation.Idle, entry.key + "_idle");
        animation.Walk = CleanClip(animation.Walk, entry.key + "_walk");
        animation.Run = CleanClip(animation.Run, entry.key + "_run");
        animation.Attack = CleanClip(animation.Attack, entry.key + "_attack");
        animation.Hit = CleanClip(animation.Hit, entry.key + "_hit");
        animation.Death = CleanClip(animation.Death, entry.key + "_death");
        animation.Talk = CleanClip(animation.Talk, entry.key + "_talk");
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) { animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
       }
       bool rocky = entry.category == "mountain" || entry.category == "nature" && entry.source.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0;
       if (rocky) {
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh) {
         var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
        }
       }
       if (entry.category == "nature" && !rocky && entry.height > 3) {
        var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
        var trunk = wrapper.AddComponent<CapsuleCollider>();
        trunk.center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .25f, bounds.center.z);
        trunk.height = bounds.size.y * .5f; trunk.radius = Mathf.Max(.08f, Mathf.Min(bounds.size.x, bounds.size.z) * .055f);
       }
       if (!actor && entry.category != "cloud" && entry.category != "nature" && entry.category != "vfx" && entry.category != "weapon" && entry.category != "mountain" && !model.GetComponentInChildren<Collider>()) {
        var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
        var collider = wrapper.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
       }
       assetPath = Output + "/Prefabs/" + entry.key + ".prefab";
       PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
       report.Add($"- PREPARED {entry.region}/{entry.category}: {entry.source} ({triangles} triangles). Multi-material slots retained; vendor controllers removed from the prepared copy.");
      } finally { UnityEngine.Object.DestroyImmediate(stage); }
     } else if (!AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath)) { report.Add("- DEFERRED " + assetPath + ": audio import unavailable."); continue; }
     var group = settings.FindGroup("Imported_" + entry.region) ?? settings.CreateGroup("Imported_" + entry.region, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
     var schema = group.GetSchema<BundledAssetGroupSchema>();
     schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
     var address = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
     entry.address = "ow.imported." + entry.key;
     address.address = entry.address;
     address.SetLabel("ImportedContent", true, true);
     accepted.Add(entry);
    } catch (Exception e) { report.Add("- DEFERRED " + entry.source + ": " + e.Message); }
   }
   var activeAddresses = new HashSet<string>(accepted.Select(e => e.address));
   foreach (var stale in settings.groups.Where(g => g && g.Name.StartsWith("Imported_")).SelectMany(g => g.entries).Where(e => e.address.StartsWith("ow.imported.") && !activeAddresses.Contains(e.address)).ToArray()) settings.RemoveAssetEntry(stale.guid);
   File.WriteAllText("Assets/Resources/World/ImportedContent.json", JsonUtility.ToJson(new ImportedContentManifest { entries = accepted.ToArray() }, true));
   report.Add($"\nPrepared {accepted.Count} of {plan.entries.Length} selected entries. Addressable bundles still need a platform build before player deployment. Texture residency and frame time require device profiling.");
   File.WriteAllLines(expansionOnly ? "Docs/AssetAudit/EXPANSION_PREPARATION_REPORT.md" : "Docs/AssetAudit/PREPARATION_REPORT.md", report);
   EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
  }
  public static void Run() { try { Prepare(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
  static void PrepareTerrainLayers() {
   const string folder = "Assets/Resources/World/Terrain";
   Directory.CreateDirectory(folder);
   foreach (string name in new[] { "Grass", "Dirt", "Rock", "Snow" }) {
    var source = name == "Snow" ? null : AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/" + name + "_Layer.terrainlayer");
    string path = folder + "/" + name + ".terrainlayer";
    var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
    if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
    if (source) EditorUtility.CopySerialized(source, layer);
    else if (name == "Snow") layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Symphonie/StylizedForestKit Samples/Shader_URP/Shader/T_snow_D.png");
    if (!layer.diffuseTexture) throw new InvalidOperationException("Terrain surface missing: " + name);
    layer.name = "Imported " + name;
    layer.tileSize = new Vector2(name == "Rock" ? 8 : 6, name == "Rock" ? 8 : 6);
    layer.metallic = 0; layer.smoothness = .12f;
    EditorUtility.SetDirty(layer);
   }
  }
  static long TriangleCount(GameObject source) {
   var meshes = source.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh)
    .Concat(source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(m => m.sharedMesh)).Where(m => m);
   long count = 0;
   foreach (var mesh in meshes) for (int i = 0; i < mesh.subMeshCount; i++) count += (long)mesh.GetIndexCount(i) / 3;
   return count;
  }
  static AnimationClip StoreClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.humanMotion && !c.name.StartsWith("__preview__"));
  static IEnumerable<AnimationClip> HumanoidFallbackClips() {
   const string folder = "Assets/Kevin Iglesias/Human Animations/Animations/Female/";
   foreach (var relative in new[] { "Idles/HumanF@Idle01.fbx", "Movement/Walk/HumanF@Walk01_Forward.fbx", "Combat/1H/HumanF@Attack1H01_R.fbx", "Combat/HumanF@CombatDamage01.fbx", "Combat/HumanF@Death01.fbx" }) {
    var clip = StoreClip(folder + relative); if (clip) yield return clip;
   }
  }
  static bool CompatibleClip(AnimationClip clip, Transform root, Animator animator) {
   if (clip.humanMotion) return animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman;
   var bindings = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();
   if (bindings.Length == 0) return false;
   int resolved = bindings.Count(b => string.IsNullOrEmpty(b.path) || b.path.EndsWith("/Bip01 Footsteps") || root.Find(b.path));
   // Expansion pass: a clip that drives 90% of its curves on this rig is the rig's own clip with a few
   // extra (missing) bones, which the animator simply ignores (Rhino_PBR's clips, 360 of 370).
   return resolved == bindings.Length || (expansionOnly && resolved >= bindings.Length * 0.9f);
  }
  static AnimationClip CleanClip(AnimationClip source, string key) {
   if (!source) return null;
   string path = Output + "/Animations/" + key + ".anim";
   var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
   if (!copy) { copy = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(copy, path); }
   else EditorUtility.CopySerialized(source, copy);
   foreach (var binding in AnimationUtility.GetCurveBindings(copy)) if (binding.path.EndsWith("/Bip01 Footsteps")) AnimationUtility.SetEditorCurve(copy, binding, null);
   var clipSettings = AnimationUtility.GetAnimationClipSettings(copy);
   clipSettings.loopTime = key.EndsWith("_idle") || key.EndsWith("_walk") || key.EndsWith("_talk") || key.EndsWith("_run");
   AnimationUtility.SetAnimationClipSettings(copy, clipSettings);
   AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
   EditorUtility.SetDirty(copy);
   return copy;
  }
  static AnimationClip Pick(IEnumerable<AnimationClip> clips, params string[] terms) {
   foreach (string term in terms) {
    var clip = clips.FirstOrDefault(c => c.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
    if (clip) return clip;
   }
   return null;
  }
  // Saved (serialized) material properties, readable even when the material's shader is missing.
  static Texture SavedTexture(Material m, string name) {
   if (!m) return null;
   var envs = new SerializedObject(m).FindProperty("m_SavedProperties.m_TexEnvs");
   for (int i = 0; envs != null && i < envs.arraySize; i++) {
    var e = envs.GetArrayElementAtIndex(i);
    if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
   }
   return null;
  }
  static bool SavedColor(Material m, string name, out Color color) {
   color = Color.white;
   var list = m ? new SerializedObject(m).FindProperty("m_SavedProperties.m_Colors") : null;
   for (int i = 0; list != null && i < list.arraySize; i++) {
    var e = list.GetArrayElementAtIndex(i);
    if (e.FindPropertyRelative("first").stringValue == name) { color = e.FindPropertyRelative("second").colorValue; return true; }
   }
   return false;
  }
  static bool SavedFloat(Material m, string name, out float value) {
   value = 0f;
   var list = m ? new SerializedObject(m).FindProperty("m_SavedProperties.m_Floats") : null;
   for (int i = 0; list != null && i < list.arraySize; i++) {
    var e = list.GetArrayElementAtIndex(i);
    if (e.FindPropertyRelative("first").stringValue == name) { value = e.FindPropertyRelative("second").floatValue; return true; }
   }
   return false;
  }
  static Material CompatibleMaterial(Material source, bool particle) {
   if (source && source.shader &&
      (source.GetTag("RenderPipeline", false, "") == "UniversalPipeline" || source.shader.name.Contains("URP"))) return source;
   string id = "missing";
   if (source && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId)) id = guid + "_" + localId;
   if (string.IsNullOrEmpty(id)) id = source ? source.name.Replace('/', '_') : "missing";
   string path = Output + "/Materials/" + id + (particle ? "_particle" : "_lit") + ".mat";
   var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

   var shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
   if (!shader) throw new InvalidOperationException("Required URP shader is unavailable.");
   var material = existing ? existing : new Material(shader) { name = source ? source.name + " URP" : "Imported missing material" };
   Texture texture = source && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source && source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
   Color color = source && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source && source.HasProperty("_Color") ? source.GetColor("_Color") : source && source.HasProperty("_TintColor") ? source.GetColor("_TintColor") : Color.white;
   // HDRP materials (or any whose shader is missing here) still carry their saved properties.
   bool foreign = source && (!source.shader || source.shader.name.Contains("InternalError") || source.shader.name.StartsWith("HDRP/"));
   if (foreign) {
    texture = texture ? texture : SavedTexture(source, "_BaseColorMap") ?? SavedTexture(source, "_MainTex") ?? SavedTexture(source, "_BaseMap");
    if (SavedColor(source, "_BaseColor", out var saved) || SavedColor(source, "_Color", out saved)) color = saved;
   }
   material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", color);
   if (source) {
    string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
    if (source.HasProperty(map)) { material.SetTextureScale("_BaseMap", source.GetTextureScale(map)); material.SetTextureOffset("_BaseMap", source.GetTextureOffset(map)); }
    if (source.HasProperty("_EmissionMap") && material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
    if (source.HasProperty("_EmissionColor") && material.HasProperty("_EmissionColor")) {
     material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
     if (source.IsKeywordEnabled("_EMISSION") || source.GetColor("_EmissionColor").maxColorComponent > .01f) material.EnableKeyword("_EMISSION");
    }
   }
   if (source && source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") && material.HasProperty("_BumpMap")) { material.SetTexture("_BumpMap", source.GetTexture("_BumpMap")); material.EnableKeyword("_NORMALMAP"); }
   if (foreign) {
    var normal = SavedTexture(source, "_NormalMap") ?? SavedTexture(source, "_BumpMap");
    if (normal) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
    // HDRP mask map: metallic R, occlusion G, smoothness A, which is URP's metallic-gloss and occlusion layout.
    var mask = SavedTexture(source, "_MaskMap");
    if (mask) {
     material.SetTexture("_MetallicGlossMap", mask); material.EnableKeyword("_METALLICSPECGLOSSMAP");
     material.SetTexture("_OcclusionMap", mask); material.EnableKeyword("_OCCLUSIONMAP");
    }
   }
   if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", source && source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source && source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .15f);
   if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", source && source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
   if (source) {
    foreach (var map in new[] { "_MetallicGlossMap", "_OcclusionMap", "_SpecGlossMap" }) if (source.HasProperty(map) && material.HasProperty(map)) {
     var textureMap = source.GetTexture(map); material.SetTexture(map, textureMap);
     if (textureMap && map == "_MetallicGlossMap") material.EnableKeyword("_METALLICSPECGLOSSMAP");
    }
    if (source.HasProperty("_OcclusionStrength") && material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", source.GetFloat("_OcclusionStrength"));
   }
   if (foreign) {
    material.SetFloat("_Smoothness", SavedFloat(source, "_SmoothnessRemapMax", out float smooth) ? Mathf.Clamp01(smooth) : 0.5f);
    material.SetFloat("_Metallic", SavedFloat(source, "_Metallic", out float metal) ? metal : 0f);
    // Alpha-tested or transparent HDRP layers (fur cards, lashes) become double-sided cut-outs.
    bool cut = SavedFloat(source, "_AlphaCutoffEnable", out float ace) && ace > 0.5f;
    bool transparentLayer = SavedFloat(source, "_SurfaceType", out float st) && st > 0.5f;
    if (cut || transparentLayer) {
     material.SetFloat("_AlphaClip", 1); material.EnableKeyword("_ALPHATEST_ON");
     material.SetFloat("_Cutoff", Mathf.Clamp(SavedFloat(source, "_AlphaCutoff", out float co) ? co : 0.5f, 0.2f, 0.6f));
     material.renderQueue = 2450;
    }
    if (SavedFloat(source, "_DoubleSidedEnable", out float ds) && ds > 0.5f) material.SetFloat("_Cull", 0);
    material.enableInstancing = true;
    if (existing) EditorUtility.SetDirty(material); else AssetDatabase.CreateAsset(material, path);
    return material;
   }
   bool foliage = source && (source.name.ToLowerInvariant().Contains("lea") || source.name.ToLowerInvariant().Contains("grass") || source.name.ToLowerInvariant().Contains("foliage"));
   bool cutout = foliage || source && source.HasProperty("_Mode") && source.GetFloat("_Mode") == 1;
   if (cutout) { material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .4f); material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450; material.SetFloat("_Cull", 0); }
   if (particle || color.a < .99f || source && source.HasProperty("_Mode") && source.GetFloat("_Mode") >= 2) {
    material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
    material.SetFloat("_DstBlend", source && source.shader && source.shader.name.Contains("Additive") ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
    material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
   }
   material.enableInstancing = true;
   if (existing) EditorUtility.SetDirty(material); else AssetDatabase.CreateAsset(material, path);
   return material;
  }
 }
}

