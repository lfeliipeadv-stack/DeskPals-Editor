# Perguntas frequentes

[← Início do wiki](Home.md) · [English](../FAQ.md)

**O editor não abre, ou o Windows fala em .NET.**
Instale o [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) e abra de novo.

**O Windows diz que o programa pode não ser seguro (SmartScreen).**
O exe não é assinado. Clique em *Mais informações → Executar assim mesmo*. Dá para conferir o download com o
`SHA256SUMS` ([Instalação](Instalacao.md#baixar)).

**Está tudo grande (ou pequeno) demais.**
*Configurações → Aparência → Tamanho do editor*: 100% é o tamanho do editor sem o zoom do Windows
([Configurações](Configuracoes.md)).

**Testar no desktop diz que o DesktopPet é antigo ou não foi encontrado.**
O teste precisa do DesktopPet 2.8.3 ou mais novo ([Testar no desktop](Testar-no-desktop.md)).

**O pet anda para o lado errado.**
O DesktopPet espera desenhos olhando para a **esquerda**: *x por passo* negativo anda para a frente. Espelhe os quadros
(no assistente, *Os desenhos olham para → a direita*, ou no [editor de pixels](Editor-de-pixels-e-skins.md)).

**O pet sai da tela.**
Uma animação que se move precisa de uma escolha em *Ao bater numa borda* ([Animações](Animacoes.md#o-que-vem-depois));
o [Verificador](Verificador.md) aponta.

**O pet some e reaparece em outro lugar.**
Uma animação sem próxima *Ao terminar* faz o pet reaparecer por uma [entrada](Entradas.md). Acrescente o que vem
depois.

**Os quadros aparecem cortados ou deslocados.**
As colunas e linhas da página [Folha de sprites](Folha-de-sprites.md) precisam dividir a imagem exatamente.

**Aparece uma cor de fundo em volta do pet.**
Escolha a *Cor transparente* certa (normalmente Magenta) ou use um PNG com transparência própria.

**O pet ficou pesado para baixar.**
[Otimize o tamanho](Compartilhar.md#otimizar-o-tamanho) e use sons curtos (64 kbps, mono).

**Fechei sem salvar.**
O editor guarda uma cópia a cada minuto e oferece na próxima vez que abrir
([A janela](A-janela.md#quando-o-editor-fecha-sem-salvar)).

**Posso editar um pet que baixei no DesktopPet?**
Pode: *Projeto → Pets baixados no DesktopPet*. Use antes *Salvar como*, para o original continuar igual.

**O pet só serve para o DesktopPet?**
É o formato `animations.xml` do desktopPet (eSheep). O editor é feito e testado para o
[DesktopPet](https://github.com/lfeliipeadv-stack/meu-pet).
