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
        Vector2 BoosterOrigin(int i){return new Vector2((i-1)*322,218+8+65-1200);}
        public void UseBooster(int index)
        {
            if(!InputEnabled||index<0||index>2)return;int unlock=index==0?3:index==1?7:10;
            if(CurrentLevel<unlock){SetMessage(Zh.Unlock(unlock));return;}
            int count=index==0?Store.Data.hint:index==1?Store.Data.freeze:Store.Data.lens;
            if(count<=0){BoosterShop(index);return;}
            var slot=Content.Find("Sticker booster tray/Source booster "+index) as RectTransform;if(slot)MenuMotion.Sparkle(slot,5,false);
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
        int freezeFxToken;
        IEnumerator SourceFreeze(int epoch)
        {
            int token=++freezeFxToken;frozen=10;TimerChanged();
            var root=Skin.Fill("Sticker freeze effect",Effects);var cg=root.gameObject.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;
            Vector2 destination=new Vector2(0,1200-201-(headerLayout?headerLayout.Offset:0));
            var drink=MenuUI.Art("menu_freeze",root,Vector2.one*.5f,BoosterOrigin(1),Vector2.one*175);
            var swoosh=MenuUI.Art("fx_cooling",root,Vector2.one*.5f,BoosterOrigin(1),Vector2.one*200);swoosh.transform.SetAsFirstSibling();
            var flakes=new List<RectTransform>();var flakeImages=new List<Image>();
            for(int i=0;i<4;i++){var f=MenuUI.Art(i%2==0?"fx_ice":"fx_glint",root,Vector2.one*.5f,BoosterOrigin(1),Vector2.one*(i%2==0?76:58));flakes.Add(f.rectTransform);flakeImages.Add(f);}
            float travel=MenuMotion.Reduced?.10f:.48f;
            yield return SourceMotion.Tween(travel,t=>{
                if(!root||epoch!=presentationEpoch)return;
                Vector2 p=Vector2.Lerp(BoosterOrigin(1),destination,SourceMotion.OutCubic(t))+Vector2.right*(Mathf.Sin(Mathf.PI*t)*155);
                drink.rectTransform.anchoredPosition=p;drink.rectTransform.localRotation=Quaternion.Euler(0,0,-25*Mathf.Sin(t*Mathf.PI));drink.rectTransform.localScale=Vector3.one*Mathf.Lerp(1,.7f,t);
                swoosh.rectTransform.anchoredPosition=p+new Vector2(0,-65);swoosh.color=new Color(1,1,1,Mathf.Sin(t*Mathf.PI)*.7f);
                for(int i=0;i<flakes.Count;i++){float u=Mathf.Clamp01(t-i*.035f);flakes[i].anchoredPosition=Vector2.Lerp(BoosterOrigin(1),destination,SourceMotion.OutCubic(u))+new Vector2(Mathf.Sin(u*Mathf.PI)*155+Mathf.Sin(i*2.4f)*32,-i*7*(1-u));flakeImages[i].color=new Color(1,1,1,Mathf.Sin(u*Mathf.PI));}
            });
            if(!root||epoch!=presentationEpoch)yield break;Destroy(drink.gameObject);Destroy(swoosh.gameObject);
            var frame=MenuUI.Art("fx_frost_frame",root,Vector2.one*.5f,destination,new Vector2(378,190));
            float born=SourceClock.Now;
            while(root&&epoch==presentationEpoch&&token==freezeFxToken&&frozen>0)
            {
                float t=SourceClock.Now-born;frame.color=new Color(1,1,1,Mathf.Min(1,t/.15f));
                for(int i=0;i<flakes.Count;i++){float side=i<2?-1:1;flakes[i].anchoredPosition=destination+new Vector2(side*(218-15*(i%2)),(i%2-.5f)*83);flakes[i].localRotation=Quaternion.Euler(0,0,MenuMotion.Reduced?0:Mathf.Sin(t*1.4f+i)*8);flakeImages[i].color=Color.white;}
                yield return null;
            }
            if(!root||epoch!=presentationEpoch)yield break;
            var origins=flakes.Select(f=>f.anchoredPosition).ToArray();
            yield return SourceMotion.Tween(MenuMotion.Reduced?.1f:.30f,t=>{if(!root)return;frame.color=new Color(1,1,1,1-t);for(int i=0;i<flakes.Count;i++){flakes[i].anchoredPosition=origins[i]+new Vector2((i<2?-1:1)*55*t,-85*t*t);flakes[i].localScale=Vector3.one*(1-.5f*t);flakeImages[i].color=new Color(1,1,1,1-t);}});
            if(root)Destroy(root.gameObject);TimerChanged();
        }
        // Centripetal Catmull-Rom, alpha .5, sampled ten times per source segment.
        static Vector2 Catmull(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {float t0=0,t1=t0+Mathf.Sqrt(Vector2.Distance(a,b))+.0001f,t2=t1+Mathf.Sqrt(Vector2.Distance(b,c))+.0001f,t3=t2+Mathf.Sqrt(Vector2.Distance(c,d))+.0001f,u=Mathf.Lerp(t1,t2,t);Vector2 A1=((t1-u)*a+(u-t0)*b)/(t1-t0),A2=((t2-u)*b+(u-t1)*c)/(t2-t1),A3=((t3-u)*c+(u-t2)*d)/(t3-t2),B1=((t2-u)*A1+(u-t0)*A2)/(t2-t0),B2=((t3-u)*A2+(u-t1)*A3)/(t3-t1);return ((t2-u)*B1+(u-t1)*B2)/(t2-t1);}
        IEnumerator SourceLens(PieceData[] answer,int epoch)
        {
            var view=BoardView;var layer=Skin.Fill("Sticker lens effect",Effects);var cg=layer.gameObject.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;
            var lens=MenuUI.Art("menu_lens",layer,Vector2.one*.5f,BoosterOrigin(2),Vector2.one*190);var glow=MenuUI.Art("fx_glint",layer,Vector2.one*.5f,BoosterOrigin(2),Vector2.one*100);
            var bounds=view.VisibleBoundsLocal;Vector2 Point(Vector2 p){var c=Effects.GetComponentInParent<Canvas>();return EffectPoint(RectTransformUtility.WorldToScreenPoint(c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,view.transform.TransformPoint(p)));}
            Vector2 scanTop=Point(new Vector2(bounds.center.x,bounds.yMax)),scanBottom=Point(new Vector2(bounds.center.x,bounds.yMin));
            yield return SourceMotion.Tween(MenuMotion.Reduced?.1f:.28f,t=>{if(lens)lens.rectTransform.anchoredPosition=Vector2.Lerp(BoosterOrigin(2),scanTop,SourceMotion.OutCubic(t));});
            if(!view||epoch!=presentationEpoch||!layer)yield break;
            yield return SourceMotion.Tween(MenuMotion.Reduced?.1f:.65f,t=>{if(!view||!lens||epoch!=presentationEpoch)return;view.SetHintPresentation(null,0,t);lens.rectTransform.anchoredPosition=Vector2.Lerp(scanTop,scanBottom,t)+Vector2.right*Mathf.Sin(t*Mathf.PI*2)*view.CellSize*.35f;lens.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*Mathf.PI*2)*10);glow.rectTransform.anchoredPosition=lens.rectTransform.anchoredPosition+new Vector2(-60,45);glow.color=new Color(1,1,1,.7f);});
            foreach(var piece in answer)
            {
                if(!view||epoch!=presentationEpoch||!layer)yield break;var from=lens.rectTransform.anchoredPosition;Vector2 destination=Vector2.zero;foreach(int cell in piece.cells)destination+=EffectCell(cell);destination/=piece.cells.Length;
                yield return SourceMotion.Tween(MenuMotion.Reduced?.1f:.24f,t=>{if(lens)lens.rectTransform.anchoredPosition=Vector2.Lerp(from,destination,SourceMotion.OutCubic(t));if(glow)glow.rectTransform.anchoredPosition=lens.rectTransform.anchoredPosition+new Vector2(-60,45);});
                if(!view||epoch!=presentationEpoch||!layer)yield break;SoundBank.Play("hint_sparkle");
                yield return SourceMotion.Tween(.20f,t=>{if(!view||!lens)return;view.SetHintPresentation(piece.cells,SourceMotion.OutQuad(t));lens.rectTransform.localScale=Vector3.one*(1+.08f*Mathf.Sin(Mathf.PI*t));glow.rectTransform.localScale=Vector3.one*(.7f+.5f*Mathf.Sin(Mathf.PI*t));});
            }
            if(!view||epoch!=presentationEpoch||!layer)yield break;view.Reveal=answer.Last().cells;view.SetHintPresentation(view.Reveal,1);
            yield return SourceMotion.Tween(.18f,t=>{if(cg)cg.alpha=1-t;});if(layer)Destroy(layer.gameObject);
            if(epoch==presentationEpoch&&view){AnimationBusy=false;SaveBoard();}
        }

    }
    public class SourceCircleRing:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/2,w=2*Skin.DP;for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;Vector2 v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));vh.AddVert(rectTransform.rect.center+v*r,color,Vector2.zero);vh.AddVert(rectTransform.rect.center+v*(r-w),color,Vector2.zero);if(i>0){int k=i*2;vh.AddTriangle(k-2,k-1,k+1);vh.AddTriangle(k-2,k+1,k);}}}
    }
}
