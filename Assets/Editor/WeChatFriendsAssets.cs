using System;
using System.IO;
using UnityEngine;

public static class WeChatFriendsAssets
{
    public static void Apply(string root)
    {
        string output=Path.GetFullPath(Path.Combine(root,"Builds/WeChat/minigame"));
        string target=Path.GetFullPath(Path.Combine(output,"open-data"));
        if(!target.StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe open-data output");
        string fonts=Path.Combine(root,"Assets/Editor/FriendsFonts");
        if(!File.Exists(Path.Combine(fonts,"friends-fonts.js")))throw new Exception("Missing friend ranking font assets; run Tools/build_friends_fonts.py");
        string art=Path.Combine(root,"Assets/Editor/FriendsArt");
        if(!File.Exists(Path.Combine(art,"friends-art.js"))||!File.Exists(Path.Combine(art,"friends-art.png")))throw new Exception("Missing friend ranking sticker assets; run Tools/build_friends_art.py");
        // Replace the SDK demo completely, including its random score uploader.
        if(Directory.Exists(target))Directory.Delete(target,true);
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root,"Assets/Editor/WeChatFriendsOpenData.js.txt"),Path.Combine(target,"index.js"));
        File.Copy(Path.Combine(root,"Assets/Editor/WeChatFriendsFont.js.txt"),Path.Combine(target,"font.js"));
        File.Copy(Path.Combine(root,"Assets/Editor/WeChatFriendsArt.js.txt"),Path.Combine(target,"art.js"));
        string artTarget=Path.Combine(target,"art");Directory.CreateDirectory(artTarget);
        File.Copy(Path.Combine(art,"friends-art.js"),Path.Combine(artTarget,"friends-art.js"));
        File.Copy(Path.Combine(art,"friends-art.png"),Path.Combine(artTarget,"friends-art.png"));
        string fontTarget=Path.Combine(target,"fonts");Directory.CreateDirectory(fontTarget);
        foreach(string file in Directory.GetFiles(fonts)){
            string extension=Path.GetExtension(file);
            if(extension==".png"||extension==".js"||extension==".txt")File.Copy(file,Path.Combine(fontTarget,Path.GetFileName(file)));
        }
        string config=File.ReadAllText(Path.Combine(output,"game.json"));
        if(!config.Contains("\"open-data\""))throw new Exception("SDK friend relation export is disabled");
        var json=LitJson.JsonMapper.ToObject(config);
        if(json.ContainsKey("plugins"))((System.Collections.IDictionary)json["plugins"]).Remove("Layout");
        File.WriteAllText(Path.Combine(output,"game.json"),LitJson.JsonMapper.ToJson(json));
        Debug.Log("INKOMINO_FRIENDS_EXPORT_PASS open-data/index.js no_server=true");
    }
}
