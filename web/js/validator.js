/* ---------------------------------------------------------------
   validator.js — o que daria errado com o pet no DeskPals: erros (o
   programa trava ou o pet não toca) e avisos (toca, mas provavelmente
   não como se queria). As mesmas regras do Editor de Pets para Windows
   (editor/Core/Validator.cs); cada problema diz onde está.
   --------------------------------------------------------------- */
import { evaluateExpression } from "./pet-engine.js";
import { kid, kids, text } from "./doc.js";
import { t } from "./i18n.js";

const KEYS = ["drag", "fall", "kill", "sync", "toss", "fall soft", "fall hard"];
/* Os nomes que o DeskPals procura (as animações dele, as reações, a pose parada enquanto fala). */
const LOOKED = new Set(KEYS.concat(["talk", "talking", "speak", "idle", "sit", "look around", "think", "blink", "wait", "busy", "stand",
  "walk", "run", "wave", "hello", "happy", "laugh", "scared", "surprised", "sleep", "eat", "love", "in love", "dance", "jump", "spin", "play",
  "cheer", "drink", "hearts", "purr", "nap", "rest", "yawn", "idea", "dizzy", "read", "coffee", "umbrella", "cold", "shiver", "hot", "sweat",
  "party", "music", "cry", "angry", "look up", "tired", "stretch", "review", "work", "shocked", "surprise", "error", "thumbs up", "nod", "ok",
  "hug", "sleepy"]));

const IDS = /\b(screenW|screenH|areaW|areaH|imageW|imageH|imageX|imageY|random|randS|scale)\b/g;

/** O que há de errado numa fórmula ("random*(screenW-imageW)/100"), ou null quando ela funciona. */
export function formulaError(textValue) {
  const s = String(textValue ?? "").trim();
  if (s === "") return null;
  const sub = s.replace(IDS, "1");
  if (/[a-z_]/i.test(sub)) {
    const word = (sub.match(/[a-z_][a-z0-9_]*/i) || [""])[0];
    return t("x.unknownWord", word) !== "x.unknownWord" ? t("x.unknownWord", word) : "“" + word + "”";
  }
  if (!/^[-+*/()\d\s.]+$/.test(sub)) return "?";
  let depth = 0;
  for (const ch of sub) { if (ch === "(") depth++; else if (ch === ")" && --depth < 0) return ")"; }
  if (depth !== 0) return "(";
  if (/[-+*/]\s*$/.test(sub) || /^\s*[*/]/.test(sub) || /[-+*/]\s*[*/)]/.test(sub) || /\(\s*\)/.test(sub)) return "?";
  return null;
}

/** Um nome para mostrar: “walk” (#1). */
const label = (a) => `“${text(a, "name")}” (#${a.getAttribute("id")})`;
const nextsOf = (el) => kids(el, "next").map((n) => ({ id: parseInt(n.textContent, 10) || 0, p: parseInt(n.getAttribute("probability") || "0", 10) || 0 }));

export function validate(doc) {
  const list = [];
  const E = (msg, page, anim = -1) => list.push({ error: true, text: msg, page, anim });
  const W = (msg, page, anim = -1) => list.push({ error: false, text: msg, page, anim });
  const ctx = { imageW: doc.frameW || 64, imageH: doc.frameH || 64, imageX: 100, imageY: 100, screenW: 1280, screenH: 720, areaW: 1280, areaH: 680, random: 50, randS: 50, scale: 1 };
  const value = (s, fallback = 0) => (formulaError(s) ? fallback : evaluateExpression(s, ctx));
  const formula = (s, what, page, anim = -1) => {
    const err = formulaError(s);
    if (err) E(t("chk.formula", what, s, err), page, anim);
  };

  const anims = doc.animations;
  const ids = new Set(anims.map((a) => +a.getAttribute("id")));
  const png = text(doc.image, "png");
  if (!png) E(t("chk.noImage"), "sheet");
  else if (doc.sheet && (doc.sheet.width % doc.tilesX !== 0 || doc.sheet.height % doc.tilesY !== 0))
    W(t("chk.grid", doc.sheet.width, doc.sheet.height, doc.tilesX, doc.tilesY), "sheet");
  if (!text(doc.header, "petname")) W(t("chk.noName"), "header");
  if (doc.spawns.length === 0) E(t("chk.noSpawn"), "spawns");
  if (anims.length === 0) E(t("chk.noAnims"), "animations");

  const count = (arr, key) => arr.reduce((m, x) => m.set(key(x), (m.get(key(x)) || 0) + 1), new Map());
  for (const [id, n] of count(anims, (a) => +a.getAttribute("id"))) if (n > 1) E(t("chk.dupId", id), "animations", id);
  for (const [id, n] of count(doc.spawns, (s) => +s.getAttribute("id"))) if (n > 1) E(t("chk.dupSpawn", id), "spawns");

  // quem leva a quem (para achar as animações que nada alcança)
  const reached = new Set();
  for (const s of doc.spawns) {
    const sid = s.getAttribute("id");
    const next = parseInt(text(s, "next", "0"), 10) || 0;
    reached.add(next);
    if (!ids.has(next)) E(t("chk.spawnNext", sid, next), "spawns");
    formula(text(s, "x"), t("chk.spawnX", sid), "spawns");
    formula(text(s, "y"), t("chk.spawnY", sid), "spawns");
  }
  const childStarts = new Set();
  for (const c of doc.childs) {
    const pid = +c.getAttribute("animationid");
    const next = parseInt(text(c, "next", "0"), 10) || 0;
    reached.add(next); childStarts.add(next);
    if (!ids.has(pid)) E(t("chk.childParent", pid), "childs");
    if (!ids.has(next)) E(t("chk.childNext", pid, next), "childs");
    formula(text(c, "x"), t("chk.childX", pid), "childs");
    formula(text(c, "y"), t("chk.childY", pid), "childs");
  }
  for (const s of doc.sounds) {
    const id = +s.getAttribute("animationid");
    if (!ids.has(id)) E(t("chk.soundAnim", id), "sounds");
  }
  const byId = new Map();
  for (const a of anims) if (!byId.has(+a.getAttribute("id"))) byId.set(+a.getAttribute("id"), a);
  const allNext = (a) => nextsOf(kid(a, "sequence")).concat(nextsOf(kid(a, "border")), nextsOf(kid(a, "gravity")), nextsOf(kid(a, "interact")));
  // toda animação tocada por uma janela filha (ela termina ali de propósito, e pode sair da tela)
  const childAnims = new Set();
  const walk = [...childStarts];
  while (walk.length) {
    const id = walk.pop();
    if (childAnims.has(id) || !byId.has(id)) continue;
    childAnims.add(id);
    for (const n of allNext(byId.get(id))) walk.push(n.id);
  }

  const frames = doc.frameCount;
  for (const a of anims) {
    const id = +a.getAttribute("id");
    const name = label(a);
    const seq = kid(a, "sequence");
    const f = kids(seq, "frame").map((x) => parseInt(x.textContent, 10));
    if (f.length === 0) E(t("chk.noFrames", name), "animations", id);
    else if (frames > 0 && doc.sheet) {
      const bad = f.find((x) => !(x >= 0 && x < frames));
      if (bad !== undefined) E(t("chk.badFrame", name, bad, frames - 1), "animations", id);
    }
    const repeatFrom = parseInt(seq ? seq.getAttribute("repeatfrom") || "0" : "0", 10) || 0;
    if (repeatFrom >= Math.max(1, f.length)) E(t("chk.repeatFrom", name), "animations", id);
    for (const n of allNext(a)) {
      reached.add(n.id);
      if (!ids.has(n.id)) E(t("chk.nextMissing", name, n.id), "animations", id);
    }
    const st = kid(a, "start"), en = kid(a, "end");
    formula(text(st, "x"), t("chk.what", name, "x"), "animations", id);
    formula(text(st, "y"), t("chk.what", name, "y"), "animations", id);
    formula(text(en, "x"), t("chk.what", name, "x"), "animations", id);
    formula(text(en, "y"), t("chk.what", name, "y"), "animations", id);
    formula(text(st, "interval"), t("chk.what", name, t("move.interval")), "animations", id);
    formula(seq ? seq.getAttribute("repeat") || "" : "", t("chk.what", name, t("anim.repeat")), "animations", id);
    if (value(text(st, "interval", "1"), 1) <= 0) W(t("chk.interval", name), "animations", id);
    const lower = text(a, "name").toLowerCase();
    const isKey = KEYS.includes(lower);
    const child = childAnims.has(id);
    if (nextsOf(seq).length === 0 && !isKey && !child && lower !== "kill") W(t("chk.noNext", name), "animations", id);
    const walks = value(text(st, "x")) !== 0 || value(text(en, "x")) !== 0;
    if (walks && !child && !isKey && nextsOf(kid(a, "border")).length === 0) W(t("chk.noBorder", name), "animations", id);
  }
  for (const a of anims) {
    const id = +a.getAttribute("id");
    // uma animação que o programa toca pelo nome (a fala com um balão aberto, as reações) é alcançada
    if (!reached.has(id) && !LOOKED.has(text(a, "name").toLowerCase())) W(t("chk.unreached", label(a)), "animations", id);
  }
  for (const k of ["drag", "fall"])
    if (!anims.some((a) => text(a, "name").toLowerCase() === k)) W(t("chk.noKey", k), "animations");

  // listas em que todas as chances são zero: nada é escolhido nelas
  for (const a of anims) {
    for (const [el, key] of [[kid(a, "sequence"), "next.end"], [kid(a, "border"), "next.border"], [kid(a, "gravity"), "next.gravity"], [kid(a, "interact"), "next.interact"]]) {
      const ns = nextsOf(el);
      if (ns.length > 0 && ns.every((n) => n.p <= 0)) W(t("chk.zeroProb", label(a), t(key)), "animations", +a.getAttribute("id"));
    }
  }
  if (doc.spawns.length > 0 && doc.spawns.every((s) => (parseInt(s.getAttribute("probability") || "0", 10) || 0) <= 0)) E(t("chk.spawnZero"), "spawns");
  // duas animações com o mesmo nome (o programa acha as reações pelo nome: só a primeira é usada)
  const byName = new Map();
  for (const a of anims) {
    const n = text(a, "name").trim().toLowerCase();
    if (n && LOOKED.has(n)) byName.set(n, (byName.get(n) || []).concat([a]));
  }
  for (const group of byName.values())
    if (group.length > 1) W(t("chk.dupName", text(group[0], "name"), group.map((a) => "#" + a.getAttribute("id")).join(", ")), "animations", +group[0].getAttribute("id"));
  // uma janela filha cujas animações nunca terminam fica na tela até o pet ir embora
  for (const c of doc.childs) {
    const start = parseInt(text(c, "next", "0"), 10) || 0;
    if (!byId.has(start)) continue;
    const seen = new Set(), todo = [start];
    let ends = false;
    while (todo.length && !ends) {
      const id = todo.pop();
      if (seen.has(id) || !byId.has(id)) continue;
      seen.add(id);
      const a = byId.get(id);
      const ns = nextsOf(kid(a, "sequence")).filter((n) => n.p > 0);
      if (ns.length === 0) ends = true;
      const st = kid(a, "start"), en = kid(a, "end");
      // uma que se move sai da tela cedo ou tarde, e o DeskPals a fecha ali
      if ([text(st, "x"), text(en, "x"), text(st, "y"), text(en, "y")].some((s) => value(s) !== 0)) ends = true;
      for (const n of ns) todo.push(n.id);
    }
    if (!ends) W(t("chk.childForever", text(byId.get(start), "name"), start), "childs");
  }
  // sons pesados deixam o pet pesado para baixar
  for (const s of doc.sounds) {
    const b64 = text(s, "base64");
    const kb = Math.floor((b64.length * 3) / 4 / 1024);
    if (kb > 300) W(t("chk.bigSound", doc.animName(s.getAttribute("animationid")) || "#" + s.getAttribute("animationid"), kb), "sounds");
  }
  // quadros que pedem memória demais
  if (doc.frameW * doc.frameH > 400 * 400) W(t("chk.bigFrame", doc.frameW, doc.frameH), "sheet");
  return list;
}
