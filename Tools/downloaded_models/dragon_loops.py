import bpy,sys,json,math
bpy.ops.import_scene.gltf(filepath='C:/Users/Hasan/Downloads/mountain_dragon.glb');s=bpy.context.scene;o=next(o for o in s.objects if o.type=='ARMATURE'); poses={}
for f in range(80,2241,2):
 s.frame_set(f);poses[f]=[b.matrix.to_quaternion().copy() for b in o.pose.bones if 'Wing' in b.name or 'Head' in b.name or 'thigh' in b.name]
rows=[]
for a in range(80,2160,2):
 for length in range(24,81,2):
  b=a+length
  if b not in poses:continue
  d=sum(min(x.rotation_difference(y).angle,2*math.pi-x.rotation_difference(y).angle)**2 for x,y in zip(poses[a],poses[b]))/len(poses[a]);rows.append((d,a,b))
print('BEST_LOOPS',sorted(rows)[:20])
