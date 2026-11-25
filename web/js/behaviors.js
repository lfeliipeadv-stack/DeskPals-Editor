/* ---------------------------------------------------------------
   behaviors.js — comportamentos prontos (o mesmo do Editor de Pets
   para Windows, Behaviors.cs): uma animação já configurada (movimento,
   bordas, gravidade, o que vem depois) que só precisa dos quadros.
   Ela se liga às animações do pet pelos nomes: "walk" vira nas
   laterais com "turn" e cai com "fall"; uma pose volta para "walk"…
   --------------------------------------------------------------- */
import { kid, kids, text } from "./doc.js";

/** [chave, nome no pet, quadros mínimos, o andar às vezes vai para ela (sorteio)]. */
export const KINDS = [
  ["walk", "walk", 2, false], ["turn", "turn", 1, false], ["idle", "idle", 1, true], ["blink", "blink", 2, true],
  ["run", "run", 2, true], ["jump", "jump", 1, true], ["climb", "climb", 2, false], ["sit", "sit", 1, true],
  ["sleep", "sleep", 1, true], ["happy", "happy", 2, true], ["dance", "dance", 2, true], ["talk", "talk", 2, false],
  ["fall", "fall", 1, false], ["land", "fall soft", 1, false], ["drag", "drag", 1, false], ["kill", "kill", 1, false],
];

const byName = (doc, name) => doc.animations.find((a) => text(a, "name").toLowerCase() === name.toLowerCase()) || null;
const idOf = (a) => +a.getAttribute("id");

function next(doc, parent, id, prob, only = "none") {
  const el = doc.make("next", id);
  el.setAttribute("probability", String(prob));
  el.setAttribute("only", only);
  doc.insertOrdered(parent, el);
}
function addNext(doc, a, id, prob) {
  if (!a) return;
  const seq = doc.ensure(a, "sequence");
  if (kids(seq, "next").some((n) => +n.textContent === id)) return;
  next(doc, seq, id, prob);
}
function addBorder(doc, a, id, prob, only = "none") {
  if (!a) return;
  const border = doc.ensure(a, "border");
  if (kids(border, "next").some((n) => +n.textContent === id && (n.getAttribute("only") || "none") === only)) return;
  next(doc, border, id, prob, only);
}
function addGravity(doc, a, id, prob) {
  if (!a) return;
  const gravity = doc.ensure(a, "gravity");
  if (kids(gravity, "next").some((n) => +n.textContent === id)) return;
  next(doc, gravity, id, prob);
}

/** Uma animação nova no fim da lista: movimento do início ao fim, quadros e repetição. */
function create(doc, name, frames, x, y0, y1, interval, repeat, op0 = 1, op1 = 1, x1 = null) {
  const id = doc.nextFreeAnimId();
  const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;");
  const xml = `<animation xmlns="${doc.ns}" id="${id}"><name>${esc(name)}</name>` +
    `<start><x>${x}</x><y>${y0}</y><offsety>0</offsety><opacity>${op0}</opacity><interval>${interval}</interval></start>` +
    `<end><x>${x1 ?? x}</x><y>${y1}</y><offsety>0</offsety><opacity>${op1}</opacity><interval>${interval}</interval></end>` +
    `<sequence repeatfrom="0" repeat="${esc(repeat)}">${frames.map((f) => `<frame>${f}</frame>`).join("")}</sequence></animation>`;
  const el = doc.xml.importNode(new DOMParser().parseFromString(xml, "application/xml").documentElement, true);
  doc.insertOrdered(doc.animationsEl, el);
  // a indentação de dentro: o modelo vem numa linha só
  indentInside(doc, el);
  return el;
}

/** Quebra as linhas de um elemento criado numa linha só, com a indentação dos irmãos. */
export function indentInside(doc, el) {
  const base = doc.indentOf(el);
  const walk = (node, depth) => {
    const children = [...node.children];
    if (!children.length) return;
    for (const c of children) {
      node.insertBefore(doc.xml.createTextNode("\n" + base + "  ".repeat(depth + 1)), c);
      walk(c, depth + 1);
    }
    node.appendChild(doc.xml.createTextNode("\n" + base + "  ".repeat(depth)));
  };
  walk(el, 0);
}

/**
 * Acrescenta um comportamento com estes quadros (chamar dentro de doc.change). speed: 1 normal, menos devagar, mais
 * rápido. lottery: o "walk" às vezes vai para ele (com esta chance), quando existe. Devolve o id da animação nova.
 */
export function createBehavior(doc, key, f, { name = null, speed = 1, lottery = 0 } = {}) {
  const ms = (v) => Math.max(20, Math.round(v / Math.max(0.2, speed)));
  const px = (v) => String(Math.round(v * speed * 100) / 100);
  const walk = byName(doc, "walk"), fall = byName(doc, "fall"), turn = byName(doc, "turn");
  let a;
  switch (key) {
    case "walk":
      a = create(doc, name || "walk", f, "-" + px(2), "0", "0", ms(150), "random/4+6");
      addNext(doc, a, idOf(a), 60);
      if (turn) { addBorder(doc, a, idOf(turn), 100); addNext(doc, a, idOf(turn), 10); addNext(doc, turn, idOf(a), 100); }
      if (fall) addGravity(doc, a, idOf(fall), 100);
      break;
    case "turn":
      a = create(doc, name || "turn", f, "0", "0", "0", ms(140), "0");
      doc.insertOrdered(kid(a, "sequence"), doc.make("action", "flip"));
      if (walk) { addNext(doc, a, idOf(walk), 100); addBorder(doc, walk, idOf(a), 100); addNext(doc, walk, idOf(a), 10); }
      break;
    case "run":
      a = create(doc, name || "run", f, "-" + px(6), "0", "0", ms(70), "random/4+8");
      if (turn) addBorder(doc, a, idOf(turn), 100);
      if (fall) addGravity(doc, a, idOf(fall), 100);
      break;
    case "jump":
      a = create(doc, name || "jump", f, "-" + px(3), "-" + px(9), px(9), ms(50), String(Math.max(0, Math.floor(18 / Math.max(1, f.length)) - 1)));
      addBorder(doc, a, walk ? idOf(walk) : idOf(a), 100, "taskbar");
      addBorder(doc, a, walk ? idOf(walk) : idOf(a), 100, "window");
      if (turn) addBorder(doc, a, idOf(turn), 100, "vertical");
      if (walk) addNext(doc, a, idOf(walk), 100);
      break;
    case "climb":
      // sobe pela lateral da tela; lá em cima, solta e cai
      a = create(doc, name || "climb", f, "0", "-" + px(2), "-" + px(2), ms(120), "random/4+12");
      addNext(doc, a, idOf(a), 100);
      if (fall) addBorder(doc, a, idOf(fall), 100, "horizontal");
      if (walk) addBorder(doc, walk, idOf(a), 40, "vertical");
      break;
    case "fall": {
      a = create(doc, name || "fall", f, "0", px(2), px(12), ms(60), "20");
      addNext(doc, a, idOf(a), 100);
      const land = byName(doc, "fall soft");
      const to = land ? idOf(land) : walk ? idOf(walk) : idOf(a);
      addBorder(doc, a, to, 100, "taskbar");
      addBorder(doc, a, to, 100, "window");
      if (walk) addGravity(doc, walk, idOf(a), 100);
      break;
    }
    case "land":
      a = create(doc, name || "fall soft", f, "0", "0", "0", ms(120), "0");
      if (walk) addNext(doc, a, idOf(walk), 100);
      // a queda passa a pousar com ela (em vez de ir direto para o andar)
      if (fall) for (const n of kids(kid(fall, "border"), "next")) if (walk && +n.textContent === idOf(walk) || +n.textContent === idOf(fall)) n.textContent = idOf(a);
      break;
    case "drag":
      a = create(doc, name || "drag", f, "0", "0", "0", ms(100), "200");
      addNext(doc, a, idOf(a), 100);
      break;
    case "kill":
      a = create(doc, name || "kill", f, "0", "0", "0", ms(100), "10", 1, 0);
      break;
    case "sleep":
    case "sit":
      a = create(doc, name || key, f, "0", "0", "0", ms(key === "sleep" ? 400 : 300), key === "sleep" ? "random/6+12" : "random/6+6");
      kid(a, "sequence").setAttribute("repeatfrom", String(Math.max(0, f.length - 1)));
      if (walk) addNext(doc, a, idOf(walk), 100);
      break;
    default: {   // idle, blink, happy, dance, talk: uma pose no lugar que volta a andar
      const rep = { idle: "random/8+4", dance: "4", happy: "2", talk: "random/3+2" }[key] || "0";
      const time = { blink: 110, dance: 160, talk: 120 }[key] || 200;
      a = create(doc, name || key, f, "0", "0", "0", ms(time), rep);
      if (walk) addNext(doc, a, idOf(walk), 100);
    }
  }
  if (lottery > 0 && walk && walk !== a) addNext(doc, walk, idOf(a), lottery);
  return idOf(a);
}
