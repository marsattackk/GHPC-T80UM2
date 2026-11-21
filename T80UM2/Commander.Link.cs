using GHPC.Crew;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weapons;
using HarmonyLib;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The НСВТ's commander: the climb-out switch and the patch that keeps the gun to the player's own vehicle.
	internal static partial class Commander
	{
		// He works it from inside with the hatch shut for now. The climb-out code stays for when he gets drawn.
		const bool Climbs = false;

		static bool IsPlayers(Station station) =>
			PlayerInput.Instance != null
			&& (object)PlayerInput.Instance.CurrentPlayerUnit == station.GetComponentInParent<Vehicle>();

		static bool SelectedBy(Station station)
		{
			var module = PlayerInput.Instance?.CurrentPlayerCrewBrain?.WeaponsModule as CrewBrainWeaponsModule;
			return module?.ActiveWeapon != null && module.ActiveWeapon.Weapon == station.Weapon;
		}

		// Every frame: with Climbs on, the box change would wait until he is out there (the magazines are outside). Off, it
		// never waits.
		internal class Link : MonoBehaviour
		{
			Station _station;
			internal void Init(Station station) => _station = station;

			void Update()
			{
				var feed = _station?.Weapon != null ? _station.Weapon.Feed : null;
				if (feed == null || _station.Ex == null) return;
				bool hold = Climbs && !_station.Ex.Climb.MayFire;
				if (feed.ForcePauseReload != hold) feed.ForcePauseReload = hold;
			}
		}

		// The НСВТ fires only from inside and only for the player's own vehicle: AI crews never fire it.
		[HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.AbleToFire), MethodType.Getter)]
		static class OutToFirePatch
		{
			static void Postfix(WeaponSystem __instance, ref bool __result)
			{
				try
				{
					if (!__result || !ByWeapon.TryGetValue(__instance, out var station) || station == null || station.Ex == null) return;
					if (Climbs ? !station.Ex.Climb.MayFire : !IsPlayers(station)) __result = false;
				}
				catch (System.Exception exception) { PatchGuard.Failed("NSVT fires only for the player's tank", exception); }
			}
		}
	}
}
