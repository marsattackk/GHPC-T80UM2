using System.Collections.Generic;
using GHPC.Equipment.Optics;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The gunner's «Агава-2» thermal (Т01-П02Т) on the TPN-3-49's slot, through the shared ThermalSight (as the T-90A's ESSA).
	// Inferred on the UM2: its photos show the «Буран-ПА» box, the «Агава-2» was the T-80УМ's / УК's. A 128-element КРТ linear
	// array, 8-12 µm, its picture on a 110 mm CRT monitor («Агава-2», odetievbrony.ru, pp. 6143-6144). It sits on the 1Г46's
	// line of sight, with two fields, a 288-line picture on a 4:3 monitor, white-hot with the polarity switch («переключатели
	// «белый/чёрный»», the same description), the day channel's shake; the fire gate stays on the gun's own lay (GunnerFcs).
	// Every mark is a screen item, so the reticle mesh is off.
	internal static partial class Sights
	{
		// «при кратности увеличения 5,5х 4x2,7°; при кратности увеличения 11х 2x1,35°» («Агава-2», odetievbrony.ru)
		const float AgavaWide = 2.7f, AgavaNarrow = 1.35f;
		// «Разрешение ЭЛТ-монитора составляло 384x288 пикселей» («Агава-2», odetievbrony.ru, p. 6143)
		const int AgavaHeight = 288;
		// 384 across the 4:3 picture, stretched over a 16:9 screen (derived)
		const int AgavaWidth = 512;
		// The mirror's travel -10 / +20 deg («Агава-2», odetievbrony.ru)
		static readonly Vector2 AgavaMirror = new Vector2(-10f, 20f);
		// ±7.5 deg («Агава-2», odetievbrony.ru)
		const float AgavaAzimuth = 7.5f;
		const string AgavaFile = "agava2_monitor.jsonc";
		const string MonitorEdgesFile = "essa_monitor.jsonc";

		// The «Агава-2» through the shared ThermalSight. Null: the thermal donor is missing.
		static string ApplyAgava(UsableOptic day, UsableOptic night, List<string> problems)
		{
			string bound = "";
			bool applied = ThermalSight.Apply(day, night, new ThermalSight.Settings
			{
				Tag = Tag,
				Name = Agava2,
				WideFov = AgavaWide,
				OtherFovs = new[] { AgavaNarrow },
				Width = AgavaWidth,
				Height = AgavaHeight,
				ElevationLimits = AgavaMirror,
				Reticle = optic =>
				{
					if (optic.reticleMesh != null) optic.reticleMesh.gameObject.SetActive(false);
				},
				Readout = optic => bound = AgavaMonitor(optic, problems),
				// A TV picture on a CRT («телевизионный монитор с диагональю 110 мм», «ЭЛТ-монитора»; «Агава-2», pp. 6143-6144)
				Monitor = true,
				Crt = true,
			});
			if (!applied) return null;
			GunnerFcs.AimClamp.SightAzimuth(night, AgavaAzimuth);
			night.name = "Agava-2 thermal sight";
			return $"«Агава-2» (inferred) {AgavaWide} / {AgavaNarrow} deg, {AgavaWidth}x{AgavaHeight}, mirror {AgavaMirror.x:0} / +{AgavaMirror.y:0} deg ±{AgavaAzimuth}, on the 1Г46's line; monitor {bound}";
		}

		// The monitor's marks (agava2_monitor.jsonc) and the 4:3 picture's edges (the T-90A's essa_monitor.jsonc), the range
		// and the two permission lamps bound to the fire control.
		static string AgavaMonitor(UsableOptic optic, List<string> problems)
		{
			var spec = Reticles.Spec(AgavaFile);
			var marks = ReticleBuild.BuildOverlay(spec, optic.transform, (float)spec.Fov, problems);
			var edges = Reticles.Spec(MonitorEdgesFile);
			ReticleBuild.BuildOverlay(edges, optic.transform, (float)edges.Fov, problems);
			return SightReadout.Attach(optic, marks, new SightReadout.Settings
			{
				Range = "range",
				RangePrefix = "<mspace=0.7em>",
				RangeSuffix = "</mspace>",
				// «индикация разрешения измерения дальности»
				LaserLamp = "laser",
				// «индикация разрешения выстрела»
				Ready = "ready",
			}, problems);
		}
	}
}
