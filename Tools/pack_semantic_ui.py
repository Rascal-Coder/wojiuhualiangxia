"""Repack the semantic atlas. Index y is Unity bottom-left, matching Sprite.Create."""
from pathlib import Path
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
INDEX = ROOT / 'Assets/Resources/MenuArt/sticker_semantic_atlas_index.json'
SOURCE = ROOT / 'Assets/Editor/StickerArtSource'
PREVIEW = ROOT / 'Assets/Editor/MenuArtSource/sticker_semantic_atlas.png'
DEST = ROOT / 'Assets/Resources/MenuArt/sticker_semantic_atlas.bytes'


def fit(image, width, height, pad=6):
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
        path = SOURCE / (region['key'] + '.png')
        if not path.exists():
            print('missing', path)
            continue
        art = fit(Image.open(path), region['width'], region['height'])
        paste_y = height - region['y'] - region['height']
        atlas.alpha_composite(art, (region['x'], paste_y))
        packed += 1
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(PREVIEW, format='PNG', optimize=True)
    shutil.copyfile(PREVIEW, DEST)
    print('packed', packed, 'regions', DEST.stat().st_size, 'bytes')


if __name__ == '__main__':
    main()
