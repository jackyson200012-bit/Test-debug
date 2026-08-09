from PIL import Image
import os

# Load the sprite sheet
img = Image.open(r'D:\Test debug\Test debug\FrameContactSheet.png')
width, height = img.size

# Grid dimensions from the image: 8 columns, 6 rows
cols = 8
rows = 6

# Calculate frame size
frame_width = width // cols
frame_height = height // rows

print(f"Image size: {width}x{height}")
print(f"Frame size: {frame_width}x{frame_height}")

# Output directory
output_dir = r'D:\Test debug\Test debug\AnimationFrames'
os.makedirs(output_dir, exist_ok=True)

# Split into individual frames
frame_count = 0
for row in range(rows):
    for col in range(cols):
        # Calculate frame position
        left = col * frame_width
        upper = row * frame_height
        right = left + frame_width
        lower = upper + frame_height
        
        # Crop the frame
        frame = img.crop((left, upper, right, lower))
        
        # Save with zero-padded filename
        frame_count += 1
        filename = f"frame_{frame_count:03d}.png"
        frame.save(os.path.join(output_dir, filename))
        print(f"Saved: {filename}")

print(f"\nDone! Split {frame_count} frames into: {output_dir}")
