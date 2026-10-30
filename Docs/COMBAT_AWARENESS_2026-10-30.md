# Combat awareness — 30 October 2026

Codex gameplay enhancement. The remaining performance investigation is reserved for Claude; GLM is handling the credits screen.

## Player behavior

Nearby enemies winding up outside the camera view produce an amber directional warning around the center of the HUD. The chevron points toward the enemy relative to the camera, including attacks from behind. Turning to face the attacker removes the extra warning; existing world-space telegraphs remain responsible for visible attacks.

The HUD considers the three nearest eligible windups. Close directions share one cue rather than stacking circles. Maximum warning range is 12–24 metres according to the attacker's existing range. Warnings stop when an attack is interrupted, its actor dies or becomes inactive, it moves out of range, or a menu/title/death screen is open. These are direction cues, not a guarantee that an attack will hit; they do not distinguish blockable/unblockable attacks or detect wall occlusion.

The three indicators are reused, respect safe-area sizing, and do not intercept taps or dragging. No attack timing, enemy AI, damage, camera behavior or saved progression is changed.

## Implementation

- New Assets/Scripts/OpenWorldCombatAwarenessHud.cs: independent HUD partial and adapter, camera-relative direction helper, bounded nearest-target selection and non-overlapping reusable cues.
- New Assets/Editor/CombatAwarenessValidation.cs: protected adventure snapshot, deterministic windup fixtures, direction and lifecycle checks. The fixtures deliberately hold windup state; this is not a human difficulty playtest.
- Existing HUD/horse/credits/Manager/performance sources remain untouched.

## Checks

Final results and completion state are recorded in Docs/DEVELOPMENT_LOG.md. Detailed assertions are in Docs/COMBAT_AWARENESS_2026-10-30.txt, and the final preview is ClaudeBridge/shots/combat_awareness.png. Android builds and physical touch testing remain deferred by the user.

## Claude performance handoff

Read Docs/LIVING_VERDANT_2026-10-30.md and Docs/Performance/Codex_Living_final.md before starting. Remaining issue: 180 ms desktop frame outlier despite reduced scoped streaming costs. Five sampled native markers do not explain the largest spikes; two smaller stalls coincide with graphics-thread waits. Capture a full CPU/render-thread timeline before attributing a cause. This pass does not alter those streaming paths.
