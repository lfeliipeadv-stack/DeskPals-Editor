/* ---------------------------------------------------------------
   doc.js — o pet aberto no editor.

   O editor muda o próprio documento XML, nó por nó, em vez de copiar
   os valores para outra estrutura e escrever um arquivo novo. Assim,
   salvar nunca perde nada que o editor não conhece: o <floating> dos
   pets que flutuam, o atributo picker, elementos de versões futuras…
   O arquivo salvo é o mesmo que foi aberto, com as mudanças feitas.
   --------------------------------------------------------------- */

export const NS = "https://esheep.petrucci.ch/";

/* A ordem dos elementos no formato (a mesma dos pets da galeria): um
   elemento que o editor cria entra no lugar certo. */
const ORDER = {
  animations: ["header", "image", "spawns", "animations", "childs", "sounds"],
  header: ["author", "title", "petname", "version", "info", "application", "icon"],
  image: ["tilesx", "tilesy", "png", "transparency", "skins"],
  spawn: ["x", "y", "next"],
  animation: ["name", "start", "end", "sequence", "border", "gravity"],
  start: ["x", "y", "offsety", "opacity", "interval"],
  end: ["x", "y", "offsety", "opacity", "interval"],
  sequence: ["frame", "next", "action"],
  child: ["x", "y", "next"],
  sound: ["probability", "loop", "base64"],
};

/** Os filhos de um elemento com este nome (sem olhar o namespace). */
export function kids(el, name) {
  const out = [];
  if (!el) return out;
  for (const c of el.children) if (!name || c.localName === name) out.push(c);
  return out;
}
export function kid(el, name) {
  if (!el) return null;
  for (const c of el.children) if (c.localName === name) return c;
  return null;
}
export function text(el, name, fallback = "") {
  const k = name ? kid(el, name) : el;
  return k ? k.textContent.trim() : fallback;
}

export class PetDoc {
  constructor(xmlText, fileName = "animations.xml") {
    xmlText = String(xmlText).replace(/^﻿/, "");
    const parse = (s) => new DOMParser().parseFromString(s, "application/xml");
    let doc = parse(xmlText);
    // pets antigos (17 da galeria) escrevem o endereço do formato em xmlns:xsd, com um espaço no meio: o
    // navegador recusa esse namespace. O nome certo do atributo é xsi:schemaLocation (o editor do desktopPet
    // faz a mesma troca); só nesse caso o arquivo muda, e só essa linha.
    if (doc.getElementsByTagName("parsererror").length && /xmlns:xsd="[^"]*\s[^"]*"/.test(xmlText)) {
      let fixed = xmlText.replace(/xmlns:xsd="([^"]*)"/, 'xsi:schemaLocation="$1"');
      if (!/xmlns:xsi=/.test(fixed)) fixed = fixed.replace(/<animations\b/, '<animations xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"');
      doc = parse(fixed);
      this.repaired = true;
    }
    if (doc.getElementsByTagName("parsererror").length) throw new Error("bad-xml");
    if (!doc.documentElement || doc.documentElement.localName !== "animations") throw new Error("bad-xml");
    this.xml = doc;
    this.root = doc.documentElement;
    this.ns = this.root.namespaceURI || "";
    this.fileName = fileName;
    this.sheet = null;        // a folha de sprites já pronta para desenhar (com a cor transparente aplicada)
    this.sheetPromise = null;
    this.listeners = new Set();
    this.undo = [];
    this.redo = [];
    this.dirty = false;
    // uma seção que falta (um pet sem sons) só é criada quando algo é posto nela: abrir e salvar sem mexer
    // devolve o mesmo arquivo
  }

  /* ---------------------------------------------------------- elementos */

  /** Um elemento novo no namespace do arquivo. */
  make(name, textValue) {
    const el = this.ns ? this.xml.createElementNS(this.ns, name) : this.xml.createElement(name);
    if (textValue !== undefined && textValue !== null) el.textContent = String(textValue);
    return el;
  }

  /** O filho com esse nome; criado no lugar certo quando não existe. */
  ensure(parent, name) {
    let el = kid(parent, name);
    if (el) return el;
    el = this.make(name);
    this.insertOrdered(parent, el);
    return el;
  }

  /** Põe um filho na posição que o formato manda (e com a mesma indentação dos irmãos). */
  insertOrdered(parent, el) {
    const order = ORDER[parent.localName] || [];
    const rank = order.indexOf(el.localName);
    let before = null;
    if (rank >= 0) {
      for (const c of parent.children) {
        const r = order.indexOf(c.localName);
        if (r > rank) { before = c; break; }
      }
    }
    // a indentação: a do irmão (ou a do pai, com dois espaços a mais)
    const indent = this.indentOf(parent) + "  ";
    if (before) {
      parent.insertBefore(this.xml.createTextNode("\n" + indent), before);
      parent.insertBefore(el, before.previousSibling);
    } else {
      const last = parent.lastChild;
      if (last && last.nodeType === 3 && /^\s*$/.test(last.nodeValue)) {
        parent.insertBefore(this.xml.createTextNode("\n" + indent), last);
        parent.insertBefore(el, last);
      } else {
        parent.appendChild(this.xml.createTextNode("\n" + indent));
        parent.appendChild(el);
        parent.appendChild(this.xml.createTextNode("\n" + this.indentOf(parent)));
      }
    }
    return el;
  }

  indentOf(el) {
    const prev = el.previousSibling;
    if (prev && prev.nodeType === 3) {
      const m = /\n([ \t]*)$/.exec(prev.nodeValue);
      if (m) return m[1];
    }
    return "";
  }

  /** Tira um elemento e o espaço que vinha antes dele. */
  removeEl(el) {
    const prev = el.previousSibling;
    if (prev && prev.nodeType === 3 && /^\s*$/.test(prev.nodeValue)) prev.remove();
    el.remove();
  }

  section(name) { return this.ensure(this.root, name); }

  /** O texto de um filho; vazio quando não existe. */
  get(parent, name, fallback = "") { return text(parent, name, fallback); }

  /** Muda o texto de um filho (criado quando falta). Um dado grande (imagem, som) vai em CDATA, como nos pets. */
  set(parent, name, value) {
    const el = this.ensure(parent, name);
    const v = value === null || value === undefined ? "" : String(value);
    if (el.textContent === v) return;
    this.change(() => {
      while (el.firstChild) el.removeChild(el.firstChild);
      if (["png", "icon", "base64"].includes(name) && v.length > 0) el.appendChild(this.xml.createCDATASection(v));
      else el.textContent = v;
    });
  }

  setAttr(el, name, value) {
    const v = String(value);
    if (el.getAttribute(name) === v) return;
    this.change(() => el.setAttribute(name, v));
  }

  /* ---------------------------------------------------------- desfazer */

  /**
   * Toda mudança passa aqui: guarda o antes (para desfazer) e avisa as páginas. Mudanças dentro de outra (dois
   * campos de uma vez) contam como uma só: um desfazer volta tudo.
   */
  change(fn, label = "") {
    if (this.inChange) { fn(); return; }
    this.inChange = true;
    const before = this.serializeLight();
    try { fn(); } finally { this.inChange = false; }
    this.undo.push(before);
    if (this.undo.length > 60) this.undo.shift();
    this.redo = [];
    this.dirty = true;
    this.emit(label);
  }

  /** O XML sem os blocos grandes repetidos: uma imagem nova guarda a anterior só uma vez. */
  serializeLight() { return new XMLSerializer().serializeToString(this.xml); }

  restore(xmlText) {
    const doc = new DOMParser().parseFromString(xmlText, "application/xml");
    this.xml = doc;
    this.root = doc.documentElement;
    this.ns = this.root.namespaceURI || "";
    this.sheetKey = null;
  }

  canUndo() { return this.undo.length > 0; }
  canRedo() { return this.redo.length > 0; }
  doUndo() {
    if (!this.undo.length) return;
    this.redo.push(this.serializeLight());
    this.restore(this.undo.pop());
    this.dirty = true;
    this.emit("undo");
  }
  doRedo() {
    if (!this.redo.length) return;
    this.undo.push(this.serializeLight());
    this.restore(this.redo.pop());
    this.dirty = true;
    this.emit("redo");
  }

  on(fn) { this.listeners.add(fn); return () => this.listeners.delete(fn); }
  emit(label) { for (const fn of this.listeners) fn(label); }

  /* ---------------------------------------------------------- salvar */

  /** O arquivo como vai ser salvo. */
  serialize() {
    let s = new XMLSerializer().serializeToString(this.xml);
    if (!s.startsWith("<?xml")) s = '<?xml version="1.0" encoding="utf-8"?>\n' + s;
    return s;
  }

  /* ---------------------------------------------------------- partes do pet */

  // ler não cria nada (null quando falta: text(null) dá ""); as páginas que escrevem usam section()
  get header() { return kid(this.root, "header"); }
  get image() { return kid(this.root, "image"); }
  get spawnsEl() { return this.section("spawns"); }
  get animationsEl() { return this.section("animations"); }
  get childsEl() { return this.section("childs"); }
  get soundsEl() { return this.section("sounds"); }

  // as listas leem sem criar a seção; acrescentar usa os *El, que a criam
  get animations() { return kids(kid(this.root, "animations"), "animation"); }
  get spawns() { return kids(kid(this.root, "spawns"), "spawn"); }
  get childs() { return kids(kid(this.root, "childs"), "child"); }
  get sounds() { return kids(kid(this.root, "sounds"), "sound"); }

  animById(id) { return this.animations.find((a) => +a.getAttribute("id") === +id) || null; }
  animName(id) { const a = this.animById(id); return a ? text(a, "name") : null; }
  nextFreeAnimId() { return Math.max(0, ...this.animations.map((a) => +a.getAttribute("id") || 0)) + 1; }
  nextFreeSpawnId() { return Math.max(0, ...this.spawns.map((s) => +s.getAttribute("id") || 0)) + 1; }

  get petName() { return text(this.header, "petname") || text(this.header, "title") || ""; }
  get tilesX() { return Math.max(1, parseInt(text(this.image, "tilesx", "1"), 10) || 1); }
  get tilesY() { return Math.max(1, parseInt(text(this.image, "tilesy", "1"), 10) || 1); }
  get frameCount() { return this.tilesX * this.tilesY; }
  get frameW() { return this.sheet ? Math.floor(this.sheet.width / this.tilesX) : 0; }
  get frameH() { return this.sheet ? Math.floor(this.sheet.height / this.tilesY) : 0; }

  /**
   * A folha de sprites pronta para desenhar: a imagem do XML, com a cor transparente (normalmente Magenta) tirada,
   * como o programa faz. Refeita só quando a imagem ou a cor mudam.
   */
  async loadSheet() {
    const png = text(this.image, "png").replace(/\s+/g, "");
    const transp = text(this.image, "transparency");
    const key = png.length + ":" + png.slice(0, 64) + png.slice(-64) + ":" + transp;
    if (this.sheetKey === key && this.sheet) return this.sheet;
    this.sheetKey = key;
    if (!png) { this.sheet = null; return null; }
    const img = await new Promise((resolve, reject) => {
      const i = new Image();
      i.onload = () => resolve(i);
      i.onerror = () => reject(new Error("bad-png"));
      i.src = "data:image/png;base64," + png;
    });
    const c = document.createElement("canvas");
    c.width = img.width; c.height = img.height;
    const g = c.getContext("2d");
    g.drawImage(img, 0, 0);
    const key3 = colorOf(transp);
    if (key3) {
      const data = g.getImageData(0, 0, c.width, c.height);
      const d = data.data;
      for (let i = 0; i < d.length; i += 4)
        if (d[i] === key3[0] && d[i + 1] === key3[1] && d[i + 2] === key3[2]) d[i + 3] = 0;
      g.putImageData(data, 0, 0);
    }
    this.sheet = c;
    return c;
  }

  /** Desenha um quadro (0 = o primeiro) num contexto, espelhado se pedido. */
  drawFrame(g, frame, x, y, w, h, flipped = false) {
    if (!this.sheet) return;
    const n = Math.max(0, Math.min(this.frameCount - 1, frame | 0));
    const fw = this.frameW, fh = this.frameH;
    const sx = (n % this.tilesX) * fw, sy = Math.floor(n / this.tilesX) * fh;
    g.save();
    g.imageSmoothingEnabled = false;
    if (flipped) { g.translate(x + w, y); g.scale(-1, 1); g.drawImage(this.sheet, sx, sy, fw, fh, 0, 0, w, h); }
    else g.drawImage(this.sheet, sx, sy, fw, fh, x, y, w, h);
    g.restore();
  }
}

/** A cor transparente do XML ("Magenta", "#FF00FF", "255,0,255") em RGB; null quando não há. */
export function colorOf(name) {
  if (!name) return null;
  const n = name.trim().toLowerCase();
  const named = { magenta: [255, 0, 255], fuchsia: [255, 0, 255], black: [0, 0, 0], white: [255, 255, 255], lime: [0, 255, 0], green: [0, 128, 0], blue: [0, 0, 255], red: [255, 0, 0], cyan: [0, 255, 255], yellow: [255, 255, 0] };
  if (named[n]) return named[n];
  let m = /^#?([0-9a-f]{6})$/i.exec(n);
  if (m) return [parseInt(m[1].slice(0, 2), 16), parseInt(m[1].slice(2, 4), 16), parseInt(m[1].slice(4, 6), 16)];
  m = /^(\d+)\s*,\s*(\d+)\s*,\s*(\d+)$/.exec(n);
  if (m) return [+m[1], +m[2], +m[3]];
  return null;
}

/* ---------------------------------------------------------------
   Um pet novo que já funciona (o mesmo modelo do editor para Windows,
   Template.cs): uma bolinha que anda, vira, pisca, cai do céu e pode
   ser pega com o mouse. Troque a folha de sprites e mude a partir daí.
   --------------------------------------------------------------- */

const BODY = [
  ".....oooooo.....",
  "...ooBBBBBBoo...",
  "..oBBWBBBBBBBo..",
  ".oBBWBBBBBBBBBo.",
  ".oBBBBBBBBBBBBo.",
  "oBBBBeBBBBeBBBBo",
  "oBBBBeBBBBeBBBBo",
  "oBBBBBBBBBBBBBBo",
  "oBBBcBBBBBBcBBBo",
  "oBBBBBBmmBBBBBBo",
  ".oBBBBBBBBBBBBo.",
  ".oDBBBBBBBBBBDo.",
  "..oDDBBBBBBDDo..",
  "...ooDDDDDDoo...",
  ".....oooooo.....",
];
const COLORS = { o: "#28345a", B: "#6ea8ff", D: "#4e80dc", W: "#dcecff", e: "#1e2232", c: "#ff96aa", m: "#28345a" };

/** A folha de sprites do modelo: 5 quadros de 60×60 px num fundo magenta. Devolve o PNG em base64. */
export function templateSheet() {
  const w = 20, h = 20, z = 3;
  const c = document.createElement("canvas");
  c.width = w * 5 * z; c.height = h * z;
  const g = c.getContext("2d");
  g.fillStyle = "#ff00ff";
  g.fillRect(0, 0, c.width, c.height);
  const px = (x, y, color) => { g.fillStyle = color; g.fillRect(x * z, y * z, z, z); };
  for (let f = 0; f < 5; f++) {
    const rows = BODY.map((r) => r.split(""));
    if (f === 3) rows.forEach((r, ri) => r.forEach((ch, i) => { if (ch === "e") r[i] = ri === 5 ? "B" : "m"; }));   // piscando
    const lift = f === 1 ? 1 : 0;
    rows.forEach((r, y) => r.forEach((ch, x) => { if (COLORS[ch]) px(f * w + 2 + x, y + 2 - lift, COLORS[ch]); }));
    const fy = 17;
    const feet = f === 1 ? [[5, fy], [12, fy - 1]] : f === 2 ? [[5, fy - 1], [12, fy]] : [[5, fy], [12, fy]];
    for (const [fx, fyy] of feet) for (let dx = 0; dx < 3; dx++) px(f * w + 2 + fx + dx, fyy, COLORS.o);
    if (f === 4) for (const ax of [0, 17]) for (let dy = 0; dy < 4; dy++) px(f * w + 1 + ax, 4 + dy, COLORS.o);
  }
  return c.toDataURL("image/png").split(",")[1];
}

function anim(id, name, x, y, interval, repeat, frames, next, { border, gravity, action, endY } = {}) {
  const nexts = (list) => list.map(([p, n]) => `<next probability="${p}" only="none">${n}</next>`).join("");
  return `<animation id="${id}"><name>${name}</name>` +
    `<start><x>${x}</x><y>${y}</y><offsety>0</offsety><opacity>1</opacity><interval>${interval}</interval></start>` +
    `<end><x>${x}</x><y>${endY ?? y}</y><offsety>0</offsety><opacity>1</opacity><interval>${interval}</interval></end>` +
    `<sequence repeatfrom="0" repeat="${repeat}">${frames.map((f) => `<frame>${f}</frame>`).join("")}${nexts(next)}${action ? `<action>${action}</action>` : ""}</sequence>` +
    (border ? `<border>${nexts(border)}</border>` : "") + (gravity ? `<gravity>${nexts(gravity)}</gravity>` : "") +
    `</animation>`;
}

/** O XML de um pet novo (nome, título e descrição na língua do editor). */
export function templateXml(names) {
  const png = templateSheet();
  const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;");
  const xml = `<?xml version="1.0" encoding="utf-8"?>
<animations xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="https://esheep.petrucci.ch/ https://adrianotiger.github.io/desktopPet/Resources/animations.xsd" xmlns="https://esheep.petrucci.ch/">
<header><author>${esc(names.author)}</author><title>${esc(names.title)}</title><petname>${esc(names.name)}</petname><version>1.0</version><info>${esc(names.info)}</info><application>1</application><icon></icon></header>
<image><tilesx>5</tilesx><tilesy>1</tilesy><png><![CDATA[${png}]]></png><transparency>Magenta</transparency></image>
<spawns><spawn id="1" probability="60"><x>random*(screenW-imageW-50)/100+25</x><y>areaH-imageH</y><next probability="100">1</next></spawn><spawn id="2" probability="40"><x>random*(screenW-imageW-50)/100+25</x><y>-imageH</y><next probability="100">5</next></spawn></spawns>
<animations>${[
    anim(1, "walk", "-2", "0", 150, "random/4+4", [1, 2], [[60, 1], [15, 2], [25, 3]], { border: [[100, 2]], gravity: [[100, 5]] }),
    anim(2, "turn", "0", "0", 150, "0", [0, 3, 0], [[100, 1]], { action: "flip" }),
    anim(3, "blink", "0", "0", 120, "0", [0, 3, 0, 0], [[100, 1]]),
    anim(4, "drag", "0", "0", 120, "100", [4], [[100, 4]]),
    anim(5, "fall", "0", "3", 60, "60", [4], [[100, 5]], { border: [[100, 6]], endY: "12" }),
    anim(6, "fall soft", "0", "0", 120, "0", [0, 3, 0], [[100, 1]]),
    anim(7, "fall hard", "0", "0", 160, "0", [3, 3, 0, 3, 0], [[100, 1]]),
  ].join("")}</animations>
<childs></childs>
<sounds></sounds>
</animations>`;
  return prettyXml(xml);
}

/** Indenta um XML escrito numa linha só (o modelo), com dois espaços por nível. */
export function prettyXml(xml) {
  const doc = new DOMParser().parseFromString(xml, "application/xml");
  const walk = (el, depth) => {
    const kidsEls = [...el.children];
    if (!kidsEls.length) return;
    for (const c of [...el.childNodes]) if (c.nodeType === 3 && /^\s*$/.test(c.nodeValue)) c.remove();
    for (const c of kidsEls) {
      el.insertBefore(doc.createTextNode("\n" + "  ".repeat(depth + 1)), c);
      walk(c, depth + 1);
    }
    el.appendChild(doc.createTextNode("\n" + "  ".repeat(depth)));
  };
  walk(doc.documentElement, 0);
  // só a raiz: o navegador escreve a declaração <?xml?> ao serializar o documento inteiro, e duas seguidas não valem
  return '<?xml version="1.0" encoding="utf-8"?>\n' + new XMLSerializer().serializeToString(doc.documentElement);
}
