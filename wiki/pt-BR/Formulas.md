# Fórmulas

[← Início do wiki](Home.md) · [English](../Formulas.md)

Posições, movimento, repetições e intervalos podem ser números ou **fórmulas**: contas (`+ - * /` e parênteses) com as
palavras abaixo. O DesktopPet faz as contas enquanto o pet roda; o editor faz as contas na [prévia](Previa.md), e o
[Verificador](Verificador.md) avisa quando uma fórmula não funciona.

| Palavra | Valor |
| --- | --- |
| `screenW` | Largura da tela, em pixels |
| `screenH` | Altura da tela |
| `areaW` | Largura da área de trabalho (a tela sem a barra de tarefas) |
| `areaH` | Altura da área de trabalho: o topo da barra de tarefas |
| `imageW` | Largura de um quadro do pet |
| `imageH` | Altura de um quadro |
| `imageX` | Onde o pet está, a partir da esquerda |
| `imageY` | Onde o pet está, a partir de cima |
| `random` | Um número sorteado de 0 a 99, novo a cada vez que a fórmula é calculada |
| `randS` | Um número sorteado de 0 a 99 que fica igual enquanto o pet roda (a prévia do editor usa 50) |
| `scale` | O tamanho do pet no DesktopPet (1, 2…) |

## Exemplos

| Fórmula | Onde | Resultado |
| --- | --- | --- |
| `areaH-imageH` | y de uma entrada | em pé na barra de tarefas |
| `-imageH` | y de uma entrada | logo acima da tela: cai do céu |
| `screenW` | x de uma entrada | na lateral direita, entrando |
| `random*(screenW-imageW-50)/100+25` | x de uma entrada | qualquer ponto da tela, a 25 px das laterais |
| `random/4+8` | repetir | de 8 a 32 vezes |
| `200+random` | intervalo | de 200 a 299 ms por passo |
| `imageX` / `imageY` | x / y de uma janela filha | onde o pet está |
| `imageX-imageW` | x de uma janela filha | um pet à frente |

Numa janela filha de um pet virado para a direita, `imageW` conta para o outro lado, então `imageX-imageW` fica sempre
na frente do pet.
