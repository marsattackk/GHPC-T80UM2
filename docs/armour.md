# The armour

How the T-80UM2's armour is put together, where the numbers come from and where it goes wrong. The short version is in
the [README](../README.md#armour-protection). The real T-80UM2 is a T-80U with the «Дрозд» (Drozd) active protection and
nothing new in its armour, so the armour here is the T-80U's: a cast turret with a cellular filling, the T-80BV's glacis,
«Контакт-5» (Kontakt-5) on top. Few plate thicknesses of the T-80U are published, and every gap is listed below and in
[details.md](details.md#where-it-isnt-the-real-tank).

## War Thunder's shapes, my numbers

The armour pieces, hull and turret, are cut from War Thunder's damage model of the T-80UM2 (184 pieces, the Kontakt-5
containers among them) and replace the T-80B's outer armour. None of War Thunder's armour values is used. Every
thickness and strength comes from the Russian sources further down, from GHPC's own convention for the part (rubber
skirts, stowage boxes, the dozer blade and the engine bulkhead stay what the vanilla T-80B has), or from my own
estimate where I found nothing. Each of those is listed in [details.md](details.md#where-it-isnt-the-real-tank). Two
of the model's choices I changed to fit the game: War Thunder draws the cast turret as one solid lump with the cheek
filling and the 80 mm plate inside it, so I cut them out of the casting and give each its own value, and the lower
front plate is part of the glacis mesh there, so I split it at the nose edge to give it its own thickness.

In game it's ordinary GHPC armour, the same kind the vanilla tanks use and hit by the same rules. The T-80B's running
gear, crew, interior, hit zones and damage model stay under it. The cast turret is built from the model's pieces as
cast steel at 0.95 of rolled steel per mm, the way the game's own T-80B turret casting is.

Inside it works like a vanilla tank. The crew, the ammo, the carousel, the engine and the rack sit at the T-80UM2's
places, and the «Дрозд» blocks and radar heads are damageable modules wired into the game's own systems (see
[screens.md](screens.md) for the system). The T-80B's three compartments get meshes rebuilt from the model's walls and
its five fender fuel tanks the shapes of the T-80UM2's tank boxes.

## Where the design comes from

The numbers, and what's inside the cheeks and the glacis, come from **Russian documents I found online**. For the
T-80U's own plates there are few, so several figures are the T-80U's (or the T-80 family's) and not specific to the
T-80UM2:

- **Turret cheeks:** the cast T-80U turret has two rows of cast blocks with cells filled with polymer («ячеистых литых
  блоков, заливаемых полимером (ПУМ) ... в два ряда и разделенных стальной плитой 20 мм»), an outer wall of 85 to 60 mm
  and a rear wall up to 190 mm, and an 80 mm BTK-1 plate behind the package. From btvt.info's armour article on the T-80U
  / T-80UD, and andrei_bt's posts on the T-80U's cellular castings. The filling's per-mm value is solved so that the
  cheek comes out at the figure I chose, below.
- **How strong the cheek is overall:** btvt.info's table of protection equivalents («Защита экв. ±35°») gives the
  T-80UD, same cast turret, 620 mm against APFSDS and 900 mm against HEAT, and its "T-80UD (1989)" line 700 / 1000,
  which looks like the same turret with its built-in reactive armour counted. I took 620 / 900 as without ERA. For
  scale, Efremov's «Уроки танкостроения» (read on otvaga) gives the T-80B 500 mm and the T-80U with Kontakt-5 1100 mm
  against APFSDS and 900 against HEAT, which reads like marketing with the two mixed up, so I didn't use it.
- **Upper glacis:** «Бронирование корпуса аналогично Т-80БВ» (Russian Wikipedia, «Т-80У»). That's steel, glass
  textolite («стеклотекстолит», СТБ) and high-hardness steel, 205 mm at 68° in Wikipedia's infobox, plus the 30 mm
  «Отражение» plate welded to the T-80B's glacis from 1984. War Thunder's five layers (50 / 30 textolite / 50 / 30
  textolite / 50 at 68°) stay as the geometry. The resistance I calibrated to is **430 mm against APFSDS**, which two
  sources give for the T-80BV / T-80B glacis: Tarasenko and Chepkov in «Техника и вооружение» 2009 («корпус 430») and
  Efremov on «Отражение-2». The HEAT figure comes from GHPC's own textolite, not from a source. Russian Wikipedia's
  550 / 800 for the T-80U is uncited and 120 mm above the measured figure, so I didn't use it.
- **Lower front plate:** 80 mm at 60° (Russian Wikipedia's «Т-80» infobox, uncited).
- **Hull sides, rear, roof, belly:** sides 80 mm, rear plates 80 mm and the roof 30 mm (all from
  Моделист-Конструктор's T-80 description, which gives the roof as 30 mm or less). The belly is 20 mm, my value. The
  lower side is 20 mm (the T-64's figure on Russian Wikipedia, uncited, since the T-80's hull comes from the T-64's) and
  the engine deck 20 mm (the source says 30 mm or less and removable; War
  Thunder draws it as titanium, which I found no source for, so it's steel).
- **The «Дрозд» housings:** four paired armoured blocks under two radar modules, «с учетом защиты от 12,7мм» (btvt.info on
  the complex). The thickness is not given. I use **25 mm**, inferred as the least that stops GHPC's 12.7 mm rounds. The
  computer box on the turret rear-left is the same.
- **Anti-radiation lining** (подбой / надбой) inside the driver's compartment and the turret: it exists on the real tank
  (Russian Wikipedia, «Т-80»; btvt.info on the T-80UD), but no T-80U thickness is published. I added it with the T-64A's
  figures from btvt.info's article on that tank (turret front 20 to 10 mm rising to 40, rear zone 60, roof 50, cabin
  30 mm of polyethylene) and marked it as inferred. War Thunder's model has no lining.
- **Where nothing could be found** (the mantlet, the turret's sides, rear and roof, the hatches, the sight heads, the
  driver's station blocks, the container steel of the reactive armour), the thickness is the model's own shape at cast
  steel x0.95 or rolled steel x1.0, and [details.md](details.md#where-it-isnt-the-real-tank) says so.

The default preset is "game-scale". `Armour preset` = "source-only" gives the plain open-source figures instead (cheeks
620 / 900, glacis 430 / 566). Only the strength of the two fillings changes, not the layout, and the reactive armour is
the same in both. Why there are two: the game's vanilla T-80B and T-72M1 come out about 10 to 20 % above their own
published figures (my measurement from the game's files), so a tank at the plain published figures would sit under tanks
it should be above. Game-scale gives the cheeks the same step up the T-90A got (744 / 984). The glacis is set to the
vanilla T-80B's 485 against APFSDS so it doesn't sit under the tank it is built on, although both come from the same real
430. Its HEAT figure is 653. All the game-scale figures are my own table, not a source's.

## How it compares

Line-of-sight protection straight from the front, in mm of steel, **without ERA**:

| | Turret cheeks (vs APFSDS / vs HEAT) | Upper glacis (vs APFSDS / vs HEAT) |
|---|---|---|
| **T-80UM2 (this mod), game-scale** | **744 / 984** | **485 / 653** |
| T-80UM2, source-only | 620 / 900 | 430 / 566 |
| T-80B (vanilla) | 595 / 669 | 485 / 602 |
| [T-90A](https://github.com/marsattackk/GHPC-T90A) (my mod) | 690 / 819 | 549 / 748 |
| [T-90M](https://github.com/marsattackk/GHPC-T90M) (my mod) | 690 / 820 | 552 / 752 |
| [Leclerc SXXI](https://github.com/marsattackk/GHPC-LeclercSXXI) (my mod), left cheek | 700 / 1100 | 500 / 800 |
| M1IP (vanilla) | 516 / 999 | 348 / 361 |

Median over a grid of straight-on shots. The T-80UM2 and the T-90s were measured in game on their cheek block and
glacis. The vanilla tanks, the M1IP's 516 / 999 included, are my measurement from their armour in the game's files over
the whole cheek face, so their thin edges pull their median down a bit. The Leclerc's are
computed from its armour table, not fired, and are my estimate of a tank whose armour is confidential.

Against the vanilla T-80B the cheeks are much stronger and the glacis is the same against APFSDS, 485, with a bit more
against HEAT. Against the T-90A the cheeks are stronger (744 against 690) and the glacis is thinner (485 against 549).
Without the reactive armour the T-80UM2's glacis is no better than a T-80B's.

## Kontakt-5

Kontakt-5 adds about **140 / 550** on top, the same figures I use on the T-90A. They come from НИИ Стали (the maker),
who give -20 % against APFSDS and -60 % against HEAT missiles, and from btvt.info, which puts it at x1.2 against APFSDS
and x1.9 to 2.0 against HEAT. (btvt.info's 2014 article on reactive armour quotes the maker at 120 mm against APFSDS,
which is close.) The cheeks then come to 884 / 1534 in the game-scale preset, and to 760 / 1450 in source-only.
Each brick is spent after one hit and gives nothing after.

The element is the 4С22, 1.37 kg, 251.9 x 131.9 x 13 mm (btvt.info, as the rest of this section). It doesn't go off for machine-gun rounds, autocannon rounds
up to 30 mm or fragments, which is what btvt.info says about it. The one exception is the fragments of a bigger HE shell
bursting within 10 m of it. GHPC on its own sets ERA off on any hit, a PKT round included. These rules are the T-90A's.

What's covered, from btvt.info's drawing of the T-80U with Kontakt-5, which I read at a small size:

- **The glacis:** 10 sections, 3 and 3 in the centre and 2 and 2 at the sides, like War Thunder's.
- **The turret front:** 9 V-shaped blocks within ±35° of the nose, 5 on the left and 4 on the right, the innermost one
  small. A block holds up to 4 elements, so how many bricks sit in each one is my guess.
- **The turret roof:** flat elements in 4 groups (behind the mantlet, in the centre, rear right, rear left). The
  drawing is small and my count is about 22, so the roof is the least certain part.
- **The front side screens:** 3 panels per side, each of 2 x 3 element cells. btvt.info says the T-80U's screens cover more
  area than the T-90's plates but protect a bit less against APFSDS.

A set-off box is gone in game, as on the T-90s. With `ERA destruction effects` on, it goes with a blast and the lid is
thrown off. The War Thunder model has no set-off brick, so that look is a best guess.

## Test firing

The cheek figure in the table above is the median over a grid of straight-on shots in game, and the glacis is read the
same way. I fire the grid again whenever the armour changes.

I haven't written a table of what stopped what for this tank, so there isn't one here. The weak spots below come from
the armour table and its sources, and where I say a round goes through it is the table's figure against the round's,
not a shot I'm quoting.

## Weak spots

If you're fighting one, or trying to keep one alive, this is where it goes wrong.

- **The lower front plate** under the glacis is 80 mm at 60°, behind the 20 mm dozer blade. APFSDS and HEAT go through.
- **The mantlet** round the gun is cast steel without the cheeks' filling, War Thunder's shape at cast x0.95 and nothing
  sourced behind it. btvt.info says the T-80U turret has smaller weak zones than the T-72B's, so it may be thicker
  than mine, but I found no figure.
- **The glacis without its Kontakt-5** is no better than a T-80B's. Once a box has gone off, that spot is open for the
  next round, and the turret's V-blocks leave joints with no brick between them (the left cheek's centre line falls in one).
- **The «Дрозд» blocks** on the turret front corners are 25 mm boxes. A hit takes that block's module out, and losing a
  radar head blinds that side.
- **From the side** it's 80 mm of hull, which stops nothing a tank fires, with the fender fuel tanks in the way. The
  lower side is a 20 mm edge. The turret's sides are thinner than the cheeks and their thickness is the model's shape,
  not a figure.
- **From above** the roof is 30 mm and the engine deck 20.
- **From behind** the hull rear is 80 mm over the engine, and the turret's rear is cast with no figure behind it.

The thin skirts and flaps are rubber, 0.8 mm of steel as GHPC counts rubber skirts, and only the reactive panels in front
of them count for anything.
