# Para desenvolvedores

[← Início do wiki](Home.md) · [English](../For-developers.md)

## Compilar

C# com Windows Forms no .NET 10. Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build src/PetEditor.csproj -c Release
dotnet publish src/PetEditor.csproj -c Release -o out    # um único PetEditor.exe
```

## O código

| Pasta | O que tem |
| --- | --- |
| `src/Core` | O modelo e o trabalho sem janelas: `PetDoc` (o pet aberto, desfazer/refazer), `PetXmlWriter`, `Validator` (o Verificador), `Expr` (fórmulas), `Behaviors` (modelos prontos), `SheetBuilder`, `Optimizer`, `GifWriter`, `Recovery`, `DesktopTest`, `L` (os textos em português e inglês), `EditorSettings`, `DevTests` |
| `src/Pages` | Uma classe por página da barra lateral |
| `src/UI` | A janela principal, os controles desenhados pelo editor (`Controls.cs`), a prévia (`PreviewStage`), os diálogos, o tour e o `WikiShots` (as fotos deste wiki) |
| `src/Shared` | Arquivos do DesktopPet: o modelo do XML (`AnimationXML.cs`), o tema, o texto colorido e o tocador de MP3 |

Textos: todo texto da janela fica em `src/Core/L.cs`, uma linha por texto, `chave|português|inglês|espanhol` (a coluna
do espanhol fica guardada para depois). Os tamanhos são escritos para 100% e crescem com o zoom (`Ui.S`, `Ui.Scaled`).

## Opções do PetEditor.exe

| Opção | O que faz |
| --- | --- |
| `<arquivo ou pasta>` | Abre esse pet. |
| `--devtests=<pasta Pets>` | Roda os testes do editor (assistente, cópia entre pets, GIF, otimizador, .zip, zoom). O código de saída é o número de falhas; o relatório fica em `%TEMP%\PetEditor-devtests.txt`. Usa o pet `pingo` da [galeria do DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet/tree/master/Pets). |
| `--roundtrip=<pasta Pets>` | Lê e grava de novo cada pet da pasta; sai com 0 quando nada mudou. |
| `--checktime=<animations.xml>` | Quanto tempo o Verificador leva num pet. |
| `--buildsheet=<saida.png> <imagens…>` | Monta uma folha de sprites a partir de imagens. |
| `<pet> --snap=<pasta>` | Uma foto de cada página e janela, no tema claro e no escuro, mais o `cortes.txt` com todo texto ou botão cortado (ou *nenhum corte*) e o `erros.txt` se algo falhou. |
| `<pet> --wikishots=<pasta>` | As fotos deste wiki: páginas, janelas, o assistente passo a passo, o verificador com problemas, o tour. O nome do usuário do Windows e a pasta dele viram palavras neutras. |
| `--snapsize=1000x660` | O tamanho da janela para `--snap` e `--wikishots`. |
| `--scale=1.5` | Outro zoom só nesta execução. |
| `--lang=en` / `--lang=pt` | Outro idioma só nesta execução (a configuração não muda). |
| `--settings=<arquivo>` | Outro arquivo de configurações só nesta execução (fotos limpas, testes). |
| `--page=settings` | A página mostrada no começo. |
| `PE_TIMING=<arquivo>` (variável de ambiente) | Anota quanto tempo cada parte da abertura leva. |

## Fazendo as fotos do wiki

```powershell
# um arquivo de configurações com o idioma, o tema e sem pets recentes
'{"Language":"pt","Theme":"light","UiSize":100,"TourDone":true,"OpenLast":false,"Recent":[]}' | Set-Content demo.json
.\PetEditor.exe --settings=demo.json --wikishots=wiki\images\pt --snapsize=1360x860 "C:\caminho\do\rex\animations.xml"
```

## Antes de uma versão

1. `--devtests` e `--roundtrip` com a galeria do DesktopPet: nenhuma falha, nenhuma diferença.
2. `--snap` em `--scale=1` (1000x660) e `--scale=1.5` (1500x990): o `cortes.txt` tem de dizer *nenhum corte* e não pode
   haver `erros.txt`.
3. `dotnet publish`, depois anexar o `PetEditor.exe` e o `SHA256SUMS` à versão no GitHub.
