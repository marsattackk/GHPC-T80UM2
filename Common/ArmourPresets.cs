using System;
using MelonLoader;

namespace T80UM2.Common
{
	internal static class ArmourPresets
	{
		internal const string GameScale = "game-scale", SourceOnly = "source-only";

		internal static MelonPreferences_Entry<string> Create(MelonPreferences_Category cfg, string description)
			=> cfg.CreateEntry("Armour preset", GameScale, description: description);

		internal static string Pick(MelonPreferences_Entry<string> entry)
		{
			string preset = entry?.Value ?? GameScale;
			return string.Equals(preset?.Trim(), SourceOnly, StringComparison.OrdinalIgnoreCase) ? SourceOnly : GameScale;
		}

		internal static DmArmour Scaled(DmArmour values, Func<DmArmour.Entry, (float ke, float ce)?> factor)
		{
			var d = new DmArmour();
			foreach (var e in values.Entries)
			{
				var c = new DmArmour.Entry
				{
					Piece = e.Piece, Mode = e.Mode, Name = e.Name, Material = e.Material, Xray = e.Xray, Source = e.Source,
					Ke = e.Ke, Ce = e.Ce, KeAfter = e.KeAfter, CeAfter = e.CeAfter, AngleMatters = e.AngleMatters, Slat = e.Slat,
				};
				if (factor(c) is (float ke, float ce)) { c.Ke *= ke; c.Ce *= ce; }
				d.Add(c);
			}
			return d;
		}

		internal sealed class Pair
		{
			readonly string _bundlePreset;
			readonly Func<DmArmour.Entry, (float ke, float ce)?> _toOther;
			DmArmour _from, _other;

			internal Pair(string bundlePreset, Func<DmArmour.Entry, (float ke, float ce)?> toOther)
			{
				_bundlePreset = bundlePreset;
				_toOther = toOther;
			}

			internal DmArmour For(DmArmour bundle, string preset)
			{
				if (bundle == null || preset == _bundlePreset) return bundle;
				if (!ReferenceEquals(bundle, _from)) { _from = bundle; _other = Scaled(bundle, _toOther); }
				return _other;
			}
		}
	}
}
