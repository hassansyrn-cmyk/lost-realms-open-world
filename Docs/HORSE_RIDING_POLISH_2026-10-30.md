# Horse riding polish — 30 October 2026

Codex follow-up to HORSE_EXTRAS_2026-10-30.md. Completed after reading shared claims; no Android build.

## Changes

- OpenWorldImportedModel: banked gallop horizontal contact now continues while the toe is within 0.12 m of its calibrated contact level (previously 0.035 m). Preserve the authored vertical landing/lift motion during correction instead of forcing the whole contact phase to a fixed height. Existing contact phases, turn hysteresis, natural speeds, stride phase and bounded joint limits are unchanged. Walk/straight gaits are unchanged.
- OpenWorldAnimalInteraction: fixed a whistle-to-mount prompt gap discovered by testing. A summoned horse stopped Idle at 3.07 m, while Nearest only searched 3.0 m despite Interact accepting 3.2 m. Horse prompt reach now shares the existing 3.2 m action range. Non-horse prompt reach stays 3.0 m. Final test confirms the prompt and successful normal interaction at 3.08 m.
- HorseExtraClipsValidation: added buffered per-hoof phase/height/speed/contact CSV traces, held-stick and sprint checks, sharp reversal/braking, temporary obstacle collision, real-terrain uphill/downhill travel, normal dismount/glider state and whistle arrival/prompt/remount. Nested iterator failures now dispose the temporary fixtures and release Busy. A focused Riding entry point avoids repeating gait measurements when checking an unrelated interaction fix.

Only these three source files changed. No vendor, clip, prefab, preparation, Photon, sibling-project or Claude-reserved performance/streaming/Addressables changes. Captures, timestamped raw reports and append-only DEVELOPMENT_LOG.md accompany this handoff. No commit or push performed.

## Actual results

All figures are from scripted inputs through real gameplay controllers in Unity Editor, with visual review of captures. They are not a human playtest or phone performance result.

Gallop skate measured with ClaudeReviewChecks.SkateStart/SkateReport:

| Measurement | Prior 0286 left/right | Repeat 0289 left/right |
|---|---:|---:|
| Hoof within 3 cm of its lowest point | 10% / 9% | 5% / 4% |
| Broader lowest 30% of hoof height | 32% / 23% | 16% / 12% |

Percentages mean hoof horizontal speed divided by body speed. First modified run 0288 measured 1%/1% near stance and 14%/11% broad; retain repeat variability rather than quoting only the best result. Walk near-stance remained 6%/6% in 0289. Broader gallop slip is approximately halved, not eliminated or certified below 10% across the full window.

0288/0289 verified all four turn banks, all eight clips and shared natural speeds, hysteresis, first wild buck/control lock, gallop release to RunStop then idle, and tame remount without another buck. Discovery interaction regressions also passed. 0289 passed held walking, sprint, sharp reversal and obstacle braking, then its too-local slope search failed; no later checks from that run are claimed.

0290 widened the slope search, passed terrain traversal and dismount, then exposed the real horse prompt gap. Its strict whistle test correctly FAILED before the range fix. Failure cleanup completed immediately.

Final focused 0291 passed:

- Wild mount/buck/control lock; holding a still stick keeps walking; held sprint reaches gallop.
- Reverse from 10.08 m/s slowed to 2.21 m/s before finishing at 180 degrees.
- Solid temporary wall blocked the horse and reduced speed; wall removed during cleanup.
- Dry slope lane (-71.75, 7.41, -175.50) to (-64.25, 9.40, -188.50), total rise 1.99 m over 15 m.
- Uphill: travelled 8.09 m, gained 1.12 m, end speed 3.02 m/s. Downhill: 8.14 m, lost 1.07 m, end speed 4.42 m/s.
- Normal dismount restored foot controller, finished the hop and left no glider.
- Whistle invoked actual Summoned behaviour, horse arrived Idle at 3.08 m, Nearest selected it, and normal Interact mounted Aster without another buck.

Evidence: HORSE_EXTRAS_DIAG_20261030_123953.txt and its CSV traces (0287 diagnostic); HORSE_EXTRAS_20261030_124345.txt (0288); HORSE_EXTRAS_20261030_124816.txt (0289); HORSE_EXTRAS_20261030_125335.txt (0290); HORSE_EXTRAS_20261030_125727.txt (final 0291).

Reviewed captures in ClaudeBridge/shots: seq_extra_gallop_l.jpg, seq_extra_gallop_r.jpg, horse_polish_obstacle.png, horse_polish_uphill.png, horse_polish_downhill.png and horse_polish_whistle.png. The final whistle capture visibly shows Ride Horse. No leg-chain inversion observed in the reviewed gallop sheets.

Latest runtime and editor compilation at 12:56:08: zero errors. Source-only git diff --check passed. Final 0291 cleanup completed, Play stopped, AllowAutoRefresh paired with the temporary test guard; editor idle, userplay=False, console=ok. The save-unavailable banner in QA captures comes from the intentional protected save snapshot; progress was restored afterward.

## Limits and next work

Broader gallop entry/exit slip still measures 12–16% in the repeat. Wider terrain, steep slopes, obstruction layouts, offscreen animation culling, human feel and Android/device coverage remain unverified. Swimming/jumping traversal remains disabled as before.

The previously logged Population.Spawn invalid-operation-handle exception reproduced after 0287 cleanup at 12:41:08, in the reserved World/OpenWorldPopulation.cs line 398 / Update line 306. It remains reported for Claude; not fixed in this task. Final 0291 console tail contained the known graphicsApiMask/EnvironmentDependencyValues import noise, without a new gameplay exception.

Next independent content task: run GLM's pending regional-discovery runtime validation. Coordinate performance work with Claude before editing his claimed files.
