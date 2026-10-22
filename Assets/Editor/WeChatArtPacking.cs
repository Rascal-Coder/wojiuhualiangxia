using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Inkomino;

// Originals remain in the source project. Only the build uses encoded image
// resources; no resizing, lossy re-encoding or GPU texture compression is applied.
public sealed class WeChatArtPacking:IDisposable
{
    const string Art="Assets/Resources/Art",Backup="Assets/Editor/WeChatOriginalArt",Packed="Assets/Resources/WeChatArt";
    [Serializable] public class Entry {public string key,sha256;public int bytes,width,height;}
    [Serializable] public class Manifest {public string encoding="Original PNG/JPG bytes, no resizing or re-encoding";public int selected,excluded;public Entry[] entries;}
    public static void Recover()
    {
        if(AssetDatabase.IsValidFolder(Backup))
        {
            if(AssetDatabase.IsValidFolder(Art))throw new Exception("Both original and backup art folders exist; refusing to overwrite assets.");
            string error=AssetDatabase.MoveAsset(Backup,Art);if(error!="")throw new Exception(error);
        }
        if(AssetDatabase.IsValidFolder(Packed)&&!AssetDatabase.DeleteAsset(Packed))throw new Exception("Cannot clean generated WeChatArt.");
    }
    public static WeChatArtPacking Create(string root)
    {
        var scope=new WeChatArtPacking();
        try{scope.Begin(root);return scope;}catch{scope.Dispose();throw;}
    }
    void Begin(string root)
    {
        Recover();
        string code=string.Join("\n",new[]{"Runtime","UI"}.SelectMany(d=>Directory.GetFiles("Assets/Scripts/"+d,"*.cs")).Select(File.ReadAllText));
        var literal=new HashSet<string>(Regex.Matches(code,"\"((?:shell_src_ui_|game_assets_|source_loading_|zh_loading_)[^\"\\r\\n]*)\"").Cast<Match>().Select(m=>m.Groups[1].Value));
        foreach(string key in new[]{"shell_src_ui_soundicon","shell_src_ui_musicicon","shell_src_ui_hapticsicon"})literal.Add(key);
        var sources=Directory.GetFiles(Art).Where(p=>p.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected=sources.Where(p=>!Path.GetFileNameWithoutExtension(p).StartsWith("shell_src_ui_")||literal.Contains(Path.GetFileNameWithoutExtension(p))||Path.GetFileNameWithoutExtension(p).StartsWith("shell_src_ui_avatars_avatar_")).ToArray();
        Directory.CreateDirectory(Packed);var entries=new List<Entry>();
        foreach(string source in selected)
        {
            byte[] bytes=File.ReadAllBytes(source);string key=Path.GetFileNameWithoutExtension(source);
            var texture=SourceArt.Decode(key,bytes);if(!texture)throw new Exception("Image decode failed: "+key);
            var entry=new Entry{key=key,bytes=bytes.Length,width=texture.width,height=texture.height};
            UnityEngine.Object.DestroyImmediate(texture);
            using(var hash=SHA256.Create())entry.sha256=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            File.WriteAllBytes(Packed+"/"+key+".bytes",bytes);entries.Add(entry);
        }
        File.WriteAllText(Path.Combine(root,"QA/WeChat/art-packing.json"),JsonUtility.ToJson(new Manifest{selected=selected.Length,excluded=sources.Length-selected.Length,entries=entries.ToArray()},true));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string moveError=AssetDatabase.MoveAsset(Art,Backup);if(moveError!="")throw new Exception(moveError);
        AssetDatabase.SaveAssets();Debug.Log("INKOMINO_WECHAT_ART selected="+selected.Length+" original_bytes="+entries.Sum(e=>(long)e.bytes)+" decoded_all=true");
    }
    public void Dispose(){Recover();AssetDatabase.SaveAssets();}
}
