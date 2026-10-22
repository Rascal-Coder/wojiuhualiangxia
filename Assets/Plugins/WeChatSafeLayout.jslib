mergeInto(LibraryManager.library, {
    InkominoReadLayoutMetrics: function () {
        var result = { ok: false };
        try {
            var info;
            try { info = wx.getWindowInfo(); } catch (ignored) { info = wx.getSystemInfoSync(); }
            var width = info.windowWidth, height = info.windowHeight;
            var safe = info.safeArea;
            var menu = null;
            try { menu = wx.getMenuButtonBoundingClientRect(); } catch (ignored) {}
            var measuredMenu = !!(menu && menu.width > 0 && menu.height > 0 && menu.bottom > 0 && menu.bottom < height * 0.35);
            // Some developer-tool/device runtimes expose the API but return {}.
            // Reserve a normal 32px capsule plus its 8px top inset in that case;
            // the C# layout adds its normal 8px gap below this reserved band.
            var statusTop = safe && safe.top >= 0 ? safe.top : (info.statusBarHeight || 0);
            var menuBottom = measuredMenu ? menu.bottom : Math.max(24, statusTop) + 40;
            result = {
                ok: width > 0 && height > 0,
                width: width, height: height,
                hasSafeArea: !!(safe && safe.right > safe.left && safe.bottom > safe.top),
                left: safe ? safe.left : 0, right: safe ? safe.right : width,
                top: safe ? safe.top : 0, bottom: safe ? safe.bottom : height,
                hasMenu: menuBottom > 0 && menuBottom < height * 0.35,
                menuBottom: menuBottom,
                menuEstimated: !measuredMenu
            };
        } catch (error) {
            // A missing native metric must not prevent a player from opening UI.
        }
        var json = JSON.stringify(result);
        var length = lengthBytesUTF8(json) + 1;
        var ptr = _malloc(length);
        stringToUTF8(json, ptr, length);
        return ptr;
    }
});
