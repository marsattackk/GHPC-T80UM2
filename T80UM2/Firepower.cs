using System;
using GHPC.Vehicle;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static class Firepower
	{
		internal static readonly int[] Counts = { 19, 9, 8, 4 };   // mine, no source gives a regulation mix
		internal static readonly int[] ConveyorCounts = { 14, 6, 6, 0 };
		internal static readonly int[] WetRackCounts = { 5, 3, 2, 0 };

		internal const float SvinetsPrevalence = 0.5f;

		internal static MelonPreferences_Entry<string> Apfsds;

		internal static void Init(MelonPreferences_Category cfg)
		{
			Apfsds = cfg.CreateEntry("APFSDS", "3BM42", description: "Default APFSDS where the game does not choose one (the mission customizer's ammo dropdown and the campaign's ammo supply override it): 3BM42 Mango (default; the serial T-80U's round), 3BM59 Svinets-1 or 3BM60 Svinets-2 (long rods: the loading mechanism reworked for them, inferred).");
			PortAmmo.S = Ammo();
		}

		static PortAmmo.Settings Ammo() => new PortAmmo.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			UnitKey = T80UM2Mod.UnitKey,
			Prefix = "t80um2",
			SourceUnit = "T80B",
			// GHPC matches rounds by name, so these can't share one with the T-90A's / T-90M's or the lists mix.
			Apfsds = new[]
			{
				new PortAmmo.Round
				{
					Id = "3BM42", Name = "3BM42 Mango APFSDS", Source = "clip_3BM32", Texture = "round_3bm42_co.png", Atlas = "125mm_co",
					Set = a =>
					{
						a.RhaPenetration = 578f;
						a.Mass = 4.85f;
						a.MuzzleVelocity = 1700f;
						a.Coeff = 0.16f;
					},
					Prevalence = 1f,
				},
				new PortAmmo.Round
				{
					Id = "3BM59", Name = "3BM59 Svinets-1 APFSDS", Source = "clip_3BM32", Texture = "round_3bm59_co.png", Atlas = "125mm_co", Shape = "3BM60",
					Set = a =>
					{
						// No official figure for the 3BM59, so it's the 3BM60's scaled by the two rounds' ratio in the same sources (0.86).
						a.RhaPenetration = 693f * 0.86f;
						a.Mass = 4.5f;
						a.MuzzleVelocity = 1660f;
						a.Coeff = 0.093f;
					},
					Prevalence = SvinetsPrevalence,
				},
				new PortAmmo.Round
				{
					Id = "3BM60", Name = "3BM60 Svinets-2 APFSDS", Source = "clip_3BM32", Texture = "round_3bm60_co.png", Atlas = "125mm_co", Shape = "3BM60",
					Set = a =>
					{
						a.RhaPenetration = 693f;
						a.Mass = 4.5f;
						a.MuzzleVelocity = 1660f;
						a.Coeff = 0.093f;
					},
					Prevalence = SvinetsPrevalence,
				},
			},
			DefaultApfsds = () => Apfsds?.Value,
			ApfsdsListName = "125mm APFSDS (T-80UM2)",
			MenuSuffix = " (T-80UM2)",
			FixedHeat = "clip_3BK18M",
			Missile = new PortAmmo.Round
			{
				Id = "9M119M", Name = "9M119M Invar ATGM", Source = "clip_9M112M", Texture = "round_9m119m_co.png", Atlas = "kobra_co", Shape = "9M119M",
				Set = a =>
				{
					a.RhaPenetration = 700f;
					a.Tandem = true;
					a.Mass = 17.2f;
					// GHPC flies a missile at constant speed, 284 m/s gives the real 17.6 s to 5 km.
					a.MuzzleVelocity = 284f;
					a.MaximumRange = 5000f;
					a.ArmingDistance = 100f;
					a.RangedFuseTime = 5000f / 284f + 0.3f;
					a.Guidance = AmmoType.GuidanceType.Laser;
				},
			},
			EraDefeating = new[] { "9M119M Invar ATGM" },
			TotalCounts = Counts,
			Racks = new[]
			{
				// Missiles across the ring: the stored 9M119M is full calibre all along, two side by side overlap 17 mm in the trays.
				new PortAmmo.Rack { Index = 0, Counts = ConveyorCounts, MissileSlots = new[] { 26, 13 } },
				new PortAmmo.Rack { Index = 1, Counts = WetRackCounts, MissileSlots = new[] { 10, 11 } },
			},
			BeamOnBaseUnit = true,
			BeamName = "T80UM2 guidance beam",
			CoaxFeed = "PKT",
			CoaxRounds = 1250,
			CoaxBelt = 250,
		};

		static readonly GunSetup.Settings Gun = new GunSetup.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			TurretSpeed = 0f,
			GunSpeed = 0f,
			GunLimits = new Vector2(-5f, 14f),
			DepressionProfile = "gun_depression.txt",
			AutoloaderCycle = 7.1f,
			GunName = "125mm Gun 2A46M-1",
			CodexName = "T80UM2 2A46M-1",
		};

		// GHPC has no smoke restock (VehicleSmokeManager only counts down), so the 4 spare grenades aren't carried.
		internal static void Apply(Vehicle vehicle)
		{
			try { PortAmmo.ApplyAtSpawn(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} ammo setup failed: " + e); }
			try { PortAmmo.ApplyCoaxAtSpawn(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} coax setup failed: " + e); }
			try { GunSetup.Apply(vehicle, Gun); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} gun setup failed: " + e); }
			try { ShtoraScreen.Apply(vehicle, Smoke); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} smoke grenade setup failed: " + e); }
			MelonCoroutines.Start(SelfCheckLater(vehicle));
		}

		internal static readonly ShtoraScreen.Settings Smoke = new ShtoraScreen.Settings
		{
			Tag = T80UM2Mod.FriendlyName,
			Heads = false,
			PanelShown = false, PanelAlways = false, AlarmTone = false, Automatic = false,
			// The model's tubes say 41 deg, but a 1 s fuse at 50-80 m then bursts 40 m up, so the 3Д17's sourced 12 deg is kept.
			LaunchAngle = 12f,
			Distance = new Vector2(50f, 80f),
			TubeHeight = 2.0f,
		};

		static System.Collections.IEnumerator SelfCheckLater(Vehicle vehicle)
		{
			for (int i = 0; i < 30; i++) yield return null;
			try { PortAmmo.SelfCheck(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T80UM2Mod.FriendlyName} ammunition self-check failed: " + e); }
		}
	}
}
