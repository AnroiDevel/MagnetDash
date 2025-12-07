mergeInto(LibraryManager.library, {
    VK_SaveString: function (keyPtr, valuePtr) {
        const key = UTF8ToString(keyPtr);
        const value = UTF8ToString(valuePtr);

        if (typeof vkBridge === "undefined") {
            console.warn("[VK_SaveString] vkBridge not found, fallback to localStorage");
            try {
                localStorage.setItem(key, value);
            } catch (e) {
                console.error(e);
            }
            return;
        }

        vkBridge.send("VKWebAppStorageSet", {
            key: key,
            value: value
        }).catch((e) => {
            console.error("[VK_SaveString] error", e);
        });
    },

    VK_LoadString: function (keyPtr, goPtr, methodPtr) {
        const key = UTF8ToString(keyPtr);
        const goName = UTF8ToString(goPtr);
        const method = UTF8ToString(methodPtr);

        function sendBack(val) {
            if (typeof val !== "string") val = "";
            SendMessage(goName, method, val);
        }

        if (typeof vkBridge === "undefined") {
            console.warn("[VK_LoadString] vkBridge not found, fallback to localStorage");
            try {
                const val = localStorage.getItem(key) || "";
                sendBack(val);
            } catch (e) {
                console.error(e);
                sendBack("");
            }
            return;
        }

        vkBridge
            .send("VKWebAppStorageGet", { keys: [key] })
            .then((data) => {
                let val = "";
                if (data && data.keys && data.keys.length > 0) {
                    val = data.keys[0].value || "";
                }
                sendBack(val);
            })
            .catch((e) => {
                console.error("[VK_LoadString] error", e);
                sendBack("");
            });
    }
});
