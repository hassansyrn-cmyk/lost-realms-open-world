# GLM / Antigravity integration review — 2026-11-01

Read-only source review by Codex; no gameplay fixes, Play-mode tests, APK builds or device checks run in this review.

P1 — Weapon shop empty on first open. World/OpenWorldWeaponShop.cs192–195 creates modalRoot active, then only calls RefreshModal inside !activeSelf. BuildModalUI never populates rows or deactivates the root. Fresh first opening therefore has no listings; closing/reopening populates it. Fix initial population and guard absent HUD before dereferencing modalRoot.

P1 — Forge purchases lack transaction-time validation. Lines461–470 deduct captured costs without checking balance, ownership, modal/player state at click time. RefreshModal destroys previous rows only at frame end. Two queued clicks on an affordable row can charge twice or drive gold/ore negative; negative balances then fail SaveStore.Validate. Use one validated purchase API, recheck every click, and disable committed buttons immediately. Debug Buy injects missing currency and therefore does not verify real affordability logic.

P2 — Workshop lifecycle uses static bool only. workshopSpawned is set once, never reset and never tied to a live root/manager. Scene recreation with statics retained removes WorldRoot but suppresses workshop recreation. Track actual instance and reset appropriately; test new game/reload/Play configurations.

P2 — Slain elite suppression runs after spawning on a10-second global scan. Population can instantiate previously completed elites alive and hostile for up to10seconds after entering a region or loading. Suppress at population spawn eligibility rather than after the fact. Current reward flags protect repeat payout, but do not guarantee completed encounters remain absent.

Evidence limitations: latest recorded compiler success22:03 only, not an independent compile of all current untracked content. GLM explicitly leaves WANTED text invisible/unconfirmed. Force-pay bounty tests do not establish actual kill hooks for every elite. Merchant/gathering/cooking verification does not cover full old/new save reload, rapid HUD clicks or phone lifecycle. Jump source now bounds hits beneath feet and handles terrain separately, but previous repeated Editor passes failed phone testing; require a new physical mainland/isles regression. Parry baker source changed through permitted pipeline; per-foot Y at0.15s alone is not full motion verification.

Positive integration: herbs/ore additive save fields validated0–999; once-only elite reward flags; merchant avoids generic free-heal path; both new modals included in IsModalOpen/CloseMenus; no extra source asset deletion needed.

Recommended order: shop initialization + atomic purchases, workshop/elite lifecycle, then device jump/parry and shop regression before more content or publishing. No agents contacted or new tasks dispatched during review.
