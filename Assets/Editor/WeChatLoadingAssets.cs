using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UnityEngine;

public static class WeChatLoadingAssets
{
    [Serializable] public sealed class ImageFile {public string key,source,cacheFile,sha256;public int bytes,width,height;}
    [Serializable] public sealed class ImageList {public ImageFile[] images;}
    public static void Apply(string root)
    {
        string output=Path.Combine(root,"Builds/WeChat/minigame"),images=Path.Combine(output,"images");
        Directory.CreateDirectory(images);
        // The background already contains the brand logo; the SDK icon is hidden.
        // Do not ship and cache another full logo for that unused image slot.
        string[] names={"background.jpg"},keys={"backgroundImage"};
        var files=new ImageFile[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            string source=Path.Combine(root,"Assets/WX-WASM-SDK-V2/Runtime/wechat-default/images",names[i]);
            byte[] bytes=File.ReadAllBytes(source);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!ImageConversion.LoadImage(texture,bytes)||texture.width<1||texture.height<1)throw new Exception("Invalid loading image: "+source);
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                files[i]=new ImageFile{key=keys[i],source="images/"+names[i],cacheFile="inkomino_boot_"+hash.Substring(0,16)+Path.GetExtension(names[i]),sha256=hash,bytes=bytes.Length,width=texture.width,height=texture.height};
                File.WriteAllBytes(Path.Combine(images,names[i]),bytes);
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        string unusedIcon=Path.Combine(images,"unity_logo.png");
        if(File.Exists(unusedIcon))File.Delete(unusedIcon);
        string list=JsonUtility.ToJson(new ImageList{images=files});
        // JsonUtility wraps arrays; retain just the JSON array for the JS module.
        string array=list.Substring(list.IndexOf('['),list.LastIndexOf(']')-list.IndexOf('[')+1);
        string module=File.ReadAllText(Path.Combine(root,"Assets/Editor/WeChatLoadingAssets.js.txt"));
        File.WriteAllText(Path.Combine(output,"inkomino-loading-assets.js"),module.Replace("$LOADING_IMAGE_FILES",array));
        string gamePath=Path.Combine(output,"game.js"),game=File.ReadAllText(gamePath);
        if(!game.Contains("from './inkomino-loading-assets'"))
        {
            game="import { prepareLoadingAssets } from './inkomino-loading-assets';\n"+game;
            const string assign="Object.assign(managerConfig, {";
            if(!game.Contains(assign))throw new Exception("SDK loading config changed; cover patch was not applied.");
            game=game.Replace(assign,"const loadingAssets = prepareLoadingAssets();\n        "+assign);
            game=ReplaceOne(game,@"backgroundImage:\s*'images/background\.jpg'","backgroundImage: loadingAssets.backgroundImage");
            game=ReplaceOne(game,@"iconImage:\s*'images/unity_logo\.png'","iconImage: loadingAssets.iconImage");
            game=game.Replace("firstStartText: '首次加载请耐心等待'","firstStartText: ''").Replace("正在加载资源","正在准备资源").Replace("编译中","正在准备资源").Replace("初始化中","正在启动游戏");
            game=ReplaceOne(game,@"iconConfig:\s*\{\s*visible:\s*true","iconConfig: { visible: false");
            File.WriteAllText(gamePath,game);
        }
        File.WriteAllText(Path.Combine(root,"QA/WeChat/loading-assets.json"),JsonUtility.ToJson(new ImageList{images=files},true));
        Debug.Log("INKOMINO_WECHAT_LOADING_IMAGES_READY images="+files.Length+" native_paths=true");
    }
    static string ReplaceOne(string text,string pattern,string replacement)
    {
        var regex=new Regex(pattern);if(regex.Matches(text).Count!=1)throw new Exception("Expected one SDK loading image reference: "+pattern);
        return regex.Replace(text,replacement,1);
    }
    public static void Validate(string root)
    {
        string output=Path.Combine(root,"Builds/WeChat/minigame");
        var manifest=JsonUtility.FromJson<ImageList>(File.ReadAllText(Path.Combine(root,"QA/WeChat/loading-assets.json")));
        foreach(var image in manifest.images)
        {
            string file=Path.Combine(output,image.source);if(!File.Exists(file)||new FileInfo(file).Length!=image.bytes)throw new Exception("Missing loading cover asset: "+image.source);
        }
        if(File.Exists(Path.Combine(output,"images/unity_logo.png")))throw new Exception("Unused hidden loading icon still packaged.");
        string game=File.ReadAllText(Path.Combine(output,"game.js"));
        if(!game.Contains("backgroundImage: loadingAssets.backgroundImage")||!game.Contains("iconImage: loadingAssets.iconImage"))throw new Exception("Native loading image paths not linked.");
        if(!Regex.IsMatch(game,@"iconConfig:\s*\{\s*visible:\s*false"))throw new Exception("Duplicate native loading icon must stay hidden.");
    }
}
