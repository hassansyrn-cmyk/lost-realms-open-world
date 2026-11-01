# Addressables reduction — 2026-10-30

Android content rebuild succeeded at18:06:07 Dubai. Prior existing Duplicate Asset Isolation groups (17:18/17:20) already promoted all125 candidates; Codex added no assets and verified their result with a fresh build. Original vendor/model files preserved.

Before:485190340bytes,125duplicates (buildlayout_2026.10.30.16.21.03.json). After:310510277bytes,1duplicate (buildlayout_2026.10.30.18.03.43.json). Compressed bundle saving174680063bytes =166.59MiB,36.00%. Unity254.64MB estimate was not the measured compressed APK saving.

Remaining duplicate: Packages/com.unity.shadergraph/Editor/Resources/Shaders/FallbackError.shader. Left intact. Content build had no BuildError. No new APK produced in this pass; final APK size and phone loading/memory tests pending. Existing shared group packing may retain shared textures longer; no device residency claim.

Added Editor/AddressablesDuplicateAudit.cs audit/rebuild helper; removed only its empty Shared_Deduplicated group. Its older report path is explicit; review/update before reusing after future content changes. Raw layout reports are in Library and may be cleared by Unity; these metrics preserve the comparison. All125 reviewed candidate paths are in ADDRESSABLES_DEDUP_CHANGES.txt.
