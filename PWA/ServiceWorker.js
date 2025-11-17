const cacheName = '"AnroiDevel-MagnetDash-1.0"';
const contentToCache = [
    "Build/PWA.loader.js",
    "Build/PWA.framework.js.unityweb",
    "Build/PWA.data.unityweb",
    "Build/PWA.wasm.unityweb",
    "TemplateData/style.css"
];

self.addEventListener('install', function (e) {
    console.log('[Service Worker] Install');
    e.waitUntil(
        caches.open(cacheName).then(function (cache) {
            console.log('[Service Worker] Caching all: app shell and content');
            return cache.addAll(contentToCache);
        })
    );
});

self.addEventListener('activate', function (e) {
    e.waitUntil(
        caches.keys().then(function (cacheNames) {
            return Promise.all(
                cacheNames.map(function (thisCacheName) {
                    if (thisCacheName !== cacheName) {
                        console.log('[Service Worker] Deleting old cache:', thisCacheName);
                        return caches.delete(thisCacheName);
                    }
                })
            );
        })
    );
    return self.clients.claim();
});

self.addEventListener('fetch', function (e) {
    e.respondWith(
        caches.open(cacheName).then(async function (cache) {
            const response = await fetch(e.request);
            if (response.status === 200 && response.type === 'basic') {
                console.log(`[Service Worker] Caching new resource: ${e.request.url}`);
                cache.put(e.request, response.clone());
            }
            return response;
        }).catch(function () {
            return caches.match(e.request);
        })
    );
});
