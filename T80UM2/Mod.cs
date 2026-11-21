using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using T80UM2.Common;
using UnityEngine;

[assembly: MelonInfo(typeof(T80UM2.T80UM2Mod), "T-80UM2", "0.3.0", "marsattackk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]
[assembly: HarmonyDontPatchAll]   // off means off: nothing is patched until Enabled has been read

namespace T80UM2
{
	// Loads the bundle, registers the unit on the T80B prefab and hangs the War Thunder model's HULL / TURRET / GUN on the base
	// rig's bones (the base's visuals off, paint, real axes). Armour, rounds and the Drozd go on with the turret at rest, before
	// the axes move. The НСВТ, the sights and the module wiring follow (Conversion.Convert).
	public partial class T80UM2Mod : MelonMod
	{
		internal const string UnitKey = "T80UM2";

		internal static GameObject Visual;   // the War Thunder model, the bundle's T80UM2_visual prefab
		internal static VanillaEdits Edits;   // the T-80B's pieces switched off or changed (Resources\vanilla_edits.txt)
		internal static MaterialRoles Roles;   // material roles: the vanilla glass at spawn
		internal static RigMap Rig;   // moving parts: hatches, the turret and gun axes
		internal static Vector3[] SmokePoints;   // the smoke tubes' launch points (smoke_points: the DLL's copy, else the bundle's)

		internal const string FriendlyName = "T-80UM2";
		const string BaseUnit = "T80B";   // registry key of the vanilla prefab "T80B" (crew, interior, damage model, running gear)
		const string ArmyFrom = "T80B";
		internal const int CoaxRounds = 1250;   // the ПКТ coax's load: 5 belts of 250 (Firepower.PktBelt)
		internal const string NumberPosition = "rear lamp";   // the tactical numbers sit on the turret-rear march lamp

		internal static MelonPreferences_Entry<bool> Enabled;
		// In the Soviet campaign roster, rarer than the T-90M (0.25): one T-80UM2 was built.
		internal static CampaignConfig Campaign;
		internal static MelonPreferences_Entry<string> Apfsds;
		internal static MelonPreferences_Entry<bool> ForceApfsds;
		internal static MelonPreferences_Entry<bool> RandomApfsds;
		internal static MelonPreferences_Entry<string> ApfsdsPool;
		internal static MelonPreferences_Entry<string> Heat;
		internal static MelonPreferences_Entry<bool> ForceHeat;
		internal static MelonPreferences_Entry<bool> RandomHeat;
		internal static MelonPreferences_Entry<string> HeatPool;
		internal static MelonPreferences_Entry<string> AmmunitionLoad;
		internal static MelonPreferences_Entry<string> EmptyRacks;
		internal static MelonPreferences_Entry<bool> FireWhenLaid;   // the gun fires only on the computer's lay
		internal static MelonPreferences_Entry<bool> WeatheredPaint;
		internal static MelonPreferences_Entry<bool> NsvtCases;   // the НСВТ's spent cases
		internal static MelonPreferences_Entry<bool> EraEffects;   // the set-off ERA look
		internal static MelonPreferences_Entry<bool> DrozdOn;   // the «Дрозд» is fitted
		internal static MelonPreferences_Entry<string> NightSight;   // the gunner's night sight: «Агава-2» thermal or «Буран-ПА»

		const string Repo = "marsattackk/GHPC-T80UM2";
		// A changed default needs a rule here. MelonLoader keeps the saved one. None so far.
		static readonly SettingsMigration[] Migrations = { };

		public override void OnInitializeMelon()
		{
			BuildStamp.Log(Info.Name);
			UnityErrors.Install(typeof(T80UM2Mod).Namespace);
			CreateSettings();
			if (!Enabled.Value) return;
			PatchClasses.Apply(this);
			PortAmmo.S = Firepower.Settings();   // the rounds' settings (the mission customiser asks for them at the menu)
			LoadData();
			RegisterUnit();
		}

		// The settings in MelonPreferences.cfg, then the file's version and its migrations.
		void CreateSettings()
		{
			var cfg = MelonPreferences.CreateCategory("T-80UM2");
			Enabled = cfg.CreateEntry("Enabled", true, description: SettingText.Enabled);
			// Rarer than the T-90M, after two missions: one T-80UM2 was built.
			Campaign = CampaignConfig.Create(cfg, FriendlyName, 0.15f, 2);
			// 3BM42, the serial T-80U's round: the T-90s default to 3BM60, whose long rod needs the reworked loading mechanism, and no
			// source gives the T-80UM2 that.
			Apfsds = cfg.CreateEntry("APFSDS", "3BM42", description: SettingText.Apfsds);
			ForceApfsds = cfg.CreateEntry("Force APFSDS", false, description: SettingText.ForceApfsds);
			RandomApfsds = cfg.CreateEntry("Random APFSDS", false, description: SettingText.RandomApfsds);
			ApfsdsPool = cfg.CreateEntry("APFSDS pool", "", description: SettingText.ApfsdsPool);
			Heat = cfg.CreateEntry("HEAT", "3BK29M", description: SettingText.Heat);
			ForceHeat = cfg.CreateEntry("Force HEAT", false, description: SettingText.ForceHeat);
			RandomHeat = cfg.CreateEntry("Random HEAT", false, description: SettingText.RandomHeat);
			HeatPool = cfg.CreateEntry("HEAT pool", "", description: SettingText.HeatPool);
			AmmunitionLoad = cfg.CreateEntry("Ammunition load", "Full", description: SettingText.AmmunitionLoad);
			EmptyRacks = cfg.CreateEntry("Empty racks", "", description: SettingText.EmptyRacks);
			NightSight = cfg.CreateEntry("Night sight", Sights.Agava2, description: SettingText.NightSight);
			FireWhenLaid = cfg.CreateEntry("Fire only when the gun is laid", true, description: SettingText.FireWhenLaid);
			WeatheredPaint = PaintFinish.Create(cfg, FriendlyName);   // "Weathered paint" (PaintFinish, as the T-90s)
			DrozdOn = cfg.CreateEntry("Drozd", true, description: SettingText.Drozd);
			EraEffects = cfg.CreateEntry("ERA destruction effects", true, description: SettingText.EraEffects);
			NsvtCases = cfg.CreateEntry("NSVT spent cases", true, description: SettingText.NsvtCases);
			Armour.Config(cfg);
			UpdateCheck.Config(cfg, FriendlyName, Info.Version, Repo);
			DevInit(cfg);
			PortSettings.Finish(cfg, Info.Version, Migrations);
		}

		// The bundle's prefabs, and the text data (rig, glass, smoke points): the DLL's copy first, the bundle's second.
		static void LoadData()
		{
			var bundle = BundleAssets.Open(Path.Combine(MelonEnvironment.ModsDirectory, "T80UM2", "t80um2"), FriendlyName);
			Visual = BundleAssets.Prefab(bundle, "T80UM2_visual", FriendlyName, required: true);
			Armour.Prefab = BundleAssets.Prefab(
				bundle,
				"T80UM2_armor",
				FriendlyName,
				required: false,
				ifMissing: "armour will not be changed.");
			if (Visual != null) MelonLogger.Msg($"Loaded {FriendlyName} visuals" + (Armour.Prefab != null ? " and armour." : "."));
			Armour.Load(bundle);
			var rig = PortText.Load(bundle, RigMap.AssetName);
			if (rig != null) Rig = RigMap.Parse(rig);
			else MelonLogger.Warning($"{FriendlyName}: no rig map; the hatches stay closed.");
			var roles = PortText.Load(bundle, MaterialRoles.AssetName);
			if (roles != null) Roles = MaterialRoles.Parse(roles);
			else MelonLogger.Warning($"{FriendlyName}: no material roles; glass keeps the bundle's own materials.");
			SmokePoints = SmokeLaunchers.Load(bundle);
			DevLoadData(bundle);
		}

		// The T-80UM2 as a unit of its own: the game's unit lists, the Soviet campaign roster and the Showcase.
		static void RegisterUnit()
		{
			var unit = new UnitDef
			{
				Key = UnitKey,
				FriendlyName = () => FriendlyName,
				BaseUnit = () => BaseUnit,
				ArmyFrom = () => ArmyFrom,
				Enabled = () => Enabled.Value,
				OnSpawn = vehicle => Conversion.Convert(vehicle, atSpawn: true),
				// Other mods convert anything called "T-80" at SkyReady, so this keeps the T-80UM2 out of it.
				ShieldFrom = new[] { "T-80" },
			};
			Campaign.Use(unit);
			UnitRegistry.Add(unit);
			UnitRegistry.TryRegister();
			ShowcaseSpawner.Add(new ShowcaseSpawner.Entry
			{
				Key = UnitKey,
				Name = () => FriendlyName,
				Anchors = new[] { BaseUnit, "T72M1" }
			});
		}

		public override void OnSceneWasInitialized(int buildIndex, string sceneName)
		{
			if (Enabled.Value) UnitRegistry.TryRegister();
		}

		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (!Enabled.Value) return;
			bool menu = MenuScenes.Contains(sceneName);
			UpdateCheck.OnSceneLoaded(menu);
			UnitRegistry.StartCampaignRoster();
			RepairKitCompat.TryPatch(HarmonyInstance);   // RepairKit's repairs reach the modules (when it is installed)
			ShowcaseSpawner.OnSceneLoaded(sceneName);
			// Donor bits copied at the menu: some mods destroy the vanilla prefabs after a mission.
			if (menu)
				DonorParts.WarmAtMenu(
					FriendlyName,
					() => ShtoraScreen.WarmDonors(Countermeasures.Screens),   // the 3Д17's white burst
					() => RemoteMg.Donor.Get(FriendlyName),   // the НСВТ's T-64B station
					() => ThermalSight.WarmDonor(FriendlyName),   // the «Агава-2»'s thermal picture
					() => VanillaMaterials.Warm(Roles));
			DevSceneLoaded(sceneName, menu, HarmonyInstance);
			// A T-80UM2 the spawn patch missed (another mod's spawner) is converted at PlayerReady.
			if (!menu) ConvertSweep.AtPlayerReady(UnitKey, vehicle => Conversion.Convert(vehicle));
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevInit(MelonPreferences_Category cfg);
		static partial void DevLoadData(AssetBundle bundle);
		static partial void DevSceneLoaded(string sceneName, bool menu, HarmonyLib.Harmony harmony);
	}
}
