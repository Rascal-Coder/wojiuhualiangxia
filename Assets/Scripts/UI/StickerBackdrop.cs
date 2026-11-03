using UnityEngine;
using UnityEngine.UI;
namespace Inkomino
{
    // Static low-contrast journal dots: a single UI mesh, no image or Update loop.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StickerBackdrop:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Color32[] spots={new Color32(255,168,190,38),new Color32(117,218,186,32),new Color32(174,133,226,28),new Color32(255,227,107,26),new Color32(141,207,252,30)};
            int i=0;
            for(float y=r.yMin+70;y<r.yMax;y+=210)for(float x=r.xMin+80;x<r.xMax;x+=190)
            {
                var c=spots[i++%spots.Length];float radius=36+((i*17)%28);
                int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);
                var edge=c;edge.a=0;
                for(int k=0;k<=16;k++)
                {
                    float a=k*Mathf.PI*2/16;
                    vh.AddVert(new Vector3(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),edge,Vector2.zero);
                    if(k>0)vh.AddTriangle(n,n+k,n+k+1);
                }
            }
        }
    }
}
