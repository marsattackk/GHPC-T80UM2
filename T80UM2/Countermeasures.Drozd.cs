using GHPC.Vehicle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The T-80UM2's «Дрозд» hard-kill APS (1030М type: four twin 107 mm blocks, 8 rounds, ±40 deg in front of the turret) on the
	// shared Aps module. The blocks' places come from the War Thunder model's tubes (a checklist only). Sources by short name:
	// btvt (btvt.narod.ru), dogswar, ТиВ 2007 / 2013, VTTV-97, Сермонак, ru.wikipedia.
	internal static class Drozd
	{
		internal static readonly Aps.Settings Settings = new Aps.Settings
		{
			Tag = "APS T-80UM2 Drozd",
			Name = "Drozd",
			OnAtSpawn = true,   // armed at spawn
			WarmUp = 300f,   // 5 min after the player switches it off and on («готов ... через 5 мин», btvt)
			HatchInterlock = true,   // open hatches block firing (dogswar)
			RoundsPerBlock = 2,   // «с одного направления: 2» (btvt)
			// Four adjacent 20 deg sectors; per side two blocks one over the other, «под углом 20 град. друг к другу» (btvt). Which
			// block takes which sector is read off the model's tubes: the upper blocks point 19 deg out, the lower straight ahead,
			// so the upper serve the outer sectors (inferred). The tubes' axes: lower 0, upper 20 deg out (the source's 20 deg; the
			// model's 0 / 19 agree). The sector centres as axes put the burst ~2 m beside a threat flying at the tank.
			// The modules (the block's two rounds inside its 25 mm housing, the radar inside its own): a destroyed block loses its
			// rounds, no cook-off; a destroyed head blinds its side.
			Blocks = new[]
			{
				new Aps.BlockSpec
				{
					Name = "left outer",
					Side = 0,
					Module = "aps_block_lo",
					AzMin = -40f,
					AzMax = -20f,
					AxisAz = -20f,
					AxisEl = 0f,
					// bone_rocket_05, _06
					Mouths = new[] { new Vector3(-1.783f, 1.780f, -0.472f), new Vector3(-1.915f, 1.781f, -0.518f) },
				},
				new Aps.BlockSpec
				{
					Name = "left inner",
					Side = 0,
					Module = "aps_block_li",
					AzMin = -20f,
					AzMax = 0f,
					AxisAz = 0f,
					AxisEl = 0f,
					// bone_rocket_07, _08
					Mouths = new[] { new Vector3(-1.532f, 1.626f, -0.267f), new Vector3(-1.673f, 1.625f, -0.267f) },
				},
				new Aps.BlockSpec
				{
					Name = "right inner",
					Side = 1,
					Module = "aps_block_ri",
					AzMin = 0f,
					AzMax = 20f,
					AxisAz = 0f,
					AxisEl = 0f,
					Mouths = new[] { new Vector3(1.567f, 1.631f, -0.332f), new Vector3(1.708f, 1.631f, -0.332f) },   // bone_rocket_03, _04
				},
				new Aps.BlockSpec
				{
					Name = "right outer",
					Side = 1,
					Module = "aps_block_ro",
					AzMin = 20f,
					AzMax = 40f,
					AxisAz = 20f,
					AxisEl = 0f,
					Mouths = new[] { new Vector3(1.940f, 1.784f, -0.568f), new Vector3(1.808f, 1.783f, -0.523f) },   // bone_rocket_01, _02
				},
			},
			SensorModules = new[] { "aps_head_l", "aps_head_r" },
			ComputerModule = "drozd_computer",   // the ЭВС on the turret rear (btvt): destroyed, the whole complex is dead
			ElMin = -6f,
			ElMax = 20f,   // btvt, dogswar table, ТиВ 2007 table
			SearchRange = 330f,   // btvt, ТиВ 2007
			TrackRange = 130f,   // btvt
			MinRange = 11f,   // a grenade from 11 m destroyed in trials (dogswar)
			SpeedMin = 70f,
			SpeedMax = 700f,   // btvt, VTTV-97; tank HEAT / HE shells only inside it
			SizeMin = 0.036f,   // 10 cm² radar cross-section, a ~36 mm disc (dogswar; diameter inferred)
			ReactionTime = 0.02f,   // inferred from the 11 m trial
			RoundSpeed = 190f,   // ru.wikipedia
			InterceptRange = 6.7f,   // burst 6.7 m from the tube mouths (btvt), nominal
			BurstMin = 6f,
			BurstMax = 8f,   // the remote time fuse set by the computer (btvt); «6-8 м»
			FragReach = 1.5f,   // the field round the burst: 120 fragments / m² at 1.5 m (Сермонак; inferred as the reach)
			// For the upper glacis shots that were let through (GHPC-T80UM2 issue 2): ru.wikipedia's ±30 deg field and 5 m ahead
			// of the burst, where ~230 fragments over the cone still put ~1 on a missile's side (~11 / m², inferred).
			ConeReach = 5f,   // mine: no source gives the field's reach; 5 m is where ~1 fragment still meets a missile
			// The computer picks the tube («номер мортиры», btvt) whose burst the threat passes, not only the bearing's block: the
			// outer tubes point 20 deg out, their burst ~3 m beside a threat flying at the hull.
			PickTube = true,
			Cooldown = 0.35f,   // per side's radar module (btvt)
			OtherSide = 0.035f,   // the other side 35 ms later (dogswar)
			VisualPrefix = "T80UM2_",   // the protected box: the tank's outline («летит в контур танка», btvt), margin 0 (inferred)
			BoxLeaveOut = new[] { "antenna" },   // the whip made the protected box 4.7 m tall
			PAtgm = 1f,
			PHeat = 0.78f,
			PHe = 0.78f,   // state trials 1.0 against ATGMs, 0.78 overall (btvt)
			PGrenade = 0.7f,   // grenades below: the specification's 0.7 (btvt)
			// Some kills break the threat up without setting its warhead off: 25 %, inferred. No source gives a split; the sources
			// list destruction, warhead detonation and deflection (dogswar, alternathistory, NSTU 2018, ТиВ 2007). Detonation is
			// kept as the default: ТиВ names it first for shaped-charge warheads, fragments set off the PG-7's and 9M113's
			// sensitive nose fuses easily, and Arena's trial film shows the threats exploding (ТиВ 2013).
			PBrokenUp = 0.25f,   // mine, no source gives a split between broken up and set off
			// A warhead set off by the fragments throws fewer fragments than its own burst: a quarter of GHPC's count (Fagot 50 ->
			// 12, at its own 1-10 mm). No source gives a number (inferred).
			WarheadFragShare = 0.25f,   // mine: a warhead set off by our fragments throws a quarter of GHPC's own count
			FragCount = 230,
			FragConeHalf = 30f,   // ru.wikipedia's ±30 deg, ~230 fragments (120 / m² at 1.5 m over the cone)
			FragSpeed = 1600f,   // Сермонак, ru.wikipedia
			FragMass = 0.003f,   // ~3 g (Сермонак)
			// No source for the fragments' penetration or drag: the T-90M's 3 g «Тельник» element (same mass, its drag and 6 mm at
			// 1200 m/s), ~10.7 mm at 1600 m/s; "a few mm to ~10 mm of steel" (inferred, low).
			FragArea = 4.86e-5f,
			FragCoeff = 0.6f,
			FragPen = 6f,
			FragPenSpeed = 1200f,
			ReloadPerRound = 60f,   // my option: 60 s per round from the vehicle menu; unlimited spares
			LaunchEvent = "event:/Weapons/launcher_9M14",   // the vanilla sound first
		};

		internal static void Apply(Vehicle v) => Aps.Apply(v, Settings);
	}
}
