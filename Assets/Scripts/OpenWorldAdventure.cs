using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LostRealmsOW {
 public sealed partial class OpenWorldManager {
  public enum AdventureStage { FindSanctuary, RecoverSigils, OpenGrove, DefeatGuardian, ReturnToSanctuary, Complete }
  public bool QuestStarted { get; private set; }
  public bool GuardianAwakened { get; private set; }
  public bool QuestCompleted { get; private set; }
  public int SanctumFociMask { get; private set; }
  public bool SanctumCompleted { get; private set; }
  public int RelicsMask { get; private set; }
  public int BountiesMask { get; private set; }
  public int CryptMask { get; private set; }
  public bool CryptCompleted => (CryptMask & 4) != 0;
  public bool StormRelayAligned { get; private set; }
  public int SanctumFociCount => ((SanctumFociMask & 1) != 0 ? 1 : 0) + ((SanctumFociMask & 2) != 0 ? 1 : 0) + ((SanctumFociMask & 4) != 0 ? 1 : 0);
  public int RelicsCount => ((RelicsMask & 1) != 0 ? 1 : 0) + ((RelicsMask & 2) != 0 ? 1 : 0) + ((RelicsMask & 4) != 0 ? 1 : 0) + ((RelicsMask & 8) != 0 ? 1 : 0);
  public int BountiesCount => ((BountiesMask & 1) != 0 ? 1 : 0) + ((BountiesMask & 2) != 0 ? 1 : 0) + ((BountiesMask & 4) != 0 ? 1 : 0);
  public bool HasRelic(int index) => (RelicsMask & (1 << index)) != 0;
  public int AdventurerRank => 1 + (CampsCaptured >= 1 ? 1 : 0) + (CampsCaptured >= 3 ? 1 : 0) + (SanctumCompleted ? 1 : 0) + (GuardianDefeated ? 1 : 0);
  public float AdventurerDamageMultiplier => 1f + (AdventurerRank - 1) * 0.06f;
  public string AdventurerTitle => AdventurerRank >= 5 ? "GRAND WARDEN" : AdventurerRank == 4 ? "REALM CHAMPION" : AdventurerRank == 3 ? "KNIGHT ERRANT" : AdventurerRank == 2 ? "PATHFINDER" : "WARDEN";
  public OpenWorldEnemy ActiveBoss {
   get {
    if (guardian && guardian.gameObject.activeSelf && !guardian.Dead) return guardian;
    if (Hero) {
     foreach (var b in BountyElites) {
      if (b && b.gameObject.activeSelf && !b.Dead && Vector3.Distance(Hero.transform.position, b.transform.position) <= 26f) {
       return b;
      }
     }
    }
    return null;
   }
  }
  public readonly HashSet<string> CollectedWorldIds = new HashSet<string>();
  public readonly HashSet<string> CapturedCampIds = new HashSet<string>();
  public readonly List<OpenWorldSanctumFocus> SanctumFoci = new List<OpenWorldSanctumFocus>();
  public readonly List<OpenWorldSanctumTrap> SanctumTraps = new List<OpenWorldSanctumTrap>();
  public readonly List<OpenWorldEnemy> SanctumGuards = new List<OpenWorldEnemy>();
  public readonly List<OpenWorldUpdraft> Updrafts = new List<OpenWorldUpdraft>();
  public readonly List<OpenWorldRelicShrine> RelicShrines = new List<OpenWorldRelicShrine>();
  public readonly List<OpenWorldEnemy> BountyElites = new List<OpenWorldEnemy>();
  public readonly List<OpenWorldGrappleAnchor> GrappleAnchors = new List<OpenWorldGrappleAnchor>();
  public readonly List<OpenWorldNPC> NPCs = new List<OpenWorldNPC>();
  public readonly List<OpenWorldCampfire> Campfires = new List<OpenWorldCampfire>();
  public int RawMeat;
  public OpenWorldCryptDungeon CryptDungeon { get; set; }
  public OpenWorldStormRelay StormRelay { get; set; }
  public string CheckpointId { get; private set; } = "hub";
  public static readonly Vector3 SanctuaryPosition = new Vector3(0, 0, 4);
  public static readonly Vector3 GroveSealPosition = new Vector3(0, 0, 98);
  public static readonly Vector3 SanctumAltarPosition = new Vector3(-60, 0, -62);
  public static readonly Vector3 ContractBoardPosition = new Vector3(-9.5f, 0f, -3.5f);
  OpenWorldSaveStore saveStore;
  bool adventureReady, saveDirty, saveBlocked;
  bool saveFailed;
  public bool SaveUnavailable => saveBlocked || saveFailed;
  float nextSave;
  OpenWorldEnemy guardian;
  GameObject groveSeal;

  public AdventureStage QuestStage => QuestCompleted ? AdventureStage.Complete :
   GuardianDefeated ? AdventureStage.ReturnToSanctuary :
   GuardianAwakened ? AdventureStage.DefeatGuardian :
   !QuestStarted ? AdventureStage.FindSanctuary :
   KeysCollected >= TotalKeys && CampsCaptured >= 3 ? AdventureStage.OpenGrove : AdventureStage.RecoverSigils;

  public string QuestHeading {
   get {
    switch (QuestStage) {
     case AdventureStage.FindSanctuary: return "A WHISPER IN THE ROOTS";
     case AdventureStage.RecoverSigils: return "THE THREE LOST SIGILS";
     case AdventureStage.OpenGrove: return "BREAK THE GROVE'S SEAL";
     case AdventureStage.DefeatGuardian: return "HEARTWOOD'S LAST WATCH";
     case AdventureStage.ReturnToSanctuary: return "BRING THE FOREST HOME";
     default: return "VERDANT RESTORED";
    }
   }
  }

  public Vector3 QuestTarget {
   get {
    if (VerdantHuntStage > 0 && VerdantHuntStage < 5) return VerdantHuntTarget;
    if (QuestStage == AdventureStage.RecoverSigils) {
     float closest = float.MaxValue;
     Vector3 target = SanctuaryPosition;
     Vector3 from = Hero ? Hero.transform.position : SanctuaryPosition;
     foreach (var camp in Camps) {
      if (!camp || camp.Captured) continue;
      float distance = (camp.transform.position - from).sqrMagnitude;
      if (distance < closest) { closest = distance; target = camp.transform.position; }
     }
     foreach (var item in Collectibles) {
      if (!item || item.Collected || item.Kind != OWCollectibleKind.TempleKey) continue;
      float distance = (item.transform.position - from).sqrMagnitude;
      if (distance < closest) { closest = distance; target = item.transform.position; }
     }
     return target;
    }
    if (QuestStage == AdventureStage.OpenGrove) return GroveSealPosition;
    if (QuestStage == AdventureStage.DefeatGuardian) return guardian ? guardian.transform.position : new Vector3(0, 0, 115);
    return SanctuaryPosition;
   }
  }

  public string QuestDetail {
   get {
    switch (QuestStage) {
     case AdventureStage.FindSanctuary: return "Listen at the sanctuary stone";
     case AdventureStage.RecoverSigils: return $"Free outposts {CampsCaptured}/3 • Find sigils {KeysCollected}/3";
     case AdventureStage.OpenGrove: return "Use the three sigils at the grove stone";
     case AdventureStage.DefeatGuardian:
      return guardian && !guardian.Dead
       ? $"Heartwood HP {Mathf.CeilToInt(guardian.Health)}/{Mathf.CeilToInt(guardian.MaxHealth)} • Dodge the red warnings"
       : "Defeat Heartwood • dodge the red warnings";
     case AdventureStage.ReturnToSanctuary: return "Return to the sanctuary for your reward";
     default: return "Explore the realm • your journey is saved";
    }
   }
  }

  bool Near(Vector3 point, float radius = 4f) {
   if (!Hero || Hero.Dead || IsModalOpen) return false;
   if (point.y <= 10f && ActiveTerrain) point.y = ActiveTerrain.SampleHeight(point) + ActiveTerrain.transform.position.y;
   Vector3 delta = Hero.transform.position - point;
   // Gate decks, sky vaults, and shrine steps sit above terrain; allow their elevation while
   // keeping the interaction radius consistent in the horizontal plane.
   return Mathf.Abs(delta.y) <= 7f && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
  }
  public string InteractionPrompt {
   get {
    if (Hero && Hero.IsMounted) return "DISMOUNT";
    if (NearVerdantTracks) return "INSPECT TRACKS";
    if (NearVerdantCampfire) return RawMeat > 0 ? "COOK OAKVALE'S MEAL (1 MEAT)" : "COOK MEAL • NEED RAW MEAT";
    if (Near(SanctuaryPosition)) {
     if (!QuestStarted) return "LISTEN";
     if (GuardianDefeated && !QuestCompleted) return "CLAIM REWARD";
     return "REST";
    }
    if (Near(ContractBoardPosition, 3.6f)) return "READ CONTRACTS";
    if (Near(OpenWorldCryptDungeon.EntrancePos, 4.2f)) return "ENTER CRYPT";
    if (Near(OpenWorldCryptDungeon.ReturnPortalPos, 3.6f)) return "EXIT CRYPT";
    if (Near(OpenWorldCryptDungeon.SunPadPos, 3.2f) && (CryptMask & 1) == 0) return "ALIGN SOLAR PAD";
    if (Near(OpenWorldCryptDungeon.MoonPadPos, 3.2f) && (CryptMask & 2) == 0) return "ALIGN LUNAR PAD";
    if (Near(OpenWorldCryptDungeon.MemoryAltarPos, 3.8f) && !CryptCompleted) {
     return (CryptMask & 3) == 3 ? "AWAKEN CRYPT ALTAR" : "READ CRYPT ALTAR";
    }
    if (Near(OpenWorldStormRelay.TurnPadPos, 3.6f) && !StormRelayAligned) return "ALIGN STORM RELAY";
    if (Near(GroveSealPosition)) return QuestStage == AdventureStage.OpenGrove ? "OPEN GROVE" :
     !GuardianAwakened ? "READ SEAL" : "";
    var focus = NearestUnattunedFocus();
    if (focus) {
     return focus.FocusIndex == 0 ? "ATTUNE EMBER" : focus.FocusIndex == 1 ? "ATTUNE FROST" : "ATTUNE GALE";
    }
    if (Near(SanctumAltarPosition)) {
     if (!SanctumCompleted) return SanctumFociCount >= 3 ? "AWAKEN ALTAR" : "READ ALTAR";
    }
    var npc = NearestNPC();
    if (npc) return "SPEAK";
    var cf = NearestCampfire();
    if (cf) return RawMeat > 0 ? $"COOK VENISON • {RawMeat} MEAT (USES 1)" : "COOK STEW • NEED RAW MEAT";
    var animal = OpenWorldAnimalInteraction.Nearest(Hero);
    if (animal) return animal.Prompt;
    return "";
   }
  }

  OpenWorldCampfire NearestCampfire(float radius = 3.2f) {
   if (!Hero || Hero.Dead || IsModalOpen) return null;
   OpenWorldCampfire best = null;
   float bestD = radius;
   foreach (var c in Campfires) {
    if (!c) continue;
    if (Near(c.transform.position, bestD)) {
     bestD = Vector2.Distance(new Vector2(Hero.transform.position.x, Hero.transform.position.z), new Vector2(c.transform.position.x, c.transform.position.z));
     best = c;
    }
   }
   return best;
  }

  OpenWorldNPC NearestNPC(float radius = 3.6f) {
   if (!Hero || Hero.Dead || IsModalOpen) return null;
   OpenWorldNPC best = null;
   float bestD = radius;
   foreach (var n in NPCs) {
    if (!n) continue;
    if (Near(n.transform.position, bestD)) {
     bestD = Vector2.Distance(new Vector2(Hero.transform.position.x, Hero.transform.position.z), new Vector2(n.transform.position.x, n.transform.position.z));
     best = n;
    }
   }
   return best;
  }

  OpenWorldSanctumFocus NearestUnattunedFocus(float radius = 3.8f) {
   if (!Hero || Hero.Dead || IsModalOpen) return null;
   OpenWorldSanctumFocus best = null;
   float bestD = radius;
   foreach (var f in SanctumFoci) {
    if (!f || f.Attuned) continue;
    if (Near(f.transform.position, bestD)) {
     bestD = Vector2.Distance(new Vector2(Hero.transform.position.x, Hero.transform.position.z), new Vector2(f.transform.position.x, f.transform.position.z));
     best = f;
    }
   }
   return best;
  }

  public bool AttuneRelic(int relicIndex) {
   if (relicIndex < 0 || relicIndex > 3) return false;
   int bit = 1 << relicIndex;
   if ((RelicsMask & bit) != 0) return false;
   RelicsMask |= bit;
   ApplyRelicAndBountyState();
   var shrine = RelicShrines.Find(s => s && s.RelicIndex == relicIndex);
   OpenWorldAudio.Play("sfx_upgrade");
   if (shrine) {
    SpawnBurst(shrine.transform.position + Vector3.up * 1.8f, new Color(1f, 0.88f, 0.36f), 22);
    ShowBanner($"ANCIENT RELIC ATTUNED: {shrine.RelicName} ({RelicsCount}/4) • {shrine.BlessingSummary}");
   }
   MarkProgressDirty();
   return true;
  }

  public bool AttuneSanctumFocus(int focusIndex) {
   if (focusIndex < 0 || focusIndex > 2) return false;
   int bit = 1 << focusIndex;
   if ((SanctumFociMask & bit) != 0) return false;
   SanctumFociMask |= bit;
   ApplySanctumState();
   string[] names = { "Ember Focus", "Frost Focus", "Gale Focus" };
   string[] sfx = { "sfx_ember_cast", "sfx_frost_cast", "sfx_gale_cast" };
   Color[] cols = { new Color(1f, 0.42f, 0.14f), new Color(0.28f, 0.86f, 1f), new Color(0.32f, 0.98f, 0.62f) };
   OpenWorldAudio.Play(sfx[focusIndex]);
   var focusObj = SanctumFoci.Find(f => f && f.FocusIndex == focusIndex);
   if (focusObj) SpawnBurst(focusObj.transform.position + Vector3.up * 2.2f, cols[focusIndex], 20);
   ShowBanner($"{names[focusIndex].ToUpperInvariant()} ATTUNED ({SanctumFociCount}/3) • " +
    (SanctumFociCount >= 3 ? "Awaken the Sanctum Memory Altar!" : "Attune the remaining sanctum pillars."));
   MarkProgressDirty();
   return true;
  }

  public bool AlignCryptPad(int padIndex) {
   if (padIndex < 0 || padIndex > 1) return false;
   int bit = 1 << padIndex;
   if ((CryptMask & bit) != 0) return false;
   CryptMask |= bit;
   ApplyCryptAndStormState();
   OpenWorldAudio.Play("sfx_upgrade");
   Vector3 p = padIndex == 0 ? OpenWorldCryptDungeon.SunPadPos : OpenWorldCryptDungeon.MoonPadPos;
   Color c = padIndex == 0 ? new Color(1f, 0.82f, 0.25f) : new Color(0.42f, 0.86f, 1f);
   SpawnBurst(p + Vector3.up * 0.8f, c, 20);
   bool both = (CryptMask & 3) == 3;
   ShowBanner((padIndex == 0 ? "SOLAR SEAL ALIGNED! " : "LUNAR SEAL ALIGNED! ") +
    (both ? "The Astral Orrery resonates — Awaken the Crypt Memory Altar!" : "Align the opposite seal pad to unlock the Crypt Memory Altar."));
   MarkProgressDirty();
   return true;
  }

  public bool ActivateStormRelay() {
   if (StormRelayAligned) return false;
   StormRelayAligned = true;
   Gold += 65;
   Gems += 2;
   ApplyCryptAndStormState();
   OpenWorldAudio.Play("sfx_complete");
   SpawnBurst(OpenWorldStormRelay.ConductorPos + Vector3.up * 2.5f, new Color(0.42f, 0.82f, 1f), 24);
   ShowBanner("STORMVEIL CONDUCTOR ALIGNED! (+65 Gold, +2 Gems • Permanent +12% Tempest Speed Blessing)");
   MarkProgressDirty();
   return true;
  }

  public bool TryGrappleToNearestAnchor(float maxDist = 25f) {
   if (!Hero || Hero.Dead || IsModalOpen) return false;
   OpenWorldGrappleAnchor best = null;
   float bestD = maxDist;
   foreach (var a in GrappleAnchors) {
    if (!a) continue;
    float d = Vector3.Distance(Hero.transform.position, a.transform.position);
    if (d <= bestD && d >= 2.2f) {
     bestD = d;
     best = a;
    }
   }
   if (!best) return false;
   SpawnBurst(Hero.transform.position + Vector3.up * 0.9f, new Color(0.28f, 1f, 0.82f), 16);
   Hero.WarpTo(best.transform.position + Vector3.up * 0.5f);
   Hero.LaunchIntoGlide(9.5f);
   CameraRig?.SnapToTarget();
   OpenWorldAudio.Play("sfx_gale_cast");
   SpawnBurst(Hero.transform.position, new Color(0.28f, 1f, 0.82f), 20);
   ShowBanner($"AETHER GRAPPLE: {best.AnchorTitle.ToUpperInvariant()}!");
   return true;
  }

  public void Interact() {
   if (!adventureReady || IsModalOpen || !Hero || Hero.Dead) return;
   if (Hero.IsMounted) { Hero.LeaveMount(); return; }
   if (InspectVerdantTracks()) return;
   if (TryCookVerdantMeal()) return;
   if (GlmQuestInteract()) return;
   if (Near(SanctuaryPosition)) {
    if (!QuestStarted) {
     QuestStarted = true;
     ShowBanner("The sanctuary whispers: Free the three outposts. Recover the lost sigils. Wake the heart of the forest.");
    } else if (GuardianDefeated && !QuestCompleted) {
     QuestCompleted = true;
     Gold += 150; Gems += 5;
     UnlockedWeapons.Add(8);
     Hero.MaxHealth = 6f;
     Hero.EquipWeapon(8);
     ShowBanner("VERDANT RESTORED • Ornate Curved Blade, +1 maximum health, 150 gold and 5 gems");
     OpenWorldAudio.Play("sfx_complete");
    } else ShowBanner("Restored at the sanctuary • Adventure saved");
    Hero.Health = Hero.MaxHealth;
    Hero.Aether = Hero.MaxAether;
    SetCheckpoint("hub");
    SpawnBurst(Hero.transform.position + Vector3.up, new Color(0.35f, 1f, 0.7f), 20);
    MarkProgressDirty();
    SaveAdventure();
   } else if (Near(ContractBoardPosition, 3.6f)) {
    OpenWorldAudio.Play("sfx_menu", 0.5f);
    string contractMsg = !CryptCompleted
     ? "CONTRACT: Descend into the Sunken Crypt (SE of Hub) and align the Astral Orrery!"
     : !SanctumCompleted
      ? "CONTRACT: Awaken the 3 Elemental Foci at the Forgotten Sanctum (SW)!"
      : !StormRelayAligned
       ? "CONTRACT: Align the Stormveil Conductor Tower at the SE Tempest Overlook!"
       : BountiesCount < 3
        ? $"CONTRACT: Hunt the Frontier Rift Bounties ({BountiesCount}/3 Slain)!"
        : "ALL KINGDOM CONTRACTS & FRONTIER BOUNTIES COMPLETED • Grand Warden of the Realms!";
    ShowBanner(contractMsg);
   } else if (Near(OpenWorldCryptDungeon.EntrancePos, 4.2f)) {
    Hero.WarpTo(OpenWorldCryptDungeon.ChamberCenter + new Vector3(0f, 0.6f, -3.5f));
    CameraRig?.SnapToTarget();
    OpenWorldAudio.Play("sfx_checkpoint");
    SpawnBurst(Hero.transform.position + Vector3.up * 0.9f, new Color(0.68f, 0.45f, 1f), 20);
    ShowBanner("DESCENDED INTO THE SUNKEN CRYPT OF THE FIRST WARDENS • Align the Solar & Lunar Pads!");
   } else if (Near(OpenWorldCryptDungeon.ReturnPortalPos, 3.6f)) {
    Hero.WarpTo(OpenWorldCryptDungeon.EntrancePos + new Vector3(0f, 0f, -2.5f));
    CameraRig?.SnapToTarget();
    OpenWorldAudio.Play("sfx_checkpoint");
    SpawnBurst(Hero.transform.position + Vector3.up * 0.9f, new Color(0.38f, 0.96f, 0.78f), 20);
    ShowBanner("ASCENDED TO THE VERDANT SURFACE");
   } else if (Near(OpenWorldCryptDungeon.SunPadPos, 3.2f) && (CryptMask & 1) == 0) {
    AlignCryptPad(0);
    SaveAdventure();
   } else if (Near(OpenWorldCryptDungeon.MoonPadPos, 3.2f) && (CryptMask & 2) == 0) {
    AlignCryptPad(1);
    SaveAdventure();
   } else if (Near(OpenWorldCryptDungeon.MemoryAltarPos, 3.8f) && !CryptCompleted) {
    if ((CryptMask & 3) != 3) {
     ShowBanner("The Crypt Memory Altar is sealed • Step onto both the Solar and Lunar Pads to align the Astral Orrery.");
     return;
    }
    CryptMask |= 4;
    Gold += 150;
    Gems += 6;
    UnlockedWeapons.Add(9);
    ApplyCryptAndStormState();
    Hero.Health = Hero.MaxHealth;
    Hero.Aether = Hero.MaxAether;
    OpenWorldAudio.Play("sfx_complete");
    SpawnBurst(OpenWorldCryptDungeon.MemoryAltarPos + Vector3.up * 1.4f, new Color(0.75f, 0.48f, 1f), 28);
    ShowBanner("FIRST WARDEN'S MEMORY AWAKENED! • Duskblade Epic Sword unlocked, +150 Gold, +6 Gems!");
    MarkProgressDirty();
    SaveAdventure();
   } else if (Near(OpenWorldStormRelay.TurnPadPos, 3.6f) && !StormRelayAligned) {
    ActivateStormRelay();
    SaveAdventure();
   } else if (Near(GroveSealPosition)) {
    if (QuestStage != AdventureStage.OpenGrove) {
     if (!GuardianAwakened) ShowBanner($"The grove is sealed • Free all 3 outposts and recover all 3 sigils ({CampsCaptured}/3, {KeysCollected}/3)");
     return;
    }
    GuardianAwakened = true;
    ApplyGuardianState();
    SetCheckpoint("grove");
    Hero.Health = Hero.MaxHealth;
    Hero.Aether = Hero.MaxAether;
    ShowBanner("The seal breaks. Heartwood awakens! Watch his attacks and strike between them.");
    OpenWorldAudio.Play("sfx_boss_heartwood_roar");
    SpawnBurst(GroveSealPosition + Vector3.up * 1.5f, new Color(0.5f, 1f, 0.65f), 28);
    MarkProgressDirty();
    SaveAdventure();
   } else if (NearestUnattunedFocus() is OpenWorldSanctumFocus focus) {
    AttuneSanctumFocus(focus.FocusIndex);
    SaveAdventure();
   } else if (Near(SanctumAltarPosition) && !SanctumCompleted) {
    if (SanctumFociCount < 3) {
     ShowBanner($"The Memory Altar slumbers • Attune all 3 elemental focus pillars around the sanctum ({SanctumFociCount}/3)");
     return;
    }
    SanctumCompleted = true;
    Gold += 100;
    Gems += 4;
    UnlockedWeapons.Add(7);
    ApplySanctumState();
    Hero.Health = Hero.MaxHealth;
    Hero.Aether = Hero.MaxAether;
    SetCheckpoint("sanctum");
    ShowBanner("SANCTUM AWAKENED • +25 Max Aether, Frost Halberd unlocked, 100 gold and 4 gems!");
    OpenWorldAudio.Play("sfx_complete");
    SpawnBurst(SanctumAltarPosition + Vector3.up * 1.4f, new Color(0.55f, 0.38f, 1f), 26);
    MarkProgressDirty();
    SaveAdventure();
   } else if (NearestNPC() is OpenWorldNPC npc) {
    npc.Converse(this);
    SaveAdventure();
   } else if (NearestCampfire() is OpenWorldCampfire cf) {
    cf.CookMeal(this);
    SaveAdventure();
   } else if (OpenWorldAnimalInteraction.Nearest(Hero) is OpenWorldAnimalInteraction animal) {
    animal.Interact(this);
   }
  }

  public void MarkProgressDirty() {
   if (!adventureReady || saveBlocked) return;
   if (!saveDirty) nextSave = Time.unscaledTime + 1f;
   saveDirty = true;
  }

  void InitializeAdventure() {
   adventureReady = false;
   string directory = Path.Combine(Application.persistentDataPath, "Adventure");
#if UNITY_EDITOR
   // Validation runs must never load or overwrite the developer's adventure.
   string testDirectory = Environment.GetEnvironmentVariable("LOST_REALMS_TEST_SAVE_DIR");
   if (Application.isBatchMode && !string.IsNullOrEmpty(testDirectory)) directory = testDirectory;
#endif
   saveStore = new OpenWorldSaveStore(directory);
   try {
    var data = saveStore.Load();
    if (data != null) RestoreAdventure(data);
    ShowBanner(saveStore.RecoveredBackup ? "Adventure recovered from backup • Return to your journey" :
     data != null ? "Welcome back, Aster • " + QuestDetail : "A whisper calls from the sanctuary stone. Approach it to begin.");
   } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) {
    saveBlocked = true;
    Debug.LogWarning("Adventure load unavailable: " + e.Message);
    ShowBanner("Save unavailable • Existing data preserved. Progress will not be saved this session.");
   }
   ApplyGuardianState();
   ApplySanctumState();
   ApplyRelicAndBountyState();
   ApplyCryptAndStormState();
   SetCheckpoint(CheckpointId);
   Hero.WarpTo(respawnPoint);
   CameraRig.SnapToTarget();
   adventureReady = true;
  }

  void ApplyGuardianState() {
   if (guardian) guardian.gameObject.SetActive(GuardianAwakened && !GuardianDefeated);
   if (groveSeal) groveSeal.SetActive(!GuardianAwakened);
  }

  void ApplySanctumState() {
   foreach (var f in SanctumFoci) {
    if (f) f.ApplyState((SanctumFociMask & (1 << f.FocusIndex)) != 0);
   }
   foreach (var t in SanctumTraps) {
    if (t) t.SetDisarmed(SanctumCompleted);
   }
   if (SanctumCompleted) {
    foreach (var g in SanctumGuards) {
     if (g && !g.Dead) g.gameObject.SetActive(false);
    }
   }
   if (Hero) {
    Hero.MaxAether = SanctumCompleted ? 125f : 100f;
    if (Hero.Aether > Hero.MaxAether) Hero.Aether = Hero.MaxAether;
   }
  }

  void ApplyRelicAndBountyState() {
   foreach (var r in RelicShrines) {
    if (r) r.ApplyState((RelicsMask & (1 << r.RelicIndex)) != 0);
   }
   foreach (var b in BountyElites) {
    if (b && b.BountyIndex >= 0 && (BountiesMask & (1 << b.BountyIndex)) != 0) {
     b.gameObject.SetActive(false);
    }
   }
  }

  void ApplyCryptAndStormState() {
   if (CryptDungeon) {
    CryptDungeon.ApplyState((CryptMask & 1) != 0, (CryptMask & 2) != 0, CryptCompleted);
   }
   if (StormRelay) {
    StormRelay.ApplyState(StormRelayAligned);
   }
  }

  void TickAdventure() {
   if (Input.GetKeyDown(KeyCode.F)) Interact();
   if (Input.GetKeyDown(KeyCode.T)) TryGrappleToNearestAnchor();
   if (saveDirty && Time.unscaledTime >= nextSave) SaveAdventure();
  }

  void SetCheckpoint(string id) {
   var gate = Gates.Find(g => g && g.WaypointId == id);
   if (!gate) gate = Gates.Find(g => g && g.WaypointId == "hub");
   if (!gate) return;
   CheckpointId = gate.WaypointId;
   gate.Discovered = true;
   respawnPoint = (gate.ArrivalPoint && Vector3.Distance(gate.ArrivalPoint.position, gate.transform.position) >= 1.8f)
    ? gate.ArrivalPoint.position
    : gate.transform.position - gate.transform.forward * 3.5f;
   if (CheckpointId == "hub") respawnPoint = new Vector3(0, 0, -6);
  }

  public OpenWorldSaveData CaptureAdventure() {
   var discovered = new List<string>();
   foreach (var gate in Gates) if (gate && gate.Discovered) discovered.Add(gate.WaypointId);
   return new OpenWorldSaveData {
    gold = Gold, gems = Gems, weapon = SavedWeapon, rawMeat = RawMeat,
    skin = SavedSkin, element = Hero ? Hero.ActiveElement : 0,
    sanctumFoci = SanctumFociMask, sanctumCompleted = SanctumCompleted,
    relics = RelicsMask, bounties = BountiesMask,
    cryptMask = CryptMask, stormRelay = StormRelayAligned,
    checkpoint = CheckpointId, questStarted = QuestStarted, guardianAwakened = GuardianAwakened,
    guardianDefeated = GuardianDefeated, questCompleted = QuestCompleted,
    collected = new List<string>(CollectedWorldIds).ToArray(), camps = new List<string>(CapturedCampIds).ToArray(),
    gates = discovered.ToArray(), weapons = new List<int>(UnlockedWeapons).ToArray(), skins = new List<int>(UnlockedSkins).ToArray()
   };
  }

  public void RestoreAdventure(OpenWorldSaveData data) {
   RawMeat = Mathf.Clamp(data.rawMeat, 0, 999);
   Gold = data.gold; Gems = data.gems;
   QuestStarted = data.questStarted; GuardianAwakened = data.guardianAwakened;
   GuardianDefeated = data.guardianDefeated; QuestCompleted = data.questCompleted;
   SanctumFociMask = data.sanctumFoci; SanctumCompleted = data.sanctumCompleted;
   RelicsMask = data.relics; BountiesMask = data.bounties;
   CryptMask = data.cryptMask; StormRelayAligned = data.stormRelay;
   CollectedWorldIds.Clear(); CollectedWorldIds.UnionWith(data.collected);
   CapturedCampIds.Clear(); CapturedCampIds.UnionWith(data.camps);
   KeysCollected = 0; ChestsOpened = 0;
   foreach (var id in CollectedWorldIds) {
    if (id.StartsWith("key:")) KeysCollected++;
    if (id.StartsWith("chest:")) ChestsOpened++;
   }
   KeysCollected = Mathf.Min(TotalKeys, KeysCollected);
   CampsCaptured = 0;
   foreach (var camp in Camps) {
    if (!camp || !CapturedCampIds.Contains(camp.PersistentId)) continue;
    camp.RestoreCaptured();
    CampsCaptured++;
   }
   foreach (var item in Collectibles) {
    if (item && CollectedWorldIds.Contains(item.PersistentId)) item.RestoreCollected();
   }
   var discoveries = new HashSet<string>(data.gates);
   foreach (var gate in Gates) if (gate) gate.Discovered = gate.WaypointId == "hub" || discoveries.Contains(gate.WaypointId);
   UnlockedWeapons.Clear(); UnlockedWeapons.UnionWith(new[] { 0, 1, 2 });
   foreach (int id in data.weapons) if (id >= 0 && id < OpenWorldWeaponCatalog.All.Length) UnlockedWeapons.Add(id);
   UnlockedSkins.Clear(); UnlockedSkins.Add(0);
   foreach (int id in data.skins) if (id >= 0 && id < OpenWorldCharacterVisual.AsterSkins.Length) UnlockedSkins.Add(id);
   Hero.MaxHealth = QuestCompleted ? 6f : 5f;
   Hero.MaxAether = SanctumCompleted ? 125f : 100f;
   Hero.Health = Hero.MaxHealth; Hero.Aether = Hero.MaxAether;
   Hero.EquipWeapon(UnlockedWeapons.Contains(data.weapon) ? data.weapon : 0);
   Hero.EquipSkin(UnlockedSkins.Contains(data.skin) ? data.skin : 0);
   Hero.RestoreElement(data.element);
   SetCheckpoint(data.checkpoint);
   ApplyGuardianState();
   ApplySanctumState();
   ApplyRelicAndBountyState();
   ApplyCryptAndStormState();
  }

  public bool SaveAdventure() {
   if (!adventureReady || saveBlocked || saveStore == null) return false;
   try {
    saveStore.Write(CaptureAdventure());
    saveDirty = false;
    saveFailed = false;
    return true;
   } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
    saveFailed = true;
    nextSave = Time.unscaledTime + 15f;
    Debug.LogWarning("Adventure save unavailable: " + e.Message);
    ShowBanner("Could not save • Check free storage. The previous save is preserved.");
    return false;
   }
  }
  void OnApplicationPause(bool paused) { if (paused) SaveAdventure(); }
  void OnApplicationFocus(bool focused) { if (!focused && saveDirty) SaveAdventure(); }
  void OnApplicationQuit() { if (saveDirty) SaveAdventure(); }
 }
}
