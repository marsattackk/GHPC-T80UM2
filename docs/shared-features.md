# Shared features in T80UM2

`Common\` is code my GHPC mods share. A file is the same wherever it appears, but each tank gets only the files it needs.
Changes meant for this repo go as an issue or a pull request describing them, and I make them in the shared copy and stamp
every mod again. A fork of your own may edit it: the top of `Common\STAMP.txt` says how. Some of these files hold features
T80UM2 doesn't switch on, and this page says which. A feature is on when a file in the tank folder names it. Where only
other Common code calls a feature's files, the row names the caller: those files stay, even where the tank never gets that far.

| Feature | What it is | Common files | Switched on in this tank |
|---|---|---|---|
| Soft-kill: jammer, laser warning and screens (Shtora) | The «Штора-1» sector jammer, the laser warning receiver and the 3Д17 smoke screens, with the page or panel that shows them. | `Shtora.cs`, `ShtoraMath.cs`, `ShtoraPage.cs`, `ShtoraScreen.cs`, `ShtoraScreen.Hud.cs`, `ShtoraScreen.Laser.cs`, `ShtoraScreen.Menu.cs`, `ShtoraScreen.Smoke.cs`, `ShtoraScreen.Station.cs`, `WeaponHudLines.cs` | yes, named in `Countermeasures.cs`, `Mod.cs` |
| Active protection (hard-kill) | Hard-kill active protection (the T-80UM2's «Дрозд», the T-90M's «Арена-М»): radars and launcher blocks that shoot down an incoming missile, rocket-propelled grenade or HEAT shell before it hits, with the fire computer's engagement logic. | `ActiveProtection.cs`, `ActiveProtectionMath.cs` | yes, named in `Countermeasures.Drozd.cs` |
| Smoke launchers | Smoke grenade launch points on the model's own tubes, the crew refresh and the smoke caps. | `SmokeLaunchers.cs`, `SmokeCaps.cs` | yes, named in `Conversion.cs`, `Countermeasures.cs`, `Mod.cs` |
| Air-burst fuse | A fuse that bursts a high-explosive round over a target at the range the gunner lased, with the fragment cone. | `FragmentCone.cs` | used by `ActiveProtection.cs` |
| Camouflage nets | Net screens that spoil a hit and are cut away where an ERA block went off. | `NetCut.cs` | used by `EraBricks.cs` |
| Remote machine-gun station | A remotely fired machine gun with its belt, brass and fire control, cloned from a donor vehicle. | `RemoteMg.cs`, `KordBelt.cs`, `KordBrass.cs` | yes, named in `Commander.cs`, `Mod.cs`, `Sights.Tkn4s.cs`, `Sights.cs` |
| Commander out of the hatch | The commander climbing out of his hatch to work an open hand-laid machine gun and back in, with his body drawn at the mount and exposed to fire while he is out. | `CrewClimb.cs`, `CrewFigure.cs` | named but switched off: `Climbs = false` in `Commander.Link.cs` |
| Thermal sights | Thermal imaging sights: the thermal view, the heat patches on the model and the sight setup. | `ThermalSight.cs`, `ThermalHeat.cs` | yes, named in `Commander.cs`, `Conversion.cs`, `Mod.cs`, `Sights.Agava2.cs` ... |
| Collimator sight | An open collimator sight (the К10-Т) seen at the gunner's eye: its reticle at infinity on the tilted glass plate, the bezel and the body. | `CollimatorSight.cs` | yes, named in `Commander.K10T.cs` |
| Gunner's fire control and sights | The stabilised sight, its aim limits, the laser, the fire gate and the readouts. | `GunnerFcs.cs`, `FcsMath.cs`, `SightReadout.cs`, `SightMotion.cs`, `TraverseLimits.cs` | yes, named in `Sights.Agava2.cs`, `Sights.Tkn4s.cs`, `Sights.cs` |
| Ballistic computer off | A sight you fire through with the ballistic computer switched off, as the real ТПН-4 and the commander's «Дубль» are: range by hand, no lead, no laser. | `ComputerOff.cs` | yes, named in `Sights.Night.cs`, `Sights.Tkn4s.cs` |
| Target designation | The commander puts the gun's aim on his own line of sight: the turret slews there and the gunner's sight follows. | `TargetDesignation.cs` | yes, named in `Sights.Tkn4s.cs`, `Sights.cs` |
| Reticles | Reticles built from text: the elements, expressions, meshes and the zoom reticles. | `PortReticles.cs`, `ReticleSpec.cs`, `ReticleSpec.Elements.cs`, `ReticleSpec.Expressions.cs`, `ReticleSpec.Json.cs`, `ReticleBuild.cs`, `ReticleMeshes.cs`, `ReticleRangeLine.cs`, `ZoomReticles.cs`, `GuidanceBeam.cs` | yes, named in `Reticles.cs`, `Sights.cs` |
| Main gun and rounds | The gun's values, its own rounds built from vanilla ones (its own HEAT throwing a full fragment cone into the tank it enters), the ammunition menu and the reload rules. | `GunSetup.cs`, `PortAmmo.cs`, `BehindArmour.cs`, `PortAmmo.Menu.cs`, `PortAmmo.Models.cs`, `PortAmmo.Spawn.cs`, `ReloadRules.cs`, `BeltShape.cs` | yes, named in `Conversion.cs`, `Firepower.cs`, `Mod.cs` |
| Reduced load | Racks left empty at spawn, by a preset or by name; the load then totals the racks kept, and the autoloader always stays loaded. | `PortAmmo.Load.cs` | yes, named in `Firepower.cs` |
| Random APFSDS | Each tank rolls its own APFSDS, and with a HEAT choice its own HEAT, from a weighted pool outside the campaign; the mission customizer's ammo dropdowns get a Random entry. | `PortAmmo.Random.cs` | yes, named in `Firepower.cs`, `Mod.cs`, `SettingText.cs` |
| HEAT choice | A choice of 125 mm HEAT rounds (3BK29M, 3BK31, 3BK18M) in the settings and the mission customizer, with their tandem precursors against add-on and built-in ERA, 3BK31's two main charges as two jets, and the tandem rounds' own impact and recap look. | `Heat125.cs`, `SplitCharge.cs`, `TandemLook.cs` | yes, named in `Firepower.cs` |
| Explosive reactive armour | ERA bricks that go off on a hit, take their neighbours with them and throw their lids, with the blast effect. | `EraBricks.cs`, `Blast.cs` | yes, named in `Armour.cs` |
| Damage-model armour | Armour built from the bundle's damage-model pieces, and the protection presets. | `DmArmour.cs`, `DmArmourApply.cs`, `DmArmourApply.Marker.cs`, `ArmourPresets.cs` | yes, named in `Armour.cs` |
| Modules and compartments | Hit zones, boxes, fuel and crew modules wired into the game's damage code. | `ModuleBuilder.cs`, `ModuleSpec.cs`, `PortModules.cs` | yes, named in `Conversion.cs` |
| Hatches, flaps and the radio whip | Hatches, flaps and the whip, driven by the base vehicle's animation and the rig map. | `HatchRig.cs`, `HatchUndersides.cs`, `RigMap.cs`, `RigBones.cs`, `PortAxes.cs` | yes, named in `Commander.cs`, `Conversion.Looks.cs`, `Conversion.cs`, `Mod.cs` ... |
| Running gear and moving parts | Wheels and track arms that follow the suspension, pivots moved to the new model and the mantlet cover. | `GearFollow.cs`, `GearArc.cs`, `Followers.cs`, `PivotMove.cs`, `MantletCover.cs`, `TrimmedSkin.cs`, `TiltedRing.cs` | yes, named in `Conversion.Looks.cs`, `Conversion.cs` |
| Mobility | Engine, gearbox and speeds set before the controller starts. | `Mobility.cs` | yes, named in `Conversion.cs`, `Drive.cs` |
| Lamps | The base vehicle's lamps moved onto the model's own lamp glasses, with their glow following. | `LampMove.cs` | yes, named in `Conversion.Looks.cs` |
| Tactical numbers | The convoy number plates moved onto a lamp or the side boxes. | `TacticalNumbers.cs` | yes, named in `Conversion.Looks.cs` |
| Crew voices and donor parts | Another vehicle's crew voices, sights and thermal parts kept as inactive copies. | `CrewVoices.cs`, `DonorParts.cs` | yes, named in `Conversion.cs`, `Mod.cs` |
| Interior and visuals | The base interior reshaped for the new model, hidden base parts, paint, materials, LODs and vanilla edits. | `InteriorMap.cs`, `VisualLods.cs`, `VisualParts.cs`, `KeepHidden.cs`, `VehiclePaint.cs`, `PaintFinish.cs`, `MaterialRoles.cs`, `VanillaEdits.cs`, `VanillaEditsApply.cs`, `VanillaMaterials.cs`, `RepairKitCompat.cs`, `NameShield.cs` | yes, named in `Armour.cs`, `Conversion.Looks.cs`, `Conversion.cs`, `Mod.cs` |
| Base rigs | The bone paths and hidden parts of the vanilla vehicle the model is built on. | `BaseRigT72M1.cs`, `BaseRigT80B.cs`, `BaseRigM60A1.cs` | yes, named in `Armour.cs`, `Conversion.Looks.cs`, `Conversion.cs` |
| Showcase and menu scenes | The showcase spawner and the menu scene names. | `ShowcaseSpawner.cs`, `MenuScenes.cs` | yes, named in `Mod.cs` |

## Plumbing every port uses

Settings, logging, the unit registry, the bundle text readers and the like: always on.

`BuildInfo.cs`, `BundleAssets.cs`, `BundleText.cs`, `ConversionSteps.cs`, `ConvertSweep.cs`, `EmbeddedFile.cs`, `Hashing.cs`, `LivePath.cs`, `MiniJson.cs`, `ModRandom.cs`, `PatchClasses.cs`, `PatchGuard.cs`, `PortSettings.cs`, `PortText.cs`, `Reflect.cs`, `UnitRegistry.cs`, `UnityErrors.cs`, `UnityKeys.cs`, `UpdateCheck.cs`, `V3.cs`
