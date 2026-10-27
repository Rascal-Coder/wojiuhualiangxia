using System;
using System.Collections.Generic;
using System.IO;
using Inkomino;
using UnityEngine;

// Invoked with -batchmode -nographics -executeMethod
// CloudSaveReconciliationChecks.Run. No scene, build settings or user save edits.
public static class CloudSaveReconciliationChecks
{
    public static void Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"inkomino-cloud-qa-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);GameObject host=null;
        var failures=new List<string>();int checks=0;
        try{
            host=new GameObject("Isolated cloud reconciliation QA");host.SetActive(false);
            var app=host.AddComponent<InkominoApp>();app.IsQA=true;
            app.Store=new SaveStore(Path.Combine(directory,"profile.json"));
            CloudSaveQA.Run(app,(ok,message)=>{checks++;if(!ok){failures.Add(message);Debug.LogError("INKOMINO_CLOUD_QA_FAIL "+message);}else Debug.Log("INKOMINO_CLOUD_QA_PASS "+message);});
            if(failures.Count>0)throw new Exception("Cloud reconciliation QA failed "+failures.Count+" / "+checks+": "+string.Join("; ",failures));
            Debug.Log("INKOMINO_CLOUD_RECONCILIATION_PASS checks="+checks);
        }finally{
            if(host)UnityEngine.Object.DestroyImmediate(host);
            // This folder was created here with a fresh random ID. Delete only
            // files created by this isolated run, then remove the empty folder.
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
