// LiveRound.Init resets RhaPenetrationOverride to 0 (resetLocalValues). An override set before Init reaches only the
// ShotInfo, and the round itself then uses Info.RhaPenetration x (speed / MaxSpeed)^2. penAfterInit sets it again after
// Init. Both users (the APS and the T-90M's air burst) pass it.
using System;
using System.Reflection;
using GHPC;
using GHPC.Weapons;
using UnityEngine;

namespace T80UM2.Common
{
	// Spall elements thrown in a cone. LiveRound.Init resets RhaPenetrationOverride to 0, so an
	// override set before Init only reaches the ShotInfo: penAfterInit sets it again after Init.
	internal static class FragmentCone
	{
		internal static AmmoType Type(string name, float mass, float area, float coeff, float caliber, float pen, float penSpeed)
		{
			var ammo = new AmmoType();
			foreach (var field in typeof(AmmoType).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				if (!field.IsLiteral && !field.IsInitOnly) field.SetValue(ammo, field.GetValue(LiveRound.SpallAmmoType));
			ammo.Name = name;
			ammo.IsSpallType = true;
			ammo.Mass = mass;
			ammo.SectionalArea = area;
			ammo.Coeff = coeff;
			ammo.Caliber = caliber;
			ammo.RhaPenetration = pen;
			ammo.MuzzleVelocity = penSpeed;
			ammo.VisualType = LiveRoundMarshaller.LiveRoundVisualType.Invisible;
			return ammo;
		}

		internal static int Spawn(AmmoType info, Vector3 at, Vector3 fwd, float halfAngle, int count, float speed, float pen,
			Unit shooter, bool npc, LiveRound parent, string name, Func<float> random, bool penAfterInit, Vector3 fallbackRight, Action<LiveRound> spawned =
				null)
		{
			var marshaller = LiveRoundMarshaller.Instance;
			if (marshaller == null || count <= 0) return 0;
			var right = Vector3.Cross(Vector3.up, fwd);
			right = right.sqrMagnitude > 1e-6f ? right.normalized : fallbackRight;
			var up = Vector3.Cross(fwd, right);
			float tan = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
			for (int i = 0; i < count; i++)
			{
				// sqrt spreads them evenly over the cone's cross-section, not bunched in the middle
				float radius = tan * Mathf.Sqrt(random()), phi = 2f * Mathf.PI * random();
				var dir = (fwd + radius * (Mathf.Cos(phi) * right + Mathf.Sin(phi) * up)).normalized;
				var round = marshaller.GetRoundOfVisualType(LiveRoundMarshaller.LiveRoundVisualType.Invisible);
				round.Info = info;
				round.RhaPenetrationOverride = pen;
				round.CurrentSpeed = speed;
				round.MaxSpeed = speed;
				round.NpcRound = npc;
				round.IsSpall = true;
				round.Shooter = shooter;
				round.transform.position = at;
				round.transform.forward = dir;
				round.Init(parent);
				if (penAfterInit) round.RhaPenetrationOverride = pen;
				round.gameObject.name = $"{name} {round.ID}";
				spawned?.Invoke(round);
			}
			return count;
		}
	}
}
