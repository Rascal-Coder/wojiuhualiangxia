using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Menu typography is deliberately independent of the source's display labels.
    // Use one readable face for body text, Chinese, punctuation and numbers.
    public static class MenuUI
    {
        public const int TitleSize=72,Body=48,Small=42;
        public static readonly Color Ink=InkTheme.Ink,Muted=InkTheme.Muted,Gold=InkTheme.Line,Green=InkTheme.Jade,Purple=InkTheme.Cinnabar;
        public static readonly Color32 Coral=new Color32(255,99,139,255),CoralChip=new Color32(255,168,190,255),Grape=new Color32(102,69,170,255),Cream=new Color32(255,249,238,255),Lemon=new Color32(255,243,209,255),MintWash=new Color32(221,248,237,255),Blush=new Color32(255,228,238,255),LilacWash=new Color32(239,228,255,255);
        static Font bodyFont,numberFont;
        static readonly Dictionary<string,Sprite> art=new Dictionary<string,Sprite>();
        [Serializable] public sealed class AtlasRegion{public string key;public int x,y,width,height,border,borderX,borderY;}
        [Serializable] public sealed class AtlasIndex{public int width,height;public AtlasRegion[] regions;}
        static bool atlasLoaded;
        static void LoadAtlas()
        {
            if(atlasLoaded)return;atlasLoaded=true;
            LoadSheet("menu_ui");LoadSheet("sticker_fx");LoadSheet("sticker_semantic");LoadSheet("sticker_barrier");
        }
        static void LoadSheet(string key)
        {
            var data=Resources.Load<TextAsset>("MenuArt/"+key+"_atlas");var index=Resources.Load<TextAsset>("MenuArt/"+key+"_atlas_index");if(!data||!index)return;
            bool effects=key!="menu_ui";var texture=new Texture2D(2,2,TextureFormat.RGBA32,effects);texture.name=effects?"Liangxia "+key+" atlas":"Liangxia shared UI atlas";
            if(!texture.LoadImage(data.bytes,true)){UnityEngine.Object.Destroy(texture);return;}texture.filterMode=effects?FilterMode.Trilinear:FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
            foreach(var r in JsonUtility.FromJson<AtlasIndex>(index.text).regions)art[r.key]=Sprite.Create(texture,new Rect(r.x,r.y,r.width,r.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(r.borderX>0?r.borderX:r.border,r.borderY>0?r.borderY:r.border,r.borderX>0?r.borderX:r.border,r.borderY>0?r.borderY:r.border));
            Resources.UnloadAsset(data);Resources.UnloadAsset(index);
        }
        public static Font BodyFont { get { if(!bodyFont)bodyFont=Resources.Load<Font>("Fonts/LiangxiaUISans-Medium");return bodyFont?bodyFont:Skin.ChineseFont; } }
        public static Font NumberFont{get{if(!numberFont)numberFont=Resources.Load<Font>("Fonts/LiangxiaUISans-Medium");return numberFont?numberFont:BodyFont;}}
        public static Text Label(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize=Body,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool display=false)
        {
            var r=Skin.Rect("Menu text "+text,parent,anchor,pos,size);var label=r.gameObject.AddComponent<Text>();
            label.font=display&&!(!string.IsNullOrEmpty(text)&&char.IsDigit(text[0]))?Skin.ChineseFont:Skin.HasChinese(text)?BodyFont:NumberFont;label.text=text;label.fontSize=fontSize;label.color=color??Ink;label.alignment=align;
            label.raycastTarget=false;label.supportRichText=false;label.resizeTextForBestFit=false;label.lineSpacing=1.06f;label.alignByGeometry=true;
            // CJK font ascent/descent can exceed the visible glyph height.
            // Do not drop a whole single line just because its line metrics
            // exceed a compact label box; the real glyph bounds are verified.
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        public static void StickerDepth(Transform parent,Vector2 size,float radius=-1)
        {
            float rad=radius>0?radius:Mathf.Min(48,Mathf.Min(size.x,size.y)*.2f);
            float drop=Mathf.Clamp(size.y*.045f,8,16);
            var shadow=Skin.RoundBox(parent,Vector2.one*.5f,new Vector2(8,-drop),size,new Color32(145,110,196,62),rad);
            shadow.name="Sticker card shadow";
        }
        public static RoundedGraphic Plate(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color,float radius=-1)
        {
            float rad=radius>0?radius:Mathf.Min(size.x,size.y)*.28f;
            var rim=Skin.RoundBox(parent,anchor,pos+new Vector2(0,-5),size,new Color32(145,110,196,48),rad);
            rim.name="Sticker plate shadow";
            var face=Skin.RoundBox(parent,anchor,pos,size,Color.white,rad);
            face.name="Sticker plate rim";
            var fill=Skin.RoundBox(parent,anchor,pos,size-Vector2.one*10,color,Mathf.Max(12,rad-6));
            fill.name="Sticker plate";
            return fill;
        }
        public static RectTransform Paper(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,bool accent=false)
        {
            var r=Skin.Rect("Menu paper",parent,anchor,pos,size);
            float radius=Mathf.Min(48,Mathf.Min(size.x,size.y)*.18f);
            StickerDepth(r,size,radius);
            var rim=Skin.RoundBox(r,Vector2.one*.5f,Vector2.zero,size,Color.white,radius);
            rim.name="Sticker card rim";
            float pad=Mathf.Clamp(Mathf.Min(size.x,size.y)*.03f,6,12);
            var sheet=Art("ink_paper",r,Vector2.one*.5f,Vector2.zero,size-Vector2.one*(pad*2));
            if(sheet){sheet.preserveAspect=false;sheet.type=Image.Type.Sliced;sheet.name="Sticker card face";}
            if(accent){var tape=Skin.RoundBox(r,new Vector2(0,1),new Vector2(72,-6),new Vector2(132,28),new Color32(255,221,105,235),8);tape.rectTransform.localRotation=Quaternion.Euler(0,0,13);tape.name="Sticker tape";}
            return r;
        }
        public static void Title(string title,string subtitle,Transform parent)
        {
            var t=Label(title,parent,new Vector2(.5f,1),new Vector2(0,-528),new Vector2(920,120),TitleSize,Ink,TextAnchor.MiddleCenter,true);t.name="Menu page title";
            if(!string.IsNullOrEmpty(subtitle))Label(subtitle,parent,new Vector2(.5f,1),new Vector2(0,-636),new Vector2(960,58),Small,Muted,TextAnchor.MiddleCenter);
        }
        public static Sprite AtlasSprite(string key){LoadAtlas();Sprite value;return art.TryGetValue(key,out value)?value:null;}
        public static Sprite TipSprite(string key){if(!art.ContainsKey(key))LoadSheet("sticker_tips");Sprite value;return art.TryGetValue(key,out value)?value:null;}
        public static Sprite BrandSprite(string key){if(!art.ContainsKey(key))LoadSheet("sticker_brand");Sprite value;return art.TryGetValue(key,out value)?value:null;}
        public const int AvatarCount=8;
        // Older profiles used IDs 1..16. Map them without rewriting player progress.
        public static int AvatarId(int id){return id<=0?1:(id-1)%AvatarCount+1;}
        public static Sprite AvatarSprite(int id){string key="avatar_"+AvatarId(id);if(!art.ContainsKey(key))LoadSheet("sticker_avatars");Sprite value;return art.TryGetValue(key,out value)?value:null;}
        public static Image Art(string key,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            LoadAtlas();
            if(!art.ContainsKey(key)){var tex=Resources.Load<Texture2D>("MenuArt/"+key);if(!tex)return null;art[key]=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100);}
            var r=Skin.Rect(key,parent,anchor,pos,size);var image=r.gameObject.AddComponent<Image>();image.sprite=art[key];image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        public static Button SecondaryButton(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,int fontSize=48)
        {
            return GameButton(text,parent,anchor,pos,size,action,fontSize,false);
        }
        public static Button GameButton(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,int fontSize=60,bool primary=true)
        {
            var r=Skin.Rect(text+" menu button",parent,anchor,pos,size);
            var face=Skin.Fill("Button artwork",r);face.gameObject.AddComponent<CanvasGroup>();
            float lift=Mathf.Clamp(size.y*.07f,10,20);
            if(primary){var depth=Skin.RoundBox(face,Vector2.one*.5f,new Vector2(0,-lift*.55f),new Vector2(size.x-6,size.y-2),new Color32(214,72,118,230),size.y*.42f);depth.name="Primary button depth";}
            else StickerDepth(face,size,size.y*.42f);
            var sheet=Art(primary?"ink_brush":"sticker_secondary",face,Vector2.one*.5f,new Vector2(0,primary?lift*.28f:0),size);if(sheet){sheet.preserveAspect=false;sheet.type=Image.Type.Sliced;sheet.pixelsPerUnitMultiplier=sheet.sprite.rect.height/Mathf.Max(1,size.y);}
            Label(text,face,Vector2.one*.5f,new Vector2(0,primary?lift*.22f:0),size-new Vector2(50,20),fontSize,Ink,TextAnchor.MiddleCenter,true);
            var button=Skin.Hit(r,action,1);MenuMotion.Press(button);return button;
        }
        public static void Tabs(Transform parent,bool friends,Action achievements,Action ranking)
        {
            var r=Paper(parent,new Vector2(.5f,1),new Vector2(0,-748),new Vector2(960,172));r.name="Achievement tabs";
            for(int i=0;i<2;i++)
            {
                bool selected=friends?i==1:i==0;var tab=Skin.Rect(i==0?"Achievements tab":"Friends tab",r,new Vector2(.5f,.5f),new Vector2(i==0?-232:232,0),new Vector2(456,144));
                if(selected){Skin.RoundBox(tab,new Vector2(.5f,.5f),new Vector2(0,-4),tab.sizeDelta,new Color32(214,72,118,70),42);Skin.RoundBox(tab,new Vector2(.5f,.5f),Vector2.zero,tab.sizeDelta-new Vector2(8,10),CoralChip,40);}
                Label(i==0?"我的成就":"好友排行",tab,new Vector2(.5f,.5f),Vector2.zero,new Vector2(420,118),54,selected?Grape:Muted,TextAnchor.MiddleCenter,true);
                Skin.Hit(tab,i==0?achievements:ranking,.8f);
            }
        }
        public static void Progress(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,float value)
        {
            var r=Skin.Rect("Menu progress",parent,anchor,pos,size);
            Skin.RoundBox(r,new Vector2(.5f,.5f),Vector2.zero,size,new Color32(228,211,241,255),size.y/2);
            float width=size.x*Mathf.Clamp01(value);if(width>0){var fill=Skin.RoundBox(r,new Vector2(0,.5f),new Vector2(width/2,0),new Vector2(width,size.y),Coral,size.y/2);fill.name="Menu progress fill";MenuMotion.RevealProgress(fill.rectTransform,width);}
        }
        public static void Adapt(Transform parent,WeChatHeaderLayout header,int firstContentChild,float bottom=2020)
        {
            var children=new List<Transform>();for(int i=firstContentChild;i<parent.childCount;i++)children.Add(parent.GetChild(i));
            var root=Skin.Fill("Menu page layout",parent);foreach(var child in children)child.SetParent(root,false);
            var fit=root.gameObject.AddComponent<MenuPageLayout>();fit.Init(header,bottom);
            foreach(var button in root.GetComponentsInChildren<Button>(true))MenuMotion.Press(button);
            MenuMotion.EnterPage(root);
        }
    }
    // Vertex shading adds a shallow, tactile bevel without a material, texture,
    // extra draw call or animation update. It follows the existing rounded mesh.
    public sealed class MenuSurfaceLight:BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vertices)
        {
            if(!IsActive())return;var bounds=((RectTransform)transform).rect;
            UIVertex vertex=new UIVertex();
            for(int i=0;i<vertices.currentVertCount;i++)
            {
                vertices.PopulateUIVertex(ref vertex,i);float t=Mathf.InverseLerp(bounds.yMin,bounds.yMax,vertex.position.y);
                Color original=vertex.color;Color shaded=Color.Lerp(original*.88f,Color.Lerp(original,Color.white,.16f),t);shaded.a=original.a;
                vertex.color=shaded;vertices.SetUIVertex(vertex,i);
            }
        }
    }
    // Keep only menu content between the dynamic wallet row and the fixed bottom
    // action/navigation boundary. The wallet, board and navigation are unaffected.
    [UnityEngine.Scripting.Preserve]
    public sealed class MenuPageLayout:MonoBehaviour
    {
        WeChatHeaderLayout header;RectTransform rect;float bottom;
        public void Init(WeChatHeaderLayout value,float end){header=value;bottom=end;rect=(RectTransform)transform;}
        void LateUpdate()
        {
            if(!rect)return;float top=Mathf.Max(480,334.5f+(header?header.Offset:0)+48);
            float scale=Mathf.Clamp01((bottom-top)/(bottom-480));
            rect.localScale=Vector3.one*scale;rect.anchoredPosition=new Vector2(0,(1-scale)*1200-(top-480*scale));
        }
    }
}
