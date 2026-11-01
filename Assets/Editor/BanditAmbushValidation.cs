using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
	public static class BanditAmbushValidation {
		public static bool Busy { get; private set; }
		static string reportPath;

		static void Log(string msg) {
			if (!string.IsNullOrEmpty(reportPath)) {
				File.AppendAllText(reportPath, msg + "\n");
			}
			Debug.Log("[AmbushValidation] " + msg);
		}

		static void Assert(bool condition, string msg) {
			if (!condition) {
				Log("FAIL: " + msg);
				throw new Exception("Assertion failed: " + msg);
			}
			Log("PASS: " + msg);
		}

		public class Runner : MonoBehaviour { }

		public static void RunAll() => Run();
		public static void Run() {
			reportPath = "Docs/QA/BANDIT_AMBUSH_INTERACTION_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
			Directory.CreateDirectory("Docs/QA");
			File.WriteAllText(reportPath, "Bandit Ambush Interaction Correctness Validation " + DateTime.Now + "\n\n");
			Busy = true;
			try {
				var go = new GameObject("BanditAmbushValidationRunner");
				var runner = go.AddComponent<Runner>();
				runner.StartCoroutine(RunSuite(go));
			} catch (Exception ex) {
				Log("FATAL: Failed to start validation runner: " + ex);
				Busy = false;
			}
		}

		static Button GetHudInteractButton() {
			var hud = OpenWorldMobileHud.Instance;
			if (!hud) return null;
			var field = typeof(OpenWorldMobileHud).GetField("interact", BindingFlags.Instance | BindingFlags.NonPublic);
			return field != null ? field.GetValue(hud) as Button : null;
		}

		static Text GetHudInteractLabel() {
			var hud = OpenWorldMobileHud.Instance;
			if (!hud) return null;
			var field = typeof(OpenWorldMobileHud).GetField("interactLabel", BindingFlags.Instance | BindingFlags.NonPublic);
			return field != null ? field.GetValue(hud) as Text : null;
		}

		static IEnumerator RunSuite(GameObject runnerGo) {
			OpenWorldManager gm = null;
			OpenWorldHero hero = null;
			HashSet<string> oldCollected = null;
			int oldGold = 0;
			int oldGems = 0;
			Vector3 oldPos = Vector3.zero;

			try {
				float timeout = 30f;
				float start = Time.realtimeSinceStartup;
				while ((OpenWorldManager.I == null || OpenWorldManager.I.Hero == null || OpenWorldManager.I.ActiveTerrain == null) &&
				       Time.realtimeSinceStartup - start < timeout) {
					yield return null;
				}

				gm = OpenWorldManager.I;
				if (!gm || !gm.Hero) {
					Log("FAIL: OpenWorldManager or Hero missing.");
					yield break;
				}
				hero = gm.Hero;
				var terrain = gm.ActiveTerrain;
				Assert(terrain != null, "ActiveTerrain is present");

				oldCollected = new HashSet<string>(gm.CollectedWorldIds);
				oldGold = gm.Gold;
				oldGems = gm.Gems;
				oldPos = hero.transform.position;

				// Clean test state
				gm.CollectedWorldIds.Remove(OpenWorldBanditAmbush.CacheWorldId);
				gm.CollectedWorldIds.Remove(OpenWorldBanditAmbush.ClearedWorldId);
				gm.CollectedWorldIds.Remove(OpenWorldBanditAmbush.ManifestWorldId);

				yield return new WaitForSeconds(0.4f);

				Vector3 anchor = OpenWorldBanditAmbush.RoadsideAnchor;
				var ambush = OpenWorldBanditAmbush.Instance;
				Assert(ambush != null, "OpenWorldBanditAmbush instance acquired");
				if (!ambush.EncounterActive) ambush.Rebuild();
				Assert(ambush.EncounterActive, "Bandit encounter built");

				// 1. Mobile HUD Interact Button Delivery Test
				Log("--- 1. Testing Mobile HUD Button Presence & Delivery ---");
				var hudBtn = GetHudInteractButton();
				var hudLabel = GetHudInteractLabel();
				Assert(hudBtn != null && hudLabel != null, "Mobile HUD interact button and label resolved via reflection");

				// 2. Solid Cover Rejection Check
				Log("--- 2. Testing Solid Cover Rejection ---");
				Vector3 nearCacheForCover = anchor + new Vector3(0.2f, 0f, 1.2f);
				nearCacheForCover.y = terrain.SampleHeight(nearCacheForCover) + terrain.transform.position.y;
				hero.WarpTo(nearCacheForCover);
				yield return new WaitForSeconds(0.35f);

				Assert(ambush.GetNearestValidTarget() == OpenWorldBanditAmbush.AmbushTarget.Cache, "Cache is targeted when unobstructed");

				// Place a solid barrier directly between hero and cache
				var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
				barrier.name = "TestSolidCoverBarrier";
				barrier.transform.position = (hero.transform.position + ambush.CacheObject.transform.position) * 0.5f + Vector3.up * 0.5f;
				barrier.transform.localScale = new Vector3(2f, 2f, 2f);
				yield return new WaitForSeconds(0.15f);

				Assert(!ambush.HasClearLineOfSight(ambush.CacheObject.transform.position, ambush.CacheObject.transform), "HasClearLineOfSight returns false when blocked by solid cover");
				Assert(ambush.GetNearestValidTarget() != OpenWorldBanditAmbush.AmbushTarget.Cache, "Solid cover blocks line of sight; cache is rejected");

				barrier.SetActive(false);
				UnityEngine.Object.Destroy(barrier);
				Physics.SyncTransforms();
				yield return new WaitForSeconds(0.2f);
				Assert(ambush.GetNearestValidTarget() == OpenWorldBanditAmbush.AmbushTarget.Cache, "Cache targeting restored after solid cover is removed");

				// 3. Independent Reachability & Nearest-Valid-Target Choice in Overlap Zone
				Log("--- 3. Testing Shared Nearest-Valid-Target Resolution & Overlap Range ---");
				// Warp to overlap zone where both note and cache are in range, but note is closer:
				// Note is at anchor + (-2.6, 0, 0.35); Cache is at anchor + (0.2, 0, 2.2)
				// At (-1.0, 0, 0.0): distance to note is 1.63m (<2.8m), distance to cache is 2.50m (<3.0m). Both valid; Note is closer.
				Vector3 nearNoteInOverlap = anchor + new Vector3(-1.0f, 0f, 0.0f);
				nearNoteInOverlap.y = terrain.SampleHeight(nearNoteInOverlap) + terrain.transform.position.y;
				hero.WarpTo(nearNoteInOverlap);
				yield return new WaitForSeconds(0.35f);

				Assert(ambush.GetNearestValidTarget() == OpenWorldBanditAmbush.AmbushTarget.Note, "Note selected as nearest valid target in overlap zone");
				Assert(gm.InteractionPrompt == "INSPECT MANIFEST", $"InteractionPrompt is 'INSPECT MANIFEST' (got '{gm.InteractionPrompt}')");
				Assert(hudBtn.gameObject.activeInHierarchy, "HUD interact button active near manifest");
				Assert(hudLabel.text == "INSPECT MANIFEST", $"HUD button label matches prompt ('{hudLabel.text}')");

				// Step towards cache in overlap zone:
				// At (-0.4, 0, 1.2): distance to cache is 1.17m (<3.0m), distance to note is 2.36m (<2.8m). Both valid; Cache is closer.
				Vector3 nearCacheInOverlap = anchor + new Vector3(-0.4f, 0f, 1.2f);
				nearCacheInOverlap.y = terrain.SampleHeight(nearCacheInOverlap) + terrain.transform.position.y;
				hero.WarpTo(nearCacheInOverlap);
				yield return new WaitForSeconds(0.35f);

				float dCache = Vector2.Distance(new Vector2(hero.transform.position.x, hero.transform.position.z), new Vector2(ambush.CacheObject.transform.position.x, ambush.CacheObject.transform.position.z));
				float dNote = Vector2.Distance(new Vector2(hero.transform.position.x, hero.transform.position.z), new Vector2(ambush.NoteObject.transform.position.x, ambush.NoteObject.transform.position.z));
				Log($"[Overlap Debug] dCache={dCache:F2}m, dNote={dNote:F2}m, target={ambush.GetNearestValidTarget()}");

				Assert(ambush.GetNearestValidTarget() == OpenWorldBanditAmbush.AmbushTarget.Cache, $"Cache selected as nearest valid target when closer in overlap zone (target={ambush.GetNearestValidTarget()})");
				Assert(gm.InteractionPrompt == "LOCKED CACHE", $"InteractionPrompt is 'LOCKED CACHE' (got '{gm.InteractionPrompt}')");
				Assert(hudLabel.text == "LOCKED CACHE", $"HUD button label matches 'LOCKED CACHE' ('{hudLabel.text}')");

				// 4. Locked State Mobile HUD Event Delivery & No-Fallthrough Check
				Log("--- 4. Testing Locked Cache Mobile HUD Click & No-Fallthrough ---");
				int goldBeforeLocked = gm.Gold;
				hudBtn.onClick.Invoke(); // Click actual HUD button
				yield return new WaitForSeconds(0.25f);

				Assert(gm.Gold == goldBeforeLocked, "Locked cache click awards zero gold");
				Assert(!gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.CacheWorldId), "CacheWorldId not marked while locked");
				Assert(!ambush.ManifestRead, "Locked cache interaction did NOT fall through to manifest note");

				// 5. Manifest Reading & Rereading via Mobile HUD Event Delivery
				Log("--- 5. Testing Trade Manifest Read & Reread via Mobile HUD Button ---");
				Vector3 noteReadingStance = anchor + new Vector3(-2.6f, 0f, -0.8f);
				noteReadingStance.y = terrain.SampleHeight(noteReadingStance) + terrain.transform.position.y;
				hero.WarpTo(noteReadingStance);
				yield return new WaitForSeconds(0.35f);

				Assert(hudLabel.text == "INSPECT MANIFEST", "HUD button label is 'INSPECT MANIFEST'");
				hudBtn.onClick.Invoke(); // Click actual HUD button to read note
				yield return new WaitForSeconds(0.25f);

				Assert(ambush.ManifestRead, "Manifest marked as read");
				Assert(gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.ManifestWorldId), "ManifestWorldId saved in CollectedWorldIds");
				Assert(gm.Gold == goldBeforeLocked, "Reading manifest awards no currency");
				Assert(gm.InteractionPrompt == "READ MANIFEST", $"Prompt updated to 'READ MANIFEST' (got '{gm.InteractionPrompt}')");

				// Re-read manifest via HUD button click
				hudBtn.onClick.Invoke();
				yield return new WaitForSeconds(0.25f);
				Assert(gm.Gold == goldBeforeLocked, "Rereading manifest produces no currency and does not double-process");

				// 6. Combat & Missing/Destroyed Enemy Reference Refusing Unlock
				Log("--- 6. Testing Combat & Missing Enemy Reference Safety ---");
				Assert(ambush.Bandit1 != null && ambush.Bandit2 != null, "Both bandits present");

				// Defeat Bandit 1
				ambush.Bandit1.TakeHit(100f, hero.transform.position, true);
				yield return new WaitForSeconds(0.3f);
				Assert(ambush.Bandit1.Dead, "Bandit 1 confirmed defeated");
				Assert(!ambush.CacheUnlocked, "Cache remains locked with 1 bandit alive");
				Assert(!gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.ClearedWorldId), "ClearedWorldId not saved");

				// Test Destroyed/Missing Enemy Reference:
				// Temporarily null the bandit2 reference to simulate destruction without confirmation
				var b2Field = typeof(OpenWorldBanditAmbush).GetField("bandit2", BindingFlags.Instance | BindingFlags.NonPublic);
				var realB2 = ambush.Bandit2;
				b2Field.SetValue(ambush, null);

				Assert(!ambush.CacheUnlocked, "Missing/destroyed enemy reference does NOT unlock cache");
				Assert(!gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.ClearedWorldId), "Missing reference does NOT write ClearedWorldId");

				// Restore real bandit2 reference and confirm defeat
				b2Field.SetValue(ambush, realB2);
				realB2.TakeHit(100f, hero.transform.position, true);
				yield return new WaitForSeconds(0.35f);

				Assert(realB2.Dead, "Bandit 2 confirmed defeated");
				Assert(ambush.CacheUnlocked, "Cache UNLOCKED after both bandits confirmed defeated");
				Assert(gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.ClearedWorldId), "ClearedWorldId saved IMMEDIATELY upon both confirmed dead");
				Assert(!gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.CacheWorldId), "CacheWorldId NOT marked yet (unlooted)");

				// 7. Streaming: Leave without Looting -> Return
				Log("--- 7. Testing Cleared-Unlooted Streaming (Leave -> Return) ---");
				hero.WarpTo(new Vector3(-50f, 0f, 50f)); // >140m
				yield return new WaitForSeconds(0.45f);
				Assert(ambush.Bandit1 == null && ambush.Bandit2 == null, "Enemies torn down when hero left the area");

				// Return to cache
				hero.WarpTo(nearCacheInOverlap);
				yield return new WaitForSeconds(0.5f);

				Assert(ambush.EncounterCleared, "Encounter remains cleared upon returning");
				Assert(!ambush.CacheLooted, "Cache remains unlooted");
				Assert(ambush.Bandit1 == null && ambush.Bandit2 == null, "Defeated bandits did NOT respawn");
				Assert(ambush.CacheUnlocked, "Cache remains UNLOCKED on return");
				Assert(gm.InteractionPrompt == "OPEN CACHE", $"Prompt is 'OPEN CACHE' on return (got '{gm.InteractionPrompt}')");
				Assert(hudLabel.text == "OPEN CACHE", $"HUD button label is 'OPEN CACHE' ('{hudLabel.text}')");

				// 8. Mobile HUD Button Payout & Exactly-Once Guarantee
				Log("--- 8. Testing Reward Payout via Mobile HUD Button Click ---");
				int goldAtUnlock = gm.Gold;
				int gemsAtUnlock = gm.Gems;

				hudBtn.onClick.Invoke(); // Click HUD button to claim reward
				yield return new WaitForSeconds(0.3f);

				Assert(ambush.CacheLooted, "Cache marked looted");
				Assert(gm.Gold == goldAtUnlock + 45, $"Gold awarded correctly (+45, now {gm.Gold})");
				Assert(gm.Gems == gemsAtUnlock + 2, $"Gems awarded correctly (+2, now {gm.Gems})");
				Assert(gm.CollectedWorldIds.Contains(OpenWorldBanditAmbush.CacheWorldId), "CacheWorldId saved in CollectedWorldIds");

				// Repeat click on looted cache
				hudBtn.onClick.Invoke();
				yield return new WaitForSeconds(0.2f);
				Assert(gm.Gold == goldAtUnlock + 45 && gm.Gems == gemsAtUnlock + 2, "Gold and gems unchanged on repeat click (exactly-once payout)");

				// 9. Isolated Disk Reload Verification
				Log("--- 9. Testing Isolated Disk Reload Verification ---");
				string tempSaveDir = Path.Combine(Application.temporaryCachePath, "AmbushDiskReloadTest_" + Guid.NewGuid().ToString("N"));
				try {
					var isolatedStore = new OpenWorldSaveStore(tempSaveDir);

					// A: Test Cleared-Unlooted state via disk save & load
					var testSnapshotCleared = gm.CaptureAdventure();
					// Ensure test snapshot contains cleared but not cache
					var collList = new List<string>(testSnapshotCleared.collected);
					collList.Remove(OpenWorldBanditAmbush.CacheWorldId);
					if (!collList.Contains(OpenWorldBanditAmbush.ClearedWorldId)) collList.Add(OpenWorldBanditAmbush.ClearedWorldId);
					testSnapshotCleared.collected = collList.ToArray();

					isolatedStore.Write(testSnapshotCleared); // Disk write with SHA256 envelope
					var loadedCleared = isolatedStore.Load(); // Disk read with hash verification

					Assert(loadedCleared != null, "Disk load returned valid snapshot");
					Assert(Array.IndexOf(loadedCleared.collected, OpenWorldBanditAmbush.ClearedWorldId) >= 0, "Disk-loaded data confirms ClearedWorldId");
					Assert(Array.IndexOf(loadedCleared.collected, OpenWorldBanditAmbush.CacheWorldId) < 0, "Disk-loaded data confirms Cache is unlooted");

					// B: Test Looted state via disk save & load
					var testSnapshotLooted = gm.CaptureAdventure();
					isolatedStore.Write(testSnapshotLooted);
					var loadedLooted = isolatedStore.Load();

					Assert(loadedLooted != null, "Disk load returned valid looted snapshot");
					Assert(Array.IndexOf(loadedLooted.collected, OpenWorldBanditAmbush.ClearedWorldId) >= 0, "Disk-loaded data confirms ClearedWorldId in looted state");
					Assert(Array.IndexOf(loadedLooted.collected, OpenWorldBanditAmbush.CacheWorldId) >= 0, "Disk-loaded data confirms CacheWorldId in looted state");
					Assert(Array.IndexOf(loadedLooted.collected, OpenWorldBanditAmbush.ManifestWorldId) >= 0, "Disk-loaded data confirms ManifestWorldId in looted state");
					Log("PASS: Isolated disk save & load verified successfully without touching user save slots");
				} finally {
					if (Directory.Exists(tempSaveDir)) {
						try { Directory.Delete(tempSaveDir, true); } catch { }
					}
				}

				// 10. Position camera for verification screenshot
				if (gm.CameraRig) {
					hero.WarpTo(anchor + new Vector3(-0.5f, 0f, -4.5f));
					yield return new WaitForSeconds(0.3f);
					gm.CameraRig.Yaw = 18f;
					gm.CameraRig.Pitch = 12f;
					gm.CameraRig.SnapToTarget();
				}

				Log("\nALL INTERACTION CORRECTNESS CHECKS PASSED SUCCESSFULLY!");
			} finally {
				Busy = false;
				if (runnerGo) UnityEngine.Object.Destroy(runnerGo);
				if (gm) {
					if (oldCollected != null) {
						gm.CollectedWorldIds.Clear();
						gm.CollectedWorldIds.UnionWith(oldCollected);
					}
					gm.Gold = oldGold;
					gm.Gems = oldGems;
				}
				if (hero) hero.WarpTo(oldPos);
			}
		}
	}
}
