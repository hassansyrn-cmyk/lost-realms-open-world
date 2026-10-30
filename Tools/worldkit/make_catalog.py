#!/usr/bin/env python3
"""Generates Docs/AssetAudit/world-kit.json — the curated World Kit catalog.
Run from the project root. Vendor prefabs are referenced in place; the kit builder makes copies."""
import json, os, sys
E = []
def add(key, source, role, biomes="", tags="", **kw):
    e = {"key": key, "source": source, "role": role, "biomes": biomes, "tags": tags}
    e.update(kw)
    E.append(e)

IFN = "Assets/Idyllic Fantasy Nature/Prefabs/"
UNS = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/"
SC = "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/"
SYM = "Assets/Symphonie/StylizedForestKit Samples/URP/Prefabs/"
PL = "Assets/Polylised - Medieval Desert City/Prefabs/"
PR = "Assets/PolyRonin/Desert Village/Prefabs/"
FE = "Assets/FantasyEnvironments/Environments/Town/Prefabs/"
FEN = "Assets/FantasyEnvironments/Environments/Prefabs/"
DM = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/"
ING = "Assets/Inguz Media Studio/The Beauty Medieval War Banners and PROPS/Prefab/"
CEM = "Assets/CemeteryPack/Prefabs/"
RPG = "Assets/RPGPP_LT/Prefabs/"
VP = "Assets/VillagePack/"
Z_UP = [-90, 0, 0]

# ---------------- trees ----------------
for i in range(1, 6):
    add(f"tree_broadleaf_{i:02}", f"{IFN}BroadleafTree_0{i}_Green.prefab", "tree", "verdant", "broadleaf,forest", scaleMin=0.95, scaleMax=1.45, sink=0.12, weight=1.0)
    add(f"tree_blossom_{i:02}", f"{IFN}BlossomTree_0{i}.prefab", "tree", "verdant", "blossom,village,meadow", scaleMin=0.85, scaleMax=1.2, sink=0.1, weight=0.22)
    add(f"tree_willow_{i:02}", f"{IFN}WillowTree_0{i}_Green.prefab", "tree", "verdant,grave", "willow,water", scaleMin=0.95, scaleMax=1.35, sink=0.12, weight=0.6)
    add(f"tree_fir_{i:02}", f"{IFN}Fir_0{i}.prefab", "tree", "verdant,frozen,storm", "conifer,highland", scaleMin=0.9, scaleMax=1.5, sink=0.15, weight=0.8)
for i in (1, 3, 5):
    add(f"tree_autumn_{i:02}", f"{IFN}BroadleafTree_0{i}_Red.prefab", "tree", "verdant", "autumn,highland", scaleMin=0.95, scaleMax=1.35, sink=0.12, weight=0.18)
for i in (2, 4):
    add(f"tree_eerie_willow_{i:02}", f"{IFN}WillowTree_0{i}_Purple.prefab", "tree", "grave", "willow,eerie,water", scaleMin=1.0, scaleMax=1.4, sink=0.12, weight=0.7)
    add(f"tree_eerie_broadleaf_{i:02}", f"{IFN}BroadleafTree_0{i}_Purple.prefab", "tree", "grave", "eerie,forest", scaleMin=0.9, scaleMax=1.3, sink=0.12, weight=0.35)
    add(f"tree_storm_broadleaf_{i:02}", f"{IFN}BroadleafTree_0{i}_Blue.prefab", "tree", "storm", "stormtouched", scaleMin=0.8, scaleMax=1.2, sink=0.12, weight=0.3)
add("tree_spruce_tall", f"{UNS}Trees/Fir/Prefabs/UNS_Spruce_01.prefab", "tree", "frozen", "conifer,snow", height=13, scaleMin=0.85, scaleMax=1.35, sink=0.2, weight=1.2)
add("tree_spruce_young", f"{UNS}Trees/Fir/Prefabs/UNS_Spruce_02.prefab", "tree", "frozen", "conifer,snow,young", height=5.5, scaleMin=0.8, scaleMax=1.3, sink=0.12, weight=0.9)
add("tree_pine_giant", f"{SC}Pine_Tree.prefab", "tree", "frozen", "conifer,giant", height=19, scaleMin=0.85, scaleMax=1.25, sink=0.3, weight=0.45)
add("tree_cedar", f"{SC}Cedar_Tree_03.prefab", "tree", "frozen,verdant", "conifer,highland", height=13, scaleMin=0.85, scaleMax=1.25, sink=0.25, weight=0.4)
add("tree_larch", f"{SC}Larch_Tree.prefab", "tree", "frozen", "conifer", height=14, scaleMin=0.85, scaleMax=1.2, sink=0.25, weight=0.35)
for i in range(1, 5):
    add(f"tree_snowoak_{i:02}", f"{SYM}Oak0{i}.prefab", "tree", "frozen", "snowy,valley", height=8.5, scaleMin=0.85, scaleMax=1.25, sink=0.12, weight=0.5)
add("tree_palm_tall", "Assets/Darth_Artisan/Free_Trees/Prefabs/Palm_Tree.prefab", "palm", "dunes", "palm,oasis", height=8, scaleMin=0.85, scaleMax=1.3, sink=0.1, weight=1.0)
add("tree_palm", f"{PR}Palm.prefab", "palm", "dunes", "palm,oasis", height=7, scaleMin=0.85, scaleMax=1.25, sink=0.1, weight=1.0)
add("tree_palm_triple", f"{PR}TriplePalms.prefab", "palm", "dunes", "palm,oasis", height=6.5, scaleMin=0.9, scaleMax=1.2, sink=0.1, weight=0.6)
for s in "abcefghij":
    add(f"tree_dead_{s}", f"{PL}prefab_trees/dead_tree_{s}.prefab", "deadtree", "dunes,grave,storm", "dead", rot=Z_UP, height=8 if s in "bc" else 11, scaleMin=0.75, scaleMax=1.15, sink=0.2, weight=0.5)
for s in "bcd":
    add(f"tree_desert_pine_{s}", f"{PL}prefab_trees/pine_{s}.prefab", "tree", "dunes", "pine,city", rot=Z_UP, scaleMin=0.85, scaleMax=1.2, sink=0.12, weight=0.6)

# ---------------- bushes, shrubs, ground plants ----------------
for n in ("01_01", "01_02", "02_01", "02_02", "03_01", "03_02"):
    add(f"bush_{n}", f"{IFN}Bush_{n}.prefab", "bush", "verdant,grave,storm", "bush", scaleMin=0.8, scaleMax=1.3, sink=0.1, weight=1.0)
add("bush_spruce", f"{UNS}Vegetation/Bushes/Prefabs/UNS_Bush.prefab", "bush", "frozen,verdant", "bush,cold", scaleMin=0.8, scaleMax=1.25, sink=0.08, weight=0.8)
for n in ("01", "02", "03"):
    add(f"cattail_{n}", f"{IFN}Cattail_{n}.prefab", "reed", "verdant,grave", "water,reed", scaleMin=0.85, scaleMax=1.3, collider="none", noShadow=True, weight=1.0)
    add(f"lily_{n}", f"{IFN}LilyPads_{n}.prefab", "lily", "verdant,grave", "water", scaleMin=0.8, scaleMax=1.3, collider="none", noShadow=True, weight=1.0)
for n in ("01", "02"):
    add(f"waterlily_{n}", f"{IFN}Waterlily_{n}.prefab", "lily", "verdant", "water,flower", scaleMin=0.8, scaleMax=1.2, collider="none", noShadow=True, weight=0.6)
add("fern", f"{SC}Fern.prefab", "fern", "verdant,frozen,grave", "forest", height=0.8, scaleMin=0.8, scaleMax=1.4, collider="none", noShadow=True)
add("mushroom_patch", f"{UNS}Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab", "mushroom", "verdant,grave", "forest", height=0.45, collider="none", noShadow=True)
add("mushroom_boletus", f"{SC}Boletus_Edulis.prefab", "mushroom", "verdant", "forest", height=0.3, collider="none", noShadow=True)
for n in ("purple_mushrooms", "red_mushooms"):
    add(f"mushroom_{n.split('_')[0]}", f"Assets/Turntable_platforms/Prefabs/mushrooms/{n}.prefab", "mushroom", "grave,verdant", "magic,forest", height=0.8, collider="none", noShadow=True)

# ---------------- terrain detail meshes (instanced grass & flowers) ----------------
for n in ("01", "02", "03"):
    add(f"detail_grass_{n}", f"{IFN}Grass_{n}.prefab", "grass", "verdant,grave,storm", "grass", detail=True, noShadow=True)
add("detail_grass_dry", f"{IFN}Grass_02.prefab", "grass", "dunes,frozen", "grass,dry", detail=True, noShadow=True, tint="D8C79A")
add("detail_grass_frost", f"{IFN}Grass_03.prefab", "grass", "frozen", "grass,frost", detail=True, noShadow=True, tint="CFE0E6")
add("detail_plants", f"{IFN}Plants.prefab", "plant", "verdant,grave", "plant", detail=True, noShadow=True)
for n in ("01", "02", "03"):
    add(f"detail_reeds_{n}", f"{IFN}Reeds_{n}.prefab", "reed", "verdant,grave", "water", detail=True, noShadow=True)
for c in ("Blue", "Orange", "Pink", "Purple", "Red", "White", "BluePurple", "OrangePinkRedPurpleBlue", "RedOrange"):
    add(f"detail_meadow_{c.lower()}", f"{IFN}FlowerMeadow_{c}.prefab", "flower", "verdant", "meadow", detail=True, noShadow=True)
for c in ("Blue_01", "Orange", "Pink", "Purple", "Red", "White", "Yellow", "YellowRed"):
    add(f"detail_flower_{c.lower()}", f"{IFN}Flower_{c}.prefab", "flower", "verdant", "flower", detail=True, noShadow=True)
add("detail_fern", f"{SC}Fern.prefab", "fern", "verdant,frozen", "forest", detail=True, noShadow=True, height=0.8)
add("detail_dandelion", f"{SC}Dandelion_02.prefab", "flower", "verdant", "meadow", detail=True, noShadow=True, height=0.45)

# ---------------- rocks, stones, cliffs ----------------
for n in ("01", "02", "03"):
    add(f"rock_big_{n}", f"{IFN}Rock_Big_{n}.prefab", "rock_big", "verdant,storm,grave,frozen", "rock", scaleMin=0.8, scaleMax=1.6, sink=0.35, align=0.6)
    add(f"rock_medium_{n}", f"{IFN}Rock_Medium_{n}.prefab", "rock", "verdant,storm,grave,frozen", "rock", scaleMin=0.7, scaleMax=1.4, sink=0.3, align=0.7)
    add(f"rock_small_{n}", f"{IFN}Rock_Small_{n}.prefab", "rock_small", "verdant,storm,grave,frozen", "rock", scaleMin=0.6, scaleMax=1.4, sink=0.25, align=0.8)
    add(f"stone_big_{n}", f"{IFN}Stone_Big_{n}.prefab", "stone", "verdant,grave,storm", "pebble,water", scaleMin=0.8, scaleMax=1.6, sink=0.15, align=0.9, collider="none")
    add(f"stones_{n}", f"{IFN}Stones_{n}.prefab", "stone", "verdant,grave", "pebble,path,water", scaleMin=0.8, scaleMax=1.3, sink=0.04, align=1.0, collider="none", noShadow=True)
    add(f"rock_sand_big_{n}", f"{IFN}Rock_Big_{n}.prefab", "rock_big", "dunes", "rock,sandstone", scaleMin=0.8, scaleMax=1.7, sink=0.35, align=0.5, tint="E9C9A0")
    add(f"rock_sand_{n}", f"{IFN}Rock_Medium_{n}.prefab", "rock", "dunes", "rock,sandstone", scaleMin=0.7, scaleMax=1.4, sink=0.3, align=0.6, tint="E9C9A0")
for n in ("01", "02", "03", "04", "05"):
    add(f"cliff_{n}", f"{IFN}Cliff_{n}.prefab", "cliff", "verdant,storm,grave,frozen", "cliff", scaleMin=0.8, scaleMax=1.5, sink=0.3, align=0.35)
    add(f"cliff_sand_{n}", f"{IFN}Cliff_{n}.prefab", "cliff", "dunes", "cliff,sandstone", scaleMin=0.8, scaleMax=1.5, sink=0.3, align=0.35, tint="E7C497")
    add(f"cliff_frost_{n}", f"{UNS}Rocks/Cliffs/Prefabs/UNS_Rock_Cliff_0{n[1]}.prefab", "cliff", "frozen", "cliff,snow", scaleMin=1.0, scaleMax=1.8, sink=0.3, align=0.35)
    add(f"rock_frost_{n}", f"{UNS}Rocks/Forest/Prefabs/UNS_Standard_Rock_0{n[1]}.prefab", "rock", "frozen", "rock,snow", scaleMin=0.8, scaleMax=1.6, sink=0.3, align=0.7)
    add(f"pebble_river_{n}", f"{UNS}Rocks/River/Prefabs/UNS_Tiny_Rock_0{n[1]}.prefab", "stone", "frozen,verdant", "pebble,water", scaleMin=1.5, scaleMax=3.0, sink=0.05, align=1.0, collider="none", noShadow=True)
add("mountain_crag", f"{UNS}Rocks/Mountain/Prefabs/UNS_Mountain.prefab", "mountain", "frozen,storm", "crag", scaleMin=0.6, scaleMax=1.1, sink=2.0, align=0.0)
for n in ("01", "02", "03", "04"):
    add(f"rockset_{n}", f"Assets/Mountain Terrain rocks and tree/Prefab/rock_set_{n}.prefab", "rock", "storm,dunes", "rock,cluster", scaleMin=0.9, scaleMax=1.6, sink=0.25, align=0.6)
for s in "ABC":
    add(f"camp_rock_{s.lower()}", f"{ING}Rock_{s}.prefab", "rock_small", "verdant,storm", "rock,camp", scaleMin=0.8, scaleMax=1.3, sink=0.15, align=0.8)
for n in ("001", "002", "003", "004", "005"):
    add(f"runestone_{n}", f"{DM}Extra Content/Fantasy Nature Pack - Forest/SFNP_Runestone_{n}.prefab", "shrine", "verdant,frozen,grave,storm", "runestone,landmark", height=2.2, scaleMin=0.85, scaleMax=1.3, sink=0.1, collider="convex")

# ---------------- forest litter ----------------
add("log_fallen", f"{UNS}Props/Logs/Prefabs/UNS_Log.prefab", "log", "verdant,frozen,grave", "forest", scaleMin=0.9, scaleMax=1.6, sink=0.1, align=0.9, collider="convex")
add("stump_old", f"{UNS}Props/Logs/Prefabs/UNS_Stump.prefab", "log", "verdant,frozen,grave", "forest,stump", scaleMin=0.8, scaleMax=1.4, sink=0.1, align=0.5, collider="convex")
add("stump_painted", f"{SC}Stump.prefab", "log", "verdant,frozen", "forest,stump", height=0.9, scaleMin=0.8, scaleMax=1.3, sink=0.15, align=0.5, collider="convex")
add("branch_fallen", f"{UNS}Props/Branches/Prefabs/UNS_Branch.prefab", "log", "verdant,frozen,grave", "forest,small", scaleMin=0.9, scaleMax=1.6, sink=0.02, align=1.0, collider="none", noShadow=True)
for n in ("01", "02", "03", "04", "05", "06"):
    add(f"branch_{n}", f"{IFN}Branch_{n}.prefab", "log", "verdant,grave", "forest,small", scaleMin=0.9, scaleMax=1.5, sink=0.02, align=1.0, collider="none", noShadow=True)

# ---------------- Verdant & Frostpine architecture ----------------
for name, h in (("House1", 0), ("House2", 0), ("House3", 0), ("WoodenHouse_1", 0), ("WoodenHouse_2", 0), ("WoodenHouse_3", 0)):
    add(f"house_{name.lower()}", f"{FE}{name}.prefab", "building", "verdant,frozen", "house,town", collider="mesh")
add("tavern_grand", f"{FE}Tavern.prefab", "building", "verdant", "tavern,landmark,town", collider="mesh", cull=0.004)
add("windmill_grand", f"{FE}Windmill.prefab", "building", "verdant", "windmill,landmark,farm", collider="mesh", cull=0.003)
add("windmill_small", "Assets/Polygrade/Top_Down Medieval City/Windmill_Free/Prefabs/Medieval_Windmill.prefab", "building", "verdant", "windmill,farm", collider="mesh")
add("house_big_village", f"{VP}BigHouse/BigHousePrefab.prefab", "building", "verdant", "house,village", collider="mesh")
add("house_old_village", f"{VP}OldHouse/OldHousePrefab.prefab", "building", "verdant,frozen", "house,village", collider="mesh")
add("armory_hall", f"{DM}Armory Structure and Attachables/Armory.prefab", "building", "verdant", "armory,smith,landmark", collider="mesh")
add("town_arch", f"{DM}Decorative Props/Entrance Arch/EntranceArch.prefab", "gate", "verdant", "arch,town", collider="mesh")
for i in range(1, 6):
    add(f"rpg_house_{i:02}", f"{RPG}rpgpp_lt_building_0{i}.prefab", "building", "verdant,frozen", "house,hamlet", collider="mesh")
add("rpg_shed_01", f"{RPG}rpgpp_lt_shed_wood_01.prefab", "building", "verdant,frozen", "shed,farm", collider="mesh")
add("rpg_shed_02", f"{RPG}rpgpp_lt_shed_wood_02.prefab", "building", "verdant,frozen", "shed,farm", collider="mesh")
for name in ("Wall_part1", "Wall_part2", "Wall_corner", "Wall_entrance", "Wall_tower1", "Wall_tower2"):
    add(f"stonewall_{name.lower().replace('wall_','')}", f"{FE}{name}.prefab", "wall", "verdant", "stonewall,town", collider="mesh")
for name in ("WoodenWall_1", "WoodenWall_2", "WoodenWall_Corner", "WoodenWall_Entrace", "WoodenWall_Towers", "WoodenWall_TowerCorner"):
    add(f"palisade_{name.lower().replace('woodenwall_','')}", f"{FE}{name}.prefab", "wall", "verdant,frozen,storm", "palisade,camp", collider="mesh")

# ---------------- Burning Dunes architecture ----------------
DESERT_HOUSES = ["01", "12", "13", "16", "20", "26", "28", "30_a", "30_c", "30_d", "30_f", "31_a", "31_c", "31_d", "31_g",
                 "32_a", "32_c", "32_e", "33_a", "33_c", "33_f", "33_h", "34_a", "34_c", "34_g", "35_a", "35_b", "35_d",
                 "38_a", "38_d", "38_f", "39_a", "39_c", "39_e", "40_b", "40_d", "40_f", "41_a", "41_b", "42", "43", "44"]
for h in DESERT_HOUSES:
    small = h.startswith("39") or h.startswith("40") or h in ("42", "43", "44", "35_a")
    add(f"desert_house_{h}", f"{PL}prefab_civilian_buildings/civilian_house_{h}.prefab", "building", "dunes", "house,desert" + (",hut" if small else ",city"), rot=Z_UP, collider="mesh")
for s in "abcdef":
    add(f"desert_ruin_{s}", f"{PL}prefab_civilian_buildings/civilian_house_ruin_{s}.prefab", "ruin", "dunes,storm", "ruin,desert", rot=Z_UP, collider="mesh")
for name, tags in (("citadel_main", "citadel,landmark"), ("church_b", "temple,landmark"), ("city_tower_a", "tower,landmark"),
                   ("castle_tower_round", "tower,wall"), ("castle_tower_octagon", "tower,wall"), ("castle_tower_rectangular", "tower,wall"),
                   ("castle_wall_5m", "wall"), ("castle_wall_10m", "wall"), ("castle_wall_15m", "wall"), ("castle_wall_corner_a", "wall,corner"),
                   ("castle_gate_b", "gate,wall"), ("fortification_gate", "gate,fort"), ("fortification_tower_big", "tower,fort"),
                   ("fortification_tower_small", "tower,fort"), ("sentinel_tower_a", "tower,watch"), ("sentinel_tower_b", "tower,watch"),
                   ("overhang_building_a", "house,city"), ("street_gate", "gate,city")):
    role = "tower" if "tower" in name else "wall" if "wall" in name else "gate" if "gate" in name else "building"
    extra = {"cull": 0.003} if "landmark" in tags else {}
    add(f"desert_{name}", f"{PL}prefab_unique_buildings/{name}.prefab", role, "dunes,storm", tags + ",desert", rot=Z_UP, collider="mesh", **extra)
for h in ("1", "2", "3", "4", "5", "6", "5-Broken"):
    add(f"oasis_house_{h.lower().replace('-', '_')}", f"{PR}House{h}.prefab", "building", "dunes", "house,oasis,hut", collider="mesh")
for name, role, tags in (("tent_a", "prop", "tent,market"), ("tent_b", "prop", "tent,market"), ("tent_c", "prop", "tent,market"), ("well", "prop", "well"),
                         ("fountain_a", "prop", "fountain,plaza"), ("fountain_b", "prop", "fountain,plaza"), ("barrel", "prop", "barrel"),
                         ("barrel_group", "prop", "barrel"), ("box", "prop", "crate"), ("wagon", "prop", "wagon"), ("street_oil_light", "lamp", "lamp,street"),
                         ("fire_cage", "lamp", "brazier"), ("banner", "banner", "banner")):
    add(f"desert_{name}", f"{PL}prefab_props/{name}.prefab", role, "dunes", tags + ",desert", rot=Z_UP, collider="convex" if role != "banner" else "box")
for j in ("1", "2", "3"):
    add(f"oasis_jar_{j}", f"{PR}Jar{j}.prefab", "prop", "dunes", "jar,clutter", collider="convex")

# ---------------- Hollow Marches (graveyard) ----------------
for n, h in (("001", 1.3), ("002", 1.1), ("003", 0.7), ("004", 1.2), ("005", 1.6)):
    add(f"tomb_{n}", f"{CEM}Tombstones/Tombstones.{n}.prefab", "tombstone", "grave", "tomb", height=h, scaleMin=0.85, scaleMax=1.15, sink=0.12, align=0.3, collider="box")
add("tomb_plain", f"{CEM}Tombstones/Tombstones.prefab", "tombstone", "grave", "tomb", height=1.0, scaleMin=0.85, scaleMax=1.15, sink=0.12, align=0.3, collider="box")
add("coffin_open", f"{CEM}Tombstones/TombstonesCoffinWood.002.prefab", "prop", "grave", "coffin", height=0.55, collider="box")
add("grave_fence_1", f"{CEM}Gate/MetObj_Fence1.prefab", "fence", "grave", "ironfence", height=1.9, collider="box")
add("grave_fence_2", f"{CEM}Gate/MetObj_Fence2.prefab", "fence", "grave", "ironfence", height=1.9, collider="box")
add("grave_gate_arch", f"{CEM}Gate/MetObj_GatesArch.prefab", "gate", "grave", "ironfence,gate", height=4.2, collider="mesh")
add("grave_lamp", f"{CEM}CandleAndLamp/MetObj_Lamp.prefab", "lamp", "grave", "lamp", height=2.6, collider="box", keepLights=True)
add("grave_column", f"{CEM}Crypt/ColumnCrypt1.prefab", "ruin", "grave", "column,crypt", height=3.4, collider="mesh")
add("grave_crypt_arch", f"{CEM}Crypt/CryptArch1.prefab", "ruin", "grave", "arch,crypt", height=3.6, collider="mesh")

# ---------------- camps, settlement props ----------------
for s in ("A", "B", "C"):
    add(f"bonfire_{s.lower()}", f"{ING}Bonfire_{s}_Big_Fire.prefab", "campfire", "verdant,frozen,storm,dunes,grave", "fire,camp", collider="none", keepLights=True)
add("bonfire_cook", f"{ING}Bonfire_D_to_Cook.prefab", "campfire", "verdant,frozen,storm", "fire,camp,cook", collider="none", keepLights=True)
for s in ("A", "B", "C"):
    add(f"firewood_{s.lower()}", f"{ING}Firewood_{s}.prefab", "prop", "verdant,frozen,storm", "firewood,camp", collider="convex")
for s in ("A", "B"):
    add(f"stake_{s.lower()}", f"{ING}Stake_{s}.prefab", "prop", "verdant,storm,dunes,grave", "stake,camp,defence", collider="convex")
for b in ("A_(1)_Banner_War_Alone", "B_(1)_Banner_War_Alone", "C_(1)_Banner_War_Alone", "E_(1)_Banner_War_E_Alone"):
    add(f"war_banner_{b[0].lower()}", f"{ING}Banner_{b}.prefab", "banner", "verdant,storm,dunes,frozen", "banner,camp", collider="box", keepAnim=True)
add("barrels_mounted", f"{ING}Barrels_Mounted.prefab", "prop", "verdant,storm", "barrel,camp", collider="convex")
add("camp_well", f"{ING}Medieval_Well.prefab", "prop", "verdant,frozen", "well", collider="mesh")
add("camp_cook", f"{ING}Medieval_Cook .prefab", "prop", "verdant,storm", "cook,camp", collider="convex", keepLights=True)
add("lantern_post", f"{ING}Farol_Light_ON.prefab", "lamp", "verdant,frozen,storm", "lamp,street", collider="box", keepLights=True)
for name, tags in (("cart1", "cart"), ("cart2", "cart"), ("cart3", "cart"), ("storage_barrel", "barrel"), ("storage_barrel_fish", "barrel"),
                   ("storage_barrel_small", "barrel"), ("storage_basket", "basket"), ("storage_bag_potato", "sack"), ("storage_bag_carrot", "sack"),
                   ("storage_jug", "jug"), ("Furniture_table", "table"), ("Wall_props", "clutter")):
    add(f"town_{name.lower()}", f"{FE}{name}.prefab", "prop", "verdant,frozen", tags + ",town", collider="convex")
for name, tags in (("Decorative Props/Combat Dummy/CombatDummy.001", "dummy,training"), ("Decorative Props/Target/Target", "target,training"),
                   ("Decorative Props/Oven/Oven", "oven,smith"), ("Decorative Props/Torch/Torch", "torch"), ("Decorative Props/Weapon Racks/WeaponRack.002", "rack,smith"),
                   ("Decorative Props/Weapon Racks/WeaponRack.004", "rack,smith"), ("Decorative Props/Anvil and Airblower/Anvil", "anvil,smith"),
                   ("Decorative Props/Cauldron and Campfire/CauldronandCampfire", "cauldron,camp"), ("Decorative Props/Box/Box.001", "crate"),
                   ("Decorative Props/Barrel/Barrel.001", "barrel"), ("Decorative Props/Fences/Fence.001", "fence"), ("Extra Content/Fantasy Farm - Props/SFF_Beehive_001 Variant", "beehive,farm")):
    key = "smith_" + name.split("/")[-1].split(" ")[0].split(".")[0].lower()
    if any(e["key"] == key for e in E): key += "_b"
    add(key, f"{DM}{name}.prefab", "fence" if "fence" in tags else "prop", "verdant", tags, collider="convex")
for name, tags in (("rpgpp_lt_fence_wood_01a", "fence,farm"), ("rpgpp_lt_fence_wood_01b", "fence,farm"), ("rpgpp_lt_fence_wood_02a", "fence,farm"),
                   ("rpgpp_lt_fence_wood_02b", "fence,farm"), ("rpgpp_lt_barrel_01", "barrel"), ("rpgpp_lt_crate_01", "crate"), ("rpgpp_lt_crate_02", "crate"),
                   ("rpgpp_lt_sack_02_set", "sack"), ("rpgpp_lt_wagon_01", "wagon"), ("rpgpp_lt_well_01", "well"), ("rpgpp_lt_bench_wood_01", "bench"),
                   ("rpgpp_lt_trough_01", "trough,farm"), ("rpgpp_lt_log_wood_02a", "firewood"), ("rpgpp_lt_hanger_clothes_01", "laundry,village"),
                   ("rpgpp_lt_awning_standing_01a", "stall,market"), ("rpgpp_lt_awning_standing_01b", "stall,market"), ("rpgpp_lt_bird_house_01", "village")):
    add(name.replace("rpgpp_lt_", "village_"), f"{RPG}{name}.prefab", "fence" if "fence" in tags else "prop", "verdant,frozen", tags, collider="box" if "fence" in tags else "convex")

# ---------------- sky ----------------
for i in range(1, 7):
    add(f"cloud_{i:02}", f"Assets/SimpleSky/Prefabs/Cloud_0{i}.prefab", "cloud", "any", "cloud", collider="none", noShadow=True, cull=0.0008)

# ---------------- VFX ----------------
HV = "Assets/Hovl Studio/Magic effects pack/Prefabs/"
FS = "Assets/Free Slash VFX/Prefabs/"
ER = "Assets/Eric VFX Studio/Free Game VFX/Prefab/"
GA = "Assets/Resources/VFX/"
VF = "Assets/Vefects/Free Fire VFX URP/Particles/"
def fx(key, path, loop=False, life=0, scale=1.0, maxp=0, tags=""):
    e = dict(loop=loop, fxScale=scale, tags=tags)
    if life: e["life"] = life
    if maxp: e["maxParticles"] = maxp
    add(key, path, "vfx", "any", **e)
fx("slash_physical", f"{FS}Slash VFX.prefab", tags="slash")
fx("slash_fire", f"{FS}Slash Fire VFX.prefab", tags="slash,fire")
fx("slash_frost", f"{FS}Slash Water VFX.prefab", tags="slash,frost")
fx("slash_storm", f"{FS}Slash Eletric VFX.prefab", tags="slash,storm")
fx("slash_earth", f"{FS}Slash Earth VFX.prefab", tags="slash,earth")
fx("slash_heavy_fire", f"{HV}Slash effects/Charge slash red.prefab", life=2.2, tags="slash,heavy,fire")
fx("slash_heavy_frost", f"{HV}Slash effects/Charge slash blue.prefab", life=2.2, tags="slash,heavy,frost")
fx("slash_heavy_arcane", f"{HV}Slash effects/Charge slash purple.prefab", life=2.2, tags="slash,heavy,arcane")
fx("slash_snow", f"{HV}Slash effects/Snow slash.prefab", life=1.5, tags="slash,frost")
fx("slash_electro", f"{HV}Slash effects/Electro slash.prefab", life=1.5, tags="slash,storm")
fx("slash_stone", f"{HV}Slash effects/Stone slash.prefab", life=2.0, tags="slash,earth")
fx("impact_generic", f"{FS}Impact.prefab", life=1.5, tags="hit")
fx("hit_fire", f"{HV}Hits and explosions/Explosion.prefab", life=2.0, tags="hit,fire")
fx("hit_frost", f"{HV}Hits and explosions/Snow hit.prefab", life=2.0, tags="hit,frost")
fx("hit_nature", f"{HV}Hits and explosions/Green hit.prefab", life=2.0, tags="hit,nature")
fx("hit_storm", f"{HV}Hits and explosions/Electro hit.prefab", life=1.5, tags="hit,storm")
fx("hit_holy", f"{HV}Hits and explosions/Holy hit.prefab", life=2.0, tags="hit,holy")
fx("hit_stone", f"{HV}Hits and explosions/Stones hit.prefab", life=2.0, tags="hit,earth")
fx("hit_crit", f"{HV}Hits and explosions/Star hit.prefab", life=2.0, tags="hit,crit")
fx("projectile_fire", f"{FS}Projectiles/Slash Projectile VFX Fire.prefab", loop=True, tags="projectile,fire")
fx("projectile_frost", f"{FS}Projectiles/Slash Projectile VFX Water.prefab", loop=True, tags="projectile,frost")
fx("projectile_storm", f"{FS}Projectiles/Slash Projectile VFX Eletric.prefab", loop=True, tags="projectile,storm")
fx("projectile_earth", f"{FS}Projectiles/Slash Projectile VFX Earth.prefab", loop=True, tags="projectile,earth")
fx("fireball", f"{ER}FX_Fireball.prefab", loop=True, tags="projectile,fire")
fx("aoe_ground", f"{HV}AoE effects/Ground AOE explosion.prefab", life=3.0, tags="aoe,earth")
fx("aoe_snow", f"{HV}AoE effects/Snow AOE.prefab", life=4.0, tags="aoe,frost")
fx("aoe_meteors", f"{HV}AoE effects/Meteors AOE.prefab", life=6.0, tags="aoe,fire")
fx("aoe_smoke", f"{HV}AoE effects/Smoke AOE explosion.prefab", life=3.0, tags="aoe,smoke")
fx("aoe_energy", f"{HV}AoE effects/Red energy explosion.prefab", life=3.0, tags="aoe,arcane")
fx("aoe_crystals", f"{HV}AoE effects/Crystals front attack.prefab", life=3.2, tags="aoe,frost")
fx("aoe_slash", f"{HV}AoE effects/AoE slash orange.prefab", life=2.6, tags="aoe,slash")
fx("aura_buff", f"{HV}Character auras/Buff.prefab", loop=True, tags="aura")
fx("aura_debuff", f"{HV}Character auras/Debuff.prefab", loop=True, tags="aura")
fx("aura_heal", f"{HV}Character auras/Healing.prefab", life=3.0, tags="aura,heal")
fx("aura_lightning", f"{HV}Character auras/Lightning aura.prefab", loop=True, tags="aura,storm")
fx("circle_magic", f"{HV}Magic circles/Magic circle.prefab", loop=True, tags="circle")
fx("circle_heal", f"{HV}Magic circles/Healing circle.prefab", loop=True, tags="circle,heal")
fx("circle_freeze", f"{HV}Magic circles/Freeze circle.prefab", loop=True, tags="circle,frost")
fx("shield_blue", f"{HV}Magic shields/Magic shield blue.prefab", loop=True, tags="shield")
fx("portal_blue", f"{HV}Portals/Portal blue.prefab", loop=True, tags="portal")
fx("portal_green", f"{HV}Portals/Portal green.prefab", loop=True, tags="portal")
fx("portal_red", f"{HV}Portals/Portal red.prefab", loop=True, tags="portal")
fx("portal_yellow", f"{HV}Portals/Portal yellow.prefab", loop=True, tags="portal")
fx("teleport", f"{HV}Environment/Teleport.prefab", life=2.0, tags="teleport")
fx("crystal_blue", f"{HV}Environment/Crystal effect blue.prefab", loop=True, tags="crystal")
fx("crystal_green", f"{HV}Environment/Crystal effect green.prefab", loop=True, tags="crystal")
fx("crystal_red", f"{HV}Environment/Crystal effect red.prefab", loop=True, tags="crystal")
fx("dust_puff", f"{HV}Smoke effects/Dust puff.prefab", life=2.5, tags="dust")
fx("smoke_puff", f"{HV}Smoke effects/Smoke puff.prefab", life=2.5, tags="smoke,death")
fx("dust_ground", f"{HV}Smoke effects/Dust ground.prefab", loop=True, tags="dust,ambient")
fx("smoke_loop", f"{HV}Smoke effects/Smoke loop.prefab", loop=True, tags="smoke,chimney")
fx("smoke_vortex", f"{HV}Smoke effects/Smoke vortex.prefab", loop=True, tags="smoke,rift")
fx("sparks_gold", f"{HV}Sparks/Sparks explode yellow.prefab", life=2.0, tags="sparks,reward")
fx("sparks_white", f"{HV}Sparks/Sparks explode white.prefab", life=2.0, tags="sparks")
fx("sparks_forge", f"{HV}Sparks/Sparks flashing yellow.prefab", loop=True, tags="sparks,forge")
fx("loot_beam", f"{ER}FX_LightPillar.prefab", loop=True, tags="loot")
fx("loot_drop", f"{ER}FX_LootDrop_Blue.prefab", loop=True, tags="loot")
fx("magic_door", f"{ER}FX_Magic Door_Gold.prefab", loop=True, tags="portal,gold")
fx("levelup_pillar", f"{ER}FX_PinkCross_Up.prefab", life=3.0, tags="reward")
fx("ga_heal", f"{GA}ga_vfx_Heal_01.prefab", life=3.0, tags="heal")
fx("ga_shockwave", f"{GA}ga_vfx_Shockwave_01.prefab", life=2.0, tags="aoe,shock")
fx("ga_lightning", f"{GA}ga_vfx_Lightning_02.prefab", life=2.5, tags="storm")
fx("ga_tornado", f"{GA}ga_vfx_Tornado_01.prefab", loop=True, tags="storm,wind")
fx("ga_rain", f"{GA}ga_vfx_Rain_01.prefab", loop=True, tags="weather")
fx("ga_portal", f"{GA}ga_vfx_Portal_01.prefab", loop=True, tags="portal")
fx("ga_explosion", f"{GA}ga_vfx_Explosion_01.prefab", life=3.0, tags="explosion")
fx("fire_campfire", f"{VF}VFX_Fire_01_Small_Smoke.prefab", loop=True, tags="fire,camp")
fx("fire_torch", f"{VF}VFX_Fire_01_Small_Simple.prefab", loop=True, scale=0.6, tags="fire,torch")
fx("fire_bonfire", f"{VF}VFX_Fire_01_Medium_Smoke.prefab", loop=True, tags="fire,bonfire")
fx("fire_big", f"{VF}VFX_Fire_01_Big_Smoke.prefab", loop=True, tags="fire,big")
fx("fire_floor", f"{VF}VFX_Fire_Floor_01.prefab", loop=True, tags="fire,ground")
fx("leaves_green", f"{IFN}FloatingLeafs_Green.prefab", loop=True, tags="ambient,leaves")
fx("leaves_yellow", f"{IFN}FloatingLeafs_Yellow.prefab", loop=True, tags="ambient,leaves")
fx("godrays", f"{IFN}GodRays.prefab", loop=True, tags="ambient,light")
fx("motes", f"{IFN}Particles.prefab", loop=True, tags="ambient,motes")

# Resolve sources that live in pack subfolders by unique file name (probe inventory).
probe = sys.argv[2] if len(sys.argv) > 2 else "ClaudeBridge/probe.json"
if os.path.exists(probe):
    paths = [x["path"] for x in json.load(open(probe))["prefabs"]]
    known = set(paths)
    for e in E:
        if e["source"] in known: continue
        base = e["source"].split("/")[-1]
        pack = "/".join(e["source"].split("/")[:2])
        hits = [q for q in paths if q.endswith("/" + base) and q.startswith(pack)]
        if len(hits) == 1: e["source"] = hits[0]
        else: print("UNRESOLVED", e["key"], e["source"], len(hits))
json.dump({"entries": E}, open(sys.argv[1] if len(sys.argv) > 1 else "Docs/AssetAudit/world-kit.json", "w"), indent=1)
print(len(E), "entries")
