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
        Vector2 EffectPoint(Vector2 screen){Vector2 p;var canvas=Effects.GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(Effects,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out p);return p;}
        Vector2 EffectCell(int cell){return EffectPoint(BoardView.CellCenterScreen(cell));}
        Vector2 BoosterOrigin(int i){return new Vector2(289.5f+i*252-540,1200-2197.5f);}
        public void UseBooster(int index)
        {
            if(!InputEnabled||index<0||index>2)return;int unlock=index==0?3:index==1?7:10;
            if(CurrentLevel<unlock){SetMessage(Zh.Unlock(unlock));return;}
            int count=index==0?Store.Data.hint:index==1?Store.Data.freeze:Store.Data.lens;
            if(count<=0){BoosterShop(index);return;}
            if(index==1){Store.Data.freeze--;Store.Save();BoosterCounts();StartCoroutine(SourceFreeze(presentationEpoch));return;}
            var answer=Board.Solve(60000);if(answer==null||answer.Count==0){SetMessage(Zh.RemoveForRoom);return;}
            AnimationBusy=true;if(index==0){Store.Data.hint--;StartCoroutine(SourcePen(answer[0],presentationEpoch));}
            else {Store.Data.lens--;StartCoroutine(SourceLens(answer.Take(2).ToArray(),presentationEpoch));}
            Store.Save();BoosterCounts();
        }
        static Vector2 Along(Vector2[] points,float fraction)
        {if(points.Length==0)return Vector2.zero;if(points.Length==1)return points[0];float total=0;for(int i=1;i<points.Length;i++)total+=Vector2.Distance(points[i-1],points[i]);float remain=Mathf.Clamp01(fraction)*total;for(int i=1;i<points.Length;i++){float d=Vector2.Distance(points[i-1],points[i]);if(remain<=d)return Vector2.Lerp(points[i-1],points[i],d<.001f?0:remain/d);remain-=d;}return points[points.Length-1];}
        IEnumerator SourcePen(PieceData piece,int epoch)
        {
            var view=BoardView;var screenPath=view.GetOutlineTrace(piece.cells);Vector2[] path=screenPath.Select(EffectPoint).ToArray();if(path.Length==0){AnimationBusy=false;yield break;}
            var pen=Skin.Rect("Source pen tip",Effects,new Vector2(.5f,.5f),BoosterOrigin(0),Vector2.zero);
            var image=Skin.Art("game_assets_pen",pen,new Vector2(.5f,.5f),new Vector2(16,20)*D,Vector2.one*56*D);
            pen.localRotation=Quaternion.Euler(0,0,-42);var group=pen.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Vector2 from=pen.anchoredPosition,to=path[0];
            yield return SourceMotion.Tween(.380f,t=>{if(pen)pen.anchoredPosition=Vector2.Lerp(from,to,t)+Vector2.up*(Mathf.Sin(Mathf.PI*t)*36*D);},SourceMotion.OutCubic);
            if(epoch!=presentationEpoch||!pen||!view)yield break;
            // Ink belongs to the locked piece. Its cell pop starts only after the outline has completed.
            view.SetPenTrace(piece.cells,0);suppress=true;string reason;bool ok=Board.TryPlace(piece.cells,out reason);suppress=false;
            if(!ok){Destroy(pen.gameObject);view.SetPenTrace(null,1);AnimationBusy=false;yield break;}
            SoundBank.Play("hint_sparkle"); // commitPen locks the piece before its ink animation.
            float length=0;for(int i=1;i<path.Length;i++)length+=Vector2.Distance(path[i-1],path[i])/D;
            float seconds=Mathf.Round(Mathf.Clamp(length/900*1000,520,1200))/1000;
            yield return SourceMotion.Tween(seconds,t=>{if(!pen||!view||epoch!=presentationEpoch)return;pen.anchoredPosition=Along(path,t);pen.localRotation=Quaternion.Euler(0,0,-42+Mathf.Sin(t*Mathf.PI*24)*4);view.SetPenTrace(piece.cells,t);});
            if(epoch!=presentationEpoch||!pen||!view)yield break;view.SetPenTrace(piece.cells,1);
            Vector2 end=pen.anchoredPosition;yield return SourceMotion.Tween(.220f,t=>{if(!pen)return;pen.anchoredPosition=end+Vector2.up*(14*D*t);pen.localScale=Vector3.one*Mathf.Lerp(1,.86f,t);group.alpha=1-t;},SourceMotion.OutQuad);
            if(pen)Destroy(pen.gameObject);if(epoch!=presentationEpoch||!view)yield break;view.SetPenTrace(null,1);AnimationBusy=false;BoardChanged();SaveBoard();
        }
        IEnumerator SourceFreeze(int epoch)
        {
            var root=Skin.Fill("Source freeze flight",Effects);var particle=Skin.Art("shell_src_ui_defaultparticle",root,new Vector2(.5f,.5f),BoosterOrigin(1),Vector2.one*64*D);
            Vector2 destination=new Vector2(0,1200-201-(headerLayout?headerLayout.Offset:0));yield return SourceMotion.Tween(.330f,t=>{if(particle)particle.rectTransform.anchoredPosition=Vector2.Lerp(BoosterOrigin(1),destination,t);},SourceMotion.OutCubic);
            if(epoch!=presentationEpoch||!root)yield break;Destroy(root.gameObject);frozen=10;TimerChanged();
            var layer=Skin.Fill("Source freeze edge",Effects);var group=layer.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            var edge=Skin.Art("shell_src_ui_freezeoverlay",layer,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400),false);edge.rectTransform.localScale=new Vector3(1.3f,1.8f,1);
            var flakeRoots=new List<RectTransform>();var yBase=new List<float>();var rand=new System.Random(19);
            for(int i=0;i<24;i++){float x=(i%2==0?1+rand.Next(13):84+rand.Next(13))*.01f*1080-540,y=1200-(4+rand.Next(91))*.01f*2400,size=(13+rand.Next(21))*D;var f=Skin.Art("shell_src_ui_freeze",layer,new Vector2(.5f,.5f),new Vector2(x,y),Vector2.one*size);f.color=new Color(1,1,1,.55f+(float)rand.NextDouble()*.4f);f.rectTransform.localRotation=Quaternion.Euler(0,0,rand.Next(360));flakeRoots.Add(f.rectTransform);yBase.Add(y);}
            float born=SourceClock.Now;while(epoch==presentationEpoch&&layer&&(frozen>0||SourceClock.Now-born<.220f))
            {float t=SourceClock.Now-born;group.alpha=SourceMotion.OutQuad(t/.220f);for(int i=0;i<flakeRoots.Count;i++){var f=flakeRoots[i];float period=2.6f+.6f*(i%4),phase=Mathf.Max(0,t-i*.300f)/period*Mathf.PI*2;f.anchoredPosition=new Vector2(f.anchoredPosition.x,yBase[i]+Mathf.Sin(phase)*(3+i%5)*D);}yield return null;}
            if(epoch!=presentationEpoch||!layer)yield break;yield return SourceMotion.Tween(.360f,t=>{if(group)group.alpha=1-t;},SourceMotion.OutQuad);if(layer)Destroy(layer.gameObject);TimerChanged();
        }
        // Centripetal Catmull-Rom, alpha .5, sampled ten times per source segment.
        static Vector2 Catmull(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {float t0=0,t1=t0+Mathf.Sqrt(Vector2.Distance(a,b))+.0001f,t2=t1+Mathf.Sqrt(Vector2.Distance(b,c))+.0001f,t3=t2+Mathf.Sqrt(Vector2.Distance(c,d))+.0001f,u=Mathf.Lerp(t1,t2,t);Vector2 A1=((t1-u)*a+(u-t0)*b)/(t1-t0),A2=((t2-u)*b+(u-t1)*c)/(t2-t1),A3=((t3-u)*c+(u-t2)*d)/(t3-t2),B1=((t2-u)*A1+(u-t0)*A2)/(t2-t0),B2=((t3-u)*A2+(u-t1)*A3)/(t3-t1);return ((t2-u)*B1+(u-t1)*B2)/(t2-t1);}
        IEnumerator SourceLens(PieceData[] answer,int epoch)
        {
            var view=BoardView;float size=Mathf.Clamp(Mathf.Round(view.CellSize/D*1.5f),56,96)*D;
            var lens=Skin.Rect("Source lens center",Effects,new Vector2(.5f,.5f),BoosterOrigin(2),Vector2.zero);var group=lens.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Skin.Art("game_assets_lens",lens,new Vector2(.5f,.5f),new Vector2(.11f,-.13f)*size,Vector2.one*size);
            var bounds=view.VisibleBoundsLocal;Vector2 Local(Vector2 p){var canvas=Effects.GetComponentInParent<Canvas>();return EffectPoint(RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,view.transform.TransformPoint(p)));}
            Vector2 B(float x,float y){return Local(new Vector2(Mathf.Lerp(bounds.xMin,bounds.xMax,x),Mathf.Lerp(bounds.yMax,bounds.yMin,y)));}
            var route=new List<Vector2>{BoosterOrigin(2),B(.20f,.22f),B(.80f,.45f),B(.24f,.78f)};route.AddRange(answer.Select(p=>EffectCell(p.cells.OrderBy(x=>x).First())));
            float begin=SourceClock.Now;Vector2 previous=route[0];
            for(int i=1;i<route.Count;i++)
            {
                int segment=i;Vector2 a=i<2?route[0]*2-route[1]:route[i-2],b=route[i-1],c=route[i],d=i+1<route.Count?route[i+1]:c*2-b;
                var sampled=new Vector2[11];for(int j=0;j<=10;j++)sampled[j]=Catmull(a,b,c,d,j/10f);
                float duration=Mathf.Clamp(Vector2.Distance(b,c)/D/.9f,i==1?320:210,i==1?440:360)/1000;
                yield return SourceMotion.Tween(duration,t=>{if(!lens||epoch!=presentationEpoch)return;Vector2 now=Along(sampled,t),velocity=(now-previous)/Mathf.Max(.001f,SourceClock.Delta);lens.anchoredPosition=now;lens.localRotation=Quaternion.Euler(0,0,-Mathf.Clamp(velocity.x/D/1000*14,-12,12));previous=now;group.alpha=Mathf.Clamp01((SourceClock.Now-begin)/.090f);},i==1?(Func<float,float>)SourceMotion.OutQuad:SourceMotion.InOutQuad);
                if(epoch!=presentationEpoch||!lens||!view)yield break;
                if(segment<=3)yield return SourceMotion.Tween(.070f,t=>{if(lens)lens.localScale=Vector3.one*(1+Mathf.Sin(t*Mathf.PI)*.06f);});
                else
                {
                    int which=segment-4;var p=answer[which];StartCoroutine(LensArrival(p,lens.anchoredPosition,epoch,which==answer.Length-1));
                    yield return SourceMotion.Tween(.230f,t=>{if(lens)lens.localScale=Vector3.one*(t<.090f/.230f?Mathf.Lerp(1,1.14f,t*.230f/.090f):Mathf.Lerp(1.14f,1,(t*.230f-.090f)/.140f));});
                }
            }
            if(epoch!=presentationEpoch||!lens)yield break;float radius=Mathf.Clamp(view.CellSize/D*.24f,9,19)*D,ratio=radius/(.26f*size);
            yield return SourceMotion.Tween(.240f,t=>{if(lens){lens.localScale=Vector3.one*Mathf.Lerp(1,ratio,t);group.alpha=1-t;}},SourceMotion.OutQuad);
            if(lens)Destroy(lens.gameObject);if(epoch!=presentationEpoch||!view)yield break;view.Reveal=answer.Last().cells;view.Refresh();AnimationBusy=false;SaveBoard();
        }
        IEnumerator LensArrival(PieceData piece,Vector2 pos,int epoch,bool permanent)
        {
            var ring=Skin.Rect("Source lens arrival ring",Effects,new Vector2(.5f,.5f),pos,Vector2.one*BoardView.CellSize*.7f);var g=ring.gameObject.AddComponent<SourceCircleRing>();g.color=new Color32(245,166,35,255);g.raycastTarget=false;
            StartCoroutine(SourceMotion.Tween(.460f,t=>{if(ring){ring.localScale=Vector3.one*Mathf.Lerp(.5f,1.45f,t);var c=g.color;c.a=t<.15f?t/.15f:1-(t-.15f)/.85f;g.color=c;}},SourceMotion.OutQuad));
            yield return SourceClock.Wait(.090f);if(epoch!=presentationEpoch||!ring||!BoardView)yield break;
            SoundBank.Play("hint_sparkle"); // Original showPick: each newly revealed lens badge.
            float radius=Mathf.Clamp(BoardView.CellSize/D*.24f,9,19)*D;var badge=Skin.Rect("Source revealed "+piece.shape,Effects,new Vector2(.5f,.5f),pos,Vector2.one*radius*2);Skin.RoundBox(badge,new Vector2(.5f,.5f),Vector2.zero,badge.sizeDelta,new Color32(255,248,220,255),radius);
            var shape=Shapes.Shapes[piece.shape];int rows=shape.cells.Max(x=>x.r)-shape.cells.Min(x=>x.r)+1,cols=shape.cells.Max(x=>x.c)-shape.cells.Min(x=>x.c)+1;float cs=radius*1.38f/Mathf.Max(rows,cols),gap=cs*.12f;var glyph=Skin.Rect("Source mini shape",badge,new Vector2(.5f,.5f),Vector2.zero,new Vector2(cols*cs,rows*cs));foreach(var c in shape.cells)Skin.RoundBox(glyph,new Vector2(0,1),new Vector2((c.c-shape.cells.Min(x=>x.c)+.5f)*cs,-(c.r-shape.cells.Min(x=>x.r)+.5f)*cs),Vector2.one*(cs-gap),Skin.Ink,cs*.16f);
            var cg=badge.gameObject.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;float born=SourceClock.Now;
            while(epoch==presentationEpoch&&badge&&BoardView){float t=SourceClock.Now-born;badge.localScale=Vector3.one*SourceMotion.Spring(t,0,1,90,5);if(!permanent){cg.alpha=t<.190f?1:1-Mathf.Clamp01((t-.190f)/.090f);if(t>=.280f)break;}else if(piece.cells.Any(c=>Board.At(c)!=null))break;yield return null;}
            if(badge)Destroy(badge.gameObject);if(ring)Destroy(ring.gameObject);
        }
    }
    public class SourceCircleRing:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/2,w=2*Skin.DP;for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;Vector2 v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));vh.AddVert(rectTransform.rect.center+v*r,color,Vector2.zero);vh.AddVert(rectTransform.rect.center+v*(r-w),color,Vector2.zero);if(i>0){int k=i*2;vh.AddTriangle(k-2,k-1,k+1);vh.AddTriangle(k-2,k+1,k);}}}
    }
}
