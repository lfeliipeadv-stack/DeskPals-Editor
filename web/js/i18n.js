/* Os textos do editor em português, inglês e espanhol: os do Editor de Pets para Windows (texts.js) e os só da
   versão web (aqui). A língua vem do navegador e pode ser trocada; fica guardada neste navegador. */
import { TEXTS } from "./texts.js";

export const LANGS = [["en", "English"], ["pt", "Português"], ["es", "Español"]];

/** A bandeira de cada idioma, em SVG (o Windows não desenha emoji de bandeira). */
const FLAGS = {
  en: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 19 10"><rect width="19" height="10" fill="#b22234"/><path d="M0 1.15h19M0 2.7h19M0 4.23h19M0 5.77h19M0 7.3h19M0 8.85h19" stroke="#fff" stroke-width=".77"/><rect width="7.6" height="5.38" fill="#3c3b6e"/><g fill="#fff"><circle cx="1.3" cy="1" r=".35"/><circle cx="3.8" cy="1" r=".35"/><circle cx="6.3" cy="1" r=".35"/><circle cx="2.55" cy="2.15" r=".35"/><circle cx="5.05" cy="2.15" r=".35"/><circle cx="1.3" cy="3.3" r=".35"/><circle cx="3.8" cy="3.3" r=".35"/><circle cx="6.3" cy="3.3" r=".35"/><circle cx="2.55" cy="4.45" r=".35"/><circle cx="5.05" cy="4.45" r=".35"/></g></svg>',
  pt: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 14"><rect width="20" height="14" fill="#009c3b"/><path d="M10 1.6 18.2 7 10 12.4 1.8 7z" fill="#ffdf00"/><circle cx="10" cy="7" r="3.3" fill="#002776"/><path d="M6.8 6.3c2.2-.5 4.6-.2 6.4.9" stroke="#fff" stroke-width=".5" fill="none"/></svg>',
  es: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 14"><rect width="20" height="14" fill="#aa151b"/><rect y="3.5" width="20" height="7" fill="#f1bf00"/></svg>',
};
export function flagImg(code) {
  const img = document.createElement("img");
  img.className = "flag";
  img.alt = "";
  img.src = "data:image/svg+xml," + encodeURIComponent(FLAGS[code] || FLAGS.en);
  return img;
}

const WEB = {
  "web.title": ["DeskPals Editor", "DeskPals Editor", "DeskPals Editor"],
  "web.tagline": ["Crie e edite pets do DeskPals direto no navegador. Nada é enviado: o pet fica no seu computador.",
    "Create and edit DeskPals pets right in the browser. Nothing is uploaded: the pet stays on your computer.",
    "Crea y edita mascotas de DeskPals en el navegador. No se sube nada: la mascota se queda en tu equipo."],
  "web.openUrl": ["Abrir de um link", "Open from a link", "Abrir desde un enlace"],
  "web.openUrlHint": ["O link de um pet no GitHub (a pasta dele ou o animations.xml).", "The link of a pet on GitHub (its folder or its animations.xml).", "El enlace de una mascota en GitHub (su carpeta o su animations.xml)."],
  "web.openUrlAsk": ["Cole o link do pet no GitHub:", "Paste the link of the pet on GitHub:", "Pega el enlace de la mascota en GitHub:"],
  "web.download": ["Baixar animations.xml", "Download animations.xml", "Descargar animations.xml"],
  "web.downloadHint": ["Salva o pet no seu computador. Para usar, ponha o arquivo na pasta do pet ou abra no DeskPals.", "Saves the pet on your computer. To use it, put the file in the pet's folder or open it in DeskPals.", "Guarda la mascota en tu equipo. Para usarla, pon el archivo en la carpeta de la mascota o ábrelo en DeskPals."],
  "web.loading": ["Abrindo…", "Opening…", "Abriendo…"],
  "web.loadFail": ["Não foi possível abrir: {0}", "Could not open: {0}", "No se pudo abrir: {0}"],
  "web.noPet": ["Abra ou crie um pet na página Projeto.", "Open or create a pet on the Project page.", "Abre o crea una mascota en la página Proyecto."],
  "web.leave": ["O pet tem mudanças que não foram baixadas. Sair mesmo assim?", "The pet has changes that were not downloaded. Leave anyway?", "La mascota tiene cambios sin descargar. ¿Salir de todos modos?"],
  "web.unsaved": ["mudanças não baixadas", "changes not downloaded", "cambios sin descargar"],
  "web.saved": ["sem mudanças para baixar", "no changes to download", "sin cambios por descargar"],
  "web.language": ["Idioma", "Language", "Idioma"],
  "web.preview": ["Prévia", "Preview", "Vista previa"],
  "web.play": ["Tocar", "Play", "Reproducir"],
  "web.pause": ["Pausar", "Pause", "Pausar"],
  "web.inPlace": ["no lugar", "in place", "en el sitio"],
  "web.moving": ["andando", "moving", "moviéndose"],
  "web.frameN": ["quadro {0}", "frame {0}", "cuadro {0}"],
  "web.addFrame": ["Clique num quadro para acrescentá-lo à animação.", "Click a frame to add it to the animation.", "Haz clic en un cuadro para añadirlo a la animación."],
  "web.name": ["Nome", "Name", "Nombre"],
  "web.chance": ["chance", "chance", "probabilidad"],
  "web.where": ["onde vale", "where", "dónde vale"],
  "web.remove": ["remover", "remove", "quitar"],
  "web.recovered": ["O pet que estava aberto aqui foi recuperado.", "The pet that was open here was recovered.", "Se recuperó la mascota que estaba abierta aquí."],
  "web.discardRecovered": ["Descartar", "Discard", "Descartar"],
  "web.testIntro": ["O pet de verdade, com o mesmo motor do DeskPals: ele entra, anda na janela e na barra de tarefas, cai e troca de animação sozinho. ↻ começa de novo com as mudanças.",
    "The real pet, with the same engine as DeskPals: it comes in, walks on the window and the taskbar, falls and changes animation by itself. ↻ starts again with your changes.",
    "La mascota de verdad, con el mismo motor de DeskPals: entra, camina por la ventana y la barra de tareas, cae y cambia de animación sola. ↻ empieza de nuevo con tus cambios."],
  "web.gallery": ["Abrir da galeria", "Open from the gallery", "Abrir de la galería"],
  "web.galleryHint": ["Os pets da galeria do DeskPals: abra um para estudar como foi feito ou para criar uma variação.",
    "The pets of the DeskPals gallery: open one to study how it was made or to make a variation.",
    "Las mascotas de la galería de DeskPals: abre una para estudiar cómo se hizo o crear una variación."],
  "web.sheetClick": ["Clique num quadro para editar os pixels dele.", "Click a frame to edit its pixels.", "Haz clic en un cuadro para editar sus píxeles."],
  "web.testThis": ["Testar esta", "Test this one", "Probar esta"],
  "web.testThisHint": ["Abre o teste com o pet começando por esta animação.", "Opens the test with the pet starting with this animation.", "Abre la prueba con la mascota empezando por esta animación."],
  "web.usedBy": ["Onde é usada", "Where it is used", "Dónde se usa"],
  "web.usedByNone": ["Nada leva a ela (a não ser que o DeskPals a toque pelo nome).", "Nothing leads to it (unless DeskPals plays it by its name).", "Nada lleva a ella (salvo que DeskPals la reproduzca por su nombre)."],
  "web.playNow": ["Tocar agora", "Play now", "Reproducir ahora"],
  "web.testDrag": ["Pegue o pet com o mouse e jogue: ele é arremessado como no programa.", "Grab the pet with the mouse and throw it: it is tossed like in the program.", "Agarra la mascota con el ratón y lánzala: sale volando como en el programa."],
  "web.fromSky": ["Cair do céu", "Fall from the sky", "Caer del cielo"],
  "web.nAnims": ["{0} animações", "{0} animations", "{0} animaciones"],
  "web.credits": ["Feito para o DeskPals. Formato do desktopPet, de Adriano Petrucci, que também tem um editor online: desktopPet-Editor.",
    "Made for DeskPals. The desktopPet format, by Adriano Petrucci, who also has an online editor: desktopPet-Editor.",
    "Hecho para DeskPals. Formato de desktopPet, de Adriano Petrucci, que también tiene un editor en línea: desktopPet-Editor."],
};

let lang = "en";

export function initLang() {
  let saved = null;
  try { saved = localStorage.getItem("deskpals-editor-lang"); } catch { /* sem armazenamento: abre em inglês */ }
  // abre sempre em inglês; quem escolher outro idioma no seletor continua nele nas próximas visitas
  lang = LANGS.some(([c]) => c === saved) ? saved : "en";
  document.documentElement.lang = lang === "pt" ? "pt-BR" : lang;
  return lang;
}

export function setLang(code) {
  lang = code;
  try { localStorage.setItem("deskpals-editor-lang", code); } catch { /* fica só nesta visita */ }
  document.documentElement.lang = code === "pt" ? "pt-BR" : code;
}

export function getLang() { return lang; }

/** Um texto na língua atual, com {0}, {1}… trocados pelos valores. */
export function t(key, ...args) {
  const row = WEB[key] || TEXTS[key];
  const i = lang === "en" ? 1 : lang === "es" ? 2 : 0;
  let s = row ? (row[i] || row[0]) : key;
  args.forEach((a, n) => { s = s.split("{" + n + "}").join(String(a)); });
  return s.replace(/\\n/g, "\n");
}
