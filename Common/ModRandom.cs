using System;
namespace T80UM2.Common
{
	// GHPC reseeds UnityEngine.Random from the clock all over the place (RandomUtil.RandomizeUnityRandom).
	internal static class ModRandom
	{
		static readonly Random Rng = new Random();

		internal static float Value => (float)Rng.NextDouble();
	}
}
