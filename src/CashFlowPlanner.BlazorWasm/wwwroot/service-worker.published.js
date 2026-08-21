// Production service worker: makes the app installable and usable offline.
//
// The cache is keyed by the content hash the SDK writes into
// service-worker-assets.js, so a new deploy lands in a brand-new cache and the
// previous one is deleted on activate. That is what keeps this from becoming a
// stale-build trap: nothing is served from an old cache once a new worker
// activates.
//
// Update timing, stated plainly: the browser checks for a new worker on load.
// A new one installs in the background and takes over on the NEXT load, so a
// deploy is visible one navigation later. We do not call skipWaiting() - doing
// so would swap the framework files underneath a running app, which for
// WebAssembly means mismatched .wasm and a hard failure mid-session.

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/,
    /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/,
    /\.webmanifest$/
];
const offlineAssetsExclude = [
    /^service-worker\.js$/,
    // Precompressed copies are never requested by the runtime and would double
    // the cache for no benefit.
    /\.br$/, /\.gz$/,
    // Source maps are debug-only weight.
    /\.map$/
];

// Derive the app root from the worker's own location rather than hardcoding '/'.
// The app is served from '/' locally but '/CashFlowPlanner/' on GitHub Pages, and
// only index.html gets its <base href> rewritten by CI - this file does not. The
// worker always sits at the app root, so './' relative to it is correct in both.
const baseUrl = new URL('./', self.location);

async function onInstall() {
    console.info('Service worker: installing', self.assetsManifest.version);

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
    console.info('Service worker: activating', self.assetsManifest.version);

    // Drop every cache from a previous deploy. Without this the origin
    // accumulates a full copy of the app per release.
    const keys = await caches.keys();
    await Promise.all(
        keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
            .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    // Serve index.html for any navigation so deep links such as /transactions
    // work offline and on a hard refresh - the same job 404.html does on
    // GitHub Pages when the worker is not yet installed.
    const shouldServeIndexHtml = event.request.mode === 'navigate';

    const request = shouldServeIndexHtml
        ? new Request(new URL('index.html', baseUrl).href)
        : event.request;

    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    // Anything not in the manifest (there is nothing external in this app, by
    // design) falls through to the network.
    return fetch(event.request);
}
