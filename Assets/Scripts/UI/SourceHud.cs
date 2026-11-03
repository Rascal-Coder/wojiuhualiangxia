using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Source: shipped Hermes module2165, ShapeGlyph12444 / CountSlot12446 /
    // TargetCard12447. The supplied parent is the strip's INNER top-left;
    // the caller supplies the source row's 12dp horizontal padding and 2dp top margin.
    public static class SourceHud
    {
        public static void BuildTargets(RectTransform parent,BoardState board,float density=2.625f)
        {
            if(!parent)return;
            var strip=parent.GetComponent<SourceTargetsStrip>();
            if(!strip)strip=parent.gameObject.AddComponent<SourceTargetsStrip>();
            strip.Init(board,Mathf.Max(.01f,density));
        }
    }

    public sealed class SourceTargetsStrip:MonoBehaviour
    {
        sealed class Card
        {
            public string Id; public int Need,Remaining;
            public RectTransform Root,CountRoot,TickRoot;
            public CanvasGroup Group; public Text Label;
            public float Begin,AlphaFrom,AlphaTo;
            public bool Animate;
        }
        readonly List<Card> cards=new List<Card>();
        static readonly Color Ink=new Color32(102,69,170,255),Paper=new Color32(255,249,238,255);
        static readonly Color Orange=new Color32(102,69,170,255),Green=new Color32(102,69,170,255);
        BoardState board; RectTransform holder,generated; float d,lastWidth=-1;
        const float WidthDp=68,HeightDp=86,GapDp=8;

        public void Init(BoardState value,float density)
        {
            if(board!=null)board.Changed-=Refresh;
            board=value;d=density;holder=(RectTransform)transform;
            if(generated){generated.gameObject.SetActive(false);Destroy(generated.gameObject);}
            cards.Clear();lastWidth=-1;
            generated=Skin.Fill("Source targets (module2165)",holder);
            if(board==null||board.Level.targets==null)return;
            float maxW=1,maxH=1;
            foreach(string id in board.Level.targets){ShapeData shape;if(!board.Library.Shapes.TryGetValue(id,out shape)||shape.cells==null||shape.cells.Length==0)continue;
                maxW=Mathf.Max(maxW,shape.cells.Max(x=>x.c)-shape.cells.Min(x=>x.c)+1);
                maxH=Mathf.Max(maxH,shape.cells.Max(x=>x.r)-shape.cells.Min(x=>x.r)+1);}
            // Same cell scale for every target in the row, including tall custom shapes.
            float cell=Mathf.Min(19,61/(maxW+(maxW-1)*.12f),47/(maxH+(maxH-1)*.12f))*d;
            foreach(string id in board.Level.targets)
            {
                var card=new Card{Id=id,Need=Mathf.Max(1,board.Level.solution==null?0:board.Level.solution.Count(x=>x.shape==id))};
                card.Root=Skin.Rect("Target "+id,generated,new Vector2(0,1),Vector2.zero,new Vector2(WidthDp,HeightDp)*d);
                card.Root.pivot=new Vector2(0,1);card.Group=card.Root.gameObject.AddComponent<CanvasGroup>();
                card.Group.interactable=false;card.Group.blocksRaycasts=false;
                MenuUI.Paper(card.Root,Vector2.one*.5f,Vector2.zero,card.Root.sizeDelta);
                ShapeData shape;
                if(board.Library.Shapes.TryGetValue(id,out shape)&&shape.cells!=null&&shape.cells.Length>0)
                {
                    float gap=cell*.12f,step=cell+gap;
                    int minC=shape.cells.Min(x=>x.c),minR=shape.cells.Min(x=>x.r);
                    int columns=shape.cells.Max(x=>x.c)-minC+1,rows=shape.cells.Max(x=>x.r)-minR+1;
                    float gw=columns*cell+(columns-1)*gap,gh=rows*cell+(rows-1)*gap;
                    var glyph=Skin.Rect("ShapeGlyph "+id,card.Root,new Vector2(.5f,1),new Vector2(0,-32*d),new Vector2(gw,gh));
                    foreach(var c in shape.cells){var tile=Skin.Rect("Preview sticker cell",glyph,new Vector2(0,1),new Vector2((c.c-minC)*step+cell/2,-(c.r-minR)*step-cell/2),Vector2.one*cell);var face=tile.gameObject.AddComponent<StickerPreviewTile>();face.color=new Color32(176,137,238,255);face.Variant=cards.Count%3;face.raycastTarget=false;}
                }
                Skin.RoundBox(card.Root,new Vector2(.5f,1),new Vector2(0,-72*d),new Vector2(51,20)*d,new Color32(239,226,255,255),10*d);
                card.CountRoot=Skin.Rect("CountSlot",card.Root,new Vector2(.5f,1),new Vector2(0,-71*d),new Vector2(64,22)*d);
                var tr=Skin.Rect("Remaining count",card.CountRoot,new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,22)*d);
                var text=tr.gameObject.AddComponent<Text>();text.font=MenuUI.BodyFont;text.fontSize=42;
                text.color=Orange;text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Overflow;
                text.verticalOverflow=VerticalWrapMode.Overflow;text.supportRichText=false;text.raycastTarget=false;card.Label=text;
                card.TickRoot=Skin.Rect("Completed CountSlot",card.Root,new Vector2(.5f,1),new Vector2(0,-71*d),Vector2.one*(19*d));
                Skin.RoundBox(card.TickRoot,new Vector2(.5f,.5f),Vector2.zero,card.TickRoot.sizeDelta,Green,9.5f*d);
                var check=Skin.Fill("White check M5.4 10.4 L8.6 13.5 L14.8 6.8",card.TickRoot).gameObject.AddComponent<SourceTargetCheck>();check.color=Color.white;check.raycastTarget=false;
                card.Remaining=Remaining(card);card.AlphaFrom=card.AlphaTo=card.Remaining==0?.45f:1;
                card.Group.alpha=card.AlphaTo;SetCounter(card);cards.Add(card);
            }
            board.Changed+=Refresh;Layout();
        }
        int Remaining(Card c){return Mathf.Max(0,c.Need-(board.Solved?Mathf.Max(c.Need,board.Pieces.Count(p=>p.shape==c.Id)):board.Pieces.Count(p=>p.shape==c.Id)));}
        void SetCounter(Card c){c.Label.text="×"+c.Remaining;c.CountRoot.gameObject.SetActive(c.Remaining!=0);c.TickRoot.gameObject.SetActive(c.Remaining==0);}
        void Refresh(){foreach(var c in cards){int value=Remaining(c);if(value==c.Remaining)continue;if(value==0&&c.Remaining>0)Feel.Soft();c.Remaining=value;c.Begin=SourceClock.Now;c.AlphaFrom=c.Group.alpha;c.AlphaTo=value==0?.45f:1;c.Animate=true;SetCounter(c);c.CountRoot.localScale=Vector3.one;c.TickRoot.localScale=Vector3.one*(value==0?0:1);}}
        void Layout()
        {
            if(!holder||!generated)return;float width=holder.rect.width;if(width<=0)width=holder.sizeDelta.x;if(width<=0)width=1016;
            if(Mathf.Abs(width-lastWidth)<.1f)return;lastWidth=width;float x=0,y=0;
            foreach(var c in cards){if(x>0&&x+WidthDp*d>width+.1f){x=0;y+=(HeightDp+GapDp)*d;}c.Root.anchoredPosition=new Vector2(x,-y);x+=(WidthDp+GapDp)*d;}
        }
        void Update()
        {
            Layout();foreach(var c in cards){if(!c.Animate)continue;float t=Mathf.Max(0,SourceClock.Now-c.Begin);
                c.Group.alpha=Mathf.Lerp(c.AlphaFrom,c.AlphaTo,SourceMotion.OutQuad(t/.240f));
                if(c.Remaining==0)c.TickRoot.localScale=Vector3.one*(t>2?1:SourceMotion.Spring(t,0,1,120,5));
                else c.CountRoot.localScale=Vector3.one*(t<.110f?Mathf.Lerp(1,1.4f,SourceMotion.OutQuad(t/.110f)):t>2?1:SourceMotion.Spring(t-.110f,1.4f,1,140,6));
                if(t>2){c.Animate=false;c.Group.alpha=c.AlphaTo;c.CountRoot.localScale=c.TickRoot.localScale=Vector3.one;}}
        }
        void OnDestroy(){if(board!=null)board.Changed-=Refresh;}
    }

    // SVG viewBox0 0 20 20, source rounded white check path. Stroke2.8,
    // round caps/joins; geometry stays sharp at every canvas scale.
    public sealed class SourceTargetCheck:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float s=Mathf.Min(r.width,r.height)/20;
            Vector2 a=r.center+new Vector2(5.4f-10,10-10.4f)*s,b=r.center+new Vector2(8.6f-10,10-13.5f)*s,c=r.center+new Vector2(14.8f-10,10-6.8f)*s;
            float radius=1.4f*s;Line(vh,a,b,radius);Line(vh,b,c,radius);Dot(vh,a,radius);Dot(vh,b,radius);Dot(vh,c,radius);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width;int k=vh.currentVertCount;vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
        void Dot(VertexHelper vh,Vector2 p,float radius){int k=vh.currentVertCount;vh.AddVert(p,color,Vector2.zero);for(int i=0;i<=16;i++){float a=i*Mathf.PI*2/16;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);if(i>0)vh.AddTriangle(k,k+i,k+i+1);}}
    }

    // A weighted 7x7 Gaussian convolution of the actual source mesh, suitable
    // for text AND rounded panels. It is a soft shadow, rather than one offset copy.
    // Example: SourceBlurShadow.Attach(text,new Color(0,0,0,.4f),new Vector2(0,-5.25f),7.875f).
    // Platform rasterizer blur kernels differ; this portable approximation is explicit.
    public sealed class SourceBlurShadow:BaseMeshEffect
    {
        public Color ShadowColor=new Color(0,0,0,.4f);
        public Vector2 Offset=new Vector2(0,-5.25f);
        public float BlurRadius=7.875f;
        public static SourceBlurShadow Attach(Graphic target,Color color,Vector2 offset,float radius)
        {var effect=target.GetComponent<SourceBlurShadow>();if(!effect)effect=target.gameObject.AddComponent<SourceBlurShadow>();effect.ShadowColor=color;effect.Offset=offset;effect.BlurRadius=radius;target.SetVerticesDirty();return effect;}
        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive()||vh.currentVertCount==0)return;
            var original=new List<UIVertex>();vh.GetUIVertexStream(original);if(original.Count*2>=64000)return;var all=new List<UIVertex>(original.Count*50);
            // Respect uGUI's 16-bit mesh limit on long paragraphs, retaining a soft kernel.
            int n=original.Count*50<64000?3:original.Count*26<64000?2:original.Count*10<64000?1:0;
            float sum=0;for(int y=-n;y<=n;y++)for(int x=-n;x<=n;x++)sum+=Weight(x,y,n);
            for(int y=-n;y<=n;y++)for(int x=-n;x<=n;x++)
            {
                float weight=Weight(x,y,n)/sum;Vector2 offset=Offset+(n==0?Vector2.zero:new Vector2(x,y)*(Mathf.Max(0,BlurRadius)/n));
                foreach(var source in original){var v=source;v.position+=new Vector3(offset.x,offset.y,0);v.color=new Color(ShadowColor.r,ShadowColor.g,ShadowColor.b,ShadowColor.a*weight*(source.color.a/255f));all.Add(v);}
            }
            all.AddRange(original);vh.Clear();vh.AddUIVertexTriangleStream(all);
        }
        static float Weight(int x,int y,int n){if(n==0)return 1;float sigma=n*.5f;return Mathf.Exp(-(x*x+y*y)/(2*sigma*sigma));}
    }
}
