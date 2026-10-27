using System;
using Inkomino;
using UnityEngine;

// Checks source/configuration and SDK callback signatures without invoking
// WeChat, creating native buttons, building a player or exporting a package.
public static class GameClubBuildChecks
{
    public static void BuildPreview()
    {
        Verify();
        BuildTools.BuildWindows();
    }

    public static void BuildWeChatPlayer()
    {
        Verify();
        CloudSaveBuildChecks.BuildWeChatPlayer();
    }

    public static WeChatWASM.ShowOption ShowOption(Action<int> done)
    {
        return new WeChatWASM.ShowOption
        {
            openlink = "configuration-only-not-a-real-openlink",
            success = result => done(0),
            fail = result => done(result == null ? 0 : result.errCode),
            complete = result => { }
        };
    }

    public static void Verify()
    {
        var asset = Resources.Load<TextAsset>("Settings/game-club");
        if (!asset) throw new Exception("Missing game-club configuration");
        var config = JsonUtility.FromJson<GameClubConfig>(asset.text);
        if (config == null) throw new Exception("Invalid game-club configuration");
        var options = GameClubEntry.NativeOptions(new RectInt(20, 40, 140, 55));
        if (options.type != WeChatWASM.GameClubButtonType.text || options.openlink != null ||
            options.text != "" || options.style.width != 140 || options.style.height != 55 ||
            options.style.borderWidth != 0)
            throw new Exception("Native home game-club button options differ");
        if (ShowOption(_ => { }).fail == null) throw new Exception("Missing PageManager callback shape");

        // Root canvas pixels and logical native pixels can differ by DPR.
        RectInt mapped;
        if (!GameClubEntry.TryMapRect(new Rect(240, 900, 480, 150), new Rect(0, 0, 1080, 2400),
            new Vector2(360, 800), out mapped) || !mapped.Equals(new RectInt(80, 450, 160, 50)))
            throw new Exception("Game-club hit target DPR conversion differs");
        if (!GameClubEntry.TryMapRect(new Rect(-300, 700, 200, 100), new Rect(-540, -1200, 1080, 2400),
            new Vector2(360, 800), out mapped) || !mapped.Equals(new RectInt(80, 133, 67, 34)))
            throw new Exception("Game-club hit target centered canvas conversion differs");
        if (GameClubEntry.TryMapRect(new Rect(1200, 50, 100, 100), new Rect(0, 0, 1080, 2400),
            new Vector2(360, 800), out mapped))
            throw new Exception("Offscreen game-club hit target remained active");
        Debug.Log("INKOMINO_GAMECLUB_CONFIG_CHECK_PASS enabled=" + config.enabled +
                  " mode=" + (string.IsNullOrWhiteSpace(config.openlink) ? "native_home" : "page_manager"));
    }
}
