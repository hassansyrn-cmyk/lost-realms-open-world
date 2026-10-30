using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Read-only inspection helpers driven through ClaudeBridge ("call LostRealmsOW.OpenWorldDiagnostics.X arg").
 // Every result is written to ClaudeBridge/out so it can be reviewed outside the editor.
 public static class OpenWorldDiagnostics {
  static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "out"));
  static void Write(string name, string text) {
   Directory.CreateDirectory(Out);
   File.WriteAllText(Path.Combine(Out, name), text);
   Debug.Log("[Diag] wrote " + name + " (" + text.Length + " chars)");
  }
  static string Safe(string s) => string.Concat((s ?? "x").Select(c => char.IsLetterOrDigit(c) ? c : '_')).Substring(0, Math.Min(60, (s ?? "x").Length));

  // Properties, keywords and textures of one or more materials ("a.mat;b.mat").
  public static void DumpMaterial(string paths) {
   var sb = new StringBuilder();
   foreach (var path in (paths ?? "").Split(';')) {
    var m = AssetDatabase.LoadAssetAtPath<Material>(path.Trim());
    if (!m) { sb.AppendLine("MISSING " + path); continue; }
    sb.AppendLine("== " + path + " shader=" + (m.shader ? m.shader.name : "null") + " queue=" + m.renderQueue);
    sb.AppendLine("   keywords: " + string.Join(" ", m.shaderKeywords));
    if (m.shader) DescribeProperties(m.shader, m, sb);
   }
   Write("mat_" + Safe(paths) + ".txt", sb.ToString());
  }

  // Every material referenced by a prefab, with its shader properties.
  public static void DumpPrefabMaterials(string path) {
   var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
   var sb = new StringBuilder();
   if (!go) sb.AppendLine("MISSING " + path);
   else foreach (var m in go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct()) {
    sb.AppendLine("== " + AssetDatabase.GetAssetPath(m) + " / " + m.name + " shader=" + (m.shader ? m.shader.name : "null"));
    sb.AppendLine("   keywords: " + string.Join(" ", m.shaderKeywords));
    if (m.shader) DescribeProperties(m.shader, m, sb);
   }
   Write("prefabmats_" + Safe(Path.GetFileNameWithoutExtension(path)) + ".txt", sb.ToString());
  }

  public static void DumpShader(string name) {
   var s = Shader.Find(name);
   var sb = new StringBuilder();
   if (!s) sb.AppendLine("MISSING " + name);
   else { sb.AppendLine("== " + s.name + " path=" + AssetDatabase.GetAssetPath(s)); DescribeProperties(s, null, sb); }
   Write("shader_" + Safe(name) + ".txt", sb.ToString());
  }

  static void DescribeProperties(Shader s, Material m, StringBuilder sb) {
   int n = s.GetPropertyCount();
   for (int i = 0; i < n; i++) {
    string pn = s.GetPropertyName(i);
    var type = s.GetPropertyType(i);
    string value = "";
    if (m) {
     switch (type) {
      case ShaderPropertyType.Color: value = m.GetColor(pn).ToString("F3"); break;
      case ShaderPropertyType.Vector: value = m.GetVector(pn).ToString("F3"); break;
      case ShaderPropertyType.Float: case ShaderPropertyType.Range: value = m.GetFloat(pn).ToString("0.###"); break;
      case ShaderPropertyType.Int: value = m.GetInt(pn).ToString(); break;
      case ShaderPropertyType.Texture: var t = m.GetTexture(pn); value = t ? AssetDatabase.GetAssetPath(t) + " " + m.GetTextureScale(pn) : "none"; break;
     }
    }
    sb.AppendLine($"   {pn} ({type}, \"{s.GetPropertyDescription(i)}\") = {value}");
   }
  }

  // File listing of a folder (assets only), e.g. to discover vendor terrain layers.
  public static void ListFolder(string folder) {
   var sb = new StringBuilder();
   foreach (var guid in AssetDatabase.FindAssets("", new[] { folder.Trim() })) sb.AppendLine(AssetDatabase.GUIDToAssetPath(guid));
   Write("list_" + Safe(folder) + ".txt", sb.ToString());
  }

  // Hierarchy, transforms and components of a prefab.
  public static void DumpPrefab(string path) {
   var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
   var sb = new StringBuilder();
   if (!go) sb.AppendLine("MISSING " + path);
   else Walk(go.transform, 0, sb);
   Write("prefab_" + Safe(Path.GetFileNameWithoutExtension(path)) + ".txt", sb.ToString());
  }

  static void Walk(Transform t, int depth, StringBuilder sb) {
   sb.Append(new string(' ', depth * 2)).Append(t.name).Append(" pos=").Append(t.localPosition.ToString("F2")).Append(" rot=").Append(t.localEulerAngles.ToString("F1")).Append(" scale=").Append(t.localScale.ToString("F3"));
   sb.Append(" [").Append(string.Join(", ", t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name))).AppendLine("]");
   if (depth < 6) foreach (Transform c in t) Walk(c, depth + 1, sb);
  }

  // Audio library inventory by folder keyword (e.g. "ambience;music;footstep").
  public static void ListAudio(string keywords) {
   var keys = (keywords ?? "").ToLowerInvariant().Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
   var sb = new StringBuilder();
   foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })) {
    string p = AssetDatabase.GUIDToAssetPath(guid);
    if (keys.Length > 0 && !keys.Any(k => p.ToLowerInvariant().Contains(k))) continue;
    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
    sb.AppendLine(p + (clip ? $" {clip.length:0.0}s {clip.channels}ch" : ""));
   }
   Write("audio_" + Safe(keywords) + ".txt", sb.ToString());
  }
 }
}
