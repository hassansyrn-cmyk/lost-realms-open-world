using System;
using UnityEngine;

namespace LostRealmsOW {
	/// <summary>
	/// Optional roadside bandit ambush near the Verdant hunting route.
	/// Features an abandoned trade wagon, scattered supply containers, a readable trade manifest,
	/// two humanoid bandit enemies (Goblin / IronGoblin), and an expansion secret cache.
	/// Fully integrated with the mobile HUD interact prompt and Manager.Interact path.
	/// </summary>
	public sealed class OpenWorldBanditAmbush : MonoBehaviour {
		public static OpenWorldBanditAmbush Instance { get; private set; }

		// Roadside position along Kings Road East directly south of the hunting spur
		public static readonly Vector3 RoadsideAnchor = new Vector3(106f, 0f, -15.5f);
		public const string CacheWorldId = "bandit-ambush:cache";
		public const string ClearedWorldId = "bandit-ambush:cleared";
		public const string ManifestWorldId = "bandit-ambush:manifest";

		public const string ManifestText = "CARAVAN MANIFEST: Consignment #14 from Caravan Post bound for Oakvale. Stolen cargo: salt-cured rations, trade coin, and iron nails. Ambushed at the southern bend—driver fled toward Caravan Post. Beware Kings Road.";

		OpenWorldManager gm;
		GameObject cluster;
		GameObject cacheObj;
		TextMesh cacheLabel;
		GameObject noteRoot;
		TextMesh noteLabel;
		OpenWorldEnemy bandit1;
		OpenWorldEnemy bandit2;
		bool bandit1ConfirmedDead;
		bool bandit2ConfirmedDead;
		bool announcedUnlock;
		float nextCheck;

		public bool CacheLooted => gm != null && gm.CollectedWorldIds != null && gm.CollectedWorldIds.Contains(CacheWorldId);
		public bool EncounterCleared => (gm != null && gm.CollectedWorldIds != null && gm.CollectedWorldIds.Contains(ClearedWorldId)) || CacheLooted;
		public bool ManifestRead => gm != null && gm.CollectedWorldIds != null && gm.CollectedWorldIds.Contains(ManifestWorldId);

		public bool EncounterActive => !EncounterCleared && ((bandit1 && !bandit1.Dead) || (bandit2 && !bandit2.Dead));
		public bool CacheUnlocked => EncounterCleared || (!EncounterActive && bandit1ConfirmedDead && bandit2ConfirmedDead);

		public OpenWorldEnemy Bandit1 => bandit1;
		public OpenWorldEnemy Bandit2 => bandit2;
		public GameObject CacheObject => cacheObj;
		public GameObject NoteObject => noteRoot;

		public enum AmbushTarget { None, Cache, Note }

		public bool HasClearLineOfSight(Vector3 targetPos, Transform targetTransform) {
			if (!gm || !gm.Hero) return false;
			Vector3 from = gm.Hero.transform.position + Vector3.up * 1.0f;
			float targetHeightOffset = (noteRoot && targetTransform == noteRoot.transform) ? 1.1f : 0.65f;
			Vector3 to = targetPos + Vector3.up * targetHeightOffset;
			if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) {
				if (hit.transform != null &&
				    !hit.transform.IsChildOf(gm.Hero.transform) &&
				    !hit.transform.IsChildOf(targetTransform)) {
					return false;
				}
			}
			return true;
		}

		public AmbushTarget GetNearestValidTarget() {
			if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return AmbushTarget.None;

			Vector3 heroPos = gm.Hero.transform.position;
			AmbushTarget candidate = AmbushTarget.None;
			float bestDistSq = float.MaxValue;

			// 1. Evaluate Cache candidate
			if (cacheObj) {
				Vector3 diff = heroPos - cacheObj.transform.position;
				float horizDistSq = new Vector2(diff.x, diff.z).sqrMagnitude;
				if (Mathf.Abs(diff.y) <= 3.0f && horizDistSq <= 9.0f) { // within 3.0m
					if (HasClearLineOfSight(cacheObj.transform.position, cacheObj.transform)) {
						candidate = AmbushTarget.Cache;
						bestDistSq = horizDistSq;
					}
				}
			}

			// 2. Evaluate Note candidate
			if (noteRoot) {
				Vector3 diff = heroPos - noteRoot.transform.position;
				float horizDistSq = new Vector2(diff.x, diff.z).sqrMagnitude;
				if (Mathf.Abs(diff.y) <= 3.0f && horizDistSq <= 7.84f) { // within 2.8m
					if (HasClearLineOfSight(noteRoot.transform.position, noteRoot.transform)) {
						if (horizDistSq < bestDistSq) {
							candidate = AmbushTarget.Note;
							bestDistSq = horizDistSq;
						}
					}
				}
			}

			return candidate;
		}

		public bool HeroNearCache => GetNearestValidTarget() == AmbushTarget.Cache;
		public bool HeroNearNote => GetNearestValidTarget() == AmbushTarget.Note;

		/// <summary>
		/// Contextual prompt for mobile HUD and world interactions.
		/// </summary>
		public string BanditAmbushPrompt {
			get {
				var target = GetNearestValidTarget();
				if (target == AmbushTarget.Note) {
					return ManifestRead ? "READ MANIFEST" : "INSPECT MANIFEST";
				}
				if (target == AmbushTarget.Cache) {
					if (CacheLooted) return null;
					return CacheUnlocked ? "OPEN CACHE" : "LOCKED CACHE";
				}
				return null;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		static void Install() {
			if (!FindFirstObjectByType<OpenWorldBanditAmbush>() && !FindAnyObjectByType<OpenWorldBanditAmbush>()) {
				new GameObject("Bandit Roadside Ambush").AddComponent<OpenWorldBanditAmbush>();
			}
		}

		void Awake() {
			Instance = this;
		}

		void Update() {
			gm = OpenWorldManager.I;
			if (!gm || !gm.Hero || !gm.ActiveTerrain || !gm.RealmActive) return;

			float dist = Vector3.Distance(gm.Hero.transform.position, RoadsideAnchor);

			// Streaming: teardown when far away (>135m), build when near (<95m)
			if (dist > 135f) {
				if (cluster) Teardown();
				return;
			}
			if (dist < 95f && !cluster) {
				Build();
			}

			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 0.15f;

			if (!cluster) return;

			// Explicit kill verification: missing/destroyed references do NOT count as confirmed kills
			if (bandit1 && bandit1.Dead) bandit1ConfirmedDead = true;
			if (bandit2 && bandit2.Dead) bandit2ConfirmedDead = true;

			// Check unlock state: immediately persist ClearedWorldId when both bandits confirmed dead
			if (!EncounterCleared && bandit1ConfirmedDead && bandit2ConfirmedDead) {
				gm.CollectedWorldIds.Add(ClearedWorldId);
				gm.MarkProgressDirty();
				announcedUnlock = true;
				gm.ShowBanner("BANDITS DEFEATED • Supply cache unlocked!");
				OpenWorldAudio.Play("sfx_power_select", 0.5f);
				UpdateCacheLabel();
			}

			// Orient floating labels toward camera
			if (Camera.main) {
				if (cacheLabel) cacheLabel.transform.rotation = Camera.main.transform.rotation;
				if (noteLabel) noteLabel.transform.rotation = Camera.main.transform.rotation;
			}

			// Note: Keyboard KeyCode.F is polled solely by OpenWorldAdventure.TickAdventure()
			// to guarantee exactly one interaction per key press across PC and mobile.
		}

		/// <summary>
		/// Dispatches interaction from mobile HUD button or Manager.Interact path.
		/// Uses the single shared nearest-valid-target choice.
		/// </summary>
		public bool TryInteractAmbush() {
			if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return false;
			var target = GetNearestValidTarget();
			if (target == AmbushTarget.Note) {
				return TryReadManifest();
			}
			if (target == AmbushTarget.Cache) {
				TryOpenCache();
				// Preserves locked cache handling: whether locked, empty, or unlocked,
				// the interaction was directed at the cache and consumed.
				return true;
			}
			return false;
		}

		public bool TryReadManifest() {
			if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen || !HeroNearNote) return false;

			gm.ShowBanner(ManifestText);
			OpenWorldAudio.Play("sfx_menu", 0.5f);

			if (!ManifestRead) {
				gm.CollectedWorldIds.Add(ManifestWorldId);
				gm.MarkProgressDirty();
				OpenWorldQuestJournal.Instance?.Record("TRADE MANIFEST", ManifestText);
				UpdateNoteLabel();
			}
			return true;
		}

		public bool TryOpenCache() {
			if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return false;
			if (!HeroNearCache) return false;

			if (CacheLooted) {
				gm.ShowBanner("The bandit cache is empty.");
				return false;
			}

			if (!CacheUnlocked) {
				gm.ShowBanner("LOCKED • Defeat the bandits guarding the supplies!");
				OpenWorldAudio.Play("sfx_error", 0.45f);
				return false;
			}

			// Claim reward exactly once
			if (!gm.CollectedWorldIds.Add(CacheWorldId)) return false;
			gm.CollectedWorldIds.Add(ClearedWorldId);

			gm.Gold += 45;
			gm.Gems += 2;
			gm.MarkProgressDirty();

			gm.ShowBanner("BANDIT SUPPLY CACHE • 45 Gold, 2 Gems");
			OpenWorldAudio.Play("sfx_upgrade", 0.65f);
			if (cacheObj) {
				gm.SpawnBurst(cacheObj.transform.position + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.25f), 20);
			}

			OpenWorldQuestJournal.Instance?.Record("FIELD NOTE", "Recovered stolen trade cargo from the roadside ambush along Kings Road.");
			UpdateCacheLabel();
			return true;
		}

		public void Rebuild() {
			Teardown();
			Build();
		}

		void Build() {
			announcedUnlock = false;
			cluster = new GameObject("Bandit Ambush Cluster");
			cluster.transform.SetParent(gm.WorldRoot, false);

			// 1. Abandoned trade wagon (aligned along road verge contour, conformed to terrain slope)
			Vector3 wagonPos = SafeGround(RoadsideAnchor + new Vector3(-1.2f, 0f, 1.8f));
			var wagon = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_wagon_01", "Props/Textures/rpgpp_lt_tex_a", cluster.transform, wagonPos, 2.0f, Color.white, true);
			if (wagon) {
				Vector3 norm = SampleTerrainNormal(wagonPos);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				wagon.transform.rotation = slopeRot * Quaternion.Euler(0f, 96f, 0f);
				SettlePropOnGround(wagon, -0.04f);
			}

			// 2. Scattered supply containers (interrupted trade story props with collision)
			// Upright wooden barrel beside wagon's western flank
			Vector3 barrelPos = SafeGround(RoadsideAnchor + new Vector3(-3.8f, 0f, 0.7f));
			var barrel = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_barrel_01", "Props/Textures/rpgpp_lt_tex_a", cluster.transform, barrelPos, 0.95f, Color.white, true);
			if (barrel) {
				Vector3 norm = SampleTerrainNormal(barrelPos);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				barrel.transform.rotation = slopeRot * Quaternion.Euler(0f, 12f, 0f);
				SettlePropOnGround(barrel, -0.02f);
			}

			// Fallen trade sack on grass near eastern verge (resting flat on ground, conformed to slope)
			Vector3 sackPos = SafeGround(RoadsideAnchor + new Vector3(2.0f, 0f, -0.2f));
			var sack = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_sack_01", "Props/Textures/rpgpp_lt_tex_a", cluster.transform, sackPos, 0.52f, Color.white, true);
			if (sack) {
				Vector3 norm = SampleTerrainNormal(sackPos);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				sack.transform.rotation = slopeRot * Quaternion.Euler(0f, 38f, 0f);
				SettlePropOnGround(sack, -0.02f);
			}

			// Supply crate 1 dropped off wagon's eastern flank
			Vector3 cratePos1 = SafeGround(RoadsideAnchor + new Vector3(5.2f, 0f, 0.4f));
			var crate1 = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_crate_01", "Props/Textures/rpgpp_lt_tex_a", cluster.transform, cratePos1, 0.72f, Color.white, true);
			if (crate1) {
				Vector3 norm = SampleTerrainNormal(cratePos1);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				crate1.transform.rotation = slopeRot * Quaternion.Euler(0f, -16f, 0f);
				SettlePropOnGround(crate1, -0.02f);
			}

			// Supply crate 2 beside crate 1
			Vector3 cratePos2 = SafeGround(RoadsideAnchor + new Vector3(7.8f, 0f, 1.0f));
			var crate2 = OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_crate_01", "Props/Textures/rpgpp_lt_tex_a", cluster.transform, cratePos2, 0.68f, Color.white, true);
			if (crate2) {
				Vector3 norm = SampleTerrainNormal(cratePos2);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				crate2.transform.rotation = slopeRot * Quaternion.Euler(0f, 28f, 0f);
				SettlePropOnGround(crate2, -0.02f);
			}

			// 3. Physical Trade Manifest Note pinned to upright stake beside the roadside approach
			Vector3 notePos = SafeGround(RoadsideAnchor + new Vector3(-5.0f, 0f, -0.5f));
			noteRoot = new GameObject("Bandit Trade Manifest Note");
			noteRoot.transform.SetParent(cluster.transform, false);
			noteRoot.transform.position = notePos + Vector3.down * 0.02f; // Embed stake base cleanly into ground
			noteRoot.transform.rotation = Quaternion.Euler(0f, 15f, 0f); // Perfectly upright per Requirement 5
			OpenWorldTrailNote.Build(noteRoot.transform);

			var nlgo = new GameObject("NoteLabel");
			nlgo.transform.SetParent(noteRoot.transform, false);
			nlgo.transform.localPosition = Vector3.up * 1.65f;
			noteLabel = nlgo.AddComponent<TextMesh>();
			noteLabel.fontSize = 26;
			noteLabel.characterSize = 0.045f;
			noteLabel.anchor = TextAnchor.MiddleCenter;
			UpdateNoteLabel();

			// 4. Reward Cache (tucked safely behind supplies on roadside verge)
			Vector3 cachePos = SafeGround(RoadsideAnchor + new Vector3(2.2f, 0f, 3.2f));
			cacheObj = OpenWorldMaterialHelper.SpawnModel("Props/Expansion/SecretCache", "Props/Expansion/Textures/SecretCache_basecolor", cluster.transform, cachePos, 0.72f, Color.white, true);
			if (!cacheObj) {
				cacheObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
				cacheObj.name = "FallbackSecretCache";
				cacheObj.transform.SetParent(cluster.transform, false);
				cacheObj.transform.position = cachePos + Vector3.up * 0.35f;
				cacheObj.transform.localScale = new Vector3(0.9f, 0.7f, 0.6f);
				var ren = cacheObj.GetComponent<Renderer>();
				if (ren) ren.sharedMaterial = OpenWorldMaterialHelper.CreateLit("ChestFallback", new Color(0.45f, 0.3f, 0.15f));
			} else {
				Vector3 norm = SampleTerrainNormal(cachePos);
				Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, norm);
				cacheObj.transform.rotation = slopeRot * Quaternion.Euler(0f, -12f, 0f);
				SettlePropOnGround(cacheObj, -0.02f);
			}

			// Floating label above cache
			var lgo = new GameObject("CacheLabel");
			lgo.transform.SetParent(cacheObj.transform, false);
			lgo.transform.localPosition = Vector3.up * 1.15f;
			cacheLabel = lgo.AddComponent<TextMesh>();
			cacheLabel.fontSize = 28;
			cacheLabel.characterSize = 0.05f;
			cacheLabel.anchor = TextAnchor.MiddleCenter;

			UpdateCacheLabel();

			// 5. Spawn enemies with ample combat space on grass verge
			if (!EncounterCleared) {
				bandit1ConfirmedDead = false;
				bandit2ConfirmedDead = false;

				Vector3 b1Pos = SafeGround(RoadsideAnchor + new Vector3(-3.8f, 0f, 4.2f));
				bandit1 = OpenWorldEnemy.Spawn("Goblin", "Bandit Skirmisher", b1Pos, cluster.transform);
				bandit1.MaxHealth = bandit1.Health = 10f;
				bandit1.AttackDamage = 0.9f;
				bandit1.MoveSpeed = 3.6f;
				bandit1.LeashRadius = 24f;
				gm.Enemies.Add(bandit1);

				Vector3 b2Pos = SafeGround(RoadsideAnchor + new Vector3(3.6f, 0f, 4.5f));
				bandit2 = OpenWorldEnemy.Spawn("IronGoblin", "Bandit Enforcer", b2Pos, cluster.transform);
				bandit2.MaxHealth = bandit2.Health = 16f;
				bandit2.AttackDamage = 1.15f;
				bandit2.MoveSpeed = 3.2f;
				bandit2.LeashRadius = 24f;
				gm.Enemies.Add(bandit2);
			} else {
				bandit1ConfirmedDead = true;
				bandit2ConfirmedDead = true;
			}
		}

		/// <summary>
		/// Calculates accurate renderer bounds relative to the root transform in root local space.
		/// </summary>
		public Bounds GetLocalRendererBounds(Transform root) {
			if (!root) return new Bounds(Vector3.zero, Vector3.zero);
			var renderers = root.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
			Bounds localBounds = new Bounds();
			bool first = true;
			foreach (var r in renderers) {
				if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
				MeshFilter mf = r.GetComponent<MeshFilter>();
				if (mf && mf.sharedMesh) {
					Bounds mb = mf.sharedMesh.bounds;
					Vector3 min = mb.min;
					Vector3 max = mb.max;
					Vector3[] pts = new Vector3[] {
						new Vector3(min.x, min.y, min.z),
						new Vector3(max.x, min.y, min.z),
						new Vector3(min.x, max.y, min.z),
						new Vector3(max.x, max.y, min.z),
						new Vector3(min.x, min.y, max.z),
						new Vector3(max.x, min.y, max.z),
						new Vector3(min.x, max.y, max.z),
						new Vector3(max.x, max.y, max.z)
					};
					foreach (var p in pts) {
						Vector3 worldP = r.transform.TransformPoint(p);
						Vector3 rootP = root.InverseTransformPoint(worldP);
						if (first) { localBounds = new Bounds(rootP, Vector3.zero); first = false; }
						else { localBounds.Encapsulate(rootP); }
					}
				} else {
					Bounds wb = r.bounds;
					Vector3 min = wb.min;
					Vector3 max = wb.max;
					Vector3[] pts = new Vector3[] {
						new Vector3(min.x, min.y, min.z),
						new Vector3(max.x, min.y, min.z),
						new Vector3(min.x, max.y, min.z),
						new Vector3(max.x, max.y, min.z),
						new Vector3(min.x, min.y, max.z),
						new Vector3(max.x, min.y, max.z),
						new Vector3(min.x, max.y, max.z),
						new Vector3(max.x, max.y, max.z)
					};
					foreach (var p in pts) {
						Vector3 rootP = root.InverseTransformPoint(p);
						if (first) { localBounds = new Bounds(rootP, Vector3.zero); first = false; }
						else { localBounds.Encapsulate(rootP); }
					}
				}
			}
			return localBounds;
		}

		/// <summary>
		/// Settles a prop's actual renderer lower bounds onto the local terrain surface,
		/// embedding its base center into the ground while preserving slope-conformed rotation.
		/// </summary>
		public void SettlePropOnGround(GameObject prop, float embedOffset = -0.02f) {
			if (!prop) return;
			Bounds localBounds = GetLocalRendererBounds(prop.transform);
			if (localBounds.size.sqrMagnitude < 0.001f) return;

			Vector3 localBaseCenter = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
			Vector3 worldBaseCenter = prop.transform.TransformPoint(localBaseCenter);
			float terrainGroundY = SafeGround(worldBaseCenter).y;

			float deltaY = (terrainGroundY + embedOffset) - worldBaseCenter.y;
			prop.transform.position += Vector3.up * deltaY;
		}

		void UpdateCacheLabel() {
			if (!cacheLabel) return;
			if (CacheLooted) {
				cacheLabel.text = "[EMPTY]";
				cacheLabel.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
			} else if (CacheUnlocked) {
				cacheLabel.text = "BANDIT CACHE  [F]";
				cacheLabel.color = new Color(1f, 0.88f, 0.35f, 1f);
			} else {
				cacheLabel.text = "LOCKED CACHE\n[DEFEAT BANDITS]";
				cacheLabel.color = new Color(0.95f, 0.35f, 0.25f, 1f);
			}
		}

		void UpdateNoteLabel() {
			if (!noteLabel) return;
			if (ManifestRead) {
				noteLabel.text = "TRADE MANIFEST";
				noteLabel.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
			} else {
				noteLabel.text = "TRADE MANIFEST  [F]";
				noteLabel.color = new Color(1f, 0.88f, 0.35f, 1f);
			}
		}

		Vector3 SafeGround(Vector3 p) {
			if (gm && gm.ActiveTerrain) {
				p.y = gm.ActiveTerrain.SampleHeight(p) + gm.ActiveTerrain.transform.position.y;
			} else {
				p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
			}
			return p;
		}

		Vector3 SampleTerrainNormal(Vector3 worldPos) {
			if (gm && gm.ActiveTerrain && gm.ActiveTerrain.terrainData) {
				var t = gm.ActiveTerrain;
				Vector3 local = worldPos - t.transform.position;
				Vector3 size = t.terrainData.size;
				float u = Mathf.Clamp01(local.x / size.x);
				float v = Mathf.Clamp01(local.z / size.z);
				return t.terrainData.GetInterpolatedNormal(u, v);
			}
			return Vector3.up;
		}

		void Teardown() {
			if (bandit1 && gm) gm.Enemies.Remove(bandit1);
			if (bandit2 && gm) gm.Enemies.Remove(bandit2);
			if (cluster) Destroy(cluster);
			cluster = null;
			cacheObj = null;
			cacheLabel = null;
			noteRoot = null;
			noteLabel = null;
			bandit1 = null;
			bandit2 = null;
		}

		void OnDestroy() {
			Teardown();
			if (Instance == this) Instance = null;
		}
	}
}
