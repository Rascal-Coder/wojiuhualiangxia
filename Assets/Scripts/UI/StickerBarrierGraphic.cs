using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Shared atlas, one static mesh. End caps retain their proportions at every cell size.
    public sealed class StickerBarrierGraphic:MaskableGraphic
    {
        public PuzzleBoardView Board;
        public int RenderedBarrierCount { get;private set; }
        float lastCell,lastDensity;Vector2 lastOrigin;object lastLevel;
        static readonly string[] Names={"plain","same","different"};
        public static float BadgeSize(float cell,float dp){return Mathf.Clamp(cell*.42f,20*dp,28*dp);}
        public override Texture mainTexture { get {var s=MenuUI.AtlasSprite("barrier_strip_plain");return s?s.texture:Texture2D.whiteTexture;} }
        void LateUpdate()
        {
            if(!Board||Board.State==null)return;
            if(lastCell!=Board.CellSize||lastDensity!=Board.PixelDensity||lastOrigin!=Board.BoardOriginLocal||lastLevel!=Board.State.Level){
                lastCell=Board.CellSize;lastDensity=Board.PixelDensity;lastOrigin=Board.BoardOriginLocal;lastLevel=Board.State.Level;SetVerticesDirty();
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();RenderedBarrierCount=0;
            if(!Board||Board.State==null||Board.State.Level.walls==null)return;
            var level=Board.State.Level;float cs=Board.CellSize,dp=Board.PixelDensity;
            float thickness=Mathf.Clamp(cs*.18f,5.5f*dp,10*dp);
            for(int pass=0;pass<2;pass++)for(int i=0;i<level.n*level.n;i++)for(int direction=0;direction<2;direction++){
                int kind=direction==0?level.walls.e[i]:level.walls.s[i];if(kind<=0)continue;
                var cell=Board.CellRect(i);var center=direction==0?new Vector2(cell.xMax,cell.center.y):new Vector2(cell.center.x,cell.yMin);
                string name=Names[Mathf.Clamp(kind-1,0,2)];
                if(pass==0){
                    var sprite=MenuUI.AtlasSprite("barrier_strip_"+name);
                    if(sprite){Strip(vh,sprite,center,direction==0?Vector2.up:Vector2.right,cs+thickness*.12f,thickness);RenderedBarrierCount++;}
                }else{
                    var sprite=MenuUI.AtlasSprite("barrier_badge_"+name);
                    if(sprite){
                        float size=BadgeSize(cs,dp);SpriteQuad(vh,sprite,center,Vector2.right,-size*.5f,size*.5f,size,0,1);
                        // Give the rule a quiet, broad center while retaining the illustrated raised rim.
                        var circle=new System.Collections.Generic.List<Vector2>();
                        for(int k=0;k<48;k++){float a=k*Mathf.PI*2/48;circle.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size*.345f);}
                        StickerMesh.Solid(vh,circle,new Color32(255,249,238,255),StickerMesh.Pixel(rectTransform)*.7f,p=>new Vector2(510.5f/512,1-510.5f/512));
                    }
                }
            }
        }
        void Strip(VertexHelper vh,Sprite sprite,Vector2 center,Vector2 axis,float length,float height)
        {
            float u=Mathf.Clamp(sprite.border.x/sprite.rect.width,.1f,.3f);
            float cap=Mathf.Min(length*.45f,height*sprite.border.x/sprite.rect.height);
            float[] x={-length*.5f,-length*.5f+cap,length*.5f-cap,length*.5f};float[] uv={0,u,1-u,1};
            for(int i=0;i<3;i++)SpriteQuad(vh,sprite,center,axis,x[i],x[i+1],height,uv[i],uv[i+1]);
        }
        void SpriteQuad(VertexHelper vh,Sprite sprite,Vector2 center,Vector2 axis,float left,float right,float height,float u0,float u1)
        {
            var side=new Vector2(-axis.y,axis.x);var r=sprite.rect;var t=sprite.texture;
            float a=(r.x+r.width*u0)/t.width,b=(r.x+r.width*u1)/t.width,c=r.y/t.height,d=r.yMax/t.height;
            int start=vh.currentVertCount;
            vh.AddVert(center+axis*left-side*height*.5f,Color.white,new Vector2(a,c));
            vh.AddVert(center+axis*left+side*height*.5f,Color.white,new Vector2(a,d));
            vh.AddVert(center+axis*right+side*height*.5f,Color.white,new Vector2(b,d));
            vh.AddVert(center+axis*right-side*height*.5f,Color.white,new Vector2(b,c));
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
