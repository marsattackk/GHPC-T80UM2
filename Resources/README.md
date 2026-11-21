# Resources

Data files embedded in the DLL. The csproj embeds every file here under its plain name except this README, so a new
file only has to be dropped in. The reticle files (`*.jsonc`) are written in the language described in
[docs/reticles.md](../docs/reticles.md).

| File | What it holds | Read by |
|---|---|---|
| `T80B_wheels_trimmed.bin` | the T-80B's running-gear skin with the fender flaps cut away, so they do not show through the T-80UM2's fenders | `Conversion.WheelsSkin`, passed to `BaseRigT80B.TrimWheels` |
| `kord_belt_round.bin` | one round of the Kord machine gun's belt, made from War Thunder's model | `Commander.BeltRoundFromResources` |
| `kord_belt_round_albedo.png` | colour texture of that belt round | `Commander.BeltRoundFromResources` |
| `kord_belt_round_normal.png` | surface-relief (normal) texture of that belt round | `Commander.BeltRoundFromResources` |
| `kord_belt_round_metal.png` | metal and smoothness texture of that belt round | `Commander.BeltRoundFromResources` |
| `round_3bm42_co.png` | stencil texture for the 3BM42 dart, from the game's own round textures | `Firepower`, applied by `PortAmmo` |
| `round_3bm59_co.png` | stencil texture for the 3BM59 dart, from the game's own round textures | `Firepower`, applied by `PortAmmo` |
| `round_3bm60_co.png` | stencil texture for the 3BM60 dart, from the game's own round textures | `Firepower`, applied by `PortAmmo` |
| `round_9m119m_co.png` | stencil texture for the 9M119M Invar missile, from the game's own round textures | `Firepower`, applied by `PortAmmo` |
| `round_3bk29m_co.png`, `round_3bk31_co.png` | stencil textures for the 3BK29M and 3BK31 HEAT shells, from the game's own round textures | `Common\Heat125.cs`, applied by `PortAmmo` |
| `round_meshes.txt` | index of the round mesh pieces below, by round, and the hit-zone capsules of the 3BM60 and 9M119M | `PortAmmo.ApplyShape` |
| `round_mesh_3bm60_charge.mesh` | the 3BM60's additional charge, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_oring.mesh` | the 3BM60's sealing ring, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_rod.mesh` | the 3BM60's dart rod, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_001.mesh` | the 3BM60's first sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_003.mesh` | the 3BM60's third sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_004.mesh` | the 3BM60's fourth sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9m119m_missile.mesh` | the 9M119M missile's warhead, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9x949_charge.mesh` | the 9M119M's booster charge, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bk29m_warhead.mesh`, `round_mesh_3bk31_warhead.mesh`, `round_mesh_3bk31_oring.mesh` | the 3BK29M and 3BK31 shells, the game's 3BK14M reshaped | `PortAmmo`, via `round_meshes.txt` |
| `1g46_day.jsonc` | 1G46 gunner's day sight reticle, range scale and readout strip | `Sights.ApplyDay` |
| `tpn4_night.jsonc` | TPN-4 gunner's night sight reticle | `Sights.ApplyNight` |
| `k10t_aa.jsonc` | K-10T commander's anti-aircraft collimator reticle | `Commander.SetupSight` |
| `tkn4s_day.jsonc` | TKN-4S commander's day sight reticle | `Sights.CloneTkn` |
| `tkn4s_night.jsonc` | TKN-4S commander's night sight reticle | `Sights.CloneTkn` |
| `agava2_monitor.jsonc` | Agava-2 thermal monitor marks, range readout and the two permission lamps | `Sights.AgavaMonitor` |
| `essa_monitor.jsonc` | the 4:3 monitor picture's edges for the Agava-2 display | `Sights.AgavaMonitor` |
| `modules_built.json` | the T-80UM2's damageable modules (APU, Drozd launcher blocks and others) and what they hook into | `PortModules`, held by `Conversion.Modules` |
| `interior_map.txt` | where each crew member and interior part moves to | `InteriorMap`, held by `Conversion.Interior` |
| `gun_depression.txt` | gun depression by turret angle, for the hull profile | `GunSetup`, name set in `Firepower` |
| `dm_armour.txt` | the armour pieces: mode, thickness figures, material, name, source | `PortText`, before the bundle's copy (`Armour.Load`) |
| `vanilla_edits.txt` | which of the T-80B's armour and parts go, and what replaces them | `PortText`, before the bundle's copy (`Armour.Load`) |
| `rig_map.txt` | the model's moving parts and axes: hatches, running gear, turret ring, trunnion, muzzle | `PortText`, before the bundle's copy (`LoadData`) |
| `material_roles.txt` | which bundle materials take the game's own glass and lens materials | `PortText`, before the bundle's copy (`LoadData`) |
| `smoke_points.txt` | the smoke tubes' launch points | `PortText`, before the bundle's copy (`SmokeLaunchers.Load`) |
| `era_links.txt` | which armour piece belongs to which reactive-armour container (empty: the table has no links) | `PortText`, before the bundle's copy (`EraBricks.LoadLinks`) |

## Formats

The six files that also sit in the bundle (`dm_armour.txt` to `era_links.txt`) are described in
[docs/bundle.md](../docs/bundle.md). These four never did. Numbers take a full stop for the decimal point, and "root
space" means the unit's own space with the turret at rest, in metres.

### `interior_map.txt`

One change per line, `|` separated, `-` for an empty field, `#` lines skipped. Paths are GHPC's own object paths under the
unit (`T-80 HULL COLLIDERS/AMMO/WET RACK/3OF19_006`).

- `copy|<slot>|<new name>|<charge slot>|<new charge name>|<index>` clones a T-80B ammo slot and its charge next to the
  original, as slot `<index>` of the same rack (`AmmoVisualPlaceholder.RackIndex`). This is how the wet rack grows, and
  `Firepower.cs` names those indices for the missiles.
- `move|<path>|<kind>|<twin>|<x,y,z>|<x,y,z,w>|<?>|<?>|<measure>|<scale>|<new parent>` sets the object's local position
  under its parent, and its local rotation (a quaternion) and scale when they aren't `-`. With a new parent it is moved
  under that first. `kind`, `twin`, `measure` and the two unnamed fields are notes from when I measured the places, and
  nothing reads them.
- `mesh|<path>|<x,y,z,x,y,z,...>|<a,b,c,...>` gives the object a new mesh, for its renderer and its mesh collider both:
  vertices in the object's local space, then triangles as vertex indices. The fuel tanks and the turret seats use it.

`InteriorMap.Parse` knows a few more kinds (`drop`, `chargeslot`, `crewcopy`, `newmesh`, `freezecarousel`) that this
tank doesn't need.

### `modules_built.json`

Generated: my build writes it as one line from a module list that isn't in this repo. A JSON formatter makes it readable
and the parser doesn't mind the whitespace, so you can edit it by hand. If you send the change back, say in the pull
request what you changed, so I can carry it into that list. The top level is `{"platform_names": {}, "modules": [...]}`,
and each module has:

- `id`, `name` (what the damage report calls it), `status` (the status line when it's destroyed, optional) and `kind`
  (`report_only` only reports a hit, and the others, such as `gunner_sight` or `radio`, take over a game system).
- `group`: the T-80B collider group it rides (`HULL`, `TURRET`, `CUPOLA`). `NSVT` means the НСВТ's cradle, and those
  modules are built with the station.
- `health`, `threshold`, `pressure`: written to the hit zone's `DestructibleComponent` (`_health`, `_damageThreshold`,
  `_pressureTolerance`).
- `hooks`: the game fields it is wired into, as `{"hook": "Class.Field", "target": "<which one>"}`. `replaces`: the
  T-80B hit zones it takes over, by name. `existing: true` marks one of the T-80B's own modules, kept rather than built.
- `boxes`: its hit boxes, each a centre `c` and a size `s` in root space. Two modules have `hulls` instead, convex
  shapes as `v` (vertex coordinates) and `t` (triangles).
- `armour` (`ke` and `ce` in mm, and the four flags), and the x-ray look: `xray` (a shape as `v` and `t`, or `false` for
  none), `xray_colour`, `xray_material`.

### `gun_depression.txt`

`<bearing> <elevation>` per line, in degrees, `#` starts a comment. The bearing is the turret's angle off the nose,
clockwise seen from above, and the elevation the lowest the gun may go there. Between two points the limit runs in a
straight line, round the full circle, and it can only raise the gun's own −5°, never lower it. The four points now give
−4° over the rear deck, from 150° to 210°, with a 5° ramp at each end.

### `round_meshes.txt` and the `.mesh` files

```
mesh|<shape>|<piece>|<file>|<vertices>|<triangles>|<SHA-1>
capsule|<shape>|<collider>|<x,y,z>|<radius>|<height>|<axis>
```

`<shape>` is the round's `Shape` in `Firepower.cs` (`3BM60`, `9M119M`). A `mesh` line puts `<file>` on every part of the
donor round's model named `<piece>`. The `<vertices>` and `<triangles>` counts and `<SHA-1>`, the start of the file's
SHA-1 in hex, have to match the file or the piece keeps the donor's shape with a warning, so change them together with
the file (an empty `<SHA-1>` skips that check). A `capsule` line resizes the hit-zone collider of that name: centre,
radius and height in metres in the round model's space, and the axis as Unity's `CapsuleCollider.direction` (0 x, 1 y,
2 z).

A `.mesh` file is little-endian: the vertex count and the triangle count as `int32`, then every vertex position (three
`float`s), every normal (three), every UV (two), and the triangles as three `int32` indices each.
