# O formato do pet

[← Início do wiki](Home.md) · [English](../Pet-format.md)

Um pet é um arquivo XML, `animations.xml`, no formato do [desktopPet](https://github.com/Adrianotiger/desktopPet)
(eSheep). O editor lê e grava esse arquivo exatamente como o DesktopPet; você nunca precisa editá-lo à mão, mas é isto
que há dentro (veja na página [XML](XML.md)).

```xml
<?xml version="1.0" encoding="utf-8"?>
<animations xmlns="https://esheep.petrucci.ch/">
  <header>
    <author>…</author>
    <title>Rex the Dino</title>
    <petname>Rex</petname>
    <version>1.0</version>
    <info>Primeira linha.[br]Segunda linha.</info>
    <application>1</application>
    <icon><![CDATA[ …o ícone, em base64… ]]></icon>
  </header>
  <image>
    <tilesx>8</tilesx>
    <tilesy>45</tilesy>
    <png><![CDATA[ …a folha de sprites, em base64… ]]></png>
    <transparency>Magenta</transparency>
  </image>
  <spawns>
    <spawn id="1" probability="40">
      <x>screenW</x>
      <y>areaH-imageH</y>
      <next>1</next>
    </spawn>
  </spawns>
  <animations>
    <animation id="1">
      <name>walk</name>
      <start>
        <x>-3</x><y>0</y><offsety>0</offsety><opacity>1</opacity><interval>150</interval>
      </start>
      <end>
        <x>-3</x><y>0</y><offsety>0</offsety><opacity>1</opacity><interval>150</interval>
      </end>
      <sequence repeatfrom="0" repeat="random/4+8">
        <frame>7</frame><frame>8</frame><frame>9</frame><frame>8</frame>
        <next probability="10" only="none">2</next>
      </sequence>
      <border><next probability="100" only="vertical">2</next></border>
      <gravity><next probability="100" only="none">5</next></gravity>
    </animation>
  </animations>
  <childs>
    <child animationid="81"><x>imageX</x><y>imageY</y><next>101</next></child>
  </childs>
  <sounds>
    <sound animationid="20"><probability>70</probability><base64>…</base64></sound>
  </sounds>
</animations>
```

| Parte | Página do editor |
| --- | --- |
| `header` | [Cabeçalho](Cabecalho.md) |
| `image` (a folha, a grade, a transparência, as skins) | [Folha de sprites](Folha-de-sprites.md) |
| `spawns` | [Entradas na tela](Entradas.md) |
| `animations` (quadros, movimento, repetição, listas de próximas) | [Animações](Animacoes.md) |
| `childs` | [Janelas filhas](Janelas-filhas.md) |
| `sounds` | [Sons](Sons.md) |

- Os números dos quadros contam a partir de 0, da esquerda para a direita e de cima para baixo na folha.
- `next` aponta para uma animação pelo `id` dela; `probability` é a chance; `only` é *onde vale* (`none`, `taskbar`,
  `window`, `horizontal`, `vertical`, `windowSide`, `petSide`…).
- `x`, `y`, `interval` e `repeat` aceitam [fórmulas](Formulas.md).
- Quando são usados, o editor grava também o `picker="no"` de uma animação (fora do painel do DesktopPet), a
  `<action>flip</action>` (virar ao terminar), o número de `<skins>` da folha e o `<loop>` de um som.

O esquema completo é o [animations.xsd](https://adrianotiger.github.io/desktopPet/Resources/animations.xsd) do desktopPet.
