using T80UM2.Common;

namespace T80UM2
{
	// The T-80UM2's engine, gearing, mass and speed caps: values only, set at spawn by the shared Mobility. No source gives the
	// T-80UM2's own figures: it is one T-80U with the «Дрозд» added, so these are the T-80U's. The T-80B's 4 + 1 side gearboxes
	// stay: its GHPC ratios 4.38 / 2.16 / 1.46 / 1, reverse 6.36 are Попов's 4.370 / 2.159 / 1.460 / 1.000, reverse 6.353 to
	// rounding. The base turbine (sound, idle, max rpm 2300, shift point 2200) is kept.
	internal static class Drive
	{
		// At spawn, before NwhChassis and VehicleController start.
		internal static void Apply(GHPC.Vehicle.Vehicle vehicle)
		{
			Mobility.Apply(vehicle, Settings);
		}

		internal static readonly Mobility.Settings Settings = new Mobility.Settings
		{
			// ГТД-1250: «Мощность двигателя 919 кВт (1250 л.с.)», and the maker's catalogue says 1250 hp. GHPC's maxPower is
			// real hp (the T-80B's 1100 is the ГТД-1000ТФ's).
			MaxPower = 1250f,
			// Final drive: the published 70 km/h top speed at the T-80B's 2300 rpm scale, NWH's formula
			// 0.42 m x 2300 x 0.105 / (1.0 x 5.216) x 3.6 = 70.0 km/h (the base's 4.2 gave 86.9). The real figures (3150 rpm,
			// final drive 5.45, 12 x 164 mm sprocket) give 68.2, and the published 16 / 32.3 / 48 / 70 are that set x 1.026.
			// Derived: solved for «70 км/ч» at the base's 2300 rpm.
			FinalDrive = 5.216f,
			// Reverse: «11 км/ч» is the base's own reverse gear 6.36 at the new final drive (11.0 by NWH's formula), so
			// ReverseGears stays the base's. The T-80B rig holds the formula's speed (T-80B 13.7 held = 13.7 computed), unlike the
			// T-72 rig (~8 % over): the T-90A's calibration tried here (x 4.52 / 4.17) held 10.2 km/h, 7.5 % low.
			// «Максимальная скорость ... 70 км/ч» (four sources): set by the gearing above (fuel cut at 2300 rpm = 70.0 km/h). The
			// cap is the cruise control's target, which eases the throttle within ~5 km/h of it (CruiseControl: 0.5 x (target - v)^2,
			// m/s), so it sits over the real top speed, as vanilla caps do (T-80B 86.4): full stick drives to the governed 70.
			MaxForwardKmh = 80f,
			// Game-scale tuning off the sources: GHPC's speed resistance is the Rigidbody's linear drag (0.1 on every vanilla
			// tank), so with the sourced 1250 hp and 47 t the tank levelled off at 63.3 km/h in 4th at 2084 rpm (the T-80B 59.7
			// against its real 70). Top speed goes as sqrt(power / (drag x mass)): 0.1 x (63.3 / 72)^2 = 0.077 leaves a free
			// plateau of ~72 km/h, so the 2300 rpm cut at 70 is the limit, as on the real tank. I chose it over more power (1530
			// hp for the same, 22 % off the ГТД-1250's figure and that much quicker off the line): drag only matters at speed and
			// is a GHPC-only number. The vanilla T-80B is untouched.
			LinearDrag = 0.077f,
			// The reverse cap stays the base's 16.1 km/h (not set): a cap at the real 11 would hold the tank well under it (the
			// cap is a cruise-control target that eases the throttle near it: a cap at the T-90A's real 4.18 held it at 2.4). The
			// gearing sets the real 11, as on every vanilla tank (T-72M1 cap 16.1, gearing 4.7).
			// Combat mass: the T-80U's 46 t «Полная масса т 46 (46,5)» + 1000 kg for the «Дрозд» complex, total inferred. GHPC
			// uses the Rigidbody's mass as kg (its T-80B: 46 500, a family value, against ~42.5 t for the real T-80B). The centre
			// of mass is set explicitly on the prefab and stays, as do the T-80B's springs.
			CombatMassKg = 47000f,
		};
	}
}
