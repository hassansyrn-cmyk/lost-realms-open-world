import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='C:/Users/Hasan/Downloads/komodo_dragon_lizard.glb')
s=bpy.context.scene;s.render.fps=24;s.frame_start=1;s.frame_end=26
obj=next(o for o in s.objects if o.type=='MESH');used=set()
for frame in range(1,27):
 s.frame_set(frame)
 used.update(k.name for k in obj.data.shape_keys.key_blocks if abs(k.value)>.00001)
for k in list(obj.data.shape_keys.key_blocks)[1:][::-1]:
 if k.name not in used:obj.shape_key_remove(k)
s.frame_set(1)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath='C:/Users/Hasan/Documents/Lost-Realms-Open-World/Assets/GameContent/DownloadedModels/Komodo/Source/Komodo.fbx',use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,path_mode='STRIP',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False)
print('RETAINED_SHAPES',len(obj.data.shape_keys.key_blocks),sorted(used))
