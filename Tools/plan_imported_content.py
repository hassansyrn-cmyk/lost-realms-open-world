"""Curated region/role placements. Does not alter vendor sources or copy bulk assets."""
from pathlib import Path
import subprocess, json, hashlib, re, math
ROOT=Path(__file__).resolve().parents[1]
paths=sorted(p.replace('\\','/') for p in subprocess.check_output(['rg','--files','Assets'],cwd=ROOT,text=True).splitlines())
entries=[]
def add(path,category,region,x,z,height,alias='',key=None,weapon=-1):
 if path not in paths: return
 key=key or category+'_'+hashlib.sha1(path.encode()).hexdigest()[:10]
 entries.append(dict(key=key,source=path,address='',category=category,region=region,position=dict(x=x,y=0,z=z),height=height,yaw=0,alias=alias,weaponId=weapon))
def choose(prefix,pattern,count,category,region,center,height,spacing=9):
 candidates=[p for p in paths if p.startswith('Assets/'+prefix+'/') and p.lower().endswith('.prefab') and re.search(pattern,p,re.I) and not re.search(r'/(demo|examples?|scenes?)/',p,re.I)]
 for i,p in enumerate(candidates[:count]):
  angle=i*2.399963; radius=spacing*(1+i//4)
  add(p,category,region,center[0]+math.cos(angle)*radius,center[1]+math.sin(angle)*radius,height)

for i,(name,key) in enumerate([('Sword','elf_sword'),('Axe','elf_axe'),('Spear','elf_spear'),('Hammer','elf_hammer'),('Staff','elf_staff')]):
 add(f'Assets/Elven Weapons/Prefabs/Elven_{name}_01.prefab','weapon','Core',-38+i*4,-74,1.3,key=key,weapon=11+i)
add('Assets/Character_Elf/Prefab/Character_Elf.prefab','npc','Core',16,8,1.8,'lyra')
add('Assets/Fox/Prefabs/Fox.prefab','wildlife','Verdant',-36,48,.7)
add('Assets/Rabbits/Prefabs/Rabbit 1.prefab','wildlife','Verdant',-28,42,.35)
add('Assets/living birds/resources/lb_robin.prefab','wildlife','Verdant',-20,54,.18,'bird')
add('Assets/living birds/resources/lb_sparrow.prefab','wildlife','Verdant',-52,38,.16,'bird')
add('Assets/Rhino/Prefab/Rhino_PBR.prefab','wildlife','Dunes',145,-110,1.6)
choose('ithappy',r'(Deer|Chicken|Dog|Horse)_001\.prefab$',4,'wildlife','Verdant',(-94,-30),.85)
add('Assets/Skeleton/Prefabs/Skeleton.prefab','enemy','Crypt',-82,-61,1.8)
add('Assets/DungeonCharacters/Skeletons_demo/DungeonSkeleton_demoPrefab.prefab','enemy','Crypt',-76,-72,1.8)
add('Assets/cave_troll/prefabs/troll.prefab','enemy','Crypt',-91,-72,2.7,'brute')
add('Assets/Wolf_Animated/Prefabs/Wolf.prefab','enemy','Frost',55,155,1.1)
add('Assets/fantasySpider/spider_myOldOne.FBX','enemy','Crypt',-85,-82,1.1)
add('Assets/LowpolyBakersHouse/Prefabs/Baker_house.prefab','building','Verdant',-42,-90,7)
choose('VillagePack',r'/(BigHouse|OldHousePrefab)\.prefab$',2,'building','Verdant',(-64,-104),7,14)
choose('BrokenVector',r'LowPolyBrickHouses/.*/House_[12]\.prefab$',2,'building','Verdant',(-90,-100),6,14)
choose('FantasyEnvironments',r'/(Windmill|WoodenHouse_1|House1)\.prefab$',3,'building','Verdant',(-116,-102),7,18)
choose('Polylised - Medieval Desert City',r'/civilian_house_\d+\.prefab$',3,'building','Dunes',(145,-82),6,16)
choose('Darth_Artisan',r'(Oak|Poplar)_Tree\.prefab$',2,'nature','Verdant',(-100,55),7,12)
choose('BrokenVector',r'LowPolyTreePack/.*/Tree Type[01] 0[12]\.prefab$',4,'nature','Verdant',(-110,82),6,12)
choose('Idyllic Fantasy Nature',r'/(BroadleafTree_0[12]_Green|Bush_01_01|WillowTree_01)\.prefab$',4,'nature','Verdant',(-40,125),5,12)
choose('Silver_Cats',r'/(Pine_Tree|Larch_Tree|Cedar_Tree_03)\.prefab$',3,'nature','Frost',(70,165),7,12)
choose('A_piece_of_nature',r'/(Rock_[34]|Pine_tree)\.prefab$',3,'nature','Verdant',(-135,15),3,10)
choose('Mountain Terrain rocks and tree',r'/rock_set_0[12]\.prefab$',2,'nature','Frost',(30,165),5,12)
add('Assets/Snow Mountain/Prefab/mountain_Snow_000.prefab','mountain','Frost',30,218,58)
add('Assets/Mountain Terrain rocks and tree/Prefab/mountain_terrain_01.prefab','mountain','Dunes',210,-135,65)
choose('Free Simple Dungeon Props - YP',r'/(Skull|Jar|Shield|Treasure)\.prefab$',4,'prop','Crypt',(-74,-85),.8,4)
choose('CemeteryPack',r'/Tombstones/Tombstones\.00[123]\.prefab$',3,'prop','Crypt',(-100,-95),1.5,5)
choose('Fantasy Chests PBR',r'/Prefabs/Mesh_Chest_01_Mobile\.prefab$',1,'prop','Crypt',(-78,-100),1,5)
choose('LowpolyBakersHouse',r'/Barrel\.prefab$',1,'prop','Verdant',(-47,-84),1.2,3)
choose('VillagePack',r'/Well\.prefab$',1,'prop','Verdant',(-68,-90),2,3)
add('Assets/BrokenVector/LowPolyBrickHouses/Prefabs/Bench.prefab','prop','Verdant',-51,-89,.8)
add('Assets/A_piece_of_nature/Prefabs/Chest.prefab','chest','Verdant',-124,28,1)
add('Assets/Low Poly Fire Particles/Prefabs/Yellow-FireWood.prefab','vfx','Verdant',5.6,-1.6,1)
add('Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Simple.prefab','vfx','Crypt',-78,-79,1)
choose('Magic Pig Games (Infinity PBR)',r'/Impacts/AAA Projectiles Vol 1 - Impact 1 nature arrow\.prefab$',1,'vfx','Verdant',(-44,125),1,3)
for effect,alias in [('Explosion','impact_ember'),('Snow hit','impact_frost'),('Green hit','impact_gale')]:
 add(f'Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/{effect}.prefab','vfx','Core',0,0,1,alias)
for alias,prefix,pattern in [
 ('sfx_step','Footstep(Snow and Grass)',r'/grass/.*\.wav$'),
 ('sfx_step2','Footsteps - Essentials',r'grass.*\.wav$'),
 ('sfx_step_snow','Footstep(Snow and Grass)',r'/snow/.*\.wav$'),
 ('sfx_attack_slash_1','SwordSoundPack',r'SWORD_01\.wav$'),
 ('sfx_attack_slash_2','SwordSoundPack',r'SWORD_02\.wav$'),
 ('sfx_attack_slash_3','SwordSoundPack',r'SWORD_03\.wav$'),
 ('sfx_impact','Epic Game Hits SFX',r'/Animation Block impact\.wav$'),
 ('sfx_bird','living birds',r'/robin\.wav$')]:
 matches=[p for p in paths if p.startswith('Assets/'+prefix+'/') and re.search(pattern,p,re.I)]
 if matches: add(matches[0],'audio','Core',0,0,1,alias,key='audio_'+alias)
# Second pass: coherent orchard, woodland edge, working village and combat feedback.
for name,alias,x,z in [('PT_Male_Peasant_01','brom',-16.5,8.2),('PT_Female_Peasant_01_a','rowan',-14.2,-3.2)]:
 add(f'Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Peasants_Citizens/Sets/{name}.prefab','npc','Core',x,z,1.8,alias)
for i,name in enumerate(['PT_Fruit_Tree_01_apples','PT_Fruit_Tree_01_pears','PT_Fruit_Tree_01_plums','PT_Pine_Tree_03_green']):
 add(f'Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/{name}.prefab','nature','Verdant',-105+i*10,-55,5.5)
for i,name in enumerate(['PT_Pine_Tree_03_logs','PT_Pine_Tree_03_stump','PT_Fruit_Tree_01_logs']):
 add(f'Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/{name}.prefab','prop','Verdant',-117+i*5,-70,.65)
for i,(folder,name,height) in enumerate([
 ('Shrubs','PT_Generic_Shrub_01_green',1.1),('Rocks','PT_River_Rock_Pile_02',1.2),
 ('Rocks','PT_Ore_Rock_01',1.8),('Mushrooms','PT_Caesars_Mushroom_01',.3),
 ('Flowers','PT_Poppy_02',.5),('Plants','PT_Grass_02',.4)]):
 add(f'Assets/Polytope Studio/Lowpoly_Environments/Prefabs/{folder}/{name}.prefab','nature','Verdant',-58+i*5,64,height)
for i in range(1,5):
 add(f'Assets/Symphonie/StylizedForestKit Samples/URP/Prefabs/Oak0{i}.prefab','nature','Verdant',-88+i*14,103,7)
for i,name in enumerate(['PT_Village_Fence_Small_01','PT_Village_Fence_Small_02','PT_Village_Fence_Small_03','PT_Hayfork_01']):
 add(f'Assets/Polytope Studio/Lowpoly_Props/Prefabs/{name}.prefab','prop','Verdant',-108+i*4,-83,1.2)
for name,alias in [('Slash VFX','fx_slash'),('Slash Fire VFX','fx_slash_ember'),('Slash Water VFX','fx_slash_frost')]:
 add(f'Assets/Free Slash VFX/Prefabs/{name}.prefab','vfx','Core',0,0,1,alias)
for name,folder,alias in [('Dust puff','Smoke effects','fx_dust'),('Sparks explode yellow','Sparks','fx_sparks')]:
 add(f'Assets/Hovl Studio/Magic effects pack/Prefabs/{folder}/{name}.prefab','vfx','Core',0,0,1,alias)
for i in range(1,4):
 add(f'Assets/SimpleSky/Prefabs/Cloud_0{i}.prefab','cloud','Sky',-150+i*110,90-i*55,12)
out=ROOT/'Docs/AssetAudit'; out.mkdir(parents=True,exist_ok=True)
for entry in entries:
 if 'Chicken_001' in entry['source']: entry['height']=.45
 if 'Deer_001' in entry['source']: entry['height']=1.4
 if 'Horse_001' in entry['source']: entry['height']=1.8
 if 'Bush_01_01' in entry['source']: entry['height']=1.4
 if entry['category']=='prop' and ('Fantasy Chests PBR/' in entry['source'] or entry['source'].endswith('/Treasure.prefab')): entry['category']='chest'
(out/'selected-content.json').write_text(json.dumps({'entries':entries},indent=2),encoding='utf-8')
print(json.dumps({'selected_entries':len(entries),'packs':len(set(e['source'].split('/')[1] for e in entries)),'roles':{c:sum(e['category']==c for e in entries) for c in sorted(set(e['category'] for e in entries))}}))
