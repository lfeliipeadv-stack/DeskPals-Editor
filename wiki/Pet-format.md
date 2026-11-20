# The pet format

[← Wiki home](Home.md) · [Português](pt-BR/Formato-do-pet.md)

A pet is one XML file, `animations.xml`, in the format of [desktopPet](https://github.com/Adrianotiger/desktopPet)
(eSheep). The editor reads and writes it exactly as DesktopPet does; you never have to edit it by hand, but this is
what is inside (see it on the [XML](XML.md) page).

```xml
<?xml version="1.0" encoding="utf-8"?>
<animations xmlns="https://esheep.petrucci.ch/">
  <header>
    <author>…</author>
    <title>Rex the Dino</title>
    <petname>Rex</petname>
    <version>1.0</version>
    <info>First line.[br]Second line.</info>
    <application>1</application>
    <icon><![CDATA[ …the icon, base64… ]]></icon>
  </header>
  <image>
    <tilesx>8</tilesx>
    <tilesy>45</tilesy>
    <png><![CDATA[ …the sprite sheet, base64… ]]></png>
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

| Part | Editor page |
| --- | --- |
| `header` | [Header](Header.md) |
| `image` (the sheet, grid, transparency, skins) | [Sprite sheet](Sprite-sheet.md) |
| `spawns` | [Entrances](Entrances.md) |
| `animations` (frames, movement, repeat, next lists) | [Animations](Animations.md) |
| `childs` | [Child windows](Child-windows.md) |
| `sounds` | [Sounds](Sounds.md) |

- Frame numbers count from 0, left to right and top to bottom of the sheet.
- `next` points to an animation by its `id`; `probability` is its chance; `only` is *where* it counts (`none`,
  `taskbar`, `window`, `horizontal`, `vertical`, `windowSide`, `petSide`…).
- `x`, `y`, `interval` and `repeat` accept [formulas](Formulas.md).
- When they are used, the editor also writes an animation's `picker="no"` (hidden from DesktopPet's panel), its
  `<action>flip</action>` (turn at the end), the number of `<skins>` of the sheet and a sound's `<loop>`.

The full schema is [animations.xsd](https://adrianotiger.github.io/desktopPet/Resources/animations.xsd) of desktopPet.
