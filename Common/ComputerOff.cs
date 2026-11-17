using System;
using System.Collections.Generic;
using System.Reflection;
using GHPC.Camera;
using GHPC.Weapons;
using HarmonyLib;

namespace T80UM2.Common
{
	internal static class ComputerOff
	{
		internal enum Mode { Manual, HandRange }

		static readonly Dictionary<CameraSlot, (FireControlSystem fcs, Mode mode, string tag)> Slots =
			new Dictionary<CameraSlot, (FireControlSystem, Mode, string)>();
		static readonly Dictionary<FireControlSystem, bool> Before = new Dictionary<FireControlSystem, bool>();
		static readonly MethodInfo SetManual = Reflect.Method(typeof(FireControlSystem), "SetManualMode", new[] { typeof(bool) });

		internal static void Register(FireControlSystem fcs, CameraSlot slot, Mode mode, string tag)
		{
			if (fcs == null || slot == null || Slots.ContainsKey(slot)) return;
			UnityKeys.Prune(Slots);
			UnityKeys.Prune(Before);
			Slots[slot] = (fcs, mode, tag);
			slot.ActiveChanged += active =>
			{
				try { Switch(slot, active); }
				catch (Exception exception) { PatchGuard.Failed("Computer-off sight", exception); }
			};
		}

		internal static bool Active(FireControlSystem fcs, out Mode mode)
		{
			mode = Mode.Manual;
			var activeSlot = CameraSlot.ActiveInstance;
			if (activeSlot == null || fcs == null || !Slots.TryGetValue(activeSlot, out var entry) || entry.fcs != fcs) return false;
			mode = entry.mode;
			return true;
		}

		static void Switch(CameraSlot slot, bool active)
		{
			if (!Slots.TryGetValue(slot, out var entry) || entry.fcs == null) return;
			var fcs = entry.fcs;
			if (active)
			{
				if (!Before.ContainsKey(fcs)) Before[fcs] = InManual(fcs);
				SetManual?.Invoke(fcs, new object[] { true });
				if (entry.mode == Mode.HandRange)
				{
					fcs.SuperelevateWeapon = true;
					fcs.AdjustAimByDegrees(0f, 0f);
				}
				Snap(fcs);
				return;
			}
			if (!Before.TryGetValue(fcs, out bool was)) return;
			Before.Remove(fcs);
			SetManual?.Invoke(fcs, new object[] { was });
			if (was)
			{
				fcs.SuperelevateWeapon = false;
				fcs.AdjustAimByDegrees(0f, 0f);
			}
			Snap(fcs);
		}

		// IsInManualMode is never written by GHPC, it always reads false.
		static bool InManual(FireControlSystem fcs)
			=> fcs.ZeroReticleRangeInAutoMode ? !fcs.ForceReticleToZeroRange : !fcs.SuperelevateWeapon;

		// ReticleMesh eases to a new range at 10 m/s; a mode switch at sight entry slid the night mark from 0 to 3000 m.
		static void Snap(FireControlSystem fcs)
		{
			var unit = fcs.GetComponentInParent<GHPC.Vehicle.Vehicle>();
			if (unit == null) return;
			float range = fcs.ForceReticleToZeroRange ? 0f : fcs.ReportedRange;
			foreach (var optic in unit.GetComponentsInChildren<GHPC.Equipment.Optics.UsableOptic>(true))
				if (optic != null && optic.FCS == fcs && optic.reticleMesh != null)
					optic.reticleMesh.SetRange(fcs.CurrentAmmoType, range, true);
		}

		// A hand-set range re-enters GHPC's manual mode, which drops the aiming angle again.
		[HarmonyPatch(typeof(FireControlSystem), "SetManualMode")]
		static class ManualModePatch
		{
			static void Postfix(FireControlSystem __instance, bool manual)
			{
				try
				{
					if (!manual
						|| Slots.Count == 0
						|| !Active(__instance, out var mode)
						|| mode != Mode.HandRange
						|| __instance.SuperelevateWeapon)
						return;
					__instance.SuperelevateWeapon = true;
					__instance.AdjustAimByDegrees(0f, 0f);
				}
				catch (Exception exception) { PatchGuard.Failed("Computer-off sight (hand range)", exception); }
			}
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.Lase))]
		static class LasePatch
		{
			static bool Prefix(FireControlSystem __instance)
			{
				try
				{
					if (Slots.Count == 0 || !Active(__instance, out _)) return true;
					return false;
				}
				catch (Exception exception)
				{
					PatchGuard.Failed("Computer-off sight laser", exception);
					return true;
				}
			}
		}
	}
}
