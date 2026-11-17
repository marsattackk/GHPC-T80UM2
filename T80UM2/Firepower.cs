using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The T-80UM2's main gun, rounds, loading mechanism stowage and coax: values only, set at spawn by the shared GunSetup and
	// PortAmmo. The sights, the reticles and the missile's beam are in Sights, the НСВТ in Commander, the «Дрозд» in
	// Countermeasures.Drozd.
	//
	// The 9М119М is a laser beam-rider: the missile flies onto its guidance unit's AimElement line. The T-80B already has a
	// guidance unit (for its radio-command 9M112): I keep it, with its hit zone and feed wiring (PortAmmo BeamOnBaseUnit), and
	// turn its AimElement into the shared GuidanceBeam on the fire control's day sight. The beam reads that main optic every
	// frame, so moving the 1Г46 changes nothing here.
	internal static partial class Firepower
	{
		// The real loadout is 45 = 28 in the МЗ conveyor + 17 hand-stowed: «В конвейере механизма заряжания находится 28
		// выстрелов, общий боекомплект 45 выстрелов». The T-80B has two racks, the carousel (28) and the hull wet rack (7). The
		// interior map gives the wet rack 10 more slots on War Thunder's places outside the carousel: 7 in the driver's
		// compartment tank-rack and 10 in the fighting compartment («7 снарядов и зарядов в отделении управления, 10 других — в
		// боевом отделении», low-medium confidence). I carry 40, 5 fewer than the sourced 45. The wet rack keeps 5 of the added
		// slots (12 in all): the T-80B's documented places and the turret rear take them clear of the crew, and the 5 that sat
		// round the gunner's head (the inferred turret left-wall rack) are not carried.
		internal const int TotalRounds = 40;
		internal const int ConveyorRounds = 28;
		internal const int PktBelt = 250;   // the ПКТ's belt

		// The mix by round (APFSDS, HEAT, HE-Frag, ATGM) is a gameplay default of mine, not sourced (no regulated mix found): the
		// T-80B's 18 / 7 / 8 / 2 of 35 scaled to 45, then the 5 dropped taken from the wet rack (3 APFSDS, 1 HEAT, 1 HE).
		internal static readonly PortAmmo.Mix Counts = new PortAmmo.Mix { Apfsds = 19, Heat = 9, He = 8, Missile = 4 };
		// Conveyor 14 / 6 / 6 + 2 fixed missile slots (26, 13) = 28; wet rack 5 / 3 / 2 + 2 fixed missile slots = 12 (a gameplay
		// split of mine, not sourced). The missile entry is 0: the fixed slots are taken off separately.
		// What the other tanks of this family call the carousel counts: the source says «конвейер».
		internal static readonly PortAmmo.Mix ConveyorCounts = new PortAmmo.Mix { Apfsds = 14, Heat = 6, He = 6 };
		internal static readonly PortAmmo.Mix WetRackCounts = new PortAmmo.Mix { Apfsds = 5, Heat = 3, He = 2 };

		// Campaign weights against the 3BM42's 1: a gameplay setting of mine, not sourced. The 3BM42 is the default.
		internal const float SvinetsPrevalence = 0.5f;

		internal static PortAmmo.Settings Settings() => new PortAmmo.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			UnitKey = T80UM2Mod.UnitKey,
			Prefix = "t80um2",
			SourceUnit = "T80B",   // the vanilla T-80B's ammo list holds 3BM32, 3BK18M, 3OF26 and 9M112M
			// APFSDS: the 3BM42 «Манго», the serial T-80U's (the «Манго» is in the T-80U's gun description), and the long «Свинец»
			// rods 3BM59 / 3BM60. The loading mechanism was reworked for the 735-740 mm rods on the T-80БВМ («доработан ...
			// механизм заряжания под боеприпасы 3БМ59 "Свинец-1", 3БМ60 "Свинец-2"», TASS 2018); no source says the UM2 got it,
			// so that is my inference. The rounds have names of their own (GHPC compares rounds by name: the T-90s'
			// "3BM60 Svinets-2" would follow their lists), values as the T-90M's.
			Apfsds = new[]
			{
				new PortAmmo.Round
				{
					Id = "3BM42",
					Name = "3BM42 Mango APFSDS",
					Source = "clip_3BM32",
					Texture = "round_3bm42_co.png",
					Atlas = "125mm_co",
					Set = ammo =>
					{
						// Derived: 230 mm at 60 deg at 2000 m (460 LOS), divided by f 0.796 of this drag.
						ammo.RhaPenetration = 578f;
						ammo.Mass = 4.85f;   // flight mass without sabot
						ammo.MuzzleVelocity = 1700f;   // «Начальная скорость снаряда, м/с 1700»
						ammo.Coeff = 0.16f;   // GHPC's 3BM32 drag (same mass and velocity in the game), game data
					},
					Prevalence = 1f,   // the default, weight 1: a gameplay setting
				},
				new PortAmmo.Round
				{
					Id = "3BM59",
					Name = "3BM59 Svinets-1 APFSDS",
					Source = "clip_3BM32",
					Texture = "round_3bm59_co.png",
					Atlas = "125mm_co",
					// The 3BM60's 735 mm outline: the same 3ВБМ22 / 23 family and the same 4Ж96 charge; I found no drawing of its
					// own, so this is inferred.
					Shape = "3BM60",
					Set = ammo =>
					{
						// No official figure: the 3BM60's official figure scaled by the two rounds' ratio at 0 deg in the same
						// sources. Defense Express has 600 / 725 (3БМ59 «до 600», 3БМ60 «700-750») and TASS 720 / 815 («700 - 740»,
						// «800 - 830» «по разным данным»): 0.83 / 0.88, mean 0.86. 693 x 0.86 = 596 at the muzzle, 516 mm LOS (258
						// at 60 deg) at 2000 m. Inferred, low confidence.
						ammo.RhaPenetration = 693f * 0.86f;
						// The rod: «Вес сердечника - 4,5 кг», 660 x 22 mm, one list for 3BM59 / 3BM60 together (tungsten at 17.6
						// g/cm3 would be 4.4 kg), low-medium confidence.
						ammo.Mass = 4.5f;
						// As the 3BM60's official 1660 (same 4Ж96 charge, «до 1700 м/с» for both): inferred.
						ammo.MuzzleVelocity = 1660f;
						// Derived: 3.5 % velocity loss per 1000 m with this mass and area (the same list), low confidence.
						ammo.Coeff = 0.093f;
					},
					Prevalence = SvinetsPrevalence,
				},
				new PortAmmo.Round
				{
					Id = "3BM60",
					Name = "3BM60 Svinets-2 APFSDS",
					Source = "clip_3BM32",
					Texture = "round_3bm60_co.png",
					Atlas = "125mm_co",
					Shape = "3BM60",
					Set = ammo =>
					{
						// Derived: 300 mm at 60 deg at 2000 m (600 LOS), divided by f 0.867 of this drag.
						ammo.RhaPenetration = 693f;
						ammo.Mass = 4.5f;   // the rod, as the T-90M's
						ammo.MuzzleVelocity = 1660f;   // «Начальная скорость снаряда, м/с 1660»
						ammo.Coeff = 0.093f;   // derived: 3.5 % velocity loss per 1000 m, low confidence
					},
					Prevalence = SvinetsPrevalence,
				},
			},
			DefaultApfsds = () => T80UM2Mod.Apfsds?.Value,
			ForceApfsds = () => T80UM2Mod.ForceApfsds?.Value == true,
			ApfsdsListName = "125mm APFSDS (T-80UM2)",
			MenuSuffix = " (T-80UM2)",
			// HEAT: the 3BK18M, the vanilla clip as it is, locked. HE-FRAG: the T-80B's own third entry, the 3OF26 (23 kg, 850 m/s).
			FixedHeat = "clip_3BK18M",
			// The 9М119М «Инвар» is the T-90A's round with a name of the T-80UM2's own.
			Missile = new PortAmmo.Round
			{
				Id = "9M119M",
				Name = "9M119M Invar ATGM",
				Source = "clip_9M112M",
				Texture = "round_9m119m_co.png",
				Atlas = "kobra_co",
				Shape = "9M119M",
				Set = ammo =>
				{
					ammo.RhaPenetration = 700f;   // «700» mm mean at normal; constant (shaped charge)
					ammo.Tandem = true;   // tandem warhead (PortAmmo's TandemEraPatch: the precursor sets off ERA)
					ammo.Mass = 17.2f;
					// GHPC flies a missile at constant speed, 284 m/s gives the real 17.6 s to 5 km.
					ammo.MuzzleVelocity = 284f;   // derived: 5000 m in 17.6 s
					ammo.MaximumRange = 5000f;   // «100 - 5000» m
					ammo.ArmingDistance = 100f;   // minimum range (the 9M112M's is 70)
					ammo.RangedFuseTime = 5000f / 284f + 0.3f;   // derived: self-destructs just past 5 km (vanilla convention)
					ammo.Guidance = AmmoType.GuidanceType.Laser;   // beam riding, receiver 9Н244
				},
			},
			EraDefeating = new[] { "9M119M Invar ATGM" },
			TotalCounts = Counts,
			// GHPC's restock fills the first rack with room from the last rack holding the round: the conveyor from the wet rack.
			Racks = new[]
			{
				// Ammo carousel: missiles at 26 (the T-80B's) and 13, across the ring, not 26 / 27 side by side. The 9М119М's
				// stored model is full calibre along its length and two neighbours overlap 17 mm in the conveyor's converging
				// trays (vanilla rounds 2.9 mm, the 3BM60 no worse). The slot choice is a gameplay / look setting.
				new PortAmmo.Rack { Index = 0, Counts = ConveyorCounts, MissileSlots = new[] { 26, 13 } },
				// Hull wet rack + 5 slots (copies 4 / 5: the turret rear).
				new PortAmmo.Rack { Index = 1, Counts = WetRackCounts, MissileSlots = new[] { 10, 11 } },
			},
			BeamOnBaseUnit = true,   // the T-80B's guidance unit kept, steered along the day-sight beam
			BeamName = "T80UM2 guidance beam",
			// Coax ПКТ: 1250 rounds in 5 belts of 250: «1250 патронов, снаряженных в пяти лентах (по 250 патронов в каждой)».
			// The rate is GHPC's PKT as it is.
			CoaxFeed = "PKT",
			CoaxRounds = T80UM2Mod.CoaxRounds,
			CoaxBelt = PktBelt,
		};

		// The 2А46М-1 on the T-80B's mount.
		internal static readonly GunSetup.Settings Gun = new GunSetup.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			// Drives: the T-80B's 25 / 25 deg/s kept (the 2Э42's 16 deg/s slew is a forum figure). 0 = the base's.
			TurretSpeed = 0f,
			GunSpeed = 0f,
			// −5 / +14 over the nose: «Углы ВН, °: −5…+14°» (the T-80B base has −5 / +15).
			GunLimits = new Vector2(-5f, 14f),
			// −4 over the rear: «снижения ... (на корму)» 4 deg, from the T-64A manual (same Kharkiv fighting compartment and gun
			// mount). The rear sector's bounds are in no source: ±150..180 deg from the nose, inferred, with 5 deg ramps.
			DepressionProfile = "gun_depression.txt",
			// One step of the conveyor takes 7.1 s («7,1 при повороте конвейера на один шаг», the T-64A manual: the family's МЗ;
			// the T-80U's own figure not found, medium confidence). GHPC's T-80B stages (7.2 s) scaled to it.
			AutoloaderCycle = 7.1f,
			GunName = "125mm Gun 2A46M-1",   // «На Т-80У установлена ... 2А46М-1»
			CodexName = "T80UM2 2A46M-1",
		};
	}
}
