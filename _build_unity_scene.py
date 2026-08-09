import re

SCENE_FILE = r"D:\Test debug\Test debug\Assets\GameThatisanTestDebug.unity"

# Define the bone hierarchy based on appearance order
# Each entry: (bone_name, parent_name)
# parent_name is None for the root (CharacterModel), "gBIp" for bones directly under gBIp
BONE_PARENTS = {
    'spine_01.x': 'gBIp',
    'spine_02.x': 'spine_01.x',
    'spine_03.x': 'spine_02.x',
    'neck.x': 'spine_03.x',
    'head.x': 'neck.x',
    'shoulder.r': 'gBIp',
    'arm_stretch.r': 'shoulder.r',
    'arm_twist.r': 'arm_stretch.r',
    'arm_twist_2.r': 'arm_stretch.r',
    'arm_twist_3.r': 'arm_stretch.r',
    'arm_twist_4.r': 'arm_stretch.r',
    'arm_twist_5.r': 'arm_stretch.r',
    'arm_twist_6.r': 'arm_stretch.r',
    'forearm_stretch.r': 'arm_twist.r',
    'forearm_twist.r': 'forearm_stretch.r',
    'forearm_twist_2.r': 'forearm_stretch.r',
    'forearm_twist_3.r': 'forearm_stretch.r',
    'forearm_twist_4.r': 'forearm_stretch.r',
    'forearm_twist_5.r': 'forearm_stretch.r',
    'forearm_twist_6.r': 'forearm_stretch.r',
    'hand.r': 'forearm_twist.r',
    'shoulder.l': 'gBIp',
    'arm_stretch.l': 'shoulder.l',
    'arm_twist.l': 'arm_stretch.l',
    'arm_twist_2.l': 'arm_stretch.l',
    'arm_twist_3.l': 'arm_stretch.l',
    'arm_twist_4.l': 'arm_stretch.l',
    'arm_twist_5.l': 'arm_stretch.l',
    'arm_twist_6.l': 'arm_stretch.l',
    'forearm_stretch.l': 'arm_twist.l',
    'forearm_twist.l': 'forearm_stretch.l',
    'forearm_twist_2.l': 'forearm_stretch.l',
    'forearm_twist_3.l': 'forearm_stretch.l',
    'forearm_twist_4.l': 'forearm_stretch.l',
    'forearm_twist_5.l': 'forearm_stretch.l',
    'forearm_twist_6.l': 'forearm_stretch.l',
    'hand.l': 'forearm_twist.l',
    'leg_stretch.l': 'gBIp',
    'foot.l': 'leg_stretch.l',
    'leg_twist_2.l': 'foot.l',
    'leg_twist_3.l': 'foot.l',
    'leg_twist_4.l': 'foot.l',
    'leg_twist_5.l': 'foot.l',
    'leg_twist_6.l': 'foot.l',
    'leg_stretch.r': 'gBIp',
    'foot.r': 'leg_stretch.r',
    'leg_twist_2.r': 'foot.r',
    'leg_twist_3.r': 'foot.r',
    'leg_twist_4.r': 'foot.r',
    'leg_twist_5.r': 'foot.r',
    'leg_twist_6.r': 'foot.r',
    'eyeP': 'gBIp',
    'gBIp': '__CharacterModel__'  # Root bone is child of CharacterModel
}

# Order of bones (appearance order)
BONE_ORDER = [
    'spine_01.x', 'spine_02.x', 'spine_03.x', 'neck.x', 'head.x',
    'shoulder.r', 'arm_stretch.r', 'arm_twist.r',
    'arm_twist_2.r', 'arm_twist_3.r', 'arm_twist_4.r', 'arm_twist_5.r', 'arm_twist_6.r',
    'forearm_stretch.r', 'forearm_twist.r',
    'forearm_twist_2.r', 'forearm_twist_3.r', 'forearm_twist_4.r', 'forearm_twist_5.r', 'forearm_twist_6.r',
    'hand.r',
    'shoulder.l', 'arm_stretch.l', 'arm_twist.l',
    'arm_twist_2.l', 'arm_twist_3.l', 'arm_twist_4.l', 'arm_twist_5.l', 'arm_twist_6.l',
    'forearm_stretch.l', 'forearm_twist.l',
    'forearm_twist_2.l', 'forearm_twist_3.l', 'forearm_twist_4.l', 'forearm_twist_5.l', 'forearm_twist_6.l',
    'hand.l',
    'leg_stretch.l', 'foot.l',
    'leg_twist_2.l', 'leg_twist_3.l', 'leg_twist_4.l', 'leg_twist_5.l', 'leg_twist_6.l',
    'leg_stretch.r', 'foot.r',
    'leg_twist_2.r', 'leg_twist_3.r', 'leg_twist_4.r', 'leg_twist_5.r', 'leg_twist_6.r',
    'eyeP',
    'gBIp'
]

# Assign fileIDs: each bone gets a GameObject (even) and Transform (odd) fileID
BONE_BASE = 1500000200
FBX_GUID = "f203cc102eeed6441ba001629ccd650e"
MESH_SUBASSET = 4300001  # First mesh subasset in FBX

def make_yaml(bone_name, index):
    """Generate YAML for a single bone (GameObject + Transform)"""
    go_fid = BONE_BASE + index * 2
    tr_fid = BONE_BASE + index * 2 + 1
    parent = BONE_PARENTS[bone_name]

    # Determine parent transform fileID
    if parent == '__CharacterModel__':
        parent_fid = 1500000002  # CharacterModel's Transform
    elif parent in BONE_ORDER:
        parent_index = BONE_ORDER.index(parent)
        parent_fid = BONE_BASE + parent_index * 2 + 1  # parent's Transform
    else:
        parent_fid = 0

    # Start building children list
    children = []

    yaml = f'--- !u!1 &{go_fid}\n'
    yaml += f'GameObject:\n'
    yaml += f'  m_ObjectHideFlags: 0\n'
    yaml += f'  m_CorrespondingSourceObject: {{"fileID": 0}}\n'
    yaml += f'  m_PrefabInstance: {{"fileID": 0}}\n'
    yaml += f'  m_PrefabAsset: ""\n'
    yaml += f'  serializedVersion: 6\n'
    comp_ref = '{fileID: ' + str(tr_fid) + '}'
    yaml += f'  - component: {comp_ref}\n'
    yaml += f'  m_Layer: 0\n'
    yaml += f'  m_Name: {bone_name}\n'
    yaml += f'  m_TagString: Untagged\n'
    yaml += f'  m_Icon: {{"fileID": 0}}\n'
    yaml += f'  m_NavMeshLayer: 0\n'
    yaml += f'  m_StaticEditorFlags: 0\n'
    yaml += f'  m_IsActive: 1\n'

    return yaml, go_fid, tr_fid, parent_fid, children

def generate_scene():
    # Read existing scene
    with open(SCENE_FILE, 'r') as f:
        content = f.read()

    bone_yaml_parts = []
    bone_fileids = {}  # name -> (go_fid, tr_fid)

    for i, bone_name in enumerate(BONE_ORDER):
        yaml_part, go_fid, tr_fid, parent_fid, children = make_yaml(bone_name, i)
        bone_fileids[bone_name] = (go_fid, tr_fid)
        bone_yaml_parts.append(yaml_part)

    # Now build each Transform with correct children and parents
    # First pass: build parent->children mapping
    parent_children = {}
    for i, bone_name in enumerate(BONE_ORDER):
        parent = BONE_PARENTS[bone_name]
        if parent not in parent_children:
            parent_children[parent] = []
        parent_children[parent].append((i, bone_name))

    # Second pass: update each bone's Transform YAML with children
    transform_yaml_parts = []
    gBIp_tr_fid = None

    for i, bone_name in enumerate(BONE_ORDER):
        go_fid, tr_fid = bone_fileids[bone_name]
        parent = BONE_PARENTS[bone_name]

        if parent == '__CharacterModel__':
            parent_fid = 1500000002
        elif parent in BONE_ORDER:
            parent_index = BONE_ORDER.index(parent)
            parent_fid = BONE_BASE + parent_index * 2 + 1
        else:
            parent_fid = 0

        if bone_name == 'gBIp':
            gBIp_tr_fid = tr_fid

        # Get children of this bone
        children = parent_children.get(bone_name, [])
        child_fids = []
        for _, child_name in children:
            _, child_tr_fid = bone_fileids[child_name]
            child_fids.append(f'  - {{"fileID": {child_tr_fid}}}\n')

        yaml = f'--- !u!4 &{tr_fid}\n'
        yaml += f'Transform:\n'
        yaml += f'  m_ObjectHideFlags: 0\n'
        yaml += f'  m_CorrespondingSourceObject: {{"fileID": 0}}\n'
        yaml += f'  m_PrefabInstance: {{"fileID": 0}}\n'
        yaml += f'  m_PrefabAsset: ""\n'
        yaml += f'  m_GameObject: {{"fileID": {go_fid}}}\n'
        yaml += f'  serializedVersion: 2\n'
        yaml += f'  m_LocalRotation: {{"x": 0, "y": 0, "z": 0, "w": 1}}\n'
        yaml += f'  m_LocalPosition: {{"x": 0, "y": 0, "z": 0}}\n'
        yaml += f'  m_LocalScale: {{"x": 1, "y": 1, "z": 1}}\n'
        yaml += f'  m_ConstrainProportionsScale: 0\n'
        if child_fids:
            yaml += f'  m_Children:\n'
            yaml += ''.join(child_fids)
        else:
            yaml += f'  m_Children: []\n'
        yaml += f'  m_Father: {{"fileID": {parent_fid}}}\n'
        yaml += f'  m_LocalEulerAnglesHint: {{"x": 0, "y": 0, "z": 0}}\n'

        transform_yaml_parts.append(yaml)

    # Combine all bone YAML
    all_bone_yaml = ''.join(bone_yaml_parts) + '\n' + ''.join(transform_yaml_parts)

    # Find the CharacterModel section and update it
    # The CharacterModel has m_Children: [] currently
    # We need to add the gBIp bone's Transform as a child
    # Also update the SkinnedMeshRenderer's RootBone to point to gBIp's Transform

    # Find the CharacterModel GameObject section
    char_model_pattern = r'--- !u!1 &1500000001\nGameObject:.*?m_IsActive: 1'
    match = re.search(char_model_pattern, content, re.DOTALL)
    if match:
        # Update CharacterModel's m_Component list to include Animator (fileID: 1500000500)
        old_component = '''  m_Component:
  - component: {fileID: 1500000002}
  - component: {fileID: 1500000003}
  m_Layer: 0
  m_Name: CharacterModel'''
        new_component = '''  m_Component:
  - component: {fileID: 1500000002}
  - component: {fileID: 1500000003}
  - component: {fileID: 1500000500}
  m_Layer: 0
  m_Name: CharacterModel'''
        content = content.replace(old_component, new_component)

    # Update CharacterModel's m_Children to reference gBIp's Transform
    char_model_trans = r'--- !u!4 &1500000002\nTransform:.*?m_Father: \{fileID: 1433059771\}'
    match = re.search(char_model_trans, content, re.DOTALL)
    if match:
        old = match.group(0)
        # Replace m_Children: [] with m_Children: [{fileID: gBIp_tr_fid}]
        new = re.sub(
            r'm_Children: \[\]',
            f'  m_Children:\n  - {{"fileID": {gBIp_tr_fid}}}',
            old
        )
        content = content.replace(old, new)

    # Update SkinnedMeshRenderer's RootBone to point to gBIp's Transform
    content = content.replace(
        'm_RootBone: {fileID: 1500000002}',
        f'm_RootBone: {{"fileID": {gBIp_tr_fid}}}'
    )

    # Update Player's m_Component list to include the Animator component reference
    # Actually, the Animator is on the CharacterModel, not the Player

    # Add Animator component YAML after the SkinnedMeshRenderer section
    animator_yaml = f'''--- !u!95 &1500000500
Animator:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{"fileID": 0}}
  m_PrefabInstance: {{"fileID": 0}}
  m_PrefabAsset: ""
  m_GameObject: {{"fileID": 1500000001}}
  m_Enabled: 1
  m_Avatar: {{"fileID": 9000000, guid: {FBX_GUID}, type: 3}}
  m_Controller: {{"fileID": 9100000, guid: {FBX_GUID}, type: 3}}
  m_CullingMode: 0
  m_ActionsAsset: {{fileID: 8300000, guid: {FBX_GUID}, type: 3}}
  m_RootBone: {{"fileID": {gBIp_tr_fid}}}
  m_AppearsAsRoot: 1
'''

    # Find the SkinnedMeshRenderer section and insert Animator after it
    smr_pattern = r'(--- !u!137 &1500000003.*?m_AOMatrices8:\n)'
    match = re.search(smr_pattern, content, re.DOTALL)
    if match:
        insert_pos = match.end()
        content = content[:insert_pos] + animator_yaml + content[insert_pos:]

    # Insert all bone YAML before the SceneRoots section
    scene_roots_pattern = r'\n--- !u!1660057539 &9223372036854775807\nSceneRoots:'
    match = re.search(scene_roots_pattern, content)
    if match:
        insert_pos = match.start()
        content = content[:insert_pos] + '\n' + all_bone_yaml + '\n' + content[insert_pos:]

    # Write back
    with open(SCENE_FILE, 'w') as f:
        f.write(content)

    print(f"gBIp Transform fileID: {gBIp_tr_fid}")
    print(f"Total bones: {len(BONE_ORDER)}")
    print("Scene file updated successfully.")

if __name__ == "__main__":
    generate_scene()
