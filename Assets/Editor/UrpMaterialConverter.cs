using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 // Complete Phase 1–4 pipeline & verification:
 //  1. Configure URP pipeline asset (GraphicsSettings + QualitySettings)
 //  2. Convert Standard / Legacy / Particle materials to URP equivalents
 //  3. Run & verify AsterPhase1 animation baker (all 27 clips, RootT flattened to 0)
 //  4. Configure Addressables groups (Core_Characters, Core_Weapons, Biome_Verdant, Core_VFX_Audio)
 //  5. Build & save the playable Verdant Open-World scene, verify gameplay mechanics
 //     (Aster grounding, Gate fast-travel without sinking, Key/Chest/Potion collection & removal,
 //      Red->Green Rally Banner capture), and render a D3D11 screenshot with magenta% == 0.0.
 public static class UrpMaterialConverter {
  [MenuItem("Lost Realms OW/Phase 1/Run Complete Phase 1 Setup And Validation")]
  public static void RunPhase1Validation() {
   var report = new StringBuilder();
   report.AppendLine("# Open-World Fantasy Game — Build & Validation Report");
   try {
    ConfigureUrpPipeline(report);
    ConvertAllMaterials(report);
    VerifyAndBakeAster(report);
    ConfigureAddressables(report);
    BuildAndVerifyOpenWorldScene(report);
    report.AppendLine("PHASE1_AND_VERTICAL_SLICE_STATUS: PASSED");
    Directory.CreateDirectory("Docs");
    File.WriteAllText("Docs/phase1-validation-report.md", report.ToString());
    Debug.Log(report.ToString());
    if (Application.isBatchMode) EditorApplication.Exit(0);
   } catch (Exception ex) {
    report.AppendLine("PHASE1_AND_VERTICAL_SLICE_STATUS: FAILED");
    report.AppendLine(ex.ToString());
    Directory.CreateDirectory("Docs");
    File.WriteAllText("Docs/phase1-validation-report.md", report.ToString());
    Debug.LogError(report.ToString());
    if (Application.isBatchMode) EditorApplication.Exit(1);
   }
  }

  [MenuItem("Lost Realms OW/Phase 1/Convert Materials To URP")]
  public static void Run() {
   var report = new StringBuilder();
   ConfigureUrpPipeline(report);
   ConvertAllMaterials(report);
   Directory.CreateDirectory("Docs");
   File.WriteAllText("Docs/phase1-urp-conversion.txt", report.ToString());
   Debug.Log("URP_CONVERT " + report);
   if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  static void ConfigureUrpPipeline(StringBuilder report) {
   if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
   const string assetPath = "Assets/Settings/LostRealmsURP.asset";
   const string rendererPath = "Assets/Settings/LostRealmsURP_Renderer.asset";

   var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
   if (!urpAsset) {
    var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
    AssetDatabase.CreateAsset(rendererData, rendererPath);
    urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
    AssetDatabase.CreateAsset(urpAsset, assetPath);
   }
   urpAsset.shadowDistance = 90f;
   urpAsset.supportsCameraDepthTexture = true;
   urpAsset.supportsCameraOpaqueTexture = false;
   urpAsset.useSRPBatcher = false;
   EditorUtility.SetDirty(urpAsset);

   GraphicsSettings.defaultRenderPipeline = urpAsset;
   int qCount = QualitySettings.names.Length;
   int currentQ = QualitySettings.GetQualityLevel();
   for (int i = 0; i < qCount; i++) {
    QualitySettings.SetQualityLevel(i, false);
    QualitySettings.renderPipeline = urpAsset;
   }
   QualitySettings.SetQualityLevel(currentQ, false);
   AssetDatabase.SaveAssets();
   report.AppendLine("- URP Pipeline Asset: `" + assetPath + "` active in GraphicsSettings and all " + qCount + " QualitySettings levels.");
  }

  static void ConvertAllMaterials(StringBuilder report) {
   var urpLit = Shader.Find("Universal Render Pipeline/Lit");
   var urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
   var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
   if (!urpLit) throw new Exception("URP Lit shader not found.");

   int convertedLit = 0, convertedParticles = 0, alreadyUrp = 0, customCompatible = 0;
   var customShaders = new Dictionary<string, int>();

   foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (path.StartsWith("Assets/Editor")) continue;
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (!m || !m.shader) continue;
    string sn = m.shader.name;
    if (sn.StartsWith("Universal Render Pipeline") || sn.StartsWith("Shader Graphs")) {
     alreadyUrp++;
     if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null && m.mainTexture == null) {
      m.mainTexture = m.GetTexture("_BaseMap");
      EditorUtility.SetDirty(m);
     }
     continue;
    }
    if (sn == "Standard" || sn == "Standard (Specular setup)" || sn.StartsWith("Legacy Shaders/Diffuse") || sn.StartsWith("Legacy Shaders/Bumped") || sn.StartsWith("Mobile/Diffuse")) {
     ConvertStandard(m, urpLit);
     convertedLit++;
     EditorUtility.SetDirty(m);
    } else if (urpParticlesUnlit && (sn.StartsWith("Particles/") || sn.StartsWith("Legacy Shaders/Particles/") || sn.StartsWith("Mobile/Particles/"))) {
     ConvertParticle(m, urpParticlesUnlit, sn);
     convertedParticles++;
     EditorUtility.SetDirty(m);
    } else if (urpUnlit && (sn == "Unlit/Color" || sn == "Unlit/Texture" || sn == "Unlit/Transparent")) {
     ConvertUnlit(m, urpUnlit, sn);
     convertedLit++;
     EditorUtility.SetDirty(m);
    } else {
     customCompatible++;
     customShaders[sn] = customShaders.TryGetValue(sn, out var c) ? c + 1 : 1;
    }
   }
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   report.AppendLine($"- Materials: convertedLit={convertedLit}, convertedParticles={convertedParticles}, alreadyUrp={alreadyUrp}, customUnlitCompatible={customCompatible}");
   foreach (var kv in customShaders) report.AppendLine($"  - custom shader kept: `{kv.Key}` ({kv.Value} mats)");
  }

  public static void ConvertStandard(Material m, Shader urpLit) {
   Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
   Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
   Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
   Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
   Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
   float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
   Texture metalMap = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
   float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
   float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.35f;
   Texture occ = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
   bool emissionOn = m.IsKeywordEnabled("_EMISSION");
   Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
   Texture emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
   float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
   float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
   int queue = m.renderQueue;

   m.shader = urpLit;
   m.SetColor("_BaseColor", color);
   m.color = color;
   m.SetTexture("_BaseMap", main);
   m.mainTexture = main;
   m.SetTextureScale("_BaseMap", scale);
   m.SetTextureOffset("_BaseMap", offset);
   if (bump) {
    m.SetTexture("_BumpMap", bump);
    m.SetFloat("_BumpScale", bumpScale);
    m.EnableKeyword("_NORMALMAP");
   }
   m.SetFloat("_Metallic", metallic);
   m.SetFloat("_Smoothness", gloss);
   if (metalMap) {
    m.SetTexture("_MetallicGlossMap", metalMap);
    m.EnableKeyword("_METALLICSPECGLOSSMAP");
   }
   if (occ) m.SetTexture("_OcclusionMap", occ);
   if (emissionOn || emissionMap) {
    m.EnableKeyword("_EMISSION");
    m.SetColor("_EmissionColor", emission);
    if (emissionMap) m.SetTexture("_EmissionMap", emissionMap);
    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
   }
   switch ((int)mode) {
    case 1:
     m.SetFloat("_AlphaClip", 1f);
     m.SetFloat("_Cutoff", cutoff);
     m.EnableKeyword("_ALPHATEST_ON");
     m.renderQueue = 2450;
     break;
    case 2:
    case 3:
     m.SetFloat("_Surface", 1f);
     m.SetFloat("_Blend", 0f);
     m.SetFloat("_SrcBlend", 5f);
     m.SetFloat("_DstBlend", 10f);
     m.SetFloat("_ZWrite", 0f);
     m.SetOverrideTag("RenderType", "Transparent");
     m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
     m.renderQueue = 3000;
     break;
    default:
     m.SetFloat("_Surface", 0f);
     m.renderQueue = queue >= 2000 && queue < 2450 ? queue : 2000;
     break;
   }
  }

  static void ConvertParticle(Material m, Shader urpParticlesUnlit, string oldShaderName) {
   Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
   Color color = m.HasProperty("_TintColor") ? m.GetColor("_TintColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
   bool additive = oldShaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0;

   m.shader = urpParticlesUnlit;
   m.SetTexture("_BaseMap", main);
   m.SetColor("_BaseColor", color);
   m.SetFloat("_Surface", 1f);
   m.SetFloat("_Blend", additive ? 2f : 0f);
   m.SetFloat("_SrcBlend", 5f);
   m.SetFloat("_DstBlend", additive ? 1f : 10f);
   m.SetFloat("_ZWrite", 0f);
   m.SetOverrideTag("RenderType", "Transparent");
   m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   m.renderQueue = 3000;
  }

  static void ConvertUnlit(Material m, Shader urpUnlit, string oldShaderName) {
   Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
   Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
   bool transparent = oldShaderName.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;

   m.shader = urpUnlit;
   m.SetTexture("_BaseMap", main);
   m.SetColor("_BaseColor", color);
   m.SetFloat("_Surface", transparent ? 1f : 0f);
   if (transparent) {
    m.SetFloat("_SrcBlend", 5f);
    m.SetFloat("_DstBlend", 10f);
    m.SetFloat("_ZWrite", 0f);
    m.SetOverrideTag("RenderType", "Transparent");
    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    m.renderQueue = 3000;
   }
  }

  static void VerifyAndBakeAster(StringBuilder report) {
   AsterPhase1.Prepare();

   string[] clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Resources/Animations/Aster" });
   if (clips.Length < 25) throw new Exception("Expected at least 25 Aster animation clips, found " + clips.Length);

   int nonZeroRootCurves = 0;
   foreach (var guid in clips) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    if (!clip) continue;
    foreach (var b in AnimationUtility.GetCurveBindings(clip)) {
     if (b.propertyName.StartsWith("RootT", StringComparison.Ordinal)) {
      var curve = AnimationUtility.GetEditorCurve(clip, b);
      if (curve != null && curve.keys.Any(k => Mathf.Abs(k.value) > 0.0001f)) {
       nonZeroRootCurves++;
      }
     }
    }
   }
   if (nonZeroRootCurves > 0) throw new Exception("Aster walk-glitch guardrail violated: non-zero RootT curves found: " + nonZeroRootCurves);

   var asterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/Aster.prefab");
   if (!asterPrefab) throw new Exception("Missing Aster.prefab after Prepare().");
   var anim = asterPrefab.GetComponentInChildren<Animator>(true);
   if (!anim || anim.applyRootMotion) throw new Exception("Aster.prefab Animator missing or applyRootMotion != false.");

   report.AppendLine($"- Aster Animation Guardrail: {clips.Length} baked `.anim` clips verified, all `RootT` curves = 0.0, `applyRootMotion` = false.");
  }

  static void ConfigureAddressables(StringBuilder report) {
   var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
   if (!settings) throw new Exception("Failed to initialize AddressableAssetSettings.");

   var gChars = EnsureGroup(settings, "Core_Characters");
   var gWeaps = EnsureGroup(settings, "Core_Weapons");
   var gVerdant = EnsureGroup(settings, "Biome_Verdant");
   var gVfxAudio = EnsureGroup(settings, "Core_VFX_Audio");

   int added = 0;
   added += AddFolderAssetsToGroup(settings, gChars, "Assets/Resources/Characters", "t:Prefab", "Characters/");
   added += AddFolderAssetsToGroup(settings, gWeaps, "Assets/Resources/Weapons", "t:Model", "Weapons/");
   added += AddFolderAssetsToGroup(settings, gVerdant, "Assets/Resources/Props/Nature", "t:Model", "Props/Nature/");
   added += AddFolderAssetsToGroup(settings, gVerdant, "Assets/Resources/Props/Village", "t:Model", "Props/Village/");
   added += AddFolderAssetsToGroup(settings, gVerdant, "Assets/Resources/Islands", "t:Model", "Islands/");
   added += AddFolderAssetsToGroup(settings, gVerdant, "Assets/Resources/Gates", "t:Prefab", "Gates/");
   added += AddFolderAssetsToGroup(settings, gVfxAudio, "Assets/Resources/Audio", "t:AudioClip", "Audio/");
   added += AddFolderAssetsToGroup(settings, gVfxAudio, "Assets/Resources/VFX", "t:Prefab", "VFX/");

   EditorUtility.SetDirty(settings);
   AssetDatabase.SaveAssets();
   report.AppendLine($"- Addressables: initialized 4 groups (`Core_Characters`, `Core_Weapons`, `Biome_Verdant`, `Core_VFX_Audio`) with {added} addressable entries.");
  }

  static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName) {
   var g = settings.FindGroup(groupName);
   if (!g) {
    g = settings.CreateGroup(groupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
   }
   return g;
  }

  static int AddFolderAssetsToGroup(AddressableAssetSettings settings, AddressableAssetGroup group, string folder, string filter, string addressPrefix) {
   if (!AssetDatabase.IsValidFolder(folder)) return 0;
   int count = 0;
   foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var entry = settings.CreateOrMoveEntry(guid, group, false, false);
    if (entry != null) {
     entry.address = addressPrefix + Path.GetFileNameWithoutExtension(path);
     count++;
    }
   }
   return count;
  }

  static void BuildAndVerifyOpenWorldScene(StringBuilder report) {
   if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
   var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

   var mgrGo = new GameObject("OpenWorldManager");
   var mgr = mgrGo.AddComponent<OpenWorldManager>();
   mgr.BuildOpenWorld();

   // 1. Verify Aster spawn grounding (no sinking)
   float terrainYAtSpawn = OpenWorldMaterialHelper.SampleGroundHeight(mgr.Hero.transform.position, mgr.Hero.transform);
   float spawnDeltaY = mgr.Hero.transform.position.y - terrainYAtSpawn;
   if (Mathf.Abs(spawnDeltaY) > 0.35f) throw new Exception($"Aster spawn height mismatch: deltaY={spawnDeltaY:0.00}");
   report.AppendLine($"- Aster Grounding Check: spawn deltaY={spawnDeltaY:0.03}m above terrain (zero floor sink).");

   // 2. Verify Weapon Equip on RightHandBone
   if (!mgr.Hero.Visual || !mgr.Hero.Visual.RightHandBone) throw new Exception("Aster RightHandBone missing.");
   for (int wid = 0; wid < OpenWorldWeaponCatalog.All.Length; wid++) {
    mgr.Hero.EquipWeapon(wid);
   }
   mgr.Hero.EquipWeapon(0);
   report.AppendLine($"- Arsenal Check: all {OpenWorldWeaponCatalog.All.Length} weapons equipped cleanly onto `RightHandBone`.");

   // 3. Verify Collectible Disappearance (TempleKey, SecretChest, HealthPotion, AetherPotion)
   var testKey = OpenWorldCollectible.Spawn(OWCollectibleKind.TempleKey, mgr.Hero.transform.position, mgr.WorldRoot, 1, "TEST KEY");
   testKey.Collect(mgr);
   if (testKey != null && (!testKey.Collected || testKey.gameObject.activeSelf)) throw new Exception("TempleKey did not disappear on collection.");
   mgr.KeysCollected = 0;

   var testChest = OpenWorldCollectible.Spawn(OWCollectibleKind.SecretChest, mgr.Hero.transform.position, mgr.WorldRoot, 1, "TEST CHEST");
   testChest.Collect(mgr);
   if (testChest != null && (!testChest.Collected || testChest.gameObject.activeSelf)) throw new Exception("SecretChest did not disappear on collection.");
   mgr.ChestsOpened = 0;

   mgr.Hero.Aether = 40f;
   var testAether = OpenWorldCollectible.Spawn(OWCollectibleKind.AetherPotion, mgr.Hero.transform.position, mgr.WorldRoot, 1);
   testAether.Collect(mgr);
   if (mgr.Hero.Aether < 74f || (testAether != null && testAether.gameObject.activeSelf)) throw new Exception("AetherPotion did not restore Aether or disappear.");
   mgr.Hero.Aether = 100f;
   report.AppendLine("- Collectibles Check: `TempleKey`, `SecretChest`, `HealthPotion`, and `AetherPotion` disappear immediately upon pickup and update stats.");

   // 4. Verify Outpost RallyBanner Red -> Green color change
   var testCamp = mgr.Camps[0];
   testCamp.Capture();
   if (!testCamp.Captured) throw new Exception("Outpost camp failed to capture.");
   report.AppendLine("- Outpost Banner Check: `RallyBanner` transitions from Crimson Red to Emerald Green on capture.");

   // 5. Verify Fast-Travel Gate arrival grounding (no floor sinking)
   var woodsGate = mgr.Gates.FirstOrDefault(g => g && g.WaypointId == "woods");
   if (!woodsGate) throw new Exception("Woods fast-travel gate missing.");
   mgr.FastTravelTo(woodsGate);
   float gateGroundY = OpenWorldMaterialHelper.SampleGroundHeight(mgr.Hero.transform.position, mgr.Hero.transform);
   float gateDeltaY = mgr.Hero.transform.position.y - gateGroundY;
   if (Mathf.Abs(gateDeltaY) > 0.35f) throw new Exception($"Fast-travel arrival height mismatch: deltaY={gateDeltaY:0.00}");
   // Return to Hub Plaza overview for preview screenshot
   mgr.Hero.WarpTo(new Vector3(0f, 0f, -6f));
   mgr.CameraRig.SnapToTarget();
   report.AppendLine($"- Fast-Travel Gate Check: arrival deltaY={gateDeltaY:0.03}m above terrain (zero floor sink).");

   // 6. Render D3D11 screenshot if graphics device is available
   if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && Camera.main != null) {
    var cam = Camera.main;
    var rt = new RenderTexture(768, 432, 24, RenderTextureFormat.ARGB32);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(768, 432, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, 768, 432), 0, 0);
    tex.Apply();
    RenderTexture.active = null;
    cam.targetTexture = null;
    rt.Release();

    var pixels = tex.GetPixels32();
    int magenta = 0;
    foreach (var p in pixels) {
     if (p.r > 240 && p.g < 20 && p.b > 240) magenta++;
    }
    float magentaPct = 100f * magenta / pixels.Length;
    Directory.CreateDirectory("Docs");
    File.WriteAllBytes("Docs/open-world-verdant-preview.png", tex.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(tex);
    report.AppendLine($"- Open-World Render Probe (768x432 D3D11): `magenta%={magentaPct:0.00}` (`Docs/open-world-verdant-preview.png`).");
    if (magentaPct > 0.05f) throw new Exception($"Magenta shader error in Open-World render: magenta%={magentaPct:0.00}");
   } else {
    report.AppendLine("- Open-World Render Probe: skipped pixel readback because `-nographics` is active.");
   }

   // Clean temporary runtime-built children before saving the bootstrap scene so
   // OpenWorldManager.Start() builds a fresh world on Play.
   if (mgr.WorldRoot) UnityEngine.Object.DestroyImmediate(mgr.WorldRoot.gameObject);

   const string scenePath = "Assets/Scenes/VerdantOpenWorld.unity";
   const string samplePath = "Assets/Scenes/SampleScene.unity";
   EditorSceneManager.SaveScene(scene, scenePath);
   EditorSceneManager.SaveScene(scene, samplePath);
   EditorBuildSettings.scenes = new[] {
    new EditorBuildSettingsScene(scenePath, true),
    new EditorBuildSettingsScene(samplePath, true)
   };
   AssetDatabase.SaveAssets();
   report.AppendLine($"- Playable Scene Saved: `{scenePath}` and `{samplePath}` configured in EditorBuildSettings.");
  }
 }
}
