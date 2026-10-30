# Combat button and summon cleanup — 30 October 2026

- Element, Grapple, Lock and Summon now use an icon-free navy circle with a gold rim. Their dynamic glyph remains the only symbol; other action buttons retain their existing single-icon artwork.
- Combat caption rectangles fit within the circular boundary and use bounded text fitting. Lock/Grapple are 76 canvas units for legibility without changing their centres or callbacks.
- ECHO is labelled SUMMON, including the guardian callout. Existing input/cooldown plumbing remains the same.
- The guardian is normalised to 3.5 m at its initial pose, spawns 2.7 m ahead to give the larger body room, and retains that scale until its existing fade-out. Damage/radius/cooldown unchanged.

Files: OpenWorldMobileHud.cs (ActionButton only), OpenWorldGenshinZeldaSystems.cs (OpenWorldEchoSpirit only), Editor/CombatButtonValidation.cs. No vendor art edits or baked-animation edits.

Actual verification results are in UI_BUTTONS_2026-10-30.txt; screenshots button_cleanup_hud.png and button_cleanup_summon.png under ClaudeBridge/shots. No Android build/device test in this pass.

