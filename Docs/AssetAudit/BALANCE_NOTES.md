# Imported weapon and reward balance check

Scope: OpenWorldWeaponsAndLoot.cs, OpenWorldManager.cs, OpenWorldHero.cs, OpenWorldVillagesAndNPCs.cs and OpenWorldGenshinZeldaSystems.cs. Docs/GAME_PLAN.md supplies direction but no numeric balance targets. Verdict: CONCERNS pending combat playtesting; the identified catalogue-growth regression is corrected.

The Epic Vault originally selected weapon IDs 6–10: three rare and two epic. With the unchanged 40% weapon branch, the epic-weapon probability was 16% per paid opening. Appending five rare discovery weapons to All.Length silently reduced it to 8%. An explicit original pool restores 16%; the new five elven weapons are world discoveries. Skin and consolation branches and prices are unchanged.

The elven hammer originally exceeded Crusher Hammer damage, reach and tempo with the same rarity/element. Its reach is now .88 versus Crusher's .92, trading range for slightly higher damage and tempo. Other additions bridge early exploration and existing quest rewards; the Ornate Curved Blade remains a stronger later Gale blade. A damage-times-tempo comparison is only a screening heuristic: actual DPS also depends on animation duration caps, contact timing, third-hit bonuses, counterstrikes, elements and rank.

Gold chests remain sinks on the direct consolation branch: expected net gold per opening is -23.75 for common and -98.75 for rare, excluding weapon/skin value; gem consolation is .75 and 1.1 respectively. Epic costs 8 gems and returns .88 expected gems through consolation, excluding item value. Repeat NPC/cooking first-reward farming is closed by saved IDs. Normal enemy farming is a renewable gameplay faucet and still needs encounter pacing measurements.

Remaining tuning: distinct polearm combo motions and contact-frame timing, weapon grips, duplicate reward compensation/pity, time-to-kill and acquisition pacing. No measured device combat balance claim is made. This check preserves existing economy behavior rather than redesigning it during asset integration.
