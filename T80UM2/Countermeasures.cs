using GHPC.Equipment;
using GHPC.Vehicle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The T-80UM2's smoke grenades. The «Дрозд» active protection is in Countermeasures.Drozd.cs.
	internal static class Countermeasures
	{
		// The 902Б «Туча» tubes' 3Д17 aerosol grenades instead of the T-80B's 902А / 3Д6 (the shared ShtoraScreen with the heads
		// off: the tubes and the grenade only, there is no Shtora on the T-80U). «8 дымовых гранатомётов 902Б»; the 3Д17's
		// «дальности ее отстрела 50- 80 м за 2-4 с»: the T-90A's tuned 3Д17 (ShtoraScreen's defaults: 1 s fuse, the cloud's
		// build, life and size, the white burst) as it is. Each grenade goes along its own tube's azimuth (the War Thunder
		// tubes); GHPC's own smoke salvos fire them.
		internal static readonly ShtoraScreen.Settings Screens = new ShtoraScreen.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			Heads = false,   // no laser warning on the T-80U
			PanelShown = false,
			PanelAlways = false,
			AlarmTone = false,
			Automatic = false,
			// Launch 12 deg: «Для гранаты ЗД17 угол возвышения ПУ подобран экспериментально и составляет 12°» (the grenade's own
			// figure, written for the Shtora tubes; for the T-80U's 902Б it is inferred). The War Thunder model's tubes point
			// ~41 deg up, which with the 3Д17's 1 s fuse would burst 50-80 m out some 40 m in the air.
			LaunchAngle = 12f,
			Distance = new Vector2(50f, 80f),
			TubeHeight = 2.0f,   // the War Thunder tubes' mouths over the ground (turret ring 1.40 m + 0.57 m), derived
		};

		// The 3Д17 grenade on the T-80B's 8 slots, after the conversion seated them on the 902Б tubes and gave them their caps.
		// GHPC has no smoke restock (VehicleSmokeManager only counts its slots down), so the 8 tubes' grenades only; the 4 spares
		// are not carried.
		internal static void FitScreens(Vehicle vehicle)
		{
			ShtoraScreen.Apply(vehicle, Screens);
		}

		// Two frames after the spawn (Conversion.LateSteps), when the crew exists.
		internal static void RefreshCrew(Vehicle vehicle)
		{
			var smoke = vehicle.GetComponentInChildren<VehicleSmokeManager>(true);
			if (smoke != null) SmokeLaunchers.RefreshCrew(smoke);
		}
	}
}
