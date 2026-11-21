using System;
using System.Collections.Generic;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	// The T-80UM2's armour: War Thunder's damage model with my own values, in place of the T-80B's outer armour. The pieces
	// come from the bundle and sit on the T-80B rig's collider groups, and the vanilla edits switch off the T-80B pieces
	// they replace. The T-80B's three compartment volumes get meshes rebuilt from the import's walls, its five fender fuel
	// tanks the shapes of the T-80UM2's tank boxes. Kontakt-5 is ERA with the T-90s' set-off rules. Running gear, internals,
	// hit zones and crew stay the T-80B's. It runs from the conversion before the pivots move: the pieces are placed on
	// their bones at rest.
	internal static partial class Armour
	{
		internal static GameObject Prefab;

		// T-80B pieces that vanilla_edits leaves on: the base's fire wall, off beside the pieces the import replaces.
		static readonly string[] BaseOff = { "Firewall" };

		static bool _warned;
		internal static DmArmour Values;

		// "Armour preset": the shipped values are the game-scale set, cheeks 744 / 984 without ERA (the T-90A's step from
		// its sourced set) and glacis 485 / 653 (the vanilla T-80B's 485 KE). Source-only scales the two calibrated fillings
		// back to the sourced targets (cheeks 620 / 900, glacis 430 / 566), each by the ratio of my two ray solves on the same
		// lines. Cheek filling KE 0.5368 / 0.9778, HEAT 1.5126 / 1.9470. Glacis filling (steel and textolite alike) KE
		// 0.880 / 0.9823, HEAT 1.0 / 1.1416.
		static readonly string[] GlacisFilling =
		{
			"superstructure_front_dm",
			"turret_01_front_dm",
			"turret_02_front_dm",
			"turret_03_front_dm",
			"turret_04_front_dm",
		};

		const float PackageKe = 0.5368f / 0.9778f;
		const float PackageCe = 1.5126f / 1.9470f;
		const float GlacisKe = 0.880f / 0.9823f;
		const float GlacisCe = 1.0f / 1.1416f;

		internal static MelonPreferences_Entry<string> Preset;

		internal static void Config(MelonPreferences_Category cfg)
		{
			Preset = ArmourPresets.Create(
				cfg,
				SettingText.ArmourPreset);
		}

		static readonly ArmourPresets.Pair Presets = new ArmourPresets.Pair(ArmourPresets.GameScale, entry =>
			entry.Variable && entry.Piece == "composite_armor_turret_01_dm" ? (PackageKe, PackageCe)
			: entry.Variable && Array.IndexOf(GlacisFilling, entry.Piece) >= 0 ? (GlacisKe, GlacisCe)
			: ((float, float)?)null);

		// From LoadData, right after the prefabs: the values, the edits and the ERA containers' links and look.
		internal static void Load(AssetBundle bundle)
		{
			var values = PortText.Load(bundle, DmArmour.AssetName);
			if (values != null) Values = DmArmour.Parse(values);
			var edits = PortText.Load(bundle, VanillaEdits.AssetName);
			if (edits != null) T80UM2Mod.Edits = VanillaEdits.Parse(edits);
			// The shared T-80B rig's list as it is now, on top of the vanilla edits' copy of it from the bundle's build (names
			// added since apply without a new bundle).
			if (T80UM2Mod.Edits != null)
				T80UM2Mod.Edits.Off = T80UM2Mod.Edits.Off.Union(BaseRigT80B.ImportReplaces).Union(BaseOff).ToArray();
			// ERA armour to its visual containers.
			EraBricks.LoadLinks(bundle);
			EraBricks.LoadGone(bundle);
			// A set-off container is gone, as on the T-90s.
			EraBricks.Vanish = true;
			EraBricks.VanishRest = name => name.StartsWith("ex_era_turret_");
			EraBricks.Look = () => T80UM2Mod.EraEffects == null || T80UM2Mod.EraEffects.Value;
			if (Prefab != null && Values == null)
				MelonLogger.Warning($"{T80UM2Mod.FriendlyName}: no armour values in the DLL or the bundle; the T-80B's armour stays.");
		}

		// War Thunder's damage model with my values in place of the T-80B's outer armour (the conversion's "armour" row).
		internal static void Apply(Vehicle vehicle)
		{
			var prefab = Prefab;
			if (prefab == null || Values == null) return;
			var root = vehicle.transform;
			var groups = DmArmourApply.GroupsFor(root);
			// The bundle's "reshape|<name>" pieces (the T-80B's volumes and fender tanks), each relative to its rig bone.
			var reshape = new Dictionary<string, (Mesh, Transform)>();
			foreach (var (group, bonePath, _) in groups)
			{
				var piecesOfGroup = prefab.transform.Find(group);
				var bone = root.Find(bonePath);
				if (piecesOfGroup == null || bone == null) continue;
				foreach (Transform piece in piecesOfGroup)
					if (piece.name.StartsWith(VanillaEdits.ReshapeKind + "|"))
						reshape[piece.name.Substring(piece.name.IndexOf('|') + 1)] = (piece.GetComponent<MeshFilter>().sharedMesh, bone);
			}
			var missing = new List<string>();
			if (T80UM2Mod.Edits != null) VanillaEditsApply.Apply(root, T80UM2Mod.Edits, reshape, missing);
			var left = new List<string>();
			string preset = ArmourPresets.Pick(Preset);
			var values = Presets.For(Values, preset);
			DmArmourApply.Apply(vehicle, prefab, values, T80UM2Mod.FriendlyName, left, null, groups);
			if (!_warned && (missing.Count > 0 || left.Count > 0))
			{
				_warned = true;
				MelonLogger.Warning(
					$"{T80UM2Mod.FriendlyName} armour: {missing.Count} T-80B armour piece(s) not found, {left.Count} piece(s) left out "
					+ $"({string.Join("; ", missing.Concat(left))}); the armour may not match the model (game update?).");
			}
			DevApplied(vehicle, values, preset, left);
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevApplied(Vehicle vehicle, DmArmour values, string preset, List<string> problems);
	}
}
