# Instalação

[← Início do wiki](Home.md) · [English](../Installation.md)

## O que é preciso

- Windows 10 ou 11, 64 bits.
- O [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (gratuito, da Microsoft). Se faltar,
  o Windows mostra uma mensagem com o link quando o editor abre.
- O [DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet) **2.8.3 ou mais novo**, só para
  [Testar no desktop](Testar-no-desktop.md). Todo o resto funciona sem ele.

## Baixar

1. Abra a [versão mais recente](https://github.com/lfeliipeadv-stack/desktop-pet-editor/releases/latest).
2. Baixe o `PetEditor.exe`.
3. Abra. Não há nada para instalar: o programa é esse único arquivo. Deixe-o onde quiser (na Área de Trabalho, numa
   pasta de programas) e crie um atalho, se preferir.

Cada versão traz também o `SHA256SUMS`, a soma de verificação do exe. Para conferir o download no PowerShell:

```powershell
Get-FileHash .\PetEditor.exe -Algorithm SHA256
```

O resultado precisa ser igual à linha do `SHA256SUMS`.

## Onde ficam as coisas

| O quê | Onde |
| --- | --- |
| Configurações (tema, idioma, tamanho, recentes, janela…) | `%AppData%\DesktopPet\PetEditor.json` |
| Cópias do trabalho não salvo (a cada minuto) | `%AppData%\DesktopPet\PetEditor\recover` |
| O pet que está sendo testado no desktop | `%TEMP%\PetEditor\test\animations.xml` |
| Uma cópia do arquivo substituído ao salvar | `animations.xml.bak`, ao lado do pet (dá para desligar nas [Configurações](Configuracoes.md)) |

## Desinstalar

Apague o `PetEditor.exe`. Para tirar também as configurações, apague `%AppData%\DesktopPet\PetEditor.json` e a pasta
`%AppData%\DesktopPet\PetEditor`. Os seus pets nunca são apagados: ficam onde você os salvou.
