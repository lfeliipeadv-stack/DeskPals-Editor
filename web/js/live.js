/* ---------------------------------------------------------------
   live.js — muitas animações tocando ao mesmo tempo, num relógio
   só: as miniaturas da lista de animações e o quadro "Todas as
   animações". Cada uma segue a própria sequência (intervalo,
   repetição, "repetir a partir do quadro", virar no fim), no lugar.
   Só as que aparecem na tela andam; com "reduzir movimento" no
   sistema, ficam paradas no primeiro quadro.
   --------------------------------------------------------------- */
import { AnimPlayer } from "./player.js";

const still = () => { try { return matchMedia("(prefers-reduced-motion: reduce)").matches; } catch { return false; } };

export class LiveSet {
  constructor(doc) {
    this.doc = doc;
    this.items = [];
    this.playing = !still();
    this.raf = 0;
    this.seen = new IntersectionObserver((list) => { for (const e of list) { const p = e.target._live; if (p) p.visible = e.isIntersecting; } });
    this.loop = this.loop.bind(this);
  }

  /** Uma tela que toca esta animação (o elemento <animation>). */
  add(canvas, animEl) {
    const p = new AnimPlayer(canvas, this.doc);
    p.anim = animEl;
    p.read();
    p.due = 0;
    p.visible = false;
    canvas._live = p;
    this.items.push(p);
    this.seen.observe(canvas);
    this.paint(p);
    if (this.playing && !this.raf) this.raf = requestAnimationFrame(this.loop);
    return p;
  }

  loop(now) {
    this.raf = 0;
    if (!this.playing) return;
    for (const p of this.items) {
      if (!p.visible || !p.total || now < p.due) continue;
      p.advance();
      this.paint(p);
      p.due = now + Math.max(16, Math.round(p.lerp(p.start.interval, p.end.interval)));
    }
    this.raf = requestAnimationFrame(this.loop);
  }

  /** O quadro atual, do maior tamanho que cabe na tela (inteiro quando dá, para a pixel art ficar nítida). */
  paint(p) {
    const c = p.canvas, g = p.g, doc = this.doc;
    g.clearRect(0, 0, c.width, c.height);
    const fw = doc.frameW, fh = doc.frameH;
    if (!doc.sheet || !fw || !fh || !p.total) return;
    let z = Math.min(c.width / fw, c.height / fh);
    if (z >= 1) z = Math.floor(z);
    const w = fw * z, h = fh * z;
    g.imageSmoothingEnabled = z < 1;
    g.globalAlpha = Math.max(0.15, Math.min(1, p.lerp(p.start.opacity, p.end.opacity)));
    doc.drawFrame(g, p.frameAt(p.step), (c.width - w) / 2, (c.height - h) / 2, w, h, p.flipped);
    g.globalAlpha = 1;
  }

  play(on) {
    this.playing = on;
    if (on && !this.raf) { const t = performance.now(); for (const p of this.items) p.due = t; this.raf = requestAnimationFrame(this.loop); }
  }

  /** Volta todas ao primeiro quadro (para ver as que começam juntas). */
  restart() { const t = performance.now(); for (const p of this.items) { p.step = 0; p.flipped = false; p.read(); p.due = t; this.paint(p); } }

  stop() {
    cancelAnimationFrame(this.raf);
    this.raf = 0;
    this.seen.disconnect();
    this.items = [];
  }
}
