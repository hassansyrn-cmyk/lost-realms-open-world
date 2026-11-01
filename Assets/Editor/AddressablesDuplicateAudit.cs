using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;

namespace LostRealmsOW {
 public sealed class AddressablesDuplicateAudit : CheckBundleDupeDependencies {
  [Serializable] class Layout { public Duplicate[] DuplicatedAssets; }
  [Serializable] class Duplicate { public string AssetGuid; }
  public static void CleanupEmptyGroup() {
   var settings = AddressableAssetSettingsDefaultObject.Settings;
   var group = settings.FindGroup("Shared_Deduplicated");
   if (group && group.entries.Count == 0) { settings.RemoveGroup(group); AssetDatabase.SaveAssets(); }
  }
  public static void ApplyAndBuild() {
   if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
   var report = "Library/com.unity.addressables/BuildReports/buildlayout_2026.10.30.16.21.03.json";
   var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(report));
   var settings = AddressableAssetSettingsDefaultObject.Settings;
   var lines = new List<string>();
   var group = settings.FindGroup("Shared_Deduplicated");
   foreach (var duplicate in layout.DuplicatedAssets) {
    string path = AssetDatabase.GUIDToAssetPath(duplicate.AssetGuid);
    Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
    if (!path.StartsWith("Assets/") || path.Contains("/Resources/") || settings.FindAssetEntry(duplicate.AssetGuid) != null || type == null ||
     !(typeof(Texture).IsAssignableFrom(type) || type == typeof(Material) || type == typeof(Shader))) { lines.Add("SKIP " + path); continue; }
    if (!group) group = settings.CreateGroup("Shared_Deduplicated", false, false, false, settings.DefaultGroup.Schemas);
    group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
    var entry = settings.CreateOrMoveEntry(duplicate.AssetGuid, group);
    entry.address = "ow.shared." + duplicate.AssetGuid;
    lines.Add("SHARED " + path);
   }
   EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
   File.WriteAllLines("Docs/Performance/ADDRESSABLES_DEDUP_CHANGES.txt", lines);
   AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
   if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
   Debug.Log("[DuplicateAudit] Shared dependency content rebuild succeeded. " + lines.Count + " candidates reviewed.");
  }
  public static void Run() {
   if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play mode before analysis.");
   var rule = new AddressablesDuplicateAudit();
   var results = rule.RefreshAnalysis(AddressableAssetSettingsDefaultObject.Settings);
   Directory.CreateDirectory("Docs/Performance");
   File.WriteAllLines("Docs/Performance/ADDRESSABLES_DUPLICATE_AUDIT.txt", results.Select(r => r.resultName));
   Debug.Log("[DuplicateAudit] Analysis only; no assets/groups changed. Results: " + results.Count);
  }
 }
}
