/* ---------------------------------------------------------------
   app.js — o DeskPals Editor no navegador: o topo, a navegação e as
   páginas (Projeto, Cabeçalho, Folha de sprites, Animações, Entradas,
   Janelas filhas, Sons, Verificador, XML e Testar).
   --------------------------------------------------------------- */
import { t, initLang, setLang, getLang, LANGS, flagImg } from "./i18n.js";
import { PetDoc, kid, kids, text, templateXml } from "./doc.js";
import { AnimPlayer } from "./player.js";
import { validate, formulaError } from "./validator.js";
import { makeZip, download, makeIco, frameCanvas, squareIcon, imageFileToPng, base64ToBytes, bytesToBase64 } from "./files.js";
import { World, Pet } from "./pet-engine.js";
import { h, $, toast, pickFile, modal, confirmBox, promptBox, alertBox, popupMenu, toggle } from "./ui.js";
import { behaviorDialog, buildSheetDialog, wizardDialog, galleryDialog } from "./dialogs.js";
import { parseSource } from "./sources.js";
import { pixelEditor } from "./pixel.js";
import { mapPage } from "./map.js";
import { LiveSet } from "./live.js";
import { encodeGif } from "./gif.js";

/* ---------------------------------------------------------- utilidades de página */
const page = () => $("#page");
/* ---------------------------------------------------------- estado */
const state = { doc: null, page: "project", anim: null, search: "", cleanup: [], problems: [] };
const PAGES = [
  ["project", "nav.project"], ["header", "nav.header"], ["sheet", "nav.sheet"], ["animations", "nav.animations"],
  ["spawns", "nav.spawns"], ["childs", "nav.childs"], ["sounds", "nav.sounds"],
  ["-", "nav.tools"], ["check", "nav.check"], ["board", "nav.board"], ["map", "nav.map"], ["xml", "nav.xml"], ["test", "act.test"],
];

/* ---------------------------------------------------------- guardar neste navegador (recuperar) */
const DB = "deskpals-editor", STORE = "pet";
function idb() {
  return new Promise((res, rej) => {
    const r = indexedDB.open(DB, 1);
    r.onupgradeneeded = () => r.result.createObjectStore(STORE);
    r.onsuccess = () => res(r.result);
    r.onerror = () => rej(r.error);
  });
}
async function saveLocal() {
  if (!state.doc) return;
  try {
    const db = await idb();
    db.transaction(STORE, "readwrite").objectStore(STORE).put({ xml: state.doc.serialize(), name: state.doc.fileName, dirty: state.doc.dirty, time: Date.now() }, "current");
  } catch { /* sem armazenamento: o pet fica só nesta aba */ }
}
async function loadLocal() {
  try {
    const db = await idb();
    return await new Promise((res) => { const r = db.transaction(STORE).objectStore(STORE).get("current"); r.onsuccess = () => res(r.result); r.onerror = () => res(null); });
  } catch { return null; }
}
async function clearLocal() {
  try { const db = await idb(); db.transaction(STORE, "readwrite").objectStore(STORE).delete("current"); } catch { /* nada a apagar */ }
}

/* ---------------------------------------------------------- abrir e salvar */
async function openDoc(xmlText, fileName = "animations.xml", { recovered = false, dirty = false } = {}) {
  let doc;
  try { doc = new PetDoc(xmlText, fileName); }
  catch { toast(t("doc.badXml")); return false; }
  try { await doc.loadSheet(); } catch { /* sem imagem válida: o verificador diz */ }
  state.doc = doc;
  doc.dirty = dirty;
  state.anim = doc.animations.length ? +doc.animations[0].getAttribute("id") : null;
  state.recovered = recovered;
  let timer = 0;
  doc.on((label) => {
    clearTimeout(timer);
    timer = setTimeout(() => { saveLocal(); refreshProblems(); }, 500);
    refreshTop();
    if (label === "undo" || label === "redo" || label === "full") { doc.loadSheet().then(render).catch(render); }
  });
  refreshProblems();
  if (!recovered) saveLocal();
  go(state.page === "project" ? "animations" : state.page);
  return true;
}

function newPet() {
  if (!confirmLeave()) return;
  openDoc(templateXml({ name: t("tpl.name") === "tpl.name" ? "Pet" : t("tpl.name"), title: t("tpl.title") === "tpl.title" ? "Pet" : t("tpl.title"),
    info: t("tpl.info") === "tpl.info" ? "" : t("tpl.info"), author: "" }), "animations.xml", { dirty: true });
}

function confirmLeave() {
  return !state.doc || !state.doc.dirty || confirm(t("web.leave"));
}

async function openFile(file) {
  if (!file) return;
  if (!confirmLeave()) return;
  try { await openDoc(await file.text(), file.name); }
  catch (e) { toast(t("web.loadFail", e.message)); }
}

/** O endereço do animations.xml para um link do GitHub (pasta, arquivo, raw) ou um link direto. */
export function rawUrl(link) {
  let u = link.trim();
  let m = /^https?:\/\/github\.com\/([^/]+)\/([^/]+)\/(?:tree|blob)\/([^/]+)\/?(.*)$/i.exec(u);
  if (m) {
    let path = m[4].replace(/\/+$/, "");
    if (!/\.xml$/i.test(path)) path = (path ? path + "/" : "") + "animations.xml";
    return `https://raw.githubusercontent.com/${m[1]}/${m[2]}/${m[3]}/${path}`;
  }
  m = /^https?:\/\/raw\.githubusercontent\.com\/.+$/i.exec(u);
  if (m) return /\.xml$/i.test(u) ? u : u.replace(/\/?$/, "/animations.xml");
  return u;
}

async function openUrl() {
  const link = await promptBox(t("web.openUrlAsk"), "https://github.com/", { title: t("web.openUrl") });
  if (!link || !/^https?:\/\//i.test(link.trim())) return;
  if (!confirmLeave()) return;
  // o link de um repositório (sem pasta nem arquivo) vai para a galeria, que procura os pets dele
  const repo = parseSource(link);
  const toGallery = () => galleryDialog((xml) => openDoc(xml, "animations.xml"), link.trim());
  if (repo && repo.path == null && !/\.xml$/i.test(link.trim())) { toGallery(); return; }
  toast(t("web.loading"));
  try {
    const r = await fetch(rawUrl(link));
    if (!r.ok) {
      // uma pasta sem animations.xml pode ser uma pasta de pets
      if (r.status === 404 && repo && !/\.xml$/i.test(link.trim())) { toGallery(); return; }
      throw new Error(r.status + " " + r.statusText);
    }
    await openDoc(await r.text(), "animations.xml");
  } catch (e) { toast(t("web.loadFail", e.message)); }
}

function saveFile() {
  const doc = state.doc;
  if (!doc) return;
  const errors = state.problems.filter((p) => p.error).length;
  if (errors > 0 && !confirm(t("msg.saveWithErrors", errors))) return;
  download("animations.xml", doc.serialize());
  doc.dirty = false;
  saveLocal();
  refreshTop();
}

function folderName(doc) {
  return (doc.petName || "pet").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, "") || "pet";
}

/**
 * O pet num .zip pronto para mandar, igual à pasta da galeria: animations.xml, icon.png, preview.gif (o pet andando e
 * um truque) e o README em inglês e em português.
 */
async function petZip(doc) {
  await doc.loadSheet().catch(() => {});
  const folder = folderName(doc);
  const files = { [folder + "/animations.xml"]: doc.serialize() };
  if (doc.sheet) {
    const icon = await iconPng(doc);
    files[folder + "/icon.png"] = new Uint8Array(await (await new Promise((r) => icon.toBlob(r, "image/png"))).arrayBuffer());
    const gif = previewGif(doc);
    if (gif) files[folder + "/preview.gif"] = gif;
  }
  const info = text(doc.header, "info").replace(/\[br\]/g, "\n");
  const name = doc.petName || folder, author = text(doc.header, "author") || "?";
  files[folder + "/README.md"] = `# ${name}\n\n${info}\n\nMade by ${author} with DeskPals Editor.\n`;
  files[folder + "/README.pt-BR.md"] = `# ${name}\n\n${info}\n\nFeito por ${author} com o DeskPals Editor.\n`;
  return { folder, blob: makeZip(files) };
}

async function exportZip() {
  const doc = state.doc;
  if (!doc) return;
  const errors = state.problems.filter((p) => p.error).length;
  if (errors > 0 && !(await confirmBox(t("msg.saveWithErrors", errors), { title: t("proj.zip") }))) return;
  const { folder, blob } = await petZip(doc);
  download(folder + ".zip", blob);
}

/** O ícone em 64 px: o do cabeçalho (um .ico) ou, sem ele, o primeiro quadro recortado. */
async function iconPng(doc) {
  const ico = text(doc.header, "icon").replace(/\s+/g, "");
  if (ico) {
    try {
      const img = await new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = rej; i.src = "data:image/x-icon;base64," + ico; });
      const c = document.createElement("canvas");
      c.width = c.height = 64;
      const g = c.getContext("2d");
      g.imageSmoothingEnabled = img.width > 64;
      g.drawImage(img, 0, 0, 64, 64);
      return c;
    } catch { /* um ícone que o navegador não lê: vai o quadro */ }
  }
  return squareIcon(frameCanvas(doc, firstFrame(doc)), 64);
}

/** A prévia animada (como a da galeria): o andar duas vezes e um truque, com uns 120 px de altura. */
function previewGif(doc) {
  const anims = doc.animations;
  const framesOf = (a) => kids(kid(a, "sequence"), "frame").map((f) => parseInt(f.textContent, 10) || 0);
  const find = (...names) => names.map((n) => anims.find((a) => text(a, "name").toLowerCase() === n && framesOf(a).length)).find(Boolean);
  const walk = find("walk", "idle") || anims.find((a) => framesOf(a).length);
  if (!walk) return null;
  const trick = find("wave", "dance", "happy", "jump", "spin", "eat", "sleep", "blink");
  let seq = [...framesOf(walk), ...framesOf(walk)];
  if (trick && trick !== walk) seq = seq.concat(framesOf(trick));
  while (seq.length < 8) seq = seq.concat(framesOf(walk));
  seq = seq.filter((i) => i >= 0 && i < doc.frameCount).slice(0, 120);
  const scale = Math.max(1, Math.floor(120 / Math.max(1, doc.frameH)));
  const canvases = seq.map((i) => {
    const c = document.createElement("canvas");
    c.width = doc.frameW * scale; c.height = doc.frameH * scale;
    doc.drawFrame(c.getContext("2d"), i, 0, 0, c.width, c.height);
    return c;
  });
  const interval = parseInt(text(kid(walk, "start"), "interval", "150"), 10) || 150;
  return encodeGif(canvases, Math.max(60, interval));
}

/**
 * Publicar na galeria: o .zip é baixado e o GitHub abre na página de envio, já preenchida (nome, autor, números).
 * O GitHub não recebe um arquivo por link: é só arrastar o .zip para a página. Quem cuida da galeria confere antes.
 */
async function publishGallery() {
  const doc = state.doc;
  if (!doc) return;
  if (!text(doc.header, "petname") || !text(doc.header, "author")) { await alertBox(t("pub.needHeader"), t("pub.title")); go("header"); return; }
  const errors = state.problems.filter((p) => p.error).length;
  if (errors > 0) { await alertBox(t("pub.errors", errors), t("pub.title")); go("check"); return; }
  if (!(await confirmBox(t("pub.confirm"), { title: t("pub.title") }))) return;
  const { folder, blob } = await petZip(doc);
  download(folder + ".zip", blob);
  let info = text(doc.header, "info").replace(/\[br\]/g, "\n").trim();
  if (info.length > 600) info = info.slice(0, 600) + "…";
  const body = t("pub.body", text(doc.header, "petname"), text(doc.header, "author"), info || "—", doc.animations.length, doc.frameCount, doc.sounds.length, folder + ".zip");
  const url = "https://github.com/lfeliipeadv-stack/DeskPals/issues/new?template=new_pet.md&labels=pet&title=" +
    encodeURIComponent("[Pet] " + text(doc.header, "petname")) + "&body=" + encodeURIComponent(body);
  window.open(url, "_blank", "noopener");
  toast(t("pub.done", folder + ".zip"), 9000);
}

/** O primeiro quadro da primeira animação (o rosto do pet, para o ícone). */
function firstFrame(doc) {
  const a = doc.animations[0];
  const f = a ? kid(kid(a, "sequence"), "frame") : null;
  return f ? parseInt(f.textContent, 10) || 0 : 0;
}

/* ---------------------------------------------------------- topo e navegação */
function refreshTop() {
  const doc = state.doc;
  $("#petname").textContent = doc ? doc.petName || t("doc.untitled") : t("web.title");
  $("#petstate").textContent = doc ? (doc.dirty ? t("web.unsaved") : t("web.saved")) : t("web.tagline");
  for (const id of ["btnSave", "btnZip"]) $("#" + id).disabled = !doc;
  $("#btnUndo").disabled = !doc || !doc.canUndo();
  $("#btnRedo").disabled = !doc || !doc.canRedo();
}

function refreshProblems() {
  state.problems = state.doc ? validate(state.doc) : [];
  const e = state.problems.filter((p) => p.error).length, w = state.problems.length - e;
  const btn = $("#btnCheck");
  btn.disabled = !state.doc;
  btn.textContent = !state.doc ? t("act.check") : state.problems.length === 0 ? "✓ " + t("act.checkOk") : t("act.checkCount", e, w);
  btn.className = "btn " + (!state.doc ? "" : e ? "badge-err" : w ? "badge-warn" : "badge-ok");
  renderNav();
}

function renderNav() {
  const nav = $("#nav");
  nav.textContent = "";
  for (const [id, key] of PAGES) {
    if (id === "-") { nav.append(h("div", { class: "group", text: t(key) })); continue; }
    const count = state.problems.filter((p) => p.page === id);
    const errs = count.filter((p) => p.error).length;
    nav.append(h("a", { href: "#" + id, class: state.page === id ? "on" : "", onclick: (e) => { e.preventDefault(); go(id); } },
      t(key), count.length ? h("span", { class: "count" + (errs ? " err" : ""), text: String(count.length) }) : null));
  }
}

function go(id, anim = null) {
  state.page = id;
  if (anim !== null && anim !== undefined && anim >= 0) state.anim = anim;
  history.replaceState(null, "", "#" + id);
  render();
}

function render() {
  for (const fn of state.cleanup) fn();
  state.cleanup = [];
  renderNav();
  refreshTop();
  const p = page();
  p.textContent = "";
  if (state.page !== "project" && !state.doc) {
    p.append(h("h1", { text: t("nav." + state.page) }), h("p", { class: "intro", text: t("web.noPet") }), h("button", { class: "btn primary", text: t("nav.project"), onclick: () => go("project") }));
    return;
  }
  ({ project: pageProject, header: pageHeader, sheet: pageSheet, animations: pageAnimations, spawns: pageSpawns, childs: pageChilds,
    sounds: pageSounds, check: pageCheck, map: (el) => mapPage(el, state.doc, go), board: pageBoard, xml: pageXml, test: pageTest })[state.page]?.(p);
}

/* ---------------------------------------------------------- campos */
/** Um campo de texto ligado ao XML: o valor muda o documento quando a pessoa sai do campo. */
function field(label, value, onCommit, { hint, multiline = false, type = "text", formula = false, small = false, title } = {}) {
  const input = multiline ? h("textarea", { rows: 3 }) : h("input", { type, step: type === "number" ? "any" : null });
  input.value = value ?? "";
  if (title) input.title = title;
  const check = () => {
    if (!formula) return;
    const err = formulaError(input.value);
    input.classList.toggle("bad", !!err);
    input.title = err ? t("chk.formula", label, input.value, err) : (title || "");
  };
  check();
  input.addEventListener("input", check);
  input.addEventListener("change", () => onCommit(input.value));
  if (small) return input;
  return h("label", { class: "field" }, h("span", { text: label }), input, hint ? h("small", { text: hint }) : null);
}

/** Uma lista de animações para escolher ("#3 walk"). */
function animSelect(value, onChange, { allowNone = false } = {}) {
  const s = h("select");
  if (allowNone) s.append(h("option", { value: "", text: "—" }));
  for (const a of state.doc.animations) {
    const id = a.getAttribute("id");
    s.append(h("option", { value: id, text: `#${id}  ${text(a, "name")}` }));
  }
  s.value = String(value);
  if (s.value !== String(value) && value !== "" && value !== null) s.append(h("option", { value: String(value), text: `#${value} ?` })), (s.value = String(value));
  s.addEventListener("change", () => onChange(s.value));
  return s;
}

/** A miniatura de um quadro numa tela deste tamanho. */
function thumb(frame, size, cls) {
  const c = h("canvas", { width: size, height: size, class: cls || null });
  const doc = state.doc;
  if (doc.sheet && doc.frameW) {
    const z = Math.min(size / doc.frameW, size / doc.frameH);
    const w = doc.frameW * z, hh = doc.frameH * z;
    doc.drawFrame(c.getContext("2d"), frame, (size - w) / 2, (size - hh) / 2, w, hh);
  }
  return c;
}

/* ---------------------------------------------------------- Projeto */
function pageProject(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("proj.title") }), h("p", { class: "intro", text: t("proj.intro") }));
  if (state.recovered && doc) {
    p.append(h("div", { class: "banner" }, h("span", { text: t("web.recovered") }),
      h("button", { class: "btn small", text: t("web.discardRecovered"), onclick: () => { clearLocal(); state.doc = null; state.recovered = false; render(); } })));
  }
  const choice = (em, title, hint, onclick, disabled = false) =>
    h("button", { class: "choice", onclick, disabled }, h("span", { class: "em", text: em }), h("b", { text: title }), h("span", { text: hint }));
  p.append(h("div", { class: "choices" },
    choice("✨", t("proj.new"), t("proj.newHint"), newPet),
    choice("🪄", t("proj.wizard"), t("proj.wizardHint"), () => {
      if (!confirmLeave()) return;
      wizardDialog(state.doc, (xml) => openDoc(xml, "animations.xml", { dirty: true }));
    }),
    choice("🐾", t("web.gallery"), t("web.galleryHint"), () => {
      if (!confirmLeave()) return;
      galleryDialog((xml) => openDoc(xml, "animations.xml"));
    }),
    choice("📂", t("proj.openFile"), t("proj.openFileHint"), async () => openFile(await pickFile(".xml,application/xml,text/xml"))),
    choice("🔗", t("web.openUrl"), t("web.openUrlHint"), openUrl),
    choice("💾", t("web.download"), t("web.downloadHint"), saveFile, !doc),
    choice("📦", t("proj.zip"), t("proj.zipHint"), exportZip, !doc),
    choice("🚀", t("proj.publish"), t("proj.publishHint"), publishGallery, !doc),
  ));
  const drop = h("div", { class: "drop", text: "⬇ animations.xml" });
  drop.addEventListener("dragover", (e) => { e.preventDefault(); drop.classList.add("over"); });
  drop.addEventListener("dragleave", () => drop.classList.remove("over"));
  drop.addEventListener("drop", (e) => { e.preventDefault(); drop.classList.remove("over"); openFile(e.dataTransfer.files[0]); });
  p.append(drop);
  if (doc) {
    p.append(h("div", { class: "card", style: "margin-top:16px" }, h("h2", { text: t("proj.thisPet", doc.petName || t("doc.untitled")) }),
      h("p", { text: t("proj.stats", doc.animations.length, doc.frameCount, doc.frameW, doc.frameH, doc.spawns.length, doc.childs.length, doc.sounds.length) })));
  }
}

/* ---------------------------------------------------------- Cabeçalho */
function pageHeader(p) {
  const doc = state.doc, hd = doc.section("header");
  p.append(h("h1", { text: t("head.title") }), h("p", { class: "intro", text: t("head.intro") }));
  const set = (name) => (v) => doc.set(hd, name, v);
  p.append(h("div", { class: "card" }, h("div", { class: "grid2" },
    field(t("head.petname"), text(hd, "petname"), set("petname"), { hint: t("head.petnameHint") }),
    field(t("head.titleField"), text(hd, "title"), set("title")),
    field(t("head.author"), text(hd, "author"), set("author")),
    field(t("head.version"), text(hd, "version"), set("version")),
  ), h("div", { style: "margin-top:12px" },
    field(t("head.info"), text(hd, "info").replace(/\[br\]/g, "\n"), (v) => doc.set(hd, "info", v.replace(/\r?\n/g, "[br]")), { multiline: true, hint: t("head.infoHint") }))));

  // o ícone: um .ico com o desenho em 16, 32, 48 e 64 px
  const icon = text(hd, "icon");
  const img = h("img", { width: 64, height: 64, style: "image-rendering:pixelated;background:var(--surface2);border-radius:8px", alt: "" });
  if (icon) img.src = "data:image/x-icon;base64," + icon.replace(/\s+/g, "");
  const frameNo = h("input", { type: "number", min: 0, max: Math.max(0, doc.frameCount - 1), value: firstFrame(doc), style: "width:80px" });
  p.append(h("div", { class: "card" }, h("h2", { text: t("head.icon") }), h("div", { class: "row" }, img,
    frameNo,
    h("button", { class: "btn", text: t("head.iconFrame"), disabled: !doc.sheet, onclick: async () => {
      doc.set(hd, "icon", await makeIco(frameCanvas(doc, +frameNo.value || 0)));
      render();
    } }),
    h("button", { class: "btn", text: t("head.iconFile"), onclick: async () => {
      const f = await pickFile("image/*");
      if (!f) return;
      const { png } = await imageFileToPng(f);
      const im = new Image();
      im.onload = async () => {
        const c = document.createElement("canvas");
        c.width = im.width; c.height = im.height; c.getContext("2d").drawImage(im, 0, 0);
        doc.set(hd, "icon", await makeIco(c));
        render();
      };
      im.src = "data:image/png;base64," + png;
    } })), h("p", { class: "hint", text: t("head.iconHint") })));
}

/* ---------------------------------------------------------- Folha de sprites */
function pageSheet(p) {
  const doc = state.doc, im = doc.section("image");
  p.append(h("h1", { text: t("sheet.title") }), h("p", { class: "intro", text: t("sheet.intro") }));
  const reload = async () => { await doc.loadSheet().catch(() => {}); render(); };
  const info = doc.sheet ? t("sheet.info", doc.sheet.width, doc.sheet.height, doc.frameCount, doc.frameW, doc.frameH) : t("sheet.empty");
  p.append(h("div", { class: "card" },
    h("div", { class: "grid2" },
      field(t("sheet.cols"), text(im, "tilesx", "1"), (v) => { doc.set(im, "tilesx", Math.max(1, parseInt(v, 10) || 1)); reload(); }, { type: "number" }),
      field(t("sheet.rows"), text(im, "tilesy", "1"), (v) => { doc.set(im, "tilesy", Math.max(1, parseInt(v, 10) || 1)); reload(); }, { type: "number" }),
      field(t("sheet.transp"), text(im, "transparency"), (v) => { doc.set(im, "transparency", v.trim()); reload(); }, { hint: t("sheet.transpHint") }),
    ),
    h("p", { class: "hint", style: "white-space:pre-line", text: info + (doc.sheet && (doc.sheet.width % doc.tilesX || doc.sheet.height % doc.tilesY) ? "\n" + t("sheet.notExact") : "") }),
    h("div", { class: "row" },
      h("button", { class: "btn", text: t("sheet.import"), onclick: async () => {
        const f = await pickFile("image/png,image/gif,image/bmp,image/*");
        if (!f) return;
        const { png, width, height } = await imageFileToPng(f);
        doc.set(im, "png", png);
        await doc.loadSheet().catch(() => {});
        toast(t("sheet.imported", width, height, doc.frameCount, doc.frameW, doc.frameH));
        render();
      } }),
      h("button", { class: "btn", text: t("build.button"), title: t("build.hint"), onclick: () =>
        buildSheetDialog(doc, async (msg) => { await doc.loadSheet().catch(() => {}); toast(msg, 6000); render(); }) }),
      h("button", { class: "btn", text: t("sheet.export"), disabled: !text(im, "png"), onclick: () =>
        download(folderName(doc) + "-sprites.png", new Blob([base64ToBytes(text(im, "png"))], { type: "image/png" })) })),
    h("p", { class: "hint", text: t("build.hint") })));
  if (!doc.sheet) return;
  p.append(h("p", { class: "hint", text: "✏️ " + t("web.sheetClick") }));
  // a folha inteira com a grade e o número de cada quadro
  const z = Math.max(1, Math.min(4, Math.floor(1400 / doc.sheet.width)));
  const c = h("canvas", { width: doc.sheet.width * z, height: doc.sheet.height * z });
  const g = c.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.drawImage(doc.sheet, 0, 0, c.width, c.height);
  g.strokeStyle = "rgba(32,150,82,.55)";
  g.fillStyle = "rgba(32,150,82,.9)";
  g.font = "11px system-ui";
  for (let i = 0; i < doc.frameCount; i++) {
    const x = (i % doc.tilesX) * doc.frameW * z, y = Math.floor(i / doc.tilesX) * doc.frameH * z;
    g.strokeRect(x + 0.5, y + 0.5, doc.frameW * z - 1, doc.frameH * z - 1);
    g.fillText(String(i), x + 3, y + 12);
  }
  // clicar num quadro abre o editor de pixels dele
  c.title = t("px.button");
  c.addEventListener("click", (e) => {
    const r = c.getBoundingClientRect();
    const col = Math.floor((e.clientX - r.left) * (c.width / r.width) / (doc.frameW * z));
    const row = Math.floor((e.clientY - r.top) * (c.height / r.height) / (doc.frameH * z));
    const i = row * doc.tilesX + col;
    if (col >= 0 && col < doc.tilesX && i >= 0 && i < doc.frameCount) pixelEditor(doc, i, async () => { await doc.loadSheet().catch(() => {}); render(); });
  });
  p.append(h("div", { class: "sheetwrap" }, c));
}

/* ---------------------------------------------------------- Animações */
function pageAnimations(p) {
  const doc = state.doc;
  const wrap = h("div", { class: "split" });
  p.append(wrap);
  // a lista, com busca
  const items = h("div", { class: "items" });
  const search = h("input", { type: "text", placeholder: t("anim.search"), value: state.search });
  // as miniaturas tocam a animação (só as que aparecem na lista)
  const live = new LiveSet(doc);
  state.cleanup.push(() => live.stop());
  const fill = () => {
    live.stop();
    items.textContent = "";
    const q = state.search.trim().toLowerCase();
    const probs = state.problems;
    for (const a of doc.animations) {
      const id = +a.getAttribute("id"), name = text(a, "name");
      if (q && !name.toLowerCase().includes(q) && String(id) !== q) continue;
      const frames = kids(kid(a, "sequence"), "frame");
      const mine = probs.filter((x) => x.anim === id);
      items.append(h("div", { class: "item" + (state.anim === id ? " on" : ""), onclick: () => { state.anim = id; render(); } },
        (() => { const c = h("canvas", { width: 36, height: 36 }); live.add(c, a); return c; })(),
        h("div", { class: "t" }, h("b", { text: name || "—" }), h("small", { text: frames.length === 1 ? t("anim.sub1", id) : t("anim.sub", id, frames.length) })),
        mine.length ? h("span", { class: mine.some((x) => x.error) ? "err" : "warn", text: mine.some((x) => x.error) ? "●" : "▲", title: mine.map((x) => x.text).join("\n") }) : null));
    }
    if (!doc.animations.length) items.append(h("p", { class: "hint", style: "padding:10px", text: t("anim.none") }));
  };
  search.addEventListener("input", () => { state.search = search.value; fill(); });
  fill();
  const newBtn = h("button", { class: "btn small primary", text: "+ " + t("anim.new") + " ▾", onclick: () => {
    const r = newBtn.getBoundingClientRect();
    popupMenu(r.left, r.bottom + 4, [
      { text: t("anim.newBlank"), onClick: () => addAnimation() },
      { text: t("anim.newModel"), onClick: () => behaviorDialog(doc, (id) => { state.anim = id; render(); }) },
    ]);
  } });
  const boardBtn = h("button", { class: "btn small", text: "▦ " + t("web.seeAll"), title: t("web.boardIntro"), onclick: () => go("board") });
  wrap.append(h("div", { class: "list" }, h("div", { class: "head" }, h("b", { text: t("anim.count", doc.animations.length) }), search, h("div", { class: "row2" }, newBtn, boardBtn)), items));
  const a = doc.animById(state.anim) || doc.animations[0];
  if (!a) { wrap.append(h("div")); return; }
  state.anim = +a.getAttribute("id");
  wrap.append(animationEditor(a));
}

/* ---------------------------------------------------------- Todas as animações: um quadro com todas tocando juntas */
function pageBoard(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("nav.board") }), h("p", { class: "intro", text: t("web.boardIntro") }));
  const live = new LiveSet(doc);
  state.cleanup.push(() => live.stop());
  const SIZES = [["web.sizeS", 64], ["web.sizeM", 96], ["web.sizeL", 144]];
  let size = 96;
  try { size = +localStorage.getItem("deskpals-editor-board") || 96; } catch { /* sem armazenamento */ }
  const grid = h("div", { class: "board" });
  const search = h("input", { type: "text", placeholder: t("anim.search"), value: state.search, style: "max-width:260px" });
  const playBtn = h("button", { class: "btn", onclick: () => { live.play(!live.playing); label(); } });
  const label = () => { playBtn.textContent = live.playing ? "⏸ " + t("web.pause") : "▶ " + t("web.play"); };
  label();
  const sizeSel = h("select", { title: t("web.size"), style: "width:auto" }, ...SIZES.map(([k, v]) => h("option", { value: String(v), text: t(k) })));
  sizeSel.value = String(SIZES.some(([, v]) => v === size) ? size : 96);
  const fill = () => {
    live.stop();
    grid.textContent = "";
    grid.style.setProperty("--cell", size + "px");
    const q = state.search.trim().toLowerCase();
    for (const a of doc.animations) {
      const id = +a.getAttribute("id"), name = text(a, "name");
      if (q && !name.toLowerCase().includes(q) && String(id) !== q) continue;
      const n = kids(kid(a, "sequence"), "frame").length;
      const c = h("canvas", { width: size, height: size });
      grid.append(h("button", { class: "bcard", title: name + " (#" + id + ")", onclick: () => go("animations", id) },
        c, h("b", { text: name || "—" }), h("small", { text: n === 1 ? t("anim.sub1", id) : t("anim.sub", id, n) })));
      live.add(c, a);
    }
    if (!grid.childElementCount) grid.append(h("p", { class: "hint", text: doc.animations.length ? "—" : t("anim.none") }));
  };
  search.addEventListener("input", () => { state.search = search.value; fill(); });
  sizeSel.addEventListener("change", () => { size = +sizeSel.value; try { localStorage.setItem("deskpals-editor-board", String(size)); } catch { /* fica só nesta visita */ } fill(); });
  p.append(h("div", { class: "row", style: "margin-bottom:12px;gap:8px;flex-wrap:wrap;align-items:center" },
    playBtn, h("button", { class: "btn", text: "↻ " + t("web.restartAll"), onclick: () => live.restart() }),
    h("label", { class: "hint", text: t("web.size") }), sizeSel, search), grid);
  fill();
}

function addAnimation() {
  const doc = state.doc;
  const id = doc.nextFreeAnimId();
  doc.change(() => {
    const el = new DOMParser().parseFromString(`<animation xmlns="${doc.ns}" id="${id}"><name>${t("anim.newName")}</name>` +
      `<start><x>0</x><y>0</y><offsety>0</offsety><opacity>1</opacity><interval>200</interval></start>` +
      `<end><x>0</x><y>0</y><offsety>0</offsety><opacity>1</opacity><interval>200</interval></end>` +
      `<sequence repeatfrom="0" repeat="0"><frame>0</frame></sequence></animation>`, "application/xml").documentElement;
    doc.insertOrdered(doc.animationsEl, doc.xml.importNode(el, true));
  });
  state.anim = id;
  render();
}

function animationEditor(a) {
  const doc = state.doc;
  const id = +a.getAttribute("id");
  const seq = doc.ensure(a, "sequence");
  const box = h("div");
  const name = text(a, "name");
  const keyText = { drag: "key.drag", fall: "key.fall", kill: "key.kill", sync: "key.sync", toss: "key.toss", "fall soft": "key.fallSoft", "fall hard": "key.fallHard" }[name.toLowerCase()];

  // a prévia
  const canvas = h("canvas", { width: 300, height: 200 });
  const player = new AnimPlayer(canvas, doc);
  state.cleanup.push(() => player.stop());
  const playBtn = h("button", { class: "btn small", text: "⏸ " + t("web.pause"), onclick: () => { player.play(!player.playing); playBtn.textContent = player.playing ? "⏸ " + t("web.pause") : "▶ " + t("web.play"); } });
  const walkBtn = h("button", { class: "btn small", text: t("web.inPlace"), onclick: () => { player.walk = !player.walk; walkBtn.textContent = player.walk ? t("web.moving") : t("web.inPlace"); player.restart(); } });
  // um passo de cada vez (pausa a prévia) e o quadro anterior por baixo, meio apagado
  const stepBtn = h("button", { class: "btn small", text: "⏭ " + t("preview.step"), onclick: () => {
    player.play(false); playBtn.textContent = "▶ " + t("web.play");
    player.stepOnce();
  } });
  const onion = toggle(t("preview.onion"), false, (v) => { player.onion = v; player.draw(); });
  const stepInfo = h("small", { class: "hint" });
  const refresh = () => { player.read(); fillFrames(); };

  box.append(h("div", { class: "card" },
    h("div", { class: "row", style: "justify-content:space-between" },
      h("h1", { style: "margin:0", text: t("anim.idLabel", id) }),
      h("div", { class: "row" },
        h("button", { class: "btn small", text: "▶ " + t("web.testThis"), title: t("web.testThisHint"), onclick: () => { state.testAnim = id; go("test"); } }),
        h("button", { class: "btn small", text: t("anim.duplicate"), onclick: () => duplicateAnimation(a) }),
        h("button", { class: "btn small danger", text: t("anim.delete"), onclick: () => deleteAnimation(a) }))),
    keyText ? h("div", { class: "keyhint", text: t(keyText) }) : null,
    h("div", { class: "preview", style: "margin-top:10px" },
      h("div", {}, canvas, h("div", { class: "row", style: "margin-top:6px" }, playBtn, stepBtn, walkBtn), h("div", { class: "row", style: "margin-top:6px" }, onion, stepInfo)),
      h("div", { style: "flex:1;min-width:220px", class: "grid2" },
        field(t("web.name"), name, (v) => { doc.set(a, "name", v.trim()); render(); }),
        field(t("anim.repeat"), seq.getAttribute("repeat") || "0", (v) => { doc.setAttr(seq, "repeat", v.trim() || "0"); player.restart(); }, { formula: true, hint: t("anim.repeatHint") }),
        field(t("anim.repeatFrom"), seq.getAttribute("repeatfrom") || "0", (v) => { doc.setAttr(seq, "repeatfrom", Math.max(0, parseInt(v, 10) || 0)); player.restart(); }, { type: "number" }),
        h("label", { class: "field" }, h("span", { text: t("anim.action") }), (() => {
          const s = h("select", {}, h("option", { value: "none", text: "—" }), h("option", { value: "flip", text: "flip" }));
          s.value = (text(seq, "action") || seq.getAttribute("action") || "none").toLowerCase() === "flip" ? "flip" : "none";
          s.addEventListener("change", () => {
            doc.change(() => {
              const act = kid(seq, "action");
              if (s.value === "none") { if (act) doc.removeEl(act); seq.removeAttribute("action"); }
              else { seq.removeAttribute("action"); (act || doc.insertOrdered(seq, doc.make("action"))).textContent = "flip"; }
            });
            player.restart();
          });
          return s;
        })(), h("small", { text: t("anim.actionHint") })),
        h("label", { class: "row", style: "gap:6px" }, (() => {
          const cb = h("input", { type: "checkbox", checked: a.getAttribute("picker") !== "no" });
          cb.addEventListener("change", () => doc.change(() => { if (cb.checked) a.removeAttribute("picker"); else a.setAttribute("picker", "no"); }));
          return cb;
        })(), t("anim.inPicker")),
      ))));

  // os quadros: arrastar para mudar a ordem, × para tirar, clicar na folha para acrescentar
  const strip = h("div", { class: "frames" });
  let dragFrom = -1;
  const fillFrames = () => {
    strip.textContent = "";
    kids(seq, "frame").forEach((fEl, i) => {
      const n = parseInt(fEl.textContent, 10) || 0;
      const cell = h("div", { class: "frame", draggable: "true", "data-i": i },
        thumb(n, 48), h("small", { text: String(n) }),
        h("button", { class: "x", text: "×", title: t("web.remove"), onclick: (e) => { e.stopPropagation(); doc.change(() => doc.removeEl(fEl)); refresh(); } }));
      cell.addEventListener("dragstart", () => { dragFrom = i; cell.classList.add("dragging"); });
      cell.addEventListener("dragend", () => cell.classList.remove("dragging"));
      cell.addEventListener("dragover", (e) => e.preventDefault());
      cell.addEventListener("drop", (e) => {
        e.preventDefault();
        if (dragFrom < 0 || dragFrom === i) return;
        const all = kids(seq, "frame");
        const moving = all[dragFrom], target = all[i];
        doc.change(() => {
          const ws = moving.previousSibling;
          if (dragFrom < i) target.after(moving); else target.before(moving);
          if (ws && ws.nodeType === 3) moving.before(ws.cloneNode());   // a indentação vai junto
          if (ws && ws.nodeType === 3 && ws.parentNode) ws.remove();
        });
        dragFrom = -1;
        refresh();
      });
      // o menu do quadro: segurar mais, repetir no fim, inverter, editar os pixels, tirar
      const menu = (x, y) => popupMenu(x, y, [
        { text: t("strip.hold", 2), onClick: () => { doc.change(() => fEl.after(doc.make("frame", n))); refresh(); } },
        { text: t("strip.duplicateEnd"), onClick: () => { doc.change(() => doc.insertOrdered(seq, doc.make("frame", n))); refresh(); } },
        { text: t("strip.reverse"), onClick: () => {
          doc.change(() => {
            const list = kids(seq, "frame"), values = list.map((f) => f.textContent).reverse();
            list.forEach((f, k) => { f.textContent = values[k]; });
          });
          refresh();
        } },
        { text: t("strip.pixels"), onClick: () => pixelEditor(doc, n, async () => { await doc.loadSheet().catch(() => {}); render(); }) },
        "-",
        { text: t("strip.remove"), danger: true, onClick: () => { doc.change(() => doc.removeEl(fEl)); refresh(); } },
      ]);
      cell.addEventListener("contextmenu", (e) => { e.preventDefault(); menu(e.clientX, e.clientY); });
      cell.addEventListener("dblclick", () => pixelEditor(doc, n, async () => { await doc.loadSheet().catch(() => {}); render(); }));
      strip.append(cell);
    });
  };
  player.onFrame = (step) => {
    const n = kids(seq, "frame").length;
    const idx = n ? (step < n ? step : Math.min(parseInt(seq.getAttribute("repeatfrom") || "0", 10) || 0, n - 1) + ((step - n) % Math.max(1, n - Math.min(parseInt(seq.getAttribute("repeatfrom") || "0", 10) || 0, n - 1)))) : -1;
    strip.querySelectorAll(".frame").forEach((el, i) => el.classList.toggle("now", i === idx));
    stepInfo.textContent = player.total ? t("preview.stepOf", step + 1, player.total) : "";
  };
  fillFrames();
  const picker = h("div", { class: "picker" });
  for (let i = 0; i < doc.frameCount; i++)
    picker.append(h("button", { title: t("web.frameN", i), onclick: () => {
      doc.change(() => doc.insertOrdered(seq, doc.make("frame", i)));
      refresh();
    } }, thumb(i, 40), h("small", { text: String(i) })));
  box.append(h("div", { class: "card" }, h("h2", { text: t("anim.frames") }), strip,
    h("p", { class: "hint", text: t("web.addFrame") }), picker));

  // o movimento: do início ao fim
  const st = doc.ensure(a, "start"), en = doc.ensure(a, "end");
  const rows = [["x", "move.x", true], ["y", "move.y", true], ["offsety", "move.offset", false], ["opacity", "move.opacity", false], ["interval", "move.interval", true]];
  const tbl = h("table", { class: "tbl" }, h("tr", {}, h("th"), h("th", { text: t("move.start") }), h("th", { text: t("move.end") })));
  for (const [key, label, formula] of rows) {
    const fallback = key === "opacity" ? "1" : key === "interval" ? "200" : "0";
    tbl.append(h("tr", {}, h("td", { text: t(label) }),
      h("td", {}, field(t(label), text(st, key, fallback), (v) => { doc.set(st, key, v.trim()); player.read(); }, { small: true, formula })),
      h("td", {}, field(t(label), text(en, key, fallback), (v) => { doc.set(en, key, v.trim()); player.read(); }, { small: true, formula }))));
  }
  box.append(h("div", { class: "card" }, h("h2", { text: t("anim.move") }), tbl, h("p", { class: "hint", text: t("anim.moveHint") })));

  // as próximas: ao terminar, numa borda, sem chão
  box.append(h("div", { class: "card" }, h("h2", { text: t("next.end") }), nextList(a, "sequence"), h("p", { class: "hint", text: t("next.hint") })));
  box.append(h("div", { class: "card" }, h("h2", { text: t("next.border") }), nextList(a, "border")));
  box.append(h("div", { class: "card" }, h("h2", { text: t("next.gravity") }), nextList(a, "gravity")));

  // onde ela é usada: quem leva até ela (um clique vai lá)
  box.append(h("div", { class: "card" }, h("h2", { text: t("web.usedBy") }), usesOf(id)));

  player.setAnimation(a);
  return box;
}

/** O que leva a uma animação: outras animações, entradas, janelas filhas e sons. */
function usesOf(id) {
  const doc = state.doc;
  const list = h("ul", { class: "uses" });
  const item = (label, onclick) => list.append(h("li", {}, h("a", { href: "#", onclick: (e) => { e.preventDefault(); onclick(); }, text: label })));
  for (const o of doc.animations) {
    const oid = +o.getAttribute("id");
    if (oid === id) continue;
    if (["sequence", "border", "gravity"].some((w) => kids(kid(o, w), "next").some((n) => +n.textContent === id)))
      item(t("uses.anim", text(o, "name"), oid), () => go("animations", oid));
  }
  for (const s of doc.spawns) if (+text(s, "next") === id) item(t("uses.spawn", s.getAttribute("id")), () => go("spawns"));
  for (const c of doc.childs) if (+text(c, "next") === id || +c.getAttribute("animationid") === id) item(t("uses.child", c.getAttribute("animationid")), () => go("childs"));
  if (doc.sounds.some((s) => +s.getAttribute("animationid") === id)) item(t("uses.sound"), () => go("sounds"));
  if (!list.children.length) list.append(h("li", { class: "hint", text: t("web.usedByNone") }));
  return list;
}

const ONLY = ["none", "taskbar", "window", "horizontal", "horizontal+", "vertical", "vertical+", "windowSide", "petSide"];

/** As próximas de uma lista (a sequência, a borda ou a gravidade): animação, chance e onde vale. */
function nextList(a, where) {
  const doc = state.doc;
  const box = h("div");
  const holder = () => kid(a, where);
  const fill = () => {
    box.textContent = "";
    const list = kids(holder(), "next");
    if (!list.length && where === "sequence") box.append(h("p", { class: "hint", text: t("next.none") }));
    for (const n of list) {
      const only = h("select", {}, ...ONLY.map((o) => h("option", { value: o, text: t("only." + o) })));
      only.value = n.getAttribute("only") || "none";
      only.addEventListener("change", () => doc.setAttr(n, "only", only.value));
      box.append(h("div", { class: "nextrow" },
        animSelect(parseInt(n.textContent, 10) || 0, (v) => doc.change(() => { n.textContent = v; })),
        field(t("web.chance"), n.getAttribute("probability") || "100", (v) => doc.setAttr(n, "probability", Math.max(0, parseInt(v, 10) || 0)), { small: true, type: "number", title: t("web.chance") }),
        only,
        h("button", { class: "btn small danger", text: "×", title: t("web.remove"), onclick: () => {
          doc.change(() => {
            const parent = n.parentNode;
            doc.removeEl(n);
            // uma borda ou gravidade sem próxima nenhuma deixa de existir (o pet não procura mais por ela)
            if (where !== "sequence" && !kids(parent, "next").length) doc.removeEl(parent);
          });
          fill();
        } })));
    }
    box.append(h("button", { class: "btn small", text: "+ " + t("next.add"), onclick: () => {
      doc.change(() => {
        const parent = where === "sequence" ? doc.ensure(a, "sequence") : doc.ensure(a, where);
        const el = doc.make("next", a.getAttribute("id"));
        el.setAttribute("probability", "100");
        el.setAttribute("only", "none");
        doc.insertOrdered(parent, el);
      });
      fill();
    } }));
  };
  fill();
  return box;
}

function duplicateAnimation(a) {
  const doc = state.doc;
  const id = doc.nextFreeAnimId();
  doc.change(() => {
    const copy = a.cloneNode(true);
    copy.setAttribute("id", id);
    const nm = kid(copy, "name");
    if (nm) nm.textContent = text(a, "name") + " " + t("anim.copy");
    const ws = a.previousSibling && a.previousSibling.nodeType === 3 ? a.previousSibling.cloneNode() : null;
    a.after(copy);
    if (ws) copy.before(ws);
  });
  state.anim = id;
  render();
}

function deleteAnimation(a) {
  const doc = state.doc;
  const id = a.getAttribute("id"), name = text(a, "name");
  const users = [];
  for (const o of doc.animations) for (const w of ["sequence", "border", "gravity"]) for (const n of kids(kid(o, w), "next"))
    if (n.textContent.trim() === id && o !== a) users.push(`“${text(o, "name")}” (#${o.getAttribute("id")})`);
  const ask = users.length ? t("anim.deleteUsed", name, [...new Set(users)].join("\n• ")) : t("anim.deleteAsk", name);
  if (!confirm(ask)) return;
  doc.change(() => {
    doc.removeEl(a);
    // ela sai também das listas de próximas, dos sons e das janelas filhas
    for (const o of doc.animations) for (const w of ["sequence", "border", "gravity"]) {
      const parent = kid(o, w);
      for (const n of kids(parent, "next")) if (n.textContent.trim() === id) doc.removeEl(n);
      if (parent && w !== "sequence" && !kids(parent, "next").length) doc.removeEl(parent);
    }
    for (const s of doc.sounds) if (s.getAttribute("animationid") === id) doc.removeEl(s);
    for (const c of doc.childs) if (c.getAttribute("animationid") === id || text(c, "next") === id) doc.removeEl(c);
  });
  state.anim = doc.animations.length ? +doc.animations[0].getAttribute("id") : null;
  render();
}

/* ---------------------------------------------------------- Entradas na tela */
function pageSpawns(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("nav.spawns") }), h("p", { class: "intro", text: t("spawns.intro") }));
  const tbl = h("table", { class: "tbl" }, h("tr", {}, ...["#", t("spawns.prob"), t("spawns.x"), t("spawns.y"), t("spawns.next"), ""].map((x) => h("th", { text: x }))));
  for (const s of doc.spawns) {
    const next = doc.ensure(s, "next");
    tbl.append(h("tr", {},
      h("td", { text: s.getAttribute("id") }),
      h("td", { style: "width:90px" }, field(t("web.chance"), s.getAttribute("probability") || "100", (v) => doc.setAttr(s, "probability", Math.max(0, parseInt(v, 10) || 0)), { small: true, type: "number" })),
      h("td", {}, field("x", text(s, "x"), (v) => doc.set(s, "x", v.trim()), { small: true, formula: true })),
      h("td", {}, field("y", text(s, "y"), (v) => doc.set(s, "y", v.trim()), { small: true, formula: true })),
      h("td", {}, animSelect(next.textContent.trim(), (v) => doc.change(() => { next.textContent = v; }))),
      h("td", {}, h("button", { class: "btn small danger", text: "×", title: t("web.remove"), onclick: () => { doc.change(() => doc.removeEl(s)); render(); } }))));
    // onde ela põe o pet, em palavras, e as posições prontas
    const preset = (key, x, y) => h("button", { class: "btn small", text: t(key), onclick: () => { doc.change(() => { doc.set(s, "x", x); doc.set(s, "y", y); }); render(); } });
    tbl.append(h("tr", { class: "sub" }, h("td"), h("td", { colspan: 5 }, h("div", { class: "row" },
      h("span", { class: "hint", text: "📍 " + whereText(text(s, "x"), text(s, "y")) }),
      h("span", { class: "hint", text: "· " + t("spawns.quick") + ":" }),
      preset("spawns.qGround", "random*(screenW-imageW-50)/100+25", "areaH-imageH"),
      preset("spawns.qRight", "screenW", "areaH-imageH"),
      preset("spawns.qSky", "random*(screenW-imageW-50)/100+25", "-imageH")))));
  }
  p.append(h("div", { class: "card" }, tbl, h("div", { class: "row", style: "margin-top:10px" },
    h("button", { class: "btn small", text: "+ " + t("list.add"), onclick: () => {
      const id = doc.nextFreeSpawnId();
      const first = doc.animations[0] ? doc.animations[0].getAttribute("id") : "1";
      doc.change(() => {
        const el = new DOMParser().parseFromString(`<spawn xmlns="${doc.ns}" id="${id}" probability="100"><x>random*(screenW-imageW-50)/100+25</x><y>areaH-imageH</y><next probability="100">${first}</next></spawn>`, "application/xml").documentElement;
        doc.insertOrdered(doc.spawnsEl, doc.xml.importNode(el, true));
      });
      render();
    } })), formulaHelp()));
}

/** Onde uma entrada põe o pet, em palavras (pelas fórmulas de costume). */
function whereText(xs, ys) {
  const x = (xs || "").replace(/\s+/g, ""), y = (ys || "").replace(/\s+/g, "");
  const height = y.includes("areaH-imageH") ? t("where.taskbar") : y.startsWith("-imageH") || y.startsWith("0-imageH") ? t("where.sky")
    : y.startsWith("areaH") || y.startsWith("screenH") ? t("where.below") : y.includes("random") ? t("where.randomHeight") : t("where.height", y);
  const side = x.startsWith("screenW") ? t("where.side") : x.includes("random") ? t("where.random") : x === "0" || x.startsWith("-imageW") ? t("where.edge") : t("where.x", x);
  return t("where.both", height, side);
}

function formulaHelp() {
  return h("p", { class: "hint", text: "screenW · screenH · areaW · areaH · imageW · imageH · imageX · imageY · random (0–99) · randS · + − * / ( )" });
}

/* ---------------------------------------------------------- Janelas filhas */
function pageChilds(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("nav.childs") }), h("p", { class: "intro", text: t("childs.intro") }));
  const tbl = h("table", { class: "tbl" }, h("tr", {}, ...[t("childs.when"), "x", "y", t("childs.anim"), ""].map((x) => h("th", { text: x }))));
  for (const c of doc.childs) {
    const next = doc.ensure(c, "next");
    tbl.append(h("tr", {},
      h("td", {}, animSelect(c.getAttribute("animationid"), (v) => doc.setAttr(c, "animationid", v))),
      h("td", {}, field("x", text(c, "x"), (v) => doc.set(c, "x", v.trim()), { small: true, formula: true })),
      h("td", {}, field("y", text(c, "y"), (v) => doc.set(c, "y", v.trim()), { small: true, formula: true })),
      h("td", {}, animSelect(next.textContent.trim(), (v) => doc.change(() => { next.textContent = v; }))),
      h("td", {}, h("button", { class: "btn small danger", text: "×", title: t("web.remove"), onclick: () => { doc.change(() => doc.removeEl(c)); render(); } }))));
  }
  p.append(h("div", { class: "card" }, doc.childs.length ? tbl : h("p", { class: "hint", text: "—" }), h("div", { class: "row", style: "margin-top:10px" },
    h("button", { class: "btn small", text: "+ " + t("list.add"), onclick: () => {
      const first = doc.animations[0] ? doc.animations[0].getAttribute("id") : "1";
      doc.change(() => {
        const el = new DOMParser().parseFromString(`<child xmlns="${doc.ns}" animationid="${first}"><x>imageX</x><y>imageY</y><next>${first}</next></child>`, "application/xml").documentElement;
        doc.insertOrdered(doc.childsEl, doc.xml.importNode(el, true));
      });
      render();
    } })), formulaHelp()));
}

/* ---------------------------------------------------------- Sons */
function pageSounds(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("nav.sounds") }), h("p", { class: "intro", text: t("sounds.intro") }));
  let audio = null;
  state.cleanup.push(() => audio && audio.pause());
  const tbl = h("table", { class: "tbl" }, h("tr", {}, ...[t("sounds.anim"), t("sounds.prob"), t("sounds.loop"), "", "KB", ""].map((x) => h("th", { text: x }))));
  for (const s of doc.sounds) {
    const b64 = text(s, "base64");
    tbl.append(h("tr", {},
      h("td", {}, animSelect(s.getAttribute("animationid"), (v) => doc.setAttr(s, "animationid", v))),
      h("td", { style: "width:90px" }, field(t("web.chance"), text(s, "probability", "100"), (v) => doc.set(s, "probability", Math.max(0, parseInt(v, 10) || 0)), { small: true, type: "number" })),
      h("td", { style: "width:80px" }, field("loop", text(s, "loop", "0"), (v) => doc.set(s, "loop", Math.max(0, parseInt(v, 10) || 0)), { small: true, type: "number" })),
      h("td", {}, h("div", { class: "row" },
        h("button", { class: "btn small", text: "▶", title: t("sounds.play"), disabled: !b64, onclick: () => { if (audio) audio.pause(); audio = new Audio("data:audio/mpeg;base64," + b64.replace(/\s+/g, "")); audio.play().catch(() => {}); } }),
        h("button", { class: "btn small", text: "MP3…", onclick: async () => {
          const f = await pickFile("audio/mpeg,.mp3");
          if (!f) return;
          doc.set(s, "base64", bytesToBase64(new Uint8Array(await f.arrayBuffer())));
          render();
        } }))),
      h("td", { text: String(Math.round((b64.length * 3) / 4 / 1024)) }),
      h("td", {}, h("button", { class: "btn small danger", text: "×", title: t("web.remove"), onclick: () => { doc.change(() => doc.removeEl(s)); render(); } }))));
  }
  p.append(h("div", { class: "card" }, doc.sounds.length ? tbl : h("p", { class: "hint", text: "—" }), h("div", { class: "row", style: "margin-top:10px" },
    h("button", { class: "btn small", text: "+ MP3…", onclick: async () => {
      const f = await pickFile("audio/mpeg,.mp3");
      if (!f) return;
      const b64 = bytesToBase64(new Uint8Array(await f.arrayBuffer()));
      const first = state.anim ?? (doc.animations[0] ? doc.animations[0].getAttribute("id") : 1);
      doc.change(() => {
        const el = doc.make("sound");
        el.setAttribute("animationid", first);
        doc.insertOrdered(doc.soundsEl, el);
        el.append(doc.make("probability", 100), doc.make("loop", 0));
        const data = doc.make("base64");
        data.appendChild(doc.xml.createCDATASection(b64));
        el.append(data);
      });
      render();
    } }))));
}

/* ---------------------------------------------------------- Verificador */
function pageCheck(p) {
  refreshProblems();
  const list = state.problems;
  p.append(h("h1", { text: t("nav.check") }), h("p", { class: "intro", text: t("chk.intro") }));
  const box = h("div", { class: "card", style: "padding:0" });
  if (!list.length) box.append(h("div", { class: "ok", text: "✓ " + t("act.checkOk") }));
  for (const x of list.slice().sort((a, b) => b.error - a.error))
    box.append(h("div", { class: "problem", onclick: () => go(x.page, x.anim) },
      h("span", { class: "ico", text: x.error ? "⛔" : "⚠️" }), h("span", { text: x.text })));
  p.append(box);
}

/* ---------------------------------------------------------- XML */
function pageXml(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("xml.title") }), h("p", { class: "intro", text: t("xml.intro") }));
  // os blocos grandes viram marcas («imagem», «ícone», «som N») e voltam ao aplicar
  const blobs = new Map();
  let soundN = 0;
  const full = doc.serialize();
  const shown = full.replace(/<(png|icon|base64)>([\s\S]*?)<\/\1>/g, (m, tag, body) => {
    if (!body.trim()) return m;
    const mark = tag === "png" ? "«" + t("xml.markImage") + "»" : tag === "icon" ? "«" + t("xml.markIcon") + "»" : "«" + t("xml.markSound") + " " + ++soundN + "»";
    blobs.set(mark, body);
    return `<${tag}>${mark}</${tag}>`;
  });
  const area = h("textarea", { class: "xml", spellcheck: "false" });
  area.value = shown;
  const status = h("span", { class: "hint" });
  const find = h("input", { type: "text", placeholder: t("xml.find"), style: "width:220px" });
  area.addEventListener("input", () => { status.textContent = t("xml.edited"); });
  p.append(h("div", { class: "card" }, h("div", { class: "row", style: "margin-bottom:10px" },
    h("button", { class: "btn primary", text: t("xml.apply"), onclick: () => {
      let s = area.value;
      for (const [mark, body] of blobs) s = s.split(mark).join(body);
      try { new PetDoc(s); } catch { toast(t("xml.bad")); return; }
      doc.change(() => doc.restore(s), "full");
      toast(t("xml.applied"));
    } }),
    h("button", { class: "btn", text: t("xml.discard"), onclick: () => render() }),
    h("button", { class: "btn", text: t("xml.copy"), onclick: async () => { try { await navigator.clipboard.writeText(full); toast(t("xml.copied")); } catch { /* sem área de transferência */ } } }),
    find, status), area));
  // buscar: Enter vai para a próxima (e volta ao começo depois da última)
  find.addEventListener("keydown", (e) => {
    if (e.key !== "Enter" || !find.value) return;
    e.preventDefault();
    const hay = area.value.toLowerCase(), needle = find.value.toLowerCase();
    let i = hay.indexOf(needle, area.selectionEnd);
    if (i < 0) i = hay.indexOf(needle);
    if (i < 0) { status.textContent = t("xml.notFound"); return; }
    status.textContent = "";
    area.focus();
    area.setSelectionRange(i, i + needle.length);
    const line = area.value.slice(0, i).split("\n").length - 1;
    area.scrollTop = Math.max(0, line * parseFloat(getComputedStyle(area).lineHeight || "18") - area.clientHeight / 3);
    find.focus();
  });
}

/* ---------------------------------------------------------- Testar */
function pageTest(p) {
  const doc = state.doc;
  p.append(h("h1", { text: t("act.test") }), h("p", { class: "intro", text: t("web.testIntro") }));
  const stage = h("div", { class: "stage" });
  const canvas = h("canvas");
  const winEl = h("div", { class: "win" });
  stage.append(winEl, h("div", { class: "taskbar" }), canvas);
  const restart = h("button", { class: "btn", text: "↻ " + t("preview.restart"), onclick: () => start() });
  const sky = h("button", { class: "btn", text: "☁ " + t("web.fromSky"), onclick: () => { if (pet) pet.arriveFromSky(); } });
  const pick = h("select", { title: t("web.playNow"), style: "width:auto;max-width:280px" }, h("option", { value: "", text: t("web.playNow") + "…" }),
    ...doc.animations.map((a) => h("option", { value: a.getAttribute("id"), text: `#${a.getAttribute("id")}  ${text(a, "name")}` })));
  pick.addEventListener("change", () => { if (pet && pick.value) pet.setNewAnimation(+pick.value); pick.value = ""; });
  const playing = h("span", { class: "hint" });
  p.append(h("div", { class: "row", style: "margin-bottom:10px" }, restart, sky, pick, playing), stage,
    h("p", { class: "hint", text: t("web.testDrag") }));

  const world = new World();
  let pet = null, raf = 0, url = null, drag = null;
  const g = canvas.getContext("2d");
  const measure = () => {
    world.width = Math.max(320, stage.clientWidth);
    world.height = Math.max(240, stage.clientHeight);
    canvas.width = world.width; canvas.height = world.height;
    const w = { id: 1, title: "", left: Math.round(world.width * 0.15), top: Math.round(world.height * 0.45), right: Math.round(world.width * 0.6), bottom: world.height - 70, z: 1, gone: false };
    world.windows = [w];
    Object.assign(winEl.style, { left: w.left + "px", top: w.top + "px", width: w.right - w.left + "px", height: w.bottom - w.top + "px" });
  };
  const draw = (target, now) => {
    if (!target.sheet) return;
    const s = target.renderState(now);
    const total = target.tilesX * target.tilesY;
    const frame = Math.max(0, Math.min(total - 1, s.frame));
    const sx = (frame % target.tilesX) * target.tileWidth, sy = Math.floor(frame / target.tilesX) * target.tileHeight;
    // a sombra cola o pet no chão (no topo da janela), como no programa
    if (target.window && !target.isChild) {
      g.save(); g.globalAlpha = 0.22; g.fillStyle = "#000";
      g.beginPath(); g.ellipse(s.x + s.width / 2, s.y + s.height - 3, s.width * 0.3, 4, 0, 0, Math.PI * 2); g.fill(); g.restore();
    }
    g.save();
    g.imageSmoothingEnabled = false;
    g.globalAlpha = Math.max(0, Math.min(1, s.opacity));
    if (s.flipped) { g.translate(s.x + s.width, s.y); g.scale(-1, 1); g.drawImage(target.sheet, sx, sy, target.tileWidth, target.tileHeight, 0, 0, s.width, s.height); }
    else g.drawImage(target.sheet, sx, sy, target.tileWidth, target.tileHeight, s.x, s.y, s.width, s.height);
    g.restore();
  };
  let shown = "";
  const loop = (now) => {
    raf = requestAnimationFrame(loop);
    if (!pet) return;
    try {
      if (stage.clientWidth !== world.width || stage.clientHeight !== world.height) measure();
      pet.update(now);
      g.clearRect(0, 0, canvas.width, canvas.height);
      draw(pet, now);
      for (const c of pet.children) draw(c, now);
      const name = pet.current ? pet.current.name + " (#" + pet.current.id + ")" : "";
      if (name !== shown) { shown = name; playing.textContent = name ? t("preview.playing", name) : ""; }
    } catch { /* um passo que falhou não para o teste */ }
  };
  // pegar o pet com o mouse (ou o dedo) e jogar: o mesmo arremesso do programa
  const local = (e) => { const r = canvas.getBoundingClientRect(); return { x: (e.clientX - r.left) * (canvas.width / r.width), y: (e.clientY - r.top) * (canvas.height / r.height) }; };
  const hit = (q) => {
    if (!pet) return false;
    const s = pet.renderState();
    return q.x >= s.x - 8 && q.x <= s.x + s.width + 8 && q.y >= s.y - 8 && q.y <= s.y + s.height + 8;
  };
  stage.addEventListener("pointerdown", (e) => {
    const q = local(e);
    if (!hit(q)) return;
    const s = pet.renderState();
    drag = { dx: q.x - s.x, dy: q.y - s.y };
    pet.beginDrag();
    stage.setPointerCapture(e.pointerId);
    stage.classList.add("grabbing");
  });
  stage.addEventListener("pointermove", (e) => {
    const q = local(e);
    if (!drag) { stage.style.cursor = hit(q) ? "grab" : ""; return; }
    pet.dragTo(q.x - drag.dx, q.y - drag.dy, performance.now());
  });
  const drop = (e) => {
    if (!drag) return;
    pet.endDrag(performance.now());
    drag = null;
    stage.classList.remove("grabbing");
    if (stage.hasPointerCapture(e.pointerId)) stage.releasePointerCapture(e.pointerId);
  };
  stage.addEventListener("pointerup", drop);
  stage.addEventListener("pointercancel", drop);

  const start = async () => {
    if (pet) pet.dispose();
    if (url) URL.revokeObjectURL(url);
    measure();
    url = URL.createObjectURL(new Blob([doc.serialize()], { type: "application/xml" }));
    pet = new Pet(world);
    pet.volume = 0.4;
    try {
      await pet.load(url);
      // a folha do editor já vem sem a cor transparente (o programa tira o magenta; o motor da demo, não)
      if (doc.sheet) pet.sheet = doc.sheet;
      // "▶ Testar esta animação" na página Animações: o pet começa por ela
      if (state.testAnim != null && doc.animById(state.testAnim)) pet.setNewAnimation(state.testAnim);
      state.testAnim = null;
    } catch (e) { toast(t("test.fail", e.message)); }
  };
  start();
  raf = requestAnimationFrame(loop);
  state.cleanup.push(() => { cancelAnimationFrame(raf); if (pet) pet.dispose(); if (url) URL.revokeObjectURL(url); });
}
/* ---------------------------------------------------------- começo */
function buildShell() {
  initLang();
  const current = LANGS.find(([c]) => c === getLang()) || LANGS[0];
  const switchTo = (code) => { if (code === getLang()) return; setLang(code); document.body.textContent = ""; buildShell(); refreshProblems(); render(); };
  const lang = h("button", { class: "btn lang", title: t("web.language"), "aria-haspopup": "menu", "aria-label": t("web.language") + ": " + current[1] },
    flagImg(current[0]), h("span", { class: "langname", text: current[1] }), h("span", { class: "caret", text: "▾" }));
  lang.addEventListener("click", () => {
    const r = lang.getBoundingClientRect();
    popupMenu(r.right - 200, r.bottom + 4, LANGS.map(([c, n]) => ({ text: n, icon: flagImg(c), on: c === getLang(), onClick: () => switchTo(c) })));
  });
  const fileBtn = h("button", { class: "btn", id: "btnOpen", text: t("act.open"), onclick: async () => openFile(await pickFile(".xml,application/xml,text/xml")) });
  document.body.append(
    h("header", { class: "top" },
      h("div", { class: "brand" }, h("img", { src: "icon.png", alt: "" }), h("span", { text: t("web.title") })),
      h("div", { class: "petinfo" }, h("b", { id: "petname" }), h("small", { id: "petstate" })),
      h("div", { class: "actions" },
        h("button", { class: "btn", text: t("act.new"), onclick: newPet }), fileBtn,
        h("button", { class: "btn primary", id: "btnSave", text: t("act.save"), onclick: saveFile }),
        h("button", { class: "btn", id: "btnZip", text: ".zip", title: t("proj.zip"), onclick: exportZip }),
        h("button", { class: "btn icon", id: "btnUndo", text: "↶", title: t("act.undo"), onclick: () => state.doc && state.doc.doUndo() }),
        h("button", { class: "btn icon", id: "btnRedo", text: "↷", title: t("act.redo"), onclick: () => state.doc && state.doc.doRedo() }),
        h("button", { class: "btn", id: "btnCheck", onclick: () => go("check") }),
        lang)),
    h("div", { class: "main" }, h("nav", { class: "side", id: "nav" }), h("main", { id: "page" })),
    h("footer", { class: "credits" }, t("web.credits"), " ",
      h("a", { href: "https://github.com/lfeliipeadv-stack/DeskPals", target: "_blank", rel: "noopener", text: "DeskPals" }), " · ",
      h("a", { href: "https://github.com/Adrianotiger/desktopPet-Editor", target: "_blank", rel: "noopener", text: "desktopPet-Editor" })));
  refreshTop();
}

document.addEventListener("keydown", (e) => {
  const inField = /INPUT|TEXTAREA|SELECT/.test(document.activeElement?.tagName || "");
  if (!state.doc || inField || !(e.ctrlKey || e.metaKey)) return;
  if (e.key.toLowerCase() === "z") { e.preventDefault(); e.shiftKey ? state.doc.doRedo() : state.doc.doUndo(); }
  if (e.key.toLowerCase() === "y") { e.preventDefault(); state.doc.doRedo(); }
  if (e.key.toLowerCase() === "s") { e.preventDefault(); saveFile(); }
});
window.addEventListener("beforeunload", (e) => { if (state.doc && state.doc.dirty) { e.preventDefault(); e.returnValue = ""; } });

buildShell();
const fromHash = location.hash.slice(1);
if (PAGES.some(([id]) => id === fromHash)) state.page = fromHash;
const saved = await loadLocal();
if (saved && saved.xml) await openDoc(saved.xml, saved.name, { recovered: true, dirty: saved.dirty });
refreshProblems();
render();

// funciona sem internet depois da primeira visita (só no endereço publicado e no servidor local "localhost")
if ("serviceWorker" in navigator && (location.protocol === "https:" || location.hostname === "localhost")) navigator.serviceWorker.register("sw.js").catch(() => {});

// o teste automático (tools/) usa isto para abrir um pet sem clicar
window.deskpalsEditor = { openDoc, state, go, rawUrl, petZip, previewGif, whereText, galleryDialog };
