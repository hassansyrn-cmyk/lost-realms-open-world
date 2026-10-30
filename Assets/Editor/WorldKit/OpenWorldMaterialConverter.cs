using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 public enum KitMaterialMode { Auto, Opaque, Cutout, CutoutTwoSided, Fade }

 // Produces URP-correct copies of vendor materials. Vendor assets are never modified: every
 // conversion is written as a new material under GameContent/WorldKit/Materials and cached
 // per source material, mode and tint so a kit build reuses one copy for many prefabs.
 public static class OpenWorldMaterialConverter {
  public const string Output = "Assets/GameContent/WorldKit/Materials";
  public const string BakedTextures = "Assets/GameContent/WorldKit/Textures";
  static List<MaterialUpgrader> upgraders;
  static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
  static readonly Dictionary<Shader, bool> compatible = new Dictionary<Shader, bool>();
  public static readonly List<string> Report = new List<string>();
  public static int Converted, Kept, Upgraded, Custom, Generic, Baked;

  public static void Begin() {
   cache.Clear(); compatible.Clear(); Report.Clear();
   Converted = Kept = Upgraded = Custom = Generic = Baked = 0;
   foliageChecked = false; foliageShader = null;
   Directory.CreateDirectory(Output);
   Directory.CreateDirectory(BakedTextures);
  }

  static List<MaterialUpgrader> Upgraders {
   get {
    if (upgraders == null) {
     try { upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset)); }
     catch (Exception e) { Debug.LogWarning("[Kit] URP upgraders unavailable: " + e.Message); upgraders = new List<MaterialUpgrader>(); }
    }
    return upgraders;
   }
  }

  static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
  static Shader ParticlesUnlit => Shader.Find("Universal Render Pipeline/Particles/Unlit");

  public static bool IsUrpCompatible(Shader s) {
   if (!s) return false;
   if (compatible.TryGetValue(s, out bool known)) return known;
   bool ok = false;
   string n = s.name;
   if (n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("Idyllic Fantasy Nature/") ||
       n.StartsWith("LostRealms/") || n.StartsWith("GAP_SG/") || n.Contains("URP") || n.StartsWith("Hidden/Universal") || n.StartsWith("Skybox/")) ok = true;
   else {
    string path = AssetDatabase.GetAssetPath(s);
    if (path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) ok = true;
    else {
     try {
      var data = ShaderUtil.GetShaderData(s);
      for (int si = 0; si < data.SubshaderCount && !ok; si++) {
       var sub = data.GetSubshader(si);
       for (int pi = 0; pi < sub.PassCount; pi++) {
        string mode = sub.GetPass(pi).FindTagValue(new ShaderTagId("LightMode")).name;
        if (mode == "UniversalForward" || mode == "UniversalForwardOnly" || mode == "UniversalGBuffer") { ok = true; break; }
       }
      }
     } catch { }
    }
   }
   compatible[s] = ok;
   return ok;
  }

  // Shaders whose untagged passes render acceptably in URP and need no copy.
  static bool UnlitPassthrough(string n) => n == "Sprites/Default" || n == "UI/Default" || n.StartsWith("Unlit/");

  static bool IsLegacyParticle(string n) =>
   n.StartsWith("Legacy Shaders/Particles/") || n.StartsWith("Mobile/Particles/") || n.StartsWith("Particles/Additive") || n.StartsWith("Particles/Alpha") ||
   n.StartsWith("Particles/Multiply") || n.StartsWith("Particles/~");

  // Optional surface "coverage" override (Idyllic Surface top coverage: moss by default; snow,
  // sand or lichen for the other biomes).
  public struct Coverage {
   public Color color; public string texture; public float level;
   public bool IsSet => color.a > 0f;
   public string Key => IsSet ? ColorUtility.ToHtmlStringRGB(color) + "|" + texture + "|" + level.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "";
  }

  public static Material Convert(Material source, bool particle, KitMaterialMode mode = KitMaterialMode.Auto, Color? tint = null, Coverage coverage = default) {
   if (!source || !source.shader) return Fallback(particle);
   string sn = source.shader.name;
   bool needsTint = tint.HasValue && tint.Value != Color.white;
   bool needsCoverage = coverage.IsSet && source.HasProperty("_Coverage_Color");
   bool compatibleShader = IsUrpCompatible(source.shader);
   // Idyllic vegetation always moves to the lit foliage shader (its own ramp ignores sun and moon).
   bool toFoliage = !particle && sn == IdyllicVegetation && FoliageShader;
   if (!toFoliage && compatibleShader && mode == KitMaterialMode.Auto && !needsTint && !needsCoverage) { Kept++; return source; }
   if (UnlitPassthrough(sn) && mode == KitMaterialMode.Auto && !needsTint) { Kept++; return source; }
   string id = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long local) ? guid + "_" + local : Sanitize(source.name);
   string key = id + "|" + (particle ? "p" : "m") + "|" + mode + "|" + (needsTint ? ColorUtility.ToHtmlStringRGBA(tint.Value) : "") + "|" + (needsCoverage ? coverage.Key : "") + (toFoliage ? "|foliage" : "");
   if (cache.TryGetValue(key, out var cached) && cached) return cached;
   string file = Output + "/" + Sanitize(source.name) + "_" + Hash(key) + ".mat";
   Material result;
   if (toFoliage) {
    result = ToFoliage(source);
    Custom++;
   } else if (compatibleShader) {
    result = new Material(source) { name = source.name + " (Kit)" };
    Kept++;
   } else if (particle || IsLegacyParticle(sn)) {
    result = ConvertParticle(source, !particle);
    Custom++;
   } else {
    result = ConvertSurface(source);
   }
   if (!result) return Fallback(particle);
   if (mode != KitMaterialMode.Auto) ApplyMode(result, mode);
   if (needsTint) ApplyTint(result, tint.Value);
   if (needsCoverage) ApplyCoverage(result, coverage);
   result.enableInstancing = true;
   var existing = AssetDatabase.LoadAssetAtPath<Material>(file);
   if (existing) { EditorUtility.CopySerialized(result, existing); UnityEngine.Object.DestroyImmediate(result); result = existing; EditorUtility.SetDirty(result); }
   else AssetDatabase.CreateAsset(result, file);
   Converted++;
   cache[key] = result;
   return result;
  }

  const string IdyllicVegetation = "Idyllic Fantasy Nature/Vegetation";
  static Shader foliageShader;
  static bool foliageChecked;

  // The realm's lit foliage shader, or null when it is missing or failed to compile (the vendor
  // vegetation material is then kept as before).
  static Shader FoliageShader {
   get {
    if (!foliageChecked) {
     foliageChecked = true;
     var s = Shader.Find("LostRealms/Foliage");
     foliageShader = s && s.isSupported && !ShaderUtil.ShaderHasError(s) ? s : null;
     if (!foliageShader) Report.Add("- LostRealms/Foliage unavailable; Idyllic vegetation kept on its vendor shader");
    }
    return foliageShader;
   }
  }

  // Property names match the vendor graph, so the copy carries texture, gradient tints, cutoff,
  // wind and push settings across unchanged.
  static Material ToFoliage(Material source) {
   var m = new Material(FoliageShader) { name = source.name + " (Foliage)" };
   m.CopyPropertiesFromMaterial(source);
   m.shaderKeywords = new string[0];
   if (source.HasProperty("_Texture")) {
    m.SetTexture("_Texture", source.GetTexture("_Texture"));
    m.SetTextureScale("_Texture", source.GetTextureScale("_Texture"));
    m.SetTextureOffset("_Texture", source.GetTextureOffset("_Texture"));
   }
   // The vendor picks the gradient source with a keyword; mirror it in the float the shader reads.
   bool byHeight = source.IsKeywordEnabled("_COLOR_OVERLAY_VERTEX_POSITION") && !source.IsKeywordEnabled("_COLOR_OVERLAY_UV");
   m.SetFloat("_COLOR_OVERLAY", byHeight ? 0f : 1f);
   // The vendor ramp added a flat grey lit layer on top of its emission; with real lighting the same
   // tints read about 30% hotter, so the converted foliage starts a step darker.
   m.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f, 1f));
   m.renderQueue = (int)RenderQueue.AlphaTest;
   m.enableInstancing = true;
   return m;
  }

  static Material ConvertSurface(Material source) {
   string sn = source.shader.name;
   // 1. Unity's own URP upgraders (Standard, Specular, Legacy, Mobile, Particles/Standard, SpeedTree, Autodesk).
   var upgrader = Upgraders.FirstOrDefault(u => u.OldShaderPath == sn && (u.NewShaderPath == null || !u.NewShaderPath.Contains("/2D/")));
   if (upgrader != null) {
    var copy = new Material(source) { name = source.name + " (URP)" };
    try {
     MaterialUpgrader.Upgrade(copy, upgrader, MaterialUpgrader.UpgradeFlags.None);
     if (copy.shader && IsUrpCompatible(copy.shader)) {
      Upgraded++;
      FixLitKeywords(copy);
      return copy;
     }
    } catch (Exception e) { Report.Add("- upgrader failed for " + source.name + " (" + sn + "): " + e.Message); }
    UnityEngine.Object.DestroyImmediate(copy);
   }
   // 2. Known vendor shaders.
   if (sn.Contains("PT_Medieval Modular NPC Shader")) { var baked = BakePolytopeNpc(source); if (baked) { Baked++; return baked; } }
   if (sn.StartsWith("Polytope Studio/PT_Vegetation_Foliage") || sn.StartsWith("Polytope Studio/PT_Vegetation_Plants") || sn.StartsWith("Polytope Studio/PT_Vegetation_Flowers")) {
    Custom++; return LitFrom(source, "_BaseTexture", null, null, cutout: true, twoSided: true, cutoff: 0.5f, smoothness: 0.08f);
   }
   if (sn.StartsWith("Polytope Studio/PT_Vegetation_Opaque") || sn.StartsWith("Polytope Studio/PT_Rock")) {
    Custom++; return LitFrom(source, "_BaseTexture", null, null, cutout: false, twoSided: false, cutoff: 0.5f, smoothness: Saved(source, "_Smoothness", 0.15f));
   }
   if (sn == "Nature/Tree Soft Occlusion Leaves" || sn == "Custom/FoliageShader" || sn == "Custom/Cutout_2Sided" || sn.Contains("Two Sided")) {
    Custom++; return LitFrom(source, "_MainTex", "_Color", "_BumpMap", cutout: true, twoSided: true, cutoff: Mathf.Clamp(Saved(source, "_Cutoff", 0.45f), 0.25f, 0.7f), smoothness: 0.06f);
   }
   if (sn == "Nature/Tree Soft Occlusion Bark" || sn == "LowPolyShaders/LowPolyPBRShader") {
    Custom++; return LitFrom(source, "_MainTex", "_Color", "_BumpMap", cutout: false, twoSided: false, cutoff: 0.5f, smoothness: Mathf.Min(0.35f, Saved(source, "_Glossiness", 0.12f)), metallic: Saved(source, "_Metallic", 0f));
   }
   // 3. Anything else: rebuild from the serialized properties (works for missing shaders too).
   Generic++;
   Report.Add("- generic conversion: " + source.name + " (" + sn + ")");
   bool foliage = LooksLikeFoliage(source);
   return LitFrom(source, null, null, null, cutout: foliage, twoSided: foliage, cutoff: 0.45f, smoothness: 0.12f);
  }

  static bool LooksLikeFoliage(Material m) {
   string n = m.name.ToLowerInvariant();
   if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") || n.Contains("grass") || n.Contains("branch") || n.Contains("fern") || n.Contains("bush") || n.Contains("flower") || n.Contains("plant")) return true;
   string rt = m.GetTag("RenderType", false, "");
   return rt == "TransparentCutout" || m.renderQueue == 2450;
  }

  // ---------- serialized property access (independent of the current shader) ----------
  static SerializedProperty FindSaved(SerializedObject so, string array, string name) {
   var arr = so.FindProperty("m_SavedProperties." + array);
   if (arr == null) return null;
   for (int i = 0; i < arr.arraySize; i++) {
    var el = arr.GetArrayElementAtIndex(i);
    var first = el.FindPropertyRelative("first");
    if (first != null && first.stringValue == name) return el.FindPropertyRelative("second");
   }
   return null;
  }

  public static Texture SavedTexture(Material m, string name, out Vector2 scale, out Vector2 offset) {
   scale = Vector2.one; offset = Vector2.zero;
   var so = new SerializedObject(m);
   var p = FindSaved(so, "m_TexEnvs", name);
   if (p == null) return null;
   var tex = p.FindPropertyRelative("m_Texture");
   var sc = p.FindPropertyRelative("m_Scale");
   var of = p.FindPropertyRelative("m_Offset");
   if (sc != null) scale = sc.vector2Value;
   if (of != null) offset = of.vector2Value;
   return tex != null ? tex.objectReferenceValue as Texture : null;
  }

  public static float Saved(Material m, string name, float fallback) {
   var p = FindSaved(new SerializedObject(m), "m_Floats", name);
   return p != null ? p.floatValue : fallback;
  }

  public static Color SavedColor(Material m, string name, Color fallback) {
   var p = FindSaved(new SerializedObject(m), "m_Colors", name);
   return p != null ? p.colorValue : fallback;
  }

  static readonly string[] BaseTextureNames = { "_BaseMap", "_MainTex", "_BaseTexture", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_Diffuse", "_DiffuseTexture", "_Texture", "_MainTexture", "_TextureSample2", "_TextureSample0" };
  static readonly string[] BaseColorNames = { "_BaseColor", "_Color", "_MainColor", "_TintColor", "_Tint" };
  static readonly string[] NormalNames = { "_BumpMap", "_NormalMap", "_Normal", "_NormalTex" };

  static Texture FirstTexture(Material m, string preferred, out Vector2 scale, out Vector2 offset) {
   scale = Vector2.one; offset = Vector2.zero;
   if (preferred != null) { var t = SavedTexture(m, preferred, out scale, out offset); if (t) return t; }
   foreach (var n in BaseTextureNames) { var t = SavedTexture(m, n, out scale, out offset); if (t) return t; }
   return null;
  }

  static Color FirstColor(Material m, string preferred) {
   if (preferred != null) { var p = FindSaved(new SerializedObject(m), "m_Colors", preferred); if (p != null) return p.colorValue; }
   var so = new SerializedObject(m);
   foreach (var n in BaseColorNames) { var p = FindSaved(so, "m_Colors", n); if (p != null) return p.colorValue; }
   return Color.white;
  }

  static Material LitFrom(Material src, string texName, string colorName, string normalName, bool cutout, bool twoSided, float cutoff, float smoothness, float metallic = 0f) {
   var shader = Lit;
   if (!shader) return null;
   var m = new Material(shader) { name = src.name + " (URP)" };
   var tex = FirstTexture(src, texName, out var scale, out var offset);
   m.SetTexture("_BaseMap", tex);
   m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset);
   var color = FirstColor(src, colorName);
   if (color.maxColorComponent < 0.02f) color = Color.white;
   color.a = 1f;
   m.SetColor("_BaseColor", color);
   Texture normal = null;
   if (normalName != null) normal = SavedTexture(src, normalName, out _, out _);
   if (!normal) foreach (var n in NormalNames) { normal = SavedTexture(src, n, out _, out _); if (normal) break; }
   if (normal && IsNormalMap(normal)) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); }
   var emissionMap = SavedTexture(src, "_EmissionMap", out _, out _) ?? SavedTexture(src, "_Illum", out _, out _);
   var emissionColor = SavedColor(src, "_EmissionColor", Color.black);
   if (emissionMap || emissionColor.maxColorComponent > 0.02f) {
    m.SetTexture("_EmissionMap", emissionMap);
    m.SetColor("_EmissionColor", emissionColor.maxColorComponent > 0.02f ? emissionColor : Color.white * 0.6f);
    m.EnableKeyword("_EMISSION");
    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
   }
   m.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
   m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
   m.SetFloat("_Surface", 0f);
   m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
   m.SetFloat("_Cutoff", cutoff);
   m.SetFloat("_Cull", twoSided ? 0f : 2f);
   m.SetFloat("_ReceiveShadows", 1f);
   m.SetFloat("_EnvironmentReflections", cutout ? 0f : 1f);
   m.SetFloat("_SpecularHighlights", cutout ? 0f : 1f);
   if (cutout) m.renderQueue = (int)RenderQueue.AlphaTest;
   FixLitKeywords(m);
   return m;
  }

  static bool IsNormalMap(Texture t) {
   string path = AssetDatabase.GetAssetPath(t);
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   return importer != null && importer.textureType == TextureImporterType.NormalMap;
  }

  public static void FixLitKeywords(Material m) {
   try {
    if (m.shader && m.shader.name == "Universal Render Pipeline/Lit") UnityEditor.BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords, null);
    else if (m.shader && m.shader.name == "Universal Render Pipeline/Simple Lit") UnityEditor.BaseShaderGUI.SetMaterialKeywords(m, null, null);
    else if (m.shader && m.shader.name.StartsWith("Universal Render Pipeline/Particles")) UnityEditor.BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
    else if (m.shader && m.shader.name == "Universal Render Pipeline/Unlit") UnityEditor.BaseShaderGUI.SetMaterialKeywords(m, null, null);
   } catch (Exception e) { Report.Add("- keyword setup failed for " + m.name + ": " + e.Message); }
  }

  // ---------- particles ----------
  static Material ConvertParticle(Material src, bool forMesh) {
   string sn = src.shader ? src.shader.name : "";
   // Unity's ParticleUpgrader handles the modern built-in particle shaders precisely.
   var upgrader = Upgraders.FirstOrDefault(u => u.OldShaderPath == sn && (u.NewShaderPath == null || !u.NewShaderPath.Contains("/2D/")));
   if (upgrader != null) {
    var copy = new Material(src) { name = src.name + " (URP)" };
    try {
     MaterialUpgrader.Upgrade(copy, upgrader, MaterialUpgrader.UpgradeFlags.None);
     if (copy.shader && IsUrpCompatible(copy.shader)) { Upgraded++; FixLitKeywords(copy); return copy; }
    } catch (Exception e) { Report.Add("- particle upgrader failed for " + src.name + ": " + e.Message); }
    UnityEngine.Object.DestroyImmediate(copy);
   }
   // Meshes without vertex colours render with URP Unlit; particle systems use the particle shader.
   var shader = forMesh ? Shader.Find("Universal Render Pipeline/Unlit") : ParticlesUnlit;
   if (!shader) return null;
   var m = new Material(shader) { name = src.name + " (URP FX)" };
   var tex = FirstTexture(src, "_MainTex", out var scale, out var offset);
   m.SetTexture("_BaseMap", tex);
   m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset);
   string lower = sn.ToLowerInvariant();
   bool additive = lower.Contains("additive") || lower.Contains("add") && !lower.Contains("alpha");
   bool premultiply = lower.Contains("premultipl");
   bool multiply = lower.Contains("multiply") && !additive;
   Color tint = SavedColor(src, "_TintColor", new Color(-1, 0, 0, 0));
   Color color;
   if (tint.r >= 0f) color = new Color(Mathf.Min(tint.r * 2f, 8f), Mathf.Min(tint.g * 2f, 8f), Mathf.Min(tint.b * 2f, 8f), Mathf.Clamp01(tint.a * 2f));
   else color = FirstColor(src, null);
   if (sn.StartsWith("Mobile/")) color = Color.white;
   m.SetColor("_BaseColor", color);
   m.SetFloat("_Surface", 1f);
   m.SetFloat("_Blend", additive ? 2f : premultiply ? 1f : multiply ? 3f : 0f);
   m.SetFloat("_Cull", 0f);
   m.SetFloat("_ColorMode", 0f);
   float invFade = Saved(src, "_InvFade", 0f);
   bool soft = lower.Contains("soft") || invFade > 0.01f;
   m.SetFloat("_SoftParticlesEnabled", soft ? 1f : 0f);
   m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
   m.SetFloat("_SoftParticlesFarFadeDistance", soft ? Mathf.Clamp(invFade > 0.01f ? 1f / invFade : 1f, 0.2f, 3f) : 1f);
   m.SetFloat("_CameraFadingEnabled", 0f);
   m.renderQueue = (int)RenderQueue.Transparent;
   FixLitKeywords(m);
   return m;
  }

  static Material fallbackLit, fallbackParticle;
  static Material Fallback(bool particle) {
   if (particle) {
    if (!fallbackParticle) fallbackParticle = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Kit_Fallback_Particle.mat");
    if (!fallbackParticle && ParticlesUnlit) {
     fallbackParticle = new Material(ParticlesUnlit) { name = "Kit Fallback Particle" };
     fallbackParticle.SetFloat("_Surface", 1f); fallbackParticle.SetFloat("_Blend", 2f);
     FixLitKeywords(fallbackParticle);
     AssetDatabase.CreateAsset(fallbackParticle, Output + "/Kit_Fallback_Particle.mat");
    }
    return fallbackParticle;
   }
   if (!fallbackLit) fallbackLit = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Kit_Fallback_Lit.mat");
   if (!fallbackLit && Lit) {
    fallbackLit = new Material(Lit) { name = "Kit Fallback Lit" };
    fallbackLit.SetColor("_BaseColor", new Color(0.55f, 0.52f, 0.48f));
    AssetDatabase.CreateAsset(fallbackLit, Output + "/Kit_Fallback_Lit.mat");
   }
   return fallbackLit;
  }

  // ---------- modes and tints on any URP material ----------
  static void ApplyMode(Material m, KitMaterialMode mode) {
   bool lit = m.shader && (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Universal Render Pipeline/Simple Lit");
   switch (mode) {
    case KitMaterialMode.Opaque:
     if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
     if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
     m.renderQueue = -1;
     break;
    case KitMaterialMode.Cutout:
    case KitMaterialMode.CutoutTwoSided:
     if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
     if (m.HasProperty("_Cutoff") && m.GetFloat("_Cutoff") <= 0.01f) m.SetFloat("_Cutoff", 0.45f);
     if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
     if (mode == KitMaterialMode.CutoutTwoSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
     if (lit) m.renderQueue = (int)RenderQueue.AlphaTest;
     break;
    case KitMaterialMode.Fade:
     if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
     if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
     m.renderQueue = (int)RenderQueue.Transparent;
     break;
   }
   FixLitKeywords(m);
  }

  static void ApplyTint(Material m, Color tint) {
   // Idyllic vegetation: the leaf/blade hue comes from a bottom-to-top colour gradient.
   if (m.HasProperty("_Top_Color") && m.HasProperty("_Bottom_Color")) {
    if (m.HasProperty("_Custom_Color")) { m.SetFloat("_Custom_Color", 1f); m.EnableKeyword("_CUSTOM_COLOR"); }
    if (m.HasProperty("_Enable_Top_Color")) { m.SetFloat("_Enable_Top_Color", 1f); m.EnableKeyword("_ENABLE_TOP_COLOR"); }
    var top = m.GetColor("_Top_Color");
    m.SetColor("_Top_Color", new Color(tint.r, tint.g, tint.b, top.a));
    var bottom = m.GetColor("_Bottom_Color");
    m.SetColor("_Bottom_Color", new Color(tint.r * 0.52f, tint.g * 0.6f, tint.b * 0.55f, bottom.a));
    return;
   }
   foreach (var n in new[] { "_BaseColor", "_Color", "_Tint", "_TintColor", "_MainColor", "_Albedo_Color", "_AlbedoColor" }) {
    if (!m.HasProperty(n)) continue;
    var c = m.GetColor(n);
    m.SetColor(n, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
    return;
   }
   Report.Add("- tint ignored (no colour property) on " + m.name + " (" + m.shader.name + ")");
  }

  static void ApplyCoverage(Material m, Coverage c) {
   m.SetColor("_Coverage_Color", new Color(c.color.r, c.color.g, c.color.b, 1f));
   if (!string.IsNullOrEmpty(c.texture) && m.HasProperty("_Coverage_Base_Map")) {
    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(c.texture);
    if (tex) m.SetTexture("_Coverage_Base_Map", tex);
    else Report.Add("- coverage texture missing: " + c.texture);
   }
   if (!float.IsNaN(c.level) && m.HasProperty("_Level")) m.SetFloat("_Level", c.level);
  }

  // ---------- Polytope modular NPC: bake the mask palette into ordinary textures ----------
  static readonly (string prop, string smooth, string metal, Color mask)[] PolytopeRegions = {
   ("_FEATHERS2COLOR", "", null, new Color(0.4980392f, 1, 1)), ("_FEATHERS1COLOR", "", null, new Color(0.4980392f, 1, 0.4980392f)),
   ("_CLOTH4COLOR", "", null, new Color(0, 0, 1)), ("_CLOTH3COLOR", "", null, new Color(0, 1, 1)), ("_CLOTH2COLOR", "", null, new Color(0, 1, 0)),
   ("_CLOTH1COLOR", "", null, new Color(0, 0.4980392f, 0)), ("_LEATHER4COLOR", "_LEATHER4SMOOTHNESS", null, new Color(1, 0.4980392f, 0.4980392f)),
   ("_LEATHER3COLOR", "_LEATHER3SMOOTHNESS", null, new Color(1, 1, 0.4980392f)), ("_LEATHER2COLOR", "_LEATHER2SMOOTHNESS", null, new Color(1, 0, 1)),
   ("_LEATHER1COLOR", "_LEATHER1SMOOTHNESS", null, new Color(1, 0.4980392f, 1)), ("_METAL4COLOR", "_METAL4SMOOTHNESS", "_METAL4METALLIC", new Color(0.4980392f, 0.4980392f, 1)),
   ("_METAL3COLOR", "_METAL3SMOOTHNESS", "_METAL3METALLIC", new Color(0, 0.4980392f, 0.4980392f)), ("_METAL2COLOR", "_METAL2SMOOTHNESS", "_METAL2METALLIC", new Color(0, 0, 0.4980392f)),
   ("_METAL1COLOR", "_METAL1SMOOTHNESS", "_METAL1METALLIC", new Color(0.4980392f, 0, 0.4980392f)), ("_OTHERCOLOR", "_OTHERSMOOTHNESS", null, new Color(1, 1, 0)),
   ("_LIPSCOLOR", "_LIPSSMOOTHNESS", null, new Color(0.4980392f, 0.4980392f, 0)), ("_SCLERACOLOR", "_SCLERASMOOTHNESS", null, new Color(0.4980392f, 0.4980392f, 0.4980392f)),
   ("_EYESCOLOR", "_EYESSMOOTHNESS", null, new Color(1, 0, 0)), ("_HAIRCOLOR", "_HAIRSMOOTHNESS", null, new Color(1, 0.4980392f, 0)),
   ("_SKINCOLOR", "_SKINSMOOTHNESS", null, new Color(0.4980392f, 0, 0))
  };

  // Optional palette overrides (villager variety). Keys are Polytope colour property names.
  public static Dictionary<string, Color> PolytopeOverride;

  public static Material BakePolytopeNpc(Material src, string variant = null) {
   var mask = SavedTexture(src, "_TextureSample0", out _, out _) as Texture2D;
   var baseTex = SavedTexture(src, "_TextureSample2", out _, out _) as Texture2D;
   if (!mask || !baseTex) { Report.Add("- Polytope bake skipped (textures missing) on " + src.name); return null; }
   var maskPx = ReadPixels(mask, out int mw, out int mh);
   var basePx = ReadPixels(baseTex, mw, mh);
   if (maskPx == null || basePx == null) return null;
   var albedo = new Color32[mw * mh];
   var metalGloss = new Color32[mw * mh];
   var regions = PolytopeRegions.Select(r => {
    Color c = PolytopeOverride != null && PolytopeOverride.TryGetValue(r.prop, out var o) ? o : SavedColor(src, r.prop, Color.white);
    return (lin: c.linear, smooth: string.IsNullOrEmpty(r.smooth) ? 0.06f : Saved(src, r.smooth, 0.3f), metal: r.metal != null ? Saved(src, r.metal, 0f) : 0f, mask: r.mask);
   }).ToArray();
   for (int i = 0; i < maskPx.Length; i++) {
    Color mk = maskPx[i];
    Color b = ((Color)basePx[i]).linear;
    Color outCol = Color.black; float smooth = 0f, metal = 0f;
    // Same evaluation order as the vendor shader: later regions override earlier ones.
    foreach (var r in regions) {
     float d = Mathf.Sqrt((r.mask.r - mk.r) * (r.mask.r - mk.r) + (r.mask.g - mk.g) * (r.mask.g - mk.g) + (r.mask.b - mk.b) * (r.mask.b - mk.b));
     if (d < 0.1f) { outCol = new Color(b.r * r.lin.r, b.g * r.lin.g, b.b * r.lin.b); smooth = r.smooth; metal = r.metal; }
    }
    Color g = new Color(Mathf.Clamp01(outCol.r), Mathf.Clamp01(outCol.g), Mathf.Clamp01(outCol.b)).gamma;
    albedo[i] = g;
    metalGloss[i] = new Color(metal, 0, 0, smooth);
   }
   string stem = Sanitize(src.name) + (variant != null ? "_" + Sanitize(variant) : "");
   var albedoTex = SaveTexture(albedo, mw, mh, BakedTextures + "/" + stem + "_Albedo.png", true);
   var mgTex = SaveTexture(metalGloss, mw, mh, BakedTextures + "/" + stem + "_MetalGloss.png", false);
   var m = new Material(Lit) { name = src.name + " (Baked)" };
   m.SetTexture("_BaseMap", albedoTex);
   m.SetColor("_BaseColor", Color.white);
   if (mgTex) { m.SetTexture("_MetallicGlossMap", mgTex); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); }
   m.SetFloat("_OcclusionStrength", 1f);
   FixLitKeywords(m);
   return m;
  }

  static Color32[] ReadPixels(Texture2D tex, out int w, out int h) {
   w = tex.width; h = tex.height;
   return ReadPixels(tex, w, h);
  }

  // Reads any texture (readable or not) through a blit, at the requested size, in sRGB bytes.
  static Color32[] ReadPixels(Texture2D tex, int w, int h) {
   string path = AssetDatabase.GetAssetPath(tex);
   if (!string.IsNullOrEmpty(path) && (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))) {
    var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
    if (raw.LoadImage(File.ReadAllBytes(path))) {
     if (raw.width == w && raw.height == h) { var px = raw.GetPixels32(); UnityEngine.Object.DestroyImmediate(raw); return px; }
     var scaled = Resample(raw, w, h); UnityEngine.Object.DestroyImmediate(raw); return scaled;
    }
    UnityEngine.Object.DestroyImmediate(raw);
   }
   var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
   var prev = RenderTexture.active;
   Graphics.Blit(tex, rt);
   RenderTexture.active = rt;
   var read = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
   read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
   read.Apply();
   RenderTexture.active = prev;
   RenderTexture.ReleaseTemporary(rt);
   var result = read.GetPixels32();
   UnityEngine.Object.DestroyImmediate(read);
   return result;
  }

  static Color32[] Resample(Texture2D src, int w, int h) {
   var px = new Color32[w * h];
   for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
    px[y * w + x] = src.GetPixel(Mathf.Clamp(x * src.width / w, 0, src.width - 1), Mathf.Clamp(y * src.height / h, 0, src.height - 1));
   }
   return px;
  }

  static Texture2D SaveTexture(Color32[] px, int w, int h, string path, bool srgb) {
   var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
   tex.SetPixels32(px); tex.Apply();
   File.WriteAllBytes(path, tex.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(tex);
   AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   if (importer) {
    importer.sRGBTexture = srgb;
    importer.filterMode = FilterMode.Point;
    importer.mipmapEnabled = true;
    importer.textureCompression = TextureImporterCompression.Uncompressed;
    importer.SaveAndReimport();
   }
   return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }

  public static string Sanitize(string s) {
   var sb = new StringBuilder();
   foreach (char c in s ?? "") sb.Append(char.IsLetterOrDigit(c) ? c : '_');
   return sb.Length > 40 ? sb.ToString(0, 40) : sb.ToString();
  }

  static string Hash(string s) {
   unchecked {
    uint h = 2166136261;
    foreach (char c in s) { h ^= c; h *= 16777619; }
    return h.ToString("x8");
   }
  }
 }
}
