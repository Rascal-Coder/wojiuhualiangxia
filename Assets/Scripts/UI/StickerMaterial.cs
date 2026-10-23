using UnityEngine;
using UnityEngine.UI;
namespace Inkomino
{
    public static class StickerMaterial
    {
        static Texture2D texture;
        public static Texture2D Texture {get{
            if(!texture){var data=Resources.Load<TextAsset>("MenuArt/sticker_material");texture=new Texture2D(2,2,TextureFormat.RGBA32,true);texture.name="Puffy sticker material";texture.LoadImage(data.bytes,true);texture.filterMode=FilterMode.Trilinear;texture.wrapMode=TextureWrapMode.Clamp;Resources.UnloadAsset(data);}return texture;
        }}
        public static Vector2 WhiteUV=>new Vector2(510.5f/512,1-510.5f/512);
        public static Vector2 UV(int variant,Vector2 p,Rect r){int i=Mathf.Abs(variant)%3;return new Vector2((i%2*256+4+Mathf.Clamp01((p.x-r.xMin)/r.width)*248)/512f,1-(i/2*256+4+(1-Mathf.Clamp01((p.y-r.yMin)/r.height))*248)/512f);}
    }
    public sealed class StickerPreviewTile:MaskableGraphic
    {
        public int Variant;
        public override Texture mainTexture=>StickerMaterial.Texture;
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();var r=rectTransform.rect;float aa=StickerMesh.Pixel(rectTransform)*.8f;
            var shadow=r;shadow.y-=r.height*.045f;
            StickerMesh.Solid(vh,StickerMesh.Rounded(shadow,r.width*.16f),new Color32(113,77,155,110),aa,p=>StickerMaterial.WhiteUV);
            StickerMesh.Solid(vh,StickerMesh.Rounded(r,r.width*.16f),Color.white,aa,p=>StickerMaterial.WhiteUV);
            float pad=r.width*.055f;var face=new Rect(r.x+pad,r.y+pad,r.width-pad*2,r.height-pad*2);
            StickerMesh.Solid(vh,StickerMesh.Rounded(face,r.width*.12f),color,aa*.65f,p=>StickerMaterial.UV(Variant,p,face));
        }
    }
}
