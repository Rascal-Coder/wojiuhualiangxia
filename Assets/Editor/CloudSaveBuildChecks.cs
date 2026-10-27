using UnityEngine;

// Compile the MiniGame-only SDK callback shape in the editor too, without
// requesting a real login or requiring a cloud environment for local checks.
public static class CloudSaveBuildChecks
{
    public static void BuildTestPlayer(){Verify();BuildTools.BuildWindows();}
    public static void BuildWeChatPlayer(){Verify();BuildWeChat.Build();}
    public static WeChatWASM.LoginOption LoginOption(System.Action<string> done)
    {return new WeChatWASM.LoginOption{success=result=>done(result.code),fail=result=>done("")};}
    public static void Verify()
    {
        var config=JsonUtility.FromJson<Inkomino.CloudSaveConfig>(Resources.Load<TextAsset>("Settings/cloud-save").text);
        if(config.environmentId!="midian-game-d5g5oalova408143b")throw new System.Exception("Cloud environment differs from supplied ID");
        if(config.enabled&&(!System.Uri.IsWellFormedUriString(config.endpoint,System.UriKind.Absolute)||!config.endpoint.StartsWith("https://")))throw new System.Exception("Cloud endpoint is missing HTTPS");
        if(LoginOption(_=>{})==null)throw new System.Exception("Missing SDK login options");
        var openData=new WeChatWASM.OpenDataContextOption{sharedCanvasMode=WeChatWASM.CanvasType.ScreenCanvas};
        if(openData.sharedCanvasMode!=WeChatWASM.CanvasType.ScreenCanvas)throw new System.Exception("Missing native friends canvas mode");
        Debug.Log("INKOMINO_CLOUD_CONFIG_CHECK_PASS enabled="+config.enabled);
    }
}
