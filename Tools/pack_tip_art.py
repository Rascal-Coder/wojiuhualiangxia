"""Pack original sticker illustrations for large loading tips; never upscale."""
from pathlib import Path
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/Editor/StickerArtSource'
DEST = ROOT / 'Assets/Resources/MenuArt'

def main():
    sheet = Image.new('RGBA', (1024, 1024))
    regions = []
    for i, (key, source) in enumerate([
        ('tip_pen', 'menu_pen'), ('tip_freeze', 'menu_freeze'),
        ('tip_lens', 'menu_lens'), ('tip_time', 'ink_time')
    ]):
        art = Image.open(SOURCE / (source + '.png')).convert('RGBA')
        art = art.crop(art.getbbox())
        art.thumbnail((480, 480), Image.Resampling.LANCZOS)
        x, y = (i % 2) * 512 + 16, (i // 2) * 512 + 16
        sheet.paste(art, (x, y))
        regions.append(dict(key=key, x=x, y=1024-y-art.height,
                            width=art.width, height=art.height, border=0))
    sheet.save(DEST / 'sticker_tips_atlas.bytes', format='PNG', optimize=True)
    (DEST / 'sticker_tips_atlas_index.json').write_text(
        json.dumps(dict(width=1024, height=1024, regions=regions), indent=2)+'\n', encoding='utf-8')
    print('Tips atlas:', (DEST / 'sticker_tips_atlas.bytes').stat().st_size, 'bytes')

if __name__ == '__main__':
    main()
