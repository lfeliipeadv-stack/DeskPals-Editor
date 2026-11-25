/* ---------------------------------------------------------------
   files.js — arquivos feitos no navegador, sem bibliotecas: um .zip
   (sem compressão: o pet já é quase todo base64), o ícone .ico com
   PNGs dentro (16, 32, 48 e 64 px) e downloads.
   --------------------------------------------------------------- */

const CRC = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c >>> 0;
  }
  return table;
})();
function crc32(bytes) {
  let c = 0xffffffff;
  for (let i = 0; i < bytes.length; i++) c = CRC[(c ^ bytes[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** Um .zip com estes arquivos ({ nome: Uint8Array | string }). */
export function makeZip(files) {
  const enc = new TextEncoder();
  const parts = [], central = [];
  let offset = 0;
  const now = new Date();
  const time = (now.getHours() << 11) | (now.getMinutes() << 5) | (now.getSeconds() >> 1);
  const date = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();
  for (const [name, content] of Object.entries(files)) {
    const data = typeof content === "string" ? enc.encode(content) : content;
    const nameBytes = enc.encode(name);
    const crc = crc32(data);
    const local = new DataView(new ArrayBuffer(30));
    local.setUint32(0, 0x04034b50, true); local.setUint16(4, 20, true); local.setUint16(6, 0x0800, true);   // UTF-8 names
    local.setUint16(8, 0, true); local.setUint16(10, time, true); local.setUint16(12, date, true);
    local.setUint32(14, crc, true); local.setUint32(18, data.length, true); local.setUint32(22, data.length, true);
    local.setUint16(26, nameBytes.length, true); local.setUint16(28, 0, true);
    parts.push(new Uint8Array(local.buffer), nameBytes, data);
    const cd = new DataView(new ArrayBuffer(46));
    cd.setUint32(0, 0x02014b50, true); cd.setUint16(4, 20, true); cd.setUint16(6, 20, true); cd.setUint16(8, 0x0800, true);
    cd.setUint16(10, 0, true); cd.setUint16(12, time, true); cd.setUint16(14, date, true);
    cd.setUint32(16, crc, true); cd.setUint32(20, data.length, true); cd.setUint32(24, data.length, true);
    cd.setUint16(28, nameBytes.length, true); cd.setUint32(42, offset, true);
    central.push(new Uint8Array(cd.buffer), nameBytes);
    offset += 30 + nameBytes.length + data.length;
  }
  const cdSize = central.reduce((n, p) => n + p.length, 0);
  const end = new DataView(new ArrayBuffer(22));
  end.setUint32(0, 0x06054b50, true);
  end.setUint16(8, Object.keys(files).length, true); end.setUint16(10, Object.keys(files).length, true);
  end.setUint32(12, cdSize, true); end.setUint32(16, offset, true);
  return new Blob([...parts, ...central, new Uint8Array(end.buffer)], { type: "application/zip" });
}

/** Baixa um arquivo com este nome. */
export function download(name, blobOrText, type = "application/xml") {
  const blob = blobOrText instanceof Blob ? blobOrText : new Blob([blobOrText], { type });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = name;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 4000);
}

export function base64ToBytes(b64) {
  const bin = atob(b64.replace(/\s+/g, ""));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}
export function bytesToBase64(bytes) {
  let s = "";
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

/** O desenho recortado (sem a borda transparente), numa tela quadrada deste tamanho, pixels nítidos. */
export function squareIcon(source, size) {
  const g0 = source.getContext("2d");
  const { width: w, height: h } = source;
  const d = g0.getImageData(0, 0, w, h).data;
  let x0 = w, y0 = h, x1 = -1, y1 = -1;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (d[(y * w + x) * 4 + 3] > 10) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
  if (x1 < 0) { x0 = 0; y0 = 0; x1 = w - 1; y1 = h - 1; }
  const cw = x1 - x0 + 1, ch = y1 - y0 + 1, side = Math.max(cw, ch);
  const c = document.createElement("canvas");
  c.width = c.height = size;
  const g = c.getContext("2d");
  g.imageSmoothingEnabled = side > size * 2;   // reduzir muito fica melhor suave; ampliar, nítido
  const scale = size / side;
  g.drawImage(source, x0, y0, cw, ch, (size - cw * scale) / 2, (size - ch * scale) / 2, cw * scale, ch * scale);
  return c;
}

async function canvasPng(c) {
  const blob = await new Promise((r) => c.toBlob(r, "image/png"));
  return new Uint8Array(await blob.arrayBuffer());
}

/** Um .ico (base64) com o desenho em 16, 32, 48 e 64 px, como o editor para Windows faz. */
export async function makeIco(source) {
  const sizes = [16, 32, 48, 64];
  const pngs = [];
  for (const s of sizes) pngs.push(await canvasPng(squareIcon(source, s)));
  const head = new DataView(new ArrayBuffer(6 + 16 * sizes.length));
  head.setUint16(2, 1, true); head.setUint16(4, sizes.length, true);
  let offset = 6 + 16 * sizes.length;
  sizes.forEach((s, i) => {
    const o = 6 + i * 16;
    head.setUint8(o, s); head.setUint8(o + 1, s); head.setUint16(o + 4, 1, true); head.setUint16(o + 6, 32, true);
    head.setUint32(o + 8, pngs[i].length, true); head.setUint32(o + 12, offset, true);
    offset += pngs[i].length;
  });
  const all = new Uint8Array(offset);
  all.set(new Uint8Array(head.buffer), 0);
  let p = 6 + 16 * sizes.length;
  for (const png of pngs) { all.set(png, p); p += png.length; }
  return bytesToBase64(all);
}

/** Um quadro da folha numa tela do tamanho dele. */
export function frameCanvas(doc, frame) {
  const c = document.createElement("canvas");
  c.width = Math.max(1, doc.frameW); c.height = Math.max(1, doc.frameH);
  doc.drawFrame(c.getContext("2d"), frame, 0, 0, c.width, c.height);
  return c;
}

/** Lê um arquivo do computador como texto ou como bytes. */
export const readText = (file) => file.text();
export const readBytes = async (file) => new Uint8Array(await file.arrayBuffer());

/** Uma imagem qualquer (PNG, GIF, BMP…) convertida em PNG base64. */
export async function imageFileToPng(file) {
  const url = URL.createObjectURL(file);
  try {
    const img = await new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = () => rej(new Error("image")); i.src = url; });
    const c = document.createElement("canvas");
    c.width = img.naturalWidth; c.height = img.naturalHeight;
    c.getContext("2d").drawImage(img, 0, 0);
    return { png: c.toDataURL("image/png").split(",")[1], width: c.width, height: c.height };
  } finally { URL.revokeObjectURL(url); }
}
