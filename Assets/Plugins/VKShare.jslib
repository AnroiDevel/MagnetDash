mergeInto(LibraryManager.library, {
    // Определение платформы: VK / OK
    VK_RequestPlatform: function (goNamePtr) {
        var goName = UTF8ToString(goNamePtr);

        if (typeof vkBridge === 'undefined') {
            console.warn('[VKPlatform] vkBridge is not defined');
            SendMessage(goName, 'OnPlatform', 'unknown');
            return;
        }

        vkBridge.send('VKWebAppGetClientVersion', {})
            .then(function (info) {
                // В ОК приходит app: "ok", в VK – "vk"
                var tag = (info && (info.app || info.environment)) || '';
                var platform = (tag === 'ok') ? 'ok' : 'vk';
                console.log('[VKPlatform] platform =', platform);
                SendMessage(goName, 'OnPlatform', platform);
            })
            .catch(function (err) {
                console.warn('[VKPlatform] error:', err);
                SendMessage(goName, 'OnPlatform', 'unknown');
            });
    },

    // Социальное действие в VK: InviteBox
    VK_SocialAction: function () {
        if (typeof vkBridge === 'undefined') {
            console.warn('[Social] vkBridge is not defined');
            return;
        }

        vkBridge.send('VKWebAppShowInviteBox', {
            message: "\u042f \u0438\u0433\u0440\u0430\u044e \u0432 Magnet Dash! \u041f\u0440\u0438\u0441\u043e\u0435\u0434\u0438\u043d\u044f\u0439\u0441\u044f!"
        })
            .then(function (res) {
                console.log('[Social] VKWebAppShowInviteBox success:', res);
            })
            .catch(function (err) {
                console.warn('[Social] VKWebAppShowInviteBox error:', err);
            });
    }
});
