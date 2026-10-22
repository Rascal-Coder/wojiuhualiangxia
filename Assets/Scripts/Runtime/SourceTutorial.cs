using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        // HBC module2147 config, module2150 walk/timeline, module2151 finger,
        // module2152 paper panel, module2144 demo -> guide -> user transitions.
        // This is the level-one inline instruction, not a substitute modal.
        public const float SourceTutorialReserveDp=112;
        int sourceTutorialEpoch=-1;string sourceTutorialPhase;
        public string SourceTutorialPhase{get{return sourceTutorialEpoch==presentationEpoch?sourceTutorialPhase:null;}private set{sourceTutorialEpoch=presentationEpoch;sourceTutorialPhase=value;}}
        void Tutorial(){StartCoroutine(SourceDrawTutorial(presentationEpoch,Board,BoardView));}
        bool TutorialAlive(int epoch,BoardState state,PuzzleBoardView view)
        {return epoch==presentationEpoch&&Playing&&ReferenceEquals(Board,state)&&BoardView==view&&view;}

        IEnumerator SourceDrawTutorial(int epoch,BoardState state,PuzzleBoardView view)
        {
            if(!view||state==null)yield break;
            var oldCanPlace=view.StrokeAllowed;float oldReserve=view.TutorialReservedDp;bool oldTutorial=view.Tutorial;
            RectTransform layer=null,paper=null;CanvasGroup dimGroup=null;Image hand=null;Text title=null,body=null;SourceTutorialHintGraphic hint=null;
            SourceTutorialPhase="demo";AnimationBusy=true;view.Tutorial=true;view.TutorialReservedDp=SourceTutorialReserveDp;view.Refresh();
            try
            {
                // Dim everything outside the board wrap, as TutorialDimOverlay does.
                layer=Skin.Fill("Source draw tutorial",Effects);dimGroup=Skin.Fill("Tutorial outside dim",layer).gameObject.AddComponent<CanvasGroup>();
                dimGroup.blocksRaycasts=false;dimGroup.interactable=false;
                var area=(RectTransform)view.transform;var corners=new Vector3[4];area.GetWorldCorners(corners);
                var lower=(Vector2)layer.InverseTransformPoint(corners[0]);var upper=(Vector2)layer.InverseTransformPoint(corners[2]);var bounds=layer.rect;
                TutorialShade(dimGroup.transform,Rect.MinMaxRect(bounds.xMin,upper.y,bounds.xMax,bounds.yMax));
                TutorialShade(dimGroup.transform,Rect.MinMaxRect(bounds.xMin,bounds.yMin,bounds.xMax,lower.y));
                TutorialShade(dimGroup.transform,Rect.MinMaxRect(bounds.xMin,lower.y,lower.x,upper.y));
                TutorialShade(dimGroup.transform,Rect.MinMaxRect(upper.x,lower.y,bounds.xMax,upper.y));
                // RN panel maxWidth360dp, actual height follows title/body line metrics.
                float panelW=Mathf.Min(360*D,area.rect.width-20*D),panelH=112*D;
                paper=Skin.Rect("mzTutPanel",area,new Vector2(.5f,0),new Vector2(0,4*D+panelH/2),new Vector2(panelW,panelH));
                Skin.RoundBox(paper,new Vector2(.5f,.5f),Vector2.zero,paper.sizeDelta,new Color32(255,215,0,255),16*D);
                Skin.RoundBox(paper,new Vector2(.5f,.5f),Vector2.zero,paper.sizeDelta-Vector2.one*4*D,new Color32(255,248,220,255),14*D);
                var titleRect=Skin.Rect("Tutorial title",paper,new Vector2(.5f,1),Vector2.zero,new Vector2(panelW-40*D,24*D));
                var trackedTitle=titleRect.gameObject.AddComponent<SourceTrackedText>();trackedTitle.LetterSpacing=D;trackedTitle.font=Skin.Font;trackedTitle.fontSize=Mathf.RoundToInt(24*D);trackedTitle.color=new Color32(204,119,34,255);trackedTitle.alignment=TextAnchor.MiddleCenter;trackedTitle.raycastTarget=false;trackedTitle.text=Zh.DrawShapes;title=trackedTitle;
                title.horizontalOverflow=HorizontalWrapMode.Overflow;title.verticalOverflow=VerticalWrapMode.Overflow;
                body=Skin.Text(Zh.DrawBody,paper,new Vector2(.5f,1),new Vector2(0,-(16+24+8+23)*D),new Vector2(panelW-40*D,46*D),Mathf.RoundToInt(16*D),new Color32(74,58,40,255));
                body.lineSpacing=23f/16;body.verticalOverflow=VerticalWrapMode.Overflow;LayoutTutorialPaper(paper,title,body,panelW);
                var handRect=Skin.Rect("mzTutHand",layer,new Vector2(.5f,.5f),Vector2.zero,Vector2.one);
                hand=handRect.gameObject.AddComponent<Image>();hand.sprite=Skin.Sprite("shell_src_ui_tutorialhand");hand.raycastTarget=false;hand.preserveAspect=true;hand.color=new Color(1,1,1,0);
                var hintRect=Skin.Fill("Tutorial guided region",area);hint=hintRect.gameObject.AddComponent<SourceTutorialHintGraphic>();hint.View=view;hint.raycastTarget=false;hint.enabled=false;
                // Keep the hint below the hand and paper; it draws only empty cells.
                paper.SetAsLastSibling();
                float started=SourceClock.Now;
                while(TutorialAlive(epoch,state,view)&&SourceClock.Now-started<.600f){dimGroup.alpha=SourceMotion.DefaultTiming((SourceClock.Now-started)/.220f);yield return null;}
                if(!TutorialAlive(epoch,state,view))yield break;dimGroup.alpha=1;
                var demo=TutorialNextPiece(state);
                if(demo!=null)
                {
                    int[] walk=TutorialWalk(demo.cells,state.Level.n);
                    yield return TutorialHand(walk,view,hand,layer,epoch,state,true);
                    if(!TutorialAlive(epoch,state,view))yield break;
                    view.Refresh();var placed=state.At(demo.cells[0]);if(placed!=null&&new HashSet<int>(placed.cells).SetEquals(demo.cells)){var style=view.Style(placed);if(style!=null){style.TutorialLocked=true;placed.locked=true;placed.tutLock=true;}}
                    float between=SourceClock.Now;while(TutorialAlive(epoch,state,view)&&SourceClock.Now-between<.650f)yield return null;
                }
                if(!TutorialAlive(epoch,state,view)||state.Solved)yield break;
                // Original @mz_mech_intro_seen_draw is persisted after demonstration.
                Store.Data.drawIntroSeen=true;Store.Save();AnimationBusy=false;SourceTutorialPhase="guide";
                title.text=Zh.YourTurn;body.text=Zh.FollowHand;LayoutTutorialPaper(paper,title,body,panelW);
                var guide=TutorialNextPiece(state);if(guide==null){var solved=state.Solve(60000);if(solved!=null)guide=solved.FirstOrDefault();}
                if(guide!=null)
                {
                    int[] guideCells=guide.cells.ToArray();var guideSet=new HashSet<int>(guideCells);
                    view.StrokeAllowed=cells=>(oldCanPlace==null||oldCanPlace(cells))&&cells.Length==guideSet.Count&&guideSet.SetEquals(cells);
                    int before=state.Pieces.Count;float idle=SourceClock.Now;IEnumerator handLoop=null;
                    while(TutorialAlive(epoch,state,view)&&!state.Solved&&state.Pieces.Count<=before)
                    {
                        if(popups.Count>0){hand.color=new Color(1,1,1,0);hint.enabled=false;handLoop=null;idle=SourceClock.Now;yield return null;continue;}
                        if(view.Trail.Count>0)
                        {
                            idle=SourceClock.Now;handLoop=null;hand.color=new Color(1,1,1,0);hint.enabled=false;
                            if(view.Trail.Count==guideSet.Count&&guideSet.SetEquals(view.Trail))view.CommitTutorialStroke();
                        }
                        else if(SourceClock.Now-idle>=.500f)
                        {
                            hint.enabled=true;hint.Cells=guideCells;hint.Pulse=(SourceClock.Now-idle-.500f);
                            if(handLoop==null)handLoop=TutorialHand(TutorialWalk(guideCells,state.Level.n),view,hand,layer,epoch,state,false);
                            if(!handLoop.MoveNext())handLoop=null;
                        }
                        yield return null;
                    }
                    view.StrokeAllowed=oldCanPlace;hand.color=new Color(1,1,1,0);hint.enabled=false;
                }
                if(!TutorialAlive(epoch,state,view)||state.Solved)yield break;
                SourceTutorialPhase="user";AnimationBusy=false;title.text=Zh.TryYourself;
                body.text=state.Level.solution!=null&&state.Level.solution.Length-state.Pieces.Count>1?Zh.RemainingShapes:Zh.LastShape;
                LayoutTutorialPaper(paper,title,body,panelW);
                // The original instruction stays inline until the level is completed.
                while(TutorialAlive(epoch,state,view)&&!state.Solved)yield return null;
                if(TutorialAlive(epoch,state,view))
                {
                    SourceTutorialPhase=null;paper.gameObject.SetActive(false);hint.enabled=false;
                    float fadeStart=SourceClock.Now;while(TutorialAlive(epoch,state,view)&&SourceClock.Now-fadeStart<.120f){dimGroup.alpha=1-SourceMotion.DefaultTiming((SourceClock.Now-fadeStart)/.120f);yield return null;}
                }
            }
            finally
            {
                if(view){view.StrokeAllowed=oldCanPlace;view.Tutorial=oldTutorial;view.TutorialReservedDp=oldReserve;view.Refresh();}
                if(layer){layer.gameObject.SetActive(false);Destroy(layer.gameObject);}if(paper){paper.gameObject.SetActive(false);Destroy(paper.gameObject);}if(hint)Destroy(hint.gameObject);
                if(epoch==presentationEpoch){AnimationBusy=false;SourceTutorialPhase=null;}
            }
        }
        static void LayoutTutorialPaper(RectTransform panel,Text title,Text body,float width)
        {
            // Source panel has no fixed height: 2dp border + 14dp padding,
            // title + 8dp margin + wrapped 23dp body lines + symmetric bottom.
            // Unity uses the shipped font's own metrics for the title line box.
            float titleH=Mathf.Max(24*D,title.preferredHeight),bodyH=Mathf.Max(23*D,body.preferredHeight);
            float height=32*D+titleH+8*D+bodyH;panel.sizeDelta=new Vector2(width,height);panel.anchoredPosition=new Vector2(0,4*D+height/2);
            title.rectTransform.sizeDelta=new Vector2(width-40*D,titleH);title.rectTransform.anchoredPosition=new Vector2(0,-16*D-titleH/2);
            body.rectTransform.sizeDelta=new Vector2(width-40*D,bodyH);body.rectTransform.anchoredPosition=new Vector2(0,-16*D-titleH-8*D-bodyH/2);
            var backgrounds=panel.GetComponentsInChildren<RoundedGraphic>();for(int i=0;i<backgrounds.Length;i++)backgrounds[i].rectTransform.sizeDelta=panel.sizeDelta-Vector2.one*(i==0?0:4*D);
        }
        static void TutorialShade(Transform parent,Rect rect){if(rect.width<=0||rect.height<=0)return;Skin.ColorBox(parent,new Vector2(.5f,.5f),rect.center,rect.size,new Color(4/255f,9/255f,18/255f,.55f));}
        static PieceData TutorialNextPiece(BoardState state)
        {return state.Level.solution==null?null:state.Level.solution.Where(p=>state.Level.targets.Contains(p.shape)&&p.cells.All(i=>state.IsActive(i)&&state.At(i)==null)).OrderBy(p=>p.cells.Min()).FirstOrDefault();}
        // Original DFS: upper-left start, R/D/L/U neighbors, unvisited-degree sort
        // (stable ties); include the backtracking edge while any cells remain unseen.
        static int[] TutorialWalk(int[] cells,int n)
        {
            if(cells==null||cells.Length==0)return new int[0];var set=new HashSet<int>(cells);var seen=new HashSet<int>();var stack=new List<int>();var path=new List<int>();
            Func<int,IEnumerable<int>> nb=i=>new[]{i%n<n-1?i+1:-1,i/n<n-1?i+n:-1,i%n>0?i-1:-1,i/n>0?i-n:-1}.Where(set.Contains);
            int first=cells.Min();seen.Add(first);stack.Add(first);path.Add(first);
            while(stack.Count>0){var next=nb(stack[stack.Count-1]).Where(i=>!seen.Contains(i)).OrderBy(i=>nb(i).Count(j=>!seen.Contains(j))).ToArray();if(next.Length>0){seen.Add(next[0]);stack.Add(next[0]);path.Add(next[0]);}else{stack.RemoveAt(stack.Count-1);if(stack.Count>0&&seen.Count<set.Count)path.Add(stack[stack.Count-1]);}}
            return path.ToArray();
        }
        IEnumerator TutorialHand(int[] walk,PuzzleBoardView view,Image hand,RectTransform layer,int epoch,BoardState state,bool demo)
        {
            if(walk.Length==0)yield break;float w=Mathf.Clamp(Mathf.Round(view.CellSize/D*.86f),44,78)*D,h=Mathf.Round(w/D*1.1594202898550725f)*D;
            hand.rectTransform.sizeDelta=new Vector2(w,h);hand.transform.SetAsLastSibling();float lift=.540f+(walk.Length-1)*.300f+.140f,total=lift+.180f+.200f;
            float start=SourceClock.Now;int next=0;bool committed=false;
            while(TutorialAlive(epoch,state,view)&&SourceClock.Now-start<total+(demo?0:.700f))
            {
                float t=SourceClock.Now-start;float alpha=t<.240f?SourceMotion.DefaultTiming(t/.240f):t<lift+.180f?1:1-SourceMotion.DefaultTiming((t-lift-.180f)/.200f);
                float scale=t<.240f?1:t<.400f?Mathf.Lerp(1,.84f,SourceMotion.InOutQuad((t-.240f)/.160f)):t<lift?.84f:Mathf.Lerp(.84f,1,SourceMotion.InOutQuad((t-lift)/.180f));
                int step=Mathf.Clamp(Mathf.FloorToInt((t-.540f)/.300f),0,Mathf.Max(0,walk.Length-2));Vector2 point=view.CellCenterLocal(walk[0]);
                if(walk.Length>1&&t>=.540f){point=Vector2.Lerp(view.CellCenterLocal(walk[step]),view.CellCenterLocal(walk[step+1]),SourceMotion.InOutQuad((t-.540f-step*.300f)/.300f));}
                var local=(Vector2)layer.InverseTransformPoint(view.transform.TransformPoint(point));hand.rectTransform.anchoredPosition=local+new Vector2(w*.36f,-h*.46f);hand.rectTransform.localScale=Vector3.one*scale;hand.color=new Color(1,1,1,Mathf.Clamp01(alpha));
                if(demo)
                {
                    if(next==0&&t>=.400f){view.BeginCell(walk[0]);next=1;}
                    while(next>0&&next<walk.Length&&t>=.540f+next*.300f){view.MoveCell(walk[next]);next++;}
                    if(!committed&&t>=lift){view.EndStroke();committed=true;}
                }
                yield return null;
            }
            // A screen change can destroy the tutorial hand while this nested
            // coroutine is yielding. Do not touch a disposed Graphic on exit.
            if(hand)hand.color=new Color(1,1,1,0);
        }
    }

    public sealed class SourceTutorialHintGraphic:MaskableGraphic
    {
        public PuzzleBoardView View;public int[] Cells;public float Pulse;
        void Update(){SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!View||Cells==null)return;float p=Mathf.Repeat(Pulse,1.6f),u=p<=.8f?SourceMotion.DefaultTiming(p/.8f):1-SourceMotion.DefaultTiming((p-.8f)/.8f);
            Color fill;ColorUtility.TryParseHtmlString(PuzzleBoardView.Palette[View.NextColorIndex%PuzzleBoardView.Palette.Length],out fill);fill.a=Mathf.Lerp(.28f,.62f,u);var set=new HashSet<int>(Cells);int n=View.State.Level.n;float cs=View.CellSize,stroke=View.PixelDensity*2;
            foreach(int i in Cells){if(View.State.At(i)!=null||View.Trail.Contains(i))continue;Vector2 p0=View.CellCenterLocal(i);Rect r=new Rect(p0-Vector2.one*cs/2,Vector2.one*cs);Quad(vh,r,fill);var ink=new Color(43/255f,38/255f,34/255f,fill.a);if(i/n==0||!set.Contains(i-n))Quad(vh,new Rect(r.xMin,r.yMax-stroke,r.width,stroke),ink);if(i/n==n-1||!set.Contains(i+n))Quad(vh,new Rect(r.xMin,r.yMin,r.width,stroke),ink);if(i%n==0||!set.Contains(i-1))Quad(vh,new Rect(r.xMin,r.yMin,stroke,r.height),ink);if(i%n==n-1||!set.Contains(i+1))Quad(vh,new Rect(r.xMax-stroke,r.yMin,stroke,r.height),ink);}
        }
        static void Quad(VertexHelper vh,Rect r,Color color){int k=vh.currentVertCount;vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    }
}
