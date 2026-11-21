# Maintaining the T-80UM2 mod

For someone who knows C# but hasn't done GHPC modding. The mod is a MelonLoader DLL that adds the T-80UM2 as its own
unit: a copy of the game's T-80B that gets converted when it spawns. Model, textures and armour meshes come from an
asset bundle, everything else is the code and data here.

If the log fills with warnings after a game update, go to [docs/game-updates.md](docs/game-updates.md).

## Building

You need Windows (GHPC is Windows-only and the tools here are PowerShell scripts), the .NET SDK (6 or newer, online once
for the 4.7.2 reference assemblies; the CI tests run on SDK 8) and GHPC with MelonLoader 0.7.3 or newer, because the
project references the game's DLLs. Start the game once after installing MelonLoader so it sets itself up:
`tools\check-game.ps1` reads its Mono.Cecil.

```
dotnet build -c Release -p:GameDir="<the game's Bin folder>"
```

`GameDir` is the game's `Bin` folder, the one with `GHPC.exe`, `MelonLoader` and `Mods` in it (not the folder above it).
It defaults to the usual Steam path. The DLL ends up in `bin\Release\T80UM2.dll`. Copy it to `Mods\T80UM2.dll` in the
game.

The bundle (model, textures, armour meshes) is not in this repo and is not built from it: take `Mods\T80UM2\t80um2` from
the latest [release zip](https://github.com/marsattackk/GHPC-T80UM2/releases/latest). 0.2.0 shipped a bundle of its own,
but it differs from 0.1.1's only in the normals and UVs of the cupola and the commander's hatch, and 0.3.0's is the
same content packed again (a new hash, no object changed), so this code runs on any of them. Without the bundle's
model the mod loads and the T-80UM2 stays a complete T-80B: nothing of the conversion is applied.

The DLL logs a stamp when it loads, like `T-80UM2 <version>+a1b2c3d.public, game <version>, Steam build <number>`, so a
player's log tells you which build they ran. The version comes from `MelonInfo` in `T80UM2\Mod.cs`, the commit from
`-p:GitCommit=<hash>` (otherwise `unknown`), the flavour from `-p:BuildFlavour` (default `public`).

## Tests

`tests\` covers the bits that don't need the game: the reticle files' expression language, the reticle JSON, MiniJson,
`dm_armour` parsing, the bundle text numbers, version comparison, the settings migrations, the fire gate, the aim clamp,
the Drozd's engagement maths (`ApsMathTests.cs`) and the sector maths that `Common\` shares with the T-90s' Shtora (this
tank doesn't use that one). It compiles those files straight out of `Common\` with stand-ins for MelonLoader, so it
runs without GHPC and GitHub Actions runs it on every push that touches code (Markdown-only ones skip it). It also
reads what ships in `Resources\`: `ShippedTextTests.cs` takes `dm_armour.txt` and fails on a line that doesn't read,
a mode or material the damage model doesn't know, or a piece count other than the one at its top (change `Pieces`
there when you add or remove a piece on purpose; it is 184 now), and `ShippedReticleTests.cs` does the reticles.
`CommonSealTests.cs` checks `Common\` against `Common\STAMP.txt`. CI never builds the mod itself, because that needs
the game's DLLs, so build it against your own copy and run `tools\check-game.ps1` before a pull request.

```
dotnet test tests
```

The two checks run from the repo's folder in PowerShell. `check-game.ps1` builds the mod itself into a temporary folder
(no build is needed first), then looks every game hook of the built DLL up in the game's own DLLs, so it needs the game
with MelonLoader started once (it uses MelonLoader's Mono.Cecil). If the game has never been started it stops with "No
Mono.Cecil.dll under <the game's Bin folder>\MelonLoader. Start the game once so MelonLoader sets itself up."
`check-settings.ps1` only reads the source and the two settings tables; it builds nothing.

```
powershell -ExecutionPolicy Bypass -File tools\check-game.ps1 -GameDir "<the game's Bin folder>"
powershell -ExecutionPolicy Bypass -File tools\check-settings.ps1
```

A second workflow runs `tools\check-settings.ps1` on every push and pull request, Markdown-only ones included, because
the tests skip those and a README edit is the usual way for the settings tables to drift from the code.

The reticle language is described in [docs/reticles.md](docs/reticles.md), and `ExpressionTests.cs` pins it: read both
before changing `ReticleSpec.Expressions.cs`. Every reticle in `Resources\` is also expanded and compared line by line
with `tests\expanded\`. After changing a reticle on purpose, write the expansions again with this one line and check the
diff is what you meant:

```
$env:T80UM2_UPDATE_EXPANDED = "1"; dotnet test tests; Remove-Item Env:T80UM2_UPDATE_EXPANDED
```

While the variable is set the test rewrites the files and passes whatever they hold, which is why the line clears it.

## Layout

```
T80UM2\     the tank itself
Common\     the stamped, sealed code shared with my other ports
Resources\  data embedded in the DLL, file by file in its README.md
tools\      check-game.ps1 with check-game-list.txt, check-settings.ps1, verify-release.ps1
.github\    the issue forms and the two workflows
BuildStamp.targets, LICENSE
tests\      the tests above, a project of their own
docs\       game-updates.md, bundle.md, reticles.md and shared-features.md for you; armour.md, screens.md and details.md for
            players; the images
```

`T80UM2\` is one file per job, some split into parts:

- `Mod.cs` (`T80UM2Mod`) is the entry point: the settings, the data load and the unit's registration. The settings' descriptions are in
  `SettingText.cs`.
- `Conversion.cs` does the converting, calling the rest: the `Steps` table at spawn, the `LateSteps` table two frames later,
  and the interior map and the modules it fits. `Conversion.Looks.cs` is what the model shows beyond its body: the T-80B's
  number plates and lamps on the War Thunder lamps, the hatch undersides, the canvas mantlet cover, the distance-level
  materials.
- `Armour.cs` loads the armour values, the vanilla edits and the ERA links (`Armour.Load`), applies the
  damage-model armour (the «Контакт-5» ERA, my own figures, the T-80B's volumes and fuel tanks reshaped to the T-80UM2's)
  and makes the `Armour preset` setting.
- `Firepower.cs` is the 2А46М-1: the rounds, the 40-round loadout (28 in the carousel, 12 by hand), the 9М119М beam-rider on the T-80B's guidance
  unit, the coax.
- `Drive.cs` is the engine, final drive, drag and mass, values only, set by the shared `Mobility`.
- `Countermeasures.cs` is the 902Б «Туча» tubes with the 3Д17 grenades (the shared screen with the Shtora's heads off).
  `Countermeasures.Drozd.cs` (`Drozd`) is the «Дрозд»: four launcher blocks, two radar heads, 8 rounds, the sectors and the threat
  kills, on the shared `Aps`.
- `Sights.cs` fits the sights on the T-80B's own optics (`Sights.Fit`, a late step, two frames after spawn) and holds the 1А45's settings, with
  `Sights.1G46.cs` (the gunner's 1Г46 day sight), `Sights.Agava2.cs` (the «Агава-2» thermal), `Sights.Night.cs` (the ТПН-4
  «Буран-ПА» and the switch between the two night sights) and `Sights.Tkn4s.cs` (the commander's ТКН-4С with «Дубль» and
  target designation). `Reticles.cs` is the shared reticle handle.
- `Commander.cs` is the commander's НСВТ «Утёс» on its pedestal: the gun, its belt and cases, the mount's controls and the
  hit zones. The commander lays and fires it from under armour, and his hatch never opens. `Commander.K10T.cs` (`K10Window`) is the
  К10-Т collimator he aims it with. `Commander.Link.cs` keeps the gun to the player's own vehicle and holds the `Climbs` switch,
  which is `false`: the seat, pose and climb that would put him on the roof are set up at the end of `Commander.FitStation`
  on every spawn and never used.

`tools\verify-release.ps1` rebuilds a tag and compares it with a release DLL or zip (the last step of Releasing).
[docs/shared-features.md](docs/shared-features.md) lists the features in `Common\` and whether the tank switches each
on.

Where to change things:

| What | Where |
|---|---|
| Tunable numbers | `Firepower.cs` (gun, rounds, mix), `Drive.cs` (engine and gears), `Sights.cs` and its parts (rangefinder, fields of view, limits), `Commander.cs` (НСВТ), `Countermeasures.Drozd.cs` («Дрозд») |
| Crew places, rack slots, the carousel | `Resources\interior_map.txt` |
| Module health and hit boxes (APU, sights, «Дрозд» blocks and radar heads) | `Resources\modules_built.json` |
| Gun depression over the rear deck | `Resources\gun_depression.txt` |
| A round's shape or hit zone | `Resources\round_meshes.txt` and its `.mesh` files |
| A reticle | its `*.jsonc` in `Resources\`, in the language of [docs/reticles.md](docs/reticles.md) |
| A setting's description | `SettingText.cs`, except the ones `Common` makes: `Add to campaign roster`, `Campaign prevalence` and `Campaign unlock after missions` (`UnitRegistry.cs`), `Weathered paint` (`PaintFinish.cs`) and `Check for updates` (`UpdateCheck.cs`) |
| Armour values, hatches, glass, ERA, smoke points | the text files in `Resources\` |

The formats of the four data files above are at the end of [Resources\README.md](Resources/README.md), the others' in
[docs/bundle.md](docs/bundle.md).

Every method named `Dev...` is an empty partial method, a hook for my own test builds. That code isn't in this repo, so
ignore the hooks. The compiler removes the declaration and every call to it, so a normal build contains nothing of them.
This lists them: `Select-String -Path T80UM2\*.cs -Pattern "partial void Dev"`. You can delete one too. A hook with a
`ref` argument goes together with the local it sets.

`Common\` is the machinery, fed by settings objects from `T80UM2\`, so the T-80UM2's numbers stay in `T80UM2\`. It is
shared by my GHPC mods and stamped into each repo, so it is never edited here: the same file is the same wherever it
appears, and each tank gets the subset it needs, so it holds code the T-80UM2 doesn't use.
[docs/shared-features.md](docs/shared-features.md) says which features those are and which the tank switches on. It is
sealed: `Common\STAMP.txt` lists a hash for every file and `tests\CommonSealTests.cs` fails, naming the file, when one
differs, so a pull request that edits a `Common\` file fails that test by design. If something there needs changing, open
an issue or a pull request that says what and why. The text of it is the proposal. I make the change in the copy my mods
share (not in this repo) and put it into each of them. The commit hash in `STAMP.txt` is that copy's, so you can't look
it up.

A fork of your own is different: change `Common\` as much as you need. The seal test then fails, naming the file and
printing its new SHA-256, and putting that hash on the file's line in `Common\STAMP.txt` makes it pass again. Anything
you'd like in my repo still goes as an issue or pull request, because it has to go into the shared copy.

One trade-off comes with that. Each port compiles `Common\` into its own DLL under its own namespace, so a player who
installs the T-90A, T-90M, Leclerc SXXI and T-80UM2 together gets one copy of every Harmony patch per port. A copy only
acts on its own vehicles and returns early when the instance isn't one of them, so what the extra copies cost is a
lookup per call. The few things the copies have to share go through `AppDomain` data under a versioned key, and hold only
game and mscorlib types, because a type from one DLL isn't the same type to another. If you change the shape of one, bump
the version suffix in every port. The Drozd's claim table is one of them (`"GHPCMods.Aps.Claims.v1"`), and it's why a
T-90M's Arena-M nearby never fires at a round the Drozd has taken.

`Resources\` is embedded under each file's plain name (one wildcard line in `T80UM2.csproj`) and read with
`GetManifestResourceStream`: the reticles (`*.jsonc`), the round textures and meshes, the НСВТ's belt round, the trimmed
T-80B wheel mesh, the module, interior and gun depression data, and the text files that used to sit in the bundle (armour
values, hatches, glass roles, ERA links, smoke points).

The bundle's contents and formats are in [docs/bundle.md](docs/bundle.md).

## What happens when a T-80UM2 spawns

At game start `T80UM2Mod.OnInitializeMelon` makes the settings (`CreateSettings`), and with `Enabled` off that's all it
does. Nothing is patched either, because `[assembly: HarmonyDontPatchAll]` stops MelonLoader patching at load and
`PatchClasses.Apply` only runs after the check. Otherwise it patches, opens the bundle and reads the text data
(`LoadData`) and registers a unit (`RegisterUnit`) `T80UM2` based on the `T80B` with the
`T80B`'s army. `UnitRegistry` copies the T-80B's entry under that name so Instant Action, the customiser and the campaign
offer it, and re-adds it whenever a scene or spawner turns up without it. The campaign roster is the Soviet one, taken
from `CampaignConfig` (`Add to campaign roster`, `Campaign prevalence`, `Campaign unlock after missions`) and redone at
every scene load.

At spawn, `SpawnPatch` (in `Common`) on `UnitSpawner.SpawnUnit` renames the unit and calls `Conversion.Convert`. That works
through the `Steps` table in `Conversion.cs`, and then starts `LateSetup`, which runs the `LateSteps` table two frames later.
The spawn steps, in the order they run:

| # | Step | What it does |
|---|---|---|
| 1 | base visuals | hides the T-80B's own hull, turret, gun, skirts and stowage; the running gear, tracks and lamps stay |
| 2 | drive | engine, gearing, mass and speed caps; before the chassis' `Start`, which reads them |
| 3 | visuals | hangs the War Thunder model's hull, turret, gun and mantlet cover on the T-80B rig's bones |
| 4 | paint | the camo-painted atlases onto the game's vehicle shader, the rest onto the thermal-aware Standard; weathered or clean by `Weathered paint` |
| 5 | glass materials | the game's own glass and lens materials onto the model's |
| 6 | running gear | the kept T-80B running gear without its fender and mud flaps |
| 7 | mantlet cover | the canvas cover skinned between turret and gun, so it stretches with elevation |
| 8 | tactical numbers | the number plates on the turret-rear march lamp |
| 9 | lamps | the T-80B's lights onto the War Thunder lamps |
| 10 | hatch undersides | the hatch lids' undersides, plain, then painted from the turret's atlas |
| 11 | LOD materials | the distance level gets the parts' final materials |
| 12 | crew voices | marks the T-80B's crew and the autoloader for its voices |
| 13 | smoke launchers | the T-80B's grenade slots onto the model's tubes, and the tube caps |
| 14 | armour | War Thunder's damage model with my values in place of the T-80B's outer armour |
| 15 | interior | crew, ammo slots, carousel, engine box and rack fire markers onto the T-80UM2's places |
| 16 | modules | builds the port's modules (`SpawnOnly`) |
| 17 | ammo setup | the rounds, racks, stowage counts and missile guidance (`SpawnOnly`) |
| 18 | coax setup | the coax belts (`SpawnOnly`) |
| 19 | gun setup | the 2А46М-1's limits, depression profile, autoloader and HUD name (`SpawnOnly`) |
| 20 | smoke grenade | the 3Д17 on the tubes' slots (`SpawnOnly`) |
| 21 | hatches | the lids onto their hinges, following the T-80B's hatch animations |
| 22 | turret axis | the turret ring, trunnion and muzzle at the real places, the turret turned about its tilted ring |
| 23 | Drozd | the active protection, when `Drozd` is on (`SpawnOnly`) |
| 24 | NSVT | the commander's machine gun (`SpawnOnly`) |
| 25 | commander's lid | the lid pinned shut whatever opens the T-80B's hatch (`Sights.Apply`) |

The order is a rule, because each step builds on the one before. The drive comes before the visuals because the chassis
reads it in its own `Start`. The looks, the armour and the rounds go on with the turret still at rest, so they come before the
turret axis moves the pivots, and the «Дрозд» goes right after the pivots with the turret still at rest, before anything
turns it. The interior map comes after the armour, whose edits it keeps clear of, and before the racks fill. The hatches come
after any bone has moved. The НСВТ comes after the «Дрозд» and before the lid is pinned.

Two frames after the spawn `LateSetup` runs the `LateSteps`, which need the components' `Start` to have run:

| # | Step | What it does |
|---|---|---|
| 1 | smoke crew refresh | the smoke launcher's crew cache, which a spawned unit filled before the crew existed |
| 2 | sight fitting | `Sights.Fit` finds the T-80B's 1G42 and TPN-3-49 and fits the 1Г46 and 1А45, the night sights, the ТКН-4С with «Дубль» and target designation |
| 3 | NSVT late setup | `Commander.LateSetup`: the gunner's sights for the gun and coax only, the part belt, the crew member (`SpawnOnly`) |
| 4 | module wiring | the modules' hit reports and status lines, after the sights because the module hit zones name the views (`SpawnOnly`) |
| 5 | thermal heat | the thermal sights' heat on the port's own parts |
| 6 | ammunition self-check | the shared check that the round models and the stored rounds are right (`SpawnOnly`) |

The order here is a rule too: the НСВТ's late setup and the module wiring need the sights fitted first. One row waits longer
than the table: the racks are filled some frames after `LoadoutManager.Start`, so the self-check starts a short coroutine and
runs 28 frames after the table (30 after the spawn). Every other row runs at the two-frame mark.

Each row runs on its own. `ConversionSteps.Run` (in `Common`) catches a step that throws and logs
`T-80UM2 <step> failed (<what the player gets instead>): <exception>`, and the rest carry on. A step catches nothing
itself, so that line is the one you see. The exceptions are the self-check's coroutine, which runs after the table is
done, and the НСВТ's drawn belt, which logs `T-80UM2 NSVT belt: ...` and leaves the gun working without it.

The rows marked `SpawnOnly` in both tables only work at spawn. GHPC builds the unit's weapon, camera and rack lists
straight after the spawn and a part added later never makes it in. A T-80UM2 the spawn patch missed is caught at
`PlayerReady` by `ConvertSweep` (in `Common`, started from `OnSceneWasLoaded`) and gets every other step. A unit converted
late gets one warning, from the modules row, the first `SpawnOnly` one:
`T-80UM2: <unit> was converted after it spawned; it keeps the T-80B's ammunition, and has no Drozd or NSVT.`

Parts borrowed from other vehicles (the T-80B's voices and sight sounds, the T-64B's machine-gun station for the НСВТ, the
M60A3's thermal screen for the «Агава-2», the 3Д17's white burst) are copied at the main menu by `DonorParts` (in
`Common`), because GHPC can unload those vehicles between missions.

Private game members are read through `Common\Reflect.cs`, which warns once instead of throwing when a name has gone.
Patches that run every frame report through `Common\PatchGuard.cs` so one throw doesn't kill the game's loop.
`tools\check-game.ps1` checks all of it against a game build without starting the game. If you add a `Reflect` call
whose member name comes from a variable, put the names in `tools\check-game-list.txt` with a `covers` line.

## Settings

MelonPreferences in the `[T-80UM2]` category of `UserData\MelonPreferences.cfg`. The README has the player wording and this
table says where each one lives. `tools\check-settings.ps1` checks both tables against the code. The three campaign
settings, `Weathered paint`, `Check for updates` and the hidden `ConfigVersion` are worded in `Common`.

| Setting | Defined in | Read by |
|---|---|---|
| `Enabled` | `Mod.cs` | everything, off means the mod does nothing |
| `Add to campaign roster`, `Campaign prevalence`, `Campaign unlock after missions` | `Common\UnitRegistry.cs` (`CampaignConfig`), set up in `T80UM2\Mod.cs` | the Soviet campaign roster, re-read at each mission load |
| `APFSDS` | `Mod.cs` | `Firepower`, the default round where the game doesn't pick one |
| `Force APFSDS` | `Mod.cs` | `Firepower` (`ForceApfsds`): the campaign's roll gives the `APFSDS` round |
| `Random APFSDS`, `APFSDS pool` | `Mod.cs` | `Firepower` (`RandomApfsds`, `ApfsdsPool`): each tank rolls its own APFSDS outside the campaign |
| `HEAT` | `Mod.cs` | `Firepower` (`DefaultHeat`), the default HEAT where the game doesn't pick one |
| `Force HEAT` | `Mod.cs` | `Firepower` (`ForceHeat`): the campaign's roll gives the `HEAT` round |
| `Random HEAT`, `HEAT pool` | `Mod.cs` | `Firepower` (`RandomHeat`, `HeatPool`): each tank rolls its own HEAT outside the campaign |
| `Ammunition load`, `Empty racks` | `Mod.cs` | `Firepower` (`LoadPreset`, `EmptyRacks`, the racks' names and `LoadPresets`): racks left empty at spawn |
| `Night sight` | `Mod.cs` | `Sights` (`Agava-2` or `Buran-PA`, changeable in play) |
| `Fire only when the gun is laid` | `Mod.cs` | `Sights`, which hands it to `GunnerFcs` as the firing permission |
| `Weathered paint` | `Common\PaintFinish.cs`, set up in `T80UM2\Mod.cs` | `Conversion` (the paint step) |
| `Drozd` | `Mod.cs` | `Conversion`, off skips the «Дрозд» step |
| `ERA destruction effects` | `Mod.cs` | `Armour`, through `EraBricks.Look` |
| `NSVT spent cases` | `Mod.cs` | `Commander` |
| `Armour preset` | `Common\ArmourPresets.cs`, made through `T80UM2\Armour.cs` (`Armour.Config`, text in `SettingText.cs`) | `Armour` (`game-scale` or `source-only` numbers) |
| `Check for updates` | `Common\UpdateCheck.cs` | asks GitHub once per start, one console line |
| `ConfigVersion` | `Common\PortSettings.cs`, set up in `T80UM2\Mod.cs` | hidden, the version that last wrote the section |

Most only apply to T-80UM2s spawned after the change. The night sight and `ERA destruction effects` are read live, the
second each time a brick goes off. Two catches when you add or change a setting:

- MelonLoader writes the old default into the player's file and keeps it. To move players to a new default, add a
  `SettingsMigration` to `Migrations` in `Mod.cs` (none so far). It only changes a value still at the old default, once.
- A decimal-comma Windows can leave `"0,3"` in the file, which MelonLoader quietly ignores. `PortSettings.Finish` reads
  it anyway, so call `Finish` after all the entries exist.

## Releasing

The armour numbers, hatches, glass, ERA links and smoke points are text in `Resources\`, so changing them is a rebuild of
the DLL and nothing else. The bundle only has to change with the model, and that needs the Unity project, which isn't in
this repo. Renaming an armour piece or a visual node is a model change too. [docs/bundle.md](docs/bundle.md) says which
edits are safe.

1. Bump the version in `MelonInfo` at the top of `T80UM2\Mod.cs`. `Repo` next to it is what the update check asks.
   The version lives only there: the README doesn't carry one. Add the release's row to the hash table in
   [docs/bundle.md](docs/bundle.md) in the same commit: the new bundle's hash if it changed (it is known before the
   build), the last row's again if it didn't. The check for that table is in the same file. Commit.
2. Run the checks on that commit, from the repo's folder: `dotnet test tests`,
   `tools\check-game.ps1 -GameDir "<the game's Bin folder>"` and `tools\check-settings.ps1` (the commands are under
   Tests). All three have to pass before anything is tagged.
3. Get the commit's 7-character short hash with `git rev-parse --short=7 HEAD` and build with it:
   `dotnet build -c Release -p:GameDir="<the game's Bin folder>" -p:GitCommit=<that hash>`. The hash goes into the DLL's
   stamp.
4. Install it, start a mission with a T-80UM2 in it (Instant Action, or the Showcase) and read `MelonLoader\Latest.log`:
   the stamp first, then no warnings or errors from the mod.
5. Zip it as `T80UM2_<version>.zip` with this layout inside, forward slashes: `Mods/T80UM2.dll` and `Mods/T80UM2/t80um2`
   (the bundle from the latest release). In a folder that holds that `Mods` folder:
   `tar -a -c -f T80UM2_<version>.zip Mods`.
6. Tag the built commit `v<version>` and push the tag. On GitHub create a release from the tag: attach the zip, and write
   the release notes, what changed for the player, a line or two each.
7. Check the result: `powershell -ExecutionPolicy Bypass -File tools\verify-release.ps1 -Tag v<version> -Release
   T80UM2_<version>.zip -GameDir "<the game's Bin folder>"` rebuilds the tag in a temporary folder with the commit the
   release's own stamp carries. It needs the game's `Bin` folder, from `-GameDir` or `GHPC_GAME_DIR`. Expect IDENTICAL:
   a release built from its tag rebuilds byte for byte. DIFFERENT means the SDK or the source differs from what built
   the release, and then you compare the decompiled code.
