using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR || WEIXINMINIGAME
using WeChatWASM;
#endif

namespace Inkomino
{
    [Serializable]
    public sealed class GameClubConfig
    {
        public bool enabled = true;
        public string openlink = "";
    }

    // The native button is only a transparent hit target. Its visible artwork,
    // typeface, focus target and layout remain the normal Unity home button.
    [DefaultExecutionOrder(1000)]
    [UnityEngine.Scripting.Preserve]
    public sealed class GameClubEntry : MonoBehaviour
    {
        const float OpenTimeout = 12f;
        InkominoApp app;
        Action<string> failure;
        RectTransform target;
        Button unityButton;
        Canvas canvas;
        GameClubConfig config;
        readonly Vector3[] corners = new Vector3[4];
        bool initialized, paused, unfocused, busy;
        int requestVersion;
        float deadline;

#if WEIXINMINIGAME && !UNITY_EDITOR
        WXGameClubButton nativeButton;
        WXPageManager pageManager;
        bool nativeAttempted, nativeShown, hasNativeRect;
        RectInt nativeRect;
        Vector2 logicalWindow;
        int lastScreenWidth, lastScreenHeight;
        float nextWindowRetry;
        bool windowFailureLogged;
#endif

        public bool Enabled { get { return config != null && config.enabled; } }
        public bool UsesOpenlink { get { return config != null && !string.IsNullOrWhiteSpace(config.openlink); } }
        public bool IsOpening { get { return busy; } }

        public void Initialize(InkominoApp owner, Action<string> onFailure)
        {
            app = owner;
            failure = onFailure;
            target = transform as RectTransform;
            unityButton = GetComponent<Button>();
            canvas = GetComponentInParent<Canvas>();
            var asset = Resources.Load<TextAsset>("Settings/game-club");
            try { config = asset ? JsonUtility.FromJson<GameClubConfig>(asset.text) : new GameClubConfig(); }
            catch (Exception error)
            {
                config = new GameClubConfig();
                Debug.LogWarning("[INK_GAMECLUB] config invalid type=" + error.GetType().Name);
            }
            if (config == null) config = new GameClubConfig();
            config.openlink = (config.openlink ?? "").Trim();
            initialized = true;
            RefreshVisibility();
        }

        public void Open()
        {
            if (!CanShow() || busy) return;
            if (!Enabled)
            {
                NotifyFailure("游戏圈暂未开放，请稍后再来");
                return;
            }
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!UsesOpenlink)
            {
                // A normal Unity click cannot programmatically open the native
                // game-club button. Only the user's tap on its overlay does so.
                RefreshVisibility();
                if (nativeButton == null) NotifyFailure("暂时无法打开游戏圈，请更新微信后重试");
                return;
            }
            int version = ++requestVersion;
            busy = true;
            deadline = Time.realtimeSinceStartup + OpenTimeout;
            try
            {
                if (!WX.CanIUse("CreatePageManager"))
                {
                    FinishFailure(version, "请更新微信后再打开游戏圈", "unsupported", 0);
                    return;
                }
                if (pageManager == null) pageManager = WX.CreatePageManager();
                pageManager.Show(new ShowOption
                {
                    openlink = config.openlink,
                    success = _ => FinishSuccess(version),
                    fail = result => FinishFailure(version, "暂时无法打开游戏圈，请稍后再试", "show", result == null ? 0 : result.errCode),
                    complete = _ => FinishSuccess(version)
                });
            }
            catch (Exception error)
            {
                Debug.LogWarning("[INK_GAMECLUB] show exception type=" + error.GetType().Name);
                FinishFailure(version, "暂时无法打开游戏圈，请稍后再试", "exception", 0);
            }
#else
            NotifyFailure("请在微信小游戏中打开游戏圈");
#endif
        }

        bool CanShow()
        {
            return initialized && this && isActiveAndEnabled && app && app.IsHomeScreen &&
                   !app.Playing && !app.IsModalOpen && !app.TransitioningLevel &&
                   !paused && !unfocused && (!unityButton || unityButton.IsInteractable());
        }

        void NotifyFailure(string message)
        {
            if (CanShow()) failure?.Invoke(message);
        }

        void FinishSuccess(int version)
        {
            if (version != requestVersion || !busy) return;
            busy = false;
            deadline = 0;
        }

        void FinishFailure(int version, string message, string stage, int code)
        {
            if (version != requestVersion || !busy) return;
            busy = false;
            deadline = 0;
            ++requestVersion;
            Debug.LogWarning("[INK_GAMECLUB] open failed stage=" + stage + " code=" + code);
#if WEIXINMINIGAME && !UNITY_EDITOR
            DisposePageManager();
#endif
            NotifyFailure(message);
        }

        public void RefreshVisibility()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            bool visible = CanShow() && Enabled && !UsesOpenlink;
            if (!visible)
            {
                SetNativeVisible(false);
                return;
            }
            if (!TryNativeRect(out var next))
            {
                SetNativeVisible(false);
                return;
            }
            if (nativeButton == null && !nativeAttempted)
            {
                nativeAttempted = true;
                try
                {
                    if (!WX.CanIUse("CreateGameClubButton")) return;
                    nativeButton = WX.CreateGameClubButton(NativeOptions(next));
                    if (nativeButton == null) return;
                    nativeButton.Hide();
                    nativeShown = false;
                    nativeRect = next;
                    hasNativeRect = true;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[INK_GAMECLUB] native create failed type=" + error.GetType().Name);
                    DisposeNative();
                    return;
                }
            }
            if (nativeButton == null) return;
            try
            {
                if (!hasNativeRect || !nativeRect.Equals(next))
                {
                    nativeButton.style.left = next.x;
                    nativeButton.style.top = next.y;
                    nativeButton.style.width = next.width;
                    nativeButton.style.height = next.height;
                    nativeRect = next;
                    hasNativeRect = true;
                }
                SetNativeVisible(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[INK_GAMECLUB] native layout failed type=" + error.GetType().Name);
                DisposeNative();
            }
#endif
        }

        [UnityEngine.Scripting.Preserve]
        void LateUpdate()
        {
            if (busy && Time.realtimeSinceStartup >= deadline)
                FinishFailure(requestVersion, "游戏圈打开超时，请稍后再试", "timeout", 0);
            // MenuMotion and the safe-area layout can move the Unity button.
            // Native writes happen only when the integer rect/visibility changes.
            RefreshVisibility();
        }

        void OnApplicationPause(bool value)
        {
            paused = value;
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!value) { logicalWindow = Vector2.zero; nextWindowRetry = 0; }
#endif
            RefreshVisibility();
        }

        void OnApplicationFocus(bool value)
        {
            unfocused = !value;
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (value) { logicalWindow = Vector2.zero; nextWindowRetry = 0; }
#endif
            RefreshVisibility();
        }

        void OnRectTransformDimensionsChange()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            logicalWindow = Vector2.zero;
#endif
        }

        void OnEnable()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            nativeAttempted = false;
#endif
            RefreshVisibility();
        }

        void OnDisable() { Dispose(); }
        void OnDestroy() { Dispose(); }

        void Dispose()
        {
            ++requestVersion;
            busy = false;
            deadline = 0;
#if WEIXINMINIGAME && !UNITY_EDITOR
            DisposeNative();
            DisposePageManager();
#endif
        }

        // Both rectangles are in the same root-canvas coordinate space. This
        // avoids multiplying logical canvas pixels by the device pixel ratio.
        public static bool TryMapRect(Rect bounds, Rect viewport, Vector2 window, out RectInt result)
        {
            result = default;
            if (viewport.width <= 0 || viewport.height <= 0 || window.x <= 0 || window.y <= 0 ||
                bounds.width <= 0 || bounds.height <= 0) return false;
            int left = Mathf.Clamp(Mathf.FloorToInt((bounds.xMin - viewport.xMin) / viewport.width * window.x), 0, Mathf.FloorToInt(window.x));
            int right = Mathf.Clamp(Mathf.CeilToInt((bounds.xMax - viewport.xMin) / viewport.width * window.x), 0, Mathf.FloorToInt(window.x));
            int top = Mathf.Clamp(Mathf.FloorToInt((viewport.yMax - bounds.yMax) / viewport.height * window.y), 0, Mathf.FloorToInt(window.y));
            int bottom = Mathf.Clamp(Mathf.CeilToInt((viewport.yMax - bounds.yMin) / viewport.height * window.y), 0, Mathf.FloorToInt(window.y));
            if (right <= left || bottom <= top) return false;
            result = new RectInt(left, top, right - left, bottom - top);
            return true;
        }

#if UNITY_EDITOR || WEIXINMINIGAME
        public static WXCreateGameClubButtonParam NativeOptions(RectInt rect)
        {
            return new WXCreateGameClubButtonParam
            {
                type = GameClubButtonType.text,
                text = "",
                // Null is omitted by the SDK; empty strings must not be passed
                // as an activity openlink when opening the game-club home.
                openlink = null,
                style = new GameClubButtonStyle
                {
                    left = rect.x, top = rect.y, width = rect.width, height = rect.height,
                    backgroundColor = "rgba(0,0,0,0)", borderColor = "rgba(0,0,0,0)",
                    color = "rgba(0,0,0,0)", borderWidth = 0, borderRadius = 0,
                    fontSize = 1, lineHeight = rect.height, textAlign = GameClubButtonTextAlign.center
                }
            };
        }
#endif

#if WEIXINMINIGAME && !UNITY_EDITOR
        bool TryNativeRect(out RectInt result)
        {
            result = default;
            if (!target || !canvas) return false;
            var viewport = canvas.rootCanvas.transform as RectTransform;
            if (!viewport) return false;
            if (logicalWindow.x <= 0 || logicalWindow.y <= 0 ||
                lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            {
                if (Time.realtimeSinceStartup < nextWindowRetry) return false;
                try
                {
                    var info = WX.GetWindowInfo();
                    logicalWindow = new Vector2((float)info.windowWidth, (float)info.windowHeight);
                    if (logicalWindow.x <= 0 || logicalWindow.y <= 0)
                    {
                        nextWindowRetry = Time.realtimeSinceStartup + 3f;
                        return false;
                    }
                    lastScreenWidth = Screen.width;
                    lastScreenHeight = Screen.height;
                    windowFailureLogged = false;
                }
                catch (Exception error)
                {
                    nextWindowRetry = Time.realtimeSinceStartup + 3f;
                    if (!windowFailureLogged) Debug.LogWarning("[INK_GAMECLUB] window unavailable type=" + error.GetType().Name);
                    windowFailureLogged = true;
                    return false;
                }
            }
            target.GetWorldCorners(corners);
            Vector3 first = viewport.InverseTransformPoint(corners[0]);
            var min = new Vector2(first.x, first.y);
            var max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 point = viewport.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, new Vector2(point.x, point.y));
                max = Vector2.Max(max, new Vector2(point.x, point.y));
            }
            return TryMapRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y), viewport.rect, logicalWindow, out result);
        }

        void SetNativeVisible(bool value)
        {
            if (nativeButton == null || nativeShown == value) return;
            try
            {
                if (value) nativeButton.Show();
                else nativeButton.Hide();
                nativeShown = value;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[INK_GAMECLUB] native visibility failed type=" + error.GetType().Name);
                DisposeNative();
            }
        }

        void DisposeNative()
        {
            var old = nativeButton;
            nativeButton = null;
            nativeShown = false;
            hasNativeRect = false;
            if (old == null) return;
            try { old.Hide(); }
            catch (Exception error) { Debug.LogWarning("[INK_GAMECLUB] native hide failed type=" + error.GetType().Name); }
            try { old.Destroy(); }
            catch (Exception error) { Debug.LogWarning("[INK_GAMECLUB] native dispose failed type=" + error.GetType().Name); }
        }

        void DisposePageManager()
        {
            var old = pageManager;
            pageManager = null;
            if (old == null) return;
            try { old.Destroy(); }
            catch (Exception error) { Debug.LogWarning("[INK_GAMECLUB] page dispose failed type=" + error.GetType().Name); }
        }
#endif
    }
}
