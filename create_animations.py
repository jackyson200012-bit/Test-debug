import os

# Animation frame assignments based on the sprite sheet analysis
# Row 1 (1-8): Idle - relatively static hand pose
# Row 2 (9-16): Walk - moderate finger movement
# Row 3 (17-24): Run - more dynamic movement
# Row 4-5 (25-40): Jump - large dynamic movements
# Row 6 (41-48): Additional jump/landing frames

animations = {
    "Idle": list(range(1, 13)),      # Frames 1-12 (static poses)
    "Walk": list(range(13, 25)),     # Frames 13-24 (moderate movement)
    "Run": list(range(25, 37)),      # Frames 25-36 (dynamic movement)
    "Jump": list(range(37, 49))      # Frames 37-48 (large movements)
}

# Unity animation clip template
def create_anim_clip(anim_name, frame_list, fps=12):
    """Generate a Unity .anim file for sprite animation"""
    
    # Calculate timing
    frame_duration = 1.0 / fps
    total_duration = len(frame_list) * frame_duration
    
    # Build the sprite keyframes
    sprite_curves = []
    for i, frame_num in enumerate(frame_list):
        time = i * frame_duration
        # Reference the sprite from the spritesheet
        # Using the GUID of FrameContactSheet.png
        sprite_curves.append(f"""        - time: {time:.6f}
          value: {{fileID: 21300000, guid: f2fe6cda40a94b9cb0f249c8661717ee, type: 3}}
          inSlope: 0
          outSlope: 0
          tangentMode: 0
          weightedMode: 0
          inWeight: 0.33333334
          outWeight: 0.33333334""")
    
    sprite_curve_block = "\n".join(sprite_curves)
    
    anim_clip = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {anim_name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves:
  - curve:
      serializedVersion: 2
      m_Curve:
{sprite_curve_block}
      m_PreWrapMode: 0
      m_PostWrapMode: 0
      path: 
      attribute: sprite
      classID: 212
      script: {{fileID: 0}}
      flags: 0
  m_PPtrCurves: []
  m_SampleRate: {fps}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_StartTime: 0
    m_StopTime: {total_duration:.6f}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_LoopTime: 1
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""
    return anim_clip

# Create Animations directory
output_dir = r'D:\Test debug\Test debug\Assets\Animations'
os.makedirs(output_dir, exist_ok=True)

# Generate each animation clip
for anim_name, frames in animations.items():
    anim_content = create_anim_clip(anim_name, frames)
    filepath = os.path.join(output_dir, f'{anim_name}.anim')
    with open(filepath, 'w') as f:
        f.write(anim_content)
    print(f"Created: {anim_name}.anim ({len(frames)} frames)")

print(f"\nAll animation clips created in: {output_dir}")
