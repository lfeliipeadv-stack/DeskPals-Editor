/* ---------------------------------------------------------------
   map.js — o mapa das animações (o mesmo do Editor de Pets para
   Windows): cada animação é um cartão, em colunas pela distância de
   uma entrada; as setas mostram o que vem depois (cinza ao terminar,
   laranja numa borda, azul sem chão, roxo numa janela filha).
   Arrastar move, a roda aproxima, clicar mostra as setas de um
   cartão, dois cliques abrem a animação.
   --------------------------------------------------------------- */
import { t } from "./i18n.js";
import { h } from "./ui.js";
import { kid, kids, text } from "./doc.js";
import { frameCanvas } from "./files.js";

const SVG = "http://www.w3.org/2000/svg";
const s = (tag, attrs = {}) => { const el = document.createElementNS(SVG, tag); for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v); return el; };
const W = 170, H = 54, GAP_X = 90, GAP_Y = 18;
const KIND_COLOR = ["#8c929c", "#e68c28", "#3c82e6", "#a05ad2"];

export function mapPage(p, doc, go) {
  p.append(h("h1", { text: t("map.title") }), h("p", { class: "intro", text: t("map.intro") }));
  const svg = s("svg", { class: "mapsvg", role: "img", "aria-label": t("map.title") });
  const fit = h("button", { class: "btn", text: t("map.fit") });
  p.append(h("div", { class: "row", style: "margin-bottom:10px" }, fit, h("span", { class: "hint", text: t("map.legend") })), h("div", { class: "mapbox" }, svg));

  // as animações, as setas e a coluna de cada uma (a distância de uma entrada)
  const anims = [];
  const seen = new Set();
  for (const a of doc.animations) { const id = +a.getAttribute("id"); if (!seen.has(id)) { seen.add(id); anims.push(a); } }
  const ids = new Set(anims.map((a) => +a.getAttribute("id")));
  const edges = [];
  for (const a of anims) {
    const from = +a.getAttribute("id");
    [["sequence", 0], ["border", 1], ["gravity", 2]].forEach(([w, kind]) => {
      for (const n of kids(kid(a, w), "next")) { const to = +n.textContent; if (ids.has(to)) edges.push({ from, to, kind }); }
    });
  }
  for (const c of doc.childs) { const from = +c.getAttribute("animationid"), to = +text(c, "next"); if (ids.has(from) && ids.has(to)) edges.push({ from, to, kind: 3 }); }
  const spawnStarts = new Set(doc.spawns.map((sp) => +text(sp, "next")));
  const depth = new Map();
  const queue = [];
  for (const id of spawnStarts) if (ids.has(id) && !depth.has(id)) { depth.set(id, 0); queue.push(id); }
  while (queue.length) {
    const id = queue.shift();
    for (const e of edges) if (e.from === id && !depth.has(e.to)) { depth.set(e.to, depth.get(id) + 1); queue.push(e.to); }
  }
  const last = depth.size ? Math.max(...depth.values()) + 1 : 0;
  for (const a of anims) if (!depth.has(+a.getAttribute("id"))) depth.set(+a.getAttribute("id"), last);
  // uma coluna comprida vira várias lado a lado (um pet com centenas de cenas continua legível)
  const maxRows = Math.max(8, Math.ceil(Math.sqrt(anims.length) * 1.3));
  const pos = new Map();
  let x = 0;
  const groups = [...new Set([...depth.values()])].sort((a, b) => a - b);
  for (const d of groups) {
    const items = anims.filter((a) => depth.get(+a.getAttribute("id")) === d).sort((a, b) => a.getAttribute("id") - b.getAttribute("id"));
    const cols = Math.ceil(items.length / maxRows);
    items.forEach((a, i) => pos.set(+a.getAttribute("id"), { x: x + Math.floor(i / maxRows) * (W + 24), y: (i % maxRows) * (H + GAP_Y) }));
    x += cols * (W + 24) - 24 + GAP_X;
  }

  // as setas e os cartões
  const defs = s("defs");
  KIND_COLOR.forEach((c, i) => {
    const m = s("marker", { id: "arrow" + i, viewBox: "0 0 10 10", refX: "9", refY: "5", markerWidth: "7", markerHeight: "7", orient: "auto-start-reverse" });
    m.append(s("path", { d: "M0,0 L10,5 L0,10 z", fill: c }));
    defs.append(m);
  });
  const world = s("g");
  const edgeLayer = s("g"), nodeLayer = s("g");
  world.append(edgeLayer, nodeLayer);
  svg.append(defs, world);
  const edgeEls = edges.map((e) => {
    const a = pos.get(e.from), b = pos.get(e.to);
    let d;
    if (e.from === e.to) d = `M${a.x + W - 24},${a.y} C${a.x + W - 10},${a.y - 34} ${a.x + W + 30},${a.y - 10} ${a.x + W},${a.y + 12}`;
    else {
      const p1 = { x: a.x + W, y: a.y + H / 2 };
      if (b.x > a.x + W) {
        const p2 = { x: b.x, y: b.y + H / 2 }, dx = Math.max(40, Math.abs(p2.x - p1.x) / 2);
        d = `M${p1.x},${p1.y} C${p1.x + dx},${p1.y} ${p2.x - dx},${p2.y} ${p2.x},${p2.y}`;
      } else {
        const p2 = { x: b.x + W, y: b.y + H / 2 };
        d = `M${p1.x},${p1.y} C${p1.x + 60},${p1.y} ${p2.x + 60},${p2.y} ${p2.x},${p2.y}`;
      }
    }
    const path = s("path", { d, fill: "none", stroke: KIND_COLOR[e.kind], "stroke-width": "1.3", "marker-end": `url(#arrow${e.kind})`, opacity: ".55" });
    if (e.kind === 1) path.setAttribute("stroke-dasharray", "6 4");
    if (e.kind === 2) path.setAttribute("stroke-dasharray", "2 4");
    edgeLayer.append(path);
    return { e, path };
  });
  const thumbs = new Map();
  const thumbOf = (frame) => {
    if (thumbs.has(frame)) return thumbs.get(frame);
    let url = "";
    if (doc.sheet && frame >= 0) {
      const c = document.createElement("canvas");
      c.width = c.height = 40;
      const fw = doc.frameW, fh = doc.frameH, z = Math.min(40 / fw, 40 / fh);
      doc.drawFrame(c.getContext("2d"), frame, (40 - fw * z) / 2, (40 - fh * z) / 2, fw * z, fh * z);
      url = c.toDataURL();
    }
    thumbs.set(frame, url);
    return url;
  };
  const nodeEls = new Map();
  for (const a of anims) {
    const id = +a.getAttribute("id"), q = pos.get(id);
    const isSpawn = spawnStarts.has(id);
    const gEl = s("g", { class: "mapnode" + (isSpawn ? " spawn" : ""), transform: `translate(${q.x},${q.y})`, tabindex: "0" });
    gEl.append(s("rect", { width: W, height: H, rx: 7 }));
    const f = kid(kid(a, "sequence"), "frame");
    const url = thumbOf(f ? parseInt(f.textContent, 10) || 0 : -1);
    if (url) gEl.append(s("image", { href: url, x: 6, y: 7, width: 40, height: 40, style: "image-rendering:pixelated" }));
    const name = s("text", { x: 52, y: 23, class: "mapname" });
    name.textContent = text(a, "name").slice(0, 20) || "—";
    const sub = s("text", { x: 52, y: 41, class: "mapsub" });
    sub.textContent = "#" + id + (isSpawn ? "  · " + t("map.entrance") : "");
    const tip = s("title"); tip.textContent = text(a, "name") + " (#" + id + ")";
    gEl.append(name, sub, tip);
    gEl.addEventListener("click", (ev) => { ev.stopPropagation(); select(id); });
    gEl.addEventListener("dblclick", () => go("animations", id));
    gEl.addEventListener("keydown", (ev) => { if (ev.key === "Enter") go("animations", id); });
    nodeLayer.append(gEl);
    nodeEls.set(id, gEl);
  }
  let selected = -1;
  const select = (id) => {
    selected = id;
    for (const [nid, el] of nodeEls) el.classList.toggle("on", nid === id);
    const big = anims.length > 60;
    for (const { e, path } of edgeEls) {
      const mine = id >= 0 && (e.from === id || e.to === id);
      path.setAttribute("opacity", mine ? "1" : id >= 0 ? (big ? "0" : ".12") : ".55");
      path.setAttribute("stroke-width", mine ? "2.4" : "1.3");
      if (mine) edgeLayer.append(path);   // por cima das outras
    }
  };
  svg.addEventListener("click", () => select(-1));

  // mover e aproximar: tudo muda a viewBox
  const bounds = { w: Math.max(...[...pos.values()].map((q) => q.x)) + W + 60, h: Math.max(...[...pos.values()].map((q) => q.y)) + H + 60 };
  let view = { x: -30, y: -40, w: bounds.w, h: bounds.h };
  const apply = () => svg.setAttribute("viewBox", `${view.x} ${view.y} ${view.w} ${view.h}`);
  const fitAll = () => {
    const r = svg.getBoundingClientRect();
    const ratio = r.width > 0 && r.height > 0 ? r.width / r.height : bounds.w / bounds.h;
    let w = bounds.w + 40, hh = bounds.h + 40;
    if (w / hh < ratio) w = hh * ratio; else hh = w / ratio;
    view = { x: -30, y: -40, w, h: hh };
    apply();
  };
  fit.addEventListener("click", fitAll);
  let drag = null;
  svg.addEventListener("pointerdown", (e) => { if (e.button !== 0) return; drag = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y, moved: false }; });
  svg.addEventListener("pointermove", (e) => {
    if (!drag) return;
    const r = svg.getBoundingClientRect(), k = view.w / r.width;
    if (Math.abs(e.clientX - drag.x) + Math.abs(e.clientY - drag.y) > 4) { drag.moved = true; svg.setPointerCapture(e.pointerId); }
    view.x = drag.vx - (e.clientX - drag.x) * k;
    view.y = drag.vy - (e.clientY - drag.y) * k;
    apply();
  });
  // soltar depois de arrastar não é clique (senão a seleção sumiria a cada arrasto)
  let swallowClick = false;
  const end = () => { swallowClick = !!(drag && drag.moved); drag = null; };
  svg.addEventListener("pointerup", end);
  svg.addEventListener("pointercancel", end);
  svg.addEventListener("click", (e) => { if (swallowClick) { swallowClick = false; e.stopPropagation(); } }, true);
  svg.addEventListener("wheel", (e) => {
    e.preventDefault();
    const r = svg.getBoundingClientRect();
    const mx = view.x + (e.clientX - r.left) / r.width * view.w, my = view.y + (e.clientY - r.top) / r.height * view.h;
    const k = e.deltaY < 0 ? 1 / 1.15 : 1.15;
    const nw = Math.max(200, Math.min(bounds.w * 4, view.w * k)), nk = nw / view.w;
    view = { x: mx - (mx - view.x) * nk, y: my - (my - view.y) * nk, w: nw, h: view.h * nk };
    apply();
  }, { passive: false });
  // um mapa grande começa legível, pelas entradas (à esquerda); "Ver tudo" mostra o mapa inteiro
  const startView = () => {
    const r = svg.getBoundingClientRect();
    if (r.width < 100 || r.height < 100 || (bounds.w <= r.width * 1.6 && bounds.h <= r.height * 1.6)) { fitAll(); return; }
    view = { x: -30, y: -40, w: r.width / 0.85, h: r.height / 0.85 };
    apply();
  };
  requestAnimationFrame(startView);
}
