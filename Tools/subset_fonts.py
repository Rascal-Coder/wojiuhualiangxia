from pathlib import Path
import sys,json,shutil
P=Path(__file__).resolve().parents[1]
# Subset OFL display/body fonts after all new labels have been authored.
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset
chars=set(chr(c) for c in range(32,127))
for f in (P/'Assets/Scripts').rglob('*.cs'):chars.update(f.read_text(encoding='utf-8-sig',errors='ignore'))
chars.update('我就划两下快乐集章元气手账好心情小确幸有灵感超顺手会发光真厉害涂鸦笔冰冰饮灵感镜')
fontreport=[]
F=P/'Assets/Editor/FontSource'
for source,weight,name,family,license in [(F/'ZCOOLKuaiLe-Regular.ttf',None,'LiangxiaSticker-Display','Liangxia Sticker',F/'ZCOOLKuaiLe-OFL.txt'),(F/'NotoSansSC-variable.ttf',600,'LiangxiaUISans-Medium','Liangxia UI Sans',F/'NotoSansSC-OFL.txt')]:
 font=TTFont(source)
 if weight:instantiateVariableFont(font,{'wght':weight},inplace=True)
 opt=subset.Options();opt.name_IDs=['*'];opt.name_legacy=True;opt.name_languages=['*']
 sub=subset.Subsetter(options=opt);sub.populate(text=''.join(chars));sub.subset(font)
 for rec in font['name'].names:
  v={1:family,2:'Regular',3:name+' 20261022',4:family,6:name,16:family,17:'Regular'}
  if rec.nameID in v:rec.string=v[rec.nameID].encode(rec.getEncoding(),errors='replace')
 dest=P/'Assets/Resources/Fonts'/(name+'.ttf');font.save(dest);shutil.copyfile(license,dest.with_name(name+'-OFL.txt'))
 fontreport.append(dict(name=name,bytes=dest.stat().st_size,glyphs=len(font.getBestCmap())))
(P/'Docs/贴纸字体子集.json').write_text(json.dumps(fontreport,indent=2),encoding='utf-8')
print(fontreport)
