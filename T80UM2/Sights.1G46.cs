using System;
using System.Collections.Generic;
using System.Linq;
using GHPC.Equipment.Optics;
using GHPC.Weaponry;
using Reticle;
using T80UM2.Common;

namespace T80UM2
{
	// The gunner's 1Г46 of the 1А45 «Иртыш» on the 1G42's slot: 2.7-12x, 20 to 4.5 deg («Комплекс управления огнем 1А45»,
	// odetievbrony.ru). The reticle is the description's Рис. 9, with stadia for a 2.5 m target and its readout strip. The
	// view, laser and beam stay at the 1G42's place on the gun, as in the T-90A.
	internal static partial class Sights
	{
		const string DayFile = "1g46_day.jsonc";

		// The 1Г46 on the 1G42's slot: reticle, zoom, readout strip (the 1G42's own canvas off).
		static void ApplyDay(UsableOptic day, Dictionary<string, AmmoCodexScriptable> rounds, List<string> problems, Fitted station)
		{
			var spec = Reticles.Spec(DayFile);
			var host = day.reticleMesh.reticleSO;
			day.reticleMesh.reticleSO = Reticles.Build(DayFile, host, host, rounds);
			ReticleMeshes.ClearSmr(day.reticleMesh);
			day.reticleMesh.Load();
			var fovs = spec.Nums("ZoomFovs").Select(fov => (float)fov).ToArray();
			day.slot.DefaultFov = fovs[0];
			day.slot.OtherFovs = fovs.Skip(1).ToArray();
			// Continuous 2.7-12x
			day.slot.AllowFreeZoom = true;
			// Horizontal fields, as the 1G42's: the eyepiece circle is the real field.
			day.slot.fovAspect = true;
			SightMotion.Stabilised(day.slot);
			var oldCanvas = day.transform.Find("1G42 Canvas");
			if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
			var overlay = ReticleBuild.BuildOverlay(spec, day.transform, (float)spec.Fov, problems);
			SightReadout.Attach(day, overlay, new SightReadout.Settings
			{
				Range = "range",
				RangeQuantize = 5,
				RangePrefix = "<mspace=0.9em>",
				RangeSuffix = "</mspace>",
				Ready = "ready",
				Ammo = "ammo",
				AmmoText = SightReadout.Glyph7,
				// The red lamp: the commander has the gun through «Дубль» or target designation (Рис. 9 of «Комплекс управления
				// огнем 1А45»), or GHPC's own override is on.
				Lit = new (string, Func<bool>)[]
				{
					("override", () => station.Dubl
						|| (station.Cu != null && station.Cu.Held)
						|| (station.Fcs != null && station.Fcs.IsOverridingNow)),
				},
			}, problems);
			// The 1G42's lamp object is in its hidden canvas
			day.OverridingObject = null;
		}
	}
}
