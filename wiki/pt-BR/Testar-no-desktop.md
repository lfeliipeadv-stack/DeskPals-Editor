# Testar no desktop

[← Início do wiki](Home.md) · [English](../Test-on-the-desktop.md)

**Testar no desktop** (`F5`) mostra o pet de verdade na sua tela: nas suas janelas, na sua barra de tarefas, no tamanho
real.

## Como funciona

1. O editor grava o pet em `%TEMP%\PetEditor\test\animations.xml`.
2. Ele abre `DesktopPet.exe --test-pet=<esse arquivo>`: um segundo DesktopPet, **só com este pet**. Ele não mexe no seu
   pet, nas suas configurações nem na sua pasta de pets, e tem um ícone próprio, pequeno, na bandeja.
3. Enquanto o teste roda, cada mudança no editor é gravada de novo um instante depois, e o pet de teste **recarrega
   sozinho** — não precisa salvar.

Para parar o teste, use o ícone de teste na bandeja, ou feche o editor.

## O que é preciso

O [DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet) **2.8.3 ou mais novo** (a versão com o modo de teste). O
editor procura sozinho:

1. ao lado do `PetEditor.exe`;
2. em `Program Files\DesktopPet`;
3. em `%LocalAppData%\DesktopPet`.

Ou escolha em [Configurações → Testar no desktop](Configuracoes.md#testar-no-desktop).

## Mensagens

| Mensagem | O que fazer |
| --- | --- |
| O pet tem *n* erro(s) que podem travar o DesktopPet. Testar mesmo assim? | Veja o [Verificador](Verificador.md); *Sim* testa assim mesmo. |
| Não encontrei o DesktopPet.exe. | Instale o DesktopPet, ou diga onde ele está nas Configurações. |
| O DesktopPet instalado (versão *x*) ainda não tem o modo de teste. | Atualize o DesktopPet (no DesktopPet: Configurações → Atualizações). |
| Não foi possível abrir o teste. | A mensagem diz o porquê (por exemplo, o Windows bloqueou o programa). |
