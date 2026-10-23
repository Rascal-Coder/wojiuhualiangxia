using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Inkomino
{
    public static class Skin
    {
        // React Native layout dp -> the measured 1080 reference canvas pixels.
        // Callers supply final pixel sizes; only dp-based source constants use DP.
        public const float DP=2.625f;
        public const float BannerHeightDp=151f,BannerFold=.436f;
        static Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Color Ink=InkTheme.Ink,Paper=InkTheme.Paper;
        static Font latinFont;
        public static Font Font{get{if(!latinFont)latinFont=Resources.Load<Font>("Fonts/LiangxiaUISans-Medium");if(!latinFont)latinFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");return latinFont;}}
        static Font chineseFont;
        public static Font ChineseFont{get{if(!chineseFont)chineseFont=Resources.Load<Font>("Fonts/LiangxiaSticker-Display");return chineseFont?chineseFont:Font;}}
        public static bool HasChinese(string value){if(string.IsNullOrEmpty(value))return false;foreach(char c in value)if(c>=0x2e80&&c<=0xffef)return true;return false;}
        public static Font FontFor(string value){return HasChinese(value)?ChineseFont:Font;}
        public static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return r;}
        public static RectTransform Fill(string name,Transform parent){var r=Rect(name,parent,Vector2.zero,Vector2.zero,Vector2.zero);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        public static Sprite Sprite(string key){var themed=InkTheme.Sprite(key);if(themed)return themed;if(!sprites.ContainsKey(key)){var tex=SourceArt.Texture(key);if(!tex)return null;sprites[key]=UnityEngine.Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100);}return sprites[key];}
        public static Image Art(string key,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,bool preserve=true){var r=Rect(key,parent,anchor,pos,size);var i=r.gameObject.AddComponent<Image>();i.sprite=Sprite(key);i.preserveAspect=preserve;i.raycastTarget=false;return i;}
        public static Image ColorBox(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color){var r=Rect("Panel",parent,anchor,pos,size);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
        public static RoundedGraphic RoundBox(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color,float radius){var r=Rect("Rounded panel",parent,anchor,pos,size);var g=r.gameObject.AddComponent<RoundedGraphic>();g.color=color;g.Radius=radius;g.raycastTarget=false;return g;}
        public static Text Text(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color? color=null,float tracking=0){var r=Rect("Text "+text,parent,anchor,pos,size);Text t=tracking==0?(Text)r.gameObject.AddComponent<LocalizedText>():r.gameObject.AddComponent<SourceTrackedText>();if(t is SourceTrackedText){((SourceTrackedText)t).LetterSpacing=tracking;((SourceTrackedText)t).CenterInk=false;}t.font=Font;t.text=text;t.fontSize=fontSize;t.color=color??Ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;if(HasChinese(text)){t.resizeTextForBestFit=true;t.resizeTextMaxSize=fontSize;t.resizeTextMinSize=Mathf.Max(10,Mathf.FloorToInt(fontSize*.7f));}return t;}
        public static void Shadow(Text t,float distance=4){SourceBlurShadow.Attach(t,new Color(0,0,0,.4f),new Vector2(0,-distance),3*DP);}
        public static void HSlice(Transform parent,string cap,string mid,Vector2 size,float aspect=-1f){
            var cs=Sprite(cap);if(cs==null)return;
            if(aspect<=0)aspect=cs.rect.width/cs.rect.height;
            // HBC fn8255: device-pixel round on H*aspect, dp-floor on W/2.
            float cw=Mathf.Max(DP,Mathf.Min(Mathf.Round(size.y*aspect),Mathf.Floor(size.x/DP/2f)*DP));
            cw=Mathf.Min(cw,Mathf.Max(0,size.x/2));
            Art(cap,parent,new Vector2(0,.5f),new Vector2(cw/2,0),new Vector2(cw,size.y),false);
            float middle=Mathf.Max(0,size.x-2*cw);
            if(middle>0)Art(mid,parent,new Vector2(.5f,.5f),Vector2.zero,new Vector2(middle,size.y),false);
            var right=Art(cap,parent,new Vector2(1,.5f),new Vector2(-cw/2,0),new Vector2(cw,size.y),false);right.rectTransform.localScale=new Vector3(-1,1,1);
        }
        public static Button Hit(Transform parent,Action action,float activeOpacity=1f){
            var img=parent.GetComponent<Image>();if(!img){img=parent.gameObject.AddComponent<Image>();img.color=new Color(1,1,1,0);}
            img.raycastTarget=true;
            if(!parent.GetComponent<CanvasGroup>())parent.gameObject.AddComponent<CanvasGroup>();
            var b=parent.gameObject.AddComponent<SourceOpacityButton>();b.targetGraphic=img;b.transition=Selectable.Transition.None;b.ActiveOpacity=activeOpacity;
            b.onClick.AddListener(()=>{action?.Invoke();});return b;
        }
        struct FrameSpec{
            public string frame,fill;public float aspect,top,bottom,side,fillAspect;
            public float Center{get{return(top+1-bottom)/2;}}
        }
        static string Variant(string variant){return string.IsNullOrEmpty(variant)?"":variant.ToUpperInvariant();}
        static FrameSpec SourceFrame(string variant){
            switch(Variant(variant)){
                case "HARD":return new FrameSpec{frame="purple",fill="purplehard",aspect=112f/372,top=43f/372,bottom=43f/372,side=43f/372,fillAspect=80f/286};
                case "EXTRA_HARD":return new FrameSpec{frame="red",fill="redxhard",aspect=112f/372,top=43f/372,bottom=43f/372,side=43f/372,fillAspect=80f/286};
                case "ULTRA_HARD":return new FrameSpec{frame="black",fill="blackuhard",aspect=115f/372,top=43f/372,bottom=43f/372,side=43f/372,fillAspect=80f/286};
                default:return new FrameSpec{frame="",fill=null,aspect=77f/261,top=25f/261,bottom=34f/261,side=30f/261,fillAspect=55f/197};
            }
        }
        static float RoundDp(float px){return Mathf.Floor(px/DP+.5f)*DP;}
        public static float ButtonFaceCenter(string variant=null){return SourceFrame(variant).Center;}
        public static Button Button(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,string color="green",int fontSize=64){return SourceButton(text,parent,anchor,pos,size,action,color,fontSize);}
        public static Button SourceButton(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,string color="green",int fontSize=64,string variant=null)
        {return MenuUI.GameButton(text,parent,anchor,pos,size,action,fontSize,color=="green");}
        static SourceTrackedText SourceLabel(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color color){
            var clip=Rect("Label clip",parent,anchor,pos,size);clip.gameObject.AddComponent<RectMask2D>();
            var r=Fill("Label "+text,clip);var label=r.gameObject.AddComponent<SourceTrackedText>();label.font=Font;label.text=text;label.fontSize=fontSize;label.color=color;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.supportRichText=false;label.CenterInk=true;return label;
        }
        public static Button ActionTile(string iconKey,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,Vector2? iconSize=null,Color? face=null,Color? shadow=null){
            var r=Rect(iconKey+" action",parent,anchor,pos,size);
            RoundBox(r,new Vector2(.5f,.5f),Vector2.zero,size,shadow??new Color32(170,85,0,255),16*DP);
            float bottom=7*DP;var front=Rect("Surface",r,new Vector2(.5f,.5f),new Vector2(0,bottom/2),new Vector2(size.x,Mathf.Max(0,size.y-bottom)));
            RoundBox(front,new Vector2(.5f,.5f),Vector2.zero,front.sizeDelta,face??new Color32(255,136,0,255),14*DP);
            Art(iconKey,front,new Vector2(.5f,.5f),Vector2.zero,iconSize??Vector2.one*(40*DP));
            return Hit(r,action,.8f);
        }
        public static Button IconButton(string key,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action){var r=Rect(key+" button",parent,anchor,pos,size);Art(key,r,new Vector2(.5f,.5f),Vector2.zero,size);var button=Hit(r,action);MenuMotion.Press(button);return button;}
        public static void PopupPanel(Transform parent,Vector2 size,string color="orange")
        {MenuUI.Paper(parent,new Vector2(.5f,.5f),Vector2.zero,size,true);}
        public static void Banner(string title,Transform parent,float width){SourceBanner(title,parent,width);}
        public static RectTransform SourceBanner(string title,Transform parent,float width,string variant=null)
        {
            var r=Rect("Banner",parent,new Vector2(.5f,1),new Vector2(0,-100),new Vector2(width,180));
            MenuUI.Label(title,r,new Vector2(.5f,.5f),Vector2.zero,new Vector2(width-140,150),72,InkTheme.Ink,TextAnchor.MiddleCenter,true);return r;
        }
    }
    // The source's ordinary buttons use activeOpacity=1. No invented scale/hover tint.
    public class SourceOpacityButton:Button
    {
        public float ActiveOpacity=1f;
        protected override void DoStateTransition(SelectionState state,bool instant){var group=GetComponent<CanvasGroup>();if(group)group.alpha=state==SelectionState.Disabled?.5f:state==SelectionState.Pressed?ActiveOpacity:1;}
    }
    // Single-line source labels use explicit tracking and clip bounds, independent of
    // font texture size. Tight ink centring is reserved for the banner's fold line.
    public class LocalizedText:Text
    {
        // Empty labels may later become Chinese (life recovery, notifications).
        public override string text{get{return base.text;}set{base.text=value;var face=Skin.FontFor(value);if(font!=face)font=face;}}
    }
    public class SourceTrackedText:LocalizedText
    {
        public float LetterSpacing,MinimumScale=.5f;public bool CenterInk;
        protected override void OnPopulateMesh(VertexHelper vh){
            base.OnPopulateMesh(vh);int count=vh.currentVertCount;if(count<4)return;
            UIVertex v=new UIVertex();int quads=count/4;float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            for(int q=0;q<quads;q++)for(int k=0;k<4;k++){int i=q*4+k;vh.PopulateUIVertex(ref v,i);v.position+=new Vector3((q-(quads-1)/2f)*LetterSpacing,0);vh.SetUIVertex(v,i);minX=Mathf.Min(minX,v.position.x);maxX=Mathf.Max(maxX,v.position.x);minY=Mathf.Min(minY,v.position.y);maxY=Mathf.Max(maxY,v.position.y);}
            float inkW=maxX-minX;if(inkW<=0)return;float scale=Mathf.Min(1f,Mathf.Max(MinimumScale,rectTransform.rect.width/inkW));
            float centerX=(minX+maxX)/2,centerY=CenterInk?(minY+maxY)/2:0;
            for(int i=0;i<count;i++){vh.PopulateUIVertex(ref v,i);v.position=new Vector3((v.position.x-centerX)*scale,(v.position.y-centerY)*scale,v.position.z);vh.SetUIVertex(v,i);}
        }
    }
    public class ButtonPop:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e){}
        public void OnPointerUp(PointerEventData e){}
        public void OnPointerExit(PointerEventData e){}
    }
    public class RoundedGraphic:MaskableGraphic
    {
        public float Radius=32;
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();StickerMesh.Solid(vh,StickerMesh.Rounded(rectTransform.rect,Radius),color,StickerMesh.Pixel(rectTransform)*.9f);}
    }
    public class SafeArea:MonoBehaviour
    {
        Rect last;int w,h;void OnEnable(){Apply();}void Update(){if(last!=Screen.safeArea||w!=Screen.width||h!=Screen.height)Apply();}
        void Apply(){last=Screen.safeArea;w=Screen.width;h=Screen.height;if(w<1||h<1)return;var r=(RectTransform)transform;r.anchorMin=new Vector2(last.x/w,last.y/h);r.anchorMax=new Vector2(last.xMax/w,last.yMax/h);r.offsetMin=r.offsetMax=Vector2.zero;}
    }
    public class PortraitFit:MonoBehaviour
    {
        void LateUpdate(){var r=(RectTransform)transform;var parent=(RectTransform)r.parent;r.sizeDelta=new Vector2(1080,2400);r.localScale=Vector3.one*Mathf.Min(parent.rect.width/1080,parent.rect.height/2400);}
    }
}
