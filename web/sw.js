// Guarda os arquivos do editor neste navegador: ele abre e funciona sem internet (a galeria e os links precisam dela).
// Ao publicar uma versão nova, mude VERSION: os arquivos antigos saem e os novos entram.
const VERSION = "deskpals-editor-2";
const FILES = [
  "./",
  "./index.html",
  "./icon-192.png",
  "./icon-512.png",
  "./icon.png",
  "./manifest.webmanifest",
  "./css/editor.css",
  "./js/app.js",
  "./js/behaviors.js",
  "./js/dialogs.js",
  "./js/doc.js",
  "./js/files.js",
  "./js/gif.js",
  "./js/i18n.js",
  "./js/map.js",
  "./js/pet-engine.js",
  "./js/pixel.js",
  "./js/player.js",
  "./js/sheet.js",
  "./js/sources.js",
  "./js/texts.js",
  "./js/ui.js",
  "./js/validator.js"
];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(VERSION).then((c) => c.addAll(FILES)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (e) => {
  e.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== VERSION).map((k) => caches.delete(k)))).then(() => self.clients.claim()));
});

// os arquivos do editor: da rede quando há (sempre a versão mais nova), do que foi guardado quando não há
self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (e.request.method !== "GET" || url.origin !== self.location.origin) return;
  e.respondWith(
    fetch(e.request).then((r) => {
      if (r.ok) { const copy = r.clone(); caches.open(VERSION).then((c) => c.put(e.request, copy)); }
      return r;
    }).catch(() => caches.match(e.request, { ignoreSearch: true }).then((r) => r || caches.match("./index.html")))
  );
});