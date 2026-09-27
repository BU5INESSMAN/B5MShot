"""Encode frames rendered by B5MShot.Demo; never captures the user's desktop."""
import sys
from pathlib import Path
from PIL import Image

source, target = map(Path, sys.argv[1:3])
target.mkdir(parents=True, exist_ok=True)
frames = [Image.open(p).convert('RGB').resize((1008, 702)) for p in sorted(source.glob('frame-*.png'))]
if len(frames) != 140:
    raise SystemExit('Expected exactly 140 renderer frames')
frames[110].save(target / 'editor-poster.webp', quality=90)
frames[0].save(target / 'editor-demo.webp', save_all=True, append_images=frames[1:], duration=50, loop=0, quality=78, method=4)
for name in ('settings', 'tray'):
    Image.open(source / f'{name}.png').save(target / f'{name}.webp', lossless=True)
print(f'Demo: {(target / "editor-demo.webp").stat().st_size} bytes')
