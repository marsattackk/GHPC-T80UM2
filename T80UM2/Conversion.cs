using System;
using System.Collections;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// Turns a spawned T80B into a T-80UM2, step after step in the order of Steps, then two frames later the steps of
	// LateSteps, which need the components' Start to have run. At spawn the armour, the rounds, the Drozd and the НСВТ go on
	// with the turret at rest; a unit converted later (another mod's spawner, caught at PlayerReady) gets the model, paint,
	// armour and sights without the parts that need that moment.
	internal static partial class Conversion
	{
		// The T80B rig's bones; the bundle's group meshes are built relative to them.
		static readonly (string group, string path)[] Bones =
		{
			("HULL", "T80B_rig/HULL"),
			("TURRET", "T80B_rig/HULL/TURRET"),
			("GUN", "T80B_rig/HULL/TURRET/gun"),
			("MANTLET_COVER", "T80B_rig/HULL/TURRET/gun")   // the canvas cover, skinned by the "mantlet cover" row
		};
		// Bundle material names of the camo-painted atlases.
		static readonly string[] Painted =
		{
			"t80um2_era_kontakt_5_t_80u",
			"t80um2_t_80u_gun",
			"t80um2_t_80u_turret",
			"t80um2_mg_mount_nsvt_t_80u",
			"t80um2_t_80u_body",
			"t80um2_t_80um2_turret"
		};

		// The crew, ammo slots, carousel, engine box and rack fire markers at the T-80UM2's places, and the port's modules;
		// both are embedded in the DLL.
		internal static readonly InteriorMap.Embedded Interior = new InteriorMap.Embedded(
			typeof(T80UM2Mod).Assembly,
			T80UM2Mod.FriendlyName,
			"T80UM2",
			"T-80B");

		internal static readonly PortModules Modules = new PortModules(
			typeof(T80UM2Mod).Assembly,
			T80UM2Mod.FriendlyName,
			"T-80B",
			"T80UM2");

		// What the steps share.
		sealed class Parts
		{
			internal Vehicle Vehicle;
			internal Transform Root;
		}

		// The steps in the order they run (the order is a rule: each one builds on the one before). Every row runs on its own:
		// a step that throws logs its name and what the player gets instead, and the steps after it still run.
		static readonly ConversionStep<Parts>[] Steps =
		{
			new ConversionStep<Parts> { What = "base visuals", Otherwise = "the T-80B's body stays drawn", Run = HideBase },
			// Before the chassis' Start.
			new ConversionStep<Parts> { What = "drive", Otherwise = "the T-80B's drive stays", Run = SetMobility },
			new ConversionStep<Parts> { What = "visuals", Otherwise = "no T-80UM2 model is drawn", Run = AttachVisuals },
			new ConversionStep<Parts> { What = "paint", Otherwise = "the hull stays on the bundle's material", Run = Paint },
			new ConversionStep<Parts> { What = "glass materials", Otherwise = "the bundle's glass stays", Run = ApplyGlass },
			// The next five before PortAxes moves the pivots: their places are prefab space with the turret at rest.
			new ConversionStep<Parts>
			{
				What = "running gear",
				Otherwise = "the T-80B's fender flaps may show through the fenders",
				Run = FitRunningGear,
			},
			new ConversionStep<Parts> { What = "mantlet cover", Otherwise = "the mantlet cover is not skinned", Run = SkinMantletCover },
			new ConversionStep<Parts> { What = "tactical numbers", Otherwise = "no number plates", Run = PlaceNumbers },
			new ConversionStep<Parts> { What = "lamps", Otherwise = "the T-80B's lights stay where they were", Run = PlaceLamps },
			new ConversionStep<Parts> { What = "hatch undersides", Otherwise = "left plain", Run = PaintHatchUndersides },
			// After the paint, the glass and the undersides: the distance level gets the parts' final materials.
			new ConversionStep<Parts>
			{
				What = "LOD materials",
				Otherwise = "a distant T-80UM2 shows the bundle's materials",
				Run = SyncLods,
			},
			new ConversionStep<Parts> { What = "crew voices", Otherwise = "the crew keeps its default voices", Run = MarkCrewVoices },
			new ConversionStep<Parts> { What = "smoke launchers", Otherwise = "the T-80B's smoke tubes stay", Run = SeatSmoke },
			// From here the order matters: armour and rounds go on with the turret at rest, before the axes move the pivots.
			new ConversionStep<Parts> { What = "armour", Otherwise = "the T-80B's armour stays", Run = ApplyArmour },
			// After the armour (the interior map's moves keep clear of the port's edits), before the racks are filled.
			new ConversionStep<Parts> { What = "interior", Otherwise = "the T-80B's crew places and racks stay", Run = FitInterior },
			// The НСВТ gun's hit zone rides its cradle: built with the station.
			new ConversionStep<Parts>
			{
				What = "modules",
				Otherwise = "the T-80B's kept",
				Run = BuildModules,
				SpawnOnly = true,
				Late = $"{T80UM2Mod.FriendlyName}: {{0}} was converted after it spawned; it keeps the T-80B's ammunition, "
					+ "and has no Drozd or NSVT.",
			},
			new ConversionStep<Parts>
			{
				What = "ammo setup",
				Otherwise = "it keeps the T-80B's ammunition",
				Run = FitRounds,
				SpawnOnly = true,
			},
			new ConversionStep<Parts>
			{
				What = "coax setup",
				Otherwise = "the coax keeps the T-80B's belt",
				Run = FitCoax,
				SpawnOnly = true,
			},
			new ConversionStep<Parts>
			{
				What = "gun setup",
				Otherwise = "the T-80B's gun speeds and limits stay",
				Run = FitGun,
				SpawnOnly = true,
			},
			// After the T-80B's 8 slots sit on the 902Б tubes and have their caps ("smoke launchers").
			new ConversionStep<Parts>
			{
				What = "smoke grenade",
				Otherwise = "the T-80B's 3D6 grenade stays",
				Run = FitSmokeGrenade,
				SpawnOnly = true,
			},
			// After any bone moves.
			new ConversionStep<Parts> { What = "hatches", Otherwise = "the hatch lids stay shut", Run = MoveHatches },
			new ConversionStep<Parts> { What = "turret axis", Otherwise = "the turret turns on the T-80B's pivots", Run = MoveAxes },
			// Right after the pivots, the turret still at rest.
			new ConversionStep<Parts>
			{
				What = "Drozd",
				Otherwise = "the tank has no Drozd",
				Run = FitDrozd,
				SpawnOnly = true,
			},
			new ConversionStep<Parts>
			{
				What = "NSVT",
				Otherwise = "the commander has no machine gun",
				Run = FitCommander,
				SpawnOnly = true,
			},
			// After the НСВТ. The sights' views are fitted by the late step "sight fitting".
			new ConversionStep<Parts> { What = "sight setup", Otherwise = "the commander's lid may open", Run = FitSights },
		};

		// The steps that need the components' Start to have run, two frames after the spawn, in this order. Every row runs on
		// its own, as in Steps.
		static readonly ConversionStep<Vehicle>[] LateSteps =
		{
			// A spawned unit's launcher keeps its empty crew cache and never passes GHPC's crew check unless it's refreshed.
			new ConversionStep<Vehicle>
			{
				What = "smoke crew refresh",
				Otherwise = "the smoke launcher may not fire",
				Run = Countermeasures.RefreshCrew,
			},
			// 1Г46 / 1А45, ТПН-4, ТКН-4С with «Дубль» and ЦУ, once the base optics have started.
			new ConversionStep<Vehicle>
			{
				What = "sight fitting",
				Otherwise = "the T-80B's sights stay",
				Run = Sights.Fit,
			},
			// After the sights: the gunner's sights take the gun and coax only.
			new ConversionStep<Vehicle>
			{
				What = "NSVT late setup",
				Otherwise = "the НСВТ may not fire, and the gunner's sights may select it",
				Run = Commander.LateSetup,
				SpawnOnly = true,
			},
			// After the sights: the module hit zones name the views.
			new ConversionStep<Vehicle>
			{
				What = "module wiring",
				Otherwise = "hits on the T-80UM2's own modules do nothing",
				Run = WireModules,
				SpawnOnly = true,
			},
			new ConversionStep<Vehicle>
			{
				What = "thermal heat",
				Otherwise = "the T-80UM2's own parts show cold in thermal sights",
				Run = HeatParts,
			},
			// The racks are filled some frames after LoadoutManager.Start: this row waits the rest of its 30 frames itself.
			new ConversionStep<Vehicle>
			{
				What = "ammunition self-check",
				Otherwise = "a missing round model is not reported",
				Run = CheckAmmo,
				SpawnOnly = true,
			},
		};

		// atSpawn: called by the spawn patch, before the racks fill and the unit's lists are built. The PlayerReady sweep converts a
		// unit it missed without what needs that moment: the modules, the rounds, the Drozd and the НСВТ.
		internal static void Convert(Vehicle vehicle, bool atSpawn = false)
		{
			if (T80UM2Mod.Visual == null || vehicle == null) return;
			bool skip = false;
			DevSkip(vehicle, atSpawn, ref skip);
			if (skip) return;
			if (!ConvertSweep.Mark(vehicle)) return;
			var parts = new Parts { Vehicle = vehicle, Root = vehicle.transform };
			Func<ConversionStep<Parts>, bool> keeps = null;
			DevKeeps(ref keeps);
			ConversionSteps.Run(T80UM2Mod.FriendlyName, Steps, parts, vehicle.name, atSpawn, keeps);
			MelonCoroutines.Start(LateSetup(vehicle, atSpawn));
			DevAfter(vehicle, atSpawn);
		}

		static IEnumerator LateSetup(Vehicle vehicle, bool atSpawn)
		{
			yield return null;
			yield return null;
			if (vehicle == null) yield break;
			Func<ConversionStep<Vehicle>, bool> keeps = null;
			DevKeeps(ref keeps);
			ConversionSteps.Run(T80UM2Mod.FriendlyName, LateSteps, vehicle, vehicle.name, atSpawn, keeps);
		}

		// The T-80B's own hull / turret / gun / skirts / stowage / lamps off; running gear and tracks stay.
		static void HideBase(Parts parts)
		{
			BaseRigT80B.HideBaseVisuals(parts.Root, "T80UM2_", keepLamps: true);
			DevBaseHidden(parts.Vehicle);
		}

		// Engine, gearing, caps, mass: before the chassis' Start.
		static void SetMobility(Parts parts)
		{
			Drive.Apply(parts.Vehicle);
		}

		static void AttachVisuals(Parts parts)
		{
			VisualParts.Attach(parts.Root, T80UM2Mod.Visual, "T80UM2_", Bones);
		}

		// Camo-painted atlases on GHPC's vehicle shader (the T-80B's own material as the template), the rest on the
		// thermal-aware Standard (FLIR).
		// Weathered (the bundle's baked maps) or clean ("Weathered paint", PaintFinish).
		static void Paint(Parts parts)
		{
			PaintFinish.Apply(T80UM2Mod.WeatheredPaint);
			DevPaint(parts);
			var template = BaseRigT80B.FindTemplate(parts.Root);
			var flir = Shader.Find("Standard (FLIR)");
			foreach (var part in parts.Root
				.GetComponentsInChildren<Transform>(true)
				.Where(child => child.name.StartsWith("T80UM2_"))
				.ToArray())
				if (part.parent == null || !part.parent.name.StartsWith("T80UM2_"))
					VehiclePaint.ApplyPaint(part.gameObject, template, flir, Painted);
			if (template == null)
				MelonLogger.Warning(
					$"{T80UM2Mod.FriendlyName}: no T-80B paint template found; the hull stays on the bundle's material.");
		}

		// The game's own glass / lens materials on the port's (the bundle's material roles).
		static void ApplyGlass(Parts parts)
		{
			VanillaMaterials.Apply(parts.Root, T80UM2Mod.Roles);
		}

		// The T-80B's own crew, one autoloader "AZ" per carousel turn.
		static void MarkCrewVoices(Parts parts)
		{
			CrewVoices.Mark(parts.Vehicle, T80UM2Mod.FriendlyName);
		}

		// The base vehicle's grenade slots onto the port's tubes, then each tube's cap given to its slot, hidden when that
		// tube fires (the launcher's crew cache is refreshed by the late step "smoke crew refresh").
		static void SeatSmoke(Parts parts)
		{
			SmokeLaunchers.Seat(parts.Vehicle, T80UM2Mod.SmokePoints);
			SmokeCaps.Apply(parts.Root);
		}

		// War Thunder's damage model with the mod's values in place of the T-80B's outer armour.
		static void ApplyArmour(Parts parts)
		{
			Armour.Apply(parts.Vehicle);
		}

		// Crew, ammo slots, carousel, engine box and rack fire markers onto the T-80UM2's places.
		static void FitInterior(Parts parts)
		{
			Interior.Apply(parts.Vehicle);
		}

		static void BuildModules(Parts parts)
		{
			Modules.Build(parts.Vehicle, "NSVT");
		}

		// Rounds, racks, stowage counts and missile guidance (the shared PortAmmo), before LoadoutManager and the feeds start.
		static void FitRounds(Parts parts)
		{
			PortAmmo.ApplyAtSpawn(parts.Vehicle);
		}

		// The coax belts.
		static void FitCoax(Parts parts)
		{
			PortAmmo.ApplyCoaxAtSpawn(parts.Vehicle);
		}

		// Gun limits, depression profile, autoloader and HUD name (Firepower.Gun).
		static void FitGun(Parts parts)
		{
			GunSetup.Apply(parts.Vehicle, Firepower.Gun);
		}

		// The 3Д17 grenade on the 902Б tubes' slots (Countermeasures.cs).
		static void FitSmokeGrenade(Parts parts)
		{
			Countermeasures.FitScreens(parts.Vehicle);
		}

		// Hatch lids onto their War Thunder hinges, following the base vehicle's hatch animations (after any bone moves).
		static void MoveHatches(Parts parts)
		{
			HatchRig.Apply(parts.Root, T80UM2Mod.Rig, "T80UM2_", T80UM2Mod.FriendlyName);
		}

		// The real turret ring, trunnion and muzzle (the rig map's axes, from the War Thunder skeleton): the T-80B pivots
		// moved there, the muzzle point onto the levelled barrel, the turret turned about its tilted ring.
		static void MoveAxes(Parts parts)
		{
			var axes = PortAxes.FromRig(T80UM2Mod.Rig, BaseRigT80B.TurretPivot, BaseRigT80B.GunPivot, T80UM2Mod.FriendlyName);
			if (axes != null) PortAxes.Apply(parts.Vehicle, axes);
			else
				MelonLogger.Warning(
					$"{T80UM2Mod.FriendlyName}: bundle's rig map has no axes; the turret turns on the T-80B's pivots.");
		}

		// The «Дрозд», when the setting is on.
		static void FitDrozd(Parts parts)
		{
			if (!T80UM2Mod.DrozdOn.Value) return;
			Drozd.Apply(parts.Vehicle);
		}

		// The commander's hand-laid НСВТ on its pedestal, laid and fired from under armour.
		static void FitCommander(Parts parts)
		{
			Commander.Apply(parts.Vehicle);
		}

		// The commander's lid pinned shut; the views are fitted by the late step "sight fitting" (Sights.cs).
		static void FitSights(Parts parts)
		{
			Sights.Apply(parts.Vehicle);
		}

		// The modules (built at spawn by "modules") wired by hand: GHPC's owners subscribe to their hit zones once. The Drozd's
		// station binds its blocks and heads by id on its own.
		static void WireModules(Vehicle vehicle)
		{
			Modules.Wire(vehicle);
		}

		// The thermal sight's heat on the port's parts (ThermalHeat: the T-80B's own HeatSource value on every T80UM2_ part,
		// then every HeatSource re-collected), once GHPC's own have started.
		static void HeatParts(Vehicle vehicle)
		{
			ThermalHeat.Apply(vehicle.transform, ThermalHeat.PortParts(vehicle.transform, "T80UM2_"), T80UM2Mod.FriendlyName);
		}

		// The shared ammunition self-check once the racks are filled, 30 frames after the spawn (28 more than the late steps
		// wait); silent when all is right.
		static void CheckAmmo(Vehicle vehicle)
		{
			MelonCoroutines.Start(CheckAmmoLater(vehicle));
		}

		static IEnumerator CheckAmmoLater(Vehicle vehicle)
		{
			for (int i = 0; i < 28; i++) yield return null;
			try { PortAmmo.SelfCheck(vehicle); }
			catch (Exception exception)
			{
				MelonLogger.Error(
					$"{T80UM2Mod.FriendlyName} ammunition self-check failed (a missing round model is not reported): " + exception);
			}
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevKeeps(ref Func<ConversionStep<Parts>, bool> keep);
		static partial void DevKeeps(ref Func<ConversionStep<Vehicle>, bool> keep);
		static partial void DevSkip(Vehicle vehicle, bool atSpawn, ref bool skip);
		static partial void DevBaseHidden(Vehicle vehicle);
		static partial void DevPaint(Parts parts);
		static partial void DevAfter(Vehicle vehicle, bool atSpawn);
	}
}
