# QA suite — how to rerun

The suite lives in `Assets/Editor/GlmQaSuite.cs` (GLM/ZCode). It is driven entirely through the
ClaudeBridge, so any agent can rerun it without touching the editor by hand.

## What it does

Play-mode tour of 10 stops — hub, Oakvale, Mirror Lake, Frostpine, Frozen Lake, Sunspire, Dune Ruins,
Hollow Marches graveyard, Stormveil overlook, sky isle ("North Pass" has no POI in code, so Frozen Lake
stands in). At each stop: 3 s settle for streaming, 3 s of wall-clock frame-time sampling (average,
P95, shortest/longest), one-shot `ProfilerRecorder` render counters (SetPass calls, triangles; the
draw-call counter reads 0 in this editor build), live enemy/creature/NPC counts, a screenshot to
`ClaudeBridge/shots/glmqa_<tier>_<stop>.jpg`, and exception capture with the stop where each happened
(known vendor noise — Fusion import worker, graphicsApiMask/EnvironmentDependencyValues, NetworkPeer,
ADTM — is filtered). One bridge job per tier; results land as `Docs/QA/qa_results_<tier>.txt`
fragments that `WriteReport` merges into `QA_BASELINE_2026-10-29.md`.

## Rerun (two jobs, ~2 min each)

1. Find the highest job number in `ClaudeBridge/inbox/` and `ClaudeBridge/done/`, then queue a job
   named `NNNN_glm_qa_balanced.txt` with:

```
log NNNN QA tour, Balanced tier
refresh
wait 2
play
waitplay 10
timeofday 10:00 lock
call LostRealmsOW.OpenWorldHero.DebugAction god
call LostRealmsOW.OpenWorldGraphics.DebugTier balanced
wait 2
call LostRealmsOW.GlmQaSuite.BeginRun balanced
waitfor !LostRealmsOW.GlmQaSuite.Busy 240
stop
log NNNN done
```

2. Repeat with `high` in place of `balanced` (job name `NNNN_glm_qa_high.txt`), plus one extra line
   before `stop`: `exec LostRealmsOW.GlmQaSuite.WriteReport` to merge both tiers into the baseline
   markdown.

Notes:
- The tier call must be `call` (with argument), not `exec` — `exec` cannot pass arguments and the
  tour would silently run at the desktop default tier.
- Keep `timeofday 10:00 lock` so lighting is comparable between runs.
- The suite writes `Docs/QA/qa_results_<tier>.txt` fragments; delete a fragment before rerunning that
  tier if you want the report to show only the newest run.
- Frame times are editor play-mode times on the desktop. They are a regression baseline and the
  reference for the Phase 4 Android pass, not device numbers.
