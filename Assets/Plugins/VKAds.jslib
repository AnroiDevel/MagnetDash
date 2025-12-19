mergeInto(LibraryManager.library, {
    VK_Log: function (msgPtr) {
        try {
            console.log(UTF8ToString(msgPtr));
        } catch (e) { }
    },

    VK_HasBridge: function () {
        return (typeof vkBridge !== 'undefined' && vkBridge && typeof vkBridge.send === 'function') ? 1 : 0;
    },

    VK_GetUrl: function () {
        var s = '';
        try { s = window.location.href || ''; } catch (e) { }
        var len = lengthBytesUTF8(s) + 1;
        var ptr = _malloc(len);
        stringToUTF8(s, ptr, len);
        return ptr;
    },

    VK_PreloadRewardedAd: function (goNamePtr, methodPtr) {
        var goName = UTF8ToString(goNamePtr);
        var method = UTF8ToString(methodPtr);

        vkBridge.send('VKWebAppCheckNativeAds', { ad_format: 'reward' })
            .then(function (res) {
                var ok = (res && res.result) ? '1' : '0';
                SendMessage(goName, method, ok);
            })
            .catch(function () {
                SendMessage(goName, method, '0');
            });
    },

    VK_ShowRewardedAd: function (goNamePtr, methodSuccessPtr, methodFailPtr) {
        var goName = UTF8ToString(goNamePtr);
        var methodSuccess = UTF8ToString(methodSuccessPtr);
        var methodFail = UTF8ToString(methodFailPtr);

        console.log('[VK_ShowRewardedAd] called go=' + goName);

        if (typeof vkBridge === 'undefined') {
            console.warn('[VK_ShowRewardedAd] vkBridge is undefined');
            SendMessage(goName, methodFail, 'vkBridge_undefined');
            return;
        }

        // На всякий случай init (обычно вызывается в html один раз)
        try { vkBridge.send('VKWebAppInit'); } catch (e) { }

        // Таймаут, чтобы не зависнуть молча
        var finished = false;
        var timer = setTimeout(function () {
            if (finished) return;
            finished = true;
            console.warn('[VK_ShowRewardedAd] timeout');
            SendMessage(goName, methodFail, 'promise_timeout');
        }, 10000);

        vkBridge.send('VKWebAppShowNativeAds', { ad_format: 'reward' })
            .then(function (res) {
                if (finished) return;
                finished = true;
                clearTimeout(timer);

                console.log('[VK_ShowRewardedAd] success', res);

                if (res && res.result) SendMessage(goName, methodSuccess, '');
                else SendMessage(goName, methodFail, 'not_shown');
            })
            .catch(function (err) {
                if (finished) return;
                finished = true;
                clearTimeout(timer);

                console.warn('[VK_ShowRewardedAd] error', err);

                var msg = 'unknown_error';
                try {
                    if (err && err.error_data && err.error_data.error_reason) msg = err.error_data.error_reason;
                    else msg = JSON.stringify(err);
                } catch (e) { }

                SendMessage(goName, methodFail, msg);
            });
    }
});
