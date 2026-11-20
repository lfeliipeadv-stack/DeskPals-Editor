# Test on the desktop

[← Wiki home](Home.md) · [Português](pt-BR/Testar-no-desktop.md)

**Test on the desktop** (`F5`) shows the pet for real on your screen: on your windows, on your taskbar, at the real
size.

## How it works

1. The editor writes the pet to `%TEMP%\PetEditor\test\animations.xml`.
2. It starts `DesktopPet.exe --test-pet=<that file>`: a second DesktopPet, with **only this pet**. It does not touch
   your own pet, your settings or your pets folder, and it has its own small icon in the tray.
3. While it runs, every change in the editor is written again a moment later, and the test pet **reloads by itself** —
   no need to save.

To stop the test, use the test icon in the tray, or close the editor.

## What you need

[DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet) **2.8.3 or newer** (the version with the test mode). The
editor looks for it by itself:

1. next to `PetEditor.exe`;
2. in `Program Files\DesktopPet`;
3. in `%LocalAppData%\DesktopPet`.

Or choose it in [Settings → Test on the desktop](Settings.md#test-on-the-desktop).

## Messages

| Message | What to do |
| --- | --- |
| The pet has *n* error(s) that can stop DesktopPet. Test anyway? | See the [Checker](Checker.md); *Yes* tests it anyway. |
| DesktopPet.exe was not found. | Install DesktopPet, or tell where it is in Settings. |
| The installed DesktopPet (version *x*) does not have the test mode yet. | Update DesktopPet (in DesktopPet: Settings → Updates). |
| Could not start the test. | The message says why (for example, Windows blocked the program). |
