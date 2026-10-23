using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Inkomino
{
    public sealed class StickerRuleGlyph:MaskableGraphic
    {
        public int Kind;
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();float d=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height);Draw(vh,rectTransform,rectTransform.rect.center,Kind,d*.29f,d*.0725f);}
        public static void Draw(VertexHelper vh,RectTransform rt,Vector2 center,int kind,float radius,float width)
        {
            if(kind==1){Line(vh,rt,center+Vector2.left*radius*.65f,center+Vector2.right*radius*.65f,width);return;}
            Line(vh,rt,center+new Vector2(-.72f,-.36f)*radius,center+new Vector2(.72f,-.36f)*radius,width);
            Line(vh,rt,center+new Vector2(-.72f,.36f)*radius,center+new Vector2(.72f,.36f)*radius,width);
            if(kind==3)Line(vh,rt,center+new Vector2(-.48f,-.78f)*radius,center+new Vector2(.48f,.78f)*radius,width);
        }
        static void Line(VertexHelper vh,RectTransform rt,Vector2 a,Vector2 b,float width)
        {
            var delta=b-a;var axis=delta.normalized;var side=new Vector2(-axis.y,axis.x);var center=(a+b)*.5f;
            var cap=StickerMesh.Rounded(new Rect(-delta.magnitude*.5f-width*.5f,-width*.5f,delta.magnitude+width,width),width*.5f);
            StickerMesh.Solid(vh,cap.Select(p=>center+axis*p.x+side*p.y).ToList(),StickerDesign.Ink,StickerMesh.Pixel(rt)*.7f);
        }
    }
}
