/* ---------------------------------------------------------------
   dialogs.js — as janelas maiores do editor: nova animação a partir
   de um modelo, montar a folha a partir de imagens ou de um GIF, o
   assistente de pet novo e abrir um pet da galeria do DeskPals.
   --------------------------------------------------------------- */
import { t } from "./i18n.js";
import { h, modal, toast, pickFile, toggle } from "./ui.js";
import { PetDoc, prettyXml } from "./doc.js";
import { KINDS, createBehavior } from "./behaviors.js";
import { loadFrames, buildSheet, petFrames, mirrored, blank } from "./sheet.js";
import { makeIco } from "./files.js";

/* ---------------------------------------------------------- peças */

/** A miniatura de um quadro (uma tela) num quadrado deste tamanho. */
function thumbOf(frame, size) {
  const c = blank(size, size);
  if (frame) {
    const z = Math.min(size / frame.width, size / frame.height);
    const g = c.getContext("2d");
    g.imageSmoothingEnabled = z < 1;
    g.drawImage(frame, (size - frame.width * z) / 2, (size - frame.height * z) / 2, frame.width * z, frame.height * z);
  }
  return c;
}

/** Todos os quadros para clicar (os marcados ficam em destaque). */
function frameGrid(frames, onPick, marked = new Set()) {
  const grid = h("div", { class: "picker tall" });
  frames.forEach((f, i) => grid.append(h("button", { class: marked.has(i) ? "on" : "", title: t("web.frameN", i), onclick: () => onPick(i) }, thumbOf(f, 40), h("small", { text: String(i) }))));
  return grid;
}

/** Os quadros escolhidos, na ordem; clicar num tira ele. */
function chosenStrip(list, frames, onChange) {
  const strip = h("div", { class: "frames" });
  const fill = () => {
    strip.textContent = "";
    if (!list.length) strip.append(h("span", { class: "hint", text: "—" }));
    list.forEach((n, i) => strip.append(h("div", { class: "frame", title: t("strip.remove"), onclick: () => { list.splice(i, 1); fill(); onChange && onChange(); } },
      thumbOf(frames[n], 48), h("small", { text: String(n) }))));
  };
  fill();
  return { el: strip, fill };
}

/* ---------------------------------------------------------- nova animação a partir de um modelo */

export function behaviorDialog(doc, onCreated) {
  if (!doc.sheet || !doc.frameCount) { toast(t("sheet.empty")); return; }
  const frames = petFrames(doc);
  let kind = KINDS[0];
  const picked = [];
  const name = h("input", { type: "text", value: kind[1] });
  let lottery = kind[3];
  const lotteryBox = toggle(t("beh.lottery"), lottery, (v) => { lottery = v; });
  const need = h("p", { class: "hint" });
  const strip = chosenStrip(picked, frames);
  const list = h("div", { class: "kindlist" });
  const show = () => {
    list.querySelectorAll("button").forEach((b) => b.classList.toggle("on", b.dataset.key === kind[0]));
    name.value = kind[1];
    lottery = kind[3];
    lotteryBox.querySelector("input").checked = lottery;
    need.textContent = t("beh.need", kind[2]) + "  " + t("beh." + kind[0] + ".d");
  };
  for (const k of KINDS)
    list.append(h("button", { "data-key": k[0], onclick: () => { kind = k; show(); } }, h("b", { text: t("beh." + k[0]) }), h("small", { text: t("beh." + k[0] + ".d") })));
  show();
  modal({
    title: t("beh.title"), wide: true,
    body: h("div", { class: "twocol" }, list, h("div", {},
      h("div", { class: "row" }, h("label", { class: "field", style: "flex:1" }, h("span", { text: t("beh.name") }), name), lotteryBox),
      h("p", { style: "margin:10px 0 4px", text: t("beh.frames") }), need, strip.el,
      frameGrid(frames, (i) => { picked.push(i); strip.fill(); }))),
    buttons: [
      { text: t("build.cancel") },
      { text: t("beh.create"), primary: true, onClick: () => {
        if (picked.length < kind[2]) { toast(t("beh.need", kind[2])); return false; }
        let id = -1;
        doc.change(() => { id = createBehavior(doc, kind[0], picked.slice(), { name: name.value.trim() || kind[1], lottery: lottery ? 10 : 0 }); });
        onCreated && onCreated(id);
      } },
    ],
  });
}

/* ---------------------------------------------------------- montar a folha a partir de imagens */

export async function buildSheetDialog(doc, onDone) {
  const files = await pickFile("image/png,image/gif,image/bmp,image/webp,image/*", true);
  if (!files || !files.length) return;
  let added;
  try { added = await loadFrames(files); } catch (e) { toast(t("msg.openFail", e.message)); return; }
  if (!added.length) return;
  const has = !!doc.sheet && doc.frameCount > 0;
  let append = has;
  const cols = h("input", { type: "number", min: 1, max: 64, value: has ? doc.tilesX : Math.min(8, added.length), style: "width:90px" });
  modal({
    title: t("build.title"),
    body: h("div", {},
      h("h3", { text: t("build.found", added.length, files.length) }),
      h("p", { class: "hint", text: t("build.size", Math.max(...added.map((f) => f.width)), Math.max(...added.map((f) => f.height))) }),
      h("div", { class: "picker" }, ...added.slice(0, 48).map((f) => h("button", {}, thumbOf(f, 40)))),
      has ? toggle(t("build.append", doc.frameCount), true, (v) => { append = v; }) : null,
      h("label", { class: "field", style: "margin-top:10px" }, h("span", { text: t("sheet.cols") }), cols, h("small", { text: t("build.colsHint") }))),
    buttons: [
      { text: t("build.cancel") },
      { text: t("build.ok"), primary: true, onClick: () => {
        const all = append && has ? petFrames(doc).concat(added) : added;
        const first = append && has ? doc.frameCount : 0;
        const built = buildSheet(all, append && has ? doc.tilesX : Math.max(1, parseInt(cols.value, 10) || 1));
        doc.change(() => {
          const im = doc.section("image");
          doc.set(im, "png", built.png);
          doc.set(im, "tilesx", built.cols);
          doc.set(im, "tilesy", built.rows);
        });
        onDone && onDone(t("build.done", added.length, first, first + added.length - 1, built.w, built.h));
      } },
    ],
  });
}

/* ---------------------------------------------------------- o assistente de pet novo */

const ROLES = [["idle", 1], ["walk", 2], ["turn", 0], ["fall", 0], ["drag", 0], ["blink", 0], ["sleep", 0], ["happy", 0], ["jump", 0]];

/** O assistente: as imagens, o que cada quadro faz, nome e jeito. onCreate(xml) recebe o pet pronto. */
export function wizardDialog(currentDoc, onCreate) {
  let frames = null;      // os quadros (telas), já virados para a esquerda
  let mirror = false;
  let step = 0;
  let role = "idle";
  const picks = Object.fromEntries(ROLES.map(([k]) => [k, []]));
  const name = h("input", { type: "text", value: t("tpl.name") });
  const author = h("input", { type: "text", value: "" });
  const speed = h("select", {}, h("option", { value: "0.7", text: t("wiz.slow") }), h("option", { value: "1", text: t("wiz.normal") }), h("option", { value: "1.5", text: t("wiz.fast") }));
  speed.value = "1";
  let climb = false;
  const climbBox = toggle(t("wiz.climb"), false, (v) => { climb = v; });
  const body = h("div", { class: "wizbody" });
  const stepsBar = h("div", { class: "steps" });

  const setFrames = (list) => { frames = mirror ? list.map(mirrored) : list; for (const k in picks) picks[k] = []; };

  const draw = () => {
    body.textContent = "";
    stepsBar.textContent = "";
    for (let i = 0; i < 3; i++) stepsBar.append(h("span", { class: i === step ? "on" : i < step ? "done" : "", text: t("wiz.step" + i) }));
    back.style.visibility = step > 0 ? "visible" : "hidden";
    next.textContent = t(step === 2 ? "wiz.create" : "wiz.next");
    if (step === 0) {
      const face = h("select", {}, h("option", { value: "left", text: t("wiz.faceLeft") }), h("option", { value: "right", text: t("wiz.faceRight") }));
      face.value = mirror ? "right" : "left";
      face.addEventListener("change", () => {
        const m = face.value === "right";
        if (m === mirror) return;
        mirror = m;
        if (frames) { frames = frames.map(mirrored); for (const k in picks) picks[k] = []; }
        draw();
      });
      body.append(h("p", { class: "intro", text: t("wiz.intro0") }),
        h("div", { class: "row" },
          h("button", { class: "btn primary", text: t("wiz.pick"), onclick: async () => {
            const files = await pickFile("image/png,image/gif,image/bmp,image/webp,image/*", true);
            if (!files || !files.length) return;
            try { setFrames(await loadFrames(files)); } catch (e) { toast(t("msg.openFail", e.message)); }
            draw();
          } }),
          currentDoc && currentDoc.sheet ? h("button", { class: "btn", text: t("wiz.useSheet"), onclick: () => { setFrames(petFrames(currentDoc)); draw(); } }) : null),
        h("label", { class: "field", style: "max-width:320px;margin-top:12px" }, h("span", { text: t("wiz.face") }), face, h("small", { text: t("wiz.faceHint") })),
        h("p", { class: "hint", text: frames ? t("wiz.loaded", frames.length, Math.max(...frames.map((f) => f.width)), Math.max(...frames.map((f) => f.height))) : t("wiz.none") }),
        frames ? frameGrid(frames, () => {}) : null);
    } else if (step === 1) {
      const roles = h("div", { class: "kindlist" });
      const hint = h("p", { class: "hint" });
      const holder = h("div");
      const showRole = () => {
        roles.textContent = "";
        for (const [k, min] of ROLES) {
          const n = picks[k].length;
          roles.append(h("button", { class: k === role ? "on" : "", onclick: () => { role = k; showRole(); } },
            h("b", { text: t("beh." + k) }),
            h("small", { class: n ? "ok" : min ? "need" : "", text: n ? "✓ " + t("wiz.nFrames", n) : min ? t("wiz.required", min) : t("wiz.optional") })));
        }
        hint.textContent = t("wiz.roleHint", t("beh." + role)) + "  " + t("beh." + role + ".d");
        holder.textContent = "";
        const strip = chosenStrip(picks[role], frames, showRole);
        holder.append(strip.el, frameGrid(frames, (i) => { picks[role].push(i); showRole(); }, new Set(picks[role])));
      };
      showRole();
      body.append(h("div", { class: "twocol" }, roles, h("div", {}, hint, holder)));
    } else {
      body.append(h("p", { class: "intro", text: t("wiz.intro2") }),
        h("div", { class: "grid2" },
          h("label", { class: "field" }, h("span", { text: t("head.petname") }), name, h("small", { text: t("head.petnameHint") })),
          h("label", { class: "field" }, h("span", { text: t("head.author") }), author),
          h("label", { class: "field" }, h("span", { text: t("wiz.speed") }), speed)),
        h("div", { style: "margin-top:12px" }, climbBox),
        h("p", { class: "hint", text: t("wiz.after") }));
    }
  };

  const back = h("button", { class: "btn", text: t("wiz.back"), onclick: () => { step--; draw(); } });
  const next = h("button", { class: "btn primary", text: t("wiz.next"), onclick: async () => {
    if (step === 0 && !frames) { toast(t("wiz.needPictures")); return; }
    if (step === 1) {
      const missing = ROLES.find(([k, min]) => picks[k].length < min);
      if (missing) { toast(t("wiz.needRole", t("beh." + missing[0]), missing[1])); return; }
    }
    if (step < 2) { step++; draw(); return; }
    const xml = await createPet(frames, picks, name.value.trim(), author.value.trim(), parseFloat(speed.value), climb);
    m.close();
    onCreate(xml);
  } });
  const m = modal({ title: t("wiz.title"), wide: true, body: h("div", {}, stepsBar, body, h("div", { class: "modal-bar inline" }, back, next)) });
  draw();
}

/** O pet novo: cabeçalho, a folha, os comportamentos dos papéis e três entradas (o mesmo do assistente para Windows). */
export async function createPet(frames, picks, name, author, speed, climb) {
  const P = (key, fallback = "idle") => (picks[key].length ? picks[key] : picks[fallback].length ? picks[fallback] : picks.idle).slice();
  if (!name) name = t("tpl.name");
  const built = buildSheet(frames, Math.min(8, frames.length));
  const icon = await makeIco(frames[picks.idle[0]]);
  const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;");
  const xml = `<?xml version="1.0" encoding="utf-8"?>
<animations xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="https://esheep.petrucci.ch/ https://adrianotiger.github.io/desktopPet/Resources/animations.xsd" xmlns="https://esheep.petrucci.ch/">
<header><author>${esc(author)}</author><title>${esc(name)}</title><petname>${esc(name.slice(0, 16))}</petname><version>1.0</version><info>${esc(t("tpl.info"))}</info><application>1</application><icon><![CDATA[${icon}]]></icon></header>
<image><tilesx>${built.cols}</tilesx><tilesy>${built.rows}</tilesy><png><![CDATA[${built.png}]]></png><transparency>Magenta</transparency></image>
<spawns></spawns><animations></animations><childs></childs><sounds></sounds>
</animations>`;
  const doc = new PetDoc(prettyXml(xml));
  const walk = createBehavior(doc, "walk", P("walk"), { speed });
  createBehavior(doc, "turn", picks.turn.length ? P("turn") : [P("idle")[0], P("walk")[0]], { speed });
  const fall = createBehavior(doc, "fall", P("fall"), { speed });
  createBehavior(doc, "land", P("idle"), { speed });
  createBehavior(doc, "drag", P("drag", "fall"), { speed });
  const idle = createBehavior(doc, "idle", P("idle"), { speed, lottery: 15 });
  if (picks.blink.length) createBehavior(doc, "blink", P("blink"), { speed, lottery: 12 });
  if (picks.sleep.length) createBehavior(doc, "sleep", P("sleep"), { speed, lottery: 4 });
  if (picks.happy.length) createBehavior(doc, "happy", P("happy"), { speed, lottery: 6 });
  if (picks.jump.length) createBehavior(doc, "jump", P("jump"), { speed, lottery: 5 });
  if (climb) createBehavior(doc, "climb", P("walk"), { speed });
  createBehavior(doc, "kill", P("idle"), { speed });
  const spawns = doc.spawnsEl;
  for (const [id, prob, x, y, nx] of [[1, 40, "screenW", "areaH-imageH", walk], [2, 30, "random*(screenW-imageW-50)/100+25", "-imageH", fall], [3, 30, "random*(screenW-imageW-50)/100+25", "areaH-imageH", idle]]) {
    const sp = doc.xml.importNode(new DOMParser().parseFromString(`<spawn xmlns="${doc.ns}" id="${id}" probability="${prob}"><x>${x}</x><y>${y}</y><next probability="100">${nx}</next></spawn>`, "application/xml").documentElement, true);
    doc.insertOrdered(spawns, sp);
  }
  return prettyXml(doc.serialize());
}

/* ---------------------------------------------------------- abrir um pet da galeria do DeskPals */

const GALLERY = "https://raw.githubusercontent.com/lfeliipeadv-stack/DeskPals/master/Pets/";

export async function galleryDialog(onOpen) {
  const grid = h("div", { class: "gallery" }, h("p", { class: "hint", text: t("web.loading") }));
  const search = h("input", { type: "text", placeholder: t("anim.searchShort") });
  const m = modal({ title: t("web.gallery"), wide: true, body: h("div", {}, h("p", { class: "hint", text: t("web.galleryHint") }), search, grid) });
  let pets;
  try {
    const r = await fetch(GALLERY + "pets.json", { cache: "no-cache" });
    if (!r.ok) throw new Error(r.status + "");
    pets = (await r.json()).pets || [];
  } catch (e) { grid.textContent = t("web.loadFail", e.message); return; }
  pets.sort((a, b) => (b.lastupdate || "").localeCompare(a.lastupdate || ""));
  const fill = () => {
    const q = search.value.trim().toLowerCase();
    grid.textContent = "";
    for (const p of pets) {
      const label = p.name || p.folder;
      if (q && !label.toLowerCase().includes(q) && !(p.author || "").toLowerCase().includes(q)) continue;
      grid.append(h("button", { class: "galpet", onclick: async () => {
        m.close();
        toast(t("web.loading"));
        try {
          const r = await fetch(GALLERY + encodeURIComponent(p.folder) + "/animations.xml");
          if (!r.ok) throw new Error(r.status + "");
          onOpen(await r.text(), p);
        } catch (e) { toast(t("web.loadFail", e.message)); }
      } }, h("img", { src: GALLERY + encodeURIComponent(p.folder) + "/icon.png", alt: "", loading: "lazy" }),
        h("b", { text: label }), h("small", { text: (p.author || "") + (p.animations ? " · " + t("web.nAnims", p.animations) : "") })));
    }
  };
  search.addEventListener("input", fill);
  fill();
}
