"""Normalize the generated brand family and prepare native launch artwork."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Assets/Editor/StickerArtSource'
DEST=ROOT/'Assets/Resources/MenuArt'

def load(name,size):
    image=Image.open(SOURCE/(name+'.png')).convert('RGBA')
    bounds=image.getchannel('A').point(lambda a:255 if a>32 else 0).getbbox()
    image=image.crop(bounds); image.thumbnail(size,Image.Resampling.LANCZOS)
    return image

def main():
    logo=load('brand_logo',(960,340));hero=load('brand_startup',(620,646))
    sheet=Image.new('RGBA',(1024,1024));regions=[]
    for key,art,x,y in [('brand_logo',logo,24,8),('brand_startup',hero,202,365)]:
        sheet.paste(art,(x,y))
        regions.append(dict(key=key,x=x,y=1024-y-art.height,width=art.width,height=art.height,border=0))
    sheet.save(DEST/'sticker_brand_atlas.bytes',format='PNG',optimize=True)
    (DEST/'sticker_brand_atlas_index.json').write_text(json.dumps(dict(width=1024,height=1024,regions=regions),indent=2)+'\n',encoding='utf-8')
    # New launcher icon uses the same new puzzle-and-marker illustration.
    icon=Image.new('RGBA',(512,512),'#FFE7EF');mark=load('brand_startup',(446,446))
    icon.alpha_composite(mark,((512-mark.width)//2,(512-mark.height)//2));icon.convert('RGB').save(ROOT/'Assets/Branding/app-icon.png',optimize=True)
    # WeChat resource initialization is a restrained lavender brand splash.
    # The engine's later loading screen owns the cream journal illustration.
    cover=Image.new('RGBA',(1080,2400),'#EFE4FC');draw=ImageDraw.Draw(cover)
    for offset in range(-2400,1080,140):
        draw.line((offset,0,offset+2400,2400),fill='#E8DCF5',width=2)
    art=load('brand_logo',(960,340))
    cover.alpha_composite(art,((1080-art.width)//2,1040-art.height//2))
    target=ROOT/'Assets/WX-WASM-SDK-V2/Runtime/wechat-default/images'
    cover.convert('RGB').save(target/'background.jpg',quality=90,optimize=True)
    # The SDK icon is disabled; retain a valid branded file for its fallback.
    logo.save(target/'unity_logo.png',optimize=True)
    print('Brand atlas bytes:',(DEST/'sticker_brand_atlas.bytes').stat().st_size)

if __name__=='__main__':main()
