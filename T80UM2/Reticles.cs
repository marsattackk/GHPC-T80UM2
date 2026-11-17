using System.Collections.Generic;
using GHPC.Weaponry;
using Reticle;
using T80UM2.Common;

namespace T80UM2
{
	internal static partial class Reticles
	{
		static readonly PortReticles All = new PortReticles(typeof(Reticles).Assembly, T80UM2Mod.FriendlyName);

		internal static ReticleSpec Spec(string file) => All.Spec(file);

		internal static ReticleSO Build(string file, ReticleSO host, ReticleSO fontHost, Dictionary<string, AmmoCodexScriptable> rounds) =>
			All.Build(file, host, fontHost, rounds);
	}
}
