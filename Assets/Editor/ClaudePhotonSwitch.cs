using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LostRealmsOW {
 // Switches the project's networking SDK from Photon PUN 2 (imported but never used by the game) to Photon
 // Fusion 2 (free 100 CCU tier for one app), as the user asked on 29 Oct 2026. The game has no multiplayer
 // code yet; this only swaps the SDK so multiplayer can be built on Fusion later.
 //  ScanPun      - lists anything outside Assets/Photon that depends on PUN: scripts (namespaces, types,
 //                 define-guarded code), assembly definitions, and scenes/prefabs/assets holding PUN
 //                 components. Writes ClaudeBridge/out/pun_usage.txt.
 //  RemovePun    - only when the scan is clean: moves Assets/Photon to the system trash (restorable from the
 //                 Recycle Bin) and removes PUN's scripting define symbols from every build target.
 //  ImportFusion - imports the newest photon-fusion-*.unitypackage found in ClaudeBridge/in or the user's
 //                 Downloads folder (no dialog). Fusion then opens its Hub, where the App ID is pasted.
 //  CheckFusion  - read-only report of the Fusion setup (ClaudeBridge/out/fusion_check.txt); says whether
 //                 an App ID has been entered without ever writing the ID itself.
 public static class ClaudePhotonSwitch {
  const string Root = "Assets/Photon";
  const string Report = "ClaudeBridge/out/pun_usage.txt";
  static readonly string[] PunDefines = { "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER", "PHOTON_REALTIME_4" };
  static readonly Regex CodeUse = new Regex(@"\b(Photon\.Pun|Photon\.Realtime|Photon\.Chat|ExitGames\.Client|PhotonNetwork|MonoBehaviourPun\w*|PhotonView|PunRPC|IPunObservable|PHOTON_UNITY_NETWORKING|PUN_2_\w+|PHOTON_REALTIME_4)\b");
  static readonly Regex AsmUse = new Regex(@"PhotonUnityNetworking|PhotonRealtime|PhotonChat|Photon3Unity3D|PhotonLibs");
  static readonly Regex GuidRef = new Regex(@"guid: ([0-9a-f]{32})");

  public static bool Clean { get; private set; }
  public static bool FusionPresent => Directory.Exists("Assets/Photon/Fusion");

  [MenuItem("Lost Realms OW/Networking/Scan PUN usage")]
  public static void ScanPun() {
   var lines = new List<string> { "PUN usage outside " + Root + " — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), "" };
   if (!Directory.Exists(Root)) { lines.Add("Assets/Photon is not in the project."); Clean = true; Write(lines); return; }
   // GUIDs of every script, plugin and asmdef inside the PUN folders (what a scene or prefab would point at).
   var punGuids = new HashSet<string>();
   foreach (var meta in Directory.GetFiles(Root, "*.meta", SearchOption.AllDirectories)) {
    var target = meta.Substring(0, meta.Length - 5);
    if (!(target.EndsWith(".cs") || target.EndsWith(".dll") || target.EndsWith(".asmdef"))) continue;
    var m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
    if (m.Success) punGuids.Add(m.Groups[1].Value);
   }
   lines.Add($"PUN scripts, plugins and assembly definitions: {punGuids.Count}");
   int code = 0, asm = 0, assets = 0, scanned = 0, skipped = 0;
   foreach (var file in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)) {
    var path = file.Replace('\\', '/');
    if (path.StartsWith(Root + "/") || path.EndsWith(".meta") || path.EndsWith("/ClaudePhotonSwitch.cs")) continue;
    string ext = Path.GetExtension(path).ToLowerInvariant();
    if (ext == ".cs") {
     scanned++;
     var text = File.ReadAllText(path);
     var hit = CodeUse.Match(text);
     if (hit.Success) { code++; lines.Add($"  code   {path}  ({hit.Value})"); }
    } else if (ext == ".asmdef" || ext == ".asmref") {
     scanned++;
     var text = File.ReadAllText(path);
     bool uses = AsmUse.IsMatch(text) || GuidRef.Matches(text.Replace("GUID:", "guid: ")).Cast<Match>().Any(m => punGuids.Contains(m.Groups[1].Value));
     if (uses) { asm++; lines.Add($"  asmdef {path}"); }
    } else if (ext == ".unity" || ext == ".prefab" || ext == ".asset" || ext == ".controller" || ext == ".mat") {
     var info = new FileInfo(path);
     if (info.Length > 12 * 1024 * 1024) { skipped++; continue; } // terrain data and baked lighting: no components
     scanned++;
     string text;
     try { text = File.ReadAllText(path); } catch { skipped++; continue; }
     foreach (Match m in GuidRef.Matches(text)) {
      if (!punGuids.Contains(m.Groups[1].Value)) continue;
      assets++; lines.Add($"  asset  {path}  (references a PUN script)");
      break;
     }
    }
   }
   var group = EditorUserBuildSettings.selectedBuildTargetGroup;
   lines.Add("");
   lines.Add($"Scanned {scanned} files ({skipped} large data files skipped). Code {code}, assembly definitions {asm}, scenes/prefabs/assets {assets}.");
   lines.Add("Define symbols (" + group + "): " + PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group)));
   Clean = code == 0 && asm == 0 && assets == 0;
   lines.Add(Clean ? "Nothing outside Assets/Photon uses PUN: safe to remove." : "Something outside Assets/Photon uses PUN: not removed.");
   Write(lines);
   Debug.Log("[PhotonSwitch] scan: " + (Clean ? "clean" : "PUN in use") + $" (code {code}, asmdef {asm}, assets {assets}); see {Report}");
  }

  [MenuItem("Lost Realms OW/Networking/Remove PUN 2 (to the Recycle Bin)")]
  public static void RemovePun() {
   ScanPun();
   if (!Clean) { Debug.LogWarning("[PhotonSwitch] PUN not removed: something still uses it (see " + Report + ")"); return; }
   int changed = 0;
   foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup))) {
    if (group == BuildTargetGroup.Unknown) continue;
    NamedBuildTarget target;
    try { target = NamedBuildTarget.FromBuildTargetGroup(group); } catch { continue; }
    string symbols;
    try { symbols = PlayerSettings.GetScriptingDefineSymbols(target); } catch { continue; }
    if (string.IsNullOrEmpty(symbols)) continue;
    var kept = symbols.Split(';').Where(s => s.Length > 0 && Array.IndexOf(PunDefines, s.Trim()) < 0).ToArray();
    string next = string.Join(";", kept);
    if (next == symbols) continue;
    try { PlayerSettings.SetScriptingDefineSymbols(target, next); changed++; } catch { }
   }
   bool moved = !AssetDatabase.IsValidFolder(Root) || AssetDatabase.MoveAssetToTrash(Root);
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   File.AppendAllText(Report, Environment.NewLine + (moved ? "Moved Assets/Photon to the system trash (restore it from the Recycle Bin if needed)." : "Could not move Assets/Photon to the trash.")
    + Environment.NewLine + $"Removed PUN define symbols from {changed} build targets." + Environment.NewLine);
   Debug.Log("[PhotonSwitch] PUN 2 " + (moved ? "moved to the trash" : "NOT moved") + $"; define symbols cleaned on {changed} build targets");
  }

  [MenuItem("Lost Realms OW/Networking/Import Photon Fusion package")]
  public static void ImportFusion() {
   var candidates = new List<string>();
   void Look(string folder) { if (Directory.Exists(folder)) candidates.AddRange(Directory.GetFiles(folder, "photon-fusion-*.unitypackage")); }
   Look("ClaudeBridge/in");
   Look(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
   var package = candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
   if (package == null) { Debug.LogWarning("[PhotonSwitch] no photon-fusion-*.unitypackage in ClaudeBridge/in or Downloads"); return; }
   if (Directory.Exists("Assets/Photon/PhotonUnityNetworking")) { Debug.LogWarning("[PhotonSwitch] PUN 2 is still in the project; run RemovePun first"); return; }
   Debug.Log("[PhotonSwitch] importing " + package);
   AssetDatabase.ImportPackage(package, false);
  }

  const string FusionConfig = "Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion";
  const string FusionSettings = "Assets/Photon/Fusion/Resources/PhotonAppSettings.asset";
  const string FusionReport = "ClaudeBridge/out/fusion_check.txt";
  static readonly Regex AppIdShape = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

  // Read-only report of the Fusion setup as the editor itself sees it (writes ClaudeBridge/out/fusion_check.txt).
  // The App ID is reported only as entered / not entered / malformed; its value is never written or logged.
  // Fusion is reached by reflection so this tool still compiles if Fusion is ever removed.
  [MenuItem("Lost Realms OW/Networking/Check Fusion setup")]
  public static void CheckFusion() {
   var lines = new List<string> { "Fusion setup — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), "" };
   lines.Add("Fusion folder: " + (FusionPresent ? "present" : "MISSING") + "; PUN 2 folder: " + (Directory.Exists(Root + "/PhotonUnityNetworking") ? "STILL PRESENT" : "gone"));
   var configType = AssetDatabase.GetMainAssetTypeAtPath(FusionConfig);
   lines.Add("NetworkProjectConfig.fusion imports as: " + (configType != null ? configType.FullName : "nothing (not imported)"));
   var settingsType = AssetDatabase.GetMainAssetTypeAtPath(FusionSettings);
   lines.Add("PhotonAppSettings.asset imports as: " + (settingsType != null ? settingsType.FullName : "nothing (not imported)"));

   // Fusion's own global-asset lookup (the one that fails inside Unity 6.6 import workers). Read-only call.
   var importer = AssetDatabase.GetImporterType(FusionConfig);
   var utils = importer?.Assembly.GetType("Fusion.Editor.FusionGlobalScriptableObjectUtils");
   var lookup = utils?.GetMethod("TryGetGlobalAssetPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(Type), typeof(string).MakeByRefType() }, null);
   foreach (var type in new[] { configType, settingsType }) {
    if (type == null || lookup == null) continue;
    var args = new object[] { type, null };
    try {
     bool found = (bool)lookup.Invoke(null, args);
     lines.Add($"Fusion finds its {type.Name}: " + (found ? args[1] : "NO"));
    } catch (Exception e) { lines.Add($"Fusion lookup of {type.Name} threw: " + (e.InnerException ?? e).Message); }
   }
   if (lookup == null) lines.Add("Fusion lookup helper not found (importer " + (importer != null ? importer.FullName : "none") + ")");

   var settings = AssetDatabase.LoadMainAssetAtPath(FusionSettings);
   if (settings != null) {
    var so = new SerializedObject(settings);
    string Value(string field) {
     var it = so.GetIterator();
     while (it.Next(true)) if (it.propertyType == SerializedPropertyType.String && it.propertyPath.EndsWith(field)) return it.stringValue ?? "";
     return null;
    }
    string fusion = Value("AppIdFusion");
    if (fusion == null) lines.Add("Fusion App ID field: not found in PhotonAppSettings");
    else if (fusion.Trim().Length == 0) lines.Add("Fusion App ID: not entered yet");
    else lines.Add("Fusion App ID: entered" + (AppIdShape.IsMatch(fusion) ? " (looks like a valid App ID)" : (AppIdShape.IsMatch(fusion.Trim()) ? " (has extra spaces around it)" : " (does NOT look like an App ID: check for a partial paste)")));
    bool otherIds = !string.IsNullOrWhiteSpace(Value("AppIdRealtime")) || !string.IsNullOrWhiteSpace(Value("AppIdQuantum"));
    lines.Add("Realtime/Quantum App IDs: " + (otherIds ? "SET (Fusion needs only its own; clear the others)" : "empty"));
   }

   var group = EditorUserBuildSettings.selectedBuildTargetGroup;
   var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group)).Split(';');
   var leftover = defines.Where(d => Array.IndexOf(PunDefines, d.Trim()) >= 0).ToArray();
   lines.Add($"Defines ({group}): Fusion {(defines.Contains("FUSION2") ? "yes" : "NO")}, PUN leftovers {(leftover.Length == 0 ? "none" : string.Join(",", leftover))}");
   lines.Add("Unity " + Application.unityVersion + "; import worker process: " + AssetDatabase.IsAssetImportWorkerProcess());
   Directory.CreateDirectory(Path.GetDirectoryName(FusionReport));
   File.WriteAllLines(FusionReport, lines);
   Debug.Log("[PhotonSwitch] Fusion check:\n" + string.Join("\n", lines.Skip(2)));
  }

  static void Write(List<string> lines) {
   Directory.CreateDirectory(Path.GetDirectoryName(Report));
   File.WriteAllLines(Report, lines);
  }
 }
}
