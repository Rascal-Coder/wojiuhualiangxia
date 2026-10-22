using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    // MiniGame ships the original encoded pixels instead of a large raw texture
    // resource. Cache decoded textures so sprites and board materials share them.
    public static class SourceArt
    {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static long MaximumDecodeMilliseconds{get;private set;}
        public static string SlowestDecodeKey{get;private set;}
        public static Texture2D Texture(string key)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            Texture2D texture;
            if(textures.TryGetValue(key,out texture))return texture;
            var bytes=Resources.Load<TextAsset>("WeChatArt/"+key);
            if(!bytes){Debug.LogError("Missing packaged art: "+key);return null;}
            texture=Decode(key,bytes.bytes);Resources.UnloadAsset(bytes);
            if(texture)textures[key]=texture;
            return texture;
#else
            return Resources.Load<Texture2D>("Art/"+key);
#endif
        }
        public static Texture2D Decode(string key,byte[] bytes)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            if(!ImageConversion.LoadImage(texture,bytes,true)){Object.Destroy(texture);return null;}
            texture.name=key;texture.filterMode=FilterMode.Bilinear;texture.anisoLevel=1;
            texture.wrapMode=key.Contains("facets")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            long elapsed=watch.ElapsedMilliseconds;if(elapsed>MaximumDecodeMilliseconds){MaximumDecodeMilliseconds=elapsed;SlowestDecodeKey=key;}
            return texture;
        }
    }
}
