using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The commander's ТКН-4С: day field 7.8 deg (the T-90A's measured field, its shared reticle), night channel 11.5 deg
	// (inferred, the T-90A's), the mirror -10 / +20 deg, in the head at the War Thunder cupola's commander_panoramic_sight
	// (inferred eye point). Two copies of the T-80B's own TPN-3-49 optic (no donor vehicle), extra sights of the gun's fire
	// control for the main gun and coax. With «Дубль» the commander lays and fires the gun looking through it, the computer
	// off, the range set by hand on its stadia and the range line on its Б / О / К scales, no laser («ПНК-4С»,
	// odetievbrony.ru), through the shared ComputerOff in hand-range mode and ReticleRangeLine. The cupola with the ТКН-4С
	// stays fixed at its rest place on the turret with its hatch shut: nothing turns it, and «Дубль» and the target
	// designation still lay the gun. Target designation: in the commander's own view (commander head, NODs) GHPC's Aim
	// Override key slews the turret to his line of sight («ПНК-4С», done by the shared TargetDesignation).
	internal static partial class Sights
	{
		const string TknDayFile = "tkn4s_day.jsonc";
		const string TknNightFile = "tkn4s_night.jsonc";

		// The T-90A's measured field (Suvorov's table has 7 deg) («Т-90», Suvorov)
		const float TknDayFov = 7.8f;
		// Day field x 7.5 / 5.1 (the magnifications), inferred as the T-90A's («ПНК-4С», odetievbrony.ru)
		const float TknNightFov = 11.5f;
		// The mirror's travel «-10 / +20» («ПНК-4С», odetievbrony.ru)
		static readonly Vector2 TknMirror = new Vector2(-10f, 20f);
		// The Б scale's span on Рис. 4.65а, to 40 («ПНК-4С», odetievbrony.ru)
		static readonly Vector2 TknRange = new Vector2(0f, 4000f);
		// In «Дубль» the gun is slaved to the ТКН-4С's line by angle, aligned «в согласованном положении» by the gun position
		// sensor's adjustment («ПНК-4С», odetievbrony.ru). The alignment distance isn't given: the family's distant-point rule
		// for the gun's day sight, «не менее 1600 м» («Танк Т-72А. Техническое описание и инструкция по эксплуатации», 1986,
		// p. 66), inferred for the ТКН-4С. The lines meet there (the shared GunnerFcs.Converge), with the view 0.59 m right of
		// and over the gun.
		const float TknConverge = 1600f;
		// Eye: 1.5 cm in front of the front of the War Thunder commander_panoramic_sight damage box (x 0.348..0.834,
		// y 1.848..2.357, z -1.302..-1.045 in the prefab), its upper middle, as the T-90A's (inferred: the head's window
		// is not measured on this model).
		internal static readonly Vector3 TknEye = new Vector3(0.591f, 2.240f, -1.030f);

		// Two inactive copies of the TPN-3-49 (slot, optic, reticle mesh, post volume) for the ТКН-4С's channels.
		static void CloneTkn(Vehicle vehicle, UsableOptic tpn, Fitted station, List<string> problems)
		{
			var turret = vehicle.AimablePlatforms?.FirstOrDefault(platform => platform != null
				&& platform.Name == "Turret traverse")?.Transform;
			if (turret == null)
			{
				problems.Add("no turret traverse drive: no ТКН-4С");
				return;
			}
			var holder = new GameObject("T80UM2 TKN-4S setup");
			holder.SetActive(false);
			holder.transform.SetParent(vehicle.transform, false);
			UsableOptic Copy()
			{
				var clone = UnityEngine.Object.Instantiate(tpn.gameObject, holder.transform, false);
				foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
				return clone.GetComponent<UsableOptic>();
			}
			station.TknDay = Copy();
			station.TknNight = Copy();
			// The day channel draws its reticle as the 1G42's does: a copy of that reticle mesh object (its renderer and post
			// draw set up for a day picture), because the TPN-3-49's draws light strokes in a day picture.
			var dayMesh = station.Day != null ? station.Day.reticleMesh : null;
			if (dayMesh != null && station.TknDay.reticleMesh != null)
			{
				var old = station.TknDay.reticleMesh.gameObject;
				var meshCopy = UnityEngine.Object.Instantiate(dayMesh.gameObject, station.TknDay.transform, false);
				meshCopy.name = old.name;
				station.TknDay.reticleMesh = meshCopy.GetComponent<ReticleMesh>();
				UnityEngine.Object.DestroyImmediate(old);
			}
			else problems.Add("ТКН-4С day channel keeps the night sight's reticle mesh (no 1G42 reticle mesh found)");
			var head = station.transform;
			head.SetParent(turret, false);
			head.position = vehicle.transform.TransformPoint(TknEye);
			head.rotation = turret.rotation;
		}

		static void ConfigureTkn(
			Vehicle vehicle,
			FireControlSystem fcs,
			UsableOptic gunnerDay,
			Fitted station,
			Dictionary<string, AmmoCodexScriptable> rounds,
			ReticleSO fontHost,
			List<string> problems)
		{
			var head = station.transform;
			var weaponsManager = vehicle.GetComponent<WeaponsManager>();
			var weapons = weaponsManager.Weapons
				.Where(weapon => weapon.Role == WeaponSystemRole.MainGun || weapon.Role == WeaponSystemRole.Coaxial)
				.Select(weapon => weapon.Weapon)
				.Where(weapon => weapon != null)
				.ToArray();
			var day = station.TknDay;
			var night = station.TknNight;
			// Night channel first: a day slot marks its linked night sight when it wakes.
			foreach (var optic in new[] { night, day })
			{
				optic.FCS = fcs;
				optic.RegisterAtStart = false;
				// Along the gun's commanded aim: «Дубль»
				optic.Alignment = OpticAlignment.BoresightStabilized;
				optic.RotateElevation = true;
				optic.RotateAzimuth = true;
				optic.CantCorrect = false;
				optic.ForceHorizontalReticleAlign = false;
				optic.Mounts = new AimablePlatform[0];
				optic.LocalElevationLimits = TknMirror;
				optic.LocalAzimuthLimits = Vector2.zero;
				optic.OverrideRangeLimits = TknRange;
				optic.OverrideRangeStep = 100;
				var slot = optic.slot;
				Commander.SlotReparent?.SetValue(slot, head);
				slot.AllowFreeZoom = false;
				slot.OtherFovs = new float[0];
				slot.fovAspect = false;
				slot.ExclusiveWeapons = weapons;
				// The TPN-3-49's shutter and cover come over with the copy.
				slot.ExclusiveItems = new GameObject[0];
				slot.ZeroScaleItems = new Transform[0];
				slot.SlotToUseOnDestroy = gunnerDay.slot;
				slot.IsExterior = false;
				slot.DirectAttachOnly = false;
				var file = optic == night ? TknNightFile : TknDayFile;
				optic.reticleMesh.reticleSO = Reticles.Build(file, optic.reticleMesh.reticleSO, fontHost, rounds);
				ReticleMeshes.ClearSmr(optic.reticleMesh);
				// The copy's built mesh and light states are the original's, so they're cleared for the new reticle
				// (the shared ReticleBuild.PrepareCopy).
				ReticleBuild.PrepareCopy(optic.reticleMesh);
				// The hand range on the selected round's scale
				optic.gameObject.AddComponent<ReticleRangeLine>().Init(optic, fcs, () => fcs.CurrentAmmoType);
				ComputerOff.Register(fcs, slot, ComputerOff.Mode.HandRange, $"{Tag} Дубль");
				// The gun's line meets the ТКН-4С's at 1600 m
				GunnerFcs.Converge.Register(fcs, slot, TknConverge);
				// The aim kept inside the mirror's travel
				GunnerFcs.AimClamp.Register(fcs, optic);
				optic.transform.SetParent(head, false);
				optic.transform.localPosition = Vector3.zero;
				optic.transform.localRotation = Quaternion.identity;
			}
			day.name = "TKN-4S sight";
			day.slot.DefaultFov = TknDayFov;
			day.slot.VisionType = NightVisionType.None;
			day.slot.LinkedNightSight = night.slot;
			day.slot.NightSightAtNightOnly = false;
			// The TPN-3-49's intensifier blur (0.15) off the day channel
			day.slot.BaseBlur = gunnerDay.slot.BaseBlur;
			day.slot.VibrationPreBlur = gunnerDay.slot.VibrationPreBlur;
			// The day channel's picture: the 1Г46's day post, not the intensifier's.
			if (day.post != null && gunnerDay.post != null)
			{
				day.post.sharedProfile = gunnerDay.post.sharedProfile;
				day.post.priority = gunnerDay.post.priority;
				day.post.weight = gunnerDay.post.weight;
			}
			night.name = "TKN-4S night channel";
			night.slot.DefaultFov = TknNightFov;
			night.slot.VisionType = NightVisionType.Intensifier;
			night.slot.LinkedNightSight = null;
			SightMotion.Stabilised(day.slot);
			SightMotion.Stabilised(night.slot, keepBlur: true);
			// The day channel's indicator strip (Рис. 4.65а and the description's screen section: the round's letter, «ГОТОВ»).
			var tknSpec = Reticles.Spec(TknDayFile);
			var strip = ReticleBuild.BuildOverlay(tknSpec, day.transform, (float)tknSpec.Fov, problems);
			SightReadout.Attach(
				day,
				strip,
				new SightReadout.Settings { Ready = "ready indicator", Ammo = "ammo", AmmoText = SightReadout.Glyph7 },
				problems);
			foreach (var optic in new[] { night, day }) optic.gameObject.SetActive(true);
			var holder = vehicle.transform.Find("T80UM2 TKN-4S setup");
			if (holder != null) UnityEngine.Object.Destroy(holder.gameObject);
			// Into the unit's camera list (the shared RemoteMg: every path to the camera manager, and Shift back in).
			RemoteMg.LateSetup(vehicle);
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevFitted(Vehicle vehicle, Fitted fitted);

		static partial void DevNight(ref string want);
	}
}
