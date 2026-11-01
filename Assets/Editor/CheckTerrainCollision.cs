using System;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
	public static class CheckTerrainCollision {
		const string ReportPath = "Docs/QA/OPEN_TERRAIN_JUMP_TRACE.txt";
		static int waitFrames = 0;
		static float startTime = 0f;
		static StringBuilder sb;

		public static void Inspect() {
			if (!Application.isPlaying) {
				Debug.LogError("Must run in Play mode");
				return;
			}
			startTime = Time.realtimeSinceStartup;
			waitFrames = 0;
			sb = new StringBuilder();
			sb.AppendLine("=== OPEN TERRAIN JUMP TRACE ===");
			sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnUpdate;
		}

		static void OnUpdate() {
			var gm = OpenWorldManager.I;
			if (!gm || !gm.Hero || !gm.ActiveTerrain) {
				if (Time.realtimeSinceStartup - startTime > 15f) {
					EditorApplication.update -= OnUpdate;
					File.WriteAllText(ReportPath, "ERROR: Timed out waiting for Hero/Terrain");
				}
				return;
			}

			waitFrames++;
			if (waitFrames == 1) {
				// Teleport hero to OPEN TERRAIN at (50, 0, 50) where there are NO props, NO gate, NO rocks!
				var terrain = gm.ActiveTerrain;
				Vector3 openPos = new Vector3(50f, 0f, 50f);
				openPos.y = terrain.SampleHeight(openPos) + terrain.transform.position.y + 0.1f;
				gm.Hero.transform.position = openPos;
				Physics.SyncTransforms();
				return;
			}

			if (waitFrames < 3) return; // Wait 3 frames

			EditorApplication.update -= OnUpdate;
			RunTrace(gm);
		}

		static Vector3 GetVelocity(OpenWorldHero hero) {
			var f = typeof(OpenWorldHero).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic);
			return f != null ? (Vector3)f.GetValue(hero) : Vector3.zero;
		}

		static bool CallProbeGround(OpenWorldHero hero) {
			var m = typeof(OpenWorldHero).GetMethod("ProbeGround", BindingFlags.Instance | BindingFlags.NonPublic);
			return m != null && (bool)m.Invoke(hero, null);
		}

		static void RunTrace(OpenWorldManager gm) {
			var hero = gm.Hero;
			var terrain = gm.ActiveTerrain;
			var cc = hero.Controller;

			sb.AppendLine("\n--- 1. OPEN TERRAIN ENVIRONMENT ---");
			Vector3 heroPos = hero.transform.position;
			float th = terrain.SampleHeight(heroPos) + terrain.transform.position.y;
			float sampleGroundY = OpenWorldMaterialHelper.SampleGroundHeight(heroPos, hero.transform);
			sb.AppendLine($"Hero Position: {heroPos}, Terrain Height: {th:F4}, SampleGroundHeight: {sampleGroundY:F4}");
			sb.AppendLine($"Diff (Hero.y - Terrain.y): {(heroPos.y - th):F4}");

			// Check all colliders near hero
			var nearColliders = Physics.OverlapSphere(heroPos, 2.0f, ~0, QueryTriggerInteraction.Ignore);
			sb.AppendLine($"Colliders within 2m: {nearColliders.Length}");
			foreach (var c in nearColliders) {
				sb.AppendLine($"  {c.name} ({c.GetType().Name}), layer={c.gameObject.layer}");
			}

			// Pre-jump state on open terrain
			sb.AppendLine("\n--- 2. PRE-JUMP STATE ON OPEN TERRAIN ---");
			sb.AppendLine($"Hero pos.y: {hero.transform.position.y:F4}");
			sb.AppendLine($"Controller.isGrounded: {cc.isGrounded}");
			sb.AppendLine($"ProbeGround(): {CallProbeGround(hero)}");
			sb.AppendLine($"Hero.Grounded: {hero.Grounded}");
			sb.AppendLine($"Hero.velocity: {GetVelocity(hero):F4}");
			sb.AppendLine($"Hero.Stamina: {hero.Stamina:F1}");

			// Test Raycast down
			Vector3 rOrigin = heroPos + Vector3.up * 0.1f;
			bool rHit = Physics.Raycast(rOrigin, Vector3.down, out var rh, 1.0f, ~0, QueryTriggerInteraction.Ignore);
			sb.AppendLine($"Raycast from hero+0.1m down 1.0m: hit={rHit}, col={rh.collider?.name}, pt={rh.point:F4}, dist={rh.distance:F4}");

			// Test Spherecast
			float r = cc.radius * 0.9f;
			Vector3 sOrigin = heroPos + Vector3.up * (r + 0.08f);
			var sHits = Physics.SphereCastAll(sOrigin, r, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore);
			sb.AppendLine($"SphereCastAll from hero+(r+0.08) radius={r:F2} down 0.5m: count={sHits.Length}");
			foreach (var sh in sHits) {
				sb.AppendLine($"  hit: {sh.collider.name} ({sh.collider.GetType().Name}), pt={sh.point:F4}, dist={sh.distance:F4}");
			}

			// Trace 20 frames with Jump press
			sb.AppendLine("\n--- 3. JUMP PRESS TRACE (20 FRAMES) ---");
			hero.TouchJump = true;
			for (int f = 0; f < 20; f++) {
				hero.SendMessage("Update", null, SendMessageOptions.DontRequireReceiver);
				var vel = GetVelocity(hero);
				sb.AppendLine($"Frame {f:D2}: pos.y={hero.transform.position.y:F4}, vel.y={vel.y:F2}, Grounded={hero.Grounded}, CC.isGrounded={cc.isGrounded}, Probe={CallProbeGround(hero)}, Gliding={hero.IsGliding}, Stamina={hero.Stamina:F1}");
			}

			Directory.CreateDirectory("Docs/QA");
			File.WriteAllText(ReportPath, sb.ToString());
			Debug.Log($"[CheckTerrainCollision] Trace written to {ReportPath}");
		}
	}
}
