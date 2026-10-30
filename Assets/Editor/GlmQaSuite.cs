// GLM QA autopilot (brief: Docs/TASKS_GLM_2026-10-29.md, Phase 1).
// Drives a play-mode tour of representative realm stops on one graphics tier per run: at each stop it
// settles for streaming, samples wall-clock frame times, records UnityEditor.UnityStats and live actor
// counts, saves a screenshot and tags exceptions raised while there. Each run appends a result fragment
// under Docs/QA/; GlmQaSuite.WriteReport merges the fragments into the baseline markdown.
// Bridge usage:
//   call LostRealmsOW.GlmQaSuite.BeginRun balanced
//   waitfor !LostRealmsOW.GlmQaSuite.Busy 240
//   exec LostRealmsOW.GlmQaSuite.WriteReport
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW
{
	public static class GlmQaSuite
	{
		const float SettleSeconds = 3f;
		const float MeasureSeconds = 3f;
		const string ResultsDir = "Docs/QA";
		const string ShotsDir = "ClaudeBridge/shots";

		// Ten stops covering the hub, every village/region asked for by the brief and the sky isle.
		// The brief's "North Pass" has no POI in code; Frozen Lake stands in for it.
		static readonly (string name, float x, float z, float yaw, float pitch)[] Stops =
		{
			("hub",          0f,     0f,    0f, 22f),
			("oakvale",      44f,   42f,  -35f, 24f),
			("mirror_lake", -150f, 150f,  120f, 26f),
			("frostpine",   -150f, 262f,  160f, 24f),
			("frozen_lake",   78f, 318f, -150f, 24f),
			("sunspire",     318f, -48f,  -80f, 24f),
			("dune_ruins",   402f,  70f,  140f, 24f),
			("graveyard",   -212f,-222f,  -60f, 26f),
			("stormveil",    232f,-262f,  -30f, 26f),
			("sky_isle",     -84f,  92f,   10f, 30f),
		};

		public static bool Busy { get; private set; }

		sealed class Result
		{
			public string stop;
			public float avgMs, p95Ms, shortestMs, longestMs;
			public int batches, setPass, triangles, enemies, creatures, npcs, exceptions;
			public Vector3 heroPos;
			public string screenshot;
		}

		sealed class Entry
		{
			public string stop, condition, stackLine;
		}

		static string tier = "unknown";
		static readonly List<Result> results = new List<Result>();
		static readonly List<Entry> exceptions = new List<Entry>();
		static Runner runner;
		static string currentStop = "startup";

		public static void BeginRun(string tierArg)
		{
			var gm = OpenWorldManager.I;
			if (Busy) { Debug.LogWarning("[GlmQa] run already in progress"); return; }
			if (!Application.isPlaying || gm == null || gm.Hero == null)
			{
				Debug.LogError("[GlmQa] BeginRun needs Play mode with the hero spawned");
				return;
			}
			tier = string.IsNullOrEmpty(tierArg) ? "unknown" : tierArg.Trim().ToLowerInvariant();
			results.Clear();
			exceptions.Clear();
			Application.logMessageReceived -= OnLog;
			Application.logMessageReceived += OnLog;
			if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
			runner = new GameObject("GlmQaRunner").AddComponent<Runner>();
			UnityEngine.Object.DontDestroyOnLoad(runner);
			Busy = true;
			Debug.Log($"[GlmQa] begin tier {tier}, {Stops.Length} stops, {SettleSeconds:0}s settle + {MeasureSeconds:0}s measure each");
		}

		// Merges every Docs/QA/qa_results_*.txt fragment into the baseline markdown. Safe to call in
		// edit mode after the runs; rerunning overwrites the report with the newest fragments.
		public static void WriteReport()
		{
			Directory.CreateDirectory(ResultsDir);
			var sb = new StringBuilder();
			sb.AppendLine("# QA baseline — 29 October 2026");
			sb.AppendLine();
			sb.AppendLine("Produced by `Assets/Editor/GlmQaSuite.cs` (GLM, ZCode) through `ClaudeBridge`: at each stop the suite waits 3 s for streaming, samples 3 s of wall-clock frame times, records `UnityEditor.UnityStats`, counts live actors, saves a screenshot to `ClaudeBridge/shots/glmqa_<tier>_<stop>.jpg` and tags console errors/exceptions raised while there. Times are editor play-mode times on the desktop (Ryzen + discrete GPU class machine), so they are a relative baseline for regression and for the Phase 4 Android pass, not phone numbers. Filtered as known vendor noise: Fusion import worker, graphicsApiMask/EnvironmentDependencyValues, NetworkPeer, ADTM audio-thread wait.");
			sb.AppendLine();
			sb.AppendLine("Rerun: `Docs/QA/README.md`. The brief's \"North Pass\" has no POI in code, so Frozen Lake stands in for it.");
			sb.AppendLine();
			foreach (var fragment in Directory.Exists(ResultsDir) ? Directory.GetFiles(ResultsDir, "qa_results_*.txt") : Array.Empty<string>())
			{
				string[] lines = File.ReadAllLines(fragment);
				if (lines.Length < 2) continue;
				sb.AppendLine(lines[0]);
				sb.AppendLine();
				sb.AppendLine("| Stop | Avg ms | P95 ms | ~FPS | Short/long ms | Batches | SetPass | Tris | Enemies | Creatures | NPCs | Errors+ |");
				sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
				var exceptionLines = new List<string>();
				for (int i = 1; i < lines.Length; i++)
				{
					if (lines[i].StartsWith("exception: "))
					{
						exceptionLines.Add(lines[i].Substring(11));
						continue;
					}
					string[] c = lines[i].Split('|');
					if (c.Length < 13) continue;
					sb.AppendLine($"| {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]}/{c[5]} | {c[6]} | {c[7]} | {c[8]} | {c[9]} | {c[10]} | {c[11]} | {c[12]} |");
				}
				sb.AppendLine();
				if (exceptionLines.Count == 0) sb.AppendLine("Exceptions: none.");
				else
				{
					sb.AppendLine("Exceptions (stop — first line — stack head):");
					foreach (var e in exceptionLines) sb.AppendLine("- " + e);
				}
				sb.AppendLine();
			}
			string path = Path.Combine(ResultsDir, "QA_BASELINE_2026-10-29.md");
			File.WriteAllText(path, sb.ToString());
			Debug.Log("[GlmQa] report written " + path);
		}

		static void OnLog(string condition, string stackTrace, LogType type)
		{
			if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
			if (IsNoise(condition)) return;
			exceptions.Add(new Entry
			{
				stop = currentStop,
				condition = condition.Replace("|", "/"),
				stackLine = FirstStackLine(stackTrace)
			});
		}

		static bool IsNoise(string message)
		{
			if (string.IsNullOrEmpty(message)) return true;
			return message.Contains("EnvironmentDependencyValues")
				|| message.Contains("graphicsApiMask")
				|| message.Contains("[FusionEditor/Import]")
				|| message.Contains("Can't update custom dependencies")
				|| message.Contains("NetworkPeer")
				|| message.Contains("ADTM");
		}

		static string FirstStackLine(string stackTrace)
		{
			if (string.IsNullOrEmpty(stackTrace)) return "";
			foreach (var raw in stackTrace.Split('\n'))
			{
				var line = raw.Trim();
				if (line.Length == 0) continue;
				if (line.StartsWith("UnityEngine.")) continue;
				int cut = line.IndexOf(" (at ");
				return cut > 0 ? line.Substring(0, cut) : line;
			}
			return "";
		}

		sealed class Runner : MonoBehaviour
		{
			int index = -1;
			int phase; // 1 = settle, 2 = measure
			double phaseStart, lastFrame;
			readonly List<double> frames = new List<double>(256);
			ProfilerRecorder batchesCounter, setPassCounter, trianglesCounter;

			void OnEnable()
			{
				// Render metrics from the profiler module instead of UnityEditor.UnityStats (whose batches
				// member is gone in Unity 6): the
				// Unity 6 members differ and ProfilerRecorder also works on device for Phase 4.
				batchesCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
				setPassCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
				trianglesCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
			}

			void OnDisable()
			{
				batchesCounter.Dispose();
				setPassCounter.Dispose();
				trianglesCounter.Dispose();
			}

			void Update()
			{
				double now = Time.realtimeSinceStartupAsDouble;
				if (index >= 0 && phase > 0 && lastFrame > 0 && phase == 2)
					frames.Add((now - lastFrame) * 1000.0);
				lastFrame = now;

				if (index < 0)
				{
					index = 0;
					Enter();
					return;
				}
				if (now - phaseStart < (phase == 1 ? SettleSeconds : MeasureSeconds)) return;

				if (phase == 1)
				{
					frames.Clear();
					phase = 2;
					phaseStart = now;
					lastFrame = 0; // first delta spans the settle/measure boundary; drop it
					currentStop = Stops[index].name;
				}
				else
				{
					Record();
					index++;
					if (index >= Stops.Length) { Finish(); return; }
					Enter();
				}
			}

			void Enter()
			{
				var s = Stops[index];
				currentStop = s.name + " (arriving)";
				var gm = OpenWorldManager.I;
				if (gm == null || gm.Hero == null) { Debug.LogError("[GlmQa] hero missing at " + s.name); Finish(); return; }
				gm.Hero.WarpTo(new Vector3(s.x, 0f, s.z));
				if (gm.CameraRig)
				{
					gm.CameraRig.enabled = true;
					gm.CameraRig.Yaw = s.yaw;
					gm.CameraRig.Pitch = s.pitch;
					gm.CameraRig.SnapToTarget();
				}
				phase = 1;
				phaseStart = Time.realtimeSinceStartupAsDouble;
			}

			void Record()
			{
				var s = Stops[index];
				var r = new Result { stop = s.name, screenshot = $"{ShotsDir}/glmqa_{tier}_{s.name}.jpg" };
				int n = frames.Count;
				if (n > 0)
				{
					double sum = 0;
					var sorted = new List<double>(frames);
					sorted.Sort();
					foreach (var f in frames) sum += f;
					r.avgMs = (float)(sum / n);
					r.p95Ms = (float)sorted[Mathf.Clamp((int)Math.Ceiling(n * 0.95) - 1, 0, n - 1)];
					r.shortestMs = (float)sorted[0];
					r.longestMs = (float)sorted[n - 1];
				}
				r.batches = (int)batchesCounter.LastValue;
				r.setPass = (int)setPassCounter.LastValue;
				r.triangles = (int)trianglesCounter.LastValue;
				r.enemies = FindObjectsByType<OpenWorldEnemy>().Length;
				r.creatures = FindObjectsByType<OpenWorldWildlife>().Length;
				r.npcs = FindObjectsByType<OpenWorldNPC>().Length;
				var gm = OpenWorldManager.I;
				r.heroPos = gm && gm.Hero ? gm.Hero.transform.position : Vector3.zero;
				r.exceptions = exceptions.Count;
				results.Add(r);
				currentStop = s.name;
				Debug.Log($"[GlmQa] {tier} {s.name}: avg {r.avgMs:0.0} ms, p95 {r.p95Ms:0.0} ms, batches {r.batches}, setPass {r.setPass}, tris {r.triangles}, foes {r.enemies}, creatures {r.creatures}, npcs {r.npcs}, errors+ {r.exceptions}, hero {r.heroPos.ToString("0.0")}");
				SaveShot(r.screenshot);
			}

			void SaveShot(string relative)
			{
				try
				{
					var cam = Camera.main;
					if (!cam) return;
					const int w = 1280, h = 720;
					var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.DefaultHDR);
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
					File.WriteAllBytes(relative, tex.EncodeToJPG(85));
					UnityEngine.Object.Destroy(tex);
				}
				catch (Exception e)
				{
					Debug.LogWarning("[GlmQa] screenshot failed: " + e.Message);
				}
			}

			void Finish()
			{
				WriteFragment();
				Busy = false;
				currentStop = "idle";
				Application.logMessageReceived -= GlmQaSuite.OnLog;
				Destroy(gameObject);
			}
		}

		static void WriteFragment()
		{
			Directory.CreateDirectory(ResultsDir);
			var sb = new StringBuilder();
			var g = OpenWorldGraphics.Instance;
			sb.AppendLine($"## {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier)} tier — target {Application.targetFrameRate} fps — {DateTime.Now:yyyy-MM-dd HH:mm} — Unity {Application.unityVersion}");
			foreach (var r in results)
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
					"{0}|{1:0.00}|{2:0.00}|{3:0}|{4:0.0}|{5:0.0}|{6}|{7}|{8}|{9}|{10}|{11}|{12}",
					r.stop, r.avgMs, r.p95Ms, r.avgMs > 0 ? 1000f / r.avgMs : 0f, r.shortestMs, r.longestMs,
					r.batches, r.setPass, r.triangles, r.enemies, r.creatures, r.npcs, r.exceptions));
			foreach (var e in exceptions)
				sb.AppendLine($"exception: {e.stop} — {e.condition} — {e.stackLine}");
			string path = Path.Combine(ResultsDir, $"qa_results_{tier}.txt");
			File.WriteAllText(path, sb.ToString());
			Debug.Log($"[GlmQa] fragment written {path} ({results.Count} stops, {exceptions.Count} tagged exceptions)");
		}
	}
}
