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
            vh.Clear();var r=rectTransform.rect;var c=new Color32(153,114,192,30);
            for(float y=r.yMin+38;y<r.yMax;y+=82)for(float x=r.xMin+40;x<r.xMax;x+=82)
            {
                int n=vh.currentVertCount;float d=2;
                vh.AddVert(new Vector3(x-d,y-d),c,Vector2.zero);vh.AddVert(new Vector3(x-d,y+d),c,Vector2.zero);
                vh.AddVert(new Vector3(x+d,y+d),c,Vector2.zero);vh.AddVert(new Vector3(x+d,y-d),c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
