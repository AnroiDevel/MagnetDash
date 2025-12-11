mergeInto(LibraryManager.library, {
    VK_ShowRewardedAd: function (goNamePtr, methodSuccessPtr, methodFailPtr) {
        var goName = UTF8ToString(goNamePtr);
        var methodSuccess = UTF8ToString(methodSuccessPtr);
        var methodFail = UTF8ToString(methodFailPtr);

        if (typeof vkBridge === 'undefined') {
            console.warn('[VK_ShowRewardedAd] vkBridge is not defined');
            SendMessage(goName, methodFail, '');
            return;
        }

        vkBridge.send('VKWebAppShowNativeAds', { ad_format: 'reward' })
            .then(function (res) {
                // res.result === true, если показ успешен
                SendMessage(goName, methodSuccess, '');
            })
            .catch(function (err) {
                console.warn('[VK_ShowRewardedAd] error:', err);
                SendMessage(goName, methodFail, '');
            });
    }
});
