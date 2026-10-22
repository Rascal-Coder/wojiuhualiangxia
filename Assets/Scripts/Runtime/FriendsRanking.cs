using System;
using UnityEngine;

namespace Inkomino
{
    // Only aggregate progress crosses into the open-data context. Friend data
    // stays there and is drawn directly onto sharedCanvas.
    public static class FriendsRanking
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        static WeChatWASM.WXOpenDataContext context;
        static WeChatWASM.WXOpenDataContext Context {get{if(context==null)context=WeChatWASM.WX.GetOpenDataContext();return context;}}
#endif
        [Serializable] sealed class Message {public string type;public int score,delta;}
        public static void SubmitProgress(int count){Send("score:update",Mathf.Clamp(count,0,500));}
        public static void Send(string type,int score=0,int delta=0)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            try{Context.PostMessage(JsonUtility.ToJson(new Message{type=type,score=score,delta=delta}));}
            catch(Exception error){Debug.LogWarning("好友排行暂不可用："+error.Message);}
#endif
        }
        public static void OpenSettings()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            WeChatWASM.WX.OpenSetting(new WeChatWASM.OpenSettingOption{});
#endif
        }
    }
}
