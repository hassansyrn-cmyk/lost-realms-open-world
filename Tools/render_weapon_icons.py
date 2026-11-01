import bpy, os, math

weapons = [
    (0, "Assets/Resources/Weapons/Aster_CurvedSword.fbx", "Assets/Resources/Weapons/Textures/Aster_CurvedSword_basecolor.jpg"),
    (1, "Assets/Resources/Weapons/PureSword1H.fbx", "Assets/Resources/Weapons/Textures/PureSword1H_basecolor.jpg"),
    (2, "Assets/Resources/Weapons/Aster_Axe.fbx", "Assets/Resources/Weapons/Textures/Aster_Axe_basecolor.jpg"),
    (3, "Assets/PurePoly/Free Fantasy RPG Weapons/Meshes/PP_Theme_01_Axe_Two_Handed_002.fbx", "Assets/Resources/Weapons/Textures/PureAxe2H_basecolor.jpg"),
    (4, "Assets/Resources/Weapons/RangerAxe.fbx", "Assets/Resources/Weapons/Textures/RangerAxe_basecolor.jpg"),
    (5, "Assets/Resources/Weapons/PureSpear.fbx", "Assets/Resources/Weapons/Textures/PureSpear_basecolor.jpg"),
    (6, "Assets/Resources/Weapons/PureHammer.fbx", "Assets/Resources/Weapons/Textures/PureHammer_basecolor.jpg"),
    (7, "Assets/Resources/Weapons/FrostHalberd.fbx", "Assets/Resources/Weapons/Textures/FrostHalberd_basecolor.png"),
    (8, "Assets/Resources/Weapons/OrnateCurvedBlade.fbx", "Assets/Resources/Weapons/Textures/OrnateCurvedBlade_basecolor.jpg"),
    (16, "Assets/PurePoly/Free Fantasy RPG Weapons/Meshes/PP_Theme_11_Sword_One-Handed_003.fbx", "Assets/Resources/Weapons/Textures/SwordShort_basecolor.jpg"),
    (17, "Assets/PurePoly/Free Fantasy RPG Weapons/Meshes/PP_Theme_11_Axe_One_Handed_002.fbx", "Assets/Resources/Weapons/Textures/AxeIron_basecolor.jpg")
]

for wid, rel_fbx, rel_tex in weapons:
    fbx_path = os.path.abspath(rel_fbx)
    tex_path = os.path.abspath(rel_tex)
    
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    # Camera
    cam_data = bpy.data.cameras.new("IconCamera")
    cam_data.type = 'ORTHO'
    cam_obj = bpy.data.objects.new("IconCamera", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    cam_obj.location = (0, -3.0, 0)
    cam_obj.rotation_euler = (math.radians(90), 0, 0)

    # Lighting
    light_data = bpy.data.lights.new(name="KeyLight", type='SUN')
    light_data.energy = 4.0
    light_obj = bpy.data.objects.new(name="KeyLight", object_data=light_data)
    scene.collection.objects.link(light_obj)
    light_obj.rotation_euler = (math.radians(45), math.radians(-30), math.radians(45))

    rim_data = bpy.data.lights.new(name="RimLight", type='SUN')
    rim_data.energy = 2.5
    rim_data.color = (0.75, 0.88, 1.0)
    rim_obj = bpy.data.objects.new(name="RimLight", object_data=rim_data)
    scene.collection.objects.link(rim_obj)
    rim_obj.rotation_euler = (math.radians(-45), math.radians(150), 0)

    if not os.path.exists(fbx_path):
        print(f"File not found: {fbx_path}")
        continue

    bpy.ops.import_scene.fbx(filepath=fbx_path)
    mesh_objs = [o for o in scene.objects if o.type == 'MESH']
    if not mesh_objs:
        print(f"No mesh in {fbx_path}")
        continue

    # Load and assign texture
    mat = bpy.data.materials.new(name="WeaponMat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if os.path.exists(tex_path):
        img = bpy.data.images.load(tex_path)
        tex_node = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex_node.image = img
        mat.node_tree.links.new(bsdf.inputs['Base Color'], tex_node.outputs['Color'])

    for o in mesh_objs:
        o.data.materials.clear()
        o.data.materials.append(mat)

    # Calculate bounding box in world space
    min_v = [1e9]*3
    max_v = [-1e9]*3
    for o in mesh_objs:
        for v in o.data.vertices:
            wco = o.matrix_world @ v.co
            for i in range(3):
                min_v[i] = min(min_v[i], wco[i])
                max_v[i] = max(max_v[i], wco[i])

    center = [(min_v[i] + max_v[i]) / 2.0 for i in range(3)]
    size = [max_v[i] - min_v[i] for i in range(3)]
    max_dim = max(size)

    # Center object
    parent_empty = bpy.data.objects.new("CenterEmpty", None)
    scene.collection.objects.link(parent_empty)
    for o in mesh_objs:
        if o.parent is None:
            o.parent = parent_empty
    parent_empty.location = (-center[0], -center[1], -center[2])
    
    # Rotate 45 deg for dynamic weapon display angle
    parent_empty.rotation_euler = (math.radians(20), math.radians(-35), math.radians(45))

    # Fit ortho camera
    cam_data.ortho_scale = max_dim * 1.35
    cam_obj.location = (0, -max_dim * 2.0, 0)

    out_file = f"Assets/Resources/UI/Icons/Weapons/Weapon_{wid}.png"
    scene.render.filepath = os.path.abspath(out_file)
    bpy.ops.render.render(write_still=True)
    print(f"Rendered {out_file} successfully!")
