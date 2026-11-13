using System;

namespace T80UM2.Common
{
	internal static class ReloadRules
	{
		internal static float ForeignFactor(float liveWork, float storedWork)
		{
			if (storedWork <= 0f || liveWork <= 0f || float.IsNaN(liveWork) || float.IsInfinity(liveWork)) return 1f;
			float k = liveWork / storedWork;
			return Math.Abs(k - 1f) < 0.001f ? 1f : k;
		}
	}
}
