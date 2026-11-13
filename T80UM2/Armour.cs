using System;
using System.Collections.Generic;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static class Armour
	{
		internal static readonly InteriorMap.Embedded Interior = new InteriorMap.Embedded(typeof(T80UM2Mod).Assembly, T80UM2Mod.FriendlyName, "T80UM2", "T-80B");
		internal static readonly PortModules Modules = new PortModules(typeof(T80UM2Mod).Assembly, T80UM2Mod.FriendlyName, "T-80B", "T80UM2");

		static bool _loaded, _warned;
		static DmArmour _values;

		static readonly string[] GlacisStack = { "superstructure_front_dm", "turret_01_front_dm", "turret_02_front_dm", "turret_03_front_dm", "turret_04_front_dm" };
		// Source-only scales just the two fillings back, each by the ratio of its two solves on the same lines.
		const float CheekKe = 0.5368f / 0.9778f, CheekCe = 1.5126f / 1.9470f;
		const float GlacisKe = 0.880f / 0.9823f, GlacisCe = 1.0f / 1.1416f;
		internal static MelonPreferences_Entry<string> Preset;
		internal static void CreatePreset(MelonPreferences_Category cfg) => Preset = ArmourPresets.Create(cfg, "game-scale (default: matched to GHPC's other tanks; turret 744/984, glacis 485/653 without ERA) or source-only (open-source figures; turret 620/900, glacis 430/566). For T-80UM2s spawned after the change.");
		static readonly ArmourPresets.Pair Presets = new ArmourPresets.Pair(ArmourPresets.GameScale, c =>
			c.Variable && c.Piece == "composite_armor_turret_01_dm" ? (CheekKe, CheekCe)
			: c.Variable && Array.IndexOf(GlacisStack, c.Piece) >= 0 ? (GlacisKe, GlacisCe)
			: ((float, float)?)null);
		static VanillaEdits _edits;

		static void Load()
		{
			if (_loaded) return;
			_loaded = true;
			var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b => b.name == "t80um2");
			if (bundle == null) return;
			var values = bundle.LoadAsset<TextAsset>(DmArmour.AssetName);
			if (values != null) _values = DmArmour.Parse(values.text);
			var edits = bundle.LoadAsset<TextAsset>(VanillaEdits.AssetName);
			if (edits != null) _edits = VanillaEdits.Parse(edits.text);
			if (_edits != null) _edits.Off = _edits.Off.Union(BaseRigT80B.ImportReplaces).Union(new[] { "Firewall" }).ToArray();
			EraBricks.LoadLinks(bundle.LoadAsset<TextAsset>(EraBricks.LinksAsset));
			EraBricks.LoadGone(bundle.LoadAsset<TextAsset>(EraBricks.GoneAsset));
			EraBricks.Vanish = true;
			EraBricks.VanishRest = n => n.StartsWith("ex_era_turret_");
			EraBricks.Look = () => T80UM2Mod.EraEffects == null || T80UM2Mod.EraEffects.Value;
			if (T80UM2Mod.Armor != null && _values == null) MelonLogger.Warning($"{T80UM2Mod.FriendlyName}: bundle has no armour values; the T-80B's armour stays.");
		}

		internal static void Apply(Vehicle vehicle, bool modules = true)
		{
			try { Pieces(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} armour failed (the T-80B's kept): " + e); }
			try { Interior.Apply(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} interior failed (the T-80B's places kept): " + e); }
			// Interior after the armour, before the racks fill. The NSVT's hit zone waits for its cradle, Nsvt builds that one.
			if (modules) try { Modules.Build(vehicle, "NSVT"); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} modules failed: " + e); }
		}

		static void Pieces(Vehicle vehicle)
		{
			Load();
			var prefab = T80UM2Mod.Armor;
			if (prefab == null || _values == null) return;
			var root = vehicle.transform;
			var groups = DmArmourApply.GroupsFor(root);
			var reshape = new Dictionary<string, (Mesh, Transform)>();
			foreach (var (group, bonePath, _) in groups)
			{
				var g = prefab.transform.Find(group);
				var bone = root.Find(bonePath);
				if (g == null || bone == null) continue;
				foreach (Transform p in g)
					if (p.name.StartsWith(VanillaEdits.ReshapeKind + "|")) reshape[p.name.Substring(p.name.IndexOf('|') + 1)] = (p.GetComponent<MeshFilter>().sharedMesh, bone);
			}
			var missing = new List<string>();
			if (_edits != null) VanillaEditsApply.Apply(root, _edits, reshape, missing);
			var left = new List<string>();
			string preset = ArmourPresets.Pick(Preset);
			var values = Presets.For(_values, preset);
			DmArmourApply.Apply(vehicle, prefab, values, T80UM2Mod.FriendlyName, left, null, groups);
			if (!_warned && (missing.Count > 0 || left.Count > 0))
			{
				_warned = true;
				MelonLogger.Warning($"{T80UM2Mod.FriendlyName} armour: {missing.Count} T-80B armour piece(s) not found, {left.Count} piece(s) left out "
					+ $"({string.Join("; ", missing.Concat(left))}); the armour may not match the model (game update?).");
			}
		}
	}
}
