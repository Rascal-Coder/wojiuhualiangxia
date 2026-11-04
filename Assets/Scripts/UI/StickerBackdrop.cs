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
            Color32[] spots={new Color32(255,122,86,58),new Color32(255,196,92,52),new Color32(255,96,122,46),new Color32(120,214,176,40)};
            int i=0;
            for(float y=r.yMin+90;y<r.yMax;y+=280)for(float x=r.xMin+110;x<r.xMax;x+=250)
            {
                var c=spots[i++%spots.Length];float radius=48+((i*23)%40);
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
