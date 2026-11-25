/* ---------------------------------------------------------------
   ui.js — o que todas as páginas usam: montar elementos, avisos,
   janelas (modais), menus e escolher arquivos.
   --------------------------------------------------------------- */
import { t } from "./i18n.js";

/** Um elemento: h("button", { class: "btn", onclick }, "texto", filho…). */
export function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") el.className = v;
    else if (k === "text") el.textContent = v;
    else if (k === "style") el.style.cssText = v;
    else if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
    else if (k === "value") el.value = v;
    else if (k === "checked") el.checked = v;
    else el.setAttribute(k, v === true ? "" : v);
  }
  for (const c of children.flat()) if (c !== null && c !== undefined && c !== false) el.append(c instanceof Node ? c : String(c));
  return el;
}

export const $ = (s, r = document) => r.querySelector(s);

let toastTimer = 0;
/** Um aviso curto embaixo da página. */
export function toast(msg, ms = 3200) {
  let el = $(".toast");
  if (!el) { el = h("div", { class: "toast", role: "status" }); document.body.append(el); }
  el.textContent = msg;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.remove(), ms);
}

/** Escolher arquivos do computador (um, ou vários com multiple). */
export function pickFile(accept, multiple = false) {
  return new Promise((res) => {
    const input = h("input", { type: "file", accept, style: "display:none" });
    if (multiple) input.multiple = true;
    input.addEventListener("change", () => { res(multiple ? [...input.files] : input.files[0] || null); input.remove(); });
    // sem escolha (cancelar), a promessa fica sem resposta: o foco volta à página e nada acontece
    document.body.append(input);
    input.click();
  });
}

/**
 * Uma janela por cima da página. buttons: [{ text, primary, danger, onClick }]; onClick devolve false para a janela
 * continuar aberta. Fecha com Esc, com o × ou clicando fora. Devolve { el, body, close }.
 */
export function modal({ title, body, buttons = [], wide = false, onClose } = {}) {
  const back = h("div", { class: "modal-back" });
  const box = h("div", { class: "modal" + (wide ? " wide" : ""), role: "dialog", "aria-modal": "true", "aria-label": title || "" });
  const content = h("div", { class: "modal-body" }, body);
  let closed = false;
  const close = () => {
    if (closed) return;
    closed = true;
    back.remove();
    document.removeEventListener("keydown", onKey, true);
    onClose && onClose();
  };
  const onKey = (e) => { if (e.key === "Escape") { e.stopPropagation(); close(); } };
  const bar = h("div", { class: "modal-bar" });
  for (const b of buttons) {
    const btn = h("button", { class: "btn" + (b.primary ? " primary" : "") + (b.danger ? " danger" : ""), text: b.text });
    btn.addEventListener("click", async () => {
      const r = b.onClick ? await b.onClick() : undefined;
      if (r !== false) close();
    });
    bar.append(btn);
  }
  box.append(h("div", { class: "modal-head" }, h("h2", { text: title || "" }),
    h("button", { class: "btn icon modal-x", text: "×", title: t("build.cancel"), onclick: close })), content);
  if (buttons.length) box.append(bar);
  back.append(box);
  back.addEventListener("mousedown", (e) => { if (e.target === back) close(); });
  document.addEventListener("keydown", onKey, true);
  document.body.append(back);
  // o primeiro campo (ou o botão principal) recebe o foco
  setTimeout(() => (box.querySelector("input, select, textarea") || bar.querySelector(".primary") || box.querySelector("button"))?.focus(), 0);
  return { el: box, body: content, close, bar };
}

/** Um aviso numa janela (OK). */
export function alertBox(text, title = "") {
  return new Promise((res) => modal({ title, body: h("p", { class: "pre", text }), buttons: [{ text: "OK", primary: true, onClick: () => res() }], onClose: res }));
}

/** Uma pergunta numa janela: true no botão principal. */
export function confirmBox(text, { title = "", ok = "OK", cancel = t("build.cancel"), danger = false } = {}) {
  return new Promise((res) => {
    let answered = false;
    modal({
      title, body: h("p", { class: "pre", text }),
      buttons: [{ text: cancel, onClick: () => { answered = true; res(false); } }, { text: ok, primary: !danger, danger, onClick: () => { answered = true; res(true); } }],
      onClose: () => { if (!answered) res(false); },
    });
  });
}

/** Um texto pedido numa janela; null quando cancela. */
export function promptBox(text, value = "", { title = "" } = {}) {
  return new Promise((res) => {
    const input = h("input", { type: "text", value });
    let answered = false;
    const ok = () => { answered = true; res(input.value); };
    input.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); ok(); m.close(); } });
    const m = modal({
      title, body: h("label", { class: "field" }, h("span", { text }), input),
      buttons: [{ text: t("build.cancel"), onClick: () => { answered = true; res(null); } }, { text: "OK", primary: true, onClick: ok }],
      onClose: () => { if (!answered) res(null); },
    });
  });
}

/** Um menu pequeno no ponto do mouse: items = [{ text, onClick, danger, disabled }] ou "-" para uma linha. */
export function popupMenu(x, y, items) {
  document.querySelector(".popmenu")?.remove();
  const menu = h("div", { class: "popmenu", role: "menu" });
  const close = () => { menu.remove(); document.removeEventListener("mousedown", outside, true); document.removeEventListener("keydown", key, true); };
  const outside = (e) => { if (!menu.contains(e.target)) close(); };
  const key = (e) => { if (e.key === "Escape") close(); };
  for (const it of items) {
    if (it === "-") { menu.append(h("hr")); continue; }
    menu.append(h("button", { role: "menuitem", class: (it.danger ? "danger" : "") + (it.on ? " on" : ""), disabled: it.disabled, onclick: () => { close(); it.onClick(); } }, it.icon || null, it.text));
  }
  document.body.append(menu);
  const r = menu.getBoundingClientRect();
  menu.style.left = Math.max(6, Math.min(x, innerWidth - r.width - 6)) + "px";
  menu.style.top = Math.max(6, Math.min(y, innerHeight - r.height - 6)) + "px";
  setTimeout(() => { document.addEventListener("mousedown", outside, true); document.addEventListener("keydown", key, true); menu.querySelector("button:not([disabled])")?.focus(); }, 0);
  return close;
}

/** Um interruptor com texto (um checkbox bonito). */
export function toggle(label, on, onChange) {
  const input = h("input", { type: "checkbox", checked: on });
  input.addEventListener("change", () => onChange(input.checked));
  return h("label", { class: "toggle" }, input, h("span", { text: label }));
}
