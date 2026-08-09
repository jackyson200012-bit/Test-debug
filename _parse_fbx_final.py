import re, struct, os

with open(r'D:\Test debug\Test debug\Assets\FBX\f2fe6cda-40a9-4b9c-b0f2-49c8661717ee_all_animations_unity.fbx', 'rb') as f:
    data = f.read()

# Parse FBX header
print("FBX Header:")
print(f"  Size: {len(data)} bytes")
print(f"  Header: {data[:20]}")

# Find all readable strings
strings = re.findall(b'[\x20-\x7e]{4,}', data)
all_text = ' '.join(s.decode('ascii', errors='replace') for s in strings)

# Find all bone names in order of appearance
bone_pattern = r'(spine_[0-9]+\.x|neck\.x|head\.x|shoulder\.[lr]|arm_stretch\.[lr]|arm_twist_[0-9]+\.lr|forearm_stretch\.[lr]|forearm_twist_[0-9]+\.lr|hand\.[lr]|leg_stretch\.[lr]|foot\.[lr]|leg_twist_[0-9]+\.lr|HiP|gBIp|lEG|LEg|eyeP|eye_P|spine_04|arm_twist\.[lr]|forearm_twist\.[lr])'
all_bones = re.findall(bone_pattern, all_text)
unique_bones = list(dict.fromkeys(all_bones))

print(f"\nBONES ({len(unique_bones)} unique, in appearance order):")
for i, b in enumerate(unique_bones):
    print(f"  {i}: {b}")

# Root bone is typically the last one or the one named gBIp/Hips
root_bone = 'gBIp' if 'gBIp' in unique_bones else 'HiP' if 'HiP' in unique_bones else 'unknown'
print(f"\nROOT BONE: {root_bone}")

# Find material names
mat_names = re.findall(r'Material\.([A-Za-z0-9_.]+)', all_text)
print(f"\nMATERIALS: {list(set(mat_names))}")

# Find animation clip names
all_strings = re.findall(r'"([^"]{3,50})"', all_text)
anim_names = [s for s in all_strings if any(kw in s.lower() for kw in ['walk', 'run', 'jump', 'idle', 'attack', 'anim', 'clip', 'action', 'motion', 'blend'])]
print(f"ANIMATIONS: {list(set(anim_names))}")

# Find the root node / scene object name
# Look for names near RootNode or in the SceneInfo
root_names = re.findall(r'RootNode.{0,200}', all_text)
print(f"\nROOT NODE REGIONS: {len(root_names)} found")
for rn in root_names[:3]:
    readable = ''.join(chr(b) if 32 <= b <= 126 else '' for b in rn.encode())
    print(f"  {repr(readable[:80])}")

# Find all potential scene/object names (readable strings that look like names)
object_names = [s for s in all_strings if re.match(r'^[A-Za-z0-9_\.\- ]{2,40}$', s) and len(s) > 1]
# Filter out common non-name strings
filtered_names = [s for s in object_names if s not in ['uv', 'Material', 'Mesh', 'FbxMesh', 'ColorS', 'ColorRGBS', 'LimbNode', 'Skeleton', 'Deformer', 'Skin', 'Geometry', 'FbxGeometry', 'FbxNode', 'NodeAttribute', 'Node', 'RootNode', 'GlobalSettings', 'GlobalInfo', 'SceneInfo', 'UserData', 'MetaData', 'VersionId', 'Title', 'Subject', 'Author', 'Keywords', 'Revision', 'CreationTimeStamp', 'FBXHeaderExtension', 'FBXHeaderVersion', 'FBXVersion', 'EncryptionType', 'Creator', 'Blender', 'AnimationCurveNode', 'AnimCurveNode', 'AnimationCurve', 'Number', 'LayerElement', 'Material', 'PolygonVertexIndex', 'ByPolygonVertex', 'GeometryVersion', 'Primary', 'Secondary', 'Tertiary', 'Quaternary', 'Quinary', 'Senary', 'Septenary', 'Octonary', 'Nonary', 'Denary']]
print(f"\nPOTENTIAL SCENE NAMES: {list(set(filtered_names))[:20]}")
