using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace LostRealmsOW {
 public static class AndroidToolchainSetup {
  static string Root => Path.GetFullPath("Tools/downloaded_models/toolchain/UnityAndroid6000.6.0f1");

  [MenuItem("Lost Realms OW/Build/Configure Local Android Tools")]
  public static void Configure() {
   if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before configuring build tools.");
   string jdk = Path.Combine(Root, "OpenJDK"), sdk = Path.Combine(Root, "SDK"), ndk = Path.Combine(Root, "NDK");
   foreach (string file in new[] { Path.Combine(jdk, "bin/java.exe"), Path.Combine(sdk, "platform-tools/adb.exe"), Path.Combine(sdk, "platforms/android-36/android.jar"), Path.Combine(sdk, "build-tools/36.0.0/aapt2.exe"), Path.Combine(ndk, "source.properties") })
    if (!File.Exists(file)) throw new FileNotFoundException("Android toolchain is incomplete", file);
   string backup = Path.Combine(Root, "previous-editor-paths.txt");
   if (!File.Exists(backup)) File.WriteAllText(backup, "JDK=" + AndroidExternalToolsSettings.jdkRootPath + "\nSDK=" + AndroidExternalToolsSettings.sdkRootPath + "\nNDK=" + AndroidExternalToolsSettings.ndkRootPath);
   AndroidExternalToolsSettings.jdkRootPath = jdk;
   AndroidExternalToolsSettings.sdkRootPath = sdk;
   AndroidExternalToolsSettings.ndkRootPath = ndk;
   Debug.Log("[AndroidTools] Configured JDK=" + AndroidExternalToolsSettings.jdkRootPath + "; SDK=" + AndroidExternalToolsSettings.sdkRootPath + "; NDK=" + AndroidExternalToolsSettings.ndkRootPath);
  }

  public static void ConfigureSupportedInput() {
   if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before changing input settings.");
   // Gameplay reads UnityEngine.Input and the touch HUD uses StandaloneInputModule.
   var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
   if (assets.Length == 0) throw new InvalidOperationException("Player Settings asset unavailable.");
   var settings = new SerializedObject(assets[0]);
   var input = settings.FindProperty("activeInputHandler");
   if (input == null) throw new InvalidOperationException("Active Input Handling property unavailable.");
   input.intValue = 0;
   settings.ApplyModifiedPropertiesWithoutUndo();
   AssetDatabase.SaveAssets();
   Debug.Log("[AndroidTools] Active Input Handling set to Input Manager (legacy), the backend used by this game.");
  }

  // The batch Run entry exits Unity. This wrapper keeps the user's interactive Editor open.
  public static void BuildInEditor() {
   if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use this entry only in an idle interactive Editor.");
   Directory.CreateDirectory("Docs");
   File.WriteAllText("Docs/android-build-result.txt", "RUNNING " + DateTime.UtcNow.ToString("o") + "\nLocal Android development APK: preparation, Addressables, ARM64 IL2CPP.");
   try {
    Configure();
    OpenWorldAndroidBuild.Build();
    Debug.Log("[AndroidTools] Local APK build completed; see Docs/android-build-result.txt.");
   } catch (Exception error) {
    File.WriteAllText("Docs/android-build-result.txt", "FAIL " + DateTime.UtcNow.ToString("o") + "\n" + error);
    Debug.LogException(error);
   }
  }
 }
}
