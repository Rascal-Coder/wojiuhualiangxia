using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Shipped modules1055/2144; FrameRing fid12356, PROFILE callback18073,
    // BoardPaper12355 and WallLines12351/21902. This is a source-derived port,
    // not a claim of pixel equivalence with react-native-svg's rasterizer.
    public static class SourceWoodFrame
    {
        public const float Frac=.18f, Bite=.30f, Corner=.25f, Tile=2.6f, Wall=.78f;
        public static void Build(RectTransform parent,PuzzleBoardView board)
        {
            if(parent==null||board==null)return;
            foreach(var old in parent.GetComponentsInChildren<SourceWoodGraphic>(true))
                if(old.Board==board&&old.Mode==0)return;
            var root=Skin.Fill("Source wood active outline",parent);
            root.SetSiblingIndex(board.transform.GetSiblingIndex());
            Add(root,board,0);Add(root,board,1);Add(root,board,2);
            // BoardMesh layer0 is opaque. Insert continuous paper over that layer,
            // then redraw its grid, before all facet/piece/outline renderers.
            int baseIndex=-1,wallIndex=-1;
            for(int i=0;i<board.transform.childCount;i++)
            {
                var mesh=board.transform.GetChild(i).GetComponent<BoardMesh>();
                if(!mesh)continue;if(mesh.Layer==0)baseIndex=i;
            }
            var paper=Skin.Fill("Source continuous paper",board.transform);Add(paper,board,3);
            paper.SetSiblingIndex(baseIndex+1);
            // The source ring bites30% into paper. The sibling ring alone would
            // be hidden there by the existing opaque BoardMesh. Repeat only its
            // active-cell intersections above paper, still below all pieces.
            var bite=Skin.Fill("Source wood inner bite",board.transform);
            Add(bite,board,0,true);Add(bite,board,1,true);Add(bite,board,2,true);
            bite.SetSiblingIndex(baseIndex+2);
            var grid=Skin.Fill("Source paper grid",board.transform);Add(grid,board,4);
            grid.SetSiblingIndex(baseIndex+3);
            for(int i=0;i<board.transform.childCount;i++)
            {var mesh=board.transform.GetChild(i).GetComponent<BoardMesh>();if(mesh&&mesh.Layer==2)wallIndex=i;}
            var barriers=Skin.Fill("Sticker barrier seals",board.transform);
            var seals=barriers.gameObject.AddComponent<StickerBarrierGraphic>();seals.Board=board;seals.raycastTarget=false;
            barriers.SetSiblingIndex(wallIndex+1);
        }
        static void Add(RectTransform parent,PuzzleBoardView board,int mode,bool inside=false)
        {
            var r=Skin.Fill("Source wood layer "+mode,parent);
            var g=r.gameObject.AddComponent<SourceWoodGraphic>();g.Board=board;g.Mode=mode;g.InsideCellsOnly=inside;g.raycastTarget=false;
        }
    }

    public sealed class SourceWoodGraphic:MaskableGraphic
    {
        public PuzzleBoardView Board;public int Mode;public bool InsideCellsOnly;
        Texture2D sourceTexture;Rect lastBounds;Vector2 lastOrigin;float lastCell,lastDensity;object lastLevel;Matrix4x4 lastMatrix;
        static readonly float[] A={0,.055f,.15f,.215f,.66f,.74f,.855f,.95f};
        static readonly float[] B={.055f,.15f,.215f,.66f,.74f,.855f,.95f,1};
        static readonly string[] Profile={"#58695B","#C7D0BC","#B0BDA7",null,"#B2BDA7","#7D907D","#6F806E","#35473C"};
        static readonly float[] BeamInsets={0,.07f,.22f,.34f};
        static readonly string[] BeamColors={"#6645AA","#FFFFFF","#E6D7F5","#FBF6FF"};
        public override Texture mainTexture
        {
            get
            {
                bool textured=Mode==6;
                if(textured&&!sourceTexture)sourceTexture=SourceArt.Texture(Mode==3?"game_assets_paper":"game_assets_wood");
                return textured&&sourceTexture?sourceTexture:Texture2D.whiteTexture;
            }
        }
        void LateUpdate()
        {
            if(!Board||Board.State==null)return;
            var bounds=Board.VisibleBoundsLocal;var origin=Board.BoardOriginLocal;
            var matrix=Board.transform.localToWorldMatrix;
            if(bounds!=lastBounds||origin!=lastOrigin||Board.CellSize!=lastCell||Board.PixelDensity!=lastDensity||lastLevel!=Board.State.Level||matrix!=lastMatrix)
            {lastBounds=bounds;lastOrigin=origin;lastCell=Board.CellSize;lastDensity=Board.PixelDensity;lastLevel=Board.State.Level;lastMatrix=matrix;SetVerticesDirty();}
        }
        static Color Co(string s){Color c;ColorUtility.TryParseHtmlString(s,out c);return c;}
        Vector2 Map(Vector2 v){return rectTransform.InverseTransformPoint(Board.transform.TransformPoint(v));}
        void Poly(VertexHelper vh,IList<Vector2> p,Color c,Func<Vector2,Vector2> uv=null)
        {
            if(InsideCellsOnly)
            {
                int n=Board.State.Level.n;float cs=Board.CellSize;var origin=Board.BoardOriginLocal;
                for(int i=0;i<n*n;i++)if(Board.State.IsActive(i))
                {Rect cell=new Rect(origin.x+i%n*cs,origin.y-(i/n+1)*cs,cs,cs);Emit(vh,SourceBoardGeometry.ClipRect(p,cell),c,uv);}
                return;
            }
            Emit(vh,p,c,uv);
        }
        void Emit(VertexHelper vh,IList<Vector2> p,Color c,Func<Vector2,Vector2> uv)
        {
            if(p.Count<3)return;
            // Cell/tile clipping also returns polygons that only touch an edge.
            // They have no visible area, yet can exhaust uGUI's vertex limit on
            // intricate outlines if emitted once for every neighbouring cell.
            double twiceArea=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];twiceArea+=(double)a.x*b.y-(double)a.y*b.x;}
            if(System.Math.Abs(twiceArea)<.00001)return;
            int at=vh.currentVertCount;
            for(int i=0;i<p.Count;i++)vh.AddVert(Map(p[i]),c,uv==null?Vector2.zero:uv(p[i]));
            for(int i=1;i+1<p.Count;i++)vh.AddTriangle(at,at+i,at+i+1);
        }
        static Vector2[] Points(Rect r){return new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin)};}
        // Preserve corresponding corner samples even when an inward radius collapses.
        // This lets a strip triangulate the source even-odd outer/inner SVG ring.
        List<Vector2> Offset(IList<Vector2> grid,float inset,float radius,float cs,Vector2 origin,float dp)
        {
            var result=new List<Vector2>();
            for(int i=0;i<grid.Count;i++)
            {
                Vector2 a=grid[(i+grid.Count-1)%grid.Count],p=grid[i],b=grid[(i+1)%grid.Count];
                Vector2 incoming=new Vector2(p.x-a.x,a.y-p.y).normalized,outgoing=new Vector2(b.x-p.x,p.y-b.y).normalized;
                Vector2 na=new Vector2(incoming.y,-incoming.x),nb=new Vector2(outgoing.y,-outgoing.x);
                Vector2 v=origin+new Vector2(p.x*cs,-p.y*cs)+(na+nb)*inset;
                bool convex=incoming.x*outgoing.y-incoming.y*outgoing.x<0;
                if(!convex){result.Add(v);continue;}
                float r=Mathf.Max(0,radius-inset);if(r<.5f*dp)r=0;
                Vector2 start=v-incoming*r,center=start+outgoing*r,radial=start-center;
                for(int k=0;k<=8;k++){float angle=-Mathf.PI*.5f*k/8;result.Add(center+new Vector2(radial.x*Mathf.Cos(angle)-radial.y*Mathf.Sin(angle),radial.x*Mathf.Sin(angle)+radial.y*Mathf.Cos(angle)));}
            }
            return result;
        }
        void Band(VertexHelper vh,IList<Vector2> grid,float outer,float inner,float radius,float cs,Vector2 origin,float dp,Color color,bool grain,float tw,float th,Vector2 anchor)
        {
            var a=Offset(grid,outer,radius,cs,origin,dp);var b=Offset(grid,inner,radius,cs,origin,dp);
            if(!grain&&!InsideCellsOnly){var ma=a.ConvertAll(Map);var mb=b.ConvertAll(Map);StickerMesh.Band(vh,ma,mb,color,StickerMesh.Pixel(rectTransform)*.9f);return;}
            for(int i=0;i<a.Count;i++)
            {int j=(i+1)%a.Count;var p=new[]{a[i],a[j],b[j],b[i]};if(grain)Tiled(vh,p,tw,th,anchor);else Poly(vh,p,color);}
        }
        void Tiled(VertexHelper vh,IList<Vector2> polygon,float tw,float th,Vector2 anchor)
        {
            // Explicitly clip to tiles: reuse the shipped texture without mutating
            // its shared Clamp import/wrap state or relying on a custom shader.
            float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            foreach(var p in polygon){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            int x0=Mathf.FloorToInt((minX-anchor.x)/tw),x1=Mathf.FloorToInt((maxX-anchor.x)/tw);
            int y0=Mathf.FloorToInt((anchor.y-maxY)/th),y1=Mathf.FloorToInt((anchor.y-minY)/th);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                Rect tile=new Rect(anchor.x+x*tw,anchor.y-(y+1)*th,tw,th);
                var clipped=SourceBoardGeometry.ClipRect(polygon,tile);
                Poly(vh,clipped,Color.white,p=>new Vector2((p.x-tile.xMin)/tw,(p.y-tile.yMin)/th));
            }
        }
        static float Round(float x){return Mathf.Floor(x+.5f);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!Board||Board.State==null||Board.CellSize<=0)return;
            var state=Board.State;int n=state.Level.n;float dp=Mathf.Max(.01f,Board.PixelDensity),cs=Board.CellSize,csDp=cs/dp;
            Vector2 origin=Board.BoardOriginLocal;float wDp=Mathf.Max(5,Round(csDp*SourceWoodFrame.Frac)),w=wDp*dp,radius=w*SourceWoodFrame.Corner;
            float outside=Mathf.Max(2,Mathf.Ceil(wDp*(1-SourceWoodFrame.Bite)))*dp;
            float tw=Mathf.Max(12,Round(wDp*SourceWoodFrame.Tile))*dp,th=Mathf.Max(5,Round(tw/dp*96/256))*dp;
            Vector2 anchor=origin+new Vector2(-outside,outside);
            if(Mode<3)
            {
                var cells=new List<int>();for(int i=0;i<n*n;i++)if(state.IsActive(i))cells.Add(i);
                foreach(var grid in SourceBoardGeometry.Trace(n,cells))
                {
                    if(Mode==0)
                    {
                        if(!InsideCellsOnly){Band(vh,grid,-.73f*w,.31f*w,radius,cs,origin+new Vector2(0,-7*dp),dp,new Color32(128,92,171,30),false,tw,th,anchor);Band(vh,grid,-.70f*w,.31f*w,radius,cs,origin+new Vector2(0,-3*dp),dp,new Color32(128,92,171,65),false,tw,th,anchor);}
                        Band(vh,grid,-.70f*w,.30f*w,radius,cs,origin,dp,Co("#B69AD9"),false,tw,th,anchor);
                    }
                    if(Mode==2)
                    {
                        Band(vh,grid,-.64f*w,-.30f*w,radius,cs,origin,dp,Co("#FFFFFF"),false,tw,th,anchor);
                        Band(vh,grid,-.30f*w,.10f*w,radius,cs,origin,dp,Co("#E8DAFB"),false,tw,th,anchor);
                        Band(vh,grid,.10f*w,.28f*w,radius,cs,origin,dp,Co("#FFFFFF"),false,tw,th,anchor);
                    }
                }
            }
            else if(Mode==3)
            {
                float size=n*cs;
                for(int i=0;i<n*n;i++)if(state.IsActive(i))
                {
                    Rect cell=new Rect(origin.x+i%n*cs,origin.y-(i/n+1)*cs,cs,cs);
                    Poly(vh,Points(cell),Co("#F6F0FC"),p=>new Vector2((p.x-origin.x)/size,1-(origin.y-p.y)/size));
                }
            }
            else if(Mode==4)DrawGrid(vh,cs,dp);
            else DrawBeams(vh,origin,cs,dp,wDp);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {Vector2 d=(b-a).normalized,m=new Vector2(-d.y,d.x)*width/2;Poly(vh,new[]{a-m,a+m,b+m,b-m},color);}
        void DrawGrid(VertexHelper vh,float cs,float dp){SourceGridGeometry.Draw(vh,rectTransform,Board);}
        List<Vector2> RoundedRect(Rect r)
        {
            var p=new List<Vector2>();float radius=Mathf.Min(r.width,r.height)/2;
            Vector2[] centers={new Vector2(r.xMax-radius,r.yMax-radius),new Vector2(r.xMax-radius,r.yMin+radius),new Vector2(r.xMin+radius,r.yMin+radius),new Vector2(r.xMin+radius,r.yMax-radius)};
            for(int i=0;i<4;i++)for(int k=0;k<=8;k++){float a=(90-i*90-k*90f/8)*Mathf.Deg2Rad;p.Add(centers[i]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}return p;
        }
        void DrawBeams(VertexHelper vh,Vector2 origin,float cs,float dp,float wDp)
        {
            var state=Board.State;if(state.Level.walls==null)return;int n=state.Level.n;
            float bwDp=Mathf.Max(3,Round(wDp*SourceWoodFrame.Wall)),bw=bwDp*dp;
            float tw=Mathf.Max(12,Round(bwDp*SourceWoodFrame.Tile))*dp,th=Mathf.Max(5,Round(tw/dp*96/256))*dp;
            for(int i=0;i<n*n;i++)for(int direction=0;direction<2;direction++)
            {
                int kind=direction==0?state.Level.walls.e[i]:state.Level.walls.s[i];if(kind<=0)continue;
                float x=origin.x+i%n*cs,y=origin.y-i/n*cs;
                Rect r=direction==0?new Rect(x+cs-bw/2,y-cs,bw,cs):new Rect(x,y-cs-bw/2,cs,bw);
                for(int k=0;k<BeamInsets.Length;k++)
                {
                    bool grain=BeamColors[k]==null;if(grain!=(Mode==6))continue;
                    float inset=BeamInsets[k]*bw;Rect inner=new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset);
                    if(inner.width<=.5f*dp||inner.height<=.5f*dp)continue;
                    var path=RoundedRect(inner);if(grain)Tiled(vh,path,tw,th,origin);else Poly(vh,path,Co(BeamColors[k]));
                }
            }
        }
    }
}
