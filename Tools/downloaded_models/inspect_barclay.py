import bpy

bpy.ops.wm.read_factory_settings(use_empty=True)
try:
    bpy.ops.import_scene.fbx(filepath='Temp/BarclayExtract/source/unit_2065_high.fbx')
    print('=== BARCLAY OBJECTS ===')
    for o in bpy.context.scene.objects[:20]:
        print(o.name, o.type)
    print('=== ARMATURES & ANIMATIONS ===')
    for a in bpy.data.actions:
        print(a.name)
except Exception as e:
    print('Error:', e)
