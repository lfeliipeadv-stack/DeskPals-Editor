/* ---------------------------------------------------------------
   pixel.js — retoque de um quadro, pixel a pixel (o mesmo do Editor
   de Pets para Windows): lápis, balde, conta-gotas, trocar cor (neste
   quadro ou em todos), linha, retângulo, contorno, espelhar, mover um
   pixel, desfazer. Salvar grava o quadro de volta na folha.
   --------------------------------------------------------------- */
import { t } from "./i18n.js";
import { h, modal, toast } from "./ui.js";
import { petFrames, buildSheet, recolor, keyOf } from "./sheet.js";

const hex = (r, g, b) => "#" + [r, g, b].map((v) => v.toString(16).padStart(2, "0")).join("");

/** Abre o editor de pixels do quadro <frame>; onSaved() depois de gravar. */
export function pixelEditor(doc, frame, onSaved) {
  if (!doc.sheet) { toast(t("sheet.empty")); return; }
  const frames = petFrames(doc);
  const img = frames[frame];
  if (!img) return;
  const W = img.width, H = img.height;
  const ctx = img.getContext("2d", { willReadFrequently: true });
  let px = ctx.getImageData(0, 0, W, H);
  const undo = [];
  const swaps = [];   // trocas de cor para fazer nos outros quadros
  let color = [0, 0, 0, 255];
  let tool = "pen";
  let everywhere = false;

  const keep = () => { undo.push(new Uint8ClampedArray(px.data)); if (undo.length > 80) undo.shift(); };
  const get = (x, y) => { const i = (y * W + x) * 4, d = px.data; return [d[i], d[i + 1], d[i + 2], d[i + 3]]; };
  const set = (x, y, c) => { if (x < 0 || y < 0 || x >= W || y >= H) return; const i = (y * W + x) * 4, d = px.data; d[i] = c[0]; d[i + 1] = c[1]; d[i + 2] = c[2]; d[i + 3] = c[3]; };
  const same = (a, b) => a[0] === b[0] && a[1] === b[1] && a[2] === b[2] && a[3] === b[3];

  // a tela grande, com o xadrez do transparente e a grade dos pixels
  const canvas = h("canvas", { class: "pxcanvas" });
  const g = canvas.getContext("2d");
  let zoom = 1, ox = 0, oy = 0;
  const layout = () => {
    const box = canvas.parentElement.getBoundingClientRect();
    const cw = Math.max(200, Math.floor(box.width)), ch = Math.max(200, Math.floor(box.height));
    canvas.width = cw; canvas.height = ch;
    zoom = Math.max(1, Math.floor(Math.min((cw - 20) / W, (ch - 20) / H)));
    ox = Math.floor((cw - W * zoom) / 2); oy = Math.floor((ch - H * zoom) / 2);
    draw();
  };
  const dark = matchMedia("(prefers-color-scheme: dark)").matches;
  const draw = () => {
    g.fillStyle = getComputedStyle(document.body).backgroundColor;
    g.fillRect(0, 0, canvas.width, canvas.height);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      g.fillStyle = (x + y) % 2 ? (dark ? "#3e3e43" : "#ffffff") : (dark ? "#343438" : "#e8eaee");
      g.fillRect(ox + x * zoom, oy + y * zoom, zoom, zoom);
    }
    ctx.putImageData(px, 0, 0);
    g.imageSmoothingEnabled = false;
    g.drawImage(img, ox, oy, W * zoom, H * zoom);
    if (zoom >= 6) {
      g.strokeStyle = "rgba(0,0,0,.12)"; g.lineWidth = 1;
      g.beginPath();
      for (let x = 0; x <= W; x++) { g.moveTo(ox + x * zoom + 0.5, oy); g.lineTo(ox + x * zoom + 0.5, oy + H * zoom); }
      for (let y = 0; y <= H; y++) { g.moveTo(ox, oy + y * zoom + 0.5); g.lineTo(ox + W * zoom, oy + y * zoom + 0.5); }
      g.stroke();
    }
  };

  // pintar
  const flood = (x0, y0, with_) => {
    const target = get(x0, y0);
    if (same(target, with_)) return;
    const stack = [[x0, y0]];
    while (stack.length) {
      const [x, y] = stack.pop();
      if (x < 0 || y < 0 || x >= W || y >= H || !same(get(x, y), target)) continue;
      set(x, y, with_);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
  };
  const line = (a, b, c) => {
    let x = a[0], y = a[1];
    const dx = Math.abs(b[0] - x), sx = x < b[0] ? 1 : -1, dy = -Math.abs(b[1] - y), sy = y < b[1] ? 1 : -1;
    let err = dx + dy;
    for (;;) {
      set(x, y, c);
      if (x === b[0] && y === b[1]) break;
      const e2 = 2 * err;
      if (e2 >= dy) { err += dy; x += sx; }
      if (e2 <= dx) { err += dx; y += sy; }
    }
  };
  let start = null, before = null, last = null, painting = false, button = 0;
  const pixelAt = (e) => {
    const r = canvas.getBoundingClientRect();
    const x = Math.floor((e.clientX - r.left - ox) / zoom), y = Math.floor((e.clientY - r.top - oy) / zoom);
    return x >= 0 && y >= 0 && x < W && y < H ? [x, y] : null;
  };
  const paint = (p, first) => {
    const with_ = button === 2 ? [0, 0, 0, 0] : color;
    if (tool === "line" || tool === "rect") {
      if (first) { keep(); start = p; before = new Uint8ClampedArray(px.data); }
      else if (before) px.data.set(before);
      if (tool === "line") line(start, p, with_);
      else { const tr = [p[0], start[1]], bl = [start[0], p[1]]; line(start, tr, with_); line(tr, p, with_); line(p, bl, with_); line(bl, start, with_); }
    } else if (tool === "pen") { if (first) keep(); if (last && !first) line(last, p, with_); else set(p[0], p[1], with_); }
    else if (tool === "fill") { if (!first) return; keep(); flood(p[0], p[1], with_); }
    else if (tool === "pick") { if (!first) return; const c = get(p[0], p[1]); setColor(c[3] === 0 ? [0, 0, 0, 255] : c); }
    else if (tool === "swap") {
      if (!first) return;
      const from = get(p[0], p[1]);
      keep();
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (same(get(x, y), from)) set(x, y, with_);
      if (everywhere) swaps.push([from, with_]);
    }
    last = p;
    draw();
  };
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());
  canvas.addEventListener("pointerdown", (e) => {
    const p = pixelAt(e);
    if (!p) return;
    painting = true; button = e.button; last = null;
    canvas.setPointerCapture(e.pointerId);
    paint(p, true);
  });
  canvas.addEventListener("pointermove", (e) => {
    if (!painting) return;
    const p = pixelAt(e);
    if (p && (!last || p[0] !== last[0] || p[1] !== last[1])) paint(p, false);
  });
  const stop = () => { painting = false; last = null; before = null; };
  canvas.addEventListener("pointerup", stop);
  canvas.addEventListener("pointercancel", stop);

  // as ferramentas
  const tools = h("div", { class: "pxtools" });
  for (const [key, label] of [["pen", "px.pen"], ["line", "px.line"], ["rect", "px.rect"], ["fill", "px.fill"], ["pick", "px.pick"], ["swap", "px.swap"]]) {
    const b = h("button", { class: "btn small" + (key === tool ? " primary" : ""), text: t(label), onclick: () => {
      tool = key;
      tools.querySelectorAll("button").forEach((x) => x.classList.toggle("primary", x === b));
    } });
    tools.append(b);
  }
  // a cor: as do próprio pet primeiro (as mais usadas), e qualquer outra
  const swatch = h("input", { type: "color", value: "#000000", title: t("px.more") });
  const setColor = (c) => { color = [c[0], c[1], c[2], 255]; swatch.value = hex(c[0], c[1], c[2]); };
  swatch.addEventListener("input", () => { const v = swatch.value; color = [parseInt(v.slice(1, 3), 16), parseInt(v.slice(3, 5), 16), parseInt(v.slice(5, 7), 16), 255]; });
  const counts = new Map();
  for (const fr of frames.slice(0, 64)) {
    const d = fr.getContext("2d").getImageData(0, 0, fr.width, fr.height).data;
    for (let i = 0; i < d.length; i += 4) if (d[i + 3] > 200) { const k = hex(d[i], d[i + 1], d[i + 2]); counts.set(k, (counts.get(k) || 0) + 1); }
  }
  const palette = h("div", { class: "pxpalette" });
  const common = [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 32).map((e) => e[0]);
  for (const k of common)
    palette.append(h("button", { class: "pxswatch", style: "background:" + k, title: k, onclick: () => setColor([parseInt(k.slice(1, 3), 16), parseInt(k.slice(3, 5), 16), parseInt(k.slice(5, 7), 16)]) }));
  if (common.length) setColor([parseInt(common[0].slice(1, 3), 16), parseInt(common[0].slice(3, 5), 16), parseInt(common[0].slice(5, 7), 16)]);

  // o quadro inteiro
  const whole = (fn) => { keep(); fn(); draw(); };
  const mirror = (horizontal) => whole(() => {
    const copy = new Uint8ClampedArray(px.data);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const sx = horizontal ? W - 1 - x : x, sy = horizontal ? y : H - 1 - y;
      const s = (sy * W + sx) * 4, d = (y * W + x) * 4;
      px.data[d] = copy[s]; px.data[d + 1] = copy[s + 1]; px.data[d + 2] = copy[s + 2]; px.data[d + 3] = copy[s + 3];
    }
  });
  const move = (dx, dy) => whole(() => {
    const copy = new Uint8ClampedArray(px.data);
    px.data.fill(0);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const nx = x + dx, ny = y + dy;
      if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
      const s = (y * W + x) * 4, d = (ny * W + nx) * 4;
      px.data[d] = copy[s]; px.data[d + 1] = copy[s + 1]; px.data[d + 2] = copy[s + 2]; px.data[d + 3] = copy[s + 3];
    }
  });
  // um contorno de um pixel em volta do desenho (os transparentes que encostam nele)
  const outline = () => whole(() => {
    const solid = (x, y) => x >= 0 && y >= 0 && x < W && y < H && get(x, y)[3] > 0;
    const around = [];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++)
      if (get(x, y)[3] === 0 && (solid(x - 1, y) || solid(x + 1, y) || solid(x, y - 1) || solid(x, y + 1))) around.push([x, y]);
    for (const [x, y] of around) set(x, y, color);
  });
  const doUndo = () => { if (!undo.length) return; px.data.set(undo.pop()); draw(); };

  const side = h("div", { class: "pxside" },
    h("b", { text: t("px.tools") }), tools,
    h("label", { class: "toggle" }, (() => { const cb = h("input", { type: "checkbox" }); cb.addEventListener("change", () => { everywhere = cb.checked; }); return cb; })(), h("span", { text: t("px.everywhere") })),
    h("p", { class: "hint", text: t("px.hint") }),
    h("b", { text: t("px.color") }), h("div", { class: "row" }, swatch, h("span", { class: "hint", text: t("px.more") })), palette,
    h("b", { text: t("px.frame") }),
    h("div", { class: "row" },
      h("button", { class: "btn small", text: "↔ " + t("px.mirrorH"), onclick: () => mirror(true) }),
      h("button", { class: "btn small", text: "↕ " + t("px.mirrorV"), onclick: () => mirror(false) }),
      h("button", { class: "btn small", text: "▢ " + t("px.outline"), onclick: outline })),
    h("div", { class: "row", title: t("px.move") },
      ...[["←", -1, 0], ["→", 1, 0], ["↑", 0, -1], ["↓", 0, 1]].map(([s, dx, dy]) => h("button", { class: "btn small", text: s, title: t("px.move"), onclick: () => move(dx, dy) }))),
    h("button", { class: "btn small", text: "↶ " + t("act.undo"), onclick: doUndo }));

  const stage = h("div", { class: "pxstage" }, canvas);
  const onKey = (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") { e.preventDefault(); e.stopPropagation(); doUndo(); }
  };
  document.addEventListener("keydown", onKey, true);
  const ro = new ResizeObserver(layout);
  modal({
    title: t("px.title", frame), wide: true,
    body: h("div", { class: "pxwrap" }, side, stage),
    onClose: () => { document.removeEventListener("keydown", onKey, true); ro.disconnect(); },
    buttons: [
      { text: t("build.cancel") },
      { text: t("px.save"), primary: true, onClick: () => {
        ctx.putImageData(px, 0, 0);
        // a troca de cor vale também nos outros quadros, quando pedida
        if (swaps.length) {
          const map = new Map(swaps.map(([a, b]) => [keyOf(...a), b]));
          frames.forEach((fr, i) => { if (i !== frame) recolor(fr, map); });
        }
        const built = buildSheet(frames, doc.tilesX);
        doc.set(doc.section("image"), "png", built.png);   // uma mudança só (desfazer volta a folha inteira)
        onSaved && onSaved();
      } },
    ],
  });
  ro.observe(stage);
  setTimeout(layout, 0);
}
