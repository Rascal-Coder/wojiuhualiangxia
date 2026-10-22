using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Inkomino;

public static class AudioBuildQA
{
    public static void BuildWindows(){Run();BuildTools.BuildWindows();}
    [Serializable] public sealed class OriginalFile { public string name,file,sha256;public long bytes; }
    [Serializable] public sealed class Inventory { public int count; public long bytes; public OriginalFile[] files; }
    [Serializable] public sealed class EntryResult { public string key,resource,sha256; public float length; public int channels,frequency; public bool decoded,hashMatches; }
    [Serializable] public sealed class Result { public string status; public int catalogCount,inventoryCount,decodedCount,hashCount; public long originalBytes; public EntryResult[] entries; public string[] errors; }
    [MenuItem("我就划两下/验证原版音频")]
    public static void Run()
    {
        var errors=new List<string>();var rows=new List<EntryResult>();var report=new Result();string root=Directory.GetParent(Application.dataPath).FullName;string outDir=Path.Combine(root,"QA/Audio");Directory.CreateDirectory(outDir);
        try
        {
            var inventory=JsonUtility.FromJson<Inventory>(File.ReadAllText(Path.Combine(outDir,"original-audio-inventory.json")));var data=Resources.Load<TextAsset>("Data/original-audio");if(!data)throw new InvalidOperationException("Missing original audio catalog");
            var catalog=JsonUtility.FromJson<OriginalAudioCatalog>(data.text);report.catalogCount=catalog.entries.Length;report.inventoryCount=inventory.files.Length;
            if(report.catalogCount!=83||inventory.count!=83||report.inventoryCount!=83)errors.Add("Catalog and inventory must each contain 83 original MP3 assets");
            var keys=new HashSet<string>();var matched=new HashSet<string>();
            foreach(var entry in catalog.entries)
            {
                var row=new EntryResult{key=entry.key,resource=entry.resource};rows.Add(row);if(!keys.Add(entry.key))errors.Add("Duplicate catalog key "+entry.key);
                var clip=Resources.Load<AudioClip>(entry.resource);row.decoded=clip&&clip.length>0&&clip.channels>0;if(clip){row.length=clip.length;row.channels=clip.channels;row.frequency=clip.frequency;}if(row.decoded)report.decodedCount++;else errors.Add("Failed AudioClip decode "+entry.key);
                string asset="Assets/Resources/"+entry.resource+".mp3";var source=inventory.files.FirstOrDefault(x=>x.file.Replace('\\','/')==asset);
                if(source==null){errors.Add("No inventory provenance for "+asset);continue;}matched.Add(source.file);
                string path=Path.Combine(root,asset);if(!File.Exists(path)){errors.Add("Missing MP3 "+asset);continue;}
                using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())row.sha256=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                long size=new FileInfo(path).Length;report.originalBytes+=size;row.hashMatches=row.sha256.Equals(source.sha256,StringComparison.OrdinalIgnoreCase)&&size==source.bytes;if(row.hashMatches)report.hashCount++;else errors.Add("Original bytes differ for "+asset);
            }
            if(matched.Count!=83)errors.Add("Not every inventory asset has exactly one catalog mapping");if(report.originalBytes!=inventory.bytes)errors.Add("Original aggregate byte count differs");
        }
        catch(Exception ex){errors.Add(ex.ToString());}
        report.status=errors.Count==0?"PASS":"FAIL";report.entries=rows.ToArray();report.errors=errors.ToArray();File.WriteAllText(Path.Combine(outDir,"editor-audio-validation.json"),JsonUtility.ToJson(report,true));Debug.Log("INKOMINO_EDITOR_AUDIO_QA "+report.status+" assets="+report.decodedCount+" originalHashes="+report.hashCount);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
    }
}
