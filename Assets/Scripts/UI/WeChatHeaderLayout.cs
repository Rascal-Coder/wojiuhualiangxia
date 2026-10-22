using UnityEngine;

namespace Inkomino
{
    // Move just the top HUD row; the portrait stage and bottom controls retain
    // their original size. A gameplay board yields space only at its top edge.
    [UnityEngine.Scripting.Preserve]
    public sealed class WeChatHeaderLayout:MonoBehaviour
    {
        struct Item {public RectTransform rect;public Vector2 position;}
        readonly System.Collections.Generic.List<Item> items=new System.Collections.Generic.List<Item>();
        RectTransform parent,board;Vector2 boardPosition,boardSize;
        public float Offset {get;private set;}
#if WEIXINMINIGAME && !UNITY_EDITOR
        float reportedOffset=-1;
#endif
        public void Track(RectTransform rect){if(!rect)return;parent=(RectTransform)rect.parent;items.Add(new Item{rect=rect,position=rect.anchoredPosition});}
        public void TrackChildren(RectTransform root,int start){for(int i=start;i<root.childCount;i++)Track(root.GetChild(i) as RectTransform);}
        public void SetBoard(RectTransform rect){board=rect;boardPosition=rect.anchoredPosition;boardSize=rect.sizeDelta;}
        void OnApplicationFocus(bool focused){if(focused)WeChatSafeLayout.Invalidate();}
        [UnityEngine.Scripting.Preserve]
        void LateUpdate()
        {
            if(!parent)return;
            float reserved=WeChatSafeLayout.ReservedTopFraction();
            var canvas=parent.GetComponentInParent<Canvas>();if(!canvas)return;
            var viewport=canvas.rootCanvas.transform as RectTransform;if(!viewport)return;
            // The WeChat plugin can expose logical canvas coordinates while
            // Screen.height reports backing pixels. Compare two points in the
            // same canvas/world space rather than mixing those pixel systems.
            var safeEdge=viewport.TransformPoint(new Vector3(0,viewport.rect.yMax-viewport.rect.height*reserved,0));
            var point=parent.TransformPoint(new Vector3(0,parent.rect.yMax-139,0));
            float scale=Mathf.Max(.0001f,parent.lossyScale.y);
            float offset=Mathf.Clamp((point.y-safeEdge.y)/scale,0,500);
            if(reserved<=0)offset=0;Offset=offset;
#if WEIXINMINIGAME && !UNITY_EDITOR
            if(WeChatSafeLayout.Ready&&Mathf.Abs(reportedOffset-offset)>.5f){reportedOffset=offset;Debug.Log("INKOMINO_SAFE_HUD offset="+offset+" reserved_fraction="+reserved+" canvas_height="+viewport.rect.height);}
#endif
            foreach(var item in items)if(item.rect)item.rect.anchoredPosition=item.position+Vector2.down*offset;
            if(board){board.anchoredPosition=boardPosition+Vector2.down*(offset/2);board.sizeDelta=new Vector2(boardSize.x,Mathf.Max(500,boardSize.y-offset));}
        }
    }
}
