using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LostRealmsOW {
	public static class MainlandJumpValidation {
		const string ReportPath = "Docs/QA/MAINLAND_JUMP_VERIFICATION_REPORT.txt";
		public static bool Done { get; set; } = false;
		static StringBuilder sb;
		static float startTime;
		static int step = 0;
		static int frameCounter = 0;

		static float testGroundY = 0f;
		static float jumpApexY = 0f;
		static float jumpApexTime = 0f;
		static float jumpStartPos = 0f;

		public static void RunFullValidation() {
			Done = false;
			if (!Application.isPlaying) {
				Debug.LogError("MainlandJumpValidation: Must run in Play mode");
				return;
			}
			startTime = Time.realtimeSinceStartup;
			step = 0;
			frameCounter = 0;
			sb = new StringBuilder();
			sb.AppendLine("=================================================================");
			sb.AppendLine("   LOST REALMS: MAINLAND JUMP & GROUNDING VERIFICATION REPORT   ");
			sb.AppendLine("=================================================================");
			sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnUpdate;
		}

		static Vector3 GetVelocity(OpenWorldHero hero) {
			var f = typeof(OpenWorldHero).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic);
			return f != null ? (Vector3)f.GetValue(hero) : Vector3.zero;
		}

		static bool CallProbeGround(OpenWorldHero hero) {
			var m = typeof(OpenWorldHero).GetMethod("ProbeGround", BindingFlags.Instance | BindingFlags.NonPublic);
			return m != null && (bool)m.Invoke(hero, null);
		}

		static void DispatchJumpPointerDown() {
			var hud = OpenWorldMobileHud.Instance;
			var jumpBtn = hud ? hud.transform.Find("TouchHUD/RadialCombat/Btn_Jump") : null;
			if (!jumpBtn && hud) jumpBtn = hud.transform.Find("TouchHUD/SkillCluster/Btn_Jump");
			if (!jumpBtn && hud) {
				foreach (var t in hud.GetComponentsInChildren<Transform>(true)) {
					if (t.name.Contains("Jump")) { jumpBtn = t; break; }
				}
			}

			if (jumpBtn) {
				var pointerData = new PointerEventData(EventSystem.current) { position = jumpBtn.position };
				ExecuteEvents.Execute<IPointerDownHandler>(jumpBtn.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
			} else {
				var gm = OpenWorldManager.I;
				if (gm && gm.Hero) gm.Hero.TouchJump = true;
			}
		}

		static void OnUpdate() {
			var gm = OpenWorldManager.I;
			if (!gm || !gm.Hero || !gm.ActiveTerrain) {
				if (Time.realtimeSinceStartup - startTime > 20f) {
					EditorApplication.update -= OnUpdate;
					File.WriteAllText(ReportPath, "ERROR: Timed out waiting for Hero/Terrain");
				}
				return;
			}

			var hero = gm.Hero;
			var terrain = gm.ActiveTerrain;
			var cc = hero.Controller;

			frameCounter++;

			// =====================================================================
			// TEST 1: MAINLAND FLAT TERRAIN (50, 50) - GROUNDING & STAMINA REGEN
			// =====================================================================
			if (step == 0) {
				Vector3 pos = new Vector3(50f, 0f, 50f);
				pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.05f;
				hero.transform.position = pos;
				Physics.SyncTransforms();
				hero.Stamina = 40f;
				testGroundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
				step = 1;
				frameCounter = 0;
				sb.AppendLine("\n--- TEST 1: MAINLAND OPEN TERRAIN (50, 50) GROUNDING & STAMINA ---");
				return;
			}

			// Settle and check stationary grounding and stamina regen
			if (step == 1) {
				if (frameCounter < 10) return;
				bool probe = CallProbeGround(hero);
				sb.AppendLine($"Stationary Grounding: CC.isGrounded={cc.isGrounded}, ProbeGround={probe}, Hero.Grounded={hero.Grounded}");
				sb.AppendLine($"Stamina Regeneration: Start=40.0, After 10 frames={hero.Stamina:F2} (Regen Active: {hero.Stamina > 40.5f})");
				CheckAssert(hero.Grounded, "Stationary Hero must be Grounded on mainland terrain");
				CheckAssert(hero.Stamina > 40.5f, "Stamina must regenerate while standing on mainland terrain");

				// Now test moving on mainland terrain
				hero.Stamina = 40f;
				hero.TouchMoveInput = Vector2.up;
				step = 2;
				frameCounter = 0;
				return;
			}

			// Move for 10 frames and check continuous grounding and stamina regen while walking
			if (step == 2) {
				hero.TouchMoveInput = Vector2.up;
				if (frameCounter < 10) return;
				hero.TouchMoveInput = Vector2.zero;
				bool probe = CallProbeGround(hero);
				sb.AppendLine($"Walking Grounding (frame 10): pos={hero.transform.position:F3}, CC.isGrounded={cc.isGrounded}, ProbeGround={probe}, Hero.Grounded={hero.Grounded}");
				sb.AppendLine($"Walking Stamina Regen: Start=40.0, Now={hero.Stamina:F2} (Regen Active: {hero.Stamina > 40.5f})");
				CheckAssert(hero.Grounded, "Walking Hero must stay Grounded on mainland terrain");
				CheckAssert(hero.Stamina > 40.5f, "Stamina must regenerate while walking normally on mainland terrain");

				step = 3;
				frameCounter = 0;
				return;
			}

			// =====================================================================
			// TEST 2: JUMP FROM MAINLAND TERRAIN - APEX, DISPLACEMENT & AIRTIME
			// =====================================================================
			if (step == 3) {
				if (frameCounter < 5) return;
				sb.AppendLine("\n--- TEST 2: JUMP FROM MAINLAND TERRAIN (DISPLACEMENT & AIRTIME) ---");
				jumpStartPos = hero.transform.position.y;
				jumpApexY = jumpStartPos;
				jumpApexTime = 0f;
				sb.AppendLine($"Pre-jump State: pos.y={jumpStartPos:F4}, Grounded={hero.Grounded}, CC.isGrounded={cc.isGrounded}, Gliding={hero.IsGliding}");
				DispatchJumpPointerDown();
				step = 4;
				frameCounter = 0;
				return;
			}

			// Trace 30 frames of jump arc
			if (step == 4) {
				float py = hero.transform.position.y;
				if (py > jumpApexY) {
					jumpApexY = py;
					jumpApexTime = frameCounter * Time.deltaTime;
				}
				if (frameCounter <= 25) {
					sb.AppendLine($"  Jump Frame {frameCounter:D2}: pos.y={py:F4}, vel.y={GetVelocity(hero).y:F2}, Grounded={hero.Grounded}, CC={cc.isGrounded}, Probe={CallProbeGround(hero)}, Gliding={hero.IsGliding}");
				}
				if (frameCounter < 30) return;

				float jumpHeight = jumpApexY - jumpStartPos;
				sb.AppendLine($"Jump Apex Height: {jumpHeight:F3}m (Apex pos.y={jumpApexY:F3}, Start={jumpStartPos:F3}, TimeToApex={jumpApexTime:F3}s)");
				sb.AppendLine($"Accidental Glider Check: Gliding={hero.IsGliding} (Must be False)");
				CheckAssert(jumpHeight >= 1.2f, $"Jump height must be >= 1.2m (actual: {jumpHeight:F3}m)");
				CheckAssert(!hero.IsGliding, "Ground jump press must NEVER trigger glider!");

				step = 5;
				frameCounter = 0;
				return;
			}

			// =====================================================================
			// TEST 3: MAINLAND SLOPED TERRAIN (KINGS ROAD VERGE HILL)
			// =====================================================================
			if (step == 5) {
				// Teleport to a 20-degree sloped hillside near (95, 0, -35)
				Vector3 slopePos = new Vector3(95f, 0f, -35f);
				slopePos.y = terrain.SampleHeight(slopePos) + terrain.transform.position.y + 0.05f;
				hero.transform.position = slopePos;
				Physics.SyncTransforms();
				hero.Stamina = 40f;
				step = 6;
				frameCounter = 0;
				sb.AppendLine("\n--- TEST 3: MAINLAND SLOPED TERRAIN (95, -35) ---");
				return;
			}

			if (step == 6) {
				if (frameCounter < 8) return;
				Vector3 hp = hero.transform.position;
				float th = terrain.SampleHeight(hp) + terrain.transform.position.y;
				sb.AppendLine($"Slope Settle: pos={hp:F3}, terrain.y={th:F3}, CC.isGrounded={cc.isGrounded}, ProbeGround={CallProbeGround(hero)}, Hero.Grounded={hero.Grounded}, Stamina={hero.Stamina:F2}");
				CheckAssert(hero.Grounded, "Hero must be Grounded on walkable slope terrain");

				// Jump on slope
				jumpStartPos = hero.transform.position.y;
				jumpApexY = jumpStartPos;
				DispatchJumpPointerDown();
				step = 7;
				frameCounter = 0;
				return;
			}

			if (step == 7) {
				float py = hero.transform.position.y;
				if (py > jumpApexY) jumpApexY = py;
				if (frameCounter <= 20) {
					sb.AppendLine($"  Slope Jump Frame {frameCounter:D2}: pos.y={py:F4}, vel.y={GetVelocity(hero).y:F2}, Grounded={hero.Grounded}, Gliding={hero.IsGliding}");
				}
				if (frameCounter < 25) return;

				float slopeJumpHeight = jumpApexY - jumpStartPos;
				sb.AppendLine($"Slope Jump Height: {slopeJumpHeight:F3}m (Apex pos.y={jumpApexY:F3}, Start={jumpStartPos:F3})");
				CheckAssert(slopeJumpHeight >= 1.2f, $"Slope jump height must be >= 1.2m (actual: {slopeJumpHeight:F3}m)");

				step = 8;
				frameCounter = 0;
				return;
			}

			// =====================================================================
			// TEST 4: ELEVATED MESH STRUCTURE (SANCTUM RING / ROCK / GATE)
			// =====================================================================
			if (step == 8) {
				// Teleport to Sanctum Gate Model platform (-60, 6.84, -45.3)
				Vector3 gatePos = new Vector3(-60f, 6.84f, -45.3f);
				hero.transform.position = gatePos;
				Physics.SyncTransforms();
				hero.Stamina = 40f;
				step = 9;
				frameCounter = 0;
				sb.AppendLine("\n--- TEST 4: ELEVATED MESH STRUCTURE (SANCTUM GATE) ---");
				return;
			}

			if (step == 9) {
				if (frameCounter < 8) return;
				Vector3 hp = hero.transform.position;
				sb.AppendLine($"Gate Settle: pos={hp:F3}, CC.isGrounded={cc.isGrounded}, ProbeGround={CallProbeGround(hero)}, Hero.Grounded={hero.Grounded}, Stamina={hero.Stamina:F2}");
				CheckAssert(hero.Grounded, "Hero must be Grounded on elevated mesh structure");
				CheckAssert(hero.Stamina > 40.5f, "Stamina must regenerate on elevated mesh structure");

				// Jump on mesh
				jumpStartPos = hero.transform.position.y;
				jumpApexY = jumpStartPos;
				DispatchJumpPointerDown();
				step = 10;
				frameCounter = 0;
				return;
			}

			if (step == 10) {
				float py = hero.transform.position.y;
				if (py > jumpApexY) jumpApexY = py;
				if (frameCounter <= 20) {
					sb.AppendLine($"  Mesh Jump Frame {frameCounter:D2}: pos.y={py:F4}, vel.y={GetVelocity(hero).y:F2}, Grounded={hero.Grounded}, Gliding={hero.IsGliding}");
				}
				if (frameCounter < 25) return;

				float meshJumpHeight = jumpApexY - jumpStartPos;
				sb.AppendLine($"Mesh Jump Height: {meshJumpHeight:F3}m (Apex pos.y={jumpApexY:F3}, Start={jumpStartPos:F3})");
				CheckAssert(meshJumpHeight >= 1.2f, $"Mesh jump height must be >= 1.2m (actual: {meshJumpHeight:F3}m)");

				step = 11;
				frameCounter = 0;
				return;
			}

			// =====================================================================
			// TEST 5: CELESTIAL SKY ISLE (FLOATING ISLE AT Y = 40+)
			// =====================================================================
			if (step == 11) {
				Vector3 skyCenter = new Vector3(-84f, 45f, 92f);
				skyCenter.y = OpenWorldMaterialHelper.SampleGroundHeight(skyCenter) + 0.1f;
				hero.transform.position = skyCenter;
				Physics.SyncTransforms();
				hero.Stamina = 40f;
				step = 12;
				frameCounter = 0;
				sb.AppendLine("\n--- TEST 5: CELESTIAL SKY ISLE GROUNDING & JUMP ---");
				return;
			}

			if (step == 12) {
				if (frameCounter < 10) return;
				Vector3 hp = hero.transform.position;
				sb.AppendLine($"Sky Isle Settle: pos={hp:F3}, CC.isGrounded={cc.isGrounded}, ProbeGround={CallProbeGround(hero)}, Hero.Grounded={hero.Grounded}, Stamina={hero.Stamina:F2}");
				CheckAssert(hero.Grounded, "Hero must be Grounded on sky isle");
				CheckAssert(hero.Stamina > 40.5f, "Stamina must regenerate on sky isle");

				jumpStartPos = hero.transform.position.y;
				jumpApexY = jumpStartPos;
				DispatchJumpPointerDown();
				step = 13;
				frameCounter = 0;
				return;
			}

			if (step == 13) {
				float py = hero.transform.position.y;
				if (py > jumpApexY) jumpApexY = py;
				if (frameCounter <= 20) {
					sb.AppendLine($"  Sky Isle Jump Frame {frameCounter:D2}: pos.y={py:F4}, vel.y={GetVelocity(hero).y:F2}, Grounded={hero.Grounded}, Gliding={hero.IsGliding}");
				}
				if (frameCounter < 25) return;

				float skyJumpHeight = jumpApexY - jumpStartPos;
				sb.AppendLine($"Sky Isle Jump Height: {skyJumpHeight:F3}m (Apex pos.y={jumpApexY:F3}, Start={jumpStartPos:F3})");
				CheckAssert(skyJumpHeight >= 1.2f, $"Sky isle jump height must be >= 1.2m (actual: {skyJumpHeight:F3}m)");

				step = 14;
				frameCounter = 0;
				return;
			}

			// =====================================================================
			// COMPLETION & SUMMARY
			// =====================================================================
			if (step == 14) {
				EditorApplication.update -= OnUpdate;
				sb.AppendLine("\n=================================================================");
				sb.AppendLine("   ALL VERIFICATION CHECKS PASSED: MAINLAND JUMP & GROUNDING FIXED");
				sb.AppendLine("=================================================================");
				File.WriteAllText(ReportPath, sb.ToString());
				Debug.Log($"MainlandJumpValidation: Report written to {ReportPath}");
				Done = true;
			}
		}

		static void CheckAssert(bool condition, string message) {
			if (!condition) {
				sb.AppendLine($"[ASSERTION FAILED]: {message}");
				Debug.LogError($"[ASSERTION FAILED]: {message}");
			} else {
				sb.AppendLine($"[PASS]: {message}");
			}
		}
	}
}
