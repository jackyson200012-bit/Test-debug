import re, struct, os

with open(r'D:\Test debug\Test debug\Assets\FBX\f2fe6cda-40a9-4b9c-b0f2-49c8661717ee_all_animations_unity.fbx', 'rb') as f:
    data = f.read()

# Parse FBX binary header
# The FBX header contains:
# - Magic number
# - File version
# - Header extension length
# - Header extension data
# - Data section (which contains the actual content)

# Find the header extension
# Header structure:
# 0x00-0x07: "Kaydara FBX Binary  \n" (or similar)
# 0x08-0x0B: File version (int32)
# Then header extension...

print("FBX Header analysis:")
print(f"  Bytes 0-15: {data[:16]}")
file_version = struct.unpack('<i', data[8:12])[0] if len(data) >= 12 else 0
print(f"  File version: {file_version}")

# Find header extension length
# In FBX 7.4+, the header extension contains:
# - Magic (0x00000001)
# - Version (int32)
# - Number of header extensions (int32)
# - Each extension has a type and length

# Let's find the data section start
# The header extension ends when we hit the data section
# Data section starts with objects (like !u! in YAML)

# Actually, the FBX binary format is:
# Header (variable length)
# Header Extension (variable length)
# Data (binary objects)

# For Blender FBX, the structure is:
# - Header: "Kaydara FBX Binary\n" + version
# - Header Extension: contains file info
# - Data: binary objects (geometry, skins, animations, etc.)

print("\n=== LOOKING FOR BONE PARENT-CHILD RELATIONSHIPS ===")
# In FBX, bones are stored as:
# Each bone has a "LimbNode" object with:
# - Name
# - Parent index (which points to another bone in the array, or -1 for root)
# - A rest position

# The bones are listed in a "Skelton" (typo in FBX) or "Skeleton" object
# Each bone's parent is stored as a long (int64) or int (int32) depending on the FBX version

# Let's find all bone names and their positions
strings = re.findall(b'[\x20-\x7e]{4,}', data)
all_text = ' '.join(s.decode('ascii', errors='replace') for s in strings)

# Find all bone names in the order they appear
bone_pattern = r'(spine_\d+\.x|neck\.x|head\.x|shoulder\.[lr]|arm_stretch\.[lr]|arm_twist_\d+\.[lr]|forearm_stretch\.[lr]|forearm_twist_\d+\.[lr]|hand\.[lr]|leg_stretch\.[lr]|foot\.[lr]|leg_twist_\d+\.[lr]|HiP|gBIp|lEG|LEg|eyeP|eye_P|spine_04|arm_twist\.[lr]|forearm_twist\.[lr]|forearm_stretch\.l|arm_twist\.l|arm_twist\.r)'
all_bones = re.findall(bone_pattern, all_text)
unique_bones = list(dict.fromkeys(all_bones))

print(f"Bones in appearance order ({len(unique_bones)} total):")
for i, b in enumerate(unique_bones):
    print(f"  {i}: {b}")

print("\n=== LOOKING FOR BONE INDEX REFERENCE PATTERN ===")
# In FBX binary, bones are stored in an array. Each bone has a parent index.
# The parent index points to the bone's parent in the array.
# Root bone has parent index -1 or 0 (depending on convention).

# Let's look at the structure around each bone to find parent relationships
# The pattern is: LimbNode [data] Skeleton [data] boneName
# Followed by another LimbNode with the same pattern

# Let's find all bone positions in the data
bone_positions = []
for i, s in enumerate(strings):
    decoded = s.decode('ascii', errors='replace')
    if decoded in unique_bones:
        bone_positions.append((i, decoded))

print(f"\nBone string indices: {[(pos[0], pos[1]) for pos in bone_positions[:20]]}")

print("\n=== LOOKING FOR ROOT NODE NAME ===")
# The root node name is typically the scene/character name from Blender
# It appears near RootNode in the FBX
root_patterns = re.findall(r'RootNodeL.{0,200}NodeAttribute', all_text)
for rp in root_patterns[:3]:
    print(f"  Root node region: {repr(rp[:80])}")

# Find all node names near RootNode
node_names = re.findall(r'RootNodeL.{0,500}LimbNode', all_text)
if node_names:
    print(f"  Root node data: {repr(node_names[0][:200])}")

print("\n=== LOOKING FOR MESH SUBASSET INDEX ===")
# In Unity's YAML format, a mesh from an FBX is referenced as:
# {fileID: <subasset_index>, guid: <guid>, type: 3}
# The subasset_index is typically 1 for the first mesh in an FBX

# Let's find the mesh name in the FBX
mesh_pattern = re.findall(r'FbxMesh.{0,100}', all_text)
print(f"FbxMesh references: {mesh_pattern[:3]}")

print("\n=== LOOKING FOR ANIMATION CLIPS ===")
# Find animation curve nodes
anim_pattern = re.findall(r'AnimationCurveNode.{0,100}', all_text)
print(f"AnimationCurveNode references: {len(anim_pattern)} found")

# Find all unique animation curve node names
all_anim_names = re.findall(r'AnimCurveNodeS\s*[\x00-\xff]{0,50}(.{2,20})', all_text)
unique_anim = list(dict.fromkeys(all_anim_names))
print(f"Unique animation names: {unique_anim[:20]}")

# Check for animation clip names
clip_names = re.findall(r'"(Action.{0,30}|Clip.{0,30}|Animation.{0,30})"', all_text)
print(f"Animation clip names: {clip_names}")
