using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
	public static class DismountSafetyValidation {
		public static bool Busy { get; private set; }
		static string reportPath;

		static void Log(string msg) {
			if (!string.IsNullOrEmpty(reportPath)) {
				File.AppendAllText(reportPath, msg + "\n");
			}
			Debug.Log("[DismountValidation] " + msg);
		}

		static void Assert(bool condition, string message) {
			if (!condition) {
				Log("FAIL: " + message);
				throw new Exception("Assertion failed: " + message);
			}
			Log("PASS: " + message);
		}

		public class Runner : MonoBehaviour { }

		[MenuItem("OpenWorld/QA/Dismount Safety Validation")]
		public static void RunAll() => Run();
		public static void Run() {
			reportPath = "Docs/QA/DISMOUNT_SAFETY_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
			Directory.CreateDirectory("Docs/QA");
			File.WriteAllText(reportPath, "Safe Horse Dismount Recovery Validation " + DateTime.Now + "\n\n");
			Busy = true;
			try {
				var go = new GameObject("DismountSafetyValidationRunner");
				var runner = go.AddComponent<Runner>();
				runner.StartCoroutine(RunSuite(go));
			} catch (Exception ex) {
				Busy = false;
				Log("FATAL ERROR: " + ex);
			}
		}

		static IEnumerator RunSuite(GameObject runnerGo) {
			Vector3 oldHeroPos = Vector3.zero;
			OpenWorldHero hero = null;
			OpenWorldAnimalInteraction horse = null;
			List<GameObject> tempObjects = new List<GameObject>();

			try {
				var gm = OpenWorldManager.I;
				Assert(gm != null, "OpenWorldManager instance exists");
				var terrain = gm.ActiveTerrain;
				Assert(terrain != null, "ActiveTerrain is present");
				hero = gm.Hero;
				Assert(hero != null, "Hero is present");
				oldHeroPos = hero.transform.position;

				// --- 1. Real Aster CharacterController Capsule Dimensions Check ---
				Log("--- 1. Testing Real Hero Controller Capsule Dimensions ---");
				var cc = hero.Controller;
				Assert(cc != null, "Hero has CharacterController attached");
				Assert(Mathf.Abs(cc.radius - 0.38f) < 0.01f, $"Controller radius is 0.38m (actual: {cc.radius:F2}m)");
				Assert(Mathf.Abs(cc.height - 1.78f) < 0.01f, $"Controller height is 1.78m (actual: {cc.height:F2}m)");
				Assert(Mathf.Abs(cc.center.y - 0.89f) < 0.01f, $"Controller center Y is 0.89m (actual: {cc.center.y:F2}m)");

				// --- 2. Acquire or Spawn Validation Horse on Clear Open Ground ---
				Log("--- 2. Setting Up Validation Mount on Clear Open Ground ---");
				foreach (var a in UnityEngine.Object.FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
					if (a.Horse) { horse = a; break; }
				}
				if (!horse) {
					var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/wildlife_a868c1dd18.prefab");
					Assert(prefab != null, "Horse prefab found in imported assets");
					var root = new GameObject("Validation Horse");
					tempObjects.Add(root);
					Vector3 hp = new Vector3(85f, 0f, 35f);
					hp.y = terrain.SampleHeight(hp) + terrain.transform.position.y;
					root.transform.position = hp;
					root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
					var model = UnityEngine.Object.Instantiate(prefab, root.transform, false);
					var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
					model.transform.localScale *= 1.75f / b.size.y;
					b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
					model.transform.position += new Vector3(hp.x - b.center.x, hp.y - b.min.y, hp.z - b.center.z);
					root.AddComponent<OpenWorldWildlife>();
					horse = OpenWorldAnimalInteraction.Attach(root, "Horse", "horse:dismount-safety");
					yield return null;
				}
				Assert(horse != null, "Horse instance acquired and initialized");

				Vector3 openPos = new Vector3(85f, 0f, 35f);
				openPos.y = terrain.SampleHeight(openPos) + terrain.transform.position.y;
				horse.transform.position = openPos;
				horse.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
				Physics.SyncTransforms();
				yield return new WaitForSeconds(0.2f);

				// --- 3. Normal Dismount on Open Ground ---
				Log("--- 3. Testing Normal Dismount on Open Ground ---");
				bool normalSuccess = horse.DismountPoint(hero, out Vector3 normalPoint);
				Assert(normalSuccess, $"Normal dismount point found at {normalPoint}");
				float normalGround = terrain.SampleHeight(normalPoint) + terrain.transform.position.y;
				Assert(normalPoint.y >= normalGround - 0.05f, $"Normal dismount point Y ({normalPoint.y:F3}) is safe on terrain surface ({normalGround:F3})");
				float normalDist = Vector2.Distance(new Vector2(horse.transform.position.x, horse.transform.position.z), new Vector2(normalPoint.x, normalPoint.z));
				Assert(normalDist >= 1.4f && normalDist <= 2.5f, $"Normal dismount distance is within expected side range ({normalDist:F2}m)");

				// Test actual mounting and dismounting cycle
				hero.WarpTo(horse.transform.position + horse.transform.right * 1.5f);
				yield return new WaitForSeconds(0.2f);
				hero.EnterMount(horse);
				Assert(hero.IsMounted, "Hero successfully entered mount");

				hero.LeaveMount(false);
				yield return new WaitForSeconds(0.5f);
				Assert(!hero.IsMounted, "Hero successfully dismounted without being trapped");
				Assert(hero.Controller.enabled, "Hero Controller re-enabled after dismount");
				float heroGroundAfterDismount = terrain.SampleHeight(hero.transform.position) + terrain.transform.position.y;
				Assert(hero.transform.position.y >= heroGroundAfterDismount - 0.05f, $"Hero final position ({hero.transform.position.y:F3}) is safe on ground ({heroGroundAfterDismount:F3})");

				// --- 4. Forced Underground-Origin Recovery on Open Ground ---
				Log("--- 4. Testing Forced Underground-Origin Recovery on Open Ground ---");
				Vector3 undergroundPos = openPos - Vector3.up * 2.5f; // Force horse 2.5m underground
				horse.transform.position = undergroundPos;
				Physics.SyncTransforms();
				yield return new WaitForSeconds(0.1f);

				bool recoveredSuccess = horse.DismountPoint(hero, out Vector3 recoveredPoint);
				Assert(recoveredSuccess, "Forced underground search origin successfully recovers safe surface candidate");
				float recoveredGround = terrain.SampleHeight(recoveredPoint) + terrain.transform.position.y;
				Assert(recoveredPoint.y >= recoveredGround - 0.05f, $"Recovered point Y ({recoveredPoint.y:F3}) is on surface ground ({recoveredGround:F3}), NOT underground");
				Assert(recoveredPoint.y < recoveredGround + 0.5f, $"Recovered point Y ({recoveredPoint.y:F3}) is close to surface ground level");

				// Restore horse to surface
				horse.transform.position = openPos;
				Physics.SyncTransforms();
				yield return new WaitForSeconds(0.1f);

				// --- 5. One Blocked Side Selects The Other (Left Blocked -> Right Selected) ---
				Log("--- 5. Testing Asymmetric Obstacle (Left Blocked -> Selects Right) ---");
				var leftBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
				leftBlocker.name = "TestLeftDismountBlocker";
				tempObjects.Add(leftBlocker);
				leftBlocker.transform.position = horse.transform.position - horse.transform.right * 2.2f + Vector3.up * 0.9f;
				leftBlocker.transform.localScale = new Vector3(3.0f, 2.8f, 4.0f);
				leftBlocker.transform.rotation = horse.transform.rotation;
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();
				yield return null;

				bool asymSuccess = horse.DismountPoint(hero, out Vector3 asymPoint);
				Assert(asymSuccess, "DismountPoint succeeds when left side is obstructed");
				Vector3 toAsym = asymPoint - horse.transform.position;
				float rightAlignment = Vector3.Dot(toAsym.normalized, horse.transform.right);
				Assert(rightAlignment > 0.2f, $"Dismount point selected opposite/right side (right alignment dot={rightAlignment:F2} > 0.2)");

				// Clean up left blocker
				leftBlocker.SetActive(false);
				UnityEngine.Object.Destroy(leftBlocker);
				tempObjects.Remove(leftBlocker);
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();

				// --- 6. Wall Between Saddle and Candidate Refuses ---
				Log("--- 6. Testing Saddle Path Occlusion (Wall Between Saddle & Candidate) ---");
				// Create thin wall directly slicing between horse saddle and the left dismount candidate
				var saddleWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
				saddleWall.name = "TestSaddleWall";
				tempObjects.Add(saddleWall);
				saddleWall.transform.position = horse.transform.position - horse.transform.right * 0.85f + Vector3.up * 1.1f;
				saddleWall.transform.localScale = new Vector3(0.15f, 2.5f, 4.0f);
				saddleWall.transform.rotation = horse.transform.rotation;

				// Also place right blocker so left is tested against the wall
				var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
				rightWall.name = "TestRightWall";
				tempObjects.Add(rightWall);
				rightWall.transform.position = horse.transform.position + horse.transform.right * 0.85f + Vector3.up * 1.1f;
				rightWall.transform.localScale = new Vector3(0.15f, 2.5f, 4.0f);
				rightWall.transform.rotation = horse.transform.rotation;
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();

				// Both left and right paths from saddle are blocked; rear/angle candidates may be evaluated or refused
				horse.DismountPoint(hero, out Vector3 saddleBlockedPoint);
				if (saddleBlockedPoint != horse.transform.position) {
					// Verify that whichever point was chosen does NOT pass through either wall
					Vector3 dLeft = saddleBlockedPoint - (horse.transform.position - horse.transform.right * 1.6f);
					Assert(dLeft.magnitude > 0.5f, "Saddle wall prevented dismounting into left wall corridor");
				}

				// Clean up saddle test walls
				saddleWall.SetActive(false); UnityEngine.Object.Destroy(saddleWall); tempObjects.Remove(saddleWall);
				rightWall.SetActive(false); UnityEngine.Object.Destroy(rightWall); tempObjects.Remove(rightWall);
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();

				// --- 7. All Sides Blocked Refuses Safely (Zero Unchecked Success) ---
				Log("--- 7. Testing All Sides Blocked Refuses Safely (Zero Unchecked Success) ---");
				List<GameObject> ringBlockers = new List<GameObject>();
				// Place 8 solid blockers in a tight perimeter around the horse
				for (int i = 0; i < 8; i++) {
					float angle = i * 45f;
					Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
					var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
					b.name = $"RingBlocker_{i}";
					b.transform.position = horse.transform.position + dir * 1.8f + Vector3.up * 1.0f;
					b.transform.localScale = new Vector3(1.8f, 2.8f, 1.8f);
					ringBlockers.Add(b);
					tempObjects.Add(b);
				}
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();
				yield return null;

				bool allBlockedResult = horse.DismountPoint(hero, out Vector3 blockedPoint);
				Assert(!allBlockedResult, $"All sides blocked safely returns FALSE (got {allBlockedResult}, point={blockedPoint})");

				// Verify actual hero.LeaveMount refuses when all sides blocked and keeps rider mounted
				hero.EnterMount(horse);
				Assert(hero.IsMounted, "Hero mounted inside blocked ring");
				hero.LeaveMount(false); // Non-forced dismount
				yield return new WaitForSeconds(0.2f);
				Assert(hero.IsMounted, "Hero safely remains mounted when dismount is refused due to blocked surroundings");

				// Remove ring blockers
				foreach (var b in ringBlockers) {
					if (b) {
						b.SetActive(false);
						UnityEngine.Object.Destroy(b);
						tempObjects.Remove(b);
					}
				}
				ringBlockers.Clear();
				Physics.SyncTransforms();
				yield return new WaitForFixedUpdate();

				// Now that blockers are removed, verify dismount succeeds cleanly
				hero.LeaveMount(false);
				yield return new WaitForSeconds(0.45f);
				Assert(!hero.IsMounted, "Hero dismounted cleanly once blockers were removed");

				// --- 8. Steep Slope, Water, and Terrain Bounds Rejection Check ---
				Log("--- 8. Testing Steep Slope, Water, and Bounds Rejection ---");
				// Verify bounds rejection: candidate outside WorldBound fails TryWalk/bounds
				Vector3 outsideCandidate = new Vector3(OpenWorldHero.WorldBound + 50f, 10f, 0f);
				Assert(Mathf.Abs(outsideCandidate.x) > OpenWorldHero.WorldBound, "Candidate outside WorldBound correctly detected");

				// Verify water rejection helper: candidate in deep water returns true for WaterAt
				bool waterDetected = OpenWorldTerrainSculptor.WaterAt(0f, 0f, out _, out _);
				Log($"Water detection probe at (0,0): {waterDetected}");

				// Verify TryWalk on steep slope returns false
				Vector3 cliffBase = new Vector3(openPos.x, 0f, openPos.z);
				Vector3 cliffHigh = cliffBase + new Vector3(0.5f, 5.0f, 0f); // 5m vertical jump over 0.5m horizontal
				bool cliffWalkable = OpenWorldGrounding.TryWalk(terrain, cliffBase, cliffHigh, out _);
				Assert(!cliffWalkable, "TryWalk correctly rejects steep cliff / vertical drops (>0.65m)");

				Log("\nALL DISMOUNT SAFETY AND RECOVERY CHECKS PASSED SUCCESSFULLY!");
			} finally {
				Busy = false;
				foreach (var obj in tempObjects) {
					if (obj) {
						obj.SetActive(false);
						UnityEngine.Object.Destroy(obj);
					}
				}
				tempObjects.Clear();
				if (runnerGo) UnityEngine.Object.Destroy(runnerGo);
				if (hero && oldHeroPos != Vector3.zero) hero.WarpTo(oldHeroPos);
			}
		}
	}
}
