/* ---------------------------------------------------------------
   sheet.js — a folha de sprites como uma lista de quadros: ler as
   imagens escolhidas (PNG, BMP, GIF animado), montar uma folha nova
   com todos os quadros do mesmo tamanho (centrados, os pés na linha
   de baixo) e trocar quadros da folha do pet.
   --------------------------------------------------------------- */
import { decodeGif } from "./gif.js";
import { frameCanvas } from "./files.js";

/** Uma tela deste tamanho. */
export function blank(w, h) {
  const c = document.createElement("canvas");
  c.width = Math.max(1, w); c.height = Math.max(1, h);
  return c;
}

/** Imagens com números no nome ficam na ordem dos números: frame_2 antes de frame_10. */
export function inOrder(files) {
  const key = (f) => f.name.replace(/\.[^.]+$/, "").replace(/\d+/g, (m) => m.padStart(8, "0")).toLowerCase();
  return [...files].sort((a, b) => (key(a) < key(b) ? -1 : key(a) > key(b) ? 1 : 0));
}

/** Os quadros dos arquivos escolhidos, na ordem: um GIF dá todos os quadros dele. Cada quadro é uma tela. */
export async function loadFrames(files) {
  const frames = [];
  for (const f of inOrder(files)) {
    const bytes = new Uint8Array(await f.arrayBuffer());
    const isGif = bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46;
    if (isGif) {
      const gif = decodeGif(bytes);
      for (const fr of gif.frames) {
        const c = blank(gif.width, gif.height);
        c.getContext("2d").putImageData(new ImageData(fr.rgba, gif.width, gif.height), 0, 0);
        frames.push(c);
      }
      if (gif.frames.length) continue;
    }
    const url = URL.createObjectURL(new Blob([bytes]));
    try {
      const img = await new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = () => rej(new Error(f.name)); i.src = url; });
      const c = blank(img.naturalWidth, img.naturalHeight);
      c.getContext("2d").drawImage(img, 0, 0);
      frames.push(c);
    } finally { URL.revokeObjectURL(url); }
  }
  return frames;
}

/** Uma folha nova com estes quadros: { png (base64), cols, rows, w, h }. Cada quadro centrado, os pés embaixo. */
export function buildSheet(frames, cols) {
  const w = Math.max(...frames.map((f) => f.width)), h = Math.max(...frames.map((f) => f.height));
  cols = Math.max(1, Math.min(cols, frames.length));
  const rows = Math.ceil(frames.length / cols);
  const sheet = blank(cols * w, rows * h);
  const g = sheet.getContext("2d");
  g.imageSmoothingEnabled = false;
  frames.forEach((f, i) => g.drawImage(f, (i % cols) * w + Math.floor((w - f.width) / 2), Math.floor(i / cols) * h + (h - f.height)));
  return { png: sheet.toDataURL("image/png").split(",")[1], cols, rows, w, h };
}

/** Todos os quadros do pet, como telas (com a cor transparente já tirada). */
export function petFrames(doc) {
  const out = [];
  for (let i = 0; i < doc.frameCount; i++) out.push(frameCanvas(doc, i));
  return out;
}

/** Uma tela espelhada (o desenho olhando para o outro lado). */
export function mirrored(c) {
  const m = blank(c.width, c.height);
  const g = m.getContext("2d");
  g.translate(c.width, 0); g.scale(-1, 1); g.drawImage(c, 0, 0);
  return m;
}

/** Troca cores exatas (#rrggbbaa → [r,g,b,a]) numa tela. */
export function recolor(c, map) {
  if (!map.size) return;
  const g = c.getContext("2d");
  const data = g.getImageData(0, 0, c.width, c.height);
  const d = data.data;
  for (let i = 0; i < d.length; i += 4) {
    const to = map.get(keyOf(d[i], d[i + 1], d[i + 2], d[i + 3]));
    if (to) { d[i] = to[0]; d[i + 1] = to[1]; d[i + 2] = to[2]; d[i + 3] = to[3]; }
  }
  g.putImageData(data, 0, 0);
}
export const keyOf = (r, g, b, a) => ((r << 24) | (g << 16) | (b << 8) | a) >>> 0;

/**
 * Grava os quadros de volta na folha do pet (mesma grade): a imagem nova tem fundo transparente de verdade, e a cor
 * transparente antiga (Magenta) continua valendo para quem ainda a usa.
 */
export function writeFrames(doc, frames, cols = doc.tilesX) {
  const built = buildSheet(frames, cols);
  return built;
}
