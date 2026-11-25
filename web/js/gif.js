/* ---------------------------------------------------------------
   gif.js — GIF animado sem bibliotecas: ler (cada quadro já montado
   sobre os anteriores, como o GIF manda) e escrever (a prévia
   animada da galeria: fundo transparente, repete para sempre).
   --------------------------------------------------------------- */

/* ---------------------------------------------------------- ler */

/** Os quadros de um GIF: { width, height, frames: [{ rgba: Uint8ClampedArray, delay }] }. */
export function decodeGif(bytes) {
  const b = bytes;
  const sig = String.fromCharCode(...b.subarray(0, 6));
  if (sig !== "GIF89a" && sig !== "GIF87a") throw new Error("not a GIF");
  let p = 6;
  const u8 = () => b[p++];
  const u16 = () => { const v = b[p] | (b[p + 1] << 8); p += 2; return v; };
  const width = u16(), height = u16();
  const flags = u8(); u8(); u8();   // cor de fundo e proporção: não usadas
  let globalTable = null;
  if (flags & 0x80) { const n = 1 << ((flags & 7) + 1); globalTable = b.subarray(p, p + n * 3); p += n * 3; }
  const screen = new Uint8ClampedArray(width * height * 4);
  const frames = [];
  let transparent = -1, delay = 0, disposal = 0;
  while (p < b.length) {
    const block = u8();
    if (block === 0x21) {
      const label = u8();
      if (label === 0xf9) {
        u8();   // tamanho (4)
        const pf = u8();
        delay = u16() * 10;
        const ti = u8();
        transparent = pf & 1 ? ti : -1;
        disposal = (pf >> 2) & 7;
        u8();   // fim do bloco
      } else {
        for (let n = u8(); n !== 0; n = u8()) p += n;   // outras extensões: pula
      }
    } else if (block === 0x2c) {
      const x = u16(), y = u16(), w = u16(), hgt = u16();
      const pf = u8();
      let table = globalTable;
      if (pf & 0x80) { const n = 1 << ((pf & 7) + 1); table = b.subarray(p, p + n * 3); p += n * 3; }
      const interlaced = (pf & 0x40) !== 0;
      const minCode = u8();
      const parts = [];
      let total = 0;
      for (let n = u8(); n !== 0; n = u8()) { parts.push(b.subarray(p, p + n)); total += n; p += n; }
      const data = new Uint8Array(total);
      let o = 0;
      for (const part of parts) { data.set(part, o); o += part.length; }
      const pixels = lzwDecode(minCode, data, w * hgt);
      const before = disposal === 3 ? screen.slice() : null;
      const rows = interlaced ? interlaceOrder(hgt) : null;
      for (let r = 0; r < hgt; r++) {
        const yy = y + (rows ? rows[r] : r);
        if (yy >= height) continue;
        for (let c = 0; c < w; c++) {
          const xx = x + c;
          if (xx >= width) continue;
          const idx = pixels[r * w + c];
          if (idx === transparent || !table || idx * 3 + 2 >= table.length) continue;
          const q = (yy * width + xx) * 4;
          screen[q] = table[idx * 3]; screen[q + 1] = table[idx * 3 + 1]; screen[q + 2] = table[idx * 3 + 2]; screen[q + 3] = 255;
        }
      }
      frames.push({ rgba: screen.slice(), delay });
      // o que acontece com este quadro antes do próximo
      if (disposal === 2) {
        for (let yy = y; yy < Math.min(height, y + hgt); yy++) screen.fill(0, (yy * width + x) * 4, (yy * width + Math.min(width, x + w)) * 4);
      } else if (disposal === 3 && before) screen.set(before);
      transparent = -1; delay = 0; disposal = 0;
    } else break;   // 0x3b (fim) ou um arquivo cortado
  }
  return { width, height, frames };
}

/** A ordem das linhas de uma imagem entrelaçada (de 8 em 8, de 8 em 8 a partir da 4, de 4 em 4, de 2 em 2). */
function interlaceOrder(h) {
  const order = [];
  for (const [start, step] of [[0, 8], [4, 8], [2, 4], [1, 2]]) for (let y = start; y < h; y += step) order.push(y);
  return order;
}

function lzwDecode(minCode, data, count) {
  const out = new Uint8Array(count);
  const clear = 1 << minCode, eoi = clear + 1;
  const prefix = new Int16Array(4096), suffix = new Uint8Array(4096), first = new Uint8Array(4096), length = new Uint16Array(4096);
  for (let i = 0; i < clear; i++) { prefix[i] = -1; suffix[i] = i; first[i] = i; length[i] = 1; }
  let size = minCode + 1, mask = (1 << size) - 1, next = eoi + 1;
  let bits = 0, cur = 0, dp = 0, op = 0, prev = -1;
  const write = (code) => {
    const len = length[code];
    let k = code;
    for (let i = len - 1; i >= 0; i--) { if (op + i < count) out[op + i] = suffix[k]; k = prefix[k]; }
    op += len;
  };
  while (op < count) {
    while (bits < size) {
      if (dp >= data.length) return out;
      cur |= data[dp++] << bits;
      bits += 8;
    }
    const code = cur & mask;
    cur >>>= size; bits -= size;
    if (code === clear) { size = minCode + 1; mask = (1 << size) - 1; next = eoi + 1; prev = -1; continue; }
    if (code === eoi) break;
    if (prev === -1) { if (code < clear) { out[op++] = code; prev = code; } continue; }
    if (code < next) {
      write(code);
      if (next < 4096) { prefix[next] = prev; suffix[next] = first[code]; first[next] = first[prev]; length[next] = length[prev] + 1; next++; }
    } else {
      // o código que acaba de nascer: a cadeia anterior mais a primeira letra dela
      if (next < 4096) { prefix[next] = prev; suffix[next] = first[prev]; first[next] = first[prev]; length[next] = length[prev] + 1; next++; }
      write(Math.min(code, next - 1));
    }
    prev = code;
    if (next >= 1 << size && size < 12) { size++; mask = (1 << size) - 1; }
  }
  return out;
}

/* ---------------------------------------------------------- escrever */

/**
 * Um GIF animado (repete para sempre, fundo transparente) de telas do mesmo tamanho. Pixel art tem poucas cores:
 * até 255 ficam exatas; mais que isso são reduzidas tirando os bits mais baixos (como o editor para Windows).
 */
export function encodeGif(canvases, delayMs) {
  const w = canvases[0].width, h = canvases[0].height;
  const datas = canvases.map((c) => c.getContext("2d").getImageData(0, 0, w, h).data);
  let shift = 0, colors;
  for (;;) {
    colors = new Map();
    let full = false;
    for (const d of datas) {
      for (let i = 0; i < d.length && !full; i += 4) {
        if (d[i + 3] < 128) continue;
        const k = ((d[i] >> shift) << 16) | ((d[i + 1] >> shift) << 8) | (d[i + 2] >> shift);
        if (!colors.has(k)) { if (colors.size === 255) { full = true; break; } colors.set(k, colors.size + 1); }
      }
      if (full) break;
    }
    if (!full) break;
    shift++;
  }
  const out = [];
  const u8 = (v) => out.push(v & 255);
  const u16 = (v) => { out.push(v & 255, (v >> 8) & 255); };
  const str = (s) => { for (const ch of s) out.push(ch.charCodeAt(0)); };
  str("GIF89a"); u16(w); u16(h); u8(0xf7); u8(0); u8(0);   // tabela de 256 cores, índice 0 = transparente
  const table = new Uint8Array(256 * 3);
  for (const [k, i] of colors) { table[i * 3] = ((k >> 16) & 255) << shift; table[i * 3 + 1] = ((k >> 8) & 255) << shift; table[i * 3 + 2] = (k & 255) << shift; }
  for (const v of table) out.push(v);
  out.push(0x21, 0xff, 0x0b); str("NETSCAPE2.0"); out.push(3, 1, 0, 0, 0);   // repete para sempre
  for (const d of datas) {
    out.push(0x21, 0xf9, 4, 0x09); u16(Math.max(2, Math.round(delayMs / 10))); u8(0); u8(0);   // apaga o quadro antes do próximo; transparente = 0
    u8(0x2c); u16(0); u16(0); u16(w); u16(h); u8(0);
    const idx = new Uint8Array(w * h);
    for (let i = 0, j = 0; i < d.length; i += 4, j++)
      idx[j] = d[i + 3] < 128 ? 0 : colors.get(((d[i] >> shift) << 16) | ((d[i + 1] >> shift) << 8) | (d[i + 2] >> shift));
    lzwEncode(out, 8, idx);
  }
  u8(0x3b);
  return new Uint8Array(out);
}

/** LZW em blocos de até 255 bytes (o mesmo algoritmo do omggif, que os leitores de GIF aceitam). */
function lzwEncode(out, minCode, index) {
  out.push(minCode);
  let sub = out.length;
  out.push(0);   // tamanho do bloco, preenchido depois
  const clear = 1 << minCode, eoi = clear + 1;
  let next = eoi + 1, size = minCode + 1, cur = 0, shift = 0;
  let table = new Map();
  const flush = (min) => {
    while (shift >= min) {
      out.push(cur & 255);
      cur >>>= 8; shift -= 8;
      if (out.length === sub + 256) { out[sub] = 255; sub = out.length; out.push(0); }
    }
  };
  const emit = (code) => { cur |= code << shift; shift += size; flush(8); };
  let ib = index[0];
  emit(clear);
  for (let i = 1; i < index.length; i++) {
    const k = index[i];
    const key = (ib << 8) | k;
    const code = table.get(key);
    if (code === undefined) {
      emit(ib);
      if (next === 4096) {
        emit(clear);
        next = eoi + 1; size = minCode + 1; table = new Map();
      } else {
        if (next >= 1 << size) size++;
        table.set(key, next++);
      }
      ib = k;
    } else ib = code;
  }
  emit(ib);
  emit(eoi);
  flush(1);
  if (sub + 1 === out.length) out[sub] = 0;
  else { out[sub] = out.length - sub - 1; out.push(0); }
}
