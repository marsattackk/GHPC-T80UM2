using System.Collections.Generic;
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
	// The night sights on the TPN-3-49's slot: the «Агава-2» thermal by default (its own part file), or the ТПН-4
	// «Буран-ПА» on a copy of the TPN-3-49 when the "Night sight" setting says so, in play too. The «Буран-ПА» keeps the
	// image intensifier's look (GHPC's 'GHPC NOD gen 2 RU'); its reticle is inferred. It fires only with the computer off,
	// the range set by hand on the scales («Т-80У», БТВТ; the shared ComputerOff in manual mode: GHPC's manual mode while it
	// is the view, no laser).
	internal static partial class Sights
	{
		// The setting's values
		internal const string Agava2 = "Agava-2";
		internal const string BuranPa = "Buran-PA";

		const string NightFile = "tpn4_night.jsonc";

		// «Поле зрения, град. 5,25» (Suvorov, «Т-90»)
		const float NightFov = 5.25f;
		// The scales' span (Б / ОК to 20: my guess, inferred from the ТПН-4ER description); the gun to 1500 m (Suvorov, «Т-90»)
		static readonly Vector2 NightRange = new Vector2(0f, 2000f);

		// The ТПН-4 «Буран-ПА» on the TPN-3-49's slot.
		static void ApplyNight(
			UsableOptic night,
			FireControlSystem fcs,
			Dictionary<string, AmmoCodexScriptable> rounds,
			ReticleSO fontHost,
			bool copy = false)
		{
			var host = night.reticleMesh.reticleSO;
			night.reticleMesh.reticleSO = Reticles.Build(NightFile, host, fontHost, rounds);
			ReticleMeshes.ClearSmr(night.reticleMesh);
			// A copy keeps the original's built mesh and light states
			if (copy) ReticleBuild.PrepareCopy(night.reticleMesh);
			else night.reticleMesh.Load();
			night.name = "TPN-4 Buran-PA night sight";
			var slot = night.slot;
			slot.DefaultFov = NightFov;
			slot.OtherFovs = new float[0];
			slot.AllowFreeZoom = false;
			// Vertical, as the TPN-3-49's
			slot.fovAspect = false;
			night.OverrideRangeLimits = NightRange;
			night.OverrideRangeStep = 100;
			// The head rides the stabilised gun (dependent): a stabilised sight's shake, the intensifier's own blur kept.
			SightMotion.Stabilised(slot, keepBlur: true);
			ComputerOff.Register(fcs, slot, ComputerOff.Mode.Manual, $"{Tag} ТПН-4");
		}

		// An inactive copy of the TPN-3-49 (slot, optic, reticle mesh, post volume) for the «Буран-ПА».
		static UsableOptic CopyBuran(Vehicle vehicle, UsableOptic tpn)
		{
			var holder = new GameObject("T80UM2 Buran-PA setup");
			holder.SetActive(false);
			holder.transform.SetParent(vehicle.transform, false);
			var copy = UnityEngine.Object.Instantiate(tpn.gameObject, holder.transform, false);
			foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
			return copy.GetComponent<UsableOptic>();
		}

		// Which night sight the night key opens from the 1Г46 (the "Night sight" setting): the 1G42 slot's linked night sight,
		// swapped when the setting changes (in play too), not while either night sight is the view. GHPC links a day slot's
		// night sight only once (CameraSlot.Awake): the links are set here. The thermal works by day too.
		internal sealed class NightChoice : MonoBehaviour
		{
			Fitted _station;
			Vehicle _vehicle;
			internal string Linked;

			internal static string Want
			{
				get
				{
					string want = T80UM2Mod.NightSight != null && T80UM2Mod.NightSight.Value == BuranPa ? BuranPa : Agava2;
					DevNight(ref want);
					return want;
				}
			}

			internal void Init(Fitted station, Vehicle vehicle)
			{
				_station = station;
				_vehicle = vehicle;
				Link(Want);
			}

			void Update()
			{
				if (_station != null && Want != Linked) Link(Want);
			}

			void Link(string want)
			{
				var day = _station.Day?.slot;
				var sight = want == BuranPa || _station.Agava == null ? _station.Night : _station.Agava;
				if (day == null || sight == null) return;
				var active = CameraSlot.ActiveInstance;
				// Swapped once out of it
				if (active != null && (active == _station.Night?.slot || active == _station.Agava?.slot)) return;
				// CameraSlot.Awake links a day slot's night sight once and never again, so every link is set by hand.
				day.LinkedNightSight = sight.slot;
				sight.slot.IsLinkedNightSight = true;
				sight.slot.LinkedDaySight = day;
				day.NightSightAtNightOnly = sight == _station.Agava ? false : _station.NightAtNightOnly;
				// VisibilityManager goes off the unit's night vision type for AI spotting, not the slot's.
				ThermalSight.SetUnitNightVision(_vehicle, sight.slot);
				Linked = want;
			}
		}
	}
}
