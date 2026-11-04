"""Pack menu chrome from StickerArtSource using Unity Sprite.Create bottom-left rects."""
from pathlib import Path
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
INDEX = ROOT / 'Assets/Resources/MenuArt/menu_ui_atlas_index.json'
SOURCE_PNG = ROOT / 'Assets/Editor/MenuArtSource/menu_ui_atlas.png'
DEST_BYTES = ROOT / 'Assets/Resources/MenuArt/menu_ui_atlas.bytes'


def fit(image, width, height, pad=4):
    image = image.convert('RGBA')
    box = image.getbbox()
    if box:
        image = image.crop(box)
    inner = (max(8, width - pad * 2), max(8, height - pad * 2))
    image.thumbnail(inner, Image.Resampling.LANCZOS)
    sheet = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    sheet.alpha_composite(image, ((width - image.width) // 2, (height - image.height) // 2))
    return sheet


def main():
    index = json.loads(INDEX.read_text(encoding='utf-8'))
    width, height = int(index['width']), int(index['height'])
    atlas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    packed = 0
    for region in index['regions']:
        source = region.get('source')
        if not source:
            continue
        path = ROOT / source
        if not path.exists():
            print('missing', path)
            continue
        art = fit(Image.open(path), region['width'], region['height'])
        # Index y is Unity bottom-left. PIL paste is top-left.
        paste_y = height - region['y'] - region['height']
        atlas.alpha_composite(art, (region['x'], paste_y))
        packed += 1
    atlas.save(SOURCE_PNG, format='PNG', optimize=True)
    shutil.copyfile(SOURCE_PNG, DEST_BYTES)
    print('packed', packed, 'regions', SOURCE_PNG.stat().st_size, 'bytes')


if __name__ == '__main__':
    main()
