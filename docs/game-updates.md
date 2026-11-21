# After a GHPC update

GHPC is a Unity Mono game with no obfuscation, so the mod patches its classes with Harmony and reads private members
through reflection. An update that renames or removes one of those breaks the feature that uses it and nothing else.
This page is how to find what broke and fix it.

## Start with the offline check

```
powershell -ExecutionPolicy Bypass -File tools\check-game.ps1 -GameDir "<the game's Bin folder>"
```

It builds the mod and looks every patch target and every `Reflect` member up in the new game's DLLs, without starting
the game. Anything gone shows as `MISSING` with the file it's used in. It also reads the hit-zone table of
`ModuleBuilder`: each owner field the tank's modules wire to (the sights' camera slots, the fire control's computer and
laser, the missile guidance unit, the radio and the НСВТ's breech) and the destructible events they hang on. Names the
code only knows at run time are in `tools\check-game-list.txt`, and the script says `NOT COVERED` if the code gains one
the list doesn't have. The RepairKit patch isn't checked because it hooks another mod, not GHPC.

Then, in this order:

1. Build against the new `Assembly-CSharp.dll` and fix the compile errors. Those are public members and the
   compiler points at every one.
2. Fix whatever `check-game.ps1` lists.
3. Start the game and read `MelonLoader\Latest.log` from the top. Harmony errors and `Reflect` warnings come before the
   main menu.
4. Check the T-80UM2 is in Instant Action, the mission customiser and the Soviet side's campaign.
5. Spawn one and read the log again. Conversion runs at spawn, so most warnings only show up here.
6. Go through the test list at the bottom.
7. Check a vanilla T-80B in a mission with the mod on: some patches touch every unit.

## What it looks like in the log

- `Game field X.y not found: a game update may have changed it, ...` comes from `Reflect`. The word after "Game" can
  also be `method`, `property` or `property setter`. Once per session per member. `can't be read as bool (...)` means
  the field is still there but its type changed. `can't be looked up (...)` usually means a method gained an overload.
- `... could not be patched (a game update?), so that part of the mod is off` with the patch class in front: its
  target went. Each class goes in on its own, so the rest of the mod still works.
- `<feature> failed (the game carries on without it this time; 1 of 3 logged)` is `PatchGuard`. The patch ran and the
  code inside threw. The stack trace has the line. It goes quiet after three.
- `Unity exception in our code: ...` is `UnityErrors`, an exception in one of the mod's behaviours or coroutines.
- `T-80UM2 <step> failed (<what you get instead>): ...` is one row of the conversion tables throwing. The spawn steps are
  base visuals, drive, visuals, paint, glass materials, running gear, mantlet cover, tactical numbers, lamps, hatch
  undersides, LOD materials, crew voices, smoke launchers, armour, interior, modules, ammo setup, coax setup, gun setup,
  smoke grenade, hatches, turret axis, Drozd, NSVT and commander's lid. The late ones, two frames after the spawn, are smoke
  crew refresh, sight fitting, NSVT late setup, module wiring, thermal heat and ammunition self-check. The rest of the
  conversion went on. `T-80UM2: <unit> was converted
  after it spawned; it keeps the T-80B's ammunition, and has no Drozd or NSVT` means the spawn patch missed that unit and
  the sweep caught it, which leaves out the steps that only work at spawn.
- `T-80UM2 modules: ...` and `T-80UM2 ammunition self-check ...` list what they couldn't find. The ammo one ends by
  asking for the log, ignore that bit.
- `APS: GHPC's live-round list not found (game update?); the APS sees nothing.` is the «Дрозд» losing its view of the
  rounds in flight.
- `donor prefab 'T80B' not in the unit registry` or `vanilla material '...' not found`: a borrowed part couldn't be
  copied.
- `T-80UM2: no armour values in the DLL or the bundle; the T-80B's armour stays.` isn't a game update: `dm_armour.txt`
  has gone missing from `Resources\` and the bundle has no copy either. `T-80UM2 armour: <n> T-80B armour piece(s) not
  found ...` can be one: it names the T-80B's armour pieces that `vanilla_edits` switches off and GHPC no longer has under
  that name.

A missing reflected member is looked up once and cached, and the feature code checks for null and skips, so a rename
shows up as a feature that's gone quiet, not a crash. Things that break most often, roughly in order: a private field
or method renamed, a patched method's parameters changed, a public member renamed (compile error), a prefab or material
renamed, and last, nothing renamed but the game's behaviour around the mod changed. That last one gives no log line at
all. You only see it by playing.

## Fixing a renamed member

Open the game's `Assembly-CSharp.dll` (`GHPC_Data\Managed`) in a .NET decompiler, find the class from the warning and
look for the same kind of member with a similar type and name. Find Usages on the old name's neighbours usually gives
it away. For a method, match the parameter types and read the body. Decompiling the previous game version and diffing
the class is the quickest way if you still have it. Change the string or signature, build, and test that feature.

If a whole mechanism has gone (say `LiveRound.penCheck` is split up), the code using it has to be rethought, not
renamed. The «Дрозд» is the one most exposed to that: it watches every round in flight through
`LiveRoundBatchHandler`.

## If GHPC moves to a new Unity version

The bundle was built with Unity 2022.3.62f2, the version GHPC runs on, and a bundle has to match the game's Unity
version to load. If that changes, expect `T-80UM2 bundle could not be loaded: <path>` and no `Loaded T-80UM2 visuals`
line. The mod still runs but the T-80UM2 keeps the T-80B's model and armour. The fix is a new bundle built against
[bundle.md](bundle.md). The code doesn't change.

## Where the mod hooks in

Files are in `Common\` unless they say `T80UM2\`. Each group ends with what you see in game when it breaks.

**Unit lists and spawning.** `UnitRegistry.cs` patches `MissionConfigMenuPanel.GetUnitKeysByClass`,
`UnitSpawner.GetMetadataByUniqueName` and `UnitSpawner.SpawnUnit`, clears the spawner's and customiser's name caches,
and sets `Unit._uniqueName` and `_friendlyName`. Broken: the T-80UM2 is missing from a list, spawns as a plain T-80B,
or shows as T-80B in the HUD. A unit the spawn patch misses is still caught by the sweep in `ConvertSweep.cs`.

**Ammunition and the menus.** `PortAmmo.Menu.cs` patches the customiser's ammo dropdown and the campaign's logistics
and unlocks, `PortAmmo.Spawn.cs` the restock check, `PortAmmo.cs` the tandem ERA rule on `LiveRound.penCheck`.
`LoadoutManager.RegisterAllBallistics` is called after the ammo list is swapped. The clips start from the T-80B's own
(`clip_3BM32`, `clip_3BK18M`, `clip_9M112M`) and the round models are re-textured and reshaped from `Resources\`.
Broken: the customiser shows the T-80B's rounds, rounds fly with no ballistics, or the models come out with the wrong
paint. The self-check says what's missing.

**Gun and reload.** `GunSetup.cs` copies the gun's codex entry before renaming it (every T-80B shares it) and rescales
`AmmoFeed`'s reload stages. Broken: reload time is the T-80B's, or every T-80B gets the T-80UM2's gun name.

**Gunner's sight and fire control.** `GunnerFcs.cs` patches `FireControlSystem` (`AdjustAimByRatio`, `Update`,
`FiringBlocked`, `CompensatedWeaponVector`, `GetLeadWorldOffset`, `UpdateLaserAim`, `DoLase`). Its `Resync` rewrites the
fields the FCS caches in Awake and DeferredStart. `ReticleMeshes.cs` clears `ReticleMesh.SMR` for every reticle swap.
`ThermalSight.cs` patches `PlayerInput.Update` and two `CameraManager` methods (`AttachCamToSlotDirect`,
`ResetTempAttach`) for the night toggle, and takes the «Агава-2»'s thermal picture from the M60A3 TTS prefab.
`ComputerOff.cs` patches `FireControlSystem.SetManualMode` and `Lase`: the «Буран-ПА», «Дубль» and the commander's own
fire all work with the computer off and the range set by hand. Broken: the sight swings past its stops, the gun fires
before it's laid, reticles don't draw or keep the old one, the night key does nothing, the «Буран-ПА» lases anyway.

**Commander's ТКН-4С.** `T80UM2\Sights.Tkn4s.cs` reads `CameraSlot._reparentTarget` and builds the sight from two
copies of the T-80B's own TPN-3-49 night sight. `TargetDesignation.cs` patches `CrewBrainWeaponsModule.SetAimVector` so GHPC's
Aim Override key slews the turret to his line of sight. Broken: the ТКН-4С has no view or sits at the wrong place, or
the target designation key does nothing.

**The НСВТ.** `RemoteMg.cs` patches `PlayerInput.Update`, three `CrewBrainWeaponsModule` methods (picked in code) and
four `CameraManager` methods (`RescanCamSlots`, `AttachCamToSlotDirect`, `ToggleCameraSlotSet`, `ResetTempAttach`), and
writes the unit's camera slots, platforms and the gun's rack, from a T-64B's remote station as the donor.
`T80UM2\Commander.cs` also reads `CameraSlot._reparentTarget`, `AmmoFeed.ReloadStage.Duration` and
`AmmoFeed._feedClipMain`, and `T80UM2\Commander.Link.cs` patches the `WeaponSystem.AbleToFire` getter so the gun fires
only for the player's own tank. The commander never leaves the hatch and fires from under armour: `Climbs` is false there,
so `OutToFirePatch` only checks whose tank it is (with it true it would also wait for him to be out). `CrewClimb.cs` is
the climb-out, switched off, `CollimatorSight.cs` the К10-Т view, `KordBelt.cs` and `KordBrass.cs` the belt and the spent
cases. Broken: free-look in the commander's seat fires the НСВТ at the cursor, the station has no view or no ammo, the
camera lands on the wrong slot, or an AI tank's gun fires.

**The «Дрозд».** `ActiveProtection.cs` (`Aps`) patches `LiveRoundBatchHandler.Update` (it scans the list of live
rounds, `_liveRounds`, every frame) and `PlayerInput.AddSmokeButtons` for the vehicle menu entry, calls
`PlayerInput.AddMenuButton` and `LiveRound.createExplosion` by reflection and reads `LiveRound._armed` and
`_failedToDetonate`. `T80UM2\Countermeasures.Drozd.cs` holds the blocks, sectors and times. Broken: no «Дрозд» entry in
the vehicle menu, missiles and rockets go through unhurt, or a burst is drawn without effect.

**Smoke.** `SmokeLaunchers.cs` seats the grenade slots on the 902Б tubes and patches
`VehicleSmokeManager.FireSmokes` (to rebuild the crew lists a runtime spawn never got) and `CalculateLaunchImpulse`.
`ShtoraScreen.cs` and `ShtoraScreen.Smoke.cs` (the 3Д17 only, the tank has no «Штора») read a dozen
`VehicleSmokeManager` and `Grenade` fields. `SmokeCaps.cs` hides a cap when its tube fires. Broken: the smoke
launchers never fire, or throw the T-80B's grenade, or the caps stay on.

**Armour and rounds.** `EraBricks.cs` patches `LiveRound.penCheck` and `UniformArmor.Detonate()` by hand (both or
neither, and the log says `ERA bricks: patching failed`) for the «Контакт-5». `DmArmourApply.cs` sets
`UniformArmor._armorType`, `_isEra` and friends and `VariableArmor._armorType`. Broken: ERA behaves as GHPC's own, or
every piece takes the game's default armour material, which is the biggest effect of anything on this page.

**Rig, modules and heat.** `Followers.cs` uses `LateFollow` shifts, and `PortAxes.cs` (with `PivotMove.cs`, `TiltedRing.cs`
and `TraverseLimits.cs`), `MantletCover.cs` and `VanillaEditsApply.cs` use `AimablePlatform._originalLocalRotation`. `PortModules.cs` builds the hit zones from the module list and
`ModuleBuilder.cs` wires them to the game's hit-zone fields. `ThermalHeat.cs` writes `HeatSource.heat` and clears its
renderer list. `Mobility.cs` writes the engine, gearbox and top speeds. Broken: parts displaced or crooked, modules
with the wrong health, parts black in white-hot, the T-80B's speeds.

**Other mods.** `RepairKitCompat.cs` patches RepairKit's `Apply(Unit, NwhChassis)` if it's installed. A rename there
logs `RepairKit found, but not its repair method`. Nothing to do with GHPC updates.

## Names looked up as strings

These don't go through `Reflect`, so a rename gives no warning of that shape.

`tools\check-game.ps1` reads the literal names in `Reflect.*`, `AccessTools.Method`, `Traverse.Create(x).Field`,
`Type.GetField` / `GetMethod` and the hit-zone table of `ModuleBuilder`. It cannot check a field enumeration
(`GetFields`, as in `PortAmmo.MakeClip` or `RemoteMg.Platform`) or a lookup whose name is a variable: after a game update
read those against the class by hand. The script's summary lists every one of them, and that list is the one to go by.

The rest, which no script reads:

- **T-80B rig** (`BaseRigT80B.cs`): the `T80B_rig/HULL`, `TURRET` and `gun` bones, the renderers it hides and the running
  gear it keeps (trimmed against `Resources\T80B_wheels_trimmed.bin`, which has to match the game's vertex count). A name
  that goes shows as the T-80B's hull or fender flaps left drawn inside the T-80UM2.
- **The T-80B's armour and interior** that War Thunder's model replaces (`vanilla_edits`, plus the collider paths in
  `Resources\interior_map.txt`). A renamed one stays switched on inside the T-80UM2's armour, and the armour warning names
  it. Some carry GHPC's own trailing space. Keep it.
- **Donors**: units `T80B` (the base, the army and the clips), a T-64B (the НСВТ's remote station), the M60A3 TTS (the
  «Агава-2»'s thermal picture) and the units `material_roles` names (`T80B`, `T62`, `T55A`, `LEO2A3`). `DonorParts.cs` copies
  them at the main menu, because VanillaAssetUtil can destroy the prefabs after a mission.
- **Ammunition** (`T80UM2\Firepower.cs`, set up by the conversion's ammo setup row): clips `clip_3BM32`, `clip_3BK18M` and `clip_9M112M` from the T-80B's list.
  The three HEAT rounds (`Common\Heat125.cs`) are all copies of `clip_3BK18M`. Built-in ERA is found by the armour's
  name (Kontakt-5, Relikt), so a mod that renames its Kontakt-5 blocks turns them into add-on ERA for 3BK29M.
  3BK31's second jet (`Common\SplitCharge.cs`) hangs off `LiveRound.logHit` and is drawn as a jet through
  `AarController.createShotTrace`. Without them it's one jet again, or fragment lines in the recap.
  The tandem pop and the white precursor line (`Common\TandemLook.cs`) use `LiveRound.doImpactVFX`,
  `ParticleEffectsManager.CreateImpactEffectOfType` and `ShotTraceVisual.jetColor`. Lose them and tandem rounds look like plain HEAT.
- **Fonts and materials**: `Soviet7SegmentFont SDF` and `Reticle LiberationSans SDF` where the reticle files name them,
  `Standard (FLIR)`, `GHPC NOD gen 2 RU` (the «Буран-ПА»'s image intensifier look) and `see-through teal gray` for the
  x-ray (no log line if that one goes).
- **Scenes and input**: the menu scenes in `MenuScenes.cs`, `TR01_showcase`, and the buttons `Toggle Night Sight` and
  `Smoke Grenades`. A renamed button silently does nothing.

## Test list after a repair

1. Start the game and read the log from the top: no `Game field`, no Harmony error.
2. T-80UM2 in Instant Action and the customiser. Spawn one and read the log.
3. Drive, traverse, elevate. Lase and fire. A reload, with the rounds the customiser offers.
4. The 1Г46 day channel at its zoom steps, the «Агава-2» in both fields, then the «Буран-ПА» through the `Night sight`
   setting (fires with the computer off, range set on the scales), then again with the sights shot out.
5. The commander's ТКН-4С day and night, «Дубль» (he lays and fires the gun) and the target designation key.
6. The НСВТ: the gun moves and fires with the К10-Т view, the spent cases fall, and an AI tank's one does not fire.
7. The «Дрозд»: on and off from the vehicle menu, a missile and a HEAT rocket fired at the front, the glacis and 40
   degrees off the nose, and a reload.
8. The 902Б tubes: all 8 in one salvo, the 3Д17 clouds going out along their tubes, the caps coming off.
9. Shoot it with an APFSDS, a HEAT and a missile and read the damage report, including a hit on a «Контакт-5» brick.
10. Traverse, then watch the AAR replay.
11. A vanilla T-80B in the same mission, untouched.
