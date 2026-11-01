using UnityEngine;

namespace LostRealmsOW {
 // Cooking recipe variety (GLM): recipes cycle per campfire interact. Each recipe declares its own
 // costs — classic recipes use one raw meat; the two gathering recipes add wild herbs (herbal stew)
 // or replace meat entirely (aether tea, herbs only). CookMeal peeks the next recipe, gates on its
 // costs, then commits — an unaffordable recipe leaves the cycle pointer untouched.
 public static class OpenWorldCookingRecipes {
  public struct Recipe {
   public string Name, Banner;
   public float BuffDuration;
   public bool RefillStamina, RefillAether;
   public int MeatCost, HerbCost;
  }

  public static readonly Recipe[] Recipes = {
   new Recipe { Name = "HEARTY STEW", Banner = "COOKED HEARTY STEW! (+15% Attack & Stamina Buff, Full HP/Aether)", BuffDuration = 180f, RefillStamina = true, RefillAether = true, MeatCost = 1, HerbCost = 0 },
   new Recipe { Name = "HUNTER'S GRILL", Banner = "COOKED HUNTER'S GRILL! (+15% Attack & Stamina Buff, Full HP, Stamina Refilled)", BuffDuration = 240f, RefillStamina = true, RefillAether = false, MeatCost = 1, HerbCost = 0 },
   new Recipe { Name = "TRAIL SNACK", Banner = "COOKED TRAIL SNACK! (+15% Attack & Stamina Buff, Full HP, Aether Refilled)", BuffDuration = 120f, RefillStamina = false, RefillAether = true, MeatCost = 1, HerbCost = 0 },
   new Recipe { Name = "HERBAL STEW", Banner = "COOKED HERBAL STEW! (Full HP/Aether/Stamina, 300 s Buff — herbs did that)", BuffDuration = 300f, RefillStamina = true, RefillAether = true, MeatCost = 1, HerbCost = 1 },
   new Recipe { Name = "AETHER TEA", Banner = "AETHER TEA! (Aether Restored — brewed from wild herbs)", BuffDuration = 0f, RefillStamina = false, RefillAether = true, MeatCost = 0, HerbCost = 2 },
  };

  // The recipe the next cook at this campfire would use, without advancing the cycle.
  public static Recipe Peek(OpenWorldCampfire fire, OpenWorldManager gm) {
   int index = Mathf.Abs(fire.GetEntityId().GetHashCode() + gm.CookCounter) % Recipes.Length;
   return Recipes[index];
  }

  // Commits the cycle advance after CookMeal consumed the peeked recipe's costs.
  public static void Advance(OpenWorldManager gm) {
   gm.CookCounter++;
  }
 }

 public sealed partial class OpenWorldManager {
  // Per-manager cooking cycle counter (shared across campfires — each cook advances the menu).
  public int CookCounter;
 }
}
