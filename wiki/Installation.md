# Installation

[← Wiki home](Home.md) · [Português](pt-BR/Instalacao.md)

## What you need

- Windows 10 or 11, 64-bit.
- The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (free, from Microsoft). If it is
  missing, Windows shows a message with a link to it when the editor opens.
- [DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet) **2.8.3 or newer**, only for
  [Test on the desktop](Test-on-the-desktop.md). Everything else works without it.

## Download

1. Open the [latest release](https://github.com/lfeliipeadv-stack/desktop-pet-editor/releases/latest).
2. Download `PetEditor.exe`.
3. Run it. There is nothing to install: the program is that one file. Keep it anywhere (the Desktop, a folder of your
   programs) and make a shortcut to it if you like.

Each release also has `SHA256SUMS`, the checksum of the exe. To check the download in PowerShell:

```powershell
Get-FileHash .\PetEditor.exe -Algorithm SHA256
```

The result must be the same as the line in `SHA256SUMS`.

## Where things are kept

| What | Where |
| --- | --- |
| Settings (theme, language, size, recent pets, window…) | `%AppData%\DesktopPet\PetEditor.json` |
| Copies of unsaved work (every minute) | `%AppData%\DesktopPet\PetEditor\recover` |
| The pet being tested on the desktop | `%TEMP%\PetEditor\test\animations.xml` |
| A copy of the file replaced when saving | `animations.xml.bak`, next to the pet (can be turned off in [Settings](Settings.md)) |

## Uninstall

Delete `PetEditor.exe`. To remove the settings too, delete `%AppData%\DesktopPet\PetEditor.json` and the folder
`%AppData%\DesktopPet\PetEditor`. Your pets are never deleted: they stay wherever you saved them.
