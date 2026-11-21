# The reticle files

Every reticle the mod draws is a description file in `Resources\` (`*.jsonc`), read by `ReticleSpec` and built into
GHPC's own reticle by `ReticleBuild` (`PortReticles` loads them). This page is the language. `tests\ExpressionTests.cs`
and `tests\ReticleTests.cs` pin its behaviour, and `tests\ShippedReticleTests.cs` expands every shipped file and compares
it line by line with `tests\expanded\`.

Seven files ship in `Resources\`, one per picture the tank draws. Sources for the real sights are in their own language
in [details.md](details.md) ("The sights"). Where a layout is mine and not a drawing's, the table says so, and
["Where it isn't the real tank"](details.md#where-it-isnt-the-real-tank) lists it again.

| File | The picture | Where it comes from |
|---|---|---|
| `1g46_day.jsonc` | the gunner's day channel of the 1Г46: chevron, lead marks, the Б, О and Н scales, the coaxial П scale, the 2.5 m stadia, the emergency stroke | «Рис. 9» of the 1А45 description. The geometry is the T-90A's, measured off the same figure and written out as a file here. The coaxial scale follows the game's ballistics for the rounds the tank carries |
| `tpn4_night.jsonc` | the ТПН-4 «Буран-ПА» night sight (setting "Night sight", the second value) | **Inferred.** I have no drawing of the «Буран-ПА» reticle. The layout is the ТПН-4ER's (Б and ОК on one column, П corners from 100 to 800 m, the Д stadia for 2.5 m, lead strokes) from its description, the angles from the game's ballistics, the positions my guess. Field 5.25 deg |
| `agava2_monitor.jsonc` | the marks on the «Агава-2» monitor: the aiming cross, the mode, range and permission boxes | the «Агава-2» description's photo of the monitor: mode symbols and the four-digit range at the top left, four permission boxes at the top right. The photo is too coarse for the rest, so the aiming marks are **inferred**. All screen marks, no reticle mesh |
| `essa_monitor.jsonc` | the black bars each side of the 4:3 thermal picture | the T-90A's file, unchanged: the «Агава-2» picture is 4:3 on a 16:9 screen as the T-90A's thermal is. The name says ESSA because it is that file |
| `tkn4s_day.jsonc`, `tkn4s_night.jsonc` | the commander's ТКН-4С, day (7.8 deg) and night channel (11.5 deg) | the T-90A's files, unchanged: the same instrument on both tanks («ПНК-4С» description). The З (anti-aircraft) scale in them does nothing here, because the НСВТ is hand-laid |
| `k10t_aa.jsonc` | the К10-Т collimator of the НСВТ: a cross with 0-10 and 0-20 divisions inside two rings | the К10-Т description: the cross, the divisions and the ring sizes (0-80, 0-120). The rings' radii on the screen I read off a schematic, so they are inferred. Counted in mils (6000 to a turn) |

The three T-90A files are byte for byte the ones in the T-90A's repo, so an edit to the ТКН-4С belongs in both.

GHPC draws a reticle as one skinned mesh. Each element is skinned to the bone of its nearest group, and the group's
**alignment** says how the game moves that bone: `Boresight` stays on the gun's line, `VerticalRange` moves with the set
range, `Impact` follows the computed impact point, and so on. An element with no aligned group above it is refused.

## The file

JSON with `//` comments and trailing commas. Positions are **mrad** from the origin, x right, y up, unless `units` says
otherwise. Any string where a number is expected is an **expression** (below).

```jsonc
{
  "name": "1G46 (T-80UM2)",       // must be unique: GHPC caches meshes by name
  "fov": 20,                    // vertical field of view, deg
  "units": "mrad",                // mrad, mil (6000 a turn), mil_nato (6400), deg, moa, field (1 = field radius)
  "lights": { "from_host": true, "set": { "NightIllumination": [4, 2.2, 0.5] } },
  "style": { "w": 0.25, "visual": "painted", "light": "NightIllumination" },
  "vars": { "Step": 2.094, "Half": "x => x / 2" },
  "defs": { "Tick": { "params": ["x"], "elements": [ { "seg": ["x", 0, "x", -1] } ] } },
  "elements": [ { "group": "Fixed", "align": "Boresight", "elements": [ ... ] } ],
  "screen": { "name": "Agava-2 monitor", "items": [ ... ] }
}
```

- `lights`: `from_host` copies the lights of the sight's own vanilla reticle; `set` gives an HDR RGB colour per light
  (NightIllumination, Powered, Loaded, LaserReady, GuidanceActive).
- `style`: defaults that a group or element can override: `w` (line width), `visual` (`painted`: etched, dark by day and
  lit at night; `additive`: projected, for monitors and thermal pictures) and `light`.
- `vars`: evaluated top to bottom, visible everywhere. Numbers, lists, `'strings'` or lambdas. The code reads some of
  them by name (`spec.Num("X")`), so a value both the drawing and the code need lives here.
- `defs`: macros. `{ "use": "Tick", "args": [3] }` evaluates the arguments where it stands, then expands the macro with
  only the globals and its parameters in scope.
- Any element may carry a `"note"`, which is ignored.

## Elements

| Element | Keys |
|---|---|
| `group` | name, `at`, `align` (None, Boresight, VerticalRange, RotaryRange, Impact, LasePoint), `ballistic` (a round key) or `stadia` (target size, m), `units`, `style`, `elements` |
| `line` | `[x, y, deg, len]`, `w`: centred on (x, y), turned `deg` counter-clockwise from +x |
| `seg` | `[x1, y1, x2, y2]`, `w` |
| `poly` | `[[x, y], ...]`, `closed`, `w`: one segment per edge |
| `dash` | `[x1, y1, x2, y2]`, `on`, `off`, `w` |
| `arc`, `circle` | `[cx, cy, r, from deg, to deg]`, `[cx, cy, r]`; `segments` (default one per 10 deg, 32 for a circle) |
| `text` | `"text {expr}"`, `at`, `size` (about 10 per mrad of height). Latin letters only (the game font), use `glyph` for Cyrillic |
| `glyph` | a letter drawn in strokes, `at`, `h`, `w`. Known: П ш Б Г Л Н (`ReticleSpec.Glyph` adds more) |
| `for` | a name or `[names]`, `in` (a range or a list; with several names each item is a list), `do` |
| `if` | an expression, `then`, `else` |
| `use` | a def's name, `args` |

In a `ballistic` group, a child's y is **metres of range** (negative is above), turned into an angle with the game's
ballistics for the round the code maps to that key. In a `stadia` group, y is the range at which a target of that size
fills the mark.

## Expressions

C-like, on doubles, lists, strings and booleans. From loosest to tightest:

| | |
|---|---|
| lambda | `x => ...`, `(a, b) => ...` |
| conditional | `c ? a : b` |
| logic | `\|\|`, then `&&` |
| equality | `==`, `!=` (on numbers, strings or lists) |
| comparison | `<`, `<=`, `>`, `>=` |
| range | `a .. b` and `a .. b step s`, both ends included (`40 .. 5 step -5`) |
| sum | `+` (numbers add, strings and lists join), `-` |
| product | `*`, `/`, `%` (C remainder, sign of the left side) |
| unary | `-`, `+`, `!` |
| call, index | `f(x)`, `list[i]` (negative counts from the end) |

Literals: numbers, `'strings'`, `true`, `false`, lists `[a, b]`. Built in: `sqrt abs sin cos tan atan atan2 deg rad
floor ceil round min max` (angles in radians; `deg` / `rad` convert), `PI`, and `Fov` (the file's `fov`). There is no
integer division: write `floor(r / 100)`. Inside a string, `{expr}` is replaced by the expression's value.

## Screen marks

Marks fixed to the screen rather than to the gun (readout rows, indicator windows, a thermal sight's electronic marks)
go in `screen.items`. They are drawn on an overlay under the sight, on an 800 x 600 reference screen whose height is the
file's `fov`, in mrad from `anchor` (`centre`, `bottom` or `top`; x from the centre line). A file can be all screen:
`"elements": []`.

| Item | Keys |
|---|---|
| `rect` | name, `at`, `size` `[w, h]`, `full_width`, `color` `[r, g, b, a]` |
| `seg` | `[x1, y1, x2, y2]`, `w`, `color` |
| `text` | `"text"`, `at`, `size` (box), `text_size` (mrad), `font` (a game TextMeshPro font), `align`, `color`; rich text allowed |
| `readout` | as `text`, plus `value`; the code sets it by name (`Overlay.Readouts[name]`) |
| `lamp` | name, `at`, `size`, `color`; drawn lit over a dim copy, switched by the code (`Overlay.Lamps[name]`) |

`SightReadout` binds named items to the fire control (the range, the ready mark, the round's letter, the laser lamp).
The «Агава-2» marks (`agava2_monitor.jsonc`) and the 4:3 picture's edges (`essa_monitor.jsonc`) are all screen items.
Their sizes are written in screen heights (`Hm`, from the file's `fov`), so the marks keep their place on the screen
whatever the field.

## Changing a reticle

1. Measure the source drawing in mrad. The files carry no comments, so where a file's numbers come from goes in the
   table at the top of this page, or "inferred" if there's no drawing.
2. Put the marks under the group whose alignment moves them right: fixed marks under `Boresight`, range scales under
   `VerticalRange` or `Impact`, ballistic scales as `ballistic` groups below those.
3. Run the tests. A changed file fails `ShippedReticleTests` until its expansion is written again, which this PowerShell
   line does without leaving the variable set (while it is, the test passes whatever the files hold):
   `$env:T80UM2_UPDATE_EXPANDED = "1"; dotnet test tests; Remove-Item Env:T80UM2_UPDATE_EXPANDED`. Read the diff of
   `tests\expanded\`, then commit both. `tests\expanded\` holds one text file per reticle (`1g46_day.txt` and so on): every node, indented by depth, then the
   screen items. The tests only compare. The variable is the one way to write them, so a changed line is always a
   decision you read in the diff.
4. Check it in game: problems in a file show as warnings in the MelonLoader console, and the reticle is drawn without
   the broken part.
