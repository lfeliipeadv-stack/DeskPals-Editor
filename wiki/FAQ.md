# FAQ

[← Wiki home](Home.md) · [Português](pt-BR/Perguntas-frequentes.md)

**The editor does not open, or Windows talks about .NET.**
Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) and open it again.

**Windows says the program may be unsafe (SmartScreen).**
The exe is not signed. Click *More info → Run anyway*. You can check the download against `SHA256SUMS`
([Installation](Installation.md#download)).

**Everything is too big (or too small).**
*Settings → Appearance → Editor size*: 100% is the size of the editor without the zoom of Windows
([Settings](Settings.md)).

**Test on the desktop says DesktopPet is too old or was not found.**
The test needs DesktopPet 2.8.3 or newer ([Test on the desktop](Test-on-the-desktop.md)).

**The pet walks the wrong way.**
DesktopPet expects drawings facing **left**: negative *x per step* walks forward. Mirror the frames (in the assistant,
*The drawings face → the right*, or in the [pixel editor](Pixel-editor-and-skins.md)).

**The pet goes off the screen.**
An animation that moves needs a choice *On a border* ([Animations](Animations.md#what-comes-next)); the
[Checker](Checker.md) points it out.

**The pet disappears and comes back somewhere else.**
An animation with no next one at the end makes the pet appear again from an [entrance](Entrances.md). Add what comes
next.

**The frames look cut or shifted.**
Columns and rows on the [Sprite sheet](Sprite-sheet.md) page must split the picture exactly.

**A background colour shows around the pet.**
Choose the right *Transparent colour* (usually Magenta) or use a PNG with its own transparency.

**The pet is heavy to download.**
[Optimize the size](Sharing.md#optimize-the-size) and use short sounds (64 kbps, mono).

**I closed without saving.**
The editor keeps a copy every minute and offers it the next time it opens
([The window](The-window.md#when-the-editor-closes-without-saving)).

**Can I edit a pet I downloaded in DesktopPet?**
Yes: *Project → Pets downloaded in DesktopPet*. Use *Save as* first, so the original stays as it is.

**Is the pet only for DesktopPet?**
It is the `animations.xml` format of desktopPet (eSheep). The editor is made and tested for
[DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet).
