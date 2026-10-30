import bpy, json, sys, os
from mathutils import Vector
root = sys.argv[sys.argv.index('--') + 1]
reports=[]
files=[('Civilian',os.path.join(root,'Assets/GameContent/DownloadedModels/Civilian/Source/Civilian 3.fbx')),('Komodo',r'C:/Users/Hasan/Downloads/komodo_dragon_lizard.glb'),('MountainDragon',r'C:/Users/Hasan/Downloads/mountain_dragon.glb')]
for name,path in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.lower().endswith('.fbx'): bpy.ops.import_scene.fbx(filepath=path)
    else: bpy.ops.import_scene.gltf(filepath=path)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    pts=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
    lo=[min(p[i] for p in pts) for i in range(3)]; hi=[max(p[i] for p in pts) for i in range(3)]
    rep={'model':name,'bounds':[lo,hi],'meshes':[{'name':o.name,'verts':len(o.data.vertices),'tris':sum(len(p.vertices)-2 for p in o.data.polygons),'shape_keys':len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0,'materials':[m.name for m in o.data.materials if m]} for o in meshes], 'armatures':[{'name':o.name,'bones':[b.name for b in o.data.bones]} for o in bpy.context.scene.objects if o.type=='ARMATURE'],'actions':[{'name':a.name,'range':list(a.frame_range)} for a in bpy.data.actions], 'images':[{'name':i.name,'size':list(i.size)} for i in bpy.data.images]}
    reports.append(rep)
    with open(os.path.join(root,'Tools/downloaded_models/inspection.json'),'w') as f: json.dump(reports,f,indent=2)
    print('MODEL_REPORT',name,json.dumps(rep))
