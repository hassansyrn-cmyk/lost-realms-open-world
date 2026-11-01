using UnityEngine;

namespace LostRealmsOW {
 // Village life for the capital square (GLM): short, repeatable conversations for the three adjacent
 // civilians — Master Smith Brom (weapons), Lorekeeper Lyra (kingdom state), Apothecary Rowan (hunting
 // and cooking advice). Lines are contextual but purely conversational: no rewards, no healing, no
 // flags are written, so talking to them again always works. Banners use the "NAME: text" shape the
 // quest journal already records, and stay short for phone screens.
 // Bridge helper holder: the bridge resolves types by name, and partial-class file names are not real
 // type names — so the debug entry point lives on its own concrete class.
 public static class OpenWorldVillageDebug {
  public static void Talk(string npcId) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) { Debug.LogError("[Village] world missing"); return; }
   var npc = gm.NPCs.Find(n => n && n.NpcId == npcId);
   if (!npc) { Debug.LogError("[Village] npc missing: " + npcId); return; }
   int goldBefore = gm.Gold;
   gm.Hero.WarpTo(npc.transform.position + Vector3.back * 2f);
   npc.Converse(gm);
   Debug.Log("[Village] talked to " + npcId + " (gold " + goldBefore + " -> " + gm.Gold + ", hp " + gm.Hero.Health.ToString("0.0") + "/" + gm.Hero.MaxHealth.ToString("0.0") + ")");
  }

  // Bridge check: the three village conversations reached the quest journal.
  public static void ValidateJournal() {
   var gm = OpenWorldManager.I;
   if (!gm) throw new System.Exception("World required");
   var journal = OpenWorldQuestJournal.Instance;
   if (!journal) throw new System.Exception("Quest journal missing");
   // Contextual variants legitimately replace base lines depending on quest/save state, so the check
   // asserts speaker coverage — every one of the three villagers recorded at least one line.
   string[] speakers = { "MASTER SMITH BROM", "LOREKEEPER LYRA", "APOTHECARY ROWAN" };
   string missing = "";
   foreach (var s in speakers) {
    bool found = false;
    foreach (var line in journal.Dialogue()) if (line.Contains(s)) found = true;
    if (!found) missing += s + "; ";
   }
   if (missing.Length > 0) throw new System.Exception("Journal speakers missing: " + missing);
   Debug.Log("[Village] journal OK: all three village conversations recorded");
  }

  // Bridge check: the recorded journal lines live in the adventure save (so they survive save/reload).
  public static void ValidateJournalSave() {
   var gm = OpenWorldManager.I;
   if (!gm) throw new System.Exception("World required");
   var saved = JsonUtility.FromJson<OpenWorldSaveData>(JsonUtility.ToJson(gm.CaptureAdventure()));
   int inSave = 0;
   foreach (var f in saved.collected) if (f.StartsWith("journal:line:")) inSave++;
   if (inSave < 3) throw new System.Exception("Journal lines missing from the save snapshot: " + inSave);
   Debug.Log("[Village] journal save OK: " + inSave + " recorded lines present in the adventure snapshot");
  }
 }

 public sealed partial class OpenWorldManager {
  public bool GlmVillageConverse(string npcId) {
   switch (npcId) {
    case "brom": return ConverseBrom();
    case "lyra": return ConverseLyra();
    case "rowan": return ConverseRowan();
    default: return false;
   }
  }

  // Master Forge-Smith Brom: weapon talk, with an active-hunt line and a reward line.
  bool ConverseBrom() {
   string line;
   if (VerdantHuntStage >= 1 && VerdantHuntStage <= 4)
    line = "Thornhide's hide turns a dull edge. Let it commit to the sweep, then strike while it recovers.";
   else if (UnlockedWeapons.Contains(17))
    line = "That Woodsman's Axe carries my mark. Chop low, let the weight do the work — and keep it out of the salt air.";
   else
    line = "Every blade I forge has a temper. Hold the heavy chops for the big ones — speed for everything else.";
   VillageBanner("Master Smith Brom", line);
   return true;
  }

  // Lorekeeper Lyra: mirrors the kingdom's real progress (crypt, guardian, relay).
  bool ConverseLyra() {
   string line;
   if (CryptCompleted)
    line = "The Sunken Crypt is silent and the Orrery aligned. Even the stars keep records, Aster — and I keep more.";
   else if (GuardianDefeated)
    line = "Heartwood sleeps at last. The First Wardens would call you one of their own.";
   else if (StormRelayAligned)
    line = "The Tempest Overlook hums tonight. Stormveil's conductor has found a steady hand at last.";
   else
    line = "Four Relic Shrines watch this kingdom, and the Sunken Crypt keeps their secrets below. Bring me their stories.";
   VillageBanner("Lorekeeper Lyra", line);
   return true;
  }

  // Apothecary Rowan: the hunting/cooking adviser — every line matches implemented mechanics.
  bool ConverseRowan() {
   string line;
   bool night = OpenWorldAtmosphere.Instance && OpenWorldAtmosphere.Instance.NightFactor > 0.5f;
   if (night)
    line = "Stay by the lanterns after dark — the graveyard dead walk, and no draught of mine mends broken bones.";
   else if (RawMeat > 0)
    line = "That raw meat will cook up nicely. Any campfire takes one piece and gives you a proper meal — hot food, full strength.";
   else
    line = "Hunter's counsel, free of charge: deer bolt the moment you sprint, so walk the last stretch. And raw meat is wasted uncooked — one piece per campfire meal.";
   VillageBanner("Apothecary Rowan", line);
   return true;
  }

  void VillageBanner(string speaker, string line) {
   ShowBanner(speaker.ToUpperInvariant() + ": \"" + line + "\"");
   Debug.Log("[Village] " + speaker + ": " + line);
  }
 }
}
