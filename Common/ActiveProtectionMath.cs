using System;

namespace T80UM2.Common
{
	internal static class ApsMath
	{
		internal enum Threat { Spall, Atgm, Grenade, Heat, He, Kinetic, Other }

		internal static string Name(Threat t)
			=> t == Threat.Atgm
				? "ATGM"
				: t == Threat.Grenade
					? "grenade / rocket"
					: t == Threat.Heat
						? "HEAT shell"
						: t == Threat.He ? "HE shell" : t == Threat.Kinetic ? "kinetic" : t == Threat.Spall ? "spall" : "other";

		internal static Threat ClassOf(bool spall, bool guided, int category, string name, string[] grenadePrefixes)
		{
			if (spall) return Threat.Spall;
			if (guided) return Threat.Atgm;
			if (category == 0)
			{
				if (name != null && grenadePrefixes != null)
					foreach (var prefix in grenadePrefixes)
						if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Threat.Grenade;
				return Threat.Heat;
			}
			if (category == 2) return Threat.He;
			if (category == 1) return Threat.Kinetic;
			return Threat.Other;
		}

		internal static float Diameter(float sectionalArea) => sectionalArea > 0f ? 2f * (float)Math.Sqrt(sectionalArea / Math.PI) : 0f;

		internal static V3 FrameVelocity(bool hasPrev, V3 move, float dt, bool hasLast, V3 last, V3 fallback)
		{
			if (!hasPrev || dt <= 1e-5f) return hasLast ? last : fallback;
			if (move.Length < 1e-4f) return hasLast ? last : fallback;
			return move * (1f / dt);
		}

		internal static string Window(Geometry g, float speed, float diameter, bool spall)
		{
			if (spall) return "spall";
			if (speed > g.SpeedMax) return $"speed {speed:0} > {g.SpeedMax:0} m/s";
			if (speed < g.SpeedMin) return $"speed {speed:0} < {g.SpeedMin:0} m/s";
			if (diameter < g.SizeMin) return $"size {diameter * 1000f:0} mm < {g.SizeMin * 1000f:0} mm";
			return null;
		}

		internal sealed class Block
		{
			public string Name;
			public V3 Mouth;
			public float AxisAz, AxisEl;
			public float AzMin, AzMax;
			public int Side;
			public int Rounds = 2;
			public V3 Axis => V3.FromAzEl(AxisAz, AxisEl);
			public V3 Burst(float range) => Mouth + Axis * range;
		}

		internal sealed class Geometry
		{
			public Block[] Blocks = new Block[0];
			public V3 Origin;
			public float ElMin = -6f, ElMax = 20f;
			public float SearchRange = 330f, TrackRange = 130f, MinRange = 11f;
			public float SpeedMin = 70f, SpeedMax = 700f;
			public float SizeMin = 0.036f;
			public float ReactionTime = 0.02f;
			public float RoundSpeed = 190f;
			public float InterceptRange = 6.7f;
			public float BurstMin = 6f, BurstMax = 8f;
			public float FragReach = 1.5f;
			public float ConeHalf = 15f, ConeReach = 3f;
			public float Cooldown = 0.35f, OtherSide = 0.035f;
			public bool PickTube;
			public float Flight => InterceptRange / RoundSpeed;
		}

		internal static float BurstRange(Geometry g, Block b, V3 p, V3 d)
		{
			var axis = b.Axis;
			var toMouth = b.Mouth - p;
			float k = V3.Dot(axis, d), denom = 1f - k * k;
			if (denom < 1e-6f) return g.InterceptRange;
			float range = (k * V3.Dot(toMouth, d) - V3.Dot(toMouth, axis)) / denom;
			return Math.Max(g.BurstMin, Math.Min(g.BurstMax, range));
		}

		internal sealed class State
		{
			public int[] Rounds;
			public bool[] Lost;
			public float[] SideFired = { float.NegativeInfinity, float.NegativeInfinity };
			public State(Geometry g)
			{
				Rounds = new int[g.Blocks.Length];
				Lost = new bool[g.Blocks.Length];
				for (int i = 0; i < Rounds.Length; i++) Rounds[i] = g.Blocks[i].Rounds;
				Drawn = new int[g.Blocks.Length, g.Blocks.Length];
			}

			public int[,] Drawn;
			public int SectorLeft(Geometry g, int sector)
			{
				int remaining = g.Blocks[sector].Rounds;
				for (int block = 0; block < Rounds.Length; block++) remaining -= Drawn[sector, block];
				return remaining;
			}

			public void Reload(int block)
			{
				Rounds[block]++;
				int source = Drawn[block, block] > 0 ? block : -1;
				for (int i = 0; i < Rounds.Length && source < 0; i++) if (Drawn[i, block] > 0) source = i;
				if (source >= 0) Drawn[source, block]--;
			}

			public int SideRounds(Geometry g, int side)
			{
				int count = 0;
				for (int i = 0; i < Rounds.Length; i++) if (g.Blocks[i].Side == side && !Lost[i]) count += Rounds[i];
				return count;
			}

			public int Total
			{
				get
				{
					int count = 0;
					for (int i = 0; i < Rounds.Length; i++) if (!Lost[i]) count += Rounds[i];
					return count;
				}
			}
		}

		internal sealed class Track
		{
			public float SeenAt = -1f, SeenRange;
			public int Block = -1;
			public int Sector = -1;
			public bool Committed, Done;
			public float FiredAt, BurstAt;
			public V3 BurstPoint;
			public float BurstRange;
			public string Why;
			public bool Seen => SeenAt >= 0f;
		}

		internal enum Act { None, Seen, Fire, Intercept, Drop }

		internal struct Result
		{
			public Act Act;
			public V3 Hit;
			public float Moved;
			public float Miss;
			public bool Fired;
			public bool InField;
		}

		internal static int BlockFor(Geometry g, float az)
		{
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				var block = g.Blocks[i];
				if (az >= block.AzMin && az < block.AzMax) return i;
			}
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				var block = g.Blocks[i];
				if (az == block.AzMax && !HasStart(g, az)) return i;
			}
			return -1;
		}

		static bool HasStart(Geometry g, float az)
		{
			foreach (var block in g.Blocks) if (block.AzMin == az) return true;
			return false;
		}

		internal static int PickBlock(Geometry g, State s, V3 p, V3 d, float az)
		{
			int best = -1;
			float bestWide = float.PositiveInfinity;
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				if (s.Lost[i] || s.Rounds[i] <= 0) continue;
				var block = g.Blocks[i];
				var burstPoint = block.Burst(BurstRange(g, block, p, d));
				if (!Field(g, p, d, burstPoint, block.Axis, out _, out float sLeave) || sLeave < 0f) continue;
				float width = Wide(p, d, burstPoint);
				if (width < bestWide)
				{
					bestWide = width;
					best = i;
				}
			}
			return best >= 0 ? best : BlockFor(g, az);
		}

		internal static bool Sphere(V3 p, V3 d, V3 c, float r, out float sIn, out float sOut)
		{
			var offset = p - c;
			float projection = V3.Dot(offset, d), constant = V3.Dot(offset, offset) - r * r, disc = projection * projection - constant;
			if (disc < 0f)
			{
				sIn = sOut = -projection;
				return false;
			}
			float root = (float)Math.Sqrt(disc);
			sIn = -projection - root;
			sOut = -projection + root;
			return true;
		}

		internal static bool RayHitsBox(V3 p, V3 d, V3 centre, V3 half)
		{
			float enter = 0f, leave = float.PositiveInfinity;
			float[] offsets = {
				p.x - centre.x,
				p.y - centre.y,
				p.z - centre.z
			}, dir = { d.x, d.y, d.z }, extents = { half.x, half.y, half.z };
			for (int k = 0; k < 3; k++)
			{
				if (Math.Abs(dir[k]) < 1e-9f)
				{
					if (offsets[k] < -extents[k] || offsets[k] > extents[k]) return false;
					continue;
				}
				float slabNear = (-extents[k] - offsets[k]) / dir[k], slabFar = (extents[k] - offsets[k]) / dir[k];
				if (slabNear > slabFar)
				{
					var swap = slabNear;
					slabNear = slabFar;
					slabFar = swap;
				}
				enter = Math.Max(enter, slabNear);
				leave = Math.Min(leave, slabFar);
				if (enter > leave) return false;
			}
			return true;
		}

		internal static Result Step(Geometry g, State s, Track t, V3 p, V3 v, bool atBox, float now, float dt, string ready)
		{
			var result = new Result { Act = Act.None };
			if (t.Done) return result;
			float speed = v.Length;
			if (speed < 1e-3f) return result;
			var direction = v * (1f / speed);
			var rel = p - g.Origin;
			float range = rel.Length;
			if (!t.Seen)
			{
				if (range > g.TrackRange) return result;
				if (V3.Dot(rel, direction) >= 0f) return result;
				float azimuth = rel.Az, elevation = rel.El;
				if (!atBox) return result;
				t.SeenAt = now - Math.Min(dt, Math.Max(0f, (g.TrackRange - range) / speed));
				t.SeenRange = range;
				if (elevation < g.ElMin || elevation > g.ElMax || BlockFor(g, azimuth) < 0)
					return Drop(t, result, $"not engaged (bearing {azimuth:+0.0;-0.0}, el {elevation:+0.0;-0.0}: outside every sector)");
				if (ready != null) return Drop(t, result, $"not engaged ({ready})");
				if (range + speed * dt < g.MinRange)
					return Drop(
						t,
						result,
						$"too close (track at {range:0.0} m, {range + speed * dt:0.0} m a frame before < {g.MinRange:0} m)");
				t.Sector = BlockFor(g, azimuth);
				if (g.PickTube && s.SectorLeft(g, t.Sector) <= 0)
					return Drop(
						t,
						result,
						$"not engaged ({g.Blocks[t.Sector].Rounds} rounds already fired from this direction, the {g.Blocks[t.Sector].Name} sector)");
				t.Block = g.PickTube ? PickBlock(g, s, p, direction, azimuth) : t.Sector;
				if (s.Lost[t.Block]) return Drop(t, result, $"not engaged (block {g.Blocks[t.Block].Name} lost)");
				if (s.Rounds[t.Block] <= 0)
					return Drop(
						t,
						result,
						$"not engaged (no rounds left in block {g.Blocks[t.Block].Name}, {s.SideRounds(g, g.Blocks[t.Block].Side)} on that side)");
				result.Act = Act.Seen;
			}
			var blk = g.Blocks[t.Block];
			float burstRange = t.Committed ? t.BurstRange : BurstRange(g, blk, p, direction);
			var burstPoint = t.Committed ? t.BurstPoint : blk.Burst(burstRange);
			var axis = blk.Axis;
			if (!t.Committed)
			{
				if (!Field(g, p, direction, burstPoint, axis, out float sHit, out float sLeave))
				{
					if (V3.Dot(burstPoint - p, direction) < 0f)
						return Drop(
							t,
							result,
							$"not fired (passes {Wide(p, direction, burstPoint):0.0} m wide of the burst point, outside the fragment field)");
					return result;
				}
				if (sLeave < 0f) return Drop(t, result, "late (past the burst point)");
				float flight = burstRange / g.RoundSpeed;
				float want = now + Math.Max(0f, sHit) / speed - flight;
				if (want > now + dt) return result;
				if (ready != null) return Drop(t, result, $"not fired ({ready})");
				if (s.Rounds[t.Block] <= 0 || s.Lost[t.Block])
					return Drop(t, result, $"not fired (block {blk.Name} {(s.Lost[t.Block] ? "lost" : "empty")})");
				int other = 1 - blk.Side;
				float react = t.SeenAt + g.ReactionTime, own = s.SideFired[blk.Side] + g.Cooldown, near = s.SideFired[other] + g.OtherSide;
				float launch = Math.Max(Math.Max(want, now - dt), Math.Max(react, Math.Max(own, near)));
				float leaveAt = now + sLeave / speed;
				if (launch + flight > leaveAt + 1e-5f)
				{
					bool busy = Math.Max(own, near) > Math.Max(Math.Max(want, now - dt), react) + 1e-6f;
					string cause = !busy
						? $"late (track {now - t.SeenAt:0.000} s, {sLeave / speed:0.000} s left in the fragment field's reach, reaction {g.ReactionTime:0.000} + flight {flight:0.000} s)"
						: $"busy (the {(launch == own ? "side's" : "other side's")} last launch {now - Math.Max(s.SideFired[blk.Side], s.SideFired[other]):0.000} s ago)";
					return Drop(t, result, cause);
				}
				if (launch > now + dt) return result;
				s.Rounds[t.Block]--;
				if (g.PickTube) s.Drawn[t.Sector, t.Block]++;
				s.SideFired[blk.Side] = launch;
				t.Committed = true;
				t.FiredAt = launch;
				t.BurstAt = launch + flight;
				t.BurstPoint = burstPoint;
				t.BurstRange = burstRange;
				result.Act = Act.Fire;
				result.Fired = true;
			}
			if (t.BurstAt > now + dt + 1e-6f) return result;
			float move = speed * (t.BurstAt - now);
			if (move < -speed * dt) move = -speed * dt;
			result.Act = Act.Intercept;
			result.Moved = move;
			result.Hit = p + direction * move;
			result.Miss = (result.Hit - burstPoint).Length;
			result.InField = InField(g, result.Hit, burstPoint, axis);
			t.Done = true;
			return result;
		}

		static float Wide(V3 p, V3 d, V3 c)
		{
			var offset = c - p;
			return (offset - d * V3.Dot(offset, d)).Length;
		}

		internal static bool InField(Geometry g, V3 x, V3 B, V3 axis)
		{
			var rel = x - B;
			float range = rel.Length;
			if (range <= g.FragReach + 0.01f) return true;
			return range <= g.ConeReach + 0.01f && V3.Dot(rel, axis) >= range * (float)Math.Cos((g.ConeHalf + 0.5f) * Math.PI / 180.0);
		}

		static bool Field(Geometry g, V3 p, V3 d, V3 B, V3 axis, out float sHit, out float sLeave)
		{
			if (Sphere(p, d, B, g.FragReach, out float sphereIn, out float sphereOut))
			{
				sHit = 0.5f * (sphereIn + sphereOut);
				sLeave = sphereOut;
				return true;
			}
			if (Sphere(p, d, B, g.ConeReach, out float coneIn, out float coneOut))
			{
				const int steps = 48;
				int inside = -1;
				for (int i = 0;
					i <= steps && inside < 0;
					i++) if (InField(g, p + d * (coneIn + (coneOut - coneIn) * i / steps), B, axis)) inside = i;
				if (inside >= 0)
				{
					float At(int i) => coneIn + (coneOut - coneIn) * i / steps;
					float Edge(float sIn, float sOut)
					{
						for (int k = 0; k < 20; k++)
						{
							float middle = 0.5f * (sIn + sOut);
							if (InField(g, p + d * middle, B, axis)) sIn = middle;
							else sOut = middle;
						}
						return sIn;
					}
					float startDistance = inside == 0 ? coneIn : Edge(At(inside), At(inside - 1));
					int last = inside;
					while (last < steps && InField(g, p + d * At(last + 1), B, axis)) last++;
					float endDistance = last == steps ? coneOut : Edge(At(last), At(last + 1));
					sLeave = endDistance;
					sHit = 0.5f * (startDistance + endDistance);
					return true;
				}
			}
			sHit = sLeave = 0f;
			return false;
		}

		static Result Drop(Track t, Result res, string why)
		{
			t.Done = true;
			t.Why = why;
			res.Act = Act.Drop;
			return res;
		}

		internal static bool Kills(float pKill, float draw) => draw < pKill;

		internal static bool BrokenUp(float pBrokenUp, float draw) => draw < pBrokenUp;
	}
}
