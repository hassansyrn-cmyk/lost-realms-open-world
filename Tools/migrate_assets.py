#!/usr/bin/env python3
"""Phase 1 asset migration for Lost-Realms-Open-World.

Copies a chosen subset of the ORIGINAL project's Assets into the new project and
follows asset GUID references so every texture / material / mesh a copied prefab
needs comes along. The original project is opened READ-ONLY (never written).

Each asset is copied together with its .meta so GUIDs are preserved.

Usage:
  python Tools/migrate_assets.py --dry-run      # report only
  python Tools/migrate_assets.py                # copy
"""
import argparse, os, re, shutil, sys, json

SRC = r"C:\Users\X1 YOGA\Documents\3D Lost Realms OpenCode\Legends-of-the-lost-realms\unity-3d\Assets"
DST = r"C:\Users\X1 YOGA\Documents\3D Lost Realms OpenCode\Lost-Realms-Open-World\Assets"

# Entries are relative to Assets/. A trailing "*" matches files by prefix inside the folder.
INCLUDE = [
    # Hero, skins, base model + Mixamo sources for the animation baker
    "Resources/Characters/Aster*", "Resources/Characters/Textures/Aster*",
    "Resources/Animations/Aster", "Resources/Animations/Shared",
    "Art/Models", "Art/Animations/AsterMixamo",
    # Starter enemy roster for the Verdant slice (kept lean; more regions later)
    "Resources/Characters/Goblin*", "Resources/Characters/IronGoblin*", "Resources/Characters/StoneBrute*",
    "Resources/Characters/Heartwood*", "Resources/Characters/Elite*", "Resources/Characters/Bomber*",
    "Resources/Characters/Caster*", "Resources/Characters/Textures/Goblin*",
    "Resources/Characters/Textures/IronGoblin*", "Resources/Characters/Textures/StoneBrute*",
    "Resources/Characters/Textures/Heartwood*", "Resources/Characters/Textures/Elite*",
    "Resources/Characters/Textures/Bomber*", "Resources/Characters/Textures/Caster*",
    "Resources/Animations/Goblin", "Resources/Animations/Elite", "Resources/Animations/Bomber",
    "Resources/Animations/StoneBrute*", "Resources/Animations/IronGoblin*",
    # Weapons: 12 representative starters (all 48 stay in the original until the loot phase)
    "Resources/Weapons/Aster_Axe*", "Resources/Weapons/Aster_CurvedSword*", "Resources/Weapons/AxeBattle*",
    "Resources/Weapons/Duskblade*", "Resources/Weapons/OrnateCurvedBlade*", "Resources/Weapons/PureSword1H*",
    "Resources/Weapons/PureSpear*", "Resources/Weapons/SwordShort*", "Resources/Weapons/FrostHalberd*",
    "Resources/Weapons/Soulreaper*", "Resources/Weapons/PureHammer*", "Resources/Weapons/RangerAxe*",
    "Resources/Weapons/Textures",
    # World dressing
    "Resources/Props/Nature", "Resources/Props/Village", "Resources/Props/Traps", "Resources/Props/Crypt",
    "Resources/Props/Expansion", "Resources/Props/Textures", "RPGPP_LT", "SimpleNaturePack",
    "Resources/Islands/Island_Meadow*", "Resources/Islands/Island_Plateau*", "Resources/Islands/Island_Crag*",
    "Resources/Islands/Island_Small*", "Resources/Islands/Textures",
    "Resources/Gates", "Resources/Pickups", "Resources/Relics",
    "Resources/Art/Realm0_Sky360*", "Resources/Art/verdant_moss_tile*",
    # VFX / audio / UI
    "Resources/VFX", "Eric VFX Studio", "Free Slash VFX", "GabrielAguiarProductions",
    "Resources/Audio", "Resources/UI",
    # Editor tooling that bakes Aster's clips (guardrail: regenerate via the Phase 1 menu)
    "Editor/AsterPhase1.cs*", "Editor/HumanoidRig.cs*",
    "Art/Materials",
]

# Never copy these (heavy source packs, scripts, scene, other realms' content).
EXCLUDE_PARTS = ["/Scripts/", "/Scenes/", "/_Recovery/", "/Art/LavaBoss", "/Art/Stormveil",
                 "/Art/Astral", "/Resources/Stormveil", "/Resources/Astral"]

GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})")
TEXTUAL = {".prefab", ".mat", ".anim", ".controller", ".asset", ".overridecontroller", ".mask", ".physicmaterial"}


def norm(p):
    return p.replace("\\", "/")


def expand(entry):
    import glob
    pat = os.path.join(SRC, entry.replace("/", os.sep))
    hits = glob.glob(pat) if "*" in entry else ([pat] if os.path.exists(pat) else [])
    out = []
    for h in hits:
        if h.endswith(".meta"):
            continue
        out.append(h)
        # a prefix glob like "Aster*" also matches siblings of the same stem (e.g. Aster.fbx.meta handled below)
    return out


def files_under(path):
    if os.path.isfile(path):
        yield path
        return
    for root, _, names in os.walk(path):
        for n in names:
            if not n.endswith(".meta"):
                yield os.path.join(root, n)


def excluded(path):
    p = "/" + norm(os.path.relpath(path, SRC)) + "/"
    return any(x in p for x in EXCLUDE_PARTS)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--max-gb", type=float, default=2.0, help="abort if the copy set exceeds this size")
    ap.add_argument("--no-closure", action="store_true")
    args = ap.parse_args()

    # 1. GUID index of the ORIGINAL project (read-only)
    guid_to_path = {}
    for root, _, names in os.walk(SRC):
        for n in names:
            if n.endswith(".meta"):
                full = os.path.join(root, n)
                try:
                    with open(full, "r", encoding="utf-8", errors="ignore") as f:
                        m = GUID_RE.search(f.read(600))
                except OSError:
                    continue
                if m:
                    guid_to_path[m.group(1)] = full[:-5]

    # 2. Seed set
    chosen = set()
    for entry in INCLUDE:
        hits = expand(entry)
        if not hits:
            print("WARN no match:", entry)
        for h in hits:
            for f in files_under(h):
                if not excluded(f):
                    chosen.add(f)

    # 3. GUID closure: textures/materials/meshes referenced by prefabs, materials, fbx metas, anims
    skipped_refs = {}
    if not args.no_closure:
        queue = list(chosen)
        while queue:
            f = queue.pop()
            ext = os.path.splitext(f)[1].lower()
            texts = []
            if ext in TEXTUAL:
                texts.append(f)
            if ext not in (".meta", ".cs"):
                if os.path.exists(f + ".meta"):
                    texts.append(f + ".meta")  # FBX metas hold material/texture remaps
            for t in texts:
                try:
                    with open(t, "r", encoding="utf-8", errors="ignore") as fh:
                        data = fh.read()
                except OSError:
                    continue
                for g in set(GUID_RE.findall(data)):
                    dep = guid_to_path.get(g)
                    if not dep or dep in chosen:
                        continue
                    if dep.endswith(".cs") or excluded(dep):
                        skipped_refs.setdefault(os.path.relpath(dep, SRC), []).append(os.path.relpath(f, SRC))
                        continue
                    if os.path.isdir(dep):
                        continue
                    chosen.add(dep)
                    queue.append(dep)

    total = sum(os.path.getsize(f) + (os.path.getsize(f + ".meta") if os.path.exists(f + ".meta") else 0) for f in chosen)
    print("files: %d   size: %.2f GB" % (len(chosen), total / 1024 ** 3))
    by_top = {}
    for f in chosen:
        rel = norm(os.path.relpath(f, SRC)).split("/")
        key = "/".join(rel[:2]) if rel[0] == "Resources" else rel[0] if len(rel) == 1 else "/".join(rel[:2]) if rel[0] == "Art" else rel[0]
        by_top[key] = by_top.get(key, 0) + os.path.getsize(f)
    for k, v in sorted(by_top.items(), key=lambda kv: -kv[1]):
        print("  %-38s %8.1f MB" % (k, v / 1024 ** 2))
    if skipped_refs:
        print("references NOT copied (scripts / excluded areas):")
        for k, v in sorted(skipped_refs.items())[:25]:
            print("  ", k, "<-", v[0], "(+%d)" % (len(v) - 1))

    if total / 1024 ** 3 > args.max_gb:
        print("ABORT: exceeds --max-gb %.1f" % args.max_gb)
        sys.exit(2)
    if args.dry_run:
        return

    # 4. Copy (files + .meta + every parent folder .meta so folder GUIDs persist)
    copied = 0
    for f in sorted(chosen):
        rel = os.path.relpath(f, SRC)
        dst = os.path.join(DST, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        if not os.path.exists(dst):
            shutil.copy2(f, dst)
            copied += 1
        if os.path.exists(f + ".meta") and not os.path.exists(dst + ".meta"):
            shutil.copy2(f + ".meta", dst + ".meta")
        parent = os.path.dirname(f)
        while os.path.normpath(parent) != os.path.normpath(SRC):
            pm = parent + ".meta"
            dm = os.path.join(DST, os.path.relpath(parent, SRC)) + ".meta"
            if os.path.exists(pm) and not os.path.exists(dm):
                os.makedirs(os.path.dirname(dm), exist_ok=True)
                shutil.copy2(pm, dm)
            parent = os.path.dirname(parent)
    manifest = os.path.join(os.path.dirname(DST), "Docs", "phase1-migration-manifest.json")
    os.makedirs(os.path.dirname(manifest), exist_ok=True)
    with open(manifest, "w", encoding="utf-8") as fh:
        json.dump(sorted(norm(os.path.relpath(f, SRC)) for f in chosen), fh, indent=0)
    print("copied %d new files. manifest: %s" % (copied, manifest))


if __name__ == "__main__":
    main()
