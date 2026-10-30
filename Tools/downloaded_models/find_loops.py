import bpy,sys,json
from mathutils import Vector
root=sys.argv[sys.argv.index('--')+1];out={}
for name,file in [('Komodo','komodo_dragon_lizard.glb'),('MountainDragon','mountain_dragon.glb')]:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath='C:/Users/Hasan/Downloads/'+file);scene=bpy.context.scene
 obj=next(o for o in scene.objects if o.type==('MESH' if name=='Komodo' else 'ARMATURE'))
 def sample(frame):
  scene.frame_set(frame)
  if name=='Komodo':
   ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();pts=[me.vertices[j].co.copy() for j in range(0,len(me.vertices),100)];ev.to_mesh_clear();return pts
  return [b.matrix.to_quaternion().copy() for b in obj.pose.bones if 'Wing' in b.name or 'Head' in b.name or 'thigh' in b.name]
 base=sample(1);rows=[]
 for f in range(12,192 if name=='Komodo' else 400):
  p=sample(f);d=sum((a-b).length_squared if name=='Komodo' else a.rotation_difference(b).angle**2 for a,b in zip(base,p))/len(p);rows.append((d,f))
 out[name]=sorted(rows)[:15]
with open(root+'/Tools/downloaded_models/loops.json','w') as f:json.dump(out,f,indent=2)

