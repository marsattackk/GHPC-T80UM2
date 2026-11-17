using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using MelonLoader;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The gunner's and commander's sights and the fire control, on the T-80B's own 1G42 and TPN-3-49 optics: the gunner's 1Г46 of
	// the 1А45 «Иртыш», the night sight (the «Агава-2» thermal, or the «Буран-ПА» when the setting says so), the commander's
	// ТКН-4С with «Дубль», and target designation from the commander's view. The 9М119М's guidance beam runs along the 1Г46's
	// line (the shared GuidanceBeam reads the fire control's main optic). Each device keeps its own part file.
	internal static partial class Sights
	{
		static string Tag => T80UM2Mod.FriendlyName;

		// The 1А45's fire control with the 1Г46's rangefinder and line of sight.
		static readonly GunnerFcs.Settings Settings = new GunnerFcs.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			// «Диапазон измерения дальности до цели, м: 400–5000» («Комплекс управления огнем 1А45», odetievbrony.ru)
			LaserMin = 400f,
			LaserMax = 5000f,
			// «от −15° до +20° в продольно-вертикальной плоскости … от −8° до +8° в горизонтальной» («Прицел 1Г46»,
			// odetievbrony.ru)
			LineOfSightLimits = new Vector2(-15f, 20f),
			LineOfSightAzimuth = 8f,
			// The manual range input step: not found, the T-90A's (inferred)
			RangeStep = 100,
			DisplayRangeIncrement = 50,
			// The 1В528's zone is 3.0 x 4.0 тыс («Комплекс управления огнем 1А45»); GHPC's 1A33 value kept, as in the T-90A
			// (inferred)
			FireGateAngle = 0.1f,
			// A range set by hand: the sight's range scales (the T-90A's 1G46)
			ManualMode = true,
			// "Fire only when the gun is laid", on by default: the real firing-permission circuit
			FireWhenLaid = () => T80UM2Mod.FireWhenLaid == null || T80UM2Mod.FireWhenLaid.Value,
		};

		internal static void Apply(Vehicle vehicle)
		{
			if (vehicle == null) return;
			// The commander's lid never opens: pinned shut whatever opens the base T-80B's commander's hatch part (the bail-out
			// plays it)
			foreach (var follower in vehicle.GetComponentsInChildren<HatchRig.Follower>(true))
			{
				if (follower.GetComponentsInChildren<Transform>(true).Any(child => child.name == "T80UM2_HATCH_COMMANDER"))
				{
					follower.Shut = true;
					follower.Pose(0f);
				}
			}
		}

		// What a converted unit holds.
		internal sealed class Fitted : RemoteMg.Station
		{
			internal FireControlSystem Fcs;
			// Night: the ТПН-4 «Буран-ПА»
			internal UsableOptic Day, Night, TknDay, TknNight;
			// The «Агава-2» thermal (null: the donor was missing)
			internal UsableOptic Agava;
			// The 1G42 slot's own "night sight at night only" (the «Буран-ПА»'s)
			internal bool NightAtNightOnly;
			internal TargetDesignation.Link Cu;
			internal override IEnumerable<CameraSlot> Sights => new[] { TknDay?.slot }.Where(slot => slot != null);
			internal bool Dubl => (TknDay != null && TknDay.slot.IsActive) || (TknNight != null && TknNight.slot.IsActive);
		}

		// Two frames after spawn (Conversion.LateSteps): the fire control has its optics, the port's rounds are loaded.
		internal static void Fit(Vehicle vehicle)
		{
			var fcs = vehicle
				.GetComponentsInChildren<FireControlSystem>(true)
				.FirstOrDefault(candidate => candidate.name == "1G42 gunner's sight");
			var optics = vehicle.GetComponentsInChildren<UsableOptic>(true);
			var day = fcs == null
				? null
				: GuidanceBeam.DayOptic(fcs)
					?? optics.FirstOrDefault(optic => optic.FCS == fcs && optic.slot != null && optic.slot.LinkedNightSight != null);
			if (day == null)
			{
				MelonLogger.Warning($"{Tag}: no 1G42 gunner's sight found; the T-80B's sights kept.");
				return;
			}
			// Already converted
			if (vehicle.GetComponentInChildren<Fitted>(true) != null) return;
			var nightSlot = day.slot.LinkedNightSight;
			var night = nightSlot == null ? null : nightSlot.PairedOptic ?? optics.FirstOrDefault(optic => optic.slot == nightSlot);
			var problems = new List<string>();
			var rounds = Rounds(vehicle, problems);
			// The T-80B's own TPN-3-49 (labelled)
			var fontHost = night != null ? night.reticleMesh.reticleSO : day.reticleMesh.reticleSO;

			// The ТКН-4С's two channels: copies of the TPN-3-49 as the base built it, before it becomes the ТПН-4.
			var station = new GameObject("T80UM2 TKN-4S").AddComponent<Fitted>();
			station.transform.SetParent(vehicle.transform, false);
			station.Fcs = fcs;
			station.Day = day;
			station.Night = night;
			if (night != null) CloneTkn(vehicle, night, station, problems);
			// The «Буран-ПА» (the setting's other night sight): a third copy, the TPN-3-49 itself becoming the «Агава-2».
			var buran = night != null ? CopyBuran(vehicle, night) : null;

			ApplyDay(day, rounds, problems, station);
			GunnerFcs.Apply(day, Settings);
			GunnerFcs.LaserAim.Register(fcs, day, Settings);
			if (night != null)
			{
				station.NightAtNightOnly = day.slot.NightSightAtNightOnly;
				string agava = ApplyAgava(day, night, problems);
				if (agava != null) station.Agava = night;
				else problems.Add("«Агава-2» not applied (thermal donor missing): the «Буран-ПА» only");
				ApplyNight(buran, fcs, rounds, fontHost, copy: true);
				// The TPN-3-49's place, the copy's own local pose
				buran.transform.SetParent(night.transform.parent, false);
				// GHPC keeps an optic's object off while its slot isn't the view, so the copy came off: switched on here, its
				// UsableOptic starts (pairs with the slot, follows it on and off) as the ТКН-4С's copies do.
				buran.gameObject.SetActive(true);
				var holder = vehicle.transform.Find("T80UM2 Buran-PA setup");
				if (holder != null) UnityEngine.Object.Destroy(holder.gameObject);
				station.Night = buran;
				station.gameObject.AddComponent<NightChoice>().Init(station, vehicle);
			}
			if (station.TknDay != null) ConfigureTkn(vehicle, fcs, day, station, rounds, fontHost, problems);
			var slots = vehicle.GetComponentsInChildren<CameraSlot>(true);
			station.Cu = TargetDesignation.Register(
				vehicle,
				fcs,
				new[] { slots.FirstOrDefault(slot => slot.name == "commander head"), slots.FirstOrDefault(slot => slot.name == "NODs") },
				Tag);
			DevFitted(vehicle, station);
			foreach (var problem in problems) MelonLogger.Warning($"{Tag} sights: {problem}");
		}

		// The rounds for the reticles' ballistic scales: the unit's loaded main-gun rounds by kind (the port's own, whichever
		// the firepower setup loaded), the coax's belt; one reticle per set (PortReticles caches by the rounds).
		static Dictionary<string, AmmoCodexScriptable> Rounds(Vehicle vehicle, List<string> problems)
		{
			var clips = vehicle.GetComponent<LoadoutManager>()?.LoadedAmmoList?.AmmoClips;
			var loaded = (clips ?? new AmmoClipCodexScriptable[0])
				.Where(clip => clip != null && clip.ClipType?.MinimalPattern != null && clip.ClipType.MinimalPattern.Length > 0)
				.Select(clip => clip.ClipType.MinimalPattern[0])
				.Where(round => round != null && round.AmmoType != null)
				.ToList();
			AmmoCodexScriptable Main(AmmoType.AmmoCategory category) =>
				loaded.FirstOrDefault(round => round.AmmoType.Guidance == AmmoType.GuidanceType.Unguided
					&& round.AmmoType.Category == category);
			var all = Resources.FindObjectsOfTypeAll<AmmoCodexScriptable>();
			AmmoCodexScriptable Named(string name) => all.FirstOrDefault(round => round.name == name);
			var weaponsManager = vehicle.GetComponent<WeaponsManager>();
			var coax = weaponsManager?.Weapons?.FirstOrDefault(weapon => weapon.Role == WeaponSystemRole.Coaxial)
				?.Weapon?.Feed?.ReadyRack?.ClipTypes?.FirstOrDefault()?.MinimalPattern?.FirstOrDefault();
			var table = new Dictionary<string, AmmoCodexScriptable>
			{
				{ "apfsds", Main(AmmoType.AmmoCategory.Penetrator) ?? Named("ammo_3BM32") },
				{ "heat", Main(AmmoType.AmmoCategory.ShapedCharge) ?? Named("ammo_3BK18M") },
				{ "he", Main(AmmoType.AmmoCategory.Explosive) ?? Named("ammo_3OF26") },
				{ "coax", coax ?? Named("ammo_762x54R") },
			};
			// The ТКН-4С's З scale (the T-90A's Kord): no function on the T-80UM2 (an open, hand-laid НСВТ); drawn for GHPC's
			// 12.7 mm B-32 ('ammo_B32', the NSVT's) when loaded, else for the coax (its marks then read the PKT).
			table["b32"] = Named("ammo_B32") ?? table["coax"];
			foreach (var entry in table.Where(entry => entry.Value == null))
				problems.Add($"no '{entry.Key}' round for the reticles' scales");
			return table;
		}
	}
}
