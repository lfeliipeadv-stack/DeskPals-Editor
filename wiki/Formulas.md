# Formulas

[← Wiki home](Home.md) · [Português](pt-BR/Formulas.md)

Positions, movement, repeats and intervals can be numbers or **formulas**: arithmetic (`+ - * /` and parentheses)
with the words below. DesktopPet works them out while the pet runs; the editor works them out in the
[preview](Preview.md) and the [Checker](Checker.md) reports a formula that does not work.

| Word | Value |
| --- | --- |
| `screenW` | Width of the screen, in pixels |
| `screenH` | Height of the screen |
| `areaW` | Width of the work area (the screen without the taskbar) |
| `areaH` | Height of the work area: the top of the taskbar |
| `imageW` | Width of one frame of the pet |
| `imageH` | Height of one frame |
| `imageX` | Where the pet is, from the left |
| `imageY` | Where the pet is, from the top |
| `random` | A random number from 0 to 99, new each time the formula is worked out |
| `randS` | A random number from 0 to 99 that stays the same while the pet runs (the editor's preview uses 50) |
| `scale` | The size of the pet in DesktopPet (1, 2…) |

## Examples

| Formula | Where | Result |
| --- | --- | --- |
| `areaH-imageH` | entrance y | standing on the taskbar |
| `-imageH` | entrance y | just above the screen: falls from the sky |
| `screenW` | entrance x | at the right side, coming in |
| `random*(screenW-imageW-50)/100+25` | entrance x | anywhere across the screen, 25 px from the sides |
| `random/4+8` | repeat | 8 to 32 times |
| `200+random` | interval | 200 to 299 ms per step |
| `imageX` / `imageY` | child window x / y | where the pet is |
| `imageX-imageW` | child window x | one pet ahead |

For a child window of a pet facing right, `imageW` counts the other way, so `imageX-imageW` is always in front of the
pet.
