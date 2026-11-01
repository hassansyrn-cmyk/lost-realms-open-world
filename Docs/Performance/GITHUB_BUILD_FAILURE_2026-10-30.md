# GitHub Android build failure — 2026-10-30

Codex investigation, Asia/Dubai. No workflow/gameplay changes and no build rerun.

## Run and timing
- Run #8: https://github.com/hassansyrn-cmyk/lost-realms-open-world/actions/runs/41039469620
- Job: https://github.com/hassansyrn-cmyk/lost-realms-open-world/actions/runs/41039469620/job/124008423786
- Manual dispatch at commit 681ec5267264fc61cb67f18ab7f315b2741ae4e1, before the recent Codex pushes.
- Run: 10:15:57–11:11:13 UTC / 14:15:57–15:11:13 Dubai, about 55m16s.
- Unity step: 10:20:17–11:11:09 UTC, about 50m52s.

## Confirmed evidence
- Checkout and Git LFS checkout succeeded.
- Library cache missed both exact and fallback keys at 10:20:17.
- Runner reported 15983 MB memory. Cleanup explicitly ran swapoff and removed swap storage; free output shows Swap 0B.
- Disk after cleanup: 108 GiB free. No disk-full log matches; no final disk telemetry available.
- Log contains 20,298 asset-import entries, including large vendor textures. Unity still imported textures immediately before termination.
- At 11:10:51.637 UTC the last logged import was Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Sky/Skyboxes/UNS_HDRI.hdr. This does NOT prove that asset is faulty; it is only the last visible operation.
- At 11:11:02.028 UTC: Killed. At 11:11:02.081: Build failed, with exit code 137. GitHub annotation repeats that code.
- No error CS compiler diagnostics found. No ordinary Android build-result failure message found; no successful APK produced/uploaded.
- Post Cache Library was skipped after failure, so this failed attempt did not seed the cache for the next run.
- Node/tool-cache deprecation notices and Ubuntu migration notice are warnings, not the observed termination.

## Diagnosis and limits
Exit 137 and Killed indicate SIGKILL termination. Memory exhaustion during a cold asset import is the leading explanation given ~16 GB RAM, zero swap and broad asset import. Kernel OOM/cgroup memory events and peak RSS were not captured, so OOM is strongly suspected, not independently proven. There is no evidence this was a 55-minute timeout or a gameplay compiler failure. The recent bear payload reduction was not present in this run, and does not guarantee the next import will fit.

## Recommended next change before retrying
1. Stop deleting swap; preserve or explicitly provision a bounded swap buffer with disk headroom. Swap can prevent abrupt memory kills but may slow import. More RAM is preferable if available; do not imply a specific runner size guarantees success without measurement.
2. Add periodic RAM/swap/disk diagnostics plus cgroup memory.events / OOM status when available, and upload diagnostic logs on failure. Do not dump credentials or Photon settings.
3. Separate cache restore/save, saving a valid partial Library after failure where appropriate, with keys including Unity version, target and dependency changes. An always-run save must not blindly cache corrupt/incomplete output or mask the primary failure.
4. If needed, measure and bound import/worker concurrency; the log already shows one named asset-import worker, so do not assume worker reduction alone solves this failure.
5. Retry only after the workflow safeguards are reviewed. No retry launched in this investigation.

References: https://docs.docker.com/engine/containers/resource_constraints/ (OOM and swap); https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor (worker controls).

Raw job log retrieved through GitHub REST with the existing Git credential held in memory, after connector log calls failed. Stored locally under ignored Temp/failed-build-41039469620.zip; despite the extension, the job-log endpoint returns plain UTF-8 text, not a ZIP. Do not publish the full raw log without redaction; licensing output may contain session metadata. Report intentionally includes only the relevant diagnostics.