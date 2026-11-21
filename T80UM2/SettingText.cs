namespace T80UM2
{
	// What the player reads in MelonPreferences.cfg: one description per setting. Static fields, not consts:
	// tools\check-settings.ps1 reads every const as a possible default.
	internal static class SettingText
	{
		static readonly string AfterChange = " For T-80UM2s spawned after the change.";

		internal static readonly string Enabled =
			"Adds the T-80UM2 as its own unit (Instant Action, campaign roster). Its unit key is \"T80UM2\", the name to give"
			+ " mods like Any Tank Any Campaign.";

		internal static readonly string Apfsds =
			"Default APFSDS where the game does not choose one (the mission customizer's ammo dropdown and the campaign's ammo"
			+ " supply override it): 3BM42 Mango (default; the serial T-80U's round), 3BM59 Svinets-1 or 3BM60 Svinets-2 (long"
			+ " rods: the loading mechanism reworked for them, inferred).";

		internal static readonly string ForceApfsds =
			"false (default): the campaign picks each T-80UM2's APFSDS from the army's ammo supply (3BM42 Mango half the time, 3BM59"
			+ " Svinets-1 and 3BM60 Svinets-2 a quarter each). true = every T-80UM2 carries the APFSDS setting's round in the campaign too."
			+ " In Instant Action the mission customizer's ammo dropdown still decides."
			+ AfterChange;

		internal static readonly string RandomApfsds =
			"false (default): each T-80UM2 carries the APFSDS setting's round where the game does not choose one. true = each"
			+ " T-80UM2 rolls its own APFSDS from APFSDS pool in Instant Action and scripted missions, and the mission customizer's"
			+ " ammo dropdown starts on Random (a round picked there still decides). The campaign keeps its own ammo supply."
			+ AfterChange;

		internal static readonly string ApfsdsPool =
			"The rounds Random APFSDS rolls from, as name:weight, comma separated (3BM42, 3BM59, 3BM60; \"3BM42:3, 3BM60:1\" rolls"
			+ " 3BM42 three times as often as 3BM60, and never 3BM59; a name without a weight counts 1). Empty (default): every"
			+ " APFSDS at its campaign weight."
			+ AfterChange;

		internal static readonly string Heat =
			"Default HEAT where the game does not choose one (the mission customizer's ammo dropdown and the campaign's ammo"
			+ " supply override it): 3BK29M (default; tandem, 700 mm, beats add-on ERA such as Kontakt-1), 3BK31 Start-2"
			+ " (experimental triple charge, 800 mm, beats built-in ERA such as Kontakt-5 too) or 3BK18M (the older single"
			+ " charge, 585 mm).";

		internal static readonly string ForceHeat =
			"false (default): the campaign picks each T-80UM2's HEAT from the army's ammo supply (3BK29M or 3BK18M; never the"
			+ " experimental 3BK31). true = every T-80UM2 carries the HEAT setting's round in the campaign too. In Instant Action"
			+ " the mission customizer's ammo dropdown still decides."
			+ AfterChange;

		internal static readonly string RandomHeat =
			"false (default): each T-80UM2 carries the HEAT setting's round where the game does not choose one. true = each"
			+ " T-80UM2 rolls its own HEAT from HEAT pool in Instant Action and scripted missions, and the mission customizer's"
			+ " HEAT dropdown starts on Random (a round picked there still decides). The campaign keeps its own ammo supply."
			+ AfterChange;

		internal static readonly string HeatPool =
			"The rounds Random HEAT rolls from, as name:weight, comma separated (3BK29M, 3BK31, 3BK18M; \"3BK29M:3, 3BK18M:1\""
			+ " rolls 3BK29M three times as often as 3BK18M and never 3BK31; a name without a weight counts 1). Empty (default):"
			+ " 3BK29M and 3BK18M at their campaign weights; the experimental 3BK31 only when named here."
			+ AfterChange;

		internal static readonly string AmmunitionLoad =
			"How much ammunition each T-80UM2 carries: Full (default, 40 rounds) or Autoloader (only the 28 in the autoloader, 2 of"
			+ " them missiles). Fewer rounds aboard, fewer to set off when the hull is hit. Applies to every T-80UM2, friend and foe."
			+ AfterChange;

		internal static readonly string EmptyRacks =
			"Racks left empty, by name, comma separated (hull: the hull rack); when set it replaces Ammunition load. The autoloader"
			+ " is always loaded. Empty (default): Ammunition load decides."
			+ AfterChange;

		internal static readonly string Drozd =
			"true (default): the Drozd active protection is fitted (two radar heads, four launcher blocks, 8 rounds; switched on"
			+ " and off in play from the vehicle menu). false = the tank has the blocks but no active protection."
			+ AfterChange;

		internal static readonly string FireWhenLaid =
			"true (default): after a lase the trigger does nothing until the gun has climbed to the elevation the computer set for"
			+ " that range, as the real firing-permission circuit has it (the ready lamp comes on when it is there). false = the gun"
			+ " fires at any time, as the game's own tanks do; a shot while it is still climbing after a lase falls short."
			+ AfterChange;

		internal static readonly string EraEffects =
			"true (default): a set-off Kontakt-5 container is gone with a blast and the plate round it sooted. false = it looks"
			+ " as before (it still works as armour, spent after one hit).";

		internal static readonly string NsvtCases =
			"true (default): the commander's NSVT throws its spent cases out forward on the right, onto the turret roof. false ="
			+ " no cases."
			+ AfterChange;

		internal static readonly string NightSight =
			"Agava-2 (default) = the gunner's thermal sight of the T-80UM (a guess for the T-80UM2: its photos show the older"
			+ " image intensifier); Buran-PA = that image intensifier, as the photos show. Can be changed in play.";

		internal static readonly string ArmourPreset =
			"game-scale (default: matched to GHPC's other tanks; turret 744/984, glacis 485/653 without ERA) or source-only"
			+ " (open-source figures; turret 620/900, glacis 430/566)."
			+ AfterChange;
	}
}
