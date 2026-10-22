mergeInto(LibraryManager.library, {
    InkominoReadProfile: function (keyPtr) {
        var result;
        try {
            var value = wx.getStorageSync(UTF8ToString(keyPtr));
            result = { ok: true, value: value == null || value === '' ? '' : (typeof value === 'string' ? value : JSON.stringify(value)) };
        } catch (error) {
            result = { ok: false, value: '', error: String(error && error.message || error) };
        }
        var json = JSON.stringify(result);
        var length = lengthBytesUTF8(json) + 1;
        var ptr = _malloc(length);
        stringToUTF8(json, ptr, length);
        return ptr;
    },
    InkominoWriteProfile: function (keyPtr, valuePtr) {
        try {
            wx.setStorageSync(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
            return 1;
        } catch (error) {
            console.error('[Inkomino] 本地存档写入失败：', String(error && error.message || error));
            return 0;
        }
    }
});
