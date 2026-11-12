'use strict';

// Keep scope relative so project Pages deployments stay under /MiniChess_Unity/.
if ('serviceWorker' in navigator && window.isSecureContext) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('./service-worker.js', {
      scope: './',
      updateViaCache: 'none'
    }).catch(error => console.warn('MiniChess PWA registration failed:', error));
  });
}
