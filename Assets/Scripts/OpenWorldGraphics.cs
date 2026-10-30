using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 // Three quality tiers. Ultra and High target desktop GPUs (full post stack, SSAO, long shadows,
 // dense grass); Balanced keeps phones at a stable frame rate. Everything that scales with the tier
 // (terrain, streaming radius, VFX budget, post-processing) listens to Changed.
 public sealed class OpenWorldGraphics : MonoBehaviour {
  public enum Tier { Balanced = 0, High = 1, Ultra = 2 }
  public static OpenWorldGraphics Instance { get; private set; }
  public static event Action<Tier> Changed;
  const string Preference = "LostRealms.GraphicsTier";
  public Tier Current { get; private set; } = Tier.High;
  public bool High => Current >= Tier.High;
  public bool Ultra => Current == Tier.Ultra;
  public string Label => Current == Tier.Ultra ? "ULTRA" : Current == Tier.High ? "HIGH" : "BALANCED";
  public int TargetFrameRate => Current == Tier.Balanced ? 30 : 60;
  UniversalRenderPipelineAsset runtimePipeline;
  RenderPipelineAsset previousOverride;
  int previousFrameRate, previousVSync;
  float previousLodBias;
  ScriptableRendererFeature ssao;
  bool ssaoWasActive;

  void Awake() {
   Instance = this;
   previousOverride = QualitySettings.renderPipeline;
   previousFrameRate = Application.targetFrameRate;
   previousVSync = QualitySettings.vSyncCount;
   previousLodBias = QualitySettings.lodBias;
   var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
   if (source) {
    runtimePipeline = Instantiate(source);
    runtimePipeline.name = "Lost Realms Runtime Graphics";
    QualitySettings.renderPipeline = runtimePipeline;
    foreach (var data in runtimePipeline.rendererDataList) {
     if (!data) continue;
     foreach (var feature in data.rendererFeatures) if (feature is ScreenSpaceAmbientOcclusion) { ssao = feature; ssaoWasActive = feature.isActive; }
    }
   }
   int fallback = Application.isMobilePlatform ? (int)Tier.Balanced : (int)Tier.High;
   Apply((Tier)Mathf.Clamp(PlayerPrefs.GetInt(Preference, fallback), 0, 2), false);
  }

  // Legacy toggle used by older menus: false = Balanced, true = High.
  public void Apply(bool high, bool save = true) => Apply(high ? Tier.High : Tier.Balanced, save);

  public void Cycle() => Apply((Tier)(((int)Current + 1) % 3));

  public void Apply(Tier tier, bool save = true) {
   Current = tier;
   bool mobile = Application.isMobilePlatform;
   if (runtimePipeline) {
    switch (tier) {
     case Tier.Ultra:
      runtimePipeline.renderScale = 1f;
      runtimePipeline.msaaSampleCount = 4;
      runtimePipeline.shadowDistance = 170f;
      runtimePipeline.shadowCascadeCount = 4;
      runtimePipeline.cascade4Split = new Vector3(0.06f, 0.16f, 0.4f);
      runtimePipeline.mainLightShadowmapResolution = 4096;
      runtimePipeline.supportsHDR = true;
      runtimePipeline.supportsCameraOpaqueTexture = true;
      runtimePipeline.supportsCameraDepthTexture = true;
      runtimePipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
      runtimePipeline.maxAdditionalLightsCount = 8;
      break;
     case Tier.High:
      runtimePipeline.renderScale = 1f;
      runtimePipeline.msaaSampleCount = 2;
      runtimePipeline.shadowDistance = 120f;
      runtimePipeline.shadowCascadeCount = 3;
      runtimePipeline.cascade3Split = new Vector2(0.12f, 0.36f);
      runtimePipeline.mainLightShadowmapResolution = 2048;
      runtimePipeline.supportsHDR = true;
      runtimePipeline.supportsCameraOpaqueTexture = true;
      runtimePipeline.supportsCameraDepthTexture = true;
      runtimePipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
      runtimePipeline.maxAdditionalLightsCount = 6;
      break;
     default:
      runtimePipeline.renderScale = mobile ? 0.8f : 0.9f;
      runtimePipeline.msaaSampleCount = 1;
      runtimePipeline.shadowDistance = 55f;
      runtimePipeline.shadowCascadeCount = 1;
      runtimePipeline.mainLightShadowmapResolution = 1024;
      // HDR stays on: the packed 32-bit HDR buffer costs the same bandwidth as RGBA8, and without it
      // every highlight clipped at 1.0 before tonemapping (~208 sRGB), leaving snow, sky and sand grey.
      runtimePipeline.supportsHDR = true;
      runtimePipeline.hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
      runtimePipeline.supportsCameraOpaqueTexture = false;
      runtimePipeline.supportsCameraDepthTexture = true;
      runtimePipeline.colorGradingMode = ColorGradingMode.LowDynamicRange;
      runtimePipeline.maxAdditionalLightsCount = 4;
      break;
    }
    runtimePipeline.useSRPBatcher = true;
   }
   if (ssao) ssao.SetActive(tier != Tier.Balanced);
   QualitySettings.lodBias = tier == Tier.Ultra ? 2.2f : tier == Tier.High ? 1.5f : 0.9f;
   QualitySettings.vSyncCount = 0;
   Application.targetFrameRate = TargetFrameRate;
   ConfigureCamera(Camera.main);
   if (OpenWorldRealm.Instance) OpenWorldRealm.Instance.ApplyQuality((int)tier);
   if (OpenWorldVfx.Instance) OpenWorldVfx.Instance.ApplyTier((int)tier);
   Changed?.Invoke(tier);
   if (save) {
    PlayerPrefs.SetInt(Preference, (int)tier);
    PlayerPrefs.Save();
   }
  }

  // Bridge diagnostics: what the camera really renders with, and picking a tier for a tour.
  public static void DebugReport() {
   var g = Instance;
   var cam = Camera.main;
   var p = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
   if (!p) p = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
   var data = cam ? cam.GetUniversalAdditionalCameraData() : null;
   Debug.Log($"[Graphics] tier {(g ? g.Label : "none")}, pipeline HDR {(p && p.supportsHDR)}, grading {(p ? p.colorGradingMode.ToString() : "?")}, " +
             $"render scale {(p ? p.renderScale : 0f):0.00}, MSAA {(p ? p.msaaSampleCount : 0)}, camera HDR {(cam && cam.allowHDR)}, post {(data && data.renderPostProcessing)}, " +
             $"saved preference {PlayerPrefs.GetInt(Preference, -1)}");
  }

  public static void DebugTier(string tier) {
   if (Instance && Enum.TryParse(tier, true, out Tier t)) Instance.Apply(t, false);
   DebugReport();
  }

  public void ConfigureCamera(Camera cam) {
   if (!cam) return;
   var data = cam.GetUniversalAdditionalCameraData();
   if (!data) return;
   data.renderPostProcessing = true;
   data.antialiasing = Current == Tier.Balanced ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
   data.renderShadows = true;
   data.requiresDepthTexture = true;
   data.requiresColorTexture = Current != Tier.Balanced;
   cam.allowHDR = true;
   cam.allowMSAA = Current != Tier.Balanced;
  }

  void OnDestroy() {
   if (Instance != this) return;
   Instance = null;
   if (ssao) ssao.SetActive(ssaoWasActive);
   if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousOverride;
   if (runtimePipeline) Destroy(runtimePipeline);
   Application.targetFrameRate = previousFrameRate;
   QualitySettings.vSyncCount = previousVSync;
   QualitySettings.lodBias = previousLodBias;
  }
 }
}
