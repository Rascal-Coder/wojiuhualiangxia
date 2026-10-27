"""Pack the approved 4x2 avatar sheet into one padded runtime texture."""
from pathlib import Path
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/Editor/StickerArtSource/avatars_sheet_4x2.png'
DEST = ROOT / 'Assets/Resources/MenuArt'

def main():
    with Image.open(SOURCE) as source:
        assert source.size == (5016, 2508) and source.mode == 'RGBA'
        sheet = Image.new('RGBA', (1024, 512))
        regions = []
        for i in range(8):
            sx, sy = (i % 4) * 1254, (i // 4) * 1254
            sprite = source.crop((sx, sy, sx + 1254, sy + 1254))
            sprite = sprite.resize((240, 240), Image.Resampling.LANCZOS)
            x, y = (i % 4) * 256 + 8, (i // 4) * 256 + 8
            sheet.paste(sprite, (x, y))
            regions.append(dict(key=f'avatar_{i+1}', x=x, y=512-y-240,
                                width=240, height=240, border=0))
    sheet.save(DEST / 'sticker_avatars_atlas.bytes', format='PNG', optimize=True)
    (DEST / 'sticker_avatars_atlas_index.json').write_text(
        json.dumps(dict(width=1024, height=512, regions=regions), indent=2)+'\n', encoding='utf-8')
    print('Avatar atlas:', (DEST / 'sticker_avatars_atlas.bytes').stat().st_size, 'bytes; 8 regions')

if __name__ == '__main__':
    main()
