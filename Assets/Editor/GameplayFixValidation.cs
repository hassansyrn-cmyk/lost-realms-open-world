using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
	public static class GameplayFixValidation {
		public static bool Busy { get; private set; }
		static string reportPath;

		static void Log(string msg) {
			if (!string.IsNullOrEmpty(reportPath)) {
				File.AppendAllText(reportPath, msg + "\n");
			}
			Debug.Log("[FixValidation] " + msg);
		}

		static void Assert(bool condition, string msg) {
			if (!condition) {
				Log("FAIL: " + msg);
				throw new Exception("Assertion failed: " + msg);
			}
			Log("PASS: " + msg);
		}

		public class Runner : MonoBehaviour { }

		public static void Run() {
			reportPath = "Docs/QA/GAMEPLAY_FIX_VALIDATION_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
			Directory.CreateDirectory("Docs/QA");
			File.WriteAllText(reportPath, "Gameplay Fix Validation " + DateTime.Now + "\n\n");
			Busy = true;
			try {
				var go = new GameObject("GameplayFixValidationRunner");
				var runner = go.AddComponent<Runner>();
				runner.StartCoroutine(RunSuite(go));
			} catch (Exception ex) {
				Log("FATAL: Failed to start validation runner: " + ex);
				Busy = false;
			}
		}

		static IEnumerator RunSuite(GameObject runnerGo) {
			try {
				float timeout = 15f;
				float start = Time.realtimeSinceStartup;
				while ((OpenWorldManager.I == null || OpenWorldManager.I.Hero == null || OpenWorldManager.I.ActiveTerrain == null) &&
				       Time.realtimeSinceStartup - start < timeout) {
					yield return null;
				}

				var gm = OpenWorldManager.I;
				if (!gm || !gm.Hero) {
					Log("FAIL: OpenWorldManager or Hero missing.");
					yield break;
				}
				var hero = gm.Hero;
				var terrain = gm.ActiveTerrain;
				Assert(terrain != null, "ActiveTerrain is present");

				// Settle into terrain
				yield return new WaitForSeconds(0.5f);

				// 1. Aster Vertical Stability Check
				Log("--- 1. Testing Aster Vertical Stability ---");
				hero.WarpTo(new Vector3(0f, 0f, 0f));
				yield return new WaitForSeconds(0.5f);

				float maxJitter = 0f;
				float lastY = hero.transform.position.y;
				for (int i = 0; i < 30; i++) {
					yield return null;
					float currentY = hero.transform.position.y;
					float delta = Mathf.Abs(currentY - lastY);
					if (delta > maxJitter) maxJitter = delta;
					lastY = currentY;
					float ground = OpenWorldMaterialHelper.SampleGroundHeight(hero.transform.position, hero.transform);
					Assert(currentY >= ground - 0.05f, $"Hero Y ({currentY:F3}) is at or above ground ({ground:F3})");
				}
				Assert(maxJitter < 0.03f, $"Hero standing vertical jitter ({maxJitter:F4}m) is smooth and under 0.03m (no 9cm popping oscillation)");

				// 2. Touch Jump Responsiveness Check
				Log("--- 2. Testing Touch Jump Responsiveness ---");
				hero.TouchJump = true;
				yield return null;
				yield return null;
				Assert(!hero.Grounded || hero.Visual?.CurrentState == "jump" || hero.Visual?.CurrentState == "land",
					$"Touch Jump triggered takeoff/jump animation successfully (state: {hero.Visual?.CurrentState})");

				while (!hero.Grounded) yield return null;
				yield return new WaitForSeconds(0.2f);

				// 3. Horse Mounting Grounding & Trapping Check
				Log("--- 3. Testing Horse Mounting, Grounding & Trapping ---");
				OpenWorldAnimalInteraction horse = null;
				foreach (var a in UnityEngine.Object.FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
					if (a.Horse) { horse = a; break; }
				}
				if (!horse) {
					var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/wildlife_a868c1dd18.prefab");
					Assert(prefab != null, "Horse prefab found");
					var root = new GameObject("Validation Horse");
					Vector3 p = hero.transform.position + hero.transform.forward * 3f;
					p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
					root.transform.position = p;
					var model = UnityEngine.Object.Instantiate(prefab, root.transform, false);
					var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
					model.transform.localScale *= 1.75f / b.size.y;
					b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
					model.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
					root.AddComponent<OpenWorldWildlife>();
					horse = OpenWorldAnimalInteraction.Attach(root, "Horse", "horse:qa-fix");
					yield return null;
				}

				Assert(horse != null, "Horse instance acquired");
				hero.WarpTo(horse.transform.position + horse.transform.right * 1.5f);
				yield return new WaitForSeconds(0.2f);

				hero.EnterMount(horse);
				Assert(hero.IsMounted, "Hero successfully mounted horse");

				// Step mounted motion over 45 frames
				for (int frame = 0; frame < 45; frame++) {
					hero.TouchMoveInput = Vector2.up * 0.5f;
					yield return null;

					float horseGround = terrain.SampleHeight(horse.transform.position) + terrain.transform.position.y;
					Assert(horse.transform.position.y >= horseGround - 0.05f,
						$"Frame {frame}: Horse Y ({horse.transform.position.y:F3}) stayed at/above ground ({horseGround:F3})");

					float heroY = hero.transform.position.y;
					Assert(heroY >= horseGround,
						$"Frame {frame}: Hero Y ({heroY:F3}) stayed seated above ground ({horseGround:F3})");
				}

				// 4. Dismount Clearance & Safe Recovery Check
				Log("--- 4. Testing Dismount Point & Safe Recovery ---");
				bool hasDismountPoint = horse.DismountPoint(hero, out Vector3 dismountTarget);
				Assert(hasDismountPoint, $"DismountPoint returned true with target {dismountTarget}");
				float targetGround = terrain.SampleHeight(dismountTarget) + terrain.transform.position.y;
				Assert(dismountTarget.y >= targetGround - 0.05f, $"Dismount point Y ({dismountTarget.y:F3}) is on clear terrain ({targetGround:F3})");

				hero.LeaveMount(true);
				yield return null;
				Assert(!hero.IsMounted, "Hero successfully dismounted without being trapped");
				Assert(hero.Controller.enabled, "Hero Controller re-enabled after dismount");
				float finalGround = terrain.SampleHeight(hero.transform.position) + terrain.transform.position.y;
				Assert(hero.transform.position.y >= finalGround - 0.05f, $"Hero final position ({hero.transform.position.y:F3}) is safe on ground ({finalGround:F3})");

				Log("\nALL 4 SYMPTOMS VERIFIED FIXED SUCCESSFULLY!");
			} finally {
				Busy = false;
				if (runnerGo) UnityEngine.Object.Destroy(runnerGo);
			}
		}
	}
}
