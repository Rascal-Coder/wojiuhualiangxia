using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Profile;

public static class BuildWeChat
{
    public const string AppId="wx3a628e8b48216b63";
    [MenuItem("我就划两下/导出微信小游戏")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath,"../QA/WeChat"));
        WeChatArtPacking.Recover();PrepareCompatibilityPlugins();BuildTools.Prepare();BuildTools.Validate();AudioBuildQA.Run();
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/WeChat.asset");
        if(!profile)throw new Exception("Missing WeChat build profile; run BuildWeChat.Configure first.");
        profile.buildPath=Path.Combine(root,"Builds/WeChat");
        string json="{\"ProjectConf\":{\"projectName\":\"我就划两下\",\"Appid\":\""+AppId+"\",\"CDN\":\"\",\"assetLoadType\":1,\"compressDataPackage\":true,\"Orientation\":0,\"MemorySize\":256,\"HideAfterCallMain\":true},\"CompileOptions\":{\"DevelopBuild\":false,\"AutoProfile\":false,\"Il2CppOptimizeSize\":true,\"profilingFuncs\":false,\"Webgl2\":true,\"enableiOSMetal\":false,\"enableEmscriptenGLX\":false,\"enableRenderThread\":false,\"autoAdaptScreen\":false,\"showMonitorSuggestModal\":false,\"enableProfileStats\":false,\"brotliMT\":false},\"SDKOptions\":{\"UseFriendRelation\":true,\"UseCompressedTexture\":false,\"UseMiniGameChat\":false,\"PreloadWXFont\":false},\"PlayerPrefsKeys\":[\"inkomino-profile-v1\"]}";
        JsonUtility.FromJsonOverwrite(json,profile.miniGameSettings);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.MiniGame,ScriptingImplementation.IL2CPP);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.MiniGame,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.MiniGame,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(root,"QA/WeChat/build-settings.json"),JsonUtility.ToJson(profile.miniGameSettings,true));
        Debug.Log("INKOMINO_WECHAT_BUILD_START appid="+AppId);
        BuildMiniGameError result;
        using(WeChatArtPacking.Create(root))result=BuildPipeline.BuildMiniGame(profile);
        File.WriteAllText(Path.Combine(root,"QA/WeChat/build-result.json"),"{\"appid\":\""+AppId+"\",\"engine\":\""+Application.unityVersion+"\",\"result\":\""+result+"\"}");
        if(result!=BuildMiniGameError.Succeeded)throw new Exception("WeChat build failed: "+result);
        WeChatLoadingAssets.Apply(root);WeChatLoadingAssets.Validate(root);WeChatFriendsAssets.Apply(root);
        ValidateExport();
        Debug.Log("INKOMINO_WECHAT_BUILD_SUCCEEDED "+profile.buildPath);
    }
    static void PrepareCompatibilityPlugins()
    {
        foreach(string path in new[]{"Assets/Plugins/WeChatProfileStorage.jslib","Assets/Plugins/WeChatWebGLCompatibility.jslib","Assets/Plugins/WeChatSafeLayout.jslib"})
        {
            var importer=AssetImporter.GetAtPath(path) as PluginImporter;
            if(!importer)throw new Exception("Missing platform compatibility plugin: "+path);
            importer.SetCompatibleWithAnyPlatform(false);importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.WebGL,true);importer.SetCompatibleWithPlatform(BuildTarget.MiniGame,true);
            importer.SaveAndReimport();
        }
    }
    public static void ValidateExport()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),output=Path.Combine(root,"Builds/WeChat/minigame");
        string data=Path.Combine(output,"data-package");
        if(!Directory.Exists(data)||!Directory.GetFiles(data,"*.br").Any(p=>new FileInfo(p).Length>0))throw new Exception("Data subpackage missing; CDN fallback is not a complete offline export.");
        if(!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(Path.Combine(output,"game.js")),@"loadDataPackageFromSubpackage\s*:\s*true"))throw new Exception("Unexpected CDN fallback.");
        if(!File.ReadAllText(Path.Combine(output,"project.config.json")).Contains(AppId))throw new Exception("Export AppID mismatch.");
        long bytes=Directory.GetFiles(output,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".symbols.unityweb")&&!p.EndsWith(".symbols.unityweb.br")&&!p.Replace('\\','/').Contains("/.plugincache/")).Sum(p=>new FileInfo(p).Length);
        if(bytes>=30L*1024*1024)throw new Exception("MiniGame package exceeds 30MiB budget.");
        Debug.Log("INKOMINO_WECHAT_EXPORT_CHECK_PASS bytes="+bytes);
    }
    public static void VerifyExisting()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath,"../QA/WeChat"));
        WeChatArtPacking.Recover();BuildTools.Validate();AudioBuildQA.Run();ValidateExport();
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        File.WriteAllText(Path.Combine(root,"QA/WeChat/reopen-validation.json"),"{\"status\":\"PASS\",\"appid\":\""+AppId+"\",\"levels\":500,\"export_guard\":true}");
    }
    public static void Configure()
    {
        BuildTools.Prepare();
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));Directory.CreateDirectory(Path.Combine(root,"QA/WeChat"));Directory.CreateDirectory("Assets/Settings/Build Profiles");
        var creator=typeof(BuildProfile).GetMethod("CreateInstance",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(BuildTarget),typeof(MiniGameBuildSubtarget)},null);
        var profile=(BuildProfile)creator.Invoke(null,new object[]{BuildTarget.MiniGame,MiniGameBuildSubtarget.WeChat});
        profile.name="WeChat-wx3a628e8b48216b63";profile.buildPath=Path.Combine(root,"Builds/WeChat");
        string path="Assets/Settings/Build Profiles/WeChat.asset";
        if(AssetDatabase.LoadAssetAtPath<BuildProfile>(path))AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(profile,path);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(root,"QA/WeChat/settings-template.json"),JsonUtility.ToJson(profile.miniGameSettings,true));
        File.WriteAllText(Path.Combine(root,"QA/WeChat/platform-template.json"),JsonUtility.ToJson(profile.platformSettings,true));
        foreach(string name in new[]{"WeChatWASM.WXProjectConf","WeChatWASM.SDKOptions","WeChatWASM.CompileOptions","WeChatWASM.FontOptions"}){
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
            if(type!=null)File.WriteAllText(Path.Combine(root,"QA/WeChat/"+type.Name+".txt"),string.Join("\n",type.GetFields(BindingFlags.Instance|BindingFlags.Public).Select(f=>f.FieldType+" "+f.Name)));
        }
        Debug.Log("INKOMINO_WECHAT_CONFIGURED settings="+profile.miniGameSettings?.GetType().FullName);
    }
    public static void Inspect()
    {
        string Describe(Type type){return type.FullName+"\n"+string.Join("\n",type.GetMembers(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.MemberType==MemberTypes.Field||m.MemberType==MemberTypes.Property||m.MemberType==MemberTypes.Method).Select(m=>m.ToString()));}
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));Directory.CreateDirectory(Path.Combine(root,"QA/WeChat"));
        File.WriteAllText(Path.Combine(root,"QA/WeChat/editor-api.txt"),"SUBTARGETS="+string.Join(",",Enum.GetNames(typeof(MiniGameBuildSubtarget)))+"\n"+Describe(typeof(BuildProfile))+"\n"+Describe(typeof(MiniGameSettings)));
        Debug.Log("INKOMINO_WECHAT_API_READY");
    }
}
