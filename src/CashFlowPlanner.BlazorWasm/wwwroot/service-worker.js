// Development service worker.
//
// Deliberately does nothing: caching during `dotnet run` makes every edit look
// like it did not apply. The real one is service-worker.published.js, which the
// Blazor SDK substitutes for this file on `dotnet publish`.
//
// It still registers so the registration path itself is exercised in dev.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => { /* pass through to the network */ });
