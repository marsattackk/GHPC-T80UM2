using System.IO;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using MelonLoader.Utils;
using T80UM2.Common;
using UnityEngine;

[assembly: MelonInfo(typeof(T80UM2.T80UM2Mod), "T-80UM2", "0.1.1", "marsattackk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace T80UM2
{
	public class T80UM2Mod : MelonMod
	{
		internal const string UnitKey = "T80UM2";
		internal const string FriendlyName = "T-80UM2";
		const string BaseUnit = "T80B";
		const string ArmyFrom = "T80B";

		static readonly (string group, string path)[] Bones = { ("HULL", "T80B_rig/HULL"), ("TURRET", "T80B_rig/HULL/TURRET"), ("GUN", "T80B_rig/HULL/TURRET/gun"),
			("MANTLET_COVER", "T80B_rig/HULL/TURRET/gun") };
		static readonly string[] Painted = { "t80um2_era_kontakt_5_t_80u", "t80um2_t_80u_gun", "t80um2_t_80u_turret", "t80um2_mg_mount_nsvt_t_80u", "t80um2_t_80u_body", "t80um2_t_80um2_turret" };

		internal static GameObject Visual, Armor;
		internal static RigMap Rig;
		internal static MaterialRoles Roles;
		internal static Vector3[] SmokePoints;
		internal static MelonPreferences_Entry<bool> Enabled;
		internal static MelonPreferences_Entry<bool> WeatheredPaint;
		internal static CampaignConfig Campaign;
		internal static MelonPreferences_Entry<bool> FireWhenLaid;
		internal static MelonPreferences_Entry<bool> EraEffects;
		internal static MelonPreferences_Entry<bool> NsvtCases;
		internal static MelonPreferences_Entry<bool> DrozdOn;
		internal static MelonPreferences_Entry<string> NightSight;
		static readonly SettingsMigration[] Migrations = { };

		static readonly string[] MenuScenes = { "MainMenu2_Scene", "MainMenu2-1_Scene", "LOADER_MENU", "LOADER_INITIAL", "t64_menu", "leo2_menu" };

		public override void OnInitializeMelon()
		{
			BuildStamp.Log(Info.Name);
			UnityErrors.Install(typeof(T80UM2Mod).Namespace);
			var cfg = MelonPreferences.CreateCategory("T-80UM2");
			Enabled = cfg.CreateEntry("Enabled", true, description: "Adds the T-80UM2 as its own unit (Instant Action, campaign roster, ATAC key \"T80UM2\").");
			Campaign = CampaignConfig.Create(cfg, FriendlyName, 0.15f, 2);
			WeatheredPaint = PaintFinish.Create(cfg, FriendlyName);
			Firepower.Init(cfg);
			DrozdOn = cfg.CreateEntry("Drozd", true, description: "true (default): the Drozd active protection is fitted (two radar heads, four launcher blocks, 8 rounds; switched on and off in play from the vehicle menu). false = the tank has the blocks but no active protection. For T-80UM2s spawned after the change.");
			FireWhenLaid = cfg.CreateEntry("Fire only when the gun is laid", true, description: "true (default): after a lase the trigger does nothing until the gun has climbed to the elevation the computer set, as the real firing-permission circuit. false = the gun fires at any time, as the game's own tanks do.");
			EraEffects = cfg.CreateEntry("ERA destruction effects", true, description: "true (default): a set-off Kontakt-5 container is gone with a blast and the plate round it sooted. false = it looks as before (it still works as armour, spent after one hit).");
			NsvtCases = cfg.CreateEntry("NSVT spent cases", true, description: "true (default): the commander's NSVT drops its spent cases into the canvas catcher bag beside the gun. false = no cases. For T-80UM2s spawned after the change.");
			NightSight = cfg.CreateEntry("Night sight", Sights.Agava2, description: "Agava-2 (default) = the gunner's thermal sight of the T-80UM (a guess for the T-80UM2: its photos show the older image intensifier); Buran-PA = that image intensifier, as the photos show. Can be changed in play.");
			Armour.CreatePreset(cfg);
			PortSettings.Finish(cfg, Info.Version, Migrations);
			if (!Enabled.Value) return;
			var bundle = BundleAssets.Open(Path.Combine(MelonEnvironment.ModsDirectory, "T80UM2", "t80um2"), FriendlyName);
			Visual = BundleAssets.Prefab(bundle, "T80UM2_visual", FriendlyName, required: true);
			Armor = BundleAssets.Prefab(bundle, "T80UM2_armor", FriendlyName, required: false, ifMissing: "armour will not be changed.");
			if (Visual != null) MelonLogger.Msg($"Loaded {FriendlyName} visuals" + (Armor != null ? " and armour." : "."));
			var rig = bundle != null ? bundle.LoadAsset<TextAsset>(RigMap.AssetName) : null;
			if (rig != null) Rig = RigMap.Parse(rig.text);
			var roles = bundle != null ? bundle.LoadAsset<TextAsset>(MaterialRoles.AssetName) : null;
			if (roles != null) Roles = MaterialRoles.Parse(roles.text);
			SmokePoints = SmokeLaunchers.Load(bundle);

			var unit = new UnitDef
			{
				Key = UnitKey,
				FriendlyName = () => FriendlyName,
				BaseUnit = () => BaseUnit,
				ArmyFrom = () => ArmyFrom,
				Enabled = () => Enabled.Value,
				OnSpawn = v => Convert(v, atSpawn: true),
				// Other mods convert anything called "T-80" at SkyReady, this keeps ours out of it.
				ShieldFrom = new[] { "T-80" },
			};
			Campaign.Use(unit);
			UnitRegistry.Add(unit);
			UnitRegistry.TryRegister();
			ShowcaseSpawner.Add(new ShowcaseSpawner.Entry { Key = UnitKey, Name = () => FriendlyName, Anchors = new[] { BaseUnit, "T80B", "T72M1" } });
		}

		public override void OnSceneWasInitialized(int buildIndex, string sceneName)
		{
			if (Enabled.Value) UnitRegistry.TryRegister();
		}

		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (!Enabled.Value) return;
			bool menu = MenuScenes.Contains(sceneName);
			UnitRegistry.StartCampaignRoster();
			RepairKitCompat.TryPatch(HarmonyInstance);
			ShowcaseSpawner.OnSceneLoaded(sceneName);
			// Donor bits copied at the menu: some mods destroy the vanilla prefabs after a mission.
			if (menu)
				DonorParts.WarmAtMenu(FriendlyName,
					() => ShtoraScreen.WarmDonors(Firepower.Smoke),
					() => RemoteMg.Donor.Get(FriendlyName),
					() => ThermalSight.WarmDonor(FriendlyName),
					() => VanillaMaterials.Warm(Roles));
			if (!menu) ConvertSweep.AtPlayerReady(UnitKey, v => Convert(v));
		}

		static void Convert(Vehicle vehicle, bool atSpawn = false)
		{
			if (Visual == null || vehicle == null) return;
			if (!ConvertSweep.Mark(vehicle)) return;
			if (!atSpawn) MelonLogger.Warning($"{FriendlyName}: {vehicle.name} was converted after it spawned; it keeps the T-80B's ammunition, and has no Drozd or NSVT.");
			BaseRigT80B.HideBaseVisuals(vehicle.transform, "T80UM2_", keepLamps: true);
			Mobility.Apply(vehicle);
			VisualParts.Attach(vehicle.transform, Visual, "T80UM2_", Bones);
			PaintFinish.Apply(WeatheredPaint);
			var template = BaseRigT80B.FindTemplate(vehicle.transform);
			var flir = Shader.Find("Standard (FLIR)");
			foreach (var part in vehicle.transform.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("T80UM2_")).ToArray())
				if (part.parent == null || !part.parent.name.StartsWith("T80UM2_")) VehiclePaint.ApplyPaint(part.gameObject, template, flir, Painted);
			if (template == null) MelonLogger.Warning($"{FriendlyName}: no T-80B paint template found; the hull stays on the bundle's material.");
			VanillaMaterials.Apply(vehicle.transform, Roles);
			Looks.Apply(vehicle.transform);
			CrewVoices.Mark(vehicle, FriendlyName);
			SmokeLaunchers.Seat(vehicle, SmokePoints);
			SmokeCaps.Apply(vehicle.transform);
			// A spawned unit's launcher keeps its empty crew cache and never passes GHPC's crew check unless it's refreshed.
			MelonCoroutines.Start(SmokeLaunchers.RefreshCrewLater(vehicle, FriendlyName));
			// Order matters from here: armour, rounds and Drozd go on with the turret at rest, before PortAxes moves the pivots.
			Armour.Apply(vehicle, modules: atSpawn);
			if (atSpawn) Firepower.Apply(vehicle);
			HatchRig.Apply(vehicle.transform, Rig, "T80UM2_", FriendlyName);
			var axes = PortAxes.FromRig(Rig, BaseRigT80B.TurretPivot, BaseRigT80B.GunPivot, FriendlyName);
			if (axes != null) PortAxes.Apply(vehicle, axes);
			else MelonLogger.Warning($"{FriendlyName}: bundle's rig map has no axes; the turret turns on the T-80B's pivots.");
			if (atSpawn && DrozdOn.Value) Drozd.Apply(vehicle);
			if (atSpawn) Nsvt.Apply(vehicle);
			Sights.Apply(vehicle);
			if (atSpawn) MelonCoroutines.Start(Drozd.WireModulesLater(vehicle));
		}
	}
}
