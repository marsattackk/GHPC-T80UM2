using GHPC.Camera;

namespace T80UM2.Common
{
	internal static class SightMotion
	{
		// A touch under the T-80B's 1G42 (0.1 / 0.15).
		internal const float Blur = 0.08f;
		internal const float Shake = 0.12f;

		internal static void Stabilised(CameraSlot slot, bool keepBlur = false)
		{
			if (slot == null) return;
			if (!keepBlur) slot.VibrationBlurScale = Blur;
			slot.VibrationShakeMultiplier = Shake;
		}
	}
}
