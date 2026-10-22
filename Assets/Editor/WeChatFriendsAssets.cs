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
        // Replace the SDK demo completely, including its random score uploader.
        if(Directory.Exists(target))Directory.Delete(target,true);
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root,"Assets/Editor/WeChatFriendsOpenData.js.txt"),Path.Combine(target,"index.js"));
        string config=File.ReadAllText(Path.Combine(output,"game.json"));
        if(!config.Contains("\"open-data\""))throw new Exception("SDK friend relation export is disabled");
        var json=LitJson.JsonMapper.ToObject(config);
        if(json.ContainsKey("plugins"))((System.Collections.IDictionary)json["plugins"]).Remove("Layout");
        File.WriteAllText(Path.Combine(output,"game.json"),LitJson.JsonMapper.ToJson(json));
        Debug.Log("INKOMINO_FRIENDS_EXPORT_PASS open-data/index.js no_server=true");
    }
}
