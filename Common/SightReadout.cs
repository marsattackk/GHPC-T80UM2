using System;
using System.Collections.Generic;
using GHPC.Equipment.Optics;
using GHPC.Weapons;
using TMPro;
using UnityEngine;

namespace T80UM2.Common
{
	internal sealed class SightReadout : MonoBehaviour
	{
		internal sealed class Settings
		{
			public string Range;
			public string RangeFormat = "0000";
			public int RangeQuantize = 10;
			public string RangePrefix = "", RangeSuffix = "";
			public string Ready;
			public string Ammo;
			public Func<AmmoType, string> AmmoText;
			public string LaserLamp;
			public float LaserRecharge = 2f;
			public (string name, Func<bool> lit)[] Lit = new (string, Func<bool>)[0];
		}

		// Seven segments can't do Cyrillic: 6 for Б, K for К, 0 for О, Y for У.
		internal static string Glyph7(AmmoType ammo) =>
			ammo.Guidance != AmmoType.GuidanceType.Unguided ? "Y"
			: ammo.Category == AmmoType.AmmoCategory.Penetrator ? "6"
			: ammo.Category == AmmoType.AmmoCategory.ShapedCharge ? "K"
			: "0";

		readonly List<(GameObject lamp, Func<bool> lit)> _lit = new List<(GameObject, Func<bool>)>();

		FireControlSystem _fcs;
		TextMeshProUGUI _ammo;
		Func<AmmoType, string> _ammoText;
		GameObject _laser;
		float _laserRecharge, _laserDarkUntil = -1f;

		internal static string Attach(UsableOptic optic, ReticleBuild.Overlay o, Settings s, List<string> problems)
		{
			var bound = new List<string>();
			GameObject Item(string name)
			{
				if (string.IsNullOrEmpty(name)) return null;
				if (o.Readouts.TryGetValue(name, out var t)) return t.gameObject;
				if (o.Lamps.TryGetValue(name, out var l)) return l;
				problems.Add($"no readout or lamp '{name}' in the description");
				return null;
			}
			if (!string.IsNullOrEmpty(s.Range))
			{
				if (o.Readouts.TryGetValue(s.Range, out var range))
				{
					optic.RangeText = range;   // GHPC writes the range into it from here on
					optic.RangeTextQuantize = s.RangeQuantize;
					optic.RangeTextDivideBy = 1;
					optic.RangeTextArchetype = s.RangeFormat;
					optic.RangeTextPrefix = s.RangePrefix;
					optic.RangeTextSuffix = s.RangeSuffix;
					optic.InvalidRangeBlink = true;
					optic.ZeroOutInvalidRange = true;
					bound.Add($"range '{s.Range}'");
				}
				else problems.Add($"no readout '{s.Range}' for the range");
			}
			var ready = Item(s.Ready);
			if (ready != null)
			{
				optic.ReadyToFireObject = ready;
				ready.SetActive(false);
				bound.Add($"ready '{s.Ready}'");
			}
			var r = o.Root.AddComponent<SightReadout>();
			r._fcs = optic.FCS;
			if (!string.IsNullOrEmpty(s.Ammo) && s.AmmoText != null)
			{
				if (o.Readouts.TryGetValue(s.Ammo, out var ammo)) { r._ammo = ammo; r._ammoText = s.AmmoText; bound.Add($"round '{s.Ammo}'"); }
				else problems.Add($"no readout '{s.Ammo}' for the round");
			}
			r._laser = Item(s.LaserLamp);
			if (r._laser != null)
			{
				r._laserRecharge = s.LaserRecharge;
				r._laser.SetActive(true);
				if (r._fcs != null) r._fcs.LaserFired += r.OnLase;
				bound.Add($"laser lamp '{s.LaserLamp}' ({s.LaserRecharge:0.#} s)");
			}
			foreach (var (name, lit) in s.Lit ?? new (string, Func<bool>)[0])
			{
				var lamp = Item(name);
				if (lamp == null || lit == null) continue;
				lamp.SetActive(false);
				r._lit.Add((lamp, lit));
				bound.Add($"lamp '{name}'");
			}
			return string.Join(", ", bound);
		}

		void OnLase() => _laserDarkUntil = Time.time + _laserRecharge;

		void OnDestroy()
		{
			if (_fcs != null && _laser != null) _fcs.LaserFired -= OnLase;
		}

		void Update()
		{
			if (_ammo != null && _fcs != null)
			{
				var a = _fcs.CurrentAmmoType;
				string t = a != null ? _ammoText(a) : "";
				if (_ammo.text != t) _ammo.text = t;
			}
			if (_laser != null)
			{
				bool lit = Time.time >= _laserDarkUntil;
				if (_laser.activeSelf != lit) _laser.SetActive(lit);
			}
			foreach (var (lamp, test) in _lit)
			{
				bool on = false;
				try { on = test(); } catch { }
				if (lamp != null && lamp.activeSelf != on) lamp.SetActive(on);
			}
		}
	}
}
