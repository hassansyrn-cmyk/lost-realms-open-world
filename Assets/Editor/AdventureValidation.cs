using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostRealmsOW {
 public static class AdventureValidation {
  static void Require(bool condition, string message) {
   if (!condition) throw new Exception("Adventure regression: " + message);
  }
  public static void Verify(OpenWorldManager gm) {
   Require(gm.QuestStage == OpenWorldManager.AdventureStage.FindSanctuary, "New game should begin at the sanctuary.");
   var boss = gm.Enemies.Find(e => e && e.IsBoss);
   Require(boss && !boss.gameObject.activeSelf, "Guardian is active before the seal opens.");
   gm.Hero.WarpTo(OpenWorldManager.GroveSealPosition + Vector3.back * 3);
   gm.Interact();
   Require(!gm.GuardianAwakened, "Seal can open before the quest.");
   gm.Hero.WarpTo(OpenWorldManager.SanctuaryPosition + Vector3.back * 3);
   gm.Interact();
   Require(gm.QuestStarted && gm.QuestStage == OpenWorldManager.AdventureStage.RecoverSigils, "Sanctuary does not start the quest.");

   // Verify Forgotten Sanctum elemental focus puzzle & trap disarming
   Require(gm.SanctumFoci.Count == 3 && gm.SanctumTraps.Count == 2 && !gm.SanctumCompleted, "Sanctum did not spawn foci and traps.");
   gm.Hero.WarpTo(OpenWorldManager.SanctumAltarPosition + Vector3.back * 2);
   gm.Interact();
   Require(!gm.SanctumCompleted, "Sanctum altar awakened before attuning foci.");
   foreach (var focus in gm.SanctumFoci) {
    gm.Hero.WarpTo(focus.transform.position + Vector3.back * 2);
    gm.Interact();
   }
   Require(gm.SanctumFociCount == 3 && gm.SanctumFoci.All(f => f.Attuned), "Sanctum elemental foci did not attune.");
   gm.Hero.WarpTo(OpenWorldManager.SanctumAltarPosition + Vector3.back * 2);
   gm.Interact();
   Require(gm.SanctumCompleted && gm.Hero.MaxAether == 125f && gm.UnlockedWeapons.Contains(7) && gm.SanctumTraps.All(t => t.Disarmed),
    "Sanctum completion did not award +25 Max Aether, Frost Halberd, or disarm traps.");

   // Verify World Boundary Clamping
   gm.Hero.WarpTo(new Vector3(999f, 0f, -999f));
   Require(Mathf.Abs(gm.Hero.transform.position.x) <= OpenWorldHero.WorldBound + 0.01f &&
           Mathf.Abs(gm.Hero.transform.position.z) <= OpenWorldHero.WorldBound + 0.01f,
           "World boundary clamping failed.");

   var chest = gm.Collectibles.Find(c => c && c.Kind == OWCollectibleKind.SecretChest);
   int gold = gm.Gold;
   string chestId = chest.PersistentId;
   chest.Collect(gm); chest.Collect(gm);
   Require(gm.Gold == gold + 45 && gm.CollectedWorldIds.Contains(chestId), "Chest can pay twice or has no persistent identity.");
   foreach (var key in gm.Collectibles.Where(c => c && c.Kind == OWCollectibleKind.TempleKey).ToArray()) key.Collect(gm);
   foreach (var camp in gm.Camps) {
    foreach (var guard in camp.Guards.ToArray()) guard.TakeHit(1000, gm.Hero.transform.position);
   }
   Require(gm.CampsCaptured == 3 && gm.KeysCollected == 3 && gm.QuestStage == OpenWorldManager.AdventureStage.OpenGrove,
    "Freeing outposts and finding sigils does not unlock the grove.");
   gm.Hero.WarpTo(OpenWorldManager.GroveSealPosition + Vector3.back * 3);
   gm.Interact();
   Require(gm.GuardianAwakened && boss.gameObject.activeSelf && gm.CheckpointId == "grove", "Grove does not awaken guardian/set checkpoint.");
   boss.TakeHit(1f, gm.Hero.transform.position, false, 0);
   Require(boss.AppliedElement == 0, "Ember elemental status was not applied to target.");
   boss.TakeHit(1f, gm.Hero.transform.position, false, 1);
   Require(boss.AppliedElement == -1, "Ember + Frost Melt elemental reaction did not trigger.");
   boss.TakeHit(1000, gm.Hero.transform.position);
   Require(gm.QuestStage == OpenWorldManager.AdventureStage.ReturnToSanctuary, "Boss victory does not advance quest.");
   gm.Hero.WarpTo(OpenWorldManager.SanctuaryPosition + Vector3.back * 3);
   gm.Interact();
   Require(gm.QuestCompleted && gm.Hero.MaxHealth == 6 && gm.UnlockedWeapons.Contains(8) && gm.Hero.EquippedWeaponId == 8,
    "Completion reward is missing.");
   Require(gm.RelicShrines.Count == 4 && gm.BountyElites.Count == 3, "Relic Shrines or Frontier Bounties did not spawn.");
   gm.AttuneRelic(0);
   Require(gm.HasRelic(0) && gm.RelicsCount == 1, "Relic Shrine did not attune.");
   Require(gm.CryptDungeon != null && gm.StormRelay != null && gm.GrappleAnchors.Count == 3 && gm.NPCs.Count == 6 && gm.Campfires.Count == 3 && gm.Hero.CompanionWisp != null,
    "Sunken Crypt, Stormveil Relay, Grapple Anchors, Kingdom NPCs, Campfires, or Companion Wisp did not spawn.");
   gm.Campfires[0].CookMeal(gm);
   Require(gm.Hero.IsMealBuffActive, "Cooking at campfire did not activate meal buff.");
   gm.Hero.PerformEchoSkill();
   gm.Hero.PerformElementalBurst();
   int beforeNpcGold = gm.Gold;
   gm.Hero.WarpTo(gm.NPCs[0].transform.position + Vector3.back * 1.5f);
   gm.Interact();
   Require(gm.Gold == beforeNpcGold + 20, "Speaking with Master Smith Brom did not award NPC greeting gold.");
   gm.Hero.WarpTo(OpenWorldCryptDungeon.EntrancePos + Vector3.back * 2f);
   gm.Interact();
   Require(Vector3.Distance(gm.Hero.transform.position, OpenWorldCryptDungeon.ChamberCenter) < 12f,
    "Crypt entrance did not warp Aster into the Abyssal Crypt Chamber.");
   gm.AlignCryptPad(0);
   gm.AlignCryptPad(1);
   gm.Hero.WarpTo(OpenWorldCryptDungeon.MemoryAltarPos + Vector3.back * 1.5f);
   gm.Interact();
   Require(gm.CryptCompleted && gm.UnlockedWeapons.Contains(9),
    "Crypt Memory Altar did not awaken or unlock Duskblade.");
   gm.Hero.WarpTo(OpenWorldCryptDungeon.ReturnPortalPos);
   gm.Interact();
   Require(Vector3.Distance(gm.Hero.transform.position, OpenWorldCryptDungeon.EntrancePos) < 8f,
    "Crypt Return Passage did not return Aster to the surface.");
   gm.ActivateStormRelay();
   Require(gm.StormRelayAligned, "Stormveil Relay did not align.");
   gm.Hero.WarpTo(OpenWorldManager.SanctuaryPosition + Vector3.back * 3);
   gold = gm.Gold;
   int gems = gm.Gems;
   gm.Interact();
   Require(gm.Gold == gold && gm.Gems == gems, "Quest completion pays repeatedly.");
   Require(gm.SaveAdventure(), "Save did not write.");
   var snapshot = gm.CaptureAdventure();
   gm.BuildOpenWorld();
   Require(gm.Gold == gold && gm.Gems == gems && gm.QuestCompleted && gm.Hero.MaxHealth == 6 && gm.Hero.EquippedWeaponId == 8,
    "Progress/equipment changed when rebuilding from disk.");
   Require(gm.HasRelic(0) && gm.RelicsCount == 1, "Attuned Relic Shrine state lost when rebuilding from disk.");
   Require(gm.CryptCompleted && gm.StormRelayAligned && gm.UnlockedWeapons.Contains(9),
    "Crypt completion or Stormveil Relay state lost when rebuilding from disk.");
   Require(gm.SanctumCompleted && gm.Hero.MaxAether == 125f && gm.UnlockedWeapons.Contains(7) && gm.SanctumTraps.All(t => t.Disarmed),
    "Sanctum completion/traps state lost when rebuilding from disk.");
   Require(gm.Camps.All(c => c.Captured && c.Guards.Count == 0), "Captured camps respawn guards after reload.");
   Require(gm.Collectibles.Where(c => c && snapshot.collected.Contains(c.PersistentId)).All(c => c.Collected && !c.gameObject.activeSelf),
    "Collected objects return after reload.");
   Require(gm.Enemies.Where(e => e && e.IsBoss).All(e => !e.gameObject.activeSelf), "Defeated guardian returned.");
   Require(gm.AdventurerRank == 5, "Adventurer Rank did not reach Rank 5 Grand Warden after full completion.");
   int restoredGold = gm.Gold, restoredGems = gm.Gems;
   gm.NPCs[0].Converse(gm);
   gm.Campfires[0].CookMeal(gm);
   Require(gm.Gold == restoredGold && gm.Gems == restoredGems, "NPC/cooking first rewards paid again after disk reload.");
   gm.Hero.Stamina = 0;
   gm.Hero.LaunchIntoGlide(20f);
   Require(!gm.Hero.IsGliding, "Exhausted hero reopened the glider.");
   gm.Hero.RefillStamina();
   Require(gm.Updrafts.Count == 2, "Gale Updraft vents did not spawn.");
   gm.Hero.LaunchIntoGlide(20f);
   Require(gm.Hero.IsGliding, "Aether Windwings Glider did not deploy on updraft launch.");
   gm.Hero.WarpTo(new Vector3(0, 0, -6));
   Require(!gm.Hero.IsGliding, "Aether Windwings Glider did not stow after warp/grounding.");
   Require(OpenWorldAtmosphere.Instance != null, "OpenWorldAtmosphere did not initialize.");
   OpenWorldAtmosphere.Instance.CycleTimeOfDay();
   OpenWorldAtmosphere.Instance.ApplyPreset(OpenWorldAtmosphere.TimeOfDayPreset.VibrantMidday, false);
   OpenWorldAtmosphere.Instance.CycleWeather();
   Require(OpenWorldAtmosphere.Instance.CurrentWeather == OpenWorldAtmosphere.WeatherPreset.SpringRain, "Weather cycle did not activate Spring Rain.");
   OpenWorldAtmosphere.Instance.ApplyWeather(OpenWorldAtmosphere.WeatherPreset.ClearSky);
   gm.CameraRig.SnapToTarget();
   VerifyCameraCollision(gm);
   Require(gm.ActiveTerrain.terrainData.terrainLayers.Length == 4, "4-layer terrain splatting was not installed.");
   VerifyDiskRecovery(snapshot);
   File.WriteAllText("Docs/adventure-validation.txt",
    "PASS: Full sanctuary/sigil/outpost/guardian/reward flow; repeated reward rejection; world rebuild loads currency, equipment, collected items, captured camps, guardian state; two-slot save corruption recovery; corrupt/future-format data preserved. Tests use isolated temporary directories, never player saves.");
  }

  static void VerifyCameraCollision(OpenWorldManager gm) {
   Vector3 focus = gm.Hero.transform.position + Vector3.up * gm.CameraRig.HeightOffset;
   Vector3 direction = -(Quaternion.Euler(gm.CameraRig.Pitch, gm.CameraRig.Yaw, 0) * Vector3.forward);
   var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
   wall.name = "Validation camera obstruction";
   wall.transform.position = focus + direction * 3;
   wall.transform.localScale = Vector3.one * 1.5f;
   Physics.SyncTransforms();
   gm.CameraRig.SnapToTarget();
   Require(Vector3.Distance(gm.CameraRig.transform.position, focus) < 2.5f, "Camera remained behind an obstruction.");
   wall.SetActive(false);
   UnityEngine.Object.Destroy(wall);
   Physics.SyncTransforms();
   gm.CameraRig.SnapToTarget();
   Require(Vector3.Distance(gm.CameraRig.transform.position, focus) > 3f, "Camera did not recover after obstruction removal.");
  }

  static void VerifyDiskRecovery(OpenWorldSaveData data) {
   string directory = Path.Combine(Path.GetTempPath(), "LostRealmsValidation", Guid.NewGuid().ToString("N"));
   var store = new OpenWorldSaveStore(directory);
   Require(store.Load() == null, "Fresh directory did not yield a new game.");
   store.Write(data); // revision 1, slot 1
   int originalGold = data.gold;
   data.gold += 17;
   store.Write(data); // revision 2, slot 0
   var latest = new OpenWorldSaveStore(directory);
   Require(latest.Load().gold == originalGold + 17, "Latest valid revision was not chosen.");
   File.WriteAllText(Path.Combine(directory, "adventure-0.json"), "{\"version\":"); // interrupted second write
   var recovery = new OpenWorldSaveStore(directory);
   Require(recovery.Load().gold == originalGold && recovery.RecoveredBackup, "Interrupted write did not recover previous snapshot.");
   recovery.Write(data);
   Require(new OpenWorldSaveStore(directory).Load().gold == originalGold + 17, "Recovered game could not save again.");
   File.WriteAllText(Path.Combine(directory, "adventure-0.json"), "{}");
   File.WriteAllText(Path.Combine(directory, "adventure-1.json"), "{}");
   bool rejected = false;
   try { new OpenWorldSaveStore(directory).Load(); } catch (InvalidDataException) { rejected = true; }
   Require(rejected && File.ReadAllText(Path.Combine(directory, "adventure-1.json")) == "{}", "Corrupt saves were silently replaced.");
   File.WriteAllText(Path.Combine(directory, "adventure-0.json"), "{\"format\":\"LostRealmsOW\",\"version\":999}");
   rejected = false;
   try { new OpenWorldSaveStore(directory).Load(); } catch (NotSupportedException) { rejected = true; }
   Require(rejected, "Future save version was not protected.");
  }
 }
}
