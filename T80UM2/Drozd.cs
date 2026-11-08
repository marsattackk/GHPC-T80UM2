using GHPC.Vehicle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static class Drozd
	{
		internal static readonly Aps.Settings Settings = new Aps.Settings
		{
			Tag = "APS T-80UM2 Drozd",
			Name = "Drozd",
			OnAtSpawn = true,
			WarmUp = 300f,
			HatchInterlock = true,
			RoundsPerBlock = 2,
			// Axes are the tubes', not the sector centres: centres put the burst ~2 m beside a threat flying straight at the tank.
			Blocks = new[]
			{
				new Aps.BlockSpec { Name = "left outer", Side = 0, Module = "aps_block_lo", AzMin = -40f, AzMax = -20f, AxisAz = -20f, AxisEl = 0f,
					Mouths = new[] { new Vector3(-1.783f, 1.780f, -0.472f), new Vector3(-1.915f, 1.781f, -0.518f) } },
				new Aps.BlockSpec { Name = "left inner", Side = 0, Module = "aps_block_li", AzMin = -20f, AzMax = 0f, AxisAz = 0f, AxisEl = 0f,
					Mouths = new[] { new Vector3(-1.532f, 1.626f, -0.267f), new Vector3(-1.673f, 1.625f, -0.267f) } },
				new Aps.BlockSpec { Name = "right inner", Side = 1, Module = "aps_block_ri", AzMin = 0f, AzMax = 20f, AxisAz = 0f, AxisEl = 0f,
					Mouths = new[] { new Vector3(1.567f, 1.631f, -0.332f), new Vector3(1.708f, 1.631f, -0.332f) } },
				new Aps.BlockSpec { Name = "right outer", Side = 1, Module = "aps_block_ro", AzMin = 20f, AzMax = 40f, AxisAz = 20f, AxisEl = 0f,
					Mouths = new[] { new Vector3(1.940f, 1.784f, -0.568f), new Vector3(1.808f, 1.783f, -0.523f) } },
			},
			SensorModules = new[] { "aps_head_l", "aps_head_r" },
			ComputerModule = "drozd_computer",
			ElMin = -6f, ElMax = 20f,
			SearchRange = 330f,
			TrackRange = 130f,
			MinRange = 11f,
			SpeedMin = 70f, SpeedMax = 700f,
			SizeMin = 0.036f,
			ReactionTime = 0.02f,
			RoundSpeed = 190f,
			InterceptRange = 6.7f,
			BurstMin = 6f, BurstMax = 8f,
			FragReach = 1.5f,
			ConeReach = 3f,
			Cooldown = 0.35f,
			OtherSide = 0.035f,
			VisualPrefix = "T80UM2_",
			BoxLeaveOut = new[] { "antenna" },   // the whip made the protected box 4.7 m tall
			PAtgm = 1f, PHeat = 0.78f, PHe = 0.78f,
			PGrenade = 0.7f,
			PBrokenUp = 0.25f,   // mine, no source gives a split between broken up and set off
			WarheadFragShare = 0.25f,   // mine: a warhead set off by our fragments throws a quarter of GHPC's own count
			FragCount = 60, FragConeHalf = 15f,
			FragSpeed = 1600f,
			FragMass = 0.003f,
			FragArea = 4.86e-5f, FragCoeff = 0.6f, FragPen = 6f, FragPenSpeed = 1200f,
			ReloadPerRound = 60f,
			LaunchEvent = "event:/Weapons/launcher_9M14",
		};

		internal static void Apply(Vehicle v) => Aps.Apply(v, Settings);

		internal static System.Collections.IEnumerator WireModulesLater(Vehicle v)
		{
			for (int i = 0; i < 6; i++) yield return null;
			if (v != null) Armour.Modules.Wire(v);
		}
	}
}
