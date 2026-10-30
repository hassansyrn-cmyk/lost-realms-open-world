import bpy, os, sys, json, math
from mathutils import Vector
root=sys.argv[sys.argv.index('--')+1]
report=[]
for name,file in [('Komodo','komodo_dragon_lizard.glb'),('MountainDragon','mountain_dragon.glb')]:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath='C:/Users/Hasan/Downloads/'+file)
 scene=bpy.context.scene
 scene.render.fps=24
 for o in list(scene.objects):
  if o.type=='MESH' and not len(o.data.materials): bpy.data.objects.remove(o,do_unlink=True)
 out=root+'/Assets/GameContent/DownloadedModels/'+name+'/Source'
 os.makedirs(out,exist_ok=True)
 tex=[]
 for m in bpy.data.materials:
  if not m.use_nodes: continue
  for n in m.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    im=n.image; dest=out+'/'+im.name+'.png'; im.filepath_raw=dest; im.file_format='PNG'; im.save()
    tex.append({'material':m.name,'image':im.name,'links':[l.to_node.name+':'+l.to_socket.name for l in n.outputs['Color'].links]})
 actions=[a for a in bpy.data.actions if a.name!='ArmatureAction']
 action=next((a for a in actions if 'Take' in a.name or 'Object' in a.name),actions[0])
 start,end=action.frame_range;scene.frame_start=int(start);scene.frame_end=math.ceil(end)
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.fbx(filepath=out+'/'+name+'.fbx',use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,path_mode='STRIP',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False)
 originals=list(scene.objects); frames=[round(start+(end-start)*i/15) for i in range(16)]
 for i,f in enumerate(frames):
  scene.frame_set(f);dg=bpy.context.evaluated_depsgraph_get(); copies=[]; pts=[]
  for o in originals:
   if o.type!='MESH':continue
   ev=o.evaluated_get(dg);mesh=bpy.data.meshes.new_from_object(ev,depsgraph=dg)
   cp=bpy.data.objects.new(str(f)+'_'+o.name,mesh);scene.collection.objects.link(cp);cp.matrix_world=o.matrix_world.copy();copies.append(cp)
   pts.extend(cp.matrix_world@Vector(p) for p in cp.bound_box)
  lo=Vector([min(p[j] for p in pts) for j in range(3)]);hi=Vector([max(p[j] for p in pts) for j in range(3)])
  scale=3.3/max(hi-lo);center=(lo+hi)*.5
  for cp in copies:
   cp.matrix_world.translation-=center;cp.matrix_world.translation*=scale;cp.scale*=scale
   cp.location+=Vector(((i%4)*4.4,(i//4)*5,0))
 for o in originals:bpy.data.objects.remove(o,do_unlink=True)
 scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
 scene.world=bpy.data.worlds.new('World');scene.world.color=(.12,.12,.12)
 camdat=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdat);scene.collection.objects.link(cam)
 target=Vector((6.6,7.5,0));cam.location=target+Vector((12,-20,24));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();camdat.type='ORTHO';camdat.ortho_scale=26;scene.camera=cam
 scene.render.resolution_x=1500;scene.render.resolution_y=1500;scene.render.resolution_percentage=100;scene.render.filepath=root+'/Tools/downloaded_models/'+name+'-poses.png';bpy.ops.render.render(write_still=True)
 report.append({'name':name,'frames':frames,'range':[start,end],'textures':tex})
 with open(root+'/Tools/downloaded_models/conversion.json','w') as f:json.dump(report,f,indent=2)

