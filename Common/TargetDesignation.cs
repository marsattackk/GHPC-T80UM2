using System.Linq;
using GHPC.Camera;
using GHPC.Crew;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weapons;
using HarmonyLib;
using UnityEngine;

namespace T80UM2.Common
{
	internal static class TargetDesignation
	{
		internal static Link Register(Vehicle vehicle, FireControlSystem main, CameraSlot[] from)
		{
			if (vehicle == null || main == null || from == null) return null;
			var l = vehicle.gameObject.GetComponent<Link>() ?? vehicle.gameObject.AddComponent<Link>();
			l.Main = main; l.From = from.Where(s => s != null).ToArray();
			return l;
		}

		internal static bool KeyHeld()
		{
			try { return InputUtil.MainPlayer != null && InputUtil.MainPlayer.GetButton("Aim Override"); }
			catch { return false; }
		}

		static bool IsPlayers(Vehicle v) => v != null && PlayerInput.Instance != null && (object)PlayerInput.Instance.CurrentPlayerUnit == v;

		[DefaultExecutionOrder(-40)]
		internal sealed class Link : MonoBehaviour
		{
			internal FireControlSystem Main;
			internal CameraSlot[] From = new CameraSlot[0];
			internal bool Held { get; private set; }

			Vehicle _vehicle;

			internal bool InView => From.Contains(CameraSlot.ActiveInstance);

			// Take the line once. The view rides the turret, a line read every frame runs away with it.
			Vector3 _line;

			void Update()
			{
				bool held = Main != null && InView && KeyHeld() && IsPlayers(_vehicle ?? (_vehicle = GetComponent<Vehicle>()));
				if (held && !Held)
				{
					var cam = Camera.main;
					if (cam == null) return;
					_line = cam.transform.forward;
				}
				Held = held;
				if (!held) return;
				Main.SetAimVector(_line, true);
			}
		}

		// GHPC's Aim Override swings the active weapon at the AI's target. In the commander's view it's the ЦУ instead.
		[HarmonyPatch(typeof(CrewBrainWeaponsModule), nameof(CrewBrainWeaponsModule.SetAimVector))]
		static class AimOverridePatch
		{
			static bool Prefix(CrewBrainWeaponsModule __instance, WeaponSystemRole weaponSystemRole)
			{
				try
				{
					if (weaponSystemRole != WeaponSystemRole.MainGun && weaponSystemRole != WeaponSystemRole.Coaxial) return true;
					if (!KeyHeld()) return true;
					var input = PlayerInput.Instance;
					if (input == null || (object)input.CurrentPlayerCrewBrain?.WeaponsModule != __instance) return true;
					var unit = input.CurrentPlayerUnit as Vehicle;
					var l = unit != null ? unit.GetComponent<Link>() : null;
					return l == null || !l.InView;
				}
				catch (System.Exception e) { PatchGuard.Failed("Target designation (aim override)", e); return true; }
			}
		}
	}
}
