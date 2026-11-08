using System;
using System.Collections;
using System.Linq;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static class Looks
	{
		static readonly TacticalNumbers.Settings Numbers = new TacticalNumbers.Settings
		{
			Mode = "rear lamp",
			LampFace = new Vector3(-0.195f, 2.440f - 1.412f, -2.276f + 1.1715f),
			LampNormal = new Vector3(0f, 0f, -1f),
			DigitScale = 1.2f,
			DigitSpacing = 0.063f,   // the T-80B's 7.3 cm puts the right digit over the lamp's frame
			PlateNames = new[] { "march light" },
		};

		static readonly LampMove.Lamp[] Lamps =
		{
			new LampMove.Lamp { Light = BaseRigT80B.TurretMarkerLight, Place = new Vector3(-0.077f, 2.422f, -2.271f)},
			new LampMove.Lamp { Light = "front marker lamp L", Place = new Vector3(-0.973f, 1.358f, 1.125f)},
			new LampMove.Lamp { Light = "front marker lamp R", Place = new Vector3(0.973f, 1.358f, 1.125f)},
			new LampMove.Lamp { Light = "tail lamp L", Place = new Vector3(-1.318f, 1.270f, -4.620f)},
			new LampMove.Lamp { Light = "tail lamp R", Place = new Vector3(1.147f, 1.292f, -4.420f)},
		};

		internal static void Apply(Transform root)
		{
			string tag = T80UM2Mod.FriendlyName;
			var turret = RigBones.Turret(root);
			BaseRigT80B.TrimWheels(root, WheelsData());
			if (BaseRigT80B.TrimFailure != null) MelonLogger.Warning($"{tag}: {BaseRigT80B.TrimFailure}; the T-80B's fender flaps may show through the fenders.");
			var gun = RigBones.Gun(root);
			var cover = gun != null ? gun.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "T80UM2_MANTLET_COVER") : null;
			if (cover != null && turret != null) MantletCover.Skin(root, turret, gun, cover);
			try
			{
				if (turret != null) TacticalNumbers.Apply(root, turret, Numbers);
				LampMove.Apply(root, Lamps, new[] { "gs_turret", "gst_illum" });
			}
			catch (Exception e) { MelonLogger.Error($"{tag} number plates / lamps failed: " + e); }
			if (HatchUndersides.Plain(root, "t80um2_hatch_inner") == 0) MelonLogger.Warning($"{tag}: no hatch underside material found (old bundle?).");
			try
			{
				HatchUndersides.Paint(root, "t80um2_hatch_inner", "t80um2_t_80u_turret", new RectInt(832, 2576, 230, 110), new Vector2Int(4096, 4096));
			}
			catch (Exception e) { MelonLogger.Error($"{tag} hatch undersides failed (left plain): " + e); }
			try { VisualLods.Sync(root); }
			catch (Exception e) { MelonLogger.Error($"{tag} LOD materials failed: " + e); }
			MelonCoroutines.Start(ThermalLater(root));
		}

		static byte[] _wheels;
		static bool _wheelsRead;
		static byte[] WheelsData()
		{
			if (_wheelsRead) return _wheels;
			_wheelsRead = true;
			using (var s = typeof(Looks).Assembly.GetManifestResourceStream("T80B_wheels_trimmed.bin"))
			{
				if (s == null) { MelonLogger.Warning($"{T80UM2Mod.FriendlyName}: no trimmed T-80B wheels skin in the DLL; the T-80B's fender flaps may show through the fenders."); return null; }
				using (var m = new System.IO.MemoryStream()) { s.CopyTo(m); _wheels = m.ToArray(); }
			}
			return _wheels;
		}

		// Two frames in, once GHPC's own HeatSources have started.
		static IEnumerator ThermalLater(Transform root)
		{
			yield return null;
			yield return null;
			if (root == null) yield break;
			try { ThermalHeat.Apply(root, ThermalHeat.PortParts(root, "T80UM2_")); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} thermal heat failed: " + e); }
		}
	}
}
