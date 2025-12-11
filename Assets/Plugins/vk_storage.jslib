mergeInto(LibraryManager.library, {
    VK_SaveString: function (keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);

        vkBridge.send('VKWebAppStorageSet', {
            key: key,
            value: value
        }).catch(function (e) {
            console.warn('[VK_SaveString] error', e);
        });
    },

    VK_LoadString: function (keyPtr, goNamePtr, methodPtr) {
        var key = UTF8ToString(keyPtr);
        var goName = UTF8ToString(goNamePtr);
        var method = UTF8ToString(methodPtr);

        vkBridge.send('VKWebAppStorageGet', {
            keys: [key]
        }).then(function (res) {
            var val = '';
            if (res.keys && res.keys.length > 0) {
                val = res.keys[0].value || '';
            }
            SendMessage(goName, method, val);
        }).catch(function (e) {
            console.warn('[VK_LoadString] error', e);
            SendMessage(goName, method, '');
        });
    }
});
