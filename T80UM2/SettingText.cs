namespace T80UM2
{
	// What the player reads in MelonPreferences.cfg: one description per setting. Static fields, not consts:
	// tools\check-settings.ps1 reads every const as a possible default.
	internal static class SettingText
	{
		static readonly string AfterChange = " For T-80UM2s spawned after the change.";

		internal static readonly string Enabled =
			"Adds the T-80UM2 as its own unit (Instant Action, campaign roster, ATAC key \"T80UM2\").";

		internal static readonly string Apfsds =
			"Default APFSDS where the game does not choose one (the mission customizer's ammo dropdown and the campaign's ammo"
			+ " supply override it): 3BM42 Mango (default; the serial T-80U's round), 3BM59 Svinets-1 or 3BM60 Svinets-2 (long"
			+ " rods: the loading mechanism reworked for them, inferred).";

		internal static readonly string ForceApfsds =
			"false (default): the campaign picks each T-80UM2's APFSDS from the army's ammo supply (3BM42 Mango half the time, 3BM59"
			+ " Svinets-1 and 3BM60 Svinets-2 a quarter each). true = every T-80UM2 carries the APFSDS setting's round in the campaign too."
			+ " In Instant Action the mission customizer's ammo dropdown still decides."
			+ AfterChange;

		internal static readonly string Drozd =
			"true (default): the Drozd active protection is fitted (two radar heads, four launcher blocks, 8 rounds; switched on"
			+ " and off in play from the vehicle menu). false = the tank has the blocks but no active protection."
			+ AfterChange;

		internal static readonly string FireWhenLaid =
			"true (default): after a lase the trigger does nothing until the gun has climbed to the elevation the computer set for"
			+ " that range, as the real firing-permission circuit has it (the ready lamp comes on when it is there). false = the gun"
			+ " fires at any time, as the game's own tanks do; a shot while it is still climbing after a lase falls short.";

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
