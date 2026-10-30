using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostRealmsOW {
 public static class OpenWorldAndroidBuild {
  // Invoke with -buildTarget Android. Development identity/signing cannot replace the original game.
  [MenuItem("Lost Realms OW/Build/Android Development APK")]
  public static void Build() {
   if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
    Debug.Log("[AndroidBuild] Switching active build target to Android...");
    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
   }
   if (!File.Exists("Assets/Resources/World/ImportedContent.json")) throw new InvalidOperationException("Prepare regional content first.");
   var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath("."))).AvailableFreeSpace;
   if (free < 6L * 1024 * 1024 * 1024) throw new IOException("At least 6 GB local free space is required before this build attempt; 10 GB is recommended for headroom.");
   PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lostrealms.openworld.dev");
   PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
   // Keep a runtime-speed option for profiling and reuse of the first native build's cache.
   bool runtimeSpeed = Array.IndexOf(Environment.GetCommandLineArgs(), "-ow-runtime-speed") >= 0;
   PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android,
    runtimeSpeed ? Il2CppCodeGeneration.OptimizeSpeed : Il2CppCodeGeneration.OptimizeSize);
   PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
   PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
   PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
   PlayerSettings.Android.useCustomKeystore = false;
   PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
   PlayerSettings.allowedAutorotateToPortrait = false;
   PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
   EditorUserBuildSettings.buildAppBundle = false;
   EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
   OpenWorldContentPreparation.Prepare();
   // Only this dedicated batch process discards its temporary preparation scene state.
   if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/VerdantOpenWorld.unity", OpenSceneMode.Single);
   var settings = AddressableAssetSettingsDefaultObject.Settings;
   if (!settings) throw new InvalidOperationException("Addressables settings missing.");
   settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
   AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
   if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException(content.Error);
   Directory.CreateDirectory("Builds/Android");
   var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
    scenes = new[] { "Assets/Scenes/VerdantOpenWorld.unity" },
    locationPathName = "Builds/Android/LostRealmsOpenWorld-Development.apk",
    target = BuildTarget.Android,
    options = BuildOptions.Development | BuildOptions.CompressWithLz4
   });
   File.WriteAllText("Docs/android-build-result.txt", $"{report.summary.result}: ARM64 IL2CPP development APK; {report.summary.totalSize} bytes; {report.summary.totalErrors} errors. Imported Addressables built for Android. Physical phone testing and release signing are separate requirements.");
   if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed: " + report.summary.result);
  }
  public static void Run() {
   try {
    Directory.CreateDirectory("Docs");
    File.WriteAllText("Docs/android-build-result.txt", "RUNNING " + DateTime.UtcNow.ToString("o") + "\nAndroid development APK: content preparation, Addressables, then ARM64 IL2CPP player build. No successful APK result yet.");
    Build(); EditorApplication.Exit(0);
   }
   catch (Exception error) {
    Directory.CreateDirectory("Docs"); File.WriteAllText("Docs/android-build-result.txt", "FAIL\n" + error);
    Debug.LogException(error); EditorApplication.Exit(1);
   }
  }
 }
}
