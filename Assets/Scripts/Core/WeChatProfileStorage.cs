using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Inkomino
{
    // Save through wx storage directly: it does not depend on browser IndexedDB
    // or the SDK's asynchronous write queue behind StorageSetStringSync.
    public static class WeChatProfileStorage
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string InkominoReadProfile(string key);
        [DllImport("__Internal")] static extern int InkominoWriteProfile(string key,string value);
        [Serializable] sealed class ReadResult{public bool ok;public string value,error;}
        public static string Read(string key)
        {
            var result=JsonUtility.FromJson<ReadResult>(InkominoReadProfile(key));
            if(result==null||!result.ok)throw new InvalidOperationException("微信本地存档读取失败："+(result==null?"无响应":result.error));
            return result.value??"";
        }
        public static void Write(string key,string value)
        {if(InkominoWriteProfile(key,value)!=1)throw new InvalidOperationException("微信本地存档写入失败，请检查存储空间后重试。");}
#endif
    }
}
