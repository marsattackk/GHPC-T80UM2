# The asset bundle

The model, textures and armour meshes live in one Unity asset bundle, `Mods\T80UM2\t80um2`. It isn't in this repo and
neither is the Unity project it came from (mine, built around War Thunder's model of the T-80UM2), so this page is what
the code expects to find in it. If you rebuild it, this is what to build. Where it says "silently", nothing goes in the
log and the tank just looks wrong.

Built with Unity **2022.3.62f2**, the same as GHPC. A bundle has to match the game's Unity version (see
[game-updates.md](game-updates.md)).

## What gets loaded

`T80UM2\Mod.cs` opens the bundle through `BundleAssets` and loads two prefabs by name:

| Asset | Needed | If missing |
|---|---|---|
| `T80UM2_visual` | yes | error, no model |
| `T80UM2_armor` | no | warning `armour will not be changed.`, the T-80B's armour stays |

Six text tables are still inside the bundle as `TextAsset`s: `dm_armour`, `vanilla_edits`, `rig_map`, `material_roles`,
`smoke_points` and `era_links`. The DLL carries its own copy of each in `Resources\` and `PortText` reads that first,
the bundle's copy only second. Where both exist they are the same text. Edit the one in `Resources\` and rebuild the
DLL, no Unity involved. The bundle of the latest release keeps its copies as the fallback, and a new bundle is only
needed for what the tables name.

| File | Needed | If missing |
|---|---|---|
| `dm_armour` | with `T80UM2_armor` | warning `bundle has no armour values; the T-80B's armour stays.` |
| `vanilla_edits` | with `T80UM2_armor` | the T-80B's own plates stay switched on under the T-80UM2's, and the armour warning names them |
| `material_roles` | no | glass keeps the bundle's own materials |
| `rig_map` | no | nothing the rig map names moves: the three hatches, the whip, the НСВТ's mount and cupola, the hanging flaps |
| `era_links` | no | nothing, the file is empty (see below) |
| `smoke_points` | no | warning, the grenades leave from the T-80B's tubes |

The catch is that most of them name things in the bundle. Numbers, armour types, x-ray colours, roles, donors, hinge
angles and smoke points are safe to change on their own. Renaming an armour piece, or pointing a line at a different
node or material, only works if the bundle has that name too. Otherwise it's a new bundle, built with the renamed pieces.

Empty lines and lines starting `#` are skipped. A bad line in `dm_armour`, `vanilla_edits` or `rig_map` is skipped with
a warning naming the line, and the rest loads. Numbers use a full stop for the decimal point. `<TAB>` below means a real
tab, no spaces around it.

## `T80UM2_visual`

`Conversion` (its visuals step) instantiates it and moves four children of the root onto the T-80B's bones. Each moved
part gets `T80UM2_` in front of its name and its local transform reset, so model each one in its bone's own space.

| Node | Goes onto | If missing |
|---|---|---|
| `HULL` | the T-80B's hull bone | silently, no hull |
| `TURRET` | the turret bone | silently, no turret |
| `GUN` | the gun bone | silently, no gun |
| `MANTLET_COVER` | the gun bone, skinned between the turret and the gun so the canvas stretches with the elevation | no canvas cover |

- Everything else is found later by name, as `T80UM2_<name>` or `<name>` anywhere under the unit: the three hatch lids
  (`HATCH_COMMANDER`, `HATCH_GUNNER`, `HATCH_DRIVER`), the radio whip `WHIP_01`, the six hanging flaps (`FLAP_ARMOR_BODY_L_01`,
  `FLAP_ARMOR_BODY_R_01`, `FLAP_DECOR_08`, `_09`, `_10` and `_13`) and the НСВТ's parts (`NSVT_H` the traverse mount,
  `NSVT_V` the elevation mount, `NSVT_GUN`, `NSVT_SKIN01` and `NSVT_SKIN02`, `CUPOLA` and `CUPOLA_SIGHT`). They are listed in
  `rig_map` where they move.
- The running gear is the T-80B's own. The rig map says `gear|base`, so War Thunder's wheels and tracks aren't used and
  aren't in the bundle's rig.
- Eight red caps for the «Дрозд» tubes, `aps_cap_0` to `aps_cap_7`, and eight for the 902Б smoke tubes, `smoke_cap_0` to
  `smoke_cap_7`. They are domes seated in the tube mouths, made at the build. The «Дрозд» hides a cap when its tube
  fires and shows it again when the block is reloaded, and each smoke cap goes when its grenade does.
- The windows of the sights and periscopes aren't modelled glass: the build generates them from the measured corners of
  each pane. That's why `material_roles` names a window material for each.
- LODs: the hull and the turret have a `LODGroup` with a simpler mesh in LOD1 (`t80um2_hull_lod1`, `t80um2_turret_lod1`).
  LOD1 gets LOD0's materials at spawn, so it needs as many submeshes as LOD0 has materials, or it is left as built.
  Moving lids stay in the full-detail level.

## Materials

Matched with `StartsWith` unless it says exact. Everything is prefixed `t80um2_`.

- The six paint materials (`t_80u_body`, `t_80u_gun`, `t_80u_turret`, `t_80um2_turret`, `mg_mount_nsvt_t_80u` and
  `era_kontakt_5_t_80u`) are replaced with a copy of the T-80B's `GHPC/VehicleShader` material. That copy takes the
  material's `_MainTex` (the weathered albedo), `_BumpMap`, `_MetallicGlossMap` (the weathered metal and gloss),
  `_DetailMask` (the paint mask) and `_OcclusionMap`. The camouflage is in the albedo, not laid on by the shader.
- The clean versions of the albedo and the metal map sit in the material's two spare slots, `_DetailAlbedoMap` and
  `_DetailNormalMap`. `Weathered paint` set to false swaps them in. The weathering itself (dirt in the creases, an earth
  gradient toward the ground, dust on the tops, rust runs, chipped edges) is baked into the `*_albedo_weathered` and
  `*_metal_weathered` maps at the build, from the model. The bundle carries both sets.
- `t80um2_hatch_inner*` is the inside of the hatch lids. `HatchUndersides` first gives them a flat plain material, then
  paints them from a smooth plate of the turret's atlas, because the model's UVs put them on bolt detail.
- Anything on those parts using plain `Standard` is switched to GHPC's `Standard (FLIR)` so it shows in thermal.
- Names in the first column of `material_roles` are matched exactly.

## `T80UM2_armor`

Shapes only, cut from War Thunder's damage model of the T-80UM2. The numbers come from `dm_armour`. `DmArmourApply`
applies `vanilla_edits` first, then puts the pieces under the root's `HULL`, `TURRET` and `GUN` onto the matching bone
and collider group.

Each piece is a direct child of its group, with a readable mesh, named `dm_<anything>|<piece>`. `<piece>` is the key in
`dm_armour` and becomes the object's name. Children not starting `dm_` are skipped, which keeps the `reshape|<name>`
meshes out of the armour. `geometry` pieces are measured through the mesh, so it has to be closed.

## Text files

### `dm_armour`

One piece per line, tab separated, at least 11 fields:

```
<piece> <mode> <ke> <ce> <ke after> <ce after> <angle> <material> <x-ray> <name> <source> [<slat>]
```

The bundle's table has 184 pieces: 34 `geometry`, 51 `nominal`, 29 `era`, 1 `composite` and 70 `drawn`.

- `mode`: `geometry` (`ke` and `ce` are multipliers per mm of path), `composite` (uniform, convex collider), `drawn`
  (x-ray and AAR only, no armour), `era` (the «Контакт-5» bricks, with `ke after` and `ce after` for once a brick has gone
  off), anything else uniform (the bundle uses `nominal`).
- `ke`, `ce`: mm of rolled steel against kinetic and chemical rounds. The numbers are mine, from War Thunder's shapes
  and my own table (see [armour.md](armour.md)).
- `angle`: `1` to count the hit angle on a uniform piece.
- `material`: `armour_steel`, `mild_steel`, `plain`, `composite`, `filling`, `filling_steel` or `liner`, all the mod's
  own armour types. Empty for `drawn`.
- `x-ray`: empty (not drawn), `template` (GHPC's see-through material) or `r,g,b[,a]` (a tinted copy).
- `name`: the label in the armour view and damage report. `source` isn't read and is empty on every row. The sources
  of the numbers are in [armour.md](armour.md).
  `slat`: `1` for slat armour.

The values in the bundle are the "game scale" set. The `Armour preset` setting's other value scales two fillings (the
turret cheek package and the glacis fillings) back to my sourced targets in code, so that preset needs no second table.

```
turret_03_back_dm<TAB>nominal<TAB>3<TAB>3<TAB>0<TAB>0<TAB>0<TAB>mild_steel<TAB><TAB>stowage box / fender sheet<TAB><source><TAB>0
```

### `vanilla_edits`

What to do to the T-80B's own armour, by collider object name, `|` separated. The pieces War Thunder's model replaces
are switched off. The bundle's list is 39 `off` lines and 8 `reshape` ones. The code adds the names the shared T-80B rig
lists as replaced, and `Firewall`, on top of the bundle's, so a name added there needs no new bundle.

| Line | Does |
|---|---|
| `off\|name` | switches the piece and its children off |
| `strip\|name` | removes its colliders, armour and renderer, keeps the children (done last) |
| `reshape\|name` | gives it the mesh of `reshape\|name` in `T80UM2_armor`: the T-80B's three compartment volumes and its five fender fuel tanks, rebuilt to the T-80UM2's shapes |
| `xform\|name\|m0,...,m15` | moves it by a `Matrix4x4` (column-major), in the unit's root space at rest |
| `bone\|path\|group\|dx,dy,dz` | moves a bone and the colliders following it, in metres (`group` is a label, not read) |

### `material_roles`

```
<bundle material><TAB><role><TAB><vanilla material or -><TAB><donor units, comma separated>
```

The role is only for the log. `-` makes the line a note. Donors are unit keys like `T80B` or `LEO2A3`, tried in order.

### `rig_map`

`|` separated, positions in the unit's root space, extra trailing fields ignored.

| Line | Fields |
|---|---|
| `gear\|mode` | `base` (leave it, the default), `follow` or `port`. The T-80UM2 uses `base`: the T-80B's running gear stays |
| `axes\|turret\|gun\|muzzle\|pitch` | the turret ring, the gun's trunnion and the muzzle where War Thunder has them, and the ring's tilt in degrees (1.5 here). The turret turns about the tilted ring and the gun and muzzle move there |
| `hatch\|?\|role\|node\|baseBone\|hinge\|axis\|angle\|lift` | one hatch, driven by the base bone's `Openness`: lifts for the first 30 %, then turns. Three here, the commander's, the gunner's and the driver's |
| `mount\|?\|node\|bone\|pivot\|axis` | a part with a pivot of its own, here the НСВТ's mounts and the cupola. The NSVT station's code turns them |
| `whip\|?\|node\|base\|tip\|rigid` | the radio whip |
| `flap\|?\|node\|a\|b\|?\|limit\|taper\|ramp\|lag` | a hanging flap on a jiggle bone, `lag` optional |

`?` fields aren't read. What `rigid`, `taper`, `ramp` and `lag` do is in `HatchRig.Whip` and `HatchRig.Flap`. There is no
`fpart` or `part` line: the running gear is the T-80B's.

### `era_links` and `era_gone`

```
<armour piece><TAB><visual brick>
```

`era_gone` is a second table of the same kind that this tank doesn't have, so no stowage goes with a brick. The
«Контакт-5» bricks are `era` pieces in `dm_armour` (`ex_era_hull_*` and `ex_era_turret_*`). A line in `era_links`
ties a piece to the brick it sets off, and pieces that name the same brick go off together. The released file is empty
(0 bytes), so every brick is picked by geometry. A brick the code looks for needs a node `<brick>` and, beside it,
`<brick>_open` (the burst look). `<brick>_lid` and `<brick>_soot` are optional. A brick that is set off vanishes from the
model, as on the T-90s.

### `smoke_points`

Floats separated by any spaces, commas, tabs or newlines, three per tube mouth, in the turret bone's space. There are 8
tubes, the 902Б's. Left and right go by the sign of x, then top down, matched to the T-80B's smoke slots. One bad
number drops the whole list, since it would shift every tube after it.

## Released bundles

SHA-256 of `Mods/T80UM2/t80um2` in each release zip. When a release changes the bundle, add a row.

| Version | Bundle | Size in bytes |
|---|---|---|
| 0.1.0 | `dcdcbcb310c857b773e410df490a1ad6ac0e7b4e047509cd7e986046bddb41ca` | 216,206,170 |
| 0.1.1 | `67126552cfe50fe11dde163755850d659b9ea97ff3986813377af7ccf3db2869` | 216,229,979 |
| 0.2.0 | `6d363133016130d02d0d378600a30ca8e62a0bc1e93959a75b6e47683ecc342a` | 216,229,964 |

```
Expand-Archive T80UM2_0.2.0.zip -DestinationPath check
(Get-FileHash check\Mods\T80UM2\t80um2 -Algorithm SHA256).Hash.ToLower()
```
