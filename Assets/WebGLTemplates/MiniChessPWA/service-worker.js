'use strict';

self.addEventListener('install', event => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
  event.waitUntil(self.clients.claim());
});

// Network only: do not retain older Unity loaders or large game binaries.
// Ordinary HTTP caching still follows the hosting server's headers.
self.addEventListener('fetch', event => {
  if (event.request.method === 'GET' &&
      new URL(event.request.url).origin === self.location.origin) {
    event.respondWith(fetch(event.request));
  }
});
