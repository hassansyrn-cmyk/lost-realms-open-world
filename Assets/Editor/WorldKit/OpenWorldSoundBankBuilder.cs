using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Builds Assets/Resources/World/SoundBank.asset: references (not copies) of vendor clips grouped by
 // purpose so the runtime soundscape can pick surface footsteps, birds and fire loops by name.
 public static class OpenWorldSoundBankBuilder {
  const string BankPath = "Assets/Resources/World/SoundBank.asset";
  const string Steps = "Assets/Footsteps - Essentials";

  static readonly (string group, string folder, string include, string exclude)[] Rules = {
   ("step_grass_walk", Steps + "/Footsteps_Grass/Footsteps_Grass_Walk", "", ""),
   ("step_grass_run", Steps + "/Footsteps_Grass/Footsteps_Grass_Run", "", ""),
   ("step_grass_land", Steps + "/Footsteps_Grass/Footsteps_Grass_Jump", "Land", ""),
   ("step_leaves_walk", Steps + "/Footsteps_Leaves/Footsteps_Leaves_Walk", "", ""),
   ("step_leaves_run", Steps + "/Footsteps_Leaves/Footsteps_Leaves_Run", "", ""),
   ("step_dirt_walk", Steps + "/Footsteps_DirtyGround/Footsteps_DirtyGround_Walk", "", ""),
   ("step_dirt_run", Steps + "/Footsteps_DirtyGround/Footsteps_DirtyGround_Run", "", ""),
   ("step_dirt_land", Steps + "/Footsteps_DirtyGround/Footsteps_DirtyGround_Land", "", ""),
   ("step_gravel_walk", Steps + "/Footsteps_Gravel/Footsteps_Gravel_Walk", "", ""),
   ("step_gravel_run", Steps + "/Footsteps_Gravel/Footsteps_Gravel_Run", "", ""),
   ("step_rock_walk", Steps + "/Footsteps_Rock/Footsteps_Rock_Walk", "", ""),
   ("step_rock_run", Steps + "/Footsteps_Rock/Footsteps_Rock_Run", "", ""),
   ("step_rock_land", Steps + "/Footsteps_Rock/Footsteps_Rock_Jump", "Land", ""),
   ("step_sand_walk", Steps + "/Footsteps_Sand/Footsteps_Sand_Walk", "", ""),
   ("step_sand_run", Steps + "/Footsteps_Sand/Footsteps_Sand_Run", "", ""),
   ("step_sand_land", Steps + "/Footsteps_Sand/Footsteps_Sand_Jump", "Land", ""),
   ("step_snow_walk", Steps + "/Footsteps_Snow/Footsteps_Snow_Walk", "", ""),
   ("step_snow_run", Steps + "/Footsteps_Snow/Footsteps_Snow_Run", "", ""),
   ("step_snow_land", Steps + "/Footsteps_Snow/Footsteps_Snow_Jump", "Land", ""),
   ("step_tile_walk", Steps + "/Footsteps_Tile/Footsteps_Tile_Walk", "", ""),
   ("step_tile_run", Steps + "/Footsteps_Tile/Footsteps_Tile_Run", "", ""),
   ("step_water_walk", Steps + "/Footsteps_Water/Footsteps_Water_Walk", "", ""),
   ("step_water_run", Steps + "/Footsteps_Water/Footsteps_Water_Run", "", ""),
   ("step_water_land", Steps + "/Footsteps_Water/Footsteps_Water_Jump", "", ""),
   ("step_wood_walk", Steps + "/Footsteps_Wood/Footsteps_Wood_Walk", "", ""),
   ("step_wood_run", Steps + "/Footsteps_Wood/Footsteps_Wood_Run", "", ""),
   ("step_mud_walk", Steps + "/Footsteps_Mud/Footsteps_Mud_Walk", "", ""),
   ("step_mud_run", Steps + "/Footsteps_Mud/Footsteps_Mud_Run", "", ""),
   ("birds", "Assets/living birds/sounds", "", "crow|flyAway"),
   ("crows", "Assets/living birds/sounds", "crow", ""),
   ("bird_flyaway", "Assets/living birds/sounds", "flyAway", ""),
   ("fire_small", "Assets/Vefects/Free Fire VFX URP/Audio", "Small", ""),
   ("fire_medium", "Assets/Vefects/Free Fire VFX URP/Audio", "Medium", ""),
   ("fire_big", "Assets/Vefects/Free Fire VFX URP/Audio", "Big", ""),
   ("magic_fire", "Assets/SFX Packs/General RPG SFX Pack/Magic SFX", "Fire", ""),
   ("magic_water", "Assets/SFX Packs/General RPG SFX Pack/Magic SFX", "Water", ""),
   ("magic_wind", "Assets/SFX Packs/General RPG SFX Pack/Magic SFX", "Wind", ""),
   // Physics pass: water entries and swim strokes, smashed and knocked props, bodies hitting the ground.
   ("splash_big", Steps + "/Footsteps_Water/Footsteps_Water_Jump", "Big", ""),
   ("splash_light", Steps + "/Footsteps_Water/Footsteps_Water_Jump", "Light", ""),
   ("prop_smash", "Assets/Deadly Kombat Free version", "wood_bat", ""),
   ("prop_knock", "Assets/Epic Game Hits SFX", "Wooden", ""),
   ("body_thud", "Assets/Deadly Kombat Free version", "body_hit_large", ""),
  };

  [MenuItem("Lost Realms OW/Audio/Build Sound Bank")]
  public static void Build() {
   var bank = AssetDatabase.LoadAssetAtPath<OpenWorldSoundBank>(BankPath);
   bool create = !bank;
   if (create) bank = ScriptableObject.CreateInstance<OpenWorldSoundBank>();
   var groups = new List<OpenWorldSoundBank.Group>();
   var log = new StringBuilder();
   foreach (var rule in Rules) {
    if (!AssetDatabase.IsValidFolder(rule.folder)) { log.AppendLine("missing folder " + rule.folder); continue; }
    var excludes = string.IsNullOrEmpty(rule.exclude) ? new string[0] : rule.exclude.Split('|');
    var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { rule.folder })
     .Select(AssetDatabase.GUIDToAssetPath)
     .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == rule.folder)
     .Where(p => string.IsNullOrEmpty(rule.include) || Path.GetFileName(p).IndexOf(rule.include, System.StringComparison.OrdinalIgnoreCase) >= 0)
     .Where(p => !excludes.Any(x => Path.GetFileName(p).IndexOf(x, System.StringComparison.OrdinalIgnoreCase) >= 0))
     .OrderBy(p => p)
     .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p))
     .Where(c => c)
     .ToArray();
    groups.Add(new OpenWorldSoundBank.Group { name = rule.group, clips = clips });
    log.AppendLine(rule.group + ": " + clips.Length);
   }
   bank.groups = groups.ToArray();
   Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
   if (create) AssetDatabase.CreateAsset(bank, BankPath);
   EditorUtility.SetDirty(bank);
   AssetDatabase.SaveAssets();
   Debug.Log("[SoundBank]\n" + log);
   try {
    string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "out"));
    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "soundbank.txt"), log.ToString());
   } catch { }
  }
 }
}
