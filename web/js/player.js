/* ---------------------------------------------------------------
   player.js — a prévia de uma animação: os quadros na ordem, com o
   intervalo indo do início ao fim, a repetição, o "repetir a partir
   do quadro", o deslocamento, a opacidade e o movimento (no lugar ou
   andando pela prévia). Ao terminar, a mesma animação começa de novo
   (a página Testar mostra o pet de verdade, trocando de animação).
   --------------------------------------------------------------- */
import { evaluateExpression } from "./pet-engine.js";
import { kid, kids, text } from "./doc.js";

export class AnimPlayer {
  constructor(canvas, doc) {
    this.canvas = canvas;
    this.g = canvas.getContext("2d");
    this.doc = doc;
    this.anim = null;
    this.playing = true;
    this.walk = false;      // andando pela prévia, ou no lugar
    this.step = 0;
    this.x = 0;
    this.flipped = false;
    this.next = 0;
    this.timer = 0;
    this.onFrame = () => {};
  }

  context() {
    const w = this.doc.frameW || 64, h = this.doc.frameH || 64;
    return { screenW: 1280, screenH: 720, areaW: 1280, areaH: 680, imageW: w, imageH: h, imageX: 100, imageY: 100,
      random: Math.floor(Math.random() * 100), randS: 50, scale: 1 };
  }

  /** A animação a tocar (o elemento <animation>); começa do primeiro quadro. */
  setAnimation(animEl) {
    this.anim = animEl;
    this.restart();
  }

  restart() {
    this.step = 0;
    this.flipped = false;
    this.x = 0;
    this.read();
    this.schedule(0);
  }

  /** Os valores da animação, lidos de novo a cada volta (as fórmulas com random mudam). */
  read() {
    const a = this.anim;
    const seq = kid(a, "sequence");
    this.frames = kids(seq, "frame").map((f) => parseInt(f.textContent, 10) || 0);
    const ctx = this.context();
    this.repeatFrom = Math.max(0, parseInt(seq ? seq.getAttribute("repeatfrom") || "0" : "0", 10) || 0);
    this.repeat = Math.max(0, evaluateExpression(seq ? seq.getAttribute("repeat") || "0" : "0", ctx));
    const n = this.frames.length;
    this.total = n === 0 ? 0 : n + Math.max(0, n - Math.min(this.repeatFrom, n - 1)) * this.repeat;
    this.action = (text(seq, "action") || (seq && seq.getAttribute("action")) || "none").toLowerCase();
    const mv = (name) => {
      const el = kid(a, name);
      return {
        x: evaluateExpression(text(el, "x", "0"), ctx), y: evaluateExpression(text(el, "y", "0"), ctx),
        interval: Math.max(10, evaluateExpression(text(el, "interval", "200"), ctx) || 200),
        offsetY: parseInt(text(el, "offsety", "0"), 10) || 0,
        opacity: text(el, "opacity", "1") === "" ? 1 : parseFloat(text(el, "opacity", "1")),
      };
    };
    this.start = mv("start");
    this.end = mv("end");
  }

  /** O quadro do passo atual (com o "repetir a partir do quadro"). */
  frameAt(step) {
    const n = this.frames.length;
    if (n === 0) return 0;
    if (step < n) return this.frames[step];
    const from = Math.min(this.repeatFrom, n - 1);
    return this.frames[from + ((step - n) % (n - from))];
  }

  /** Quanto do caminho do início ao fim já foi (0..1): o intervalo, o movimento e a opacidade vão mudando. */
  progress() { return this.total <= 1 ? 0 : this.step / (this.total - 1); }
  lerp(a, b) { return a + (b - a) * this.progress(); }

  schedule(ms) {
    clearTimeout(this.timer);
    if (!this.playing || !this.anim) { this.draw(); return; }
    this.timer = setTimeout(() => this.tick(), ms);
  }

  /** O passo seguinte: anda (andando pela prévia), e ao fim vira (flip) e começa de novo. */
  advance() {
    const dx = this.lerp(this.start.x, this.end.x);
    if (this.walk) this.x += (this.flipped ? -dx : dx);
    this.step++;
    if (this.step >= this.total) {
      if (this.action === "flip") this.flipped = !this.flipped;
      this.step = 0;
      this.read();
    }
  }

  tick() {
    if (!this.anim) return;
    if (this.total === 0) { this.draw(); return; }
    this.draw();
    this.advance();
    this.schedule(Math.round(this.lerp(this.start.interval, this.end.interval)));
  }

  /** Um passo só (com a prévia pausada). */
  stepOnce() {
    clearTimeout(this.timer);
    if (!this.anim || !this.total) return;
    this.advance();
    this.draw();
  }

  play(on) { this.playing = on; this.schedule(0); }
  stop() { clearTimeout(this.timer); this.anim = null; }

  draw() {
    const c = this.canvas, g = this.g;
    g.clearRect(0, 0, c.width, c.height);
    const fw = this.doc.frameW, fh = this.doc.frameH;
    if (!this.doc.sheet || !fw || !fh || !this.anim) return;
    // o maior tamanho inteiro que cabe (pixel art fica nítida)
    const z = Math.max(1, Math.floor(Math.min((c.width - 8) / fw, (c.height - 16) / fh)));
    const w = fw * z, h = fh * z;
    let x = (c.width - w) / 2;
    if (this.walk) {
      const span = c.width + w;
      x = ((((x + this.x * z) % span) + span) % span) - w / 2;
    }
    const offset = this.lerp(this.start.offsetY, this.end.offsetY) * z;
    const y = c.height - h - 8 + offset;
    // o chão
    g.fillStyle = "rgba(0,0,0,.12)";
    g.beginPath(); g.ellipse(x + w / 2, c.height - 9, w * 0.3, 3, 0, 0, Math.PI * 2); g.fill();
    // o quadro anterior, meio apagado, por baixo (para alinhar um quadro com o outro)
    if (this.onion && this.total > 1) {
      g.globalAlpha = 0.3;
      this.doc.drawFrame(g, this.frameAt(this.step > 0 ? this.step - 1 : this.total - 1), x, y, w, h, this.flipped);
    }
    g.globalAlpha = Math.max(0, Math.min(1, this.lerp(this.start.opacity, this.end.opacity)));
    // o pet olha para a esquerda; espelhado ele anda para a direita
    this.doc.drawFrame(g, this.frameAt(this.step), x, y, w, h, this.flipped);
    g.globalAlpha = 1;
    this.onFrame(this.step, this.frameAt(this.step));
  }
}
