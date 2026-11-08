using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GHPC;
using GHPC.Equipment;
using GHPC.Utility;
using GHPC.Vehicle;
using HarmonyLib;
using UnityEngine;

namespace T80UM2.Common
{
	internal static class DmArmourApply
	{
		internal static readonly (string group, string bone, string colliders)[] T72Groups =
		{
			("HULL", "---MESH---/HULL", "HULL COLLIDERS"),
			("TURRET", "---MESH---/HULL/TURRET", "TURRET COLLIDERS"),
			("GUN", "---MESH---/HULL/TURRET/GUN", "GUN COLLIDERS"),
		};

		internal static readonly (string group, string bone, string colliders)[] T80BGroups =
		{
			("HULL", BaseRigT80B.HullPath, "T-80 HULL COLLIDERS"),
			("TURRET", BaseRigT80B.TurretPath, "T-80 TURRET COLLIDERS"),
			("GUN", BaseRigT80B.GunPath, "T-80 GUN COLLIDERS"),
		};

		internal static (string group, string bone, string colliders)[] GroupsFor(Transform root) =>
			root.Find(T72Groups[0].bone) != null || root.Find(BaseRigT80B.HullPath) == null ? T72Groups : T80BGroups;

		internal static void Apply(Vehicle vehicle, GameObject prefab, DmArmour values, string tag, List<string> problems, StringBuilder table = null,
			(string group, string bone, string colliders)[] groups = null)
		{
			if (prefab == null || values == null) { problems.Add("no damage-model pieces or values in the bundle"); return; }
			var root = vehicle.transform;
			var inst = Object.Instantiate(prefab);
			foreach (var (group, bonePath, token) in groups ?? GroupsFor(root))
			{
				var g = inst.transform.Find(group);
				if (g == null) continue;
				var bone = root.Find(bonePath);
				var target = ArmorRoot(vehicle, token);
				if (bone == null || target == null) { problems.Add($"{group}: no rig bone / collider group, {g.childCount} piece(s) left out"); continue; }
				foreach (var piece in g.Cast<Transform>().ToList())
				{
					string[] f = piece.name.Split('|');
					if (f.Length < 2 || !f[0].StartsWith("dm_")) continue;
					var e = values[f[1]];
					if (e == null) { problems.Add($"'{f[1]}': no value, left out"); continue; }
					if (e.Drawn)
					{
						piece.name = f[1];
						Xray(piece.gameObject, e, tag);
						Followers.PlaceOnBone(piece, bone, target);
						continue;
					}
					var codex = Codex(e, tag);
					if (codex == null) { problems.Add($"'{f[1]}': unknown material '{e.Material}', left out"); continue; }
					piece.name = f[1];
					Setup(piece.gameObject, e, codex, vehicle, tag);
					Followers.PlaceOnBone(piece, bone, target);
					table?.AppendLine($"  {group,-6} {f[1],-30} {e.Name,-42} {e.Mode,-9} {(e.Variable ? $"KE x{e.Ke:0.000}/mm HEAT x{e.Ce:0.000}/mm" : $"KE {e.Ke,4:0} mm HEAT {e.Ce,4:0} mm")}"
						+ $"{(e.Era ? $" (after {e.KeAfter:0}/{e.CeAfter:0})" : "")}{(!e.Variable && e.AngleMatters ? " angled" : "")}{(e.Slat ? " slat" : "")} {e.Material}{(e.Xray.Length > 0 ? " x-ray " + e.Xray : "")}  [{e.Source}]");
				}
			}
			Object.Destroy(inst);
		}

		static readonly AccessTools.FieldRef<UniformArmor, string> UName = Reflect.FieldRefAccess<UniformArmor, string>("_name");
		static readonly AccessTools.FieldRef<UniformArmor, ArmorCodexScriptable> UMaterial = Reflect.FieldRefAccess<UniformArmor, ArmorCodexScriptable>("_armorType");
		static readonly AccessTools.FieldRef<UniformArmor, bool> UIsEra = Reflect.FieldRefAccess<UniformArmor, bool>("_isEra");
		static readonly AccessTools.FieldRef<UniformArmor, bool> UIsSlat = Reflect.FieldRefAccess<UniformArmor, bool>("_isSlatArmor");
		static readonly AccessTools.FieldRef<UniformArmor, bool> UNoDecals = Reflect.FieldRefAccess<UniformArmor, bool>("_noImpactDecals");

		static void Setup(GameObject go, DmArmour.Entry e, ArmorCodexScriptable codex, Vehicle vehicle, string tag)
		{
			go.layer = 8;
			go.tag = "Penetrable";
			var col = go.AddComponent<MeshCollider>();
			col.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
			// VariableArmor measures entry to exit through its own mesh, it has to stay non-convex.
			col.convex = e.Convex;
			if (e.Variable)
			{
				var va = go.AddComponent<VariableArmor>();
				va.SetName(e.Name);
				Reflect.Field(typeof(VariableArmor), "_armorType").SetValue(va, codex);
				va.Unit = vehicle;
			}
			else
			{
				var armor = go.AddComponent<UniformArmor>();
				UName(armor) = e.Name;
				UMaterial(armor) = codex;
				UIsEra(armor) = e.Era;
				UIsSlat(armor) = e.Slat;
				UNoDecals(armor) = e.Era;
				armor.ThicknessListed = UniformArmor.ThicknessMode.RHAE;
				armor.PrimarySabotRha = e.Ke;
				armor.PrimaryHeatRha = e.Ce;
				armor.SecondarySabotRha = e.KeAfter;
				armor.SecondaryHeatRha = e.CeAfter;
				armor.AngleMatters = e.AngleMatters;
				armor.DetonatedObjects = new GameObject[0];
				armor.UndetonatedObjects = new GameObject[0];
				armor.Unit = vehicle;
				if (e.Era) EraBricks.Register(armor, vehicle.transform);
			}
			Xray(go, e, tag);
		}

		static void Xray(GameObject go, DmArmour.Entry e, string tag)
		{
			if (e.Xray.Length == 0)
			{
				var r = go.GetComponent<Renderer>();
				if (r != null) r.enabled = false;
				return;
			}
			var aar = go.AddComponent<AarVisual>();
			aar.HideUntilAar = true;
			var mat = XrayMaterial(e.Xray, tag);
			aar.SwitchMaterials = mat != null;
			aar.AarMaterial = mat;
			aar.UpdateVisibility();
		}

		internal const string XrayTemplate = "see-through teal gray";
		static readonly Dictionary<string, Material> _xray = new Dictionary<string, Material>();

		internal static Material XrayMaterial(string spec, string tag)
		{
			if (_xray.TryGetValue(spec, out var m) && m != null) return m;
			// No handle on GHPC's x-ray material, but the T-72M1's cheek array carries it, so any loaded AarVisual will do.
			var from = Resources.FindObjectsOfTypeAll<AarVisual>().FirstOrDefault(v => v.AarMaterial != null && v.AarMaterial.name == XrayTemplate);
			m = from != null ? from.AarMaterial : null;
			if (m == null) return null;
			if (spec != "template")
			{
				var c = spec.Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
				if (c.Length < 3) return null;
				m = new Material(m) { name = $"{tag} see-through {spec}", color = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 0.5f), hideFlags = HideFlags.DontUnloadUnusedAsset };
			}
			return _xray[spec] = m;
		}

		internal static readonly string[] Materials = DmArmour.Materials;
		static readonly Dictionary<string, ArmorCodexScriptable> _codex = new Dictionary<string, ArmorCodexScriptable>();

		// Our own codex assets. The vanilla ones are shared by every tank in the game, never edit them.
		static ArmorCodexScriptable Codex(DmArmour.Entry e, string tag)
		{
			if (!Materials.Contains(e.Material)) return null;
			string key = e.Variable ? $"{e.Material}|{e.Ke.ToString("R", CultureInfo.InvariantCulture)}|{e.Ce.ToString("R", CultureInfo.InvariantCulture)}" : e.Material;
			if (_codex.TryGetValue(key, out var c) && c != null) return c;
			float ke = e.Variable ? e.Ke : 1f, ce = e.Variable ? e.Ce : 1f;
			var t = new ArmorType
			{
				Name = e.Material.Replace('_', ' '), ThicknessSource = ArmorType.RhaSource.Multipliers, RhaeMultiplierKe = ke, RhaeMultiplierCe = ce,
				CanRicochet = false, NormalizesHits = true, CanShatterLongRods = false, HarderIsBetter = false,
				SpallAngleMultiplier = 1f, SpallPowerMultiplier = 1f, CrushThicknessModifier = 1f,
			};
			switch (e.Material)
			{
				case "armour_steel":
				case "mild_steel":
					bool armour = e.Material == "armour_steel";
					t.Name = armour ? "armor steel" : "mild steel";
					t.BHN = armour ? 313f : 129f;
					if (!e.Variable) t.ThicknessSource = ArmorType.RhaSource.BHN;
					t.CanShatterLongRods = armour; t.CanRicochet = true;
					break;
				case "plain": break;
				case "composite":
					t.CanRicochet = true; t.CanShatterLongRods = true; t.SpallAngleMultiplier = 0.5f; t.SpallPowerMultiplier = 0.5f;
					break;
				case "filling":
				case "filling_steel":
					bool steel = e.Material == "filling_steel";
					t.NormalizesHits = false; t.CanShatterLongRods = steel; t.BHN = steel ? 450f : 100f; t.SpallAngleMultiplier = 0.5f; t.SpallPowerMultiplier = 0.5f;
					break;
				case "liner":
					t.NormalizesHits = false; t.HarderIsBetter = true; t.BHN = 2f; t.SpallAngleMultiplier = 0.33f; t.SpallPowerMultiplier = 0.9f; t.CrushThicknessModifier = 0.05f;
					break;
			}
			var codex = ScriptableObject.CreateInstance<ArmorCodexScriptable>();
			codex.name = $"{tag} {t.Name}{(e.Variable ? $" x{ke:0.###}/{ce:0.###} per mm" : "")}";
			codex.ArmorType = t;
			Object.DontDestroyOnLoad(codex);
			return _codex[key] = codex;
		}

		internal static Transform ArmorRoot(Vehicle vehicle, string token)
		{
			Transform follower = vehicle.GetComponentsInChildren<LateFollowTarget>(true)
				.SelectMany(t => t.LateFollowers)
				.Where(f => f != null && f.name.Contains(token))
				.Select(f => f.transform)
				.FirstOrDefault()
				?? vehicle.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains(token));
			if (follower == null) return null;
			return follower.Find("ARMOR") ?? follower;
		}
	}
}
