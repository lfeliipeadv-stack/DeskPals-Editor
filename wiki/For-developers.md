# For developers

[← Wiki home](Home.md) · [Português](pt-BR/Para-desenvolvedores.md)

## Build

C# with Windows Forms on .NET 10. Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build src/PetEditor.csproj -c Release
dotnet publish src/PetEditor.csproj -c Release -o out    # a single PetEditor.exe
```

## The code

| Folder | What it has |
| --- | --- |
| `src/Core` | The model and the work without windows: `PetDoc` (the open pet, undo/redo), `PetXmlWriter`, `Validator` (the Checker), `Expr` (formulas), `Behaviors` (ready-made animations), `SheetBuilder`, `Optimizer`, `GifWriter`, `Recovery`, `DesktopTest`, `L` (the texts in Portuguese and English), `EditorSettings`, `DevTests` |
| `src/Pages` | One class per page of the side bar |
| `src/UI` | The main window, the controls drawn by the editor (`Controls.cs`), the preview (`PreviewStage`), the dialogs, the tour, and `WikiShots` (the pictures of this wiki) |
| `src/Shared` | Files from DesktopPet: the XML model (`AnimationXML.cs`), the theme, the coloured text and the MP3 player |

Texts: every text of the window is in `src/Core/L.cs`, one line per text, `key|Portuguese|English|Spanish` (the
Spanish column is kept for later). Sizes are written for 100% and grow with the zoom (`Ui.S`, `Ui.Scaled`).

## Options of PetEditor.exe

| Option | What it does |
| --- | --- |
| `<file or folder>` | Opens that pet. |
| `--devtests=<Pets folder>` | Runs the editor's tests (assistant, copy between pets, GIF, optimizer, .zip, zoom). The exit code is the number of failures; the report is `%TEMP%\PetEditor-devtests.txt`. Uses the `pingo` pet of the [DesktopPet gallery](https://github.com/lfeliipeadv-stack/meu-pet/tree/master/Pets). |
| `--roundtrip=<Pets folder>` | Reads and writes again every pet of the folder; exits with 0 when nothing changed. |
| `--checktime=<animations.xml>` | How long the Checker takes on a pet. |
| `--buildsheet=<out.png> <pictures…>` | Builds a sprite sheet from pictures. |
| `<pet> --snap=<folder>` | A picture of every page and window, light and dark, plus `cortes.txt` with every text or button cut off (or *nenhum corte*) and `erros.txt` if something failed. |
| `<pet> --wikishots=<folder>` | The pictures of this wiki: pages, windows, the assistant step by step, the checker with problems, the tour. The name of the Windows user and its folder are replaced by neutral words. |
| `--snapsize=1000x660` | The window size for `--snap` and `--wikishots`. |
| `--scale=1.5` | Another zoom for this run. |
| `--lang=en` / `--lang=pt` | Another language for this run (the setting does not change). |
| `--settings=<file>` | Another settings file for this run (clean pictures, tests). |
| `--page=settings` | The page shown at the start. |
| `PE_TIMING=<file>` (environment variable) | Writes how long each part of the start takes. |

## Making the pictures of the wiki

```powershell
# a settings file with the language, the theme and no recent pets
'{"Language":"en","Theme":"light","UiSize":100,"TourDone":true,"OpenLast":false,"Recent":[]}' | Set-Content demo.json
.\PetEditor.exe --settings=demo.json --wikishots=wiki\images\en --snapsize=1360x860 "C:\path\to\rex\animations.xml"
```

## Before a release

1. `--devtests` and `--roundtrip` with the DesktopPet gallery: no failure, no difference.
2. `--snap` at `--scale=1` (1000x660) and `--scale=1.5` (1500x990): `cortes.txt` must say *nenhum corte* and there must
   be no `erros.txt`.
3. `dotnet publish`, then attach `PetEditor.exe` and `SHA256SUMS` to the GitHub release.
