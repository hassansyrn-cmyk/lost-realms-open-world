using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 // Global post-processing stack built at runtime: neutral tonemapping, HDR bloom, colour grading with
 // a painterly split tone, and a soft vignette. OpenWorldAtmosphere feeds it a per-biome / time-of-day
 // grade every frame so the frozen north reads cold, the dunes warm and the marches sickly.
 public sealed class OpenWorldPostFX : MonoBehaviour {
  public static OpenWorldPostFX Instance { get; private set; }

  public struct Grade {
   public float exposure, contrast, saturation, temperature, tint, vignette, bloom, splitBalance;
   public Color filter, splitShadows, splitHighlights;
   public static Grade Neutral => new Grade {
    exposure = 0.05f, contrast = 6f, saturation = 2f, temperature = 0f, tint = 0f, vignette = 0.15f, bloom = 0.32f, splitBalance = -18f,
    filter = Color.white, splitShadows = new Color(0.52f, 0.57f, 0.64f), splitHighlights = new Color(1f, 0.95f, 0.88f)
   };
  }

  Volume volume;
  VolumeProfile profile;
  Tonemapping tonemapping;
  Bloom bloom;
  ColorAdjustments color;
  WhiteBalance white;
  Vignette vignette;
  SplitToning split;
  Grade current = Grade.Neutral;

  public static OpenWorldPostFX Attach(Transform parent) {
   if (Instance) return Instance;
   var go = new GameObject("Post Processing");
   if (parent) go.transform.SetParent(parent, false);
   var fx = go.AddComponent<OpenWorldPostFX>();
   fx.Build();
   return fx;
  }

  void Build() {
   Instance = this;
   profile = ScriptableObject.CreateInstance<VolumeProfile>();
   profile.name = "Lost Realms Post Profile";
   tonemapping = profile.Add<Tonemapping>(true);
   tonemapping.mode.Override(TonemappingMode.Neutral);
   bloom = profile.Add<Bloom>(true);
   bloom.threshold.Override(1.15f);
   bloom.intensity.Override(0.32f);
   bloom.scatter.Override(0.65f);
   bloom.clamp.Override(24f);
   color = profile.Add<ColorAdjustments>(true);
   white = profile.Add<WhiteBalance>(true);
   vignette = profile.Add<Vignette>(true);
   vignette.smoothness.Override(0.42f);
   vignette.rounded.Override(false);
   vignette.color.Override(new Color(0.05f, 0.04f, 0.06f));
   split = profile.Add<SplitToning>(true);
   volume = gameObject.AddComponent<Volume>();
   volume.isGlobal = true;
   volume.priority = 10f;
   volume.weight = 1f;
   volume.sharedProfile = profile;
   ApplyTier(OpenWorldGraphics.Instance ? OpenWorldGraphics.Instance.Current : OpenWorldGraphics.Tier.High);
   OpenWorldGraphics.Changed += ApplyTier;
   Push(current);
   if (OpenWorldGraphics.Instance) OpenWorldGraphics.Instance.ConfigureCamera(Camera.main);
  }

  void ApplyTier(OpenWorldGraphics.Tier tier) {
   if (!bloom) return;
   bloom.highQualityFiltering.Override(tier == OpenWorldGraphics.Tier.Ultra);
   bloom.downscale.Override(tier == OpenWorldGraphics.Tier.Balanced ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
   bloom.maxIterations.Override(tier == OpenWorldGraphics.Tier.Balanced ? 4 : 6);
   split.active = tier != OpenWorldGraphics.Tier.Balanced || !Application.isMobilePlatform;
   if (OpenWorldGraphics.Instance) OpenWorldGraphics.Instance.ConfigureCamera(Camera.main);
  }

  Camera configured;
  void Update() {
   var cam = Camera.main;
   if (cam && cam != configured && OpenWorldGraphics.Instance) { configured = cam; OpenWorldGraphics.Instance.ConfigureCamera(cam); }
  }

  public void SetGrade(in Grade g) {
   current = g;
   Push(g);
  }

  void Push(in Grade g) {
   if (!color) return;
   color.postExposure.Override(g.exposure);
   color.contrast.Override(Mathf.Clamp(g.contrast, -100f, 100f));
   color.saturation.Override(Mathf.Clamp(g.saturation, -100f, 100f));
   color.colorFilter.Override(g.filter);
   white.temperature.Override(Mathf.Clamp(g.temperature, -100f, 100f));
   white.tint.Override(Mathf.Clamp(g.tint, -100f, 100f));
   vignette.intensity.Override(Mathf.Clamp01(g.vignette));
   bloom.intensity.Override(Mathf.Max(0f, g.bloom));
   split.shadows.Override(g.splitShadows);
   split.highlights.Override(g.splitHighlights);
   split.balance.Override(Mathf.Clamp(g.splitBalance, -100f, 100f));
  }

  void OnDestroy() {
   OpenWorldGraphics.Changed -= ApplyTier;
   if (Instance == this) Instance = null;
   if (profile) Destroy(profile);
  }
 }
}
