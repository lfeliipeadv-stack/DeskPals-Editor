/* ---------------------------------------------------------------
   sources.js — de onde vêm os pets da galeria: a do DeskPals e
   qualquer repositório do GitHub que a pessoa colar, como no
   DeskPals (GallerySources.cs). O editor procura, nesta ordem:
     1. o pets.json da pasta dada (ou de Pets/, ou da raiz);
     2. sem lista, toda pasta com um animations.xml (a árvore de
        arquivos do GitHub; 60 perguntas por hora sem login).
   As fontes e as listas achadas ficam guardadas neste navegador.
   --------------------------------------------------------------- */

const NAME = "[A-Za-z0-9](?:[A-Za-z0-9._-]{0,99})";
const STORE = "deskpals-editor-sources";
/** Mais pets que uma busca lista sem pets.json. */
export const MAX_SCANNED = 500;

export const OFFICIAL = { owner: "lfeliipeadv-stack", repo: "DeskPals", branch: "master", path: "Pets", official: true };

export const sourceKey = (s) => `${s.owner}/${s.repo}`.toLowerCase() + (s.path ? ":" + s.path.toLowerCase() : "");
export const sourceLabel = (s) => s.official ? "DeskPals" : `${s.owner}/${s.repo}` + (s.path && s.path !== "Pets" ? "/" + s.path : "");

/** Uma pasta sem "..", sem barra no começo e sem caracteres estranhos. */
const safePath = (p) => p.split("/").every((x) => x && x !== "." && x !== ".." && !/[\\:*?"<>|]/.test(x));

/**
 * Um link do GitHub (github.com/dono/repo, …/tree/ramo/pasta, …/blob/ramo/pasta/animations.xml, um link raw)
 * ou "dono/repo". Null quando não é um. O ramo e a pasta que faltam são achados por checkSource.
 */
export function parseSource(text) {
  text = (text || "").trim().replace(/\/+$/, "");
  if (!text) return null;
  let owner, repo, branch = null, path = null, file = false, rest = "";
  let m = new RegExp(`^(?:https?://)?(?:www\\.)?github\\.com/(${NAME})/(${NAME})(?:\\.git)?(?:/(tree|blob)/([^/\\s?#]+)(/[^\\s?#]*)?)?/?(?:[?#].*)?$`, "i").exec(text);
  if (m) {
    [, owner, repo] = m;
    if (m[4]) branch = decodeURIComponent(m[4]);
    rest = m[5] ? decodeURIComponent(m[5]).replace(/^\/+|\/+$/g, "") : "";
    file = (m[3] || "").toLowerCase() === "blob";
  } else if ((m = new RegExp(`^(?:https?://)?raw\\.githubusercontent\\.com/(${NAME})/(${NAME})/([^/\\s?#]+)(/[^\\s?#]*)?$`, "i").exec(text))) {
    [, owner, repo] = m;
    branch = decodeURIComponent(m[3]);
    rest = m[4] ? decodeURIComponent(m[4]).replace(/^\/+|\/+$/g, "") : "";
    file = /\.[a-z0-9]+$/i.test(rest);
  } else if ((m = new RegExp(`^(${NAME})/(${NAME})$`).exec(text))) {
    [, owner, repo] = m;
  } else return null;
  // um link para um arquivo (animations.xml, pets.json, um leia-me…) vale a pasta dele
  if (file) path = rest.includes("/") ? rest.slice(0, rest.lastIndexOf("/")) : "";
  else if (rest) path = rest;
  if (path && !safePath(path)) return null;
  return { owner, repo: repo.replace(/\.git$/i, ""), branch, path };
}

const enc = (p) => p.split("/").filter(Boolean).map(encodeURIComponent).join("/");
/** O endereço da pasta de uma fonte, com a barra no fim. */
export const baseUrl = (s) => `https://raw.githubusercontent.com/${s.owner}/${s.repo}/${enc(s.branch)}/` + (s.path ? enc(s.path) + "/" : "");
/** O endereço da pasta de um pet de uma fonte, com a barra no fim. */
export const petUrl = (s, p) => baseUrl(s) + (p.path ? enc(p.path) + "/" : p.path === "" ? "" : enc(p.folder) + "/");

/** Os pets de uma lista como o editor usa: "folder" um nome simples e "path" onde ficam os arquivos. */
export function normalize(list) {
  const out = [];
  for (const p of list || []) {
    if (!p || typeof p.folder !== "string" || !p.folder.trim()) continue;
    const q = { ...p };
    const folder = q.folder.replace(/^\/+|\/+$/g, "");
    if (typeof q.path !== "string") q.path = folder.includes("/") ? folder : undefined;
    if (q.path && !safePath(q.path)) continue;
    if (!q.path && !safePath(folder)) continue;
    q.folder = folder.slice(folder.lastIndexOf("/") + 1);
    out.push(q);
  }
  return out;
}

/** "gsheep-green" vira "Gsheep Green". */
export const niceName = (p) => (p.name && String(p.name).trim()) ||
  p.folder.replace(/[_-]+/g, " ").split(" ").filter(Boolean).map((w) => w[0].toUpperCase() + w.slice(1)).join(" ");

export class SourceError extends Error {
  constructor(kind, arg) { super(kind); this.kind = kind; this.arg = arg; }
}

async function api(what) {
  let r;
  try { r = await fetch("https://api.github.com/" + what, { headers: { Accept: "application/vnd.github+json" } }); }
  catch { return { status: 0, json: null, limited: false }; }
  const left = r.headers.get("x-ratelimit-remaining");
  const limited = (r.status === 403 || r.status === 429) && (left === null || left === "0");
  const reset = +r.headers.get("x-ratelimit-reset") || 0;
  return { status: r.status, json: r.ok ? await r.json() : null, limited, reset };
}

async function fetchList(s) {
  let r;
  try { r = await fetch(baseUrl(s) + "pets.json", { cache: "no-cache" }); } catch { return null; }
  if (!r.ok) return null;
  try {
    const text = (await r.text()).replace(/^﻿/, "");
    if (text.length > 4_000_000) return null;
    const pets = normalize(JSON.parse(text).pets);
    return pets.length ? pets : null;
  } catch { return null; }   // um arquivo com esse nome que não é uma lista de pets
}

/** Os pets de uma fonte que já tem ramo e pasta (a do DeskPals, ou uma guardada): o pets.json, ou a árvore. */
export async function listPets(s) {
  if (s.scanned) {
    const found = await scan({ ...s });
    if (!found) throw new SourceError("none", sourceLabel(s));
    return found;
  }
  const pets = await fetchList(s);
  if (!pets) throw new SourceError("none", sourceLabel(s));
  return pets;
}

/** A lista feita da árvore de arquivos do GitHub: toda pasta com um animations.xml dentro de s.path. */
async function scan(s) {
  const r = await api(`repos/${s.owner}/${s.repo}/git/trees/${encodeURIComponent(s.branch)}?recursive=1`);
  if (r.limited) throw new SourceError("limit", r.reset);
  if (!r.json || !Array.isArray(r.json.tree)) return null;
  const blobs = r.json.tree.filter((e) => e && e.type === "blob" && typeof e.path === "string");
  const all = new Set(blobs.map((e) => e.path));
  const xmls = blobs.filter((e) => e.path === "animations.xml" || e.path.endsWith("/animations.xml"));
  if (s.path == null) s.path = xmls.length && xmls.every((x) => x.path.startsWith("Pets/")) ? "Pets" : "";
  const prefix = s.path ? s.path + "/" : "";
  const pets = [];
  for (const x of xmls) {
    if (!x.path.startsWith(prefix)) continue;
    const dir = x.path.slice(prefix.length, x.path.length - "animations.xml".length).replace(/\/+$/, "");
    const folder = dir ? dir.slice(dir.lastIndexOf("/") + 1) : (s.path ? s.path.slice(s.path.lastIndexOf("/") + 1) : s.repo);
    pets.push({ folder, path: dir, author: s.owner, icon: all.has(prefix + (dir ? dir + "/" : "") + "icon.png") });
    if (pets.length >= MAX_SCANNED) break;
  }
  pets.truncated = !!r.json.truncated || xmls.length > pets.length && pets.length >= MAX_SCANNED;
  return pets;
}

/**
 * O repositório tem mesmo pets. Acha o que não foi dado: o ramo (o padrão do repositório; master ou main quando o
 * GitHub não responde), a pasta (Pets, depois a raiz) e a lista (o pets.json, ou toda pasta com animations.xml).
 * Devolve { source, pets }; lança SourceError("notFound" | "none" | "limit").
 */
export async function checkSource(given) {
  const s = { owner: given.owner, repo: given.repo };
  let branches = given.branch ? [given.branch] : null;
  let limited = 0;
  if (!branches) {
    const r = await api(`repos/${s.owner}/${s.repo}`);
    if (r.status === 404) throw new SourceError("notFound", `${s.owner}/${s.repo}`);
    if (r.limited) limited = r.reset || 1;
    if (r.json) { s.repo = r.json.name || s.repo; s.owner = r.json.owner?.login || s.owner; }
    branches = r.json?.default_branch ? [r.json.default_branch] : ["master", "main"];
  }
  const folders = given.path != null ? [given.path] : ["Pets", ""];
  for (const branch of branches)
    for (const path of folders) {
      const pets = await fetchList({ ...s, branch, path });
      if (pets) return { source: { ...s, branch, path, scanned: false }, pets };
    }
  for (const branch of branches) {
    const probe = { ...s, branch, path: given.path };
    let pets;
    try { pets = await scan(probe); }
    catch (e) { if (e.kind === "limit") { limited = e.arg || 1; continue; } throw e; }
    if (pets && pets.length) return { source: { ...s, branch, path: probe.path, scanned: true }, pets };
  }
  throw limited ? new SourceError("limit", limited) : new SourceError("none", `${s.owner}/${s.repo}` + (given.path ? "/" + given.path : ""));
}

/* ---- guardadas neste navegador (com a última lista achada, para não gastar as perguntas ao GitHub) ---- */

export function savedSources() {
  try {
    const list = JSON.parse(localStorage.getItem(STORE) || "[]");
    return Array.isArray(list) ? list.filter((x) => x && x.owner && x.repo && x.branch) : [];
  } catch { return []; }
}

export function saveSources(list) {
  try { localStorage.setItem(STORE, JSON.stringify(list)); } catch { /* sem armazenamento: vale só nesta visita */ }
}
