import re, struct, os

with open(r'D:\Test debug\Test debug\Assets\FBX\f2fe6cda-40a9-4b9c-b0f2-49c8661717ee_all_animations_unity.fbx', 'rb') as f:
    data = f.read()

print(f"FBX size: {len(data)} bytes")
print(f"Header: {data[:16]}")

# Parse FBX header to find offsets
# FBX binary format: header, header extension, data
# The header extension contains file info
# Data section contains the actual content

# Find all readable strings
strings = re.findall(b'[\x20-\x7e]{4,}', data)
all_text = ' '.join(s.decode('ascii', errors='replace') for s in strings)

print("\n=== BONE HIERARCHY ===")
# Find all bone names in the skeleton
bone_pattern = r'(spine_\d+\.x|neck\.x|head\.x|shoulder\.[lr]|arm_stretch\.[lr]|arm_twist_\d+\.[lr]|forearm_stretch\.[lr]|forearm_twist_\d+\.[lr]|hand\.[lr]|leg_stretch\.[lr]|foot\.[lr]|leg_twist_\d+\.[lr]|HiP|gBIp|lEG|LEg|eyeP|eye_P|spine_04|arm_twist\.[lr]|forearm_twist\.[lr]|forearm_stretch\.l|arm_twist\.l|arm_twist\.r)'
all_bones = re.findall(bone_pattern, all_text)
unique_bones = list(dict.fromkeys(all_bones))
print(f'Found {len(unique_bones)} unique bones:')
for i, b in enumerate(unique_bones):
    print(f'  {i}: {b}')

print("\n=== ROOT BONE ===")
# The root bone should be the last one listed or the one without a parent
# In FBX, the root bone is typically the one that has no parent
# Let's find it
hip_bones = [b for b in unique_bones if 'HiP' in b.upper() or 'hIp' in b or 'hIP' in b]
print(f'Hip bones found: {hip_bones}')

# The root bone in the bone array (last in appearance order is typically root in Blender exports)
print(f'Last bone (likely root): {unique_bones[-1] if unique_bones else "none"}')

print("\n=== MESH AND MATERIALS ===")
# Find mesh names
mesh_names = re.findall(r'"(Mesh[0-9A-Za-z_.\-]{0,50})"', all_text)
print(f'Mesh names in quoted strings: {mesh_names}')

# Find material names
mat_pattern = r'"Material\.[A-Za-z0-9_.]+|"[A-Z][A-Za-z0-9_]*\.mat'
mat_names = re.findall(mat_pattern, all_text)
print(f'Material names found: {list(set(mat_names))}')

# Find all material references in the readable strings
mat_refs = re.findall(r'Material\.[A-Za-z0-9_.]+', all_text)
print(f'Material references: {list(set(mat_refs))}')

print("\n=== ANIMATION CLIPS ===")
# Find animation clip names
clip_pattern = r'AnimCurveNodeL\s*[\x00-\xff]{0,50}AnimCurveNodeS\s*[\x00-\xff]{0,50}(.{2,30})'
clips = re.findall(clip_pattern, all_text)
unique_clips = list(dict.fromkeys(clips))
print(f'Animation curves found: {len(unique_clips)} unique')
for c in unique_clips[:10]:
    print(f'  {c}')

# Find if there are animation curve names
all_anims = re.findall(r'"([^"]{3,40})"', all_text)
anim_names = [a for a in all_anims if any(kw in a.lower() for kw in ['walk', 'run', 'jump', 'idle', 'attack', 'idle', 'anim', 'clip', 'action', 'motion'])]
print(f'Animation-related names: {list(set(anim_names))}')

print("\n=== SCENE/OBJECT NAMES ===")
# Find the root node name and scene object names
all_strings = re.findall(r'"([^"]{2,60})"', all_text)
# Filter for likely object/scene names
for s in all_strings[:100]:
    # Skip binary data (contains non-alphanumeric chars)
    if re.match(r'^[A-Za-z0-9_\.\- ]{1,40}$', s):
        print(f'  "{s}"')
