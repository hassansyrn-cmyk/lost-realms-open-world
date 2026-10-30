"""Read-only asset inspection; writes reproducible inventories under Docs/AssetAudit."""
from pathlib import Path
from collections import Counter, defaultdict
import json, re, struct, wave

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Docs' / 'AssetAudit'
EXISTING = {'Art','Resources','Scripts','Editor','Scenes','Settings','AddressableAssetsData'}
MODEL = {'.prefab','.fbx','.obj','.blend'}
AUDIO = {'.wav','.mp3','.ogg','.aiff'}
TEXTURES = {'.png','.jpg','.jpeg','.tga','.psd','.tif','.exr'}
RULES = {
 'animals': r'\b(fox|wolf|rabbit|bird|cat|dog|deer|horse|rhino|bear|boar|chicken|sheep|cow|butterfly)',
 'enemies': r'(troll|dragon|spider|skeleton|monster|goblin|orc|demon|enemy|zombie)',
 'characters': r'(character|npc|elf|human|knight|warrior|people|civilian|peasant)',
 'weapons': r'(weapon|sword|axe|hammer|spear|shield|bow|dagger|staff)',
 'weather': r'(rain|snow|weather|storm|lightning|cloud|sky)',
 'environment': r'(terrain|mountain|rock|tree|forest|nature|grass|bush|plant|flower|water|river)',
 'buildings': r'(building|house|village|city|castle|wall|tower|bridge|roof|door)',
 'props': r'(prop|chest|barrel|crate|torch|fence|trap|cemetery|dungeon|grave)',
 'effects': r'(vfx|particle|effect|firework|slash|explos|magic)',
 'audio': r'(sfx|sound|audio|footstep|music)',
 'animation': r'(animation|motion|mocap|animator)'}

def categories(text):
 return [key for key, pattern in RULES.items() if re.search(pattern, text.lower())] or ['other']

def main():
 OUT.mkdir(parents=True, exist_ok=True)
 packs, entries, guids, material_refs, problems = {}, [], defaultdict(list), {}, []
 previous_pack = ''
 for path in sorted((ROOT/'Assets').rglob('*')):
  if not path.is_file(): continue
  relative = path.relative_to(ROOT).as_posix()
  pack = path.relative_to(ROOT/'Assets').parts[0]
  if pack != previous_pack:
   print('Scanning '+pack, flush=True); previous_pack = pack
  ext = path.suffix.lower()
  if ext == '.meta':
   raw = path.read_text(encoding='utf-8', errors='replace')
   match = re.search(r'^guid: ([a-f0-9]+)', raw, re.M)
   if match: guids[match[1]].append(relative[:-5])
   continue
  size = path.stat().st_size
  summary = packs.setdefault(pack, {'existing_project_folder':pack in EXISTING or not (ROOT/'Assets'/pack).is_dir(),'files':0,'bytes':0,'extensions':Counter(),'categories':Counter(),'prefabs':[],'models':[],'audio':[],'docs':[]})
  summary['files'] += 1; summary['bytes'] += size; summary['extensions'][ext] += 1
  for category in categories(pack): summary['categories'][category] += 1
  if ext == '.prefab': summary['prefabs'].append(relative)
  elif ext in MODEL: summary['models'].append(relative)
  elif ext in AUDIO: summary['audio'].append(relative)
  if ext in {'.txt','.md','.pdf','.html'}: summary['docs'].append(relative)
  if ext not in MODEL | AUDIO | TEXTURES | {'.mat','.shader','.shadergraph','.controller','.anim','.unity','.asset','.cs'}: continue
  entry = {'path':relative,'pack':pack,'extension':ext,'bytes':size,'categories':categories(pack+'/'+path.stem)}
  entry['demo_or_tool'] = bool(re.search(r'(^|/)(demo[s]?|examples?|samples?|editor|test[s]?)(/|\b)', relative, re.I))
  if ext == '.png':
   try:
    with path.open('rb') as stream: header=stream.read(24)
    if header[:8] == b'\x89PNG\r\n\x1a\n':
     w,h = struct.unpack('>II', header[16:24]); entry['dimensions']=[w,h]
     entry['power_of_two'] = w>0 and h>0 and (w&(w-1))==0 and (h&(h-1))==0
   except OSError: pass
  if ext == '.wav':
   try:
    with wave.open(str(path),'rb') as clip:
     entry['audio']={'seconds':round(clip.getnframes()/clip.getframerate(),3),'sample_rate':clip.getframerate(),'channels':clip.getnchannels()}
   except (wave.Error,EOFError): pass
  if ext in {'.prefab','.mat','.controller','.unity'}:
   raw = path.read_text(encoding='utf-8',errors='replace')
   if ext == '.prefab':
    entry['components'] = dict(Counter(re.findall(r'^([A-Za-z][A-Za-z0-9]*):\s*$',raw,re.M)))
    entry['missing_script_slots']=len(re.findall(r'm_Script: \{fileID: 0\}',raw))
   if ext == '.mat':
    shader = re.search(r'm_Shader: \{([^}]+)\}',raw)
    entry['shader_reference']=shader.group(1) if shader else ''
    material_refs[relative] = sorted(set(re.findall(r'guid: ([a-f0-9]{32})',raw)))
  meta = path.with_name(path.name+'.meta')
  if meta.exists() and ext in MODEL | TEXTURES | AUDIO:
   raw=meta.read_text(encoding='utf-8',errors='replace')
   if ext in TEXTURES:
    entry['max_texture_size']=int((re.search(r'  maxTextureSize: (\d+)',raw) or [None,'0'])[1])
    entry['android_override']=bool(re.search(r'buildTarget: Android\s+maxTextureSize:.*?overridden: 1',raw,re.S))
   elif ext in MODEL:
    entry['animation_type']=int((re.search(r'animationType: (\d+)',raw) or [None,'0'])[1])
  entries.append(entry)
 duplicates={g:p for g,p in guids.items() if len(p)>1}
 for e in entries:
  if e.get('missing_script_slots'): problems.append({'kind':'missing_script','path':e['path'],'count':e['missing_script_slots']})
  if e.get('dimensions') and max(e['dimensions'])>2048: problems.append({'kind':'source_texture_over_2k','path':e['path'],'dimensions':e['dimensions']})
 result={'schema':1,'scanned_root':'Assets','packs':packs,'entries':entries,'duplicate_guids':duplicates,'material_dependencies':material_refs,'findings':problems}
 (OUT/'inventory.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 lines=['# Imported asset inventory — 21 October 2026','','Filesystem inspection covers every file under Assets. Categories are search hints, not proof of render/runtime compatibility.','Third-party asset names are retained to preserve GUID references. Source texture size is not a measured Android memory cost.','']
 for name,p in packs.items():
  if p['existing_project_folder']: continue
  lines += ['## '+name,'',f"{p['files']} files; {p['bytes']/1048576:.1f} MiB of source assets; {len(p['prefabs'])} prefabs; {len(p['models'])} models; {len(p['audio'])} audio clips.",'Likely roles: '+', '.join(p['categories'])+'.','']
  lines += ['- '+candidate for candidate in (p['prefabs'][:8] or p['models'][:5] or p['audio'][:4])]
  lines += ['']
 (OUT/'PACK_INVENTORY.md').write_text('\n'.join(lines),encoding='utf-8')
 print(json.dumps({'packs':len(packs),'imported_packs':sum(not p['existing_project_folder'] for p in packs.values()),'files':sum(p['files'] for p in packs.values()),'source_gib':round(sum(p['bytes'] for p in packs.values())/1073741824,2),'prefabs':sum(len(p['prefabs']) for p in packs.values()),'duplicate_guids':len(duplicates),'manifest':str(OUT/'inventory.json')}))

if __name__=='__main__': main()
