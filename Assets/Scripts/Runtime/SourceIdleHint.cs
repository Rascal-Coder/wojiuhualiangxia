using System.Linq;
using UnityEngine;

namespace Inkomino
{
    // Shipped module2147 IDLE_HINT and module2144 hintPiece/callback6:
    // levels1..10, idle10000ms, pulse800ms per half, solve budget60000.
    public sealed class SourceIdleHint:MonoBehaviour
    {
        InkominoApp app;PuzzleBoardView view;BoardState state;
        SourceTutorialHintGraphic hint;float idleSince,pulseSince;bool tried,initialized;
        public void Init(InkominoApp owner,PuzzleBoardView boardView)
        {
            Detach();app=owner;view=boardView;state=view?view.State:null;
            if(!app||!view||state==null){enabled=false;return;}
            var layer=Skin.Fill("Source idle hint (under pieces and symbols)",view.transform);
            hint=layer.gameObject.AddComponent<SourceTutorialHintGraphic>();hint.View=view;hint.raycastTarget=false;hint.enabled=false;
            // Source idle fill is below facets, walls, glyphs and numbered labels.
            // Insert after the opaque paper/grid layer, not above the board UI.
            var paper=view.GetComponentsInChildren<BoardMesh>(true).FirstOrDefault(m=>m.Layer==1&&m.transform.parent==view.transform);
            if(paper)layer.SetSiblingIndex(paper.transform.GetSiblingIndex());else layer.SetAsFirstSibling();
            state.Changed+=ResetIdle;initialized=true;enabled=true;ResetIdle();
        }
        void Update()
        {
            if(!initialized)return;
            if(!app||!view||!app.Playing||!ReferenceEquals(app.Board,state)||app.BoardView!=view){Detach();Destroy(this);return;}
            bool eligible=state.Level.id>0&&state.Level.id<=10&&!state.Solved&&app.InputEnabled&&app.SourceTutorialPhase==null&&view.Trail.Count==0;
            if(!eligible||Input.anyKey||Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2)){ResetIdle();return;}
            if(hint&&hint.enabled){hint.Pulse=Mathf.Max(0,SourceClock.Now-pulseSince);return;}
            if(tried||SourceClock.Now-idleSince<10)return;
            tried=true;
            // The source prefers a still-missing target shape among a valid
            // continuation, then takes the continuation's first piece.
            // Board.Solve uses the exported continuation when compatible and
            // bounded constraint search otherwise; it never changes this board.
            var continuation=state.Solve(60000);
            if(continuation==null||continuation.Count==0)return;
            var piece=continuation.FirstOrDefault(p=>!state.Pieces.Any(x=>x.shape==p.shape))??continuation[0];
            if(piece.cells==null||piece.cells.Length==0||piece.cells.Any(i=>!state.IsActive(i)||state.At(i)!=null))return;
            hint.Cells=piece.cells.ToArray();hint.Pulse=0;pulseSince=SourceClock.Now;hint.enabled=true;
        }
        void ResetIdle(){idleSince=SourceClock.Now;tried=false;if(hint){hint.enabled=false;hint.Cells=null;}}
        void Detach()
        {
            if(state!=null)state.Changed-=ResetIdle;
            if(hint){hint.gameObject.SetActive(false);Destroy(hint.gameObject);hint=null;}
            initialized=false;
        }
        void OnDestroy(){Detach();}
    }
}
