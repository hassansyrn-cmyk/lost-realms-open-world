using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 // One-shot configuration of the project's own URP assets for the high-end look:
 // post-processing enabled on the renderer, SSAO renderer feature, opaque texture for refractive
 // water, soft shadows, HDR colour grading, and the sky material used by OpenWorldAtmosphere.
 public static class OpenWorldRenderSetup {
  const string RendererPath = "Assets/Settings/LostRealmsURP_Renderer.asset";
  const string PipelinePath = "Assets/Settings/LostRealmsURP.asset";
  const string PostDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
  const string SkyPath = "Assets/Resources/World/Realm/RealmSky.mat";

  [MenuItem("Lost Realms OW/Graphics/Configure Render Pipeline")]
  public static void Configure() {
   var log = new StringBuilder();
   var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
   if (!data) {
    var any = AssetDatabase.FindAssets("t:UniversalRendererData").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.StartsWith("Assets/"));
    if (any != null) data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(any);
   }
   if (data) {
    if (!data.postProcessData) {
     data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostDataPath);
     log.AppendLine("post-processing data assigned: " + (data.postProcessData ? "yes" : "MISSING"));
    }
    var existing = data.rendererFeatures.FirstOrDefault(f => f is ScreenSpaceAmbientOcclusion);
    if (!existing) {
     var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
     ssao.name = "ScreenSpaceAmbientOcclusion";
     AssetDatabase.AddObjectToAsset(ssao, data);
     data.rendererFeatures.Add(ssao);
     var so = new SerializedObject(data);
     var map = so.FindProperty("m_RendererFeatureMap");
     if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId)) {
      map.InsertArrayElementAtIndex(map.arraySize);
      map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
     }
     so.ApplyModifiedPropertiesWithoutUndo();
     existing = ssao;
     log.AppendLine("SSAO renderer feature added");
    }
    var fso = new SerializedObject(existing);
    SetFloat(fso, "m_Settings.Intensity", 1.35f);
    SetFloat(fso, "m_Settings.Radius", 0.45f);
    SetFloat(fso, "m_Settings.DirectLightingStrength", 0.3f);
    SetFloat(fso, "m_Settings.Falloff", 120f);
    SetBool(fso, "m_Settings.Downsample", true);
    SetBool(fso, "m_Settings.AfterOpaque", false);
    SetInt(fso, "m_Settings.Source", 1);       // depth-normals
    SetInt(fso, "m_Settings.NormalSamples", 1); // medium
    SetInt(fso, "m_Settings.Samples", 1);       // medium (8)
    SetInt(fso, "m_Settings.BlurQuality", 0);   // bilateral
    SetInt(fso, "m_Settings.AOMethod", 0);      // blue noise
    fso.ApplyModifiedPropertiesWithoutUndo();
    existing.SetActive(true);
    EditorUtility.SetDirty(existing);
    EditorUtility.SetDirty(data);
   } else log.AppendLine("renderer data not found");

   var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
   if (pipeline) {
    var so = new SerializedObject(pipeline);
    SetBool(so, "m_RequireDepthTexture", true);
    SetBool(so, "m_RequireOpaqueTexture", true);
    SetBool(so, "m_SupportsHDR", true);
    SetBool(so, "m_SoftShadowsSupported", true);
    SetInt(so, "m_SoftShadowQuality", 2);
    SetInt(so, "m_ShadowCascadeCount", 3);
    SetFloat(so, "m_ShadowDistance", 120f);
    SetInt(so, "m_ColorGradingMode", 1);
    SetInt(so, "m_ColorGradingLutSize", 32);
    SetInt(so, "m_MainLightShadowmapResolution", 2048);
    so.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(pipeline);
    log.AppendLine("pipeline: opaque texture, HDR, soft shadows, 3 cascades, HDR grading");
   } else log.AppendLine("pipeline asset not found");

   // Sky material (Resources so the runtime and player builds include the shader).
   var skyShader = Shader.Find("LostRealms/Sky");
   if (skyShader) {
    Directory.CreateDirectory(Path.GetDirectoryName(SkyPath));
    var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
    if (!sky) { sky = new Material(skyShader) { name = "RealmSky" }; AssetDatabase.CreateAsset(sky, SkyPath); }
    else sky.shader = skyShader;
    EditorUtility.SetDirty(sky);
    log.AppendLine("sky material ready");
   } else log.AppendLine("LostRealms/Sky shader not found (import pending?)");

   AssetDatabase.SaveAssets();
   string report = log.ToString();
   Debug.Log("[RenderSetup]\n" + report);
   try {
    string shots = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "out"));
    Directory.CreateDirectory(shots);
    File.WriteAllText(Path.Combine(shots, "render_setup.txt"), report);
   } catch { }
  }

  static void SetFloat(SerializedObject so, string path, float v) { var p = so.FindProperty(path); if (p != null) p.floatValue = v; }
  // intValue writes the raw enum value, so explicit-valued enums (shadow resolution) work too.
  static void SetInt(SerializedObject so, string path, int v) { var p = so.FindProperty(path); if (p != null) p.intValue = v; }
  static void SetBool(SerializedObject so, string path, bool v) { var p = so.FindProperty(path); if (p != null) p.boolValue = v; }
 }
}
