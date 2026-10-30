using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LostRealmsOW {
 [Serializable]
 public sealed class OpenWorldSaveData {
  public int rawMeat;
  public int gold = 120, gems = 10, weapon, skin, element, sanctumFoci, relics, bounties, cryptMask;
  public bool questStarted, guardianAwakened, guardianDefeated, questCompleted, sanctumCompleted, stormRelay;
  public string checkpoint = "hub";
  public string[] collected = Array.Empty<string>();
  public string[] camps = Array.Empty<string>();
  public string[] gates = new[] { "hub" };
  public int[] weapons = new[] { 0, 1, 2 };
  public int[] skins = new[] { 0 };
 }

 // Alternating complete snapshots: an interrupted write cannot destroy the other slot.
 // Hashes detect truncation/corruption; they are not an anti-cheat mechanism.
 public sealed class OpenWorldSaveStore {
  [Serializable]
  sealed class Envelope {
   public string format = "LostRealmsOW";
   public int version = 1;
   public long revision;
   public string payload, hash;
  }
  readonly string directory;
  long revision;
  public bool RecoveredBackup { get; private set; }
  public OpenWorldSaveStore(string directory) { this.directory = directory; }
  string Slot(int index) => Path.Combine(directory, "adventure-" + index + ".json");

  public OpenWorldSaveData Load() {
   OpenWorldSaveData best = null;
   bool any = false, damaged = false;
   revision = 0;
   for (int i = 0; i < 2; i++) {
    string path = Slot(i);
    if (!File.Exists(path)) continue;
    any = true;
    try {
     var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
     if (envelope == null || envelope.format != "LostRealmsOW") throw new InvalidDataException();
     // Preserve newer save formats rather than replacing them with an older game's data.
     if (envelope.version > 1) throw new NotSupportedException("This adventure was saved by a newer game version.");
     if (envelope.version != 1 || envelope.revision <= 0 || string.IsNullOrEmpty(envelope.payload) ||
         envelope.hash != Hash(envelope.payload)) throw new InvalidDataException();
     var data = JsonUtility.FromJson<OpenWorldSaveData>(envelope.payload);
     Validate(data);
     if (envelope.revision > revision) { best = data; revision = envelope.revision; }
    } catch (NotSupportedException) { throw; }
    catch (Exception e) when (e is IOException || e is ArgumentException || e is FormatException) { damaged = true; }
   }
   if (any && best == null) throw new InvalidDataException("Both adventure save slots are unreadable. Existing files have been preserved.");
   RecoveredBackup = damaged && best != null;
   return best;
  }

  public void Write(OpenWorldSaveData data) {
   Validate(data);
   string payload = JsonUtility.ToJson(data);
   long next = checked(revision + 1);
   var envelope = new Envelope { revision = next, payload = payload, hash = Hash(payload) };
   byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
   Directory.CreateDirectory(directory);
   using (var stream = new FileStream(Slot((int)(next % 2)), FileMode.Create, FileAccess.Write, FileShare.None)) {
    stream.Write(bytes, 0, bytes.Length);
    stream.Flush(true);
   }
   revision = next;
  }

  static string Hash(string value) {
   using (var algorithm = SHA256.Create()) return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
  }
  static void Validate(OpenWorldSaveData data) {
   if (data == null || data.rawMeat < 0 || data.rawMeat > 999 || data.gold < 0 || data.gems < 0 || data.collected == null || data.camps == null ||
       data.gates == null || data.weapons == null || data.skins == null || string.IsNullOrEmpty(data.checkpoint) ||
       data.element < 0 || data.element > 2 || data.weapon < 0 || data.skin < 0 ||
       (data.questCompleted && (!data.guardianDefeated || !data.questStarted)) ||
       (data.guardianDefeated && !data.guardianAwakened) ||
       data.sanctumFoci < 0 || data.sanctumFoci > 7 ||
       data.relics < 0 || data.relics > 15 ||
       data.bounties < 0 || data.bounties > 7 ||
       data.cryptMask < 0 || data.cryptMask > 7 ||
       (data.sanctumCompleted && data.sanctumFoci != 7)) throw new InvalidDataException("Invalid adventure snapshot.");
  }
 }
}
