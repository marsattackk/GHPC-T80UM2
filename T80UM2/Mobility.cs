namespace T80UM2
{
	internal static class Mobility
	{
		internal static void Apply(GHPC.Vehicle.Vehicle vehicle)
		{
			T80UM2.Common.Mobility.Apply(vehicle, Settings);
		}

		internal static readonly T80UM2.Common.Mobility.Settings Settings = new T80UM2.Common.Mobility.Settings
		{
			MaxPower = 1250f,
			FinalDrive = 5.216f,
			// The cap's a cruise-control target that eases off within ~5 km/h of it, so it sits over the real 70.
			MaxForwardKmh = 80f,
			// GHPC's only speed resistance is Rigidbody drag (0.1 on every vanilla tank). At 0.1 it levels off at 63 km/h,
			// 0.077 lets the 2300 rpm cut set the real 70. Drag over extra power so it isn't quicker off the line.
			LinearDrag = 0.077f,
			CombatMassKg = 47000f,
		};
	}
}
