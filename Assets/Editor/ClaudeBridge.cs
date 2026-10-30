using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace LostRealmsOW {
 // Development automation bridge: executes queued editor commands from <project>/ClaudeBridge/inbox.
 // Commands are plain text, one per line, processed in file-name order. Results, compiler output,
 // console messages and captures are written next to the inbox so an external reviewer can follow
 // progress without keyboard access to the editor. Nothing here ships in a player build.
 [InitializeOnLoad]
 public static class ClaudeBridge {
  static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge"));
  static string Inbox => Path.Combine(Root, "inbox");
  static string Done => Path.Combine(Root, "done");
  static string Shots => Path.Combine(Root, "shots");
  static string ConsolePath => Path.Combine(Root, "console.log");
  static string StatusPath => Path.Combine(Root, "status.txt");
  static string CompilePath => Path.Combine(Root, "compile.log");
  const string ActiveKey = "ClaudeBridge.ActiveFile";
  const string LineKey = "ClaudeBridge.ActiveLine";
  const string WaitUntilKey = "ClaudeBridge.WaitUntil";
  const string PlayStartKey = "ClaudeBridge.PlayStart";
  const string BridgePlayKey = "ClaudeBridge.BridgePlay";
  static double nextPoll, nextHeartbeat;
  static string lastState = "ready";
  static bool pausedLogged, statusWritten;
  static double nextFreeSpace; static string cachedFreeSpace;
  static string commandFile; static DateTime commandStamp; static string[] commandLines;
  static readonly Dictionary<string, MemberInfo> boolMembers = new Dictionary<string, MemberInfo>();
  // Last failure to append to console.log (null while appends succeed); shown in status.txt so a silent
  // capture failure (locked file, full disk) is visible from outside.
  static volatile string consoleError;

  // A Play session the developer started themselves: the queue pauses (no refresh, bakes or
  // runtime calls) until it ends, so a recompile never hot-reloads a game someone is playing.
  static bool UserPlaying => EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(BridgePlayKey, false);
  static readonly object fileLock = new object();
  static readonly List<string> compileMessages = new List<string>();

  static ClaudeBridge() {
   try {
    Directory.CreateDirectory(Inbox); Directory.CreateDirectory(Done); Directory.CreateDirectory(Shots);
   } catch { return; }
   Application.logMessageReceivedThreaded -= OnLog;
   Application.logMessageReceivedThreaded += OnLog;
   CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
   CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
   CompilationPipeline.compilationStarted -= OnCompilationStarted;
   CompilationPipeline.compilationStarted += OnCompilationStarted;
   EditorApplication.playModeStateChanged -= OnPlayMode;
   EditorApplication.playModeStateChanged += OnPlayMode;
   EditorApplication.update -= Poll;
   EditorApplication.update += Poll;
   WriteStatus("ready");
  }

  static void OnLog(string message, string stackTrace, LogType type) {
   if (type == LogType.Log && message.StartsWith("[Bridge]") == false && message.Length > 4000) message = message.Substring(0, 4000);
   var sb = new StringBuilder();
   sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(type.ToString().ToUpperInvariant()).Append(' ').Append(message);
   if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
    if (!string.IsNullOrEmpty(stackTrace)) sb.Append('\n').Append(stackTrace.Length > 3000 ? stackTrace.Substring(0, 3000) : stackTrace);
   }
   sb.Append('\n');
   lock (fileLock) {
    try { File.AppendAllText(ConsolePath, sb.ToString()); consoleError = null; }
    catch (Exception e) { consoleError = DateTime.Now.ToString("HH:mm:ss") + " " + e.GetType().Name + ": " + e.Message; }
   }
  }

  static string FreeSpace() {
   if (cachedFreeSpace != null && EditorApplication.timeSinceStartup < nextFreeSpace) return cachedFreeSpace;
   nextFreeSpace = EditorApplication.timeSinceStartup + 30;
   try { cachedFreeSpace = (new DriveInfo(Path.GetPathRoot(Root)).AvailableFreeSpace / 1073741824.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " GB"; }
   catch (Exception e) { cachedFreeSpace = "unknown (" + e.GetType().Name + ")"; }
   return cachedFreeSpace;
  }

  // Diagnostics for the console capture, appended to ClaudeBridge/out/bridge_probe.txt: play state, Unity
  // logger state, who listens to the Application log callbacks, free disk space, and whether a Debug.Log
  // reaches console.log. Run with "exec LostRealmsOW.ClaudeBridge.Probe".
  public static void Probe() {
   var lines = new List<string>();
   string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
   long before = File.Exists(ConsolePath) ? new FileInfo(ConsolePath).Length : -1;
   Debug.Log("[Bridge] probe " + stamp + (EditorApplication.isPlaying ? " in Play mode" : " in Edit mode"));
   long after = File.Exists(ConsolePath) ? new FileInfo(ConsolePath).Length : -1;
   lines.Add($"{stamp} playing={EditorApplication.isPlaying} console grew {after - before} bytes on Debug.Log (size {after}); last append error: {consoleError ?? "none"}; free {FreeSpace()}");
   lines.Add($"  logger enabled={Debug.unityLogger.logEnabled} filter={Debug.unityLogger.filterLogType} handler={Debug.unityLogger.logHandler?.GetType().FullName}");
   foreach (var field in new[] { "s_LogCallbackHandler", "s_LogCallbackHandlerThreaded" }) {
    var f = typeof(Application).GetField(field, BindingFlags.NonPublic | BindingFlags.Static);
    var d = f?.GetValue(null) as Delegate;
    lines.Add("  " + field + ": " + (f == null ? "field not found" : d == null ? "no listeners" : string.Join(", ", d.GetInvocationList().Select(x => x.Method.DeclaringType?.FullName + "." + x.Method.Name))));
   }
   object Setting(string name) { try { return typeof(EditorSettings).GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null); } catch { return null; } }
   lines.Add($"  enter play mode options: enabled={Setting("enterPlayModeOptionsEnabled")} options={Setting("enterPlayModeOptions")}");
   try { Directory.CreateDirectory(Path.Combine(Root, "out")); File.AppendAllLines(Path.Combine(Root, "out", "bridge_probe.txt"), lines); }
   catch (Exception e) { consoleError = "probe write failed: " + e.GetType().Name + ": " + e.Message; }
  }

  static void OnCompilationStarted(object context) {
   lock (fileLock) { compileMessages.Clear(); try { File.WriteAllText(CompilePath, "COMPILING " + DateTime.Now.ToString("HH:mm:ss") + "\n"); } catch { } }
  }

  static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages) {
   var lines = messages.Where(m => m.type == CompilerMessageType.Error || (m.type == CompilerMessageType.Warning && m.file != null && (m.file.Contains("Assets/Scripts") || m.file.Contains("Assets/Editor"))))
    .Select(m => (m.type == CompilerMessageType.Error ? "ERROR " : "WARN ") + m.message).ToList();
   lock (fileLock) {
    compileMessages.AddRange(lines);
    try { File.AppendAllText(CompilePath, Path.GetFileName(assembly) + ": " + lines.Count(l => l.StartsWith("ERROR")) + " errors\n" + string.Join("\n", lines) + (lines.Count > 0 ? "\n" : "")); } catch { }
   }
  }

  static void OnPlayMode(PlayModeStateChange change) {
   if (change == PlayModeStateChange.EnteredPlayMode) SessionState.SetFloat(PlayStartKey, (float)EditorApplication.timeSinceStartup);
   if (change == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(BridgePlayKey);
   // Seen on 29 Oct 2026 (Unity 6000.6.0f1): after Play mode ends, OnLog stops being called until the next
   // script reload although it is still registered (probe job 0060), so messages after a Play session never
   // reached console.log. Subscribing again re-arms it.
   if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.EnteredPlayMode) {
    Application.logMessageReceivedThreaded -= OnLog;
    Application.logMessageReceivedThreaded += OnLog;
   }
   WriteStatus("playmode " + change);
  }

  static void WriteStatus(string state, bool force = false) {
   if (statusWritten && !force && state == lastState) return;
   lastState = state;
   try {
    File.WriteAllText(StatusPath + ".next",
     "time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\nstate=" + state + "\ncompiling=" + EditorApplication.isCompiling +
     "\nupdating=" + EditorApplication.isUpdating + "\nplaying=" + EditorApplication.isPlaying +
     "\nuserplay=" + UserPlaying +
     "\nactive=" + SessionState.GetString(ActiveKey, "") + ":" + SessionState.GetInt(LineKey, 0) +
     "\nconsole=" + (consoleError ?? "ok") + "\nfree=" + FreeSpace() + "\n");
     // Atomic replacement tolerates readers mapping the previous status file.
     if (File.Exists(StatusPath)) File.Replace(StatusPath + ".next", StatusPath, null);
     else File.Move(StatusPath + ".next", StatusPath);
     statusWritten = true;
   } catch { }
  }

  static void Poll() {
   double now = EditorApplication.timeSinceStartup;
   if (now >= nextHeartbeat) { nextHeartbeat = now + 2.0; WriteStatus(lastState, true); }
   if (now < nextPoll) return;
   nextPoll = now + 0.5;
   if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
   if (UserPlaying) {
    if (!pausedLogged) { pausedLogged = true; WriteStatus("paused: developer play session"); }
    return;
   }
   if (pausedLogged) { pausedLogged = false; WriteStatus("ready"); }
   try { Step(); } catch (Exception e) { Debug.LogError("[Bridge] command failed: " + e); AdvanceLine(); }
  }

  static void Step() {
   string active = SessionState.GetString(ActiveKey, "");
   if (string.IsNullOrEmpty(active) || !File.Exists(active)) {
    var next = Directory.Exists(Inbox) ? Directory.GetFiles(Inbox, "*.txt").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault() : null;
    if (next == null) return;
    active = next;
    SessionState.SetString(ActiveKey, active);
    SessionState.SetInt(LineKey, 0);
    Debug.Log("[Bridge] begin " + Path.GetFileName(active));
   }
   DateTime stamp = File.GetLastWriteTimeUtc(active);
   if (commandLines == null || commandFile != active || commandStamp != stamp) {
    commandLines = File.ReadAllLines(active); commandFile = active; commandStamp = stamp;
   }
   var lines = commandLines;
   int index = SessionState.GetInt(LineKey, 0);
   if (index >= lines.Length) { Finish(active); return; }
   string line = lines[index].Trim();
   if (line.Length == 0 || line.StartsWith("#")) { AdvanceLine(); return; }
   WriteStatus("run " + line);
   if (Execute(line)) AdvanceLine();
  }

  static void AdvanceLine() {
   SessionState.SetInt(LineKey, SessionState.GetInt(LineKey, 0) + 1);
   SessionState.EraseFloat(WaitUntilKey);
  }

  static void Finish(string active) {
   try {
    string target = Path.Combine(Done, Path.GetFileName(active));
    if (File.Exists(target)) File.Delete(target);
    File.Move(active, target);
   } catch (Exception e) { Debug.LogWarning("[Bridge] could not archive command file: " + e.Message); }
   Debug.Log("[Bridge] end " + Path.GetFileName(active));
   SessionState.EraseString(ActiveKey); SessionState.EraseInt(LineKey);
   WriteStatus("idle");
  }

  // Returns true when the command completed and the queue may advance.
  static bool Execute(string line) {
   string[] parts = line.Split(new[] { ' ' }, 2);
   string cmd = parts[0].ToLowerInvariant();
   string arg = parts.Length > 1 ? parts[1].Trim() : "";
   switch (cmd) {
    case "log": Debug.Log("[Bridge] " + arg); return true;
    case "refresh": AssetDatabase.Refresh(); return true;
    case "reimport": AssetDatabase.ImportAsset(arg, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive); return true;
    case "clearconsole": lock (fileLock) { try { File.WriteAllText(ConsolePath, ""); } catch { } } return true;
    case "wait": {
     float until = SessionState.GetFloat(WaitUntilKey, -1);
     if (until < 0) { SessionState.SetFloat(WaitUntilKey, (float)EditorApplication.timeSinceStartup + Parse(arg, 1)); return false; }
     return EditorApplication.timeSinceStartup >= until;
    }
    case "play":
     if (!EditorApplication.isPlaying) {
      // Play mode cannot start while scripts have compile errors; waiting would hold the whole queue
      // (every agent's jobs) until someone refreshed by hand. End this job instead and say why.
      if (EditorUtility.scriptCompilationFailed && !EditorApplication.isPlayingOrWillChangePlaymode) {
       Debug.LogError("[Bridge] play refused: scripts have compile errors (see ClaudeBridge/compile.log); ending " + Path.GetFileName(SessionState.GetString(ActiveKey, "")) + " so the queue keeps moving");
       SessionState.SetInt(LineKey, int.MaxValue - 1);
       return false;
      }
      if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(BridgePlayKey, true); EditorApplication.EnterPlaymode(); }
      return false;
     }
     return true;
    case "stop":
     if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return false; }
     return !EditorApplication.isPlayingOrWillChangePlaymode;
    case "waitplay": {
     if (!EditorApplication.isPlaying) return false;
     return EditorApplication.timeSinceStartup - SessionState.GetFloat(PlayStartKey, 0) >= Parse(arg, 5);
    }
    // waitfor [!]Namespace.Type.Member [timeout]: holds the queue until a static bool reads true
    // (or false with "!"), e.g. "waitfor !LostRealmsOW.OpenWorldMotionCapture.Busy 40".
    case "waitfor": {
     var wp = arg.Split(' ');
     bool negate = wp[0].StartsWith("!", StringComparison.Ordinal);
     float until = SessionState.GetFloat(WaitUntilKey, -1);
     if (until < 0) SessionState.SetFloat(WaitUntilKey, (float)EditorApplication.timeSinceStartup + (wp.Length > 1 ? Parse(wp[1], 30) : 30));
     else if (EditorApplication.timeSinceStartup >= until) { Debug.LogWarning("[Bridge] waitfor timed out: " + arg); return true; }
     return ReadStaticBool(negate ? wp[0].Substring(1) : wp[0]) != negate;
    }
    case "menu": if (!EditorApplication.ExecuteMenuItem(arg)) Debug.LogError("[Bridge] menu not found: " + arg); return true;
    case "exec": Invoke(arg); return true;
    case "openscene": UnityEditor.SceneManagement.EditorSceneManager.OpenScene(arg, UnityEditor.SceneManagement.OpenSceneMode.Single); return true;
    case "save": AssetDatabase.SaveAssets(); return true;
    case "setting": ApplySetting(arg); return true;
    case "shot": Capture(arg); return true;
    case "view": SetView(arg); return true;
    case "tp": Teleport(arg); return true;
    case "follow": ReleaseView(); return true;
    case "timeofday": RuntimeCall("LostRealmsOW.OpenWorldAtmosphere", "DebugSetPreset", arg); return true;
    case "call": RuntimeCallLine(arg); return true;
    default: Debug.LogError("[Bridge] unknown command: " + line); return true;
   }
  }

  static float Parse(string s, float fallback) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

  static void Invoke(string qualified) {
   int dot = qualified.LastIndexOf('.');
   string typeName = qualified.Substring(0, dot), method = qualified.Substring(dot + 1);
   var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
   if (type == null) { Debug.LogError("[Bridge] type not found: " + typeName); return; }
   var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
   if (mi == null) { Debug.LogError("[Bridge] method not found: " + qualified); return; }
   var started = DateTime.Now;
   mi.Invoke(null, null);
   Debug.Log("[Bridge] exec " + qualified + " finished in " + (DateTime.Now - started).TotalSeconds.ToString("0.0") + "s");
  }

  static bool ReadStaticBool(string qualified) {
   int dot = qualified.LastIndexOf('.');
   if (dot <= 0) return false;
   string typeName = qualified.Substring(0, dot), member = qualified.Substring(dot + 1);
   if (!boolMembers.TryGetValue(qualified, out var resolved)) {
    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
    if (type == null) return false;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    resolved = (MemberInfo)type.GetProperty(member, flags) ?? type.GetField(member, flags);
    if (resolved == null) return false;
    boolMembers[qualified] = resolved;
   }
   object value = resolved is PropertyInfo property ? property.GetValue(null) : ((FieldInfo)resolved).GetValue(null);
   return value is bool b && b;
  }

  static void RuntimeCallLine(string arg) {
   var p = arg.Split(new[] { ' ' }, 2);
   int dot = p[0].LastIndexOf('.');
   RuntimeCall(p[0].Substring(0, dot), p[0].Substring(dot + 1), p.Length > 1 ? p[1] : null);
  }

  static void RuntimeCall(string typeName, string method, string argument) {
   var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
   var mi = type?.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
   if (mi == null) { Debug.LogError("[Bridge] runtime method missing: " + typeName + "." + method); return; }
   var ps = mi.GetParameters();
   mi.Invoke(null, ps.Length == 0 ? null : new object[] { argument });
  }

  static void ApplySetting(string arg) {
   switch (arg) {
    case "runInBackground": PlayerSettings.runInBackground = true; break;
    case "noThrottle": EditorPrefs.SetInt("InteractionMode", 1); break;
    default: Debug.LogError("[Bridge] unknown setting " + arg); break;
   }
  }

  // view x y z yaw pitch [fov]: detaches the gameplay camera for a framed capture.
  static void SetView(string arg) {
   var f = arg.Split(' ').Select(s => Parse(s, 0)).ToArray();
   var cam = Camera.main;
   if (!cam) { Debug.LogError("[Bridge] no main camera"); return; }
   var rig = cam.GetComponent<OpenWorldCamera>();
   if (rig) rig.enabled = false;
   cam.transform.position = new Vector3(f[0], f[1], f[2]);
   cam.transform.rotation = Quaternion.Euler(f.Length > 4 ? f[4] : 15, f.Length > 3 ? f[3] : 0, 0);
   if (f.Length > 5 && f[5] > 1) cam.fieldOfView = f[5];
  }

  static void ReleaseView() {
   var cam = Camera.main;
   if (!cam) return;
   var rig = cam.GetComponent<OpenWorldCamera>();
   if (rig) { rig.enabled = true; rig.SnapToTarget(); }
   cam.fieldOfView = 60;
  }

  // tp x z [yaw pitch]: moves Aster onto the ground at x,z and frames the follow camera.
  static void Teleport(string arg) {
   var f = arg.Split(' ').Select(s => Parse(s, 0)).ToArray();
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Debug.LogError("[Bridge] no hero"); return; }
   gm.Hero.WarpTo(new Vector3(f[0], 0, f[1]));
   var rig = gm.CameraRig;
   if (rig) {
    rig.enabled = true;
    if (f.Length > 2) rig.Yaw = f[2];
    if (f.Length > 3) rig.Pitch = f[3];
    rig.SnapToTarget();
   }
  }

  // shot name [width height] [ui]: renders the main camera (optionally with screen-space UI) to a PNG.
  static void Capture(string arg) {
   var p = arg.Split(' ');
   string name = p[0];
   int w = p.Length > 2 ? (int)Parse(p[1], 1600) : 1600, h = p.Length > 2 ? (int)Parse(p[2], 900) : 900;
   bool ui = p.Contains("ui");
   var cam = Camera.main;
   if (!cam) { Debug.LogError("[Bridge] no camera for capture"); return; }
   string path = Path.Combine(Shots, name + (ui ? ".png" : ".jpg"));
   if (ui) {
    ScreenCapture.CaptureScreenshot(path, 1);
    Debug.Log("[Bridge] screen capture requested " + path);
    return;
   }
   // Render into an HDR target: with an 8-bit target URP kept an LDR intermediate, so everything
   // brighter than 1.0 clipped before tonemapping (snow and sky turned flat grey-white at ~208).
   // The tonemapped result is then copied into an sRGB 8-bit target for readback.
   var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.DefaultHDR);
   rt.antiAliasing = 4;
   var previous = cam.targetTexture;
   cam.targetTexture = rt;
   cam.Render();
   cam.targetTexture = previous;
   var ldr = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
   Graphics.Blit(rt, ldr);
   var active = RenderTexture.active;
   RenderTexture.active = ldr;
   var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
   tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
   tex.Apply();
   RenderTexture.active = active;
   RenderTexture.ReleaseTemporary(ldr);
   RenderTexture.ReleaseTemporary(rt);
   File.WriteAllBytes(path, tex.EncodeToJPG(88));
   UnityEngine.Object.DestroyImmediate(tex);
   Debug.Log("[Bridge] captured " + path + " at " + cam.transform.position.ToString("F1"));
  }
 }
}
