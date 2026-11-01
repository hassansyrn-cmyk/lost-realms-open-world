using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
	public static class BanditCampPlacementValidation {
		const string ReportPath = "Docs/QA/BANDIT_CAMP_PLACEMENT_REPORT.txt";
		static int waitFrames = 0;
		static float startTime = 0f;
		public static string CaptureSuffix = "after";

		public static void RunValidation() {
			if (!Application.isPlaying) {
				Debug.LogError("BanditCampPlacementValidation must be run in Play mode.");
				return;
			}
			startTime = Time.realtimeSinceStartup;
			waitFrames = 0;
			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnUpdate;
			Debug.Log("[BanditCampPlacementValidation] Hooked EditorApplication.update, waiting for world readiness...");
		}

		static void OnUpdate() {
			var gm = OpenWorldManager.I;
			if (!gm || !gm.Hero || !gm.ActiveTerrain) {
				if (Time.realtimeSinceStartup - startTime > 15f) {
					EditorApplication.update -= OnUpdate;
					Debug.LogError("[BanditCampPlacementValidation] Timed out waiting for OpenWorldManager / Hero.");
					File.WriteAllText(ReportPath, "ERROR: Timed out waiting for OpenWorldManager / Hero.");
				}
				return;
			}

			waitFrames++;
			if (waitFrames < 10) return;

			EditorApplication.update -= OnUpdate;
			ExecuteValidation(gm);
		}

		static void ExecuteValidation(OpenWorldManager gm) {
			var sb = new StringBuilder();
			sb.AppendLine($"=== BANDIT CAMP PHYSICAL PLACEMENT VALIDATION ===");
			sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

			var ambush = OpenWorldBanditAmbush.Instance;
			if (!ambush) {
				sb.AppendLine("FAIL: OpenWorldBanditAmbush instance not found.");
				WriteReport(sb);
				return;
			}

			var terrain = gm.ActiveTerrain;
			Vector3 anchor = OpenWorldBanditAmbush.RoadsideAnchor;
			float anchorGroundY = SampleTerrain(terrain, anchor);
			Vector3 baseAnchor = new Vector3(anchor.x, anchorGroundY, anchor.z);

			// Move hero near camp to trigger streaming build
			gm.Hero.transform.position = baseAnchor + new Vector3(0f, 0f, -6f);
			ambush.Rebuild();

			var cluster = GameObject.Find("Bandit Ambush Cluster");
			if (!cluster) {
				sb.AppendLine("FAIL: Bandit Ambush Cluster GameObject not found after rebuild.");
				WriteReport(sb);
				return;
			}

			sb.AppendLine($"Cluster position: {cluster.transform.position}");
			sb.AppendLine($"Roadside anchor: {baseAnchor:F3}");

			var transforms = new List<Transform>();
			for (int i = 0; i < cluster.transform.childCount; i++) {
				transforms.Add(cluster.transform.GetChild(i));
			}

			sb.AppendLine($"\n--- PROPS IN CLUSTER ({transforms.Count} items) ---");
			int overlapCount = 0;
			int floatingCount = 0;
			int buriedCount = 0;
			bool roadPassable = true;

			foreach (var t in transforms) {
				sb.AppendLine($"\nProp: {t.name}");
				sb.AppendLine($"  Position: {t.position.x:F3}, {t.position.y:F3}, {t.position.z:F3}");
				sb.AppendLine($"  Rotation: {t.rotation.eulerAngles.x:F1}, {t.rotation.eulerAngles.y:F1}, {t.rotation.eulerAngles.z:F1}");
				sb.AppendLine($"  LocalScale: {t.localScale.x:F3}, {t.localScale.y:F3}, {t.localScale.z:F3}");

				var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(t);
				sb.AppendLine($"  Bounds Center: {bounds.center.x:F3}, {bounds.center.y:F3}, {bounds.center.z:F3}");
				sb.AppendLine($"  Bounds Size: {bounds.size.x:F3}, {bounds.size.y:F3}, {bounds.size.z:F3}");
				sb.AppendLine($"  Bounds Min: {bounds.min.x:F3}, {bounds.min.y:F3}, {bounds.min.z:F3}");
				sb.AppendLine($"  Bounds Max: {bounds.max.x:F3}, {bounds.max.y:F3}, {bounds.max.z:F3}");

				float terrainCenterY = SampleTerrain(terrain, bounds.center);
				float terrainPivotY = SampleTerrain(terrain, t.position);
				float deltaMinY = bounds.min.y - terrainCenterY;

				// Physical base center and 4 base corners in world space
				var localBounds = ambush.GetLocalRendererBounds(t);
				Vector3 localBaseCenter = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
				Vector3 worldBaseCenter = t.TransformPoint(localBaseCenter);
				float groundBaseCenter = SampleTerrain(terrain, worldBaseCenter);
				float baseCenterClearance = worldBaseCenter.y - groundBaseCenter;

				Vector3 lc1 = new Vector3(localBounds.min.x, localBounds.min.y, localBounds.min.z);
				Vector3 lc2 = new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z);
				Vector3 lc3 = new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z);
				Vector3 lc4 = new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z);

				Vector3 wc1 = t.TransformPoint(lc1);
				Vector3 wc2 = t.TransformPoint(lc2);
				Vector3 wc3 = t.TransformPoint(lc3);
				Vector3 wc4 = t.TransformPoint(lc4);

				float clr1 = wc1.y - SampleTerrain(terrain, wc1);
				float clr2 = wc2.y - SampleTerrain(terrain, wc2);
				float clr3 = wc3.y - SampleTerrain(terrain, wc3);
				float clr4 = wc4.y - SampleTerrain(terrain, wc4);

				float minCornerClearance = Mathf.Min(Mathf.Min(clr1, clr2), Mathf.Min(clr3, clr4));
				float maxCornerClearance = Mathf.Max(Mathf.Max(clr1, clr2), Mathf.Max(clr3, clr4));

				sb.AppendLine($"  Terrain at Pivot: {terrainPivotY:F3}, at Center: {terrainCenterY:F3}");
				sb.AppendLine($"  Base Center Ground: {groundBaseCenter:F3}, Clearance: {baseCenterClearance:F3} m");
				sb.AppendLine($"  Contact Clearance Range: [{minCornerClearance:F3} m to {maxCornerClearance:F3} m]");

				if (minCornerClearance < -0.15f || baseCenterClearance < -0.10f) {
					sb.AppendLine($"  FAIL: Prop is deeply buried (exceeds -15cm corner or -10cm base: min={minCornerClearance:F3}m, base={baseCenterClearance:F3}m)");
					buriedCount++;
				}
				if (maxCornerClearance > 0.08f || baseCenterClearance > 0.05f) {
					sb.AppendLine($"  FAIL: Prop is floating (exceeds +8cm corner or +5cm base: max={maxCornerClearance:F3}m, base={baseCenterClearance:F3}m)");
					floatingCount++;
				}

				float roadDist = OpenWorldTerrainSculptor.RoadDistance(t.position.x, t.position.z);
				sb.AppendLine($"  Road Distance: {roadDist:F2} m");
				if (roadDist < 2.0f) {
					sb.AppendLine($"  FAIL: Prop obstructs road walking route (road dist < 2.0m: {roadDist:F2}m)");
					roadPassable = false;
				}

				// Check overlap with other children (excluding labels / child effects)
				foreach (var other in transforms) {
					if (other == t) continue;
					var otherBounds = OpenWorldMaterialHelper.ComputeRendererBounds(other);
					if (bounds.Intersects(otherBounds)) {
						sb.AppendLine($"  FAIL: Intersects with {other.name} (overlap volume detected)");
						overlapCount++;
					}
				}
			}

			// Requirement 5: Manifest note upright and independent reachability
			bool noteUpright = false;
			if (ambush.NoteObject) {
				float noteRotX = ambush.NoteObject.transform.rotation.eulerAngles.x;
				float noteRotZ = ambush.NoteObject.transform.rotation.eulerAngles.z;
				noteUpright = (Mathf.Abs(Mathf.DeltaAngle(noteRotX, 0f)) < 1.0f) && (Mathf.Abs(Mathf.DeltaAngle(noteRotZ, 0f)) < 1.0f);
			}

			float cacheNoteDist = 0f;
			if (ambush.CacheObject && ambush.NoteObject) {
				cacheNoteDist = Vector3.Distance(ambush.CacheObject.transform.position, ambush.NoteObject.transform.position);
			}

			sb.AppendLine($"\n--- INDEPENDENT INTERACTION & ACCESSIBILITY ---");
			sb.AppendLine($"Manifest note upright: {noteUpright} (rot: {ambush.NoteObject?.transform.rotation.eulerAngles:F1})");
			sb.AppendLine($"Cache to Manifest Note distance: {cacheNoteDist:F2} m (requirement >= 4.0m)");

			// Test independent targeting:
			// 1. Stand at Note
			gm.Hero.transform.position = ambush.NoteObject.transform.position + new Vector3(0.5f, 0f, -0.8f);
			var targetAtNote = ambush.GetNearestValidTarget();
			sb.AppendLine($"Target when standing near note: {targetAtNote} (expected: Note)");

			// 2. Stand at Cache
			gm.Hero.transform.position = ambush.CacheObject.transform.position + new Vector3(0f, 0f, -1.0f);
			var targetAtCache = ambush.GetNearestValidTarget();
			sb.AppendLine($"Target when standing near cache: {targetAtCache} (expected: Cache)");

			// Requirement 7: Streaming and duplicate prevention test
			sb.AppendLine($"\n--- STREAMING TEARDOWN & REBUILD ---");
			int childCountBefore = cluster.transform.childCount;
			// Move far away (>140m)
			gm.Hero.transform.position = baseAnchor + new Vector3(200f, 0f, 200f);
			ambush.SendMessage("Update", null, SendMessageOptions.DontRequireReceiver);
			bool teardownOk = (ambush.CacheObject == null && ambush.NoteObject == null);
			sb.AppendLine($"Far distance teardown: references cleared = {teardownOk}");

			// Return near (<90m)
			gm.Hero.transform.position = baseAnchor + new Vector3(0f, 0f, -6f);
			ambush.SendMessage("Update", null, SendMessageOptions.DontRequireReceiver);
			bool rebuildOk = (ambush.CacheObject != null && ambush.NoteObject != null);
			var rebuiltCluster = GameObject.Find("Bandit Ambush Cluster");
			int childCountAfter = rebuiltCluster ? rebuiltCluster.transform.childCount : 0;
			sb.AppendLine($"Return near rebuild: rebuilt={rebuildOk}, child count before={childCountBefore}, after={childCountAfter} (duplicate props = {childCountAfter != childCountBefore})");

			// Walking route test: Aster traverses Kings Road past the camp from X=95 to X=120 at road centerline Z=-18.5
			sb.AppendLine($"\n--- ROAD PASSABILITY WALK TEST ---");
			bool roadWalkUnobstructed = true;
			for (float wx = 95f; wx <= 120f; wx += 2.5f) {
				Vector3 roadPt = new Vector3(wx, 0f, -18.5f);
				roadPt.y = SampleTerrain(terrain, roadPt);
				float minPropDist = float.MaxValue;
				foreach (var t in transforms) {
					if (!t) continue;
					var b = OpenWorldMaterialHelper.ComputeRendererBounds(t);
					Vector3 closest = b.ClosestPoint(roadPt);
					float d = Vector3.Distance(new Vector3(roadPt.x, 0f, roadPt.z), new Vector3(closest.x, 0f, closest.z));
					if (d < minPropDist) minPropDist = d;
				}
				if (minPropDist < 1.0f) {
					roadWalkUnobstructed = false;
					sb.AppendLine($"  Road obstruction at X={wx:F1}: prop distance {minPropDist:F2}m < 1.0m");
				}
			}
			sb.AppendLine($"Road walk test (X=95 to 120): unobstructed = {roadWalkUnobstructed}");

			// Capture matching camera angles with grounded camera positions
			Vector3 frontCamPos = baseAnchor + new Vector3(0f, 0f, -7.5f);
			frontCamPos.y = SampleTerrain(terrain, frontCamPos) + 1.6f;
			Vector3 frontTarget = baseAnchor + new Vector3(0f, 0.8f, 1.2f);
			CaptureView(sb, $"bandit_camp_front_{CaptureSuffix}.png", frontCamPos, frontTarget);

			Vector3 sideCamPos = baseAnchor + new Vector3(-8.5f, 0f, 0.5f);
			sideCamPos.y = SampleTerrain(terrain, sideCamPos) + 1.6f;
			Vector3 sideTarget = baseAnchor + new Vector3(0.5f, 0.8f, 1.0f);
			CaptureView(sb, $"bandit_camp_side_{CaptureSuffix}.png", sideCamPos, sideTarget);

			Vector3 rearCamPos = baseAnchor + new Vector3(2.5f, 0f, 7.5f);
			rearCamPos.y = SampleTerrain(terrain, rearCamPos) + 1.8f;
			Vector3 rearTarget = baseAnchor + new Vector3(-0.5f, 0.8f, 0.8f);
			CaptureView(sb, $"bandit_camp_rear_{CaptureSuffix}.png", rearCamPos, rearTarget);

			Vector3 detailCamPos = baseAnchor + new Vector3(-2.2f, 0f, -2.5f);
			detailCamPos.y = SampleTerrain(terrain, detailCamPos) + 1.2f;
			Vector3 detailTarget = baseAnchor + new Vector3(-0.8f, 0.7f, 1.0f);
			CaptureView(sb, $"bandit_camp_detail_{CaptureSuffix}.png", detailCamPos, detailTarget);

			sb.AppendLine($"\n=== FINAL VERDICT ===");
			bool pass = (overlapCount == 0) && (floatingCount == 0) && (buriedCount == 0) &&
			            roadPassable && roadWalkUnobstructed && noteUpright && (cacheNoteDist >= 4.0f) &&
			            (targetAtNote == OpenWorldBanditAmbush.AmbushTarget.Note) &&
			            (targetAtCache == OpenWorldBanditAmbush.AmbushTarget.Cache) &&
			            teardownOk && (childCountAfter == childCountBefore);

			sb.AppendLine(pass ? "ALL VERIFICATION CHECKS PASSED PERFECTLY." : "VERIFICATION FAILED ON ONE OR MORE CHECKS.");
			WriteReport(sb);
			Debug.Log($"BanditCampPlacementValidation finished. Result: {(pass ? "PASS" : "FAIL")}");
		}

		static float SampleTerrain(Terrain terrain, Vector3 worldPos) {
			if (terrain) {
				return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
			}
			return OpenWorldMaterialHelper.SampleGroundHeight(worldPos);
		}

		static void CaptureView(StringBuilder sb, string shotName, Vector3 camPos, Vector3 lookTarget) {
			var cam = Camera.main;
			if (!cam) return;
			cam.transform.position = camPos;
			cam.transform.LookAt(lookTarget);

			Directory.CreateDirectory("ClaudeBridge/shots");
			string path = Path.Combine("ClaudeBridge/shots", shotName);
			var rt = new RenderTexture(1280, 720, 24);
			var prevRt = cam.targetTexture;
			cam.targetTexture = rt;
			cam.Render();

			RenderTexture.active = rt;
			var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
			tex.Apply();

			cam.targetTexture = prevRt;
			RenderTexture.active = null;
			UnityEngine.Object.Destroy(rt);

			File.WriteAllBytes(path, tex.EncodeToPNG());
			UnityEngine.Object.Destroy(tex);
			sb.AppendLine($"Captured screenshot: {path}");
		}

		static void WriteReport(StringBuilder sb) {
			Directory.CreateDirectory("Docs/QA");
			File.WriteAllText(ReportPath, sb.ToString());
		}
	}
}
