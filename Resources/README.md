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
| `round_meshes.txt` | index of the round mesh pieces below, by round, and the hit-zone capsules of the 3BM60 and 9M119M | `PortAmmo.ApplyShape` |
| `round_mesh_3bm60_charge.mesh` | the 3BM60's additional charge, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_oring.mesh` | the 3BM60's sealing ring, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_rod.mesh` | the 3BM60's dart rod, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_001.mesh` | the 3BM60's first sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_003.mesh` | the 3BM60's third sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_3bm60_sabot_004.mesh` | the 3BM60's fourth sabot petal, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9m119m_missile.mesh` | the 9M119M missile's warhead, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9x949_charge.mesh` | the 9M119M's booster charge, made from War Thunder's model | `PortAmmo`, via `round_meshes.txt` |
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
