// Teste do editor no Chrome sem janela: abre pets reais, passa por todas as páginas, anota erros do navegador,
// tira uma foto de cada página e confere que salvar não perde nada.
//   node tools/check.mjs <pasta Pets> <pasta das fotos> pet1 pet2 …
import { spawn } from "node:child_process";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const [petsDir, outDir, ...pets] = process.argv.slice(2);
const here = fileURLToPath(new URL(".", import.meta.url));
const port = 8765 + Math.floor(Math.random() * 100);
const server = spawn(process.execPath, [join(here, "serve.mjs"), String(port)], { stdio: "ignore" });
const chromePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const cdpPort = 9400 + Math.floor(Math.random() * 100);
const profile = join(tmpdir(), "deskpals-editor-test-" + Date.now());
const chrome = spawn(chromePath, ["--headless=new", `--remote-debugging-port=${cdpPort}`, `--user-data-dir=${profile}`, "--no-first-run", "--window-size=1366,900", "about:blank"], { stdio: "ignore" });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const report = { errors: [], pages: [], pets: [] };
mkdirSync(outDir, { recursive: true });

try {
  let ok = false;
  for (let i = 0; i < 50 && !ok; i++) { try { await (await fetch(`http://127.0.0.1:${cdpPort}/json/version`)).json(); ok = true; } catch { await sleep(200); } }
  const target = await (await fetch(`http://127.0.0.1:${cdpPort}/json/new?about:blank`, { method: "PUT" })).json();
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener("open", r));
  let id = 0; const waiting = new Map();
  ws.addEventListener("message", (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && waiting.has(m.id)) { waiting.get(m.id)(m); waiting.delete(m.id); }
    if (m.method === "Runtime.exceptionThrown") report.errors.push((m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text).slice(0, 400));
    if (m.method === "Runtime.consoleAPICalled" && m.params.type === "error") report.errors.push("console: " + m.params.args.map((a) => a.value ?? a.description ?? "").join(" ").slice(0, 300));
    if (m.method === "Log.entryAdded" && m.params.entry.level === "error") report.errors.push("log: " + m.params.entry.text.slice(0, 300) + " " + (m.params.entry.url || ""));
  });
  const send = (method, params = {}) => new Promise((r) => { const i = ++id; waiting.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  const evaluate = async (expression) => {
    const r = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
    if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description || r.result.exceptionDetails.text);
    return r.result?.result?.value;
  };
  const shot = async (name) => {
    const r = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
    writeFileSync(join(outDir, name + ".png"), Buffer.from(r.result.data, "base64"));
  };
  await send("Runtime.enable"); await send("Page.enable"); await send("Log.enable");
  const mobile = process.env.MOBILE === "1";
  await send("Emulation.setDeviceMetricsOverride", mobile ? { width: 390, height: 844, deviceScaleFactor: 1, mobile: true } : { width: 1366, height: 900, deviceScaleFactor: 1, mobile: false });
  await send("Page.navigate", { url: `http://127.0.0.1:${port}/#project` });
  for (let i = 0; i < 100; i++) { await sleep(100); try { if (await evaluate("!!window.deskpalsEditor")) break; } catch { } }
  await sleep(300);
  await shot("00-inicio");
  // idioma: abre em inglês; o seletor tem as bandeiras e troca de verdade
  report.lang = { start: await evaluate("document.documentElement.lang") };
  await evaluate("document.querySelector('.btn.lang').click()"); await sleep(200);
  report.lang.flags = await evaluate("[...document.querySelectorAll('.popmenu img.flag')].filter(i => i.complete && i.naturalWidth > 0).length");
  await shot("00b-idiomas");
  await evaluate("[...document.querySelectorAll('.popmenu button')].find(b => b.textContent.includes('Português')).click()"); await sleep(300);
  report.lang.after = await evaluate("document.documentElement.lang + ' / ' + document.querySelector('.btn.lang').textContent");

  const pages = ["project", "header", "sheet", "animations", "spawns", "childs", "sounds", "check", "map", "xml", "test"];
  for (const pet of pets) {
    const xml = readFileSync(join(petsDir, pet, "animations.xml"), "utf8");
    const opened = await evaluate(`window.deskpalsEditor.openDoc(${JSON.stringify(xml)}, "animations.xml")`);
    await sleep(400);
    const entry = { pet, opened };
    // salvar sem mexer: o arquivo sai igual (só o que o navegador escreve de outro jeito)
    const same = await evaluate(`(() => {
      const doc = window.deskpalsEditor.state.doc;
      const a = new XMLSerializer().serializeToString(new DOMParser().parseFromString(${JSON.stringify(xml)}, "application/xml")).replace(/^<\\?xml[^>]*>\\s*/, "");
      const b = doc.serialize().replace(/^<\\?xml[^>]*>\\s*/, "");
      if (a === b) return { same: true, repaired: !!doc.repaired };
      let i = 0; while (i < a.length && a[i] === b[i]) i++;
      return { same: false, repaired: !!doc.repaired, at: i, original: a.slice(Math.max(0, i - 60), i + 80), saved: b.slice(Math.max(0, i - 60), i + 80) };
    })()`);
    entry.sameAfterSave = same.same;
    entry.repaired = same.repaired;
    if (!same.same) entry.diff = same;
    entry.problems = await evaluate(`window.deskpalsEditor.state.problems.map(p => (p.error ? "ERRO " : "aviso ") + p.text)`);
    for (const pg of pages) {
      await evaluate(`window.deskpalsEditor.go(${JSON.stringify(pg)})`);
      await sleep(pg === "test" ? 2500 : pg === "animations" ? 900 : 350);
      if (pet === pets[0]) await shot(String(pages.indexOf(pg) + 1).padStart(2, "0") + "-" + pg);
    }
    report.pets.push(entry);
  }

  // mudanças de verdade: um quadro a mais, desfazer, e o que o editor não conhece continua lá
  if (pets.length) {
    report.edits = await evaluate(`(async () => {
      const E = window.deskpalsEditor, doc = E.state.doc;
      const out = {};
      const a = doc.animations[0], seq = a.getElementsByTagNameNS("*", "sequence")[0];
      const before = seq.getElementsByTagNameNS("*", "frame").length;
      doc.change(() => doc.insertOrdered(seq, doc.make("frame", 0)));
      out.frameAdded = seq.getElementsByTagNameNS("*", "frame").length === before + 1;
      out.frameBeforeNext = [...seq.children].findIndex(c => c.localName === "next") < 0 || [...seq.children].map(c => c.localName).lastIndexOf("frame") < [...seq.children].findIndex(c => c.localName === "next");
      doc.doUndo();
      out.undone = doc.animations[0].getElementsByTagNameNS("*", "frame").length === before;
      doc.set(doc.header, "petname", "Teste ção");
      out.renamed = doc.serialize().includes("<petname>Teste ção</petname>");
      return out;
    })()`);
  }
  // as partes maiores: GIF, modelos prontos, assistente, editor de pixels, mapa, .zip, galeria
  if (process.env.FEATURES !== "0" && pets.length) {
    const xml = readFileSync(join(petsDir, pets[0], "animations.xml"), "utf8");
    await evaluate(`window.deskpalsEditor.openDoc(${JSON.stringify(xml)}, "animations.xml")`);
    await sleep(400);
    report.features = await evaluate(`(async () => {
      const out = {};
      const E = window.deskpalsEditor;
      const gif = await import("/js/gif.js");
      // 1. GIF: gravar e ler de volta dá os mesmos pixels (um pequeno e um grande, que passa de 4096 códigos)
      const mk = (w, hh, seed, colors) => {
        const c = document.createElement("canvas"); c.width = w; c.height = hh;
        const g = c.getContext("2d");
        for (let y = 0; y < hh; y++) for (let x = 0; x < w; x++) {
          if ((x * 7 + y * 3 + seed) % 11 === 0) continue;
          const k = (x * 31 + y * 17 + seed * 13) % colors;
          g.fillStyle = "rgb(" + (k * 37 % 256) + "," + (k * 91 % 256) + "," + (k * 53 % 256) + ")";
          g.fillRect(x, y, 1, 1);
        }
        return c;
      };
      const roundtrip = (frames) => {
        const bytes = gif.encodeGif(frames, 120);
        const dec = gif.decodeGif(bytes);
        let diff = 0;
        dec.frames.forEach((f, i) => {
          const d = frames[i].getContext("2d").getImageData(0, 0, frames[i].width, frames[i].height).data;
          for (let k = 0; k < d.length; k += 4) {
            const a = d[k + 3] >= 128, b = f.rgba[k + 3] >= 128;
            if (a !== b || (a && (d[k] !== f.rgba[k] || d[k + 1] !== f.rgba[k + 1] || d[k + 2] !== f.rgba[k + 2]))) diff++;
          }
        });
        return { frames: dec.frames.length, diff, bytes: bytes.length, delay: dec.frames[0].delay };
      };
      out.gifSmall = roundtrip([mk(23, 17, 1, 9), mk(23, 17, 2, 9), mk(23, 17, 3, 9)]);
      out.gifBig = roundtrip([mk(256, 200, 5, 250), mk(256, 200, 6, 250)]);

      // 2. modelos prontos num pet novo: o pulo se liga ao andar, o pouso entra na queda
      const docMod = await import("/js/doc.js");
      const beh = await import("/js/behaviors.js");
      out.templateOpened = await E.openDoc(docMod.templateXml({ name: "Teste", title: "Teste", info: "", author: "x" }), "animations.xml");
      const doc = E.state.doc;
      out.templateIsNew = doc.petName === "Teste";
      let jump = -1, land = -1;
      doc.change(() => { jump = beh.createBehavior(doc, "jump", [0, 1], { lottery: 10 }); });
      const walk = doc.animations.find((a) => docMod.text(a, "name") === "walk");
      const ja = doc.animById(jump);
      out.behJump = {
        exists: !!ja && docMod.text(ja, "name") === "jump",
        walkGoesToJump: docMod.kids(docMod.kid(walk, "sequence"), "next").some((n) => +n.textContent === jump && n.getAttribute("probability") === "10"),
        landsOnTaskbar: docMod.kids(docMod.kid(ja, "border"), "next").some((n) => n.getAttribute("only") === "taskbar"),
      };
      const validator = await import("/js/validator.js");
      out.behErrors = validator.validate(doc).filter((p) => p.error).map((p) => p.text);
      out.undoOneStep = (() => { const n = doc.animations.length; doc.doUndo(); return doc.animations.length === n - 1; })();

      // 3. o assistente: um pet inteiro a partir de quadros, sem erros
      const dialogs = await import("/js/dialogs.js");
      const sheet = await import("/js/sheet.js");
      await doc.loadSheet();
      const frames = sheet.petFrames(doc);
      const picks = { idle: [0], walk: [1, 2], turn: [], fall: [4], drag: [4], blink: [0, 3], sleep: [], happy: [], jump: [] };
      const wxml = await dialogs.createPet(frames, picks, "Bolinha", "Teste", 1, true);
      out.wizardOpened = await E.openDoc(wxml, "animations.xml");
      const wdoc = E.state.doc;
      out.wizard = {
        names: wdoc.animations.map((a) => docMod.text(a, "name")).join(","),
        spawns: wdoc.spawns.length,
        icon: docMod.text(wdoc.header, "icon").length > 100,
        errors: validator.validate(wdoc).filter((p) => p.error).map((p) => p.text),
        warnings: validator.validate(wdoc).filter((p) => !p.error).length,
      };
      // 4. montar uma folha com quadros de tamanhos diferentes: o maior manda, os pés embaixo
      const a = document.createElement("canvas"); a.width = 10; a.height = 20;
      const b = document.createElement("canvas"); b.width = 30; b.height = 12;
      const built = sheet.buildSheet([a, b, a], 2);
      out.buildSheet = { cols: built.cols, rows: built.rows, w: built.w, h: built.h };
      return out;
    })()`);
    // 5. o editor de pixels: salvar sem mexer não muda nenhum pixel; e uma foto dele
    report.features.pixel = await evaluate(`(async () => {
      const E = window.deskpalsEditor;
      await E.openDoc(${JSON.stringify(xml)}, "animations.xml");
      const doc = E.state.doc;
      const files = await import("/js/files.js");
      const px = await import("/js/pixel.js");
      const before = files.frameCanvas(doc, 3).getContext("2d").getImageData(0, 0, doc.frameW, doc.frameH).data;
      let saved = false;
      px.pixelEditor(doc, 3, () => { saved = true; });
      await new Promise((r) => setTimeout(r, 400));
      return { opened: !!document.querySelector(".pxcanvas"), before: Array.from(before.slice(0, 0)) };
    })()`);
    await shot("20-pixel-editor");
    report.features.pixelSave = await evaluate(`(async () => {
      const E = window.deskpalsEditor, doc = E.state.doc;
      const files = await import("/js/files.js");
      const before = files.frameCanvas(doc, 3).getContext("2d").getImageData(0, 0, doc.frameW, doc.frameH).data;
      [...document.querySelectorAll(".modal .btn.primary")].pop().click();
      await new Promise((r) => setTimeout(r, 300));
      await doc.loadSheet();
      const after = files.frameCanvas(doc, 3).getContext("2d").getImageData(0, 0, doc.frameW, doc.frameH).data;
      let diff = 0;
      for (let k = 0; k < before.length; k += 4) if (before[k + 3] !== after[k + 3] || (before[k + 3] > 0 && (before[k] !== after[k] || before[k + 1] !== after[k + 1] || before[k + 2] !== after[k + 2]))) diff++;
      return { diffPixels: diff, frames: doc.frameCount, undo: doc.canUndo() };
    })()`);
    // 6. o mapa, o .zip (com a prévia animada) e as entradas em palavras
    await evaluate(`window.deskpalsEditor.go("map")`);
    await sleep(700);
    await shot("21-map");
    report.features.map = await evaluate(`({ nodes: document.querySelectorAll(".mapnode").length, anims: window.deskpalsEditor.state.doc.animations.length, edges: document.querySelectorAll(".mapsvg path[marker-end]").length })`);
    report.features.zip = await evaluate(`(async () => {
      const E = window.deskpalsEditor;
      const { folder, blob } = await E.petZip(E.state.doc);
      const text = new TextDecoder("latin1").decode(new Uint8Array(await blob.arrayBuffer()));
      const gif = E.previewGif(E.state.doc);
      const dec = (await import("/js/gif.js")).decodeGif(gif);
      return { folder, kb: Math.round(blob.size / 1024), files: ["animations.xml", "icon.png", "preview.gif", "README.md", "README.pt-BR.md"].filter((n) => text.includes(folder + "/" + n)), gifFrames: dec.frames.length, gifW: dec.width, gifH: dec.height };
    })()`);
    report.features.where = await evaluate(`[window.deskpalsEditor.whereText("random*(screenW-imageW-50)/100+25", "areaH-imageH"), window.deskpalsEditor.whereText("screenW", "-imageH")]`);
    // 7. as janelas: modelo pronto, assistente e galeria (fotos)
    await evaluate(`window.deskpalsEditor.go("animations"); (async () => { const d = await import("/js/dialogs.js"); d.behaviorDialog(window.deskpalsEditor.state.doc, () => {}); })()`);
    await sleep(500); await shot("22-modelo");
    await evaluate(`document.querySelector(".modal-x").click()`);
    await evaluate(`(async () => { const d = await import("/js/dialogs.js"); d.galleryDialog(() => {}); })()`);
    for (let i = 0; i < 40; i++) { await sleep(250); if (await evaluate(`document.querySelectorAll(".galpet").length > 0`)) break; }
    await sleep(800);
    report.features.gallery = await evaluate(`document.querySelectorAll(".galpet").length`);
    await shot("23-galeria");
    // uma fonte nova: o link de um repositório do GitHub (pets.json em Pets/), guardada para a próxima vez
    await evaluate(`[...document.querySelectorAll(".chip.add")][0].click()`);
    await evaluate(`(() => { const i = document.querySelector(".srcadd input"); i.value = "https://github.com/Adrianotiger/desktopPet"; document.querySelector(".srcadd .btn").click(); })()`);
    for (let i = 0; i < 60; i++) { await sleep(250); if (await evaluate(`document.querySelectorAll(".galpet").length > 0 && document.querySelector(".chip.on")?.textContent.includes("Adrianotiger")`)) break; }
    await sleep(800);
    report.features.source = await evaluate(`({ chips: [...document.querySelectorAll(".chip")].map((c) => c.textContent), pets: document.querySelectorAll(".galpet").length, status: document.querySelector(".srcstatus span")?.textContent })`);
    await shot("23b-fonte");
    await evaluate(`document.querySelector(".modal-x").click()`);
    await evaluate(`window.deskpalsEditor.galleryDialog(() => {})`); await sleep(1500);
    report.features.sourceKept = await evaluate(`[...document.querySelectorAll(".chip")].map((c) => c.textContent).join(" | ")`);
    await evaluate(`document.querySelector(".modal-x").click()`);
    await evaluate(`(async () => { const d = await import("/js/dialogs.js"); d.wizardDialog(window.deskpalsEditor.state.doc, () => {}); })()`);
    await sleep(300);
    await evaluate(`[...document.querySelectorAll(".modal .btn")].find((b) => b.textContent.includes("${"Usar"}") || b.textContent.includes("Use"))?.click()`);
    await sleep(300);
    await evaluate(`[...document.querySelectorAll(".modal .btn.primary")].pop().click()`);
    await sleep(400); await shot("24-assistente");
    await evaluate(`document.querySelector(".modal-x").click()`);
    await evaluate(`window.deskpalsEditor.go("test")`);
    await sleep(2500); await shot("25-teste");
    report.features.testPlaying = await evaluate(`[...document.querySelectorAll("#page .hint")].map((x) => x.textContent).find((s) => /:/.test(s)) || ""`);
  }

  writeFileSync(join(outDir, "report.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ errors: report.errors.slice(0, 15), errorCount: report.errors.length,
    pets: report.pets.map((p) => ({ pet: p.pet, opened: p.opened, sameAfterSave: p.sameAfterSave, problems: p.problems.length, firstProblems: p.problems.slice(0, 3) })), edits: report.edits, features: report.features }, null, 1));
  ws.close();
} finally {
  chrome.kill(); server.kill();
}
