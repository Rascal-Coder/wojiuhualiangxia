using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Inkomino
{
    public class PuzzleBoardView:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public BoardState State;public Action<string> Message;public Func<bool> CanInput;public Func<int[],bool> StrokeAllowed;
        public readonly List<int> Trail=new List<int>();public int[] Reveal;public bool Tutorial;
        public float PixelDensity=2.625f;public bool FitActiveBounds=true;public float TutorialReservedDp;public bool Animating;
        public int[] DrawTrace;public float DrawProgress=1;public int[] HintCells;public float HintStrength,ScanProgress=-1;
        public static readonly string[] Palette={"#FF83AC","#8DCFFC","#B6A0F5","#FFD86B","#75DABA","#FFA8D2","#FFAA7A","#D6EA81","#8EAAFA","#C7A5EA"};
        internal sealed class PieceStyle
        {public int ColorIndex,TextureIndex;public Vector2 Offset;public float Born;public int[] Order;public float[] CellTimes;public bool Locked,PopAfterInk,TutorialLocked;public float Duration{get{return .58f+.025f*Mathf.Max(0,Order.Length-1);}}}
        readonly Dictionary<int,float> entered=new Dictionary<int,float>();
        readonly Dictionary<PieceData,PieceStyle> styles=new Dictionary<PieceData,PieceStyle>();
        readonly List<BoardMesh> meshes=new List<BoardMesh>();readonly System.Random random=new System.Random();
        RectTransform rt;Transform labels;int pointer=int.MinValue,pressCell=-1,cursorCell=-1;PieceData removed;
        bool moved,stylesInitialized,victoryRequested;float cellSize,boardSize,victoryStart=float.PositiveInfinity,flashUntil;
        Vector2 origin,lastSize;Rect visibleBounds;HashSet<int> flashCells;PieceStyle trailStyle;int nextColor;Material glassMaterial;
        public float CellSize{get{EnsureLayout();return cellSize;}}
        public int NextColorIndex{get{return nextColor;}}
        public Vector2 BoardOriginLocal{get{EnsureLayout();return origin;}}
        public Rect VisibleBoundsLocal{get{EnsureLayout();return visibleBounds;}}
        public float VictoryTimeRemaining{get{return victoryRequested?Mathf.Max(0,victoryStart+.480f-SourceClock.Now):0;}}
        internal float VictoryInk{get{return victoryRequested?SourceMotion.OutCubic((SourceClock.Now-victoryStart)/.180f):0;}}
        internal float SweepProgress{get{return victoryRequested?Mathf.Clamp01((SourceClock.Now-victoryStart-.080f)/.400f):-1;}}
        internal bool SweepVisible{get{float t=SourceClock.Now-victoryStart-.080f;return victoryRequested&&t>=0&&t<=.400f;}}
        internal float BoardSize{get{EnsureLayout();return boardSize;}}
        internal bool BadCell(int i){return flashCells!=null&&SourceClock.Now<flashUntil&&flashCells.Contains(i);}
        public void Init(BoardState state)
        {
            State=state;rt=(RectTransform)transform;var hit=gameObject.GetComponent<Image>()??gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);hit.raycastTarget=true;
            // Shared tintable vinyl material; standard UI shader preserves white die-cut edges and alpha coverage.
            Layer(0);for(int i=0;i<3;i++)Layer(1,i);Layer(3);Layer(2);Layer(7);Layer(8);
            // Detailed round SVG strokes can exceed uGUI's16-bit vertex limit
            // when the whole board shares one graphic. Preserve all samples and
            // submit small cell batches instead of reducing symbol detail.
            for(int start=0;start<state.Level.n*state.Level.n;start+=4){var symbols=Layer(5);symbols.SymbolStart=start;symbols.SymbolEnd=start+4;}
            nextColor=random.Next(Palette.Length);State.Changed+=Refresh;Refresh();
        }
        BoardMesh Layer(int layer,int facet=0){var r=Skin.Fill("Source board layer "+layer+"-"+facet,transform);var m=r.gameObject.AddComponent<BoardMesh>();m.View=this;m.Layer=layer;m.FacetIndex=facet;m.raycastTarget=false;if(layer==1&&glassMaterial)m.material=glassMaterial;meshes.Add(m);return m;}
        void OnDestroy(){if(State!=null)State.Changed-=Refresh;if(glassMaterial)Destroy(glassMaterial);}
        void Update()
        {
            if(State==null||rt==null)return;bool changed=lastSize!=rt.rect.size;if(changed){EnsureLayout();RefreshLabels();}
            bool previous=Animating;Animating=Trail.Count>0||DrawTrace!=null&&DrawProgress<1||flashCells!=null&&SourceClock.Now<flashUntil||VictoryTimeRemaining>0;
            foreach(var p in State.Pieces){PieceStyle s;if(styles.TryGetValue(p,out s)&&SourceClock.Now<s.Born+Mathf.Max(.80f,s.Duration+.15f))Animating=true;}
            if(Animating||previous||changed||DrawTrace!=null)Dirty();
        }
        void Dirty(){foreach(var m in meshes)if(m)m.SetVerticesDirty();}
        void EnsureLayout()
        {
            if(rt==null)rt=transform as RectTransform;if(rt==null||State==null)return;Rect r=rt.rect;lastSize=r.size;int n=State.Level.n;
            int r0=n,c0=n,r1=-1,c1=-1;for(int i=0;i<n*n;i++)if(State.IsActive(i)){r0=Mathf.Min(r0,i/n);c0=Mathf.Min(c0,i%n);r1=Mathf.Max(r1,i/n);c1=Mathf.Max(c1,i%n);}
            if(r1<r0){r0=c0=0;r1=c1=n-1;}
            if(!FitActiveBounds){cellSize=Mathf.Min(r.width,r.height)/n;origin=new Vector2(r.center.x-cellSize*n/2,r.center.y+cellSize*n/2);boardSize=cellSize*n;visibleBounds=new Rect(origin.x,origin.y-boardSize,boardSize,boardSize);return;}
            float d=Mathf.Max(.01f,PixelDensity),w=Mathf.Max(0,Mathf.Floor(r.width/d)-4),h=Mathf.Max(0,Mathf.Floor(r.height/d)-4-TutorialReservedDp);
            float bw=c1-c0+1,bh=r1-r0+1,cs=Mathf.Max(0,Mathf.Floor(Mathf.Min(w/(bw+.252f),h/(bh+.252f))));cellSize=cs*d;
            float wood=Mathf.Max(5,Mathf.Floor(cs*.18f+.5f)),outside=Mathf.Max(2,Mathf.Ceil(wood*.7f))*d;
            float width=bw*cellSize+2*outside,height=bh*cellSize+2*outside;Vector2 center=r.center+Vector2.up*(TutorialReservedDp*d*.5f);
            visibleBounds=new Rect(center.x-width/2,center.y-height/2,width,height);origin=new Vector2(visibleBounds.xMin+outside-c0*cellSize,visibleBounds.yMax-outside+r0*cellSize);boardSize=n*cellSize;
        }
        public Vector2 CellCenterLocal(int i){EnsureLayout();int n=State.Level.n;return origin+new Vector2((i%n+.5f)*cellSize,-(i/n+.5f)*cellSize);}
        Camera ScreenCamera{get{var c=GetComponentInParent<Canvas>();return c&&c.renderMode!=RenderMode.ScreenSpaceOverlay?c.worldCamera:null;}}
        public Vector2 CellCenterScreen(int i){return RectTransformUtility.WorldToScreenPoint(ScreenCamera,transform.TransformPoint(CellCenterLocal(i)));}
        public Vector2[] GetOutlineTrace(int[] cells)
        {EnsureLayout();var paths=SourceBoardGeometry.Paths(State.Level.n,cells,cellSize,origin,0,4.5f*PixelDensity);if(paths.Count==0)return new Vector2[0];var p=paths.OrderByDescending(x=>SourceBoardGeometry.Length(x)).First();var result=p.Select(x=>RectTransformUtility.WorldToScreenPoint(ScreenCamera,transform.TransformPoint(x))).ToList();result.Add(result[0]);return result.ToArray();}
        public void SetPenTrace(int[] cells,float progress)
        {
            int[] selected=cells??DrawTrace;DrawTrace=cells;DrawProgress=Mathf.Clamp01(progress);
            if(selected!=null)foreach(var p in State.Pieces)if(new HashSet<int>(selected).SetEquals(p.cells))
            {
                var s=Style(p);if(s==null)continue;
                if(!s.Locked){s.PopAfterInk=SourceClock.Now-s.Born>=0&&SourceClock.Now-s.Born<.100f;s.Locked=true;p.locked=true;if(s.PopAfterInk)s.Born=-1000;}
                if(progress>=1&&s.PopAfterInk){s.PopAfterInk=false;s.Born=SourceClock.Now;}
            }
            Dirty();
        }
        public void PlayVictory()
        {if(victoryRequested)return;SyncStyles();float wait=0;foreach(var p in State.Pieces){var s=styles[p];wait=Mathf.Max(wait,s.Born+s.Duration-SourceClock.Now);}victoryStart=SourceClock.Now+Mathf.Clamp(wait,0,.160f);victoryRequested=true;Animating=true;Dirty();}
        PieceStyle MakeStyle(int[] order,bool animate)
        {float d=Mathf.Max(.01f,PixelDensity),cs=cellSize/d;return new PieceStyle{ColorIndex=nextColor,TextureIndex=random.Next(3),Offset=new Vector2(-Mathf.Floor((float)random.NextDouble()*cs+.5f)*d,-Mathf.Floor((float)random.NextDouble()*cs+.5f)*d),Born=animate?SourceClock.Now:-1000,Order=order.ToArray()};}
        void PickNextColor(PieceStyle last)
        {var used=new HashSet<int>();foreach(int cell in last.Order)foreach(int neighbor in State.Neighbors(cell)){var p=State.At(neighbor);if(p!=null&&styles.ContainsKey(p))used.Add(styles[p].ColorIndex);}var candidates=Enumerable.Range(0,Palette.Length).Where(i=>!used.Contains(i)&&i!=last.ColorIndex).ToList();if(candidates.Count==0)candidates=Enumerable.Range(0,Palette.Length).Where(i=>!used.Contains(i)).ToList();if(candidates.Count==0)candidates=Enumerable.Range(0,Palette.Length).ToList();nextColor=candidates[random.Next(candidates.Count)];}
        void SyncStyles()
        {
            EnsureLayout();foreach(var stale in styles.Keys.Where(p=>p!=removed&&!State.Pieces.Contains(p)).ToArray())styles.Remove(stale);foreach(var p in State.Pieces)
            {if(styles.ContainsKey(p))continue;bool fromTrail=trailStyle!=null&&Trail.Count==p.cells.Length&&new HashSet<int>(Trail).SetEquals(p.cells);
             var s=fromTrail?new PieceStyle{ColorIndex=trailStyle.ColorIndex,TextureIndex=trailStyle.TextureIndex,Offset=trailStyle.Offset,Order=Trail.ToArray(),Born=SourceClock.Now,CellTimes=Trail.Select(c=>entered.ContainsKey(c)?entered[c]:SourceClock.Now-.3f).ToArray()}:MakeStyle(p.cells,stylesInitialized);
             if(DrawTrace!=null&&new HashSet<int>(DrawTrace).SetEquals(p.cells)){s.Locked=true;s.PopAfterInk=true;s.Born=-1000;}
             if(p.appearanceSaved){s.ColorIndex=Mathf.Clamp(p.colorIndex,0,Palette.Length-1);s.TextureIndex=Mathf.Clamp(p.textureIndex,0,2);s.Offset=new Vector2(p.textureOffsetX,p.textureOffsetY);}else {p.appearanceSaved=true;p.colorIndex=s.ColorIndex;p.textureIndex=s.TextureIndex;p.textureOffsetX=s.Offset.x;p.textureOffsetY=s.Offset.y;}s.Locked|=p.locked&&!p.tutLock;s.TutorialLocked|=p.tutLock;p.locked=s.Locked||s.TutorialLocked;styles[p]=s;PickNextColor(s);}
            stylesInitialized=true;
        }
        internal PieceStyle Style(PieceData p){PieceStyle s;return styles.TryGetValue(p,out s)?s:null;}
        internal PieceStyle TrailStyle{get{return trailStyle;}}
        internal Rect CellRect(int i){int n=State.Level.n;return new Rect(origin.x+i%n*cellSize,origin.y-(i/n+1)*cellSize,cellSize,cellSize);}
        float CellAge(PieceStyle s,int cell){int index=Mathf.Max(0,Array.IndexOf(s.Order,cell));return SourceClock.Now-(s.CellTimes!=null&&index<s.CellTimes.Length?s.CellTimes[index]:s.Born+.045f*index);}
        internal void Appearance(PieceData p,int cell,out float scale,out float opacity)
        {scale=1;opacity=1;var s=Style(p);if(s==null||MenuMotion.Reduced)return;float t=CellAge(s,cell);float u=SourceMotion.OutCubic(t/.22f);scale=Mathf.Lerp(.86f,1,u);opacity=SourceMotion.OutQuad(t/.12f);}
        internal void TrailAppearance(int cell,out float scale,out float opacity)
        {float age=entered.ContainsKey(cell)?SourceClock.Now-entered[cell]:1;float u=MenuMotion.Reduced?1:SourceMotion.OutCubic(age/.22f);scale=Mathf.Lerp(.86f,1,u);opacity=MenuMotion.Reduced?1:SourceMotion.OutQuad(age/.12f);}
        internal float CornerFold(PieceData p,int cell)
        {var s=Style(p);if(s==null||MenuMotion.Reduced)return 0;float t=SourceClock.Now-s.Born-.025f*Mathf.Max(0,Array.IndexOf(s.Order,cell));return cellSize*.36f*(1-SourceMotion.InOutQuad((t-.08f)/.40f));}
        public void Refresh(){if(State==null||meshes.Count==0)return;SyncStyles();Dirty();RefreshLabels();}
        void RefreshLabels()
        {
            if(State==null||rt==null)return;if(labels)Destroy(labels.gameObject);labels=Skin.Fill("Source board symbols",transform);int n=State.Level.n;
            // SVG symbols and wall marks are Layer5 geometry, independent of fonts.
            for(int i=0;i<n*n;i++){int kind=State.Level.icons==null?0:State.Level.icons[i],num=State.Level.nums==null?0:State.Level.nums[i];if(kind>0||num<=0)continue;float font=Mathf.Clamp(cellSize/PixelDensity*.3f,10,22)*PixelDensity;Skin.Text(num.ToString(),labels,new Vector2(.5f,.5f),CellCenterLocal(i),Vector2.one*cellSize,Mathf.RoundToInt(font),Skin.Ink);}
        }
        public int CellAt(Vector2 screen,Camera camera){EnsureLayout();if(cellSize<=0)return -1;Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,screen,camera,out local);int n=State.Level.n,c=Mathf.FloorToInt((local.x-origin.x)/cellSize),r=Mathf.FloorToInt((origin.y-local.y)/cellSize);return r>=0&&r<n&&c>=0&&c<n?r*n+c:-1;}
        void EnterCell(int cell){float previous=Trail.Count>0&&entered.ContainsKey(Trail[Trail.Count-1])?entered[Trail[Trail.Count-1]]:SourceClock.Now-.025f;Trail.Add(cell);entered[cell]=Mathf.Min(SourceClock.Now+.12f,Mathf.Max(SourceClock.Now,previous+.025f));}
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue||!(CanInput?.Invoke()??true))return;pointer=e.pointerId;BeginCell(CellAt(e.position,e.pressEventCamera));}
        public void OnDrag(PointerEventData e){if(pointer==e.pointerId)MoveCell(CellAt(e.position,e.pressEventCamera));}
        public void OnPointerUp(PointerEventData e){if(pointer!=e.pointerId)return;pointer=int.MinValue;EndStroke();}
        public bool CommitTutorialStroke(){pointer=int.MinValue;return EndStroke();}
        public void Cancel(){if(removed!=null)State.Pieces.Add(removed);removed=null;Trail.Clear();entered.Clear();trailStyle=null;pointer=int.MinValue;Refresh();}
        public void BeginCell(int cell){Trail.Clear();entered.Clear();cursorCell=pressCell=cell;moved=false;removed=State.At(cell);if(removed!=null&&Style(removed)!=null&&(Style(removed).Locked||Style(removed).TutorialLocked)){removed=null;return;}if(removed!=null){State.Pieces.Remove(removed);Trail.AddRange(removed.cells);foreach(int c in removed.cells)entered[c]=SourceClock.Now-.3f;trailStyle=Style(removed);}else if(State.IsActive(cell)){EnterCell(cell);trailStyle=MakeStyle(new[]{cell},false);}Refresh();}
        public void MoveCell(int cell)
        {
            if(!State.IsActive(cell)||State.At(cell)!=null||Trail.Count==0)return;if(cell==pressCell&&!moved)return;moved=true;if(cell==cursorCell){Refresh();return;}
            int last=cursorCell,n=State.Level.n;if(Math.Abs(cell/n-last/n)+Math.Abs(cell%n-last%n)>1){int r=last/n,c=last%n;for(int k=0;k<n*2&&last!=cell;k++){int next=r!=cell/n?(r+Math.Sign(cell/n-r))*n+c:r*n+c+Math.Sign(cell%n-c);if(!State.IsActive(next)||State.At(next)!=null||State.Wall(last,next)>0)break;if(!Trail.Contains(next))EnterCell(next);last=cursorCell=next;r=last/n;c=last%n;}Refresh();return;}
            if(State.Wall(last,cell)==0){if(!Trail.Contains(cell))EnterCell(cell);cursorCell=cell;}Refresh();
        }
        public bool EndStroke()
        {
            if(removed!=null&&!moved){removed=null;Trail.Clear();trailStyle=null;SoundBank.Play("card_pick");State.Notify();Refresh();Message?.Invoke(Zh.PieceRemoved);return false;}
            string reason="";int[] attempt=Trail.ToArray();bool allowed=StrokeAllowed==null||StrokeAllowed(attempt);bool ok=allowed&&State.TryPlace(attempt,out reason);if(!ok&&removed!=null)State.Pieces.Add(removed);
            if(!ok&&allowed&&attempt.Length>0){flashCells=new HashSet<int>(attempt);flashUntil=SourceClock.Now+.320f;Animating=true;}
            removed=null;Trail.Clear();trailStyle=null;Refresh();if(ok){Reveal=null;SoundBank.Play("place");Message?.Invoke("");}else if(reason.Length>0){SoundBank.Play("invalid");Message?.Invoke(reason);}return ok;
        }
        public void SetHintPresentation(int[] cells,float strength,float scan=-1){HintCells=cells;HintStrength=strength;ScanProgress=scan;Dirty();}
        public void NotifyChanged(){Refresh();}
    }
    public class BoardMesh:MaskableGraphic
    {
        public PuzzleBoardView View;public int Layer,FacetIndex;public int SymbolStart,SymbolEnd=int.MaxValue;Texture2D texture;
        public override Texture mainTexture{get{if(Layer==1)return StickerMaterial.Texture;if(!texture&&Layer==3)texture=SourceArt.Texture("game_assets_shine");return texture?texture:Texture2D.whiteTexture;}}
        static Color Co(string s){Color c;ColorUtility.TryParseHtmlString(s,out c);return c;}
        static Color Alpha(Color c,float a){c.a=a;return c;}
        void Polygon(VertexHelper vh,IList<Vector2> p,Color c,Func<Vector2,Vector2> uv=null){if(Layer==1){StickerMesh.Solid(vh,p,c,StickerMesh.Pixel(rectTransform)*.8f,uv??(v=>StickerMaterial.WhiteUV));return;}int s=vh.currentVertCount;foreach(var v in p)vh.AddVert(v,c,uv==null?Vector2.zero:uv(v));for(int k=1;k+1<p.Count;k++)vh.AddTriangle(s,s+k,s+k+1);}
        void Quad(VertexHelper vh,Rect r,Color c,Func<Vector2,Vector2> uv=null){Polygon(vh,new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin)},c,uv);}
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float w,Color c){var d=(b-a).normalized;var normal=new Vector2(-d.y,d.x)*w/2;Polygon(vh,new[]{a-normal,a+normal,b+normal,b-normal},c);}
        void Stroke(VertexHelper vh,IList<Vector2> p,float width,Color c,bool closed=true,Func<Vector2,Color> gradient=null)
        {
            if(p.Count<2)return;int start=vh.currentVertCount;
            for(int i=0;i<p.Count;i++){Vector2 previous=i==0?(closed?p[p.Count-1]:p[0]-(p[1]-p[0])):p[i-1],next=i==p.Count-1?(closed?p[0]:p[i]+(p[i]-p[i-1])):p[i+1];var a=(p[i]-previous).normalized;var b=(next-p[i]).normalized;var na=new Vector2(-a.y,a.x);var nb=new Vector2(-b.y,b.x);var m=(na+nb).normalized;if(m.sqrMagnitude<.01f)m=nb;float pressure=1;float length=width*.5f*pressure/Mathf.Max(.25f,Vector2.Dot(m,nb));Color co=gradient==null?c:gradient(p[i]);vh.AddVert(p[i]-m*length,co,Vector2.zero);vh.AddVert(p[i]+m*length,co,Vector2.zero);}
            int segments=closed?p.Count:p.Count-1;for(int i=0;i<segments;i++){int a=start+i*2,b=start+((i+1)%p.Count)*2;vh.AddTriangle(a,a+1,b+1);vh.AddTriangle(b+1,b,a);}
        }
        List<List<Vector2>> Paths(IEnumerable<int> cells,float inset=0){return SourceBoardGeometry.Paths(View.State.Level.n,cells,View.CellSize,View.BoardOriginLocal,inset,4.5f*View.PixelDensity);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(View==null||View.State==null||View.CellSize<=0)return;var b=View.State;int n=b.Level.n;float cs=View.CellSize,dp=View.PixelDensity;
            bool validTrail=View.Trail.Count>0&&b.Library.Match(b.Level,View.Trail)!=null;
            for(int i=0;i<n*n;i++)
            {
                if(!b.IsActive(i))continue;Rect cell=View.CellRect(i);var piece=b.At(i);bool trail=View.Trail.Contains(i);
                if(Layer==0)
                {
                    Quad(vh,cell,Co("#FFF9EE"));
                }
                if(Layer==1&&(piece!=null||trail))
                {
                    var style=piece!=null?View.Style(piece):View.TrailStyle;if(style==null||style.TextureIndex!=FacetIndex)continue;
                    float scale=1,opacity=1;if(piece!=null)View.Appearance(piece,i,out scale,out opacity);else if(trail){View.TrailAppearance(i,out scale,out opacity);opacity*=validTrail?1:.86f;}if(opacity<=0)continue;
                    Color color=Co(PuzzleBoardView.Palette[style.ColorIndex]);color.a=opacity;
                    float inset=Mathf.Clamp(cs*.035f,1.4f*dp,3.5f*dp);
                    Rect outer=new Rect(cell.x+inset,cell.y+inset,cs-inset*2,cs-inset*2);
                    var polygon=SourceBoardGeometry.CellPolygon(outer,cs*.12f,true,true,true,true);
                    for(int k=0;k<polygon.Count;k++)polygon[k]=cell.center+(polygon[k]-cell.center)*scale;
                    float fold=piece!=null?View.CornerFold(piece,i):0;
                    var cut=CutCorner(polygon,outer.xMax+outer.yMax-fold);
                    Polygon(vh,cut.Select(v=>v+Vector2.down*1.4f*dp).ToList(),Alpha(Co("#A48ABF"),opacity*.60f));
                    Polygon(vh,cut,Alpha(Color.white,opacity));
                    Rect face=new Rect(outer.x+1.5f*dp,outer.y+1.5f*dp,outer.width-3*dp,outer.height-3*dp);
                    var facePath=SourceBoardGeometry.CellPolygon(face,cs*.09f,true,true,true,true);
                    for(int k=0;k<facePath.Count;k++)facePath[k]=cell.center+(facePath[k]-cell.center)*scale;
                    var fill=CutCorner(facePath,outer.xMax+outer.yMax-fold-1.3f*dp);
                    Polygon(vh,fill,color,p=>StickerMaterial.UV(style.TextureIndex,p,face));
                    if(fold>1*dp)
                    {
                        Vector2 foldA=new Vector2(outer.xMax-fold,outer.yMax),foldB=new Vector2(outer.xMax,outer.yMax-fold),tip=new Vector2(outer.xMax-fold*.86f,outer.yMax-fold*.86f);
                        Polygon(vh,new[]{foldA,foldB,tip+Vector2.down*1.5f*dp},Alpha(Co("#6645AA"),.20f*opacity));
                        Polygon(vh,new[]{foldA,foldB,tip},Alpha(Co("#FFF6FF"),opacity));Line(vh,foldA,foldB,.8f*dp,Alpha(Color.white,opacity));
                    }
                }
                if(Layer==2&&piece==null&&View.BadCell(i))Quad(vh,cell,new Color(214/255f,84/255f,72/255f,.55f));
            }
            if(Layer==0)SourceGridGeometry.Draw(vh,rectTransform,View);
            if(Layer==2)
            {
                foreach(var p in b.Pieces){bool drawing=View.DrawTrace!=null&&new HashSet<int>(View.DrawTrace).SetEquals(p.cells);var s=View.Style(p);if(drawing&&s!=null)s.Locked=true;if(!drawing&&s!=null)DrawFinishedOutline(vh,p.cells,s);DrawPiece(vh,p.cells,drawing?View.DrawProgress:1,drawing,s!=null&&s.Locked);}
            }
            if(Layer==7)DrawStickerFeedback(vh);
            if(Layer==8)DrawHint(vh);
            if(Layer==3&&View.SweepVisible)DrawSweep(vh);
            if(Layer==5)DrawSymbols(vh);
        }
        void DrawFinishedOutline(VertexHelper vh,IEnumerable<int> cells,PuzzleBoardView.PieceStyle style)
        {
            float opacity=MenuMotion.Reduced?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(style.Duration-.18f,style.Duration+.12f,SourceClock.Now-style.Born));
            if(opacity<=0)return;
            float dp=View.PixelDensity,aa=StickerMesh.Pixel(rectTransform)*.75f;
            Color edge=Color.Lerp(Co(PuzzleBoardView.Palette[style.ColorIndex]),Co("#49335F"),.42f);
            // Trace the union perimeter, including holes, rather than outlining each cell again.
            foreach(var path in Paths(cells,2.4f*dp)){
                StickerMesh.Outline(vh,path,4.1f*dp,Alpha(Color.white,opacity),aa);
                StickerMesh.Outline(vh,path,1.55f*dp,Alpha(edge,opacity),aa);
            }
        }
        void DrawPiece(VertexHelper vh,IEnumerable<int> cells,float progress,bool drawing,bool drawInk)
        {
            float dp=View.PixelDensity;Color ink=Co("#6645AA"),light=Co("#FFFDF6");var paths=Paths(cells);
            if(drawInk&&drawing){foreach(var p in paths)if(drawing&&progress<1)Stroke(vh,SourceBoardGeometry.Prefix(p,progress),2.3f*dp,ink,false);else Stroke(vh,p,2.3f*dp,ink);
            foreach(var p in Paths(cells,3.5f*dp))if(drawing&&progress<1)Stroke(vh,SourceBoardGeometry.Prefix(p,progress),2*dp,light,false);else Stroke(vh,p,2*dp,light);}
            if(View.VictoryInk<=0)return;
            foreach(var p in paths){Stroke(vh,p.Select(x=>x+new Vector2(.9f,-1.2f)*dp).ToList(),6.2f*dp,Alpha(Co("#6645AA"),.85f*View.VictoryInk));Stroke(vh,p.Select(x=>x+new Vector2(-.8f,1)*dp).ToList(),5.8f*dp,Alpha(Co("#FFFFFF"),.95f*View.VictoryInk));Stroke(vh,p,5*dp,Color.white,true,GoldAt);}
            foreach(var p in Paths(cells,3.5f*dp))Stroke(vh,p,1.4f*dp,Alpha(Co("#FFF7D6"),.85f*View.VictoryInk));
        }
        static List<Vector2> CutCorner(IList<Vector2> points,float limit)
        {
            var result=new List<Vector2>();if(points.Count==0)return result;
            Vector2 a=points[points.Count-1];bool insideA=a.x+a.y<=limit;
            foreach(var b in points){bool insideB=b.x+b.y<=limit;if(insideA!=insideB){float delta=b.x+b.y-a.x-a.y;if(Mathf.Abs(delta)>.0001f)result.Add(Vector2.LerpUnclamped(a,b,(limit-a.x-a.y)/delta));}if(insideB)result.Add(b);a=b;insideA=insideB;}return result;
        }
        void DrawHint(VertexHelper vh)
        {
            float dp=View.PixelDensity;
            if(View.ScanProgress>=0){float scanY=Mathf.Lerp(View.VisibleBoundsLocal.yMax,View.VisibleBoundsLocal.yMin,View.ScanProgress);for(int i=0;i<View.State.Level.n*View.State.Level.n;i++)if(View.State.IsActive(i)&&View.State.At(i)==null){var cell=View.CellRect(i);float alpha=Mathf.Max(0,1-Mathf.Abs(cell.center.y-scanY)/(View.CellSize*.8f));if(alpha>0)Quad(vh,cell,Alpha(Co("#C8ADFF"),.25f*alpha));}}
            if(View.HintCells==null)return;
            foreach(int cell in View.HintCells)if(View.State.At(cell)!=null)return;
            float strength=Mathf.Clamp01(View.HintStrength);
            foreach(int cell in View.HintCells){Rect r=View.CellRect(cell);float pad=3*dp;r=new Rect(r.x+pad,r.y+pad,r.width-2*pad,r.height-2*pad);Polygon(vh,SourceBoardGeometry.CellPolygon(r,4*dp,true,true,true,true),Alpha(Co("#FFE36B"),.30f*strength));}
            foreach(var path in Paths(View.HintCells,3*dp)){Stroke(vh,path,5*dp,Alpha(Color.white,strength));Stroke(vh,path,2.2f*dp,Alpha(Co("#9D6ED9"),strength));}
        }
        void DrawStickerFeedback(VertexHelper vh)
        {
            float dp=View.PixelDensity;
            if(MenuMotion.Reduced)return;
            foreach(var piece in View.State.Pieces)
            {
                var style=View.Style(piece);if(style==null||style.Order.Length==0)continue;
                float t=(SourceClock.Now-style.Born-.28f)/.46f;if(t<0||t>=1)continue;
                Vector2 center=View.CellCenterLocal(style.Order[style.Order.Length-1]);
                for(int i=0;i<6;i++)
                {
                    float angle=(i*60+25)*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    var point=center+direction*View.CellSize*(.20f+.35f*SourceMotion.OutQuad(t));float radius=(i%2==0?5:3)*dp*Mathf.Sin(Mathf.PI*t);
                    Color color=Alpha(Co(i%3==0?"#FF638B":i%3==1?"#FFE36B":"#6645AA"),(1-t)*.95f);
                    Polygon(vh,new[]{point+Vector2.up*radius*1.5f,point+Vector2.right*radius,point+Vector2.down*radius*1.5f,point+Vector2.left*radius},color);
                }
            }
        }
        Color GoldAt(Vector2 p){float[] stops={0,.25f,.45f,.6f,.8f,1};string[] colors={"#FF83AC","#B6A0F5","#FFE36B","#75DABA","#FF83AC","#B6A0F5"};Vector2 local=p-View.BoardOriginLocal;float t=Mathf.Clamp01((.7f*local.x-local.y)/(View.BoardSize*1.49f));for(int i=1;i<stops.Length;i++)if(t<=stops[i])return Alpha(Color.Lerp(Co(colors[i-1]),Co(colors[i]),Mathf.InverseLerp(stops[i-1],stops[i],t)),View.VictoryInk);return Alpha(Co(colors[colors.Length-1]),View.VictoryInk);}
        static List<Vector2> CirclePoints(Vector2 center,float radius,int segments=48)
        {var p=new List<Vector2>();for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;p.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}return p;}
        void Disc(VertexHelper vh,Vector2 center,float radius,Color color){StickerMesh.Solid(vh,CirclePoints(center,radius),color,StickerMesh.Pixel(rectTransform)*.8f);}
        void RoundStroke(VertexHelper vh,IList<Vector2> path,float width,Color color,bool closed=true)
        {if(!closed&&path.Count==2){var d=path[1]-path[0];var axis=d.normalized;var side=new Vector2(-axis.y,axis.x);var center=(path[0]+path[1])*.5f;var capsule=StickerMesh.Rounded(new Rect(-d.magnitude*.5f-width*.5f,-width*.5f,d.magnitude+width,width),width*.5f);StickerMesh.Solid(vh,capsule.Select(p=>center+axis*p.x+side*p.y).ToList(),color,StickerMesh.Pixel(rectTransform)*.7f);return;}for(int i=1;i<path.Count;i++)Line(vh,path[i-1],path[i],width,color);if(closed&&path.Count>1)Line(vh,path[path.Count-1],path[0],width,color);foreach(var p in path)Disc(vh,p,width/2,color);}
        void ConcavePolygon(VertexHelper vh,IList<Vector2> path,Color color)
        {int start=vh.currentVertCount;foreach(var p in path)vh.AddVert(p,color,Vector2.zero);var indices=SourceBoardGeometry.Triangulate(path);for(int i=0;i+2<indices.Count;i+=3)vh.AddTriangle(start+indices[i],start+indices[i+1],start+indices[i+2]);}
        static Vector2 Cubic(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        static List<Vector2> IconPath(int kind,Vector2 center,float radius)
        {
            var p=new List<Vector2>();
            if(kind==1)for(int i=0;i<10;i++){float a=-Mathf.PI/2+i*Mathf.PI/5,r=i%2==0?radius:radius*.46f;p.Add(center+new Vector2(Mathf.Cos(a),-Mathf.Sin(a))*r);}
            else if(kind==2){Vector2 a=new Vector2(0,-.9f),b=new Vector2(-1.45f,.05f),c=new Vector2(-.8f,1.1f),d=new Vector2(0,.35f),e=new Vector2(.8f,1.1f),f=new Vector2(1.45f,.05f);for(int i=0;i<24;i++)p.Add(center+Cubic(a,b,c,d,i/24f)*radius);for(int i=0;i<24;i++)p.Add(center+Cubic(d,e,f,a,i/24f)*radius);}
            else if(kind==3)p.AddRange(new[]{center+Vector2.up*radius,center+Vector2.right*radius*.78f,center+Vector2.down*radius,center+Vector2.left*radius*.78f});
            else if(kind==5)for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40,r=radius*(.74f+.26f*Mathf.Cos(4*a));p.Add(center+new Vector2(Mathf.Cos(a),-Mathf.Sin(a))*r);}
            return p;
        }
        void DrawSymbols(VertexHelper vh)
        {
            var b=View.State;int n=b.Level.n;float dp=View.PixelDensity,cs=View.CellSize,r=Mathf.Clamp(cs/dp*.17f,6,14)*dp,w=Mathf.Max(1.4f,cs/dp*.028f)*dp;Color paper=Co("#FFF9EE"),ink=Co("#6645AA");
            int first=Mathf.Max(0,SymbolStart),last=Mathf.Min(n*n,SymbolEnd);
            if(b.Level.icons!=null)for(int i=first;i<last;i++)
            {
                int kind=b.Level.icons[i];if(kind<1||kind>5||!b.IsActive(i))continue;Vector2 center=View.CellCenterLocal(i);Disc(vh,center,r*1.35f,Alpha(paper,.8f));
                if(kind==4){int start=vh.currentVertCount;for(int j=0;j<48;j++){float a=j*Mathf.PI*2/48;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));vh.AddVert(center+d*r,paper,Vector2.zero);vh.AddVert(center+d*r*.38f,paper,Vector2.zero);}for(int j=0;j<48;j++){int a=start+j*2,z=start+((j+1)%48)*2;vh.AddTriangle(a,z,z+1);vh.AddTriangle(a,z+1,a+1);}RoundStroke(vh,CirclePoints(center,r),w,ink);RoundStroke(vh,CirclePoints(center,r*.38f),w,ink);}
                else{var p=IconPath(kind,center,r);ConcavePolygon(vh,p,paper);RoundStroke(vh,p,w,ink);}
            }
            if(b.Level.walls==null)return;float wr=StickerBarrierGraphic.BadgeSize(cs,dp)*.29f,ww=Mathf.Max(1.5f*dp,wr*.25f);
            for(int i=first;i<last;i++){Rect cell=View.CellRect(i);if(b.Level.walls.e[i]>0)WallMark(vh,new Vector2(cell.xMax,cell.center.y),b.Level.walls.e[i],wr,ww,paper,ink);if(b.Level.walls.s[i]>0)WallMark(vh,new Vector2(cell.center.x,cell.yMin),b.Level.walls.s[i],wr,ww,paper,ink);}
        }
        void WallMark(VertexHelper vh,Vector2 center,int kind,float radius,float width,Color paper,Color ink)
        {
            StickerRuleGlyph.Draw(vh,rectTransform,center,kind,radius,width);
        }

        void DrawSweep(VertexHelper vh)
        {
            float size=View.BoardSize,width=size*.32f,height=size*2.6f,u=SourceMotion.InOutQuad(View.SweepProgress);Vector2 o=View.BoardOriginLocal;
            Vector2 center=o+new Vector2(Mathf.Lerp(-size,2*size,u)+width/2,-size/2);float angle=22*Mathf.Deg2Rad;Vector2 across=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),along=new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle));
            Vector2[] band={center-across*width/2-along*height/2,center-across*width/2+along*height/2,center+across*width/2+along*height/2,center+across*width/2-along*height/2};
            int n=View.State.Level.n;foreach(var piece in View.State.Pieces)foreach(int cell in piece.cells){Func<int,bool> same=j=>Array.IndexOf(piece.cells,j)>=0;bool top=cell/n>0&&same(cell-n),bottom=cell/n<n-1&&same(cell+n),left=cell%n>0&&same(cell-1),right=cell%n<n-1&&same(cell+1);var boundary=SourceBoardGeometry.CellPolygon(View.CellRect(cell),4.5f*View.PixelDensity,!top&&!left,!top&&!right,!bottom&&!right,!bottom&&!left);var clip=SourceBoardGeometry.ClipConvex(band,boundary);if(clip.Count<3)continue;Polygon(vh,clip,Color.white,p=>new Vector2(Vector2.Dot(p-center,across)/width+.5f,Vector2.Dot(p-center,along)/height+.5f));}
        }
    }
}
