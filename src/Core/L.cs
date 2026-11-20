using System.Globalization;

namespace PetEditor
{
	/// <summary>
	/// The texts of the editor ("key|pt|en|es", \n = new line). For now the editor offers Portuguese and English only:
	/// the Spanish column is kept for later, but not used.
	/// </summary>
	internal static class L
	{
		static int col = 0;   // 0 pt, 1 en
		static Dictionary<string, string[]> table;

		public static string Current => col == 1 ? "en" : "pt";

		/// <summary>"pt", "en", or "auto" (as Windows: Portuguese in a Portuguese Windows, English in any other).</summary>
		public static void Use(string lang)
		{
			col = lang switch
			{
				"pt" => 0,
				"en" => 1,
				_ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? 0 : 1,   // "auto", and "es" saved before
			};
		}

		/// <summary>A text in every language the editor has (to recognise it whatever the language was).</summary>
		public static string[] All(string key) { table ??= Load(); return table.TryGetValue(key, out var t) ? t : new[] { key }; }

		public static string T(string key, params object[] args)
		{
			table ??= Load();
			if (!table.TryGetValue(key, out var t)) { System.Diagnostics.Debug.WriteLine("missing text: " + key); return key; }
			string s = t[Math.Min(col, t.Length - 1)];
			return args.Length == 0 ? s : string.Format(s, args);
		}

		/// <summary>Self-check: keys used in the code that have no text.</summary>
		public static bool Has(string key) { table ??= Load(); return table.ContainsKey(key); }

		static Dictionary<string, string[]> Load()
		{
			var d = new Dictionary<string, string[]>();
			foreach (var raw in Texts.Split('\n'))
			{
				var line = raw.TrimEnd('\r');
				if (line.Trim().Length == 0 || line.StartsWith("//")) continue;
				var p = line.Split('|');
				if (p.Length < 4) continue;
				d[p[0].Trim()] = p.Skip(1).Take(3).Select(x => x.Replace("\\n", "\n").Replace("¦", "|")).ToArray();
			}
			return d;
		}

		const string Texts = @"
app.name|Editor de Pets|Pet Editor|Editor de Mascotas
doc.untitled|Pet sem nome|Untitled pet|Mascota sin nombre
doc.badXml|O arquivo não é um animations.xml válido.|The file is not a valid animations.xml.|El archivo no es un animations.xml válido.
doc.readmeBy|Pet feito por|Pet made by|Mascota hecha por
doc.notSaved|Ainda não salvo|Not saved yet|Aún no guardado
uses.anim|a animação “{0}” (#{1}) leva a ela|animation “{0}” (#{1}) leads to it|la animación “{0}” (#{1}) lleva a ella
uses.spawn|a entrada #{0} começa com ela|entrance #{0} starts with it|la entrada #{0} empieza con ella
uses.child|uma janela filha (#{0}) usa ela|a child window (#{0}) uses it|una ventana hija (#{0}) la usa
uses.sound|um som toca com ela|a sound plays with it|un sonido suena con ella
tpl.title|Meu novo pet|My new pet|Mi nueva mascota
tpl.name|Bolinha|Blobby|Bolita
tpl.info|Um pet novo, feito no Editor de Pets.|A new pet, made with the Pet Editor.|Una mascota nueva, hecha con el Editor de Mascotas.

act.new|Novo|New|Nuevo
act.open|Abrir|Open|Abrir
act.save|Salvar|Save|Guardar
act.saveAs|Salvar como|Save as|Guardar como
act.undo|Desfazer|Undo|Deshacer
act.redo|Refazer|Redo|Rehacer
act.check|Verificar|Check|Verificar
act.checkOk|Tudo certo|All good|Todo bien
act.checkCount|Verificar  ({0} erros, {1} avisos)|Check  ({0} errors, {1} warnings)|Verificar  ({0} errores, {1} avisos)
act.test|Testar no desktop|Test on the desktop|Probar en el escritorio

nav.project|Projeto|Project|Proyecto
nav.header|Cabeçalho|Header|Encabezado
nav.sheet|Folha de sprites|Sprite sheet|Hoja de sprites
nav.animations|Animações|Animations|Animaciones
nav.spawns|Entradas na tela|Entrances|Entradas en pantalla
nav.childs|Janelas filhas|Child windows|Ventanas hijas
nav.sounds|Sons|Sounds|Sonidos
nav.tools|FERRAMENTAS|TOOLS|HERRAMIENTAS
nav.check|Verificador|Checker|Verificador
nav.map|Mapa das animações|Animation map|Mapa de animaciones
nav.xml|XML|XML|XML
nav.settings|Configurações|Settings|Configuración

msg.openFail|Não foi possível abrir: {0}|Could not open: {0}|No se pudo abrir: {0}
msg.saveFail|Não foi possível salvar: {0}|Could not save: {0}|No se pudo guardar: {0}
msg.saveChanges|Salvar as mudanças em “{0}”?|Save the changes to “{0}”?|¿Guardar los cambios en “{0}”?
msg.noXmlInFolder|Esta pasta não tem um animations.xml.|This folder has no animations.xml.|Esta carpeta no tiene un animations.xml.
msg.saveWithErrors|O pet tem {0} erro(s) que podem travar o DesktopPet. Salvar mesmo assim?|The pet has {0} error(s) that can stop DesktopPet. Save anyway?|La mascota tiene {0} error(es) que pueden detener DesktopPet. ¿Guardar de todos modos?
open.folder|Escolha a pasta do pet (a que tem o animations.xml)|Choose the pet's folder (the one with animations.xml)|Elige la carpeta de la mascota (la que tiene animations.xml)
open.filter|Pet do DesktopPet (animations.xml)¦*.xml|DesktopPet pet (animations.xml)¦*.xml|Mascota de DesktopPet (animations.xml)¦*.xml

proj.title|Projeto|Project|Proyecto
proj.intro|Crie um pet novo ou abra um que já existe. O pet é um único arquivo, animations.xml, com as imagens, as animações e os sons.|Create a new pet or open one that exists. A pet is a single file, animations.xml, with its pictures, animations and sounds.|Crea una mascota nueva o abre una que ya existe. La mascota es un solo archivo, animations.xml, con las imágenes, las animaciones y los sonidos.
proj.new|Novo pet|New pet|Nueva mascota
proj.newHint|Começa com um modelo que já anda, vira, pisca e cai do céu.|Starts from a model that already walks, turns, blinks and falls from the sky.|Empieza con un modelo que ya camina, gira, parpadea y cae del cielo.
proj.openFile|Abrir arquivo|Open a file|Abrir archivo
proj.openFileHint|Um animations.xml de qualquer pasta.|An animations.xml from any folder.|Un animations.xml de cualquier carpeta.
proj.openFolder|Abrir pasta|Open a folder|Abrir carpeta
proj.openFolderHint|A pasta de um pet (com animations.xml, README e ícone).|A pet's folder (with animations.xml, README and icon).|La carpeta de una mascota (con animations.xml, README e icono).
proj.saveAs|Salvar como|Save as|Guardar como
proj.saveAsHint|Uma cópia deste pet em outro lugar.|A copy of this pet somewhere else.|Una copia de esta mascota en otro lugar.
proj.zip|Exportar como .zip|Export as .zip|Exportar como .zip
proj.zipHint|Um arquivo só, pronto para mandar: o pet, o ícone, a prévia animada e o README.|One file, ready to send: the pet, the icon, the animated preview and the README.|Un solo archivo, listo para enviar: la mascota, el icono, la vista previa animada y el README.
zip.filter|Arquivo zip (*.zip)¦*.zip|Zip file (*.zip)¦*.zip|Archivo zip (*.zip)¦*.zip
zip.done|Pronto: {0} ({1} KB). Quem receber descompacta a pasta dentro dos pets do DesktopPet.|Done: {0} ({1} KB). Whoever gets it unzips the folder into the pets of DesktopPet.|Listo: {0} ({1} KB). Quien lo reciba descomprime la carpeta dentro de las mascotas de DesktopPet.
proj.thisPet|Este pet: {0}|This pet: {0}|Esta mascota: {0}
proj.stats|{0} animações · {1} quadros de {2}×{3} px · {4} entradas · {5} janelas filhas · {6} sons|{0} animations · {1} frames of {2}×{3} px · {4} entrances · {5} child windows · {6} sounds|{0} animaciones · {1} cuadros de {2}×{3} px · {4} entradas · {5} ventanas hijas · {6} sonidos
proj.showFolder|Mostrar na pasta|Show in folder|Mostrar en la carpeta
proj.recent|Abertos recentemente|Recently opened|Abiertos recientemente
proj.noRecent|Nenhum ainda.|None yet.|Ninguno todavía.
proj.installed|Pets baixados no DesktopPet|Pets downloaded in DesktopPet|Mascotas descargadas en DesktopPet
proj.installedHint|Abra um deles para estudar como foi feito ou para criar uma variação (use “Salvar como” para não mexer no original).|Open one to see how it was made or to make a variation (use “Save as” to keep the original).|Abre una para ver cómo se hizo o para crear una variación (usa “Guardar como” para no tocar el original).
proj.noInstalled|Nenhum pet baixado neste computador.|No pet downloaded on this computer.|Ninguna mascota descargada en este equipo.

head.title|Cabeçalho|Header|Encabezado
head.intro|O nome e as informações que aparecem no DesktopPet (menu, galeria e “sobre”).|The name and details shown in DesktopPet (menu, gallery and “about”).|El nombre y la información que aparecen en DesktopPet (menú, galería y “acerca de”).
head.petname|Nome do pet|Pet name|Nombre de la mascota
head.petnameHint|Curto (até 16 letras): aparece no menu e nos balões.|Short (up to 16 letters): shown in the menu and the bubbles.|Corto (hasta 16 letras): aparece en el menú y los globos.
head.titleField|Título|Title|Título
head.author|Autor|Author|Autor
head.version|Versão|Version|Versión
head.info|Descrição|Description|Descripción
head.infoHint|Cada linha vira uma linha na janela “sobre”.|Each line becomes a line in the “about” window.|Cada línea se convierte en una línea en la ventana “acerca de”.
head.icon|Ícone|Icon|Icono
head.iconFrame|Usar um quadro|Use a frame|Usar un cuadro
head.iconFile|Importar imagem|Import a picture|Importar imagen
head.iconFilter|Imagens¦*.png;*.ico;*.bmp;*.gif|Pictures¦*.png;*.ico;*.bmp;*.gif|Imágenes¦*.png;*.ico;*.bmp;*.gif
head.iconHint|O ícone é gerado em 16, 32, 48 e 64 px, recortado no desenho e com os pixels nítidos.|The icon is made at 16, 32, 48 and 64 px, cropped to the drawing, with crisp pixels.|El icono se genera en 16, 32, 48 y 64 px, recortado al dibujo y con píxeles nítidos.

sheet.page|Folha de sprites|Sprite sheet|Hoja de sprites
sheet.title|Folha de sprites|Sprite sheet|Hoja de sprites
sheet.intro|Uma imagem PNG com todos os quadros do pet, do mesmo tamanho, em linhas e colunas. Diga quantas colunas e linhas ela tem.|A PNG picture with every frame of the pet, all the same size, in rows and columns. Tell how many columns and rows it has.|Una imagen PNG con todos los cuadros de la mascota, del mismo tamaño, en filas y columnas. Indica cuántas columnas y filas tiene.
sheet.clickHint|clique num quadro para pôr na animação|click a frame to add it to the animation|haz clic en un cuadro para añadirlo a la animación
sheet.import|Importar imagem|Import picture|Importar imagen
sheet.export|Exportar PNG|Export PNG|Exportar PNG
sheet.cols|Colunas|Columns|Columnas
sheet.rows|Linhas|Rows|Filas
sheet.transp|Cor transparente|Transparent colour|Color transparente
sheet.transpHint|A cor do fundo que some (normalmente Magenta). Imagens com transparência própria não precisam dela.|The background colour that disappears (usually Magenta). Pictures with their own transparency do not need it.|El color de fondo que desaparece (normalmente Magenta). Las imágenes con transparencia propia no lo necesitan.
sheet.skins|Skins (cores)|Skins (colours)|Skins (colores)
sheet.skinsHint|Se a folha tem o pet em várias cores (uma depois da outra), quantas são.|If the sheet has the pet in several colours (one after the other), how many.|Si la hoja tiene la mascota en varios colores (uno tras otro), cuántos son.
sheet.view|Ver como|View as|Ver como
sheet.zoomThumbs|Miniaturas|Thumbnails|Miniaturas
sheet.filter|Imagens¦*.png;*.gif;*.bmp|Pictures¦*.png;*.gif;*.bmp|Imágenes¦*.png;*.gif;*.bmp
sheet.imported|Imagem de {0}×{1} px importada: {2} quadros de {3}×{4} px. Ajuste as colunas e linhas se os quadros não ficaram certos.|Picture of {0}×{1} px imported: {2} frames of {3}×{4} px. Change columns and rows if the frames are not right.|Imagen de {0}×{1} px importada: {2} cuadros de {3}×{4} px. Ajusta columnas y filas si los cuadros no quedaron bien.
sheet.info|Imagem: {0}×{1} px\n{2} quadros de {3}×{4} px|Picture: {0}×{1} px\n{2} frames of {3}×{4} px|Imagen: {0}×{1} px\n{2} cuadros de {3}×{4} px
sheet.notExact|A imagem não se divide exatamente nessa grade: confira colunas e linhas.|The picture does not split exactly in this grid: check columns and rows.|La imagen no se divide exactamente en esta cuadrícula: revisa columnas y filas.
sheet.empty|Ainda não há folha de sprites. Importe uma imagem na página Folha de sprites.|No sprite sheet yet. Import a picture in the Sprite sheet page.|Todavía no hay hoja de sprites. Importa una imagen en la página Hoja de sprites.

anim.count|Animações ({0})|Animations ({0})|Animaciones ({0})
anim.new|Nova|New|Nueva
anim.search|Buscar por nome ou número|Search by name or number|Buscar por nombre o número
anim.searchShort|Buscar|Search|Buscar
anim.sub|#{0}  ·  {1} quadros|#{0}  ·  {1} frames|#{0}  ·  {1} cuadros
anim.hiddenShort|fora do painel|not in the panel|fuera del panel
anim.none|Nenhuma animação. Crie uma com “+ Nova”.|No animation. Create one with “+ New”.|Ninguna animación. Crea una con “+ Nueva”.
anim.inPicker|Aparece no painel de animações|Shown in the animation panel|Aparece en el panel de animaciones
anim.duplicate|Duplicar|Duplicate|Duplicar
anim.delete|Excluir|Delete|Eliminar
anim.idLabel|Animação #{0}|Animation #{0}|Animación #{0}
anim.frames|Quadros|Frames|Cuadros
anim.framesHint|Clique na folha de sprites (embaixo, à direita) para acrescentar um quadro. Arraste para mudar a ordem; o × (ou o botão direito) tira.|Click the sprite sheet (bottom right) to add a frame. Drag to change the order; the × (or the right button) removes it.|Haz clic en la hoja de sprites (abajo a la derecha) para añadir un cuadro. Arrastra para cambiar el orden; la × (o el botón derecho) lo quita.
anim.addHint|Clique num quadro da folha de sprites (embaixo, à direita) para acrescentá-lo.|Click a frame of the sprite sheet (bottom right) to add it.|Haz clic en un cuadro de la hoja de sprites (abajo a la derecha) para añadirlo.
anim.repeat|Repetir|Repeat|Repetir
anim.repeatHint|Quantas vezes repete (0 = toca uma vez). Aceita fórmulas: random/4+8.|How many times it repeats (0 = plays once). Formulas work: random/4+8.|Cuántas veces se repite (0 = una vez). Acepta fórmulas: random/4+8.
anim.repeatFrom|Repetir a partir do quadro|Repeat from frame|Repetir desde el cuadro
anim.action|Ao terminar|At the end|Al terminar
anim.actionHint|“Virar” espelha o pet e inverte o sentido.|“Turn” mirrors the pet and its direction.|“Girar” refleja la mascota e invierte el sentido.
anim.move|Movimento|Movement|Movimiento
anim.moveHint|x e y: pixels por passo (x negativo anda para a frente; y positivo desce). Do início ao fim, os valores vão mudando aos poucos. Intervalo: milissegundos por passo. Aceitam fórmulas com screenW, screenH, areaW, areaH, imageW, imageH, imageX, imageY, random e randS.|x and y: pixels per step (negative x walks forward; positive y goes down). From start to end the values change little by little. Interval: milliseconds per step. Formulas work with screenW, screenH, areaW, areaH, imageW, imageH, imageX, imageY, random and randS.|x e y: píxeles por paso (x negativo avanza; y positivo baja). Del inicio al fin, los valores cambian poco a poco. Intervalo: milisegundos por paso. Aceptan fórmulas con screenW, screenH, areaW, areaH, imageW, imageH, imageX, imageY, random y randS.
anim.newName|nova animação|new animation|nueva animación
anim.copy|(cópia)|(copy)|(copia)
anim.deleteAsk|Excluir a animação “{0}”?|Delete the animation “{0}”?|¿Eliminar la animación “{0}”?
anim.deleteUsed|Excluir a animação “{0}”? Ela é usada em:\n• {1}\n\nEla sai também das listas de próximas, dos sons e das janelas filhas. Confira as entradas depois.|Delete the animation “{0}”? It is used by:\n• {1}\n\nIt also leaves the next lists, sounds and child windows. Check the entrances afterwards.|¿Eliminar la animación “{0}”? Se usa en:\n• {1}\n\nTambién sale de las listas de siguientes, sonidos y ventanas hijas. Revisa las entradas después.
action.none|nada|nothing|nada
action.flip|virar (flip)|turn (flip)|girar (flip)
move.x|x por passo|x per step|x por paso
move.y|y por passo|y per step|y por paso
move.offset|deslocamento y|y offset|desplazamiento y
move.opacity|opacidade|opacity|opacidad
move.interval|intervalo (ms)|interval (ms)|intervalo (ms)
move.start|Início|Start|Inicio
move.end|Fim|End|Fin
key.drag|“drag” é a animação de quando o pet é pego com o mouse.|“drag” is played while the pet is held with the mouse.|“drag” es la animación mientras se sostiene la mascota con el ratón.
key.fall|“fall” é a animação de queda: quando é solto ou fica sem chão.|“fall” is the falling animation: when let go or without ground.|“fall” es la animación de caída: al soltarla o sin suelo.
key.kill|“kill” toca quando o pet vai embora (fechando o programa ou trocando de pet).|“kill” plays when the pet goes away (closing the app or changing pet).|“kill” suena cuando la mascota se va (al cerrar o cambiar de mascota).
key.sync|“sync” é tocada por todos os pets ao mesmo tempo.|“sync” is played by every pet at the same time.|“sync” la tocan todas las mascotas a la vez.
key.toss|“toss” é tocada enquanto o pet voa depois de ser jogado.|“toss” plays while the pet flies after being thrown.|“toss” suena mientras la mascota vuela tras ser lanzada.
key.fallSoft|“fall soft” toca quando o pet jogado cai devagar.|“fall soft” plays when a thrown pet lands slowly.|“fall soft” suena cuando la mascota lanzada cae despacio.
key.fallHard|“fall hard” toca quando o pet jogado cai com força.|“fall hard” plays when a thrown pet lands hard.|“fall hard” suena cuando la mascota lanzada cae con fuerza.

next.end|Ao terminar|At the end|Al terminar
next.border|Ao bater numa borda|On a border|Al chocar con un borde
next.gravity|Sem chão (gravidade)|Without ground (gravity)|Sin suelo (gravedad)
next.hint|Uma é sorteada pelas chances. “Onde vale”: só na barra de tarefas, só em cima de janelas, só nas laterais da tela…|One is drawn by the chances. “Where”: only on the taskbar, only on top of windows, only at the screen sides…|Se sortea una por las probabilidades. “Dónde”: solo en la barra de tareas, solo sobre ventanas, solo en los lados de la pantalla…
next.add|acrescentar próxima|add a next one|añadir siguiente
next.none|Nenhuma: ao terminar, o pet reaparece em outro lugar da tela.|None: at the end the pet appears again somewhere else.|Ninguna: al terminar, la mascota reaparece en otro lugar.
only.none|em qualquer lugar|anywhere|en cualquier lugar
only.taskbar|só na barra de tarefas|only on the taskbar|solo en la barra de tareas
only.window|só em cima de janela|only on a window|solo sobre una ventana
only.horizontal|só borda de cima/baixo|only top/bottom border|solo borde superior/inferior
only.horizontal+|qualquer borda horizontal|any horizontal border|cualquier borde horizontal
only.vertical|só laterais da tela|only screen sides|solo lados de la pantalla
only.vertical+|qualquer lateral|any side|cualquier lateral
only.windowSide|lateral de janela|side of a window|lado de una ventana
only.petSide|lado de outro pet|side of another pet|lado de otra mascota

preview.title|Prévia|Preview|Vista previa
preview.playing|tocando: {0}|playing: {0}|reproduciendo: {0}
preview.pause|Pausar|Pause|Pausar
preview.play|Continuar|Continue|Continuar
preview.next|Próxima|Next|Siguiente
preview.restart|Do começo|Restart|Desde el inicio
preview.loop|Repetir só esta|Repeat only this|Repetir solo esta
preview.options|Opções|Options|Opciones
key.space|Espaço|Space|Espacio
drop.open|Abrir o pet “{0}”|Open the pet “{0}”|Abrir la mascota “{0}”
drop.copy|Copiar animações dele para este pet…|Copy its animations into this pet…|Copiar sus animaciones a esta mascota…
drop.wizard|Criar um pet novo com {0} imagem(ns) (assistente)…|Make a new pet with {0} picture(s) (assistant)…|Crear una mascota nueva con {0} imagen(es) (asistente)…
drop.sheet|Acrescentar à folha de sprites deste pet…|Add to the sprite sheet of this pet…|Añadir a la hoja de sprites de esta mascota…
preview.paused|pausado|paused|en pausa
preview.noSheet|Importe a folha de sprites para ver a prévia.|Import the sprite sheet to see the preview.|Importa la hoja de sprites para ver la vista previa.

list.add|Nova|New|Nueva
list.remove|Remover|Remove|Quitar
spawns.count|Entradas ({0})|Entrances ({0})|Entradas ({0})
spawns.title|Entradas na tela|Entrances|Entradas en pantalla
spawns.empty|Sem entradas: o pet não sabe onde aparecer. Crie uma com “+ Nova”.|No entrance: the pet does not know where to appear. Create one with “+ New”.|Sin entradas: la mascota no sabe dónde aparecer. Crea una con “+ Nueva”.
spawns.intro|De onde o pet entra na tela e com qual animação começa. Uma entrada é sorteada pelas chances sempre que o pet aparece.|Where the pet comes onto the screen and with which animation it starts. One is drawn by its chance every time the pet appears.|Desde dónde entra la mascota en la pantalla y con qué animación empieza. Se sortea una por su probabilidad cada vez que aparece.
spawns.item|Entrada #{0}: {1}|Entrance #{0}: {1}|Entrada #{0}: {1}
spawns.sub|chance {0}  ·  x {1}  ·  y {2}|chance {0}  ·  x {1}  ·  y {2}|probabilidad {0}  ·  x {1}  ·  y {2}
spawns.id|Número|Number|Número
spawns.prob|Chance|Chance|Probabilidad
spawns.probHint|Comparada com as outras entradas.|Compared with the other entrances.|Comparada con las otras entradas.
spawns.x|Posição x|Position x|Posición x
spawns.xHint|Da esquerda da tela. Quando o pet está virado para a direita, é contado da direita. screenW = largura da tela.|From the left of the screen. When the pet faces right it counts from the right. screenW = screen width.|Desde la izquierda. Cuando la mascota mira a la derecha se cuenta desde la derecha. screenW = ancho de la pantalla.
spawns.y|Posição y|Position y|Posición y
spawns.yHint|De cima da tela. areaH-imageH = em cima da barra de tarefas; -imageH = acima da tela (cai do céu).|From the top. areaH-imageH = on the taskbar; -imageH = above the screen (falls from the sky).|Desde arriba. areaH-imageH = sobre la barra de tareas; -imageH = encima de la pantalla (cae del cielo).
spawns.next|Começa com a animação|Starts with the animation|Empieza con la animación
spawns.quick|Posições prontas|Ready positions|Posiciones listas
spawns.qGround|Na barra de tarefas|On the taskbar|En la barra de tareas
spawns.qRight|Entrando pela lateral|Coming from the side|Entrando por el lado
spawns.qSky|Caindo do céu|Falling from the sky|Cayendo del cielo
spawns.try|Testar esta entrada|Try this entrance|Probar esta entrada
childs.count|Janelas filhas ({0})|Child windows ({0})|Ventanas hijas ({0})
childs.title|Janelas filhas|Child windows|Ventanas hijas
childs.empty|Nenhuma janela filha.|No child window.|Ninguna ventana hija.
childs.intro|Uma segunda imagem que aparece quando uma animação começa: uma bola, uma banheira, uma nave. Ela toca as suas próprias animações e some quando elas acabam.|A second picture that appears when an animation starts: a ball, a bathtub, a ship. It plays its own animations and goes away when they end.|Una segunda imagen que aparece cuando empieza una animación: una pelota, una bañera, una nave. Toca sus propias animaciones y desaparece cuando terminan.
childs.item|{0} → {1}|{0} → {1}|{0} → {1}
childs.when|Quando começar a animação|When this animation starts|Cuando empiece la animación
childs.whenHint|A janela filha é criada no início dela.|The child window is made when it starts.|La ventana hija se crea al empezar.
childs.anim|A janela filha toca|The child window plays|La ventana hija toca
childs.animHint|A primeira animação da janela filha (ela segue as próximas dessa animação).|The first animation of the child window (it follows that animation's next ones).|La primera animación de la ventana hija (sigue las siguientes de esa animación).
childs.x|Posição x|Position x|Posición x
childs.xHint|imageX = onde está o pet. imageX-imageW = um pet à frente (do lado certo, mesmo virado).|imageX = where the pet is. imageX-imageW = one pet ahead (on the right side even when turned).|imageX = donde está la mascota. imageX-imageW = una mascota adelante (del lado correcto aunque gire).
childs.y|Posição y|Position y|Posición y
childs.yHint|imageY = a altura do pet.|imageY = the pet's height.|imageY = la altura de la mascota.
childs.open|Abrir a animação da janela filha|Open the child window's animation|Abrir la animación de la ventana hija
sounds.count|Sons ({0})|Sounds ({0})|Sonidos ({0})
sounds.title|Sons|Sounds|Sonidos
sounds.empty|Nenhum som.|No sound.|Ningún sonido.
sounds.intro|Um MP3 que pode tocar quando uma animação começa. Sons curtos e leves (até uns 100 KB) deixam o pet pequeno.|An MP3 that may play when an animation starts. Short, light sounds (up to about 100 KB) keep the pet small.|Un MP3 que puede sonar cuando empieza una animación. Sonidos cortos y ligeros (hasta unos 100 KB) mantienen la mascota pequeña.
sounds.sub|chance {0}%  ·  {1}|chance {0}%  ·  {1}|probabilidad {0}%  ·  {1}
sounds.noFile|sem arquivo|no file|sin archivo
sounds.anim|Toca com a animação|Plays with the animation|Suena con la animación
sounds.prob|Chance (%)|Chance (%)|Probabilidad (%)
sounds.probHint|De tocar cada vez que a animação começa.|Of playing each time the animation starts.|De sonar cada vez que empieza la animación.
sounds.loop|Repetições|Repeats|Repeticiones
sounds.loopHint|0 = toca uma vez.|0 = plays once.|0 = suena una vez.
sounds.import|Importar MP3|Import MP3|Importar MP3
sounds.filter|Som MP3¦*.mp3|MP3 sound¦*.mp3|Sonido MP3¦*.mp3
sounds.big|O som tem {0} KB e vai deixar o pet pesado. Usar mesmo assim?|The sound is {0} KB and will make the pet heavy. Use it anyway?|El sonido tiene {0} KB y hará pesada la mascota. ¿Usarlo de todos modos?
sounds.play|Ouvir|Listen|Escuchar
sounds.export|Exportar MP3|Export MP3|Exportar MP3

chk.title|Verificação do pet|Pet check|Verificación de la mascota
chk.allGood|Tudo certo: nada que atrapalhe o pet.|All good: nothing in the pet's way.|Todo bien: nada que estorbe a la mascota.
chk.summary|{0} erro(s) e {1} aviso(s). Clique num item para ir até o lugar.|{0} error(s) and {1} warning(s). Click an item to go there.|{0} error(es) y {1} aviso(s). Haga clic en un elemento para ir al lugar.
chk.formula|{0}: a fórmula “{1}” não funciona ({2}).|{0}: the formula “{1}” does not work ({2}).|{0}: la fórmula “{1}” no funciona ({2}).
chk.noImage|Falta a folha de sprites.|The sprite sheet is missing.|Falta la hoja de sprites.
chk.grid|A imagem de {0}×{1} px não se divide em {2} colunas × {3} linhas.|The {0}×{1} px picture does not split into {2} columns × {3} rows.|La imagen de {0}×{1} px no se divide en {2} columnas × {3} filas.
chk.noName|O pet não tem nome.|The pet has no name.|La mascota no tiene nombre.
chk.noSpawn|Não há nenhuma entrada na tela: o pet não aparece.|There is no entrance: the pet does not appear.|No hay ninguna entrada: la mascota no aparece.
chk.noAnims|Não há animações.|There are no animations.|No hay animaciones.
chk.dupId|Duas animações com o número #{0}.|Two animations with the number #{0}.|Dos animaciones con el número #{0}.
chk.dupSpawn|Duas entradas com o número #{0}.|Two entrances with the number #{0}.|Dos entradas con el número #{0}.
chk.spawnNext|A entrada #{0} começa com a animação #{1}, que não existe.|Entrance #{0} starts with animation #{1}, which does not exist.|La entrada #{0} empieza con la animación #{1}, que no existe.
chk.spawnX|Entrada #{0}, x|Entrance #{0}, x|Entrada #{0}, x
chk.spawnY|Entrada #{0}, y|Entrance #{0}, y|Entrada #{0}, y
chk.childParent|Uma janela filha é criada pela animação #{0}, que não existe.|A child window is made by animation #{0}, which does not exist.|Una ventana hija la crea la animación #{0}, que no existe.
chk.childNext|A janela filha da animação #{0} toca a #{1}, que não existe.|The child window of animation #{0} plays #{1}, which does not exist.|La ventana hija de la animación #{0} toca la #{1}, que no existe.
chk.childX|Janela filha da animação #{0}, x|Child window of animation #{0}, x|Ventana hija de la animación #{0}, x
chk.childY|Janela filha da animação #{0}, y|Child window of animation #{0}, y|Ventana hija de la animación #{0}, y
chk.soundAnim|Um som toca com a animação #{0}, que não existe.|A sound plays with animation #{0}, which does not exist.|Un sonido suena con la animación #{0}, que no existe.
chk.noFrames|{0} não tem quadros.|{0} has no frames.|{0} no tiene cuadros.
chk.badFrame|{0} usa o quadro {1}, mas a folha só vai até o {2}.|{0} uses frame {1}, but the sheet only goes up to {2}.|{0} usa el cuadro {1}, pero la hoja solo llega al {2}.
chk.repeatFrom|{0}: “repetir a partir do quadro” passa do último quadro.|{0}: “repeat from frame” is past the last frame.|{0}: “repetir desde el cuadro” pasa del último cuadro.
chk.nextMissing|{0} leva à animação #{1}, que não existe (o DesktopPet trava).|{0} leads to animation #{1}, which does not exist (DesktopPet stops).|{0} lleva a la animación #{1}, que no existe (DesktopPet se detiene).
chk.what|{0}, {1}|{0}, {1}|{0}, {1}
chk.interval|{0}: o intervalo é zero ou negativo.|{0}: the interval is zero or negative.|{0}: el intervalo es cero o negativo.
chk.noNext|{0} não tem próxima: ao terminar, o pet reaparece em outro lugar.|{0} has no next one: at the end the pet appears somewhere else.|{0} no tiene siguiente: al terminar, la mascota reaparece en otro lugar.
chk.noBorder|{0} anda, mas não diz o que fazer numa borda: o pet sai da tela.|{0} moves but says nothing for a border: the pet leaves the screen.|{0} se mueve pero no dice qué hacer en un borde: la mascota sale de la pantalla.
chk.unreached|{0} nunca é tocada (nenhuma animação ou entrada leva a ela).|{0} is never played (no animation or entrance leads to it).|{0} nunca se toca (ninguna animación o entrada lleva a ella).
chk.noKey|Não há animação “{0}”: o DesktopPet usa uma genérica.|There is no “{0}” animation: DesktopPet uses a plain one.|No hay animación “{0}”: DesktopPet usa una genérica.

test.errors|O pet tem {0} erro(s) que podem travar o DesktopPet. Testar mesmo assim?|The pet has {0} error(s) that can stop DesktopPet. Test anyway?|La mascota tiene {0} error(es) que pueden detener DesktopPet. ¿Probar de todos modos?
test.noApp|Não encontrei o DesktopPet.exe. Indique onde ele está nas Configurações.|DesktopPet.exe was not found. Tell where it is in the Settings.|No encontré DesktopPet.exe. Indica dónde está en la Configuración.
test.fail|Não foi possível abrir o teste: {0}|Could not start the test: {0}|No se pudo iniciar la prueba: {0}

set.title|Configurações|Settings|Configuración
set.look|Aparência|Appearance|Apariencia
set.theme|Tema|Theme|Tema
set.themeAuto|Igual ao Windows|As Windows|Como Windows
set.themeLight|Claro|Light|Claro
set.themeDark|Escuro|Dark|Oscuro
set.accent|Cor de destaque|Accent colour|Color de acento
set.lang|Idioma|Language|Idioma
set.uiSize|Tamanho do editor|Editor size|Tamaño del editor
set.uiSizeAuto|Igual ao Windows ({0}%)|As Windows ({0}%)|Igual que Windows ({0}%)
set.uiSizeHint|Deixa letras, botões e caixas maiores ou menores. Vale ao reabrir o editor.|Makes letters, buttons and boxes bigger or smaller. It applies when the editor opens again.|Hace letras, botones y cajas más grandes o más pequeños. Se aplica al abrir el editor de nuevo.
set.uiSizeRestart|O novo tamanho vale ao reabrir o editor. Reabrir agora? (o pet aberto continua aberto)|The new size applies when the editor opens again. Open it again now? (the open pet stays open)|El nuevo tamaño se aplica al abrir el editor de nuevo. ¿Abrirlo ahora? (la mascota abierta sigue abierta)
set.langAuto|Igual ao Windows|As Windows|Como Windows
set.thumbs|Tamanho das miniaturas|Thumbnail size|Tamaño de las miniaturas
set.small|Pequenas|Small|Pequeñas
set.medium|Médias|Medium|Medianas
set.large|Grandes|Large|Grandes
set.files|Arquivos|Files|Archivos
set.openLast|Abrir o último pet ao iniciar|Open the last pet at start|Abrir la última mascota al iniciar
set.backup|Guardar uma cópia (.bak) ao salvar por cima|Keep a copy (.bak) when saving over a file|Guardar una copia (.bak) al sobrescribir
set.extras|Criar README.md e icon.png na pasta do pet quando faltarem|Create README.md and icon.png in the pet's folder when missing|Crear README.md e icon.png en la carpeta de la mascota si faltan
set.clearRecent|Limpar a lista de recentes|Clear the recent list|Borrar la lista de recientes
set.cleared|Lista de recentes limpa.|Recent list cleared.|Lista de recientes borrada.
set.preview|Prévia|Preview|Vista previa
set.back|Fundo|Background|Fondo
set.backSky|Céu|Sky|Cielo
set.backChecker|Xadrez|Checker|Ajedrez
set.backDark|Escuro|Dark|Oscuro
set.zoom|Tamanho do pet|Pet size|Tamaño de la mascota
set.zoomHint|Como a opção “escala” do DesktopPet.|Like the “scale” option of DesktopPet.|Como la opción “escala” de DesktopPet.
set.sound|Tocar os sons na prévia|Play the sounds in the preview|Reproducir los sonidos en la vista previa
set.test|Testar no desktop|Test on the desktop|Probar en el escritorio
set.testHint|O teste abre um segundo DesktopPet só com este pet: ele não mexe no seu pet nem nas suas configurações, e recarrega sozinho quando você muda algo no editor. Para encerrar, use o ícone de teste na bandeja ou feche o editor.|The test opens a second DesktopPet with only this pet: it does not touch your pet or settings, and reloads by itself when you change something. To stop it, use the test icon in the tray or close the editor.|La prueba abre un segundo DesktopPet solo con esta mascota: no toca tu mascota ni tu configuración y se recarga sola cuando cambias algo. Para terminar, usa el icono de prueba en la bandeja o cierra el editor.
set.appPath|DesktopPet.exe|DesktopPet.exe|DesktopPet.exe
set.browse|Procurar|Browse|Buscar
set.found|Usando {0}|Using {0}|Usando {0}
set.notFound|DesktopPet.exe não encontrado|DesktopPet.exe not found|DesktopPet.exe no encontrado
set.about|Sobre|About|Acerca de
set.aboutText|Editor de Pets {0}, do DesktopPet.|Pet Editor {0}, from DesktopPet.|Editor de Mascotas {0}, de DesktopPet.
set.keys|Atalhos: Ctrl+N novo · Ctrl+O abrir · Ctrl+S salvar · Ctrl+Shift+S salvar como · Ctrl+Z desfazer · Ctrl+Y refazer · F5 testar no desktop · Delete exclui o item escolhido da lista · Na prévia: Espaço pausa, → avança um passo, R recomeça. Arraste imagens, um GIF ou um animations.xml para a janela.|Shortcuts: Ctrl+N new · Ctrl+O open · Ctrl+S save · Ctrl+Shift+S save as · Ctrl+Z undo · Ctrl+Y redo · F5 test on the desktop · Delete removes the chosen item of a list · In the preview: Space pauses, → one step, R restarts. Drag pictures, a GIF or an animations.xml onto the window.|Atajos: Ctrl+N nuevo · Ctrl+O abrir · Ctrl+S guardar · Ctrl+Shift+S guardar como · Ctrl+Z deshacer · Ctrl+Y rehacer · F5 probar en el escritorio · Supr elimina el elemento elegido de una lista · En la vista previa: Espacio pausa, → avanza un paso, R reinicia. Arrastre imágenes, un GIF o un animations.xml a la ventana.

chk.intro|Tudo o que pode atrapalhar o pet no DesktopPet. Erros fazem o pet travar ou sumir; avisos são coisas que funcionam, mas provavelmente não como você quer. Clique num item para ir até ele.|Everything that can get in the pet's way in DesktopPet. Errors stop or hide the pet; warnings work, but probably not as you mean. Click an item to go there.|Todo lo que puede estorbar a la mascota en DesktopPet. Los errores la detienen o la ocultan; los avisos funcionan, pero probablemente no como quieres. Haz clic en un elemento para ir allí.
chk.all|Todos|All|Todos
chk.errors|Erros|Errors|Errores
chk.warnings|Avisos|Warnings|Avisos
chk.again|Verificar de novo|Check again|Verificar de nuevo
chk.zeroProb|{0}: todas as chances de “{1}” são zero, nenhuma é escolhida.|{0}: every chance of “{1}” is zero, none is chosen.|{0}: todas las probabilidades de “{1}” son cero, ninguna se elige.
chk.spawnZero|Todas as entradas têm chance zero: o pet não aparece.|Every entrance has chance zero: the pet does not appear.|Todas las entradas tienen probabilidad cero: la mascota no aparece.
chk.dupName|Duas animações se chamam “{0}” ({1}): quando o DesktopPet procura pelo nome, só a primeira é usada.|Two animations are called “{0}” ({1}): when DesktopPet looks for the name, only the first is used.|Dos animaciones se llaman “{0}” ({1}): cuando DesktopPet busca por nombre, solo se usa la primera.
chk.childForever|A janela filha que começa em “{0}” (#{1}) nunca termina: fica na tela até o pet ir embora.|The child window that starts with “{0}” (#{1}) never ends: it stays on the screen until the pet goes.|La ventana hija que empieza con “{0}” (#{1}) nunca termina: se queda en pantalla hasta que la mascota se va.
chk.bigSound|O som de “{0}” tem {1} KB: deixa o pet pesado para baixar.|The sound of “{0}” is {1} KB: it makes the pet heavy to download.|El sonido de “{0}” tiene {1} KB: hace pesada la mascota.
chk.bigFrame|Quadros de {0}×{1} px são grandes demais para um pet de área de trabalho.|Frames of {0}×{1} px are too big for a desktop pet.|Cuadros de {0}×{1} px son demasiado grandes para una mascota de escritorio.

map.title|Mapa das animações|Animation map|Mapa de animaciones
map.intro|Cada cartão é uma animação, em colunas pela distância de uma entrada (as que nada alcança ficam na última). As setas mostram o que vem depois. Arraste para mover, use a rodinha para aproximar, clique num cartão para destacar as setas dele e dê dois cliques para abrir.|Each card is an animation, in columns by how far it is from an entrance (those nothing reaches go last). Arrows show what comes next. Drag to move, use the wheel to zoom, click a card to highlight its arrows and double-click to open it.|Cada tarjeta es una animación, en columnas por su distancia a una entrada (las que nada alcanza van al final). Las flechas muestran lo que sigue. Arrastra para mover, usa la rueda para acercar, haz clic en una tarjeta para resaltar sus flechas y doble clic para abrirla.
map.fit|Ver tudo|Fit all|Ver todo
map.legend|cinza: ao terminar  ·  laranja tracejado: borda  ·  azul pontilhado: sem chão  ·  roxo: janela filha  ·  contorno verde: entrada|grey: at the end  ·  orange dashed: border  ·  blue dotted: no ground  ·  purple: child window  ·  green outline: entrance|gris: al terminar  ·  naranja discontinuo: borde  ·  azul punteado: sin suelo  ·  morado: ventana hija  ·  contorno verde: entrada
map.entrance|entrada|entrance|entrada

xml.title|XML|XML|XML
xml.intro|O arquivo do pet como vai ser salvo. Os blocos grandes aparecem como «imagem», «ícone» e «som N». Você pode editar o texto e clicar em Aplicar (pode desfazer depois).|The pet's file as it will be saved. Big blocks show as «image», «icon» and «sound N». You can edit the text and click Apply (it can be undone).|El archivo de la mascota tal como se guardará. Los bloques grandes aparecen como «imagen», «icono» y «sonido N». Puedes editar el texto y pulsar Aplicar (se puede deshacer).
xml.apply|Aplicar|Apply|Aplicar
xml.discard|Descartar mudanças|Discard changes|Descartar cambios
xml.copy|Copiar tudo|Copy all|Copiar todo
xml.export|Salvar uma cópia|Save a copy|Guardar una copia
xml.find|Buscar (Enter)|Find (Enter)|Buscar (Enter)
xml.copied|XML completo copiado.|Full XML copied.|XML completo copiado.
xml.edited|Texto mudado: clique em Aplicar.|Text changed: click Apply.|Texto cambiado: pulsa Aplicar.
xml.applied|Aplicado.|Applied.|Aplicado.
xml.markImage|imagem|image|imagen
xml.markIcon|ícone|icon|icono
xml.markSound|som|sound|sonido
xml.bad|O texto não é um XML de pet válido. Confira as marcas < e > e os nomes das tags.|The text is not a valid pet XML. Check the < and > marks and the tag names.|El texto no es un XML de mascota válido. Revisa las marcas < y > y los nombres de las etiquetas.
xml.notFound|Não encontrado.|Not found.|No encontrado.
xml.leave|Aplicar as mudanças feitas no XML?|Apply the changes made to the XML?|¿Aplicar los cambios hechos en el XML?

build.button|Montar a partir de imagens…|Build from pictures…|Montar desde imágenes…
build.hint|Escolha vários PNGs (um por quadro) ou um GIF animado: cada imagem vira um quadro, centrada e com os pés embaixo.|Choose several PNGs (one per frame) or an animated GIF: each picture becomes a frame, centred with the feet at the bottom.|Elige varios PNG (uno por cuadro) o un GIF animado: cada imagen se vuelve un cuadro, centrado y con los pies abajo.
build.title|Montar a folha de sprites|Build the sprite sheet|Montar la hoja de sprites
build.filter|Imagens¦*.png;*.gif;*.bmp|Pictures¦*.png;*.gif;*.bmp|Imágenes¦*.png;*.gif;*.bmp
build.found|{0} quadros em {1} arquivo(s)|{0} frames in {1} file(s)|{0} cuadros en {1} archivo(s)
build.size|O maior tem {0}×{1} px: esse vai ser o tamanho de cada quadro.|The biggest is {0}×{1} px: that will be the size of each frame.|El mayor tiene {0}×{1} px: ese será el tamaño de cada cuadro.
build.append|Acrescentar depois dos {0} quadros que já existem (os números deles não mudam)|Add after the {0} frames already there (their numbers stay)|Añadir después de los {0} cuadros que ya existen (sus números no cambian)
build.colsHint|Para uma folha nova. Ao acrescentar, as colunas atuais continuam.|For a new sheet. When adding, the current columns stay.|Para una hoja nueva. Al añadir, se mantienen las columnas actuales.
build.ok|Montar|Build|Montar
build.cancel|Cancelar|Cancel|Cancelar
build.done|{0} quadros novos, números {1} a {2}. Cada quadro tem {3}×{4} px.|{0} new frames, numbers {1} to {2}. Each frame is {3}×{4} px.|{0} cuadros nuevos, números {1} a {2}. Cada cuadro tiene {3}×{4} px.

anim.newBlank|Em branco|Blank|En blanco
anim.newModel|A partir de um modelo pronto…|From a ready-made model…|A partir de un modelo listo…
anim.newImport|Copiar de outro pet…|Copy from another pet…|Copiar de otra mascota…
tip.anim.new|Criar uma animação: em branco, a partir de um comportamento pronto ou copiada de outro pet.|Create an animation: blank, from a ready behaviour or copied from another pet.|Crear una animación: en blanco, desde un comportamiento listo o copiada de otra mascota.

beh.title|Nova animação a partir de um modelo|New animation from a model|Nueva animación desde un modelo
beh.name|Nome|Name|Nombre
beh.lottery|O andar às vezes vai para ela|Walking sometimes goes into it|Al caminar a veces pasa a ella
beh.frames|Quadros (clique na folha, na ordem)|Frames (click the sheet, in order)|Cuadros (haz clic en la hoja, en orden)
beh.need|Precisa de pelo menos {0} quadro(s).|Needs at least {0} frame(s).|Necesita al menos {0} cuadro(s).
beh.create|Criar a animação|Create the animation|Crear la animación
beh.walk|Andar|Walk|Caminar
beh.walk.d|Anda para a frente; vira nas laterais e cai quando fica sem chão.|Walks forward; turns at the sides and falls without ground.|Camina hacia adelante; gira en los lados y cae sin suelo.
beh.turn|Virar|Turn|Girar
beh.turn.d|Vira para o outro lado (o DesktopPet espelha o desenho).|Turns to the other side (DesktopPet mirrors the drawing).|Gira hacia el otro lado (DesktopPet refleja el dibujo).
beh.idle|Parado|Standing|Quieto
beh.idle.d|Fica parado um pouco e volta a andar.|Stands a while and walks again.|Se queda quieto un rato y vuelve a caminar.
beh.blink|Piscar|Blink|Parpadear
beh.blink.d|Pisca os olhos.|Blinks.|Parpadea.
beh.run|Correr|Run|Correr
beh.run.d|Corre rápido para a frente.|Runs forward fast.|Corre rápido hacia adelante.
beh.jump|Pular|Jump|Saltar
beh.jump.d|Dá um pulo para a frente e cai de volta no chão.|Jumps forward and lands again.|Da un salto hacia adelante y vuelve al suelo.
beh.climb|Subir pela lateral|Climb the side|Trepar por el lado
beh.climb.d|Ao chegar na lateral da tela, sobe por ela; lá em cima, solta e cai.|At the screen side it climbs up; at the top it lets go and falls.|Al llegar al lado de la pantalla, sube; arriba se suelta y cae.
beh.sit|Sentar|Sit|Sentarse
beh.sit.d|Senta e fica um tempo sentado.|Sits down for a while.|Se sienta un rato.
beh.sleep|Dormir|Sleep|Dormir
beh.sleep.d|Dorme um bom tempo (o DesktopPet também usa quando você pede para ele dormir).|Sleeps a good while (DesktopPet also uses it when you tell it to sleep).|Duerme un buen rato (DesktopPet también la usa cuando le pides que duerma).
beh.happy|Feliz|Happy|Feliz
beh.happy.d|Mostra que está feliz (o DesktopPet usa nas reações).|Shows it is happy (DesktopPet uses it in reactions).|Muestra que está feliz (DesktopPet la usa en reacciones).
beh.dance|Dançar|Dance|Bailar
beh.dance.d|Dança no lugar.|Dances in place.|Baila en su sitio.
beh.talk|Falar|Talk|Hablar
beh.talk.d|Mexe a boca: o DesktopPet usa enquanto mostra um balão.|Moves its mouth: DesktopPet uses it while a bubble is shown.|Mueve la boca: DesktopPet la usa mientras muestra un globo.
beh.fall|Cair|Fall|Caer
beh.fall.d|Cai cada vez mais rápido até o chão ou uma janela (o DesktopPet usa quando o pet é solto).|Falls faster and faster to the ground or a window (DesktopPet uses it when the pet is let go).|Cae cada vez más rápido al suelo o a una ventana (DesktopPet la usa al soltar la mascota).
beh.land|Pousar|Land|Aterrizar
beh.land.d|Chega ao chão depois de cair ou de ser jogado.|Reaches the ground after falling or being thrown.|Llega al suelo después de caer o de ser lanzada.
beh.drag|Ser pego|Picked up|Ser agarrada
beh.drag.d|Enquanto o mouse segura o pet.|While the mouse holds the pet.|Mientras el ratón sujeta la mascota.
beh.kill|Ir embora|Leave|Irse
beh.kill.d|Some aos poucos quando o pet vai embora.|Fades out when the pet leaves.|Se desvanece cuando la mascota se va.

imp.choose|Escolha o pet de onde copiar|Choose the pet to copy from|Elige la mascota de donde copiar
imp.title|Copiar animações de “{0}”|Copy animations from “{0}”|Copiar animaciones de “{0}”
imp.intro|Marque as animações. Os quadros delas vão para o fim da folha deste pet (se forem maiores, todos os quadros ficam desse tamanho). Os números mudam sozinhos.|Tick the animations. Their frames go to the end of this pet's sheet (if they are bigger, every frame takes that size). The numbers change by themselves.|Marca las animaciones. Sus cuadros van al final de la hoja de esta mascota (si son mayores, todos los cuadros toman ese tamaño). Los números cambian solos.
imp.follow|Trazer também o que vem depois (cena inteira, janelas filhas e sons)|Bring what follows too (whole scene, child windows and sounds)|Traer también lo que sigue (escena entera, ventanas hijas y sonidos)
imp.ok|Copiar|Copy|Copiar

wiz.title|Assistente de pet novo|New pet assistant|Asistente de mascota nueva
wiz.step0|1. As imagens do pet|1. The pet's pictures|1. Las imágenes de la mascota
wiz.step1|2. O que cada quadro faz|2. What each frame does|2. Qué hace cada cuadro
wiz.step2|3. Nome e jeito|3. Name and manner|3. Nombre y forma de ser
wiz.intro0|Escolha as imagens do pet: vários PNGs (um por quadro), um GIF animado ou a folha de sprites deste pet. Diga também para que lado os desenhos olham.|Choose the pet's pictures: several PNGs (one per frame), an animated GIF or the sprite sheet of this pet. Also tell which way the drawings face.|Elige las imágenes de la mascota: varios PNG (uno por cuadro), un GIF animado o la hoja de sprites de esta mascota. Indica también hacia dónde miran los dibujos.
wiz.pick|Escolher imagens ou GIF…|Choose pictures or GIF…|Elegir imágenes o GIF…
wiz.useSheet|Usar a folha deste pet|Use this pet's sheet|Usar la hoja de esta mascota
wiz.face|Os desenhos olham para|The drawings face|Los dibujos miran hacia
wiz.faceLeft|a esquerda|the left|la izquierda
wiz.faceRight|a direita (serão espelhados)|the right (they will be mirrored)|la derecha (se reflejarán)
wiz.faceHint|O DesktopPet usa desenhos olhando para a esquerda e espelha quando o pet vira.|DesktopPet uses drawings facing left and mirrors them when the pet turns.|DesktopPet usa dibujos mirando a la izquierda y los refleja cuando la mascota gira.
wiz.none|Nenhuma imagem ainda.|No picture yet.|Ninguna imagen todavía.
wiz.loaded|{0} quadros de {1}×{2} px.|{0} frames of {1}×{2} px.|{0} cuadros de {1}×{2} px.
wiz.roleHint|Clique nos quadros de “{0}”, na ordem.|Click the frames of “{0}”, in order.|Haz clic en los cuadros de “{0}”, en orden.
wiz.nFrames|{0} quadro(s)|{0} frame(s)|{0} cuadro(s)
wiz.required|obrigatório (pelo menos {0})|required (at least {0})|obligatorio (al menos {0})
wiz.optional|opcional|optional|opcional
wiz.intro2|Por último, o nome e o jeito do pet. Depois de criar, tudo pode ser mudado nas páginas do editor.|Last, the pet's name and manner. After it is made, everything can be changed in the editor's pages.|Por último, el nombre y la forma de ser. Después de crearla, todo se puede cambiar en las páginas del editor.
wiz.speed|Velocidade|Speed|Velocidad
wiz.slow|Devagar|Slow|Despacio
wiz.normal|Normal|Normal|Normal
wiz.fast|Rápido|Fast|Rápido
wiz.climb|Sobe pelas laterais da tela|Climbs the screen sides|Trepa por los lados de la pantalla
wiz.after|O pet vai andar, virar nas laterais, cair do céu, ser pego com o mouse e, de vez em quando, fazer as outras animações que você escolheu. Ele entra na tela pela lateral, caindo do céu ou já na barra de tarefas.|The pet will walk, turn at the sides, fall from the sky, be picked up with the mouse and now and then do the other animations you chose. It comes in from the side, falling from the sky or already on the taskbar.|La mascota caminará, girará en los lados, caerá del cielo, se podrá agarrar con el ratón y de vez en cuando hará las otras animaciones que elegiste. Entra por el lado, cayendo del cielo o ya en la barra de tareas.
wiz.next|Avançar|Next|Siguiente
wiz.back|Voltar|Back|Atrás
wiz.create|Criar o pet|Create the pet|Crear la mascota
wiz.needPictures|Escolha as imagens primeiro.|Choose the pictures first.|Elige las imágenes primero.
wiz.needRole|“{0}” precisa de pelo menos {1} quadro(s).|“{0}” needs at least {1} frame(s).|“{0}” necesita al menos {1} cuadro(s).
proj.wizard|Assistente de pet novo|New pet assistant|Asistente de mascota nueva
proj.wizardHint|A partir das suas imagens ou de um GIF: em três passos o pet já anda.|From your pictures or a GIF: in three steps the pet already walks.|A partir de tus imágenes o de un GIF: en tres pasos la mascota ya camina.
proj.gallery|Preparar para a galeria|Prepare for the gallery|Preparar para la galería
proj.galleryHint|A pasta completa do pet: README, ícone, prévia animada e a linha no pets.json.|The pet's complete folder: README, icon, animated preview and its pets.json line.|La carpeta completa: README, icono, vista previa animada y su línea en pets.json.

preview.step|Um passo|One step|Un paso
preview.stepOf|passo {0} de {1}|step {0} of {1}|paso {0} de {1}
preview.windowToggle|Janela de teste|Test window|Ventana de prueba
preview.window|Janela de teste|Test window|Ventana de prueba
preview.onion|Quadro anterior|Frame before|Cuadro anterior
tip.preview.step|Pausa e avança um passo de cada vez.|Pauses and goes one step at a time.|Pausa y avanza un paso cada vez.
tip.preview.window|Uma janela no palco: veja o pet pousar nela, andar em cima e bater nas laterais. Arraste pela barra de título.|A window on the stage: see the pet land on it, walk on it and bump into its sides. Drag it by its title bar.|Una ventana en el escenario: mira a la mascota aterrizar, caminar encima y chocar con sus lados. Arrástrala por la barra de título.
tip.preview.onion|Mostra o quadro anterior transparente por baixo, para ver quanto cada quadro anda.|Shows the frame before see-through underneath, to see how far each frame moves.|Muestra el cuadro anterior transparente debajo, para ver cuánto avanza cada cuadro.
tip.preview.mouse|Pegue o pet com o mouse: solte devagar para ele cair, ou jogue para arremessar.|Grab the pet with the mouse: let go slowly to drop it, or fling it to throw it.|Agarra la mascota con el ratón: suelta despacio para que caiga, o lánzala.
strip.hold|Segurar mais ({0}×)|Hold longer ({0}×)|Mantener más ({0}×)
strip.duplicateEnd|Repetir no fim|Repeat at the end|Repetir al final
strip.pixels|Editar os pixels deste quadro…|Edit the pixels of this frame…|Editar los píxeles de este cuadro…
strip.reverse|Inverter a ordem|Reverse the order|Invertir el orden
strip.remove|Tirar da animação|Remove from the animation|Quitar de la animación

px.button|Editar pixels…|Edit pixels…|Editar píxeles…
tip.px.button|Retoque um quadro: lápis, linha, retângulo, balde, conta-gotas, trocar cor, contorno, espelhar e mover.|Retouch a frame: pencil, line, rectangle, bucket, eyedropper, colour swap, outline, mirror and move.|Retoca un cuadro: lápiz, línea, rectángulo, cubo, cuentagotas, cambiar color, contorno, reflejar y mover.
px.title|Editar pixels do quadro {0}|Edit the pixels of frame {0}|Editar los píxeles del cuadro {0}
px.tools|Ferramenta|Tool|Herramienta
px.pen|Lápis|Pencil|Lápiz
px.fill|Balde|Bucket|Cubo
px.pick|Conta-gotas|Eyedropper|Cuentagotas
px.swap|Trocar cor|Swap colour|Cambiar color
px.line|Linha|Line|Línea
px.rect|Retângulo|Rectangle|Rectángulo
tip.px.line|Arraste do primeiro ao último pixel da linha (botão direito apaga).|Drag from the first to the last pixel of the line (right button erases).|Arrastre del primer al último píxel de la línea (botón derecho borra).
tip.px.rect|Arraste de um canto ao outro: o contorno de um retângulo (botão direito apaga).|Drag from one corner to the other: the outline of a rectangle (right button erases).|Arrastre de una esquina a la otra: el contorno de un rectángulo (botón derecho borra).
px.outline|Contorno|Outline|Contorno
tip.px.outline|Põe uma borda de 1 pixel, na cor escolhida, em volta do desenho.|Puts a 1 pixel border, in the chosen colour, around the drawing.|Pone un borde de 1 píxel, del color elegido, alrededor del dibujo.
tip.px.pen|Pinta um pixel (botão direito apaga).|Paints a pixel (right button erases).|Pinta un píxel (botón derecho borra).
tip.px.fill|Pinta a área da mesma cor.|Paints the area of the same colour.|Pinta el área del mismo color.
tip.px.pick|Pega a cor de um pixel.|Takes the colour of a pixel.|Toma el color de un píxel.
tip.px.swap|Clique numa cor: ela vira a cor escolhida em todo o quadro.|Click a colour: it becomes the chosen colour in the whole frame.|Haz clic en un color: se vuelve el color elegido en todo el cuadro.
px.everywhere|Trocar a cor também nos outros quadros|Swap the colour in the other frames too|Cambiar el color también en los otros cuadros
px.hint|Botão esquerdo pinta, o direito apaga. Ctrl+Z desfaz.|Left button paints, right erases. Ctrl+Z undoes.|Botón izquierdo pinta, el derecho borra. Ctrl+Z deshace.
px.color|Cor|Colour|Color
px.more|Outra cor…|Other colour…|Otro color…
px.frame|O quadro inteiro|The whole frame|El cuadro entero
px.mirrorH|Espelhar|Mirror|Reflejar
px.mirrorV|De cabeça para baixo|Upside down|Boca abajo
px.move|Mover um pixel|Move one pixel|Mover un píxel
px.save|Salvar no quadro|Save to the frame|Guardar en el cuadro

skin.button|Criar uma skin…|Make a skin…|Crear una skin…
tip.skin.button|Uma cópia do pet em outras cores; o DesktopPet escolhe uma skin para cada pet na tela.|A copy of the pet in other colours; DesktopPet picks a skin for each pet on screen.|Una copia de la mascota en otros colores; DesktopPet elige una skin para cada mascota en pantalla.
skin.title|Nova skin (outras cores)|New skin (other colours)|Nueva skin (otros colores)
skin.intro|Clique na cor da direita para trocar. A prévia mostra o primeiro quadro. A skin nova vai para o fim da folha, com todos os quadros.|Click the colour on the right to change it. The preview shows the first frame. The new skin goes to the end of the sheet, with every frame.|Haz clic en el color de la derecha para cambiarlo. La vista previa muestra el primer cuadro. La skin nueva va al final de la hoja, con todos los cuadros.
skin.ok|Criar a skin|Make the skin|Crear la skin
skin.none|Troque pelo menos uma cor.|Change at least one colour.|Cambia al menos un color.
skin.done|Pronto: o pet agora tem {0} skins.|Done: the pet now has {0} skins.|Listo: la mascota ahora tiene {0} skins.

opt.button|Otimizar o tamanho…|Optimize the size…|Optimizar el tamaño…
tip.opt.button|Deixe o pet mais leve para baixar: tire quadros sem uso e comprima a imagem.|Make the pet lighter to download: remove unused frames and compress the picture.|Haz la mascota más ligera: quita cuadros sin uso y comprime la imagen.
opt.title|Otimizar o tamanho|Optimize the size|Optimizar el tamaño
opt.size|O pet tem {0} KB|The pet is {0} KB|La mascota tiene {0} KB
opt.parts|Imagem: {0} KB · sons: {1} KB|Picture: {0} KB · sounds: {1} KB|Imagen: {0} KB · sonidos: {1} KB
opt.unused|Quadros que nenhuma animação usa: {0}|Frames no animation uses: {0}|Cuadros que ninguna animación usa: {0}
opt.removeUnused|Tirar os quadros sem uso|Remove the unused frames|Quitar los cuadros sin uso
opt.removed|{0} quadros tirados; os outros foram renumerados.|{0} frames removed; the others were renumbered.|{0} cuadros quitados; los demás se renumeraron.
opt.skins|Com várias skins, os quadros não são tirados (cada skin precisa dos mesmos números).|With several skins frames are not removed (every skin needs the same numbers).|Con varias skins no se quitan cuadros (cada skin necesita los mismos números).
opt.compress|Comprimir a imagem|Compress the picture|Comprimir la imagen
opt.compressHint|Usa uma paleta de até 256 cores, só se a imagem continuar igual pixel por pixel.|Uses a palette of up to 256 colours, only if the picture stays the same pixel by pixel.|Usa una paleta de hasta 256 colores, solo si la imagen queda igual píxel por píxel.
opt.compressed|A imagem ficou {0} KB menor.|The picture is {0} KB smaller.|La imagen quedó {0} KB más pequeña.
opt.notSmaller|Não deu para diminuir sem mudar a imagem.|It could not be made smaller without changing the picture.|No se pudo reducir sin cambiar la imagen.
opt.heavySound|O som de “{0}” tem {1} KB: diminua a qualidade (64 kbps, mono) num editor de áudio.|The sound of “{0}” is {1} KB: lower its quality (64 kbps, mono) in an audio editor.|El sonido de “{0}” tiene {1} KB: baja su calidad (64 kbps, mono) en un editor de audio.

gal.title|Preparar para a galeria|Prepare for the gallery|Preparar para la galería
gal.intro|Cria a pasta do pet como a galeria do DesktopPet usa: animations.xml, icon.png, README em inglês e português e preview.gif (o pet andando e um truque). Se a pasta escolhida tiver o pets.json da galeria, a linha do pet também é atualizada.|Makes the pet's folder as the DesktopPet gallery uses it: animations.xml, icon.png, README in English and Portuguese and preview.gif (the pet walking and a trick). If the chosen folder has the gallery's pets.json, the pet's line is updated too.|Crea la carpeta de la mascota como la usa la galería de DesktopPet: animations.xml, icon.png, README en inglés y portugués y preview.gif (la mascota caminando y un truco). Si la carpeta elegida tiene el pets.json de la galería, también se actualiza su línea.
gal.parent|Pasta dos pets|Pets folder|Carpeta de mascotas
gal.parentHint|Na galeria do DesktopPet, é a pasta Pets do repositório (a que tem o pets.json).|In the DesktopPet gallery it is the Pets folder of the repository (the one with pets.json).|En la galería de DesktopPet es la carpeta Pets del repositorio (la que tiene pets.json).
gal.folder|Nome da pasta do pet|Pet folder name|Nombre de la carpeta
gal.folderHint|Sem espaços nem acentos (por exemplo: meu_pet).|No spaces or accents (for example: my_pet).|Sin espacios ni acentos (por ejemplo: mi_mascota).
gal.json|Atualizar o pets.json (tamanho, contagens e data)|Update pets.json (size, counts and date)|Actualizar pets.json (tamaño, cuentas y fecha)
gal.ok|Preparar a pasta|Prepare the folder|Preparar la carpeta
gal.done|Pasta pronta: {0}|Folder ready: {0}|Carpeta lista: {0}
gal.jsonDone|O pets.json foi atualizado.|pets.json was updated.|Se actualizó pets.json.

rec.ask|O editor fechou sem salvar “{0}” ({1}).\nArquivo: {2}\n\nRecuperar as mudanças?|The editor closed without saving “{0}” ({1}).\nFile: {2}\n\nRecover the changes?|El editor se cerró sin guardar “{0}” ({1}).\nArchivo: {2}\n\n¿Recuperar los cambios?
set.tour|Ver o tour de novo|Show the tour again|Ver el tour de nuevo
tour.next|Próximo|Next|Siguiente
tour.skip|Pular|Skip|Saltar
tour.done|Começar|Start|Empezar
tour.welcome|Bem-vindo ao Editor de Pets! Aqui você cria um pet seu para o DesktopPet ou muda um que já existe. Vou mostrar as partes principais em 8 passos.|Welcome to the Pet Editor! Here you make a pet of your own for DesktopPet or change one. Here are the main parts in 8 steps.|¡Bienvenido al Editor de Mascotas! Aquí creas una mascota propia para DesktopPet o cambias una. Te muestro las partes principales en 8 pasos.
tour.project|Projeto: comece aqui. O Assistente monta um pet a partir das suas imagens em três passos; também dá para abrir um pet que já existe.|Project: start here. The Assistant builds a pet from your pictures in three steps; you can also open an existing pet.|Proyecto: empieza aquí. El Asistente crea una mascota con tus imágenes en tres pasos; también puedes abrir una existente.
tour.sheet|Folha de sprites: a imagem com todos os quadros. Importe um PNG, monte a partir de vários PNGs ou de um GIF, retoque pixels e crie skins de outras cores.|Sprite sheet: the picture with every frame. Import a PNG, build it from PNGs or a GIF, retouch pixels and make skins in other colours.|Hoja de sprites: la imagen con todos los cuadros. Importa un PNG, móntala desde PNG o un GIF, retoca píxeles y crea skins de otros colores.
tour.animations|Animações: o coração do pet. Escolha os quadros, o movimento e o que vem depois. A prévia embaixo mostra como fica, e você pode pegar o pet com o mouse.|Animations: the heart of the pet. Choose frames, movement and what comes next. The preview below shows the result, and you can grab the pet with the mouse.|Animaciones: el corazón de la mascota. Elige cuadros, movimiento y lo que sigue. La vista previa abajo muestra el resultado, y puedes agarrar la mascota con el ratón.
tour.spawns|Entradas na tela: de onde o pet aparece e com qual animação começa.|Entrances: where the pet appears and with which animation it starts.|Entradas: dónde aparece la mascota y con qué animación empieza.
tour.check|Verificador: avisa o que pode travar ou atrapalhar o pet no DesktopPet. Ao lado ficam o Mapa das animações e o XML.|Checker: tells what can stop or disturb the pet in DesktopPet. Next to it are the Animation map and the XML.|Verificador: avisa lo que puede detener o estorbar a la mascota en DesktopPet. Al lado están el Mapa de animaciones y el XML.
tour.test|Testar no desktop (F5): o pet aparece de verdade na sua tela, num DesktopPet de teste que não mexe no seu pet, e se atualiza a cada mudança.|Test on the desktop (F5): the pet really appears on your screen, in a test DesktopPet that does not touch your pet, and updates with every change.|Probar en el escritorio (F5): la mascota aparece de verdad en tu pantalla, en un DesktopPet de prueba que no toca tu mascota, y se actualiza con cada cambio.
tour.settings|Configurações: tema claro ou escuro, cores, idioma e a prévia. Este tour pode ser visto de novo aqui.|Settings: light or dark theme, colours, language and the preview. This tour can be seen again here.|Configuración: tema claro u oscuro, colores, idioma y la vista previa. Este tour se puede ver de nuevo aquí.

tip.act.new|Um pet novo: pelo assistente (a partir das suas imagens) ou pelo modelo pronto. Ctrl+N|A new pet: with the assistant (from your pictures) or the ready model. Ctrl+N|Una mascota nueva: con el asistente (con tus imágenes) o con el modelo listo. Ctrl+N
tip.act.open|Abrir um animations.xml. Ctrl+O|Open an animations.xml. Ctrl+O|Abrir un animations.xml. Ctrl+O
tip.act.save|Salvar o pet (com uma cópia .bak do anterior). Ctrl+S|Save the pet (with a .bak copy of the old one). Ctrl+S|Guardar la mascota (con una copia .bak de la anterior). Ctrl+S
tip.act.undo|Desfazer a última mudança. Ctrl+Z|Undo the last change. Ctrl+Z|Deshacer el último cambio. Ctrl+Z
tip.act.redo|Refazer. Ctrl+Y|Redo. Ctrl+Y|Rehacer. Ctrl+Y
tip.act.check|Ver o que pode atrapalhar o pet no DesktopPet.|See what can get in the pet's way in DesktopPet.|Ver lo que puede estorbar a la mascota en DesktopPet.
tip.act.test|Ver o pet de verdade na tela, sem mexer no seu DesktopPet. F5|See the pet for real on the screen, without touching your DesktopPet. F5|Ver la mascota de verdad en pantalla, sin tocar tu DesktopPet. F5
tip.nav.project|Pet novo, abrir, recentes, assistente e preparar para a galeria.|New pet, open, recent, assistant and gallery.|Mascota nueva, abrir, recientes, asistente y galería.
tip.nav.header|Nome, autor, descrição e ícone.|Name, author, description and icon.|Nombre, autor, descripción e icono.
tip.nav.sheet|A imagem com os quadros, retoque de pixels, skins e tamanho.|The picture with the frames, pixel retouch, skins and size.|La imagen con los cuadros, retoque de píxeles, skins y tamaño.
tip.nav.animations|Quadros, movimento, próximas animações e prévia.|Frames, movement, next animations and preview.|Cuadros, movimiento, siguientes animaciones y vista previa.
tip.nav.spawns|De onde o pet entra na tela.|Where the pet comes onto the screen.|Desde dónde entra la mascota en la pantalla.
tip.nav.childs|Imagens extras que aparecem com uma animação (bola, banheira, nave).|Extra pictures that appear with an animation (ball, bathtub, ship).|Imágenes extra que aparecen con una animación (pelota, bañera, nave).
tip.nav.sounds|Sons MP3 que tocam com as animações.|MP3 sounds played with the animations.|Sonidos MP3 que suenan con las animaciones.
tip.nav.check|Erros e avisos do pet.|Errors and warnings of the pet.|Errores y avisos de la mascota.
tip.nav.map|Todas as animações e como uma leva à outra.|Every animation and how one leads to another.|Todas las animaciones y cómo una lleva a otra.
tip.nav.xml|O arquivo do pet, para ver ou editar à mão.|The pet's file, to see or edit by hand.|El archivo de la mascota, para ver o editar a mano.
tip.nav.settings|Tema, cores, idioma, prévia e o DesktopPet do teste.|Theme, colours, language, preview and the test DesktopPet.|Tema, colores, idioma, vista previa y el DesktopPet de prueba.
test.oldApp|O DesktopPet instalado (versão {0}) ainda não tem o modo de teste. Atualize para a versão {1} ou mais nova (Configurações → Atualizações no DesktopPet) e tente de novo.|The installed DesktopPet (version {0}) does not have the test mode yet. Update it to version {1} or newer (Settings → Updates in DesktopPet) and try again.|El DesktopPet instalado (versión {0}) aún no tiene el modo de prueba. Actualízalo a la versión {1} o más nueva (Configuración → Actualizaciones en DesktopPet) y prueba de nuevo.
spawns.subText|chance {0}  ·  {1}|chance {0}  ·  {1}|probabilidad {0}  ·  {1}
where.both|{0}, {1}|{0}, {1}|{0}, {1}
where.taskbar|na barra de tarefas|on the taskbar|en la barra de tareas
where.sky|caindo do céu|falling from the sky|cayendo del cielo
where.below|subindo de baixo|rising from below|subiendo desde abajo
where.randomHeight|numa altura aleatória|at a random height|a una altura aleatoria
where.height|na altura {0}|at height {0}|a la altura {0}
where.side|entrando pela lateral|coming in from the side|entrando por el lado
where.random|num lugar aleatório|at a random place|en un lugar aleatorio
where.edge|no canto da tela|at the screen edge|en el borde de la pantalla
where.x|em x = {0}|at x = {0}|en x = {0}
snap.cut|passa da borda|goes past the edge|pasa del borde
";
	}
}
