using System.Linq;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// What the T-80UM2 shows beyond its body: the T-80B's number plates, lamps and lamp glows on the War Thunder model's lamps,
	// the hatch lids' undersides painted. Run at spawn after the paint, before PortAxes moves the pivots (places are prefab
	// space with the turret at rest). The driver's hatch opens through HatchRig (the T-80B's dormant AnimatedPart woken); the
	// stub needs nothing: the T-80B has no ejector (its autoloader puts the stub back in the carousel tray, as the real
	// T-80's does).
	internal static partial class Conversion
	{
		// The turret-rear march-number lamp (War Thunder ex_lantern_b_01): its glass (headlight_glass, 15 x 15 cm) has its area
		// centre at (-0.195, 2.440) and its outermost face at z -2.269; the plates sit 7 mm outside it, as on the T-90s.
		// Turret-bone space: prefab minus the T-80B's turret bone (0, 1.412, -1.1715). The T-80B's plates (12.4 x 8.2 cm, as the
		// T-72's) x1.2 fill the window. The lamp has two windows (as the T-90M's), centres 6.3 cm apart (measured against
		// the model's lamp face): the T-80B's 7.3 cm put the right digit over the frame.
		static readonly TacticalNumbers.Settings Numbers = new TacticalNumbers.Settings
		{
			Mode = T80UM2Mod.NumberPosition,
			LampFace = new Vector3(-0.195f, 2.440f - 1.412f, -2.276f + 1.1715f),
			LampNormal = new Vector3(0f, 0f, -1f),
			DigitScale = 1.2f,
			DigitSpacing = 0.063f,   // the T-80B's 7.3 cm puts the right digit over the lamp's frame
			PlateNames = new[] { "march light" },   // "march light" (lit) and "march light non illum"
		};

		// The T-80B's lights onto the War Thunder lamps (box centres, a couple of cm outside each lens as the T-80B's sit off
		// theirs). Which War Thunder lamp is which is read from where it sits (no source names them): inferred.
		static readonly LampMove.Lamp[] Lamps =
		{
			// The glow quad sits 1.2 cm above and 2.6 cm in front of this light (T-80B), so the light goes there below the lens
			// ring's centre (-0.077, 2.434). The small lamp right of the number lamp, ex_lantern_b_02 (inferred).
			new LampMove.Lamp { Light = BaseRigT80B.TurretMarkerLight, Place = new Vector3(-0.077f, 2.422f, -2.271f)},
			// ex_lantern_03 (inferred)
			new LampMove.Lamp { Light = "front marker lamp L", Place = new Vector3(-0.973f, 1.358f, 1.125f)},
			// ex_lantern_04 (inferred)
			new LampMove.Lamp { Light = "front marker lamp R", Place = new Vector3(0.973f, 1.358f, 1.125f)},
			// ex_lantern_b_04 (inferred)
			new LampMove.Lamp { Light = "tail lamp L", Place = new Vector3(-1.318f, 1.270f, -4.620f)},
			// ex_lantern_b_03 (inferred)
			new LampMove.Lamp { Light = "tail lamp R", Place = new Vector3(1.147f, 1.292f, -4.420f)},
		};

		// The kept T-80B running gear without its fender and mud flaps.
		static void FitRunningGear(Parts parts)
		{
			BaseRigT80B.TrimWheels(parts.Root, WheelsSkin());
			if (BaseRigT80B.TrimFailure != null)
				MelonLogger.Warning(
					$"{T80UM2Mod.FriendlyName}: {BaseRigT80B.TrimFailure}; the T-80B's fender flaps may show through the fenders.");
		}

		// The canvas mantlet cover (War Thunder ex_decor_01, on the gun bone): skinned between turret and gun so it
		// stretches with elevation (MantletCover, as the T-90s); before PortAxes, as theirs.
		static void SkinMantletCover(Parts parts)
		{
			var turret = RigBones.Turret(parts.Root);
			var gun = RigBones.Gun(parts.Root);
			var cover = gun != null
				? gun.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "T80UM2_MANTLET_COVER")
				: null;
			if (cover != null && turret != null) MantletCover.Skin(parts.Root, turret, gun, cover);
		}

		static void PlaceNumbers(Parts parts)
		{
			var turret = RigBones.Turret(parts.Root);
			if (turret != null) TacticalNumbers.Apply(parts.Root, turret, Numbers);
		}

		static void PlaceLamps(Parts parts)
		{
			LampMove.Apply(parts.Root, Lamps, new[] { "gs_turret", "gst_illum" }, T80UM2Mod.FriendlyName);
		}

		// Hatch undersides (HatchUndersides): the plain fallback, then the turret's paint from a smooth plate of the
		// t_80u_turret atlas (4096 x 4096): x 832-1062, y 2576-2686 px (picked because its worn albedo has no seam and
		// it is flat).
		static void PaintHatchUndersides(Parts parts)
		{
			if (HatchUndersides.Plain(parts.Root, "t80um2_hatch_inner") == 0)
				MelonLogger.Warning($"{T80UM2Mod.FriendlyName}: no hatch underside material found (old bundle?).");
			HatchUndersides.Paint(
				parts.Root,
				"t80um2_hatch_inner",
				"t80um2_t_80u_turret",
				new RectInt(832, 2576, 230, 110),
				new Vector2Int(4096, 4096));
		}

		// Distance level (the bundle's LODGroups): LOD1 gets the parts' final materials.
		static void SyncLods(Parts parts)
		{
			VisualLods.Sync(parts.Root, T80UM2Mod.FriendlyName);
		}

		static byte[] _wheels;
		static bool _wheelsRead;
		static byte[] WheelsSkin()
		{
			if (_wheelsRead) return _wheels;
			_wheelsRead = true;
			using (var stream = typeof(Conversion).Assembly.GetManifestResourceStream("T80B_wheels_trimmed.bin"))
			{
				if (stream == null)
				{
					MelonLogger.Warning(
						$"{T80UM2Mod.FriendlyName}: no trimmed T-80B wheels skin in the DLL; the T-80B's fender flaps may show through the fenders.");
					return null;
				}
				using (var memory = new System.IO.MemoryStream())
				{
					stream.CopyTo(memory);
					_wheels = memory.ToArray();
				}
			}
			return _wheels;
		}
	}
}
