using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    [RequireComponent(typeof(RawImage))]
    public sealed class FriendsRankView:MonoBehaviour
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        Texture2D texture;Rect last;bool shown;readonly Vector3[] corners=new Vector3[4];
        void Start(){FriendsRanking.Send("friends:prepare");Resize();FriendsRanking.Send("friends:show");}
        void LateUpdate(){Resize();}
        void Resize()
        {
            var root=GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            ((RectTransform)transform).GetWorldCorners(corners);
            var a=root.InverseTransformPoint(corners[0]);var b=root.InverseTransformPoint(corners[2]);
            var bounds=root.rect;
            var rect=new Rect(Mathf.Round((a.x-bounds.xMin)/bounds.width*Screen.width),Mathf.Round((bounds.yMax-b.y)/bounds.height*Screen.height),Mathf.Max(1,Mathf.Round((b.x-a.x)/bounds.width*Screen.width)),Mathf.Max(1,Mathf.Round((b.y-a.y)/bounds.height*Screen.height)));
            if(shown&&rect==last)return;
            if(shown)WeChatWASM.WX.HideOpenData();
            if(texture)Destroy(texture);
            texture=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGBA32,false);texture.Apply();
            var image=GetComponent<RawImage>();image.texture=texture;image.uvRect=new Rect(0,1,1,-1);
            WeChatWASM.WX.ShowOpenData(texture,(int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);last=rect;shown=true;
            Debug.Log("INKOMINO_FRIENDS_SURFACE friends-20261022-r2 size="+rect.width+"x"+rect.height);
        }
        void OnApplicationFocus(bool focus){if(focus&&shown)FriendsRanking.Send("friends:refresh");}
        void OnDisable(){if(shown){WeChatWASM.WX.HideOpenData();FriendsRanking.Send("friends:hide");shown=false;}if(texture){Destroy(texture);texture=null;}}
#endif
    }
}
