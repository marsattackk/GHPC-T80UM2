using System;
using System.Collections.Generic;
using System.Linq;

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

		// category is (int)AmmoType.AmmoCategory, an int so the tests run without GHPC. Should GHPC reorder that enum,
		// rounds fall into the wrong class and get its kill chance.
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

		// GHPC leaves a missile standing still for the odd frame. Reading 0 m/s there let a Konkurs through at 23 m.
		internal static V3 FrameVelocity(bool hasPrev, V3 move, float dt, bool hasLast, V3 last, V3 fallback)
		{
			if (!hasPrev || dt <= 1e-5f) return hasLast ? last : fallback;
			if (move.Length < 1e-4f) return hasLast ? last : fallback;
			return move * (1f / dt);
		}

		internal static bool InWindow(Geometry g, float speed, float diameter, bool spall)
			=> !(spall || speed > g.SpeedMax || speed < g.SpeedMin || diameter < g.SizeMin);

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
			public Sensor[] Sensors = new Sensor[0];
			public bool OverlapPick;
			public bool ClimbBurst;
			public float BurstHeight = 0f;
			public int RoundsPerThreat = 1;
			public float RoundGap = 0.02f;
			public float Flight => InterceptRange / RoundSpeed;
		}

		internal sealed class Sensor
		{
			public string Name;
			public float AzMin, AzMax;
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
			public bool[] SensorLost;
			public State(Geometry g)
			{
				int sides = 2;
				foreach (var block in g.Blocks) sides = Math.Max(sides, block.Side + 1);
				if (sides > SideFired.Length)
				{
					SideFired = new float[sides];
					for (int side = 0; side < sides; side++) SideFired[side] = float.NegativeInfinity;
				}
				SensorLost = new bool[g.Sensors.Length];
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
			public V3 Axis;
			public List<Shot> Shots = new List<Shot>();
			public string Why;
			public bool Seen => SeenAt >= 0f;
		}

		internal sealed class Shot
		{
			public int Block;
			public float FiredAt, BurstAt, BurstRange;
			public V3 BurstPoint, Axis;
			public bool Burst;
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
			public V3 Axis;
			public Shot Shot;
			public int FirstShot;
			public int ShotCount;
			public bool More;
		}

		internal static float WrapDeg(float angle)
		{
			while (angle > 180f) angle -= 360f;
			while (angle < -180f) angle += 360f;
			return angle;
		}

		internal static bool Holds(float az, float min, float max, bool closed)
		{
			if (min < max && min >= -180f && max <= 180f) return az >= min && (closed ? az <= max : az < max);
			float width = max - min;
			if (width < 0f) width += 360f;
			if (width >= 360f) return true;
			float offset = az - min;
			offset -= 360f * (float)Math.Floor(offset / 360f);
			return closed ? offset <= width : offset < width;
		}

		internal static int BlockFor(Geometry g, float az)
		{
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				var block = g.Blocks[i];
				if (Holds(az, block.AzMin, block.AzMax, false)) return i;
			}
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				var block = g.Blocks[i];
				if (az == WrapDeg(block.AzMax) && !HasStart(g, az)) return i;
			}
			return -1;
		}

		static bool HasStart(Geometry g, float az)
		{
			foreach (var block in g.Blocks) if (WrapDeg(block.AzMin) == az) return true;
			return false;
		}

		internal static string BlockLabel(Block b)
		{
			var words = (b.Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return words.Length == 0 ? "?" : string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
		}

		internal static string BlockSummary(Geometry g, State s)
		{
			var order = Enumerable.Range(0, g.Blocks.Length)
				.OrderBy(i => Math.Abs(WrapDeg(g.Blocks[i].AxisAz)) < 90f ? 0 : 1)
				.ThenBy(i => WrapDeg(g.Blocks[i].AxisAz) < 0f ? 0 : 1)
				.ThenBy(i => Math.Abs(WrapDeg(g.Blocks[i].AxisAz)));
			return string.Join(" ", order.Select(i => $"{BlockLabel(g.Blocks[i])} {(s.Lost[i] ? "lost" : s.Rounds[i].ToString())}"));
		}

		internal static string RadarSummary(Geometry g, State s)
		{
			var lost = Enumerable.Range(0, g.Sensors.Length).Where(i => s.SensorLost[i]).Select(i => g.Sensors[i].Name).ToList();
			string count = $"{g.Sensors.Length - lost.Count} of {g.Sensors.Length}";
			return lost.Count == 0 ? count : $"{count}, {string.Join(", ", lost)} lost";
		}

		internal static string SensorLoss(Geometry g, State s, float az)
		{
			if (g.Sensors.Length == 0) return null;
			var lost = new List<string>();
			for (int i = 0; i < g.Sensors.Length; i++)
			{
				var sensor = g.Sensors[i];
				if (!Holds(az, sensor.AzMin, sensor.AzMax, true)) continue;
				if (!s.SensorLost[i]) return null;
				lost.Add(sensor.Name);
			}
			if (lost.Count == 0) return $"outside every radar arc (bearing {az:+0.0;-0.0})";
			return lost.Count == 1 ? $"radar sensor {lost[0]} lost" : $"radar sensors {string.Join(", ", lost)} lost";
		}

		internal struct Plan
		{
			public V3 Point;
			public V3 Axis;
			public float Range;
			public float SHit;
			public float SLeave;
			public bool Reach;
		}

		internal static Plan PlanBurst(Geometry g, Block b, V3 p, V3 d)
		{
			var plan = new Plan();
			if (!g.ClimbBurst)
			{
				plan.Range = BurstRange(g, b, p, d);
				plan.Point = b.Burst(plan.Range);
				plan.Axis = b.Axis;
				plan.Reach = Field(g, p, d, plan.Point, plan.Axis, out plan.SHit, out plan.SLeave);
				return plan;
			}
			float target;
			if (Sphere(p, d, g.Origin, g.InterceptRange, out float enter, out _)) target = Math.Max(enter, 0f);
			else target = Math.Max(0f, -V3.Dot(p - g.Origin, d));
			var aim = p + d * target;
			plan.Point = aim + new V3(0f, g.BurstHeight, 0f);
			plan.Range = (plan.Point - b.Mouth).Length;
			plan.Axis = (aim - plan.Point).Normalized;
			plan.SHit = target;
			plan.Reach = Sphere(p, d, plan.Point, Math.Max(g.ConeReach, g.FragReach), out _, out plan.SLeave);
			return plan;
		}

		internal static int PickCovering(Geometry g, State s, V3 p, V3 d, float az)
		{
			int best = -1, mostRounds = -1, unusable = -1;
			float bestMiss = float.PositiveInfinity;
			for (int i = 0; i < g.Blocks.Length; i++)
			{
				var block = g.Blocks[i];
				if (!Holds(az, block.AzMin, block.AzMax, false)) continue;
				if (s.Lost[i] || s.Rounds[i] <= 0)
				{
					if (unusable < 0) unusable = i;
					continue;
				}
				if (mostRounds < 0 || s.Rounds[i] > s.Rounds[mostRounds]) mostRounds = i;
				var plan = PlanBurst(g, block, p, d);
				if (!plan.Reach || plan.SLeave < 0f) continue;
				float miss = Wide(p, d, plan.Point);
				bool nearer = miss < bestMiss - 0.05f;
				bool tie = Math.Abs(miss - bestMiss) <= 0.05f && best >= 0 && s.Rounds[i] > s.Rounds[best];
				if (best < 0 || nearer || tie)
				{
					best = i;
					bestMiss = miss;
				}
			}
			if (best >= 0) return best;
			if (mostRounds >= 0) return mostRounds;
			return unusable >= 0 ? unusable : BlockFor(g, az);
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
			return Slab(p.x - centre.x, d.x, half.x, ref enter, ref leave)
				&& Slab(p.y - centre.y, d.y, half.y, ref enter, ref leave)
				&& Slab(p.z - centre.z, d.z, half.z, ref enter, ref leave);
		}

		static bool Slab(float offset, float dir, float extent, ref float enter, ref float leave)
		{
			if (Math.Abs(dir) < 1e-9f) return !(offset < -extent || offset > extent);
			float slabNear = (-extent - offset) / dir, slabFar = (extent - offset) / dir;
			if (slabNear > slabFar)
			{
				var swap = slabNear;
				slabNear = slabFar;
				slabFar = swap;
			}
			enter = Math.Max(enter, slabNear);
			leave = Math.Min(leave, slabFar);
			return !(enter > leave);
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
				string blind = SensorLoss(g, s, azimuth);
				if (blind != null) return Drop(t, result, $"not engaged ({blind})");
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
				t.Block = g.PickTube
					? PickBlock(g, s, p, direction, azimuth)
					: g.OverlapPick ? PickCovering(g, s, p, direction, azimuth) : t.Sector;
				if (s.Lost[t.Block]) return Drop(t, result, $"not engaged (block {g.Blocks[t.Block].Name} lost)");
				if (s.Rounds[t.Block] <= 0)
					return Drop(
						t,
						result,
						$"not engaged (no rounds left in block {g.Blocks[t.Block].Name}, {s.SideRounds(g, g.Blocks[t.Block].Side)} on that side)");
				result.Act = Act.Seen;
			}
			var blk = g.Blocks[t.Block];
			if (!t.Committed)
			{
				var plan = PlanBurst(g, blk, p, direction);
				var burstPoint = plan.Point;
				float burstRange = plan.Range;
				if (!plan.Reach)
				{
					if (V3.Dot(burstPoint - p, direction) < 0f)
						return Drop(
							t,
							result,
							$"not fired (passes {Wide(p, direction, burstPoint):0.0} m wide of the burst point, outside the fragment field)");
					return result;
				}
				if (plan.SLeave < 0f) return Drop(t, result, "late (past the burst point)");
				float flight = burstRange / g.RoundSpeed;
				float want = now + Math.Max(0f, plan.SHit) / speed - flight;
				if (want > now + dt) return result;
				if (ready != null) return Drop(t, result, $"not fired ({ready})");
				if (s.Rounds[t.Block] <= 0 || s.Lost[t.Block])
					return Drop(t, result, $"not fired (block {blk.Name} {(s.Lost[t.Block] ? "lost" : "empty")})");
				string blind = SensorLoss(g, s, rel.Az);
				if (blind != null) return Drop(t, result, $"not fired ({blind})");
				float react = t.SeenAt + g.ReactionTime, own = s.SideFired[blk.Side] + g.Cooldown;
				float near = float.NegativeInfinity, lastAny = float.NegativeInfinity;
				for (int side = 0; side < s.SideFired.Length; side++)
				{
					lastAny = Math.Max(lastAny, s.SideFired[side]);
					if (side != blk.Side) near = Math.Max(near, s.SideFired[side] + g.OtherSide);
				}
				float launch = Math.Max(Math.Max(want, now - dt), Math.Max(react, Math.Max(own, near)));
				float leaveAt = now + plan.SLeave / speed;
				if (launch + flight > leaveAt + 1e-5f)
				{
					bool busy = Math.Max(own, near) > Math.Max(Math.Max(want, now - dt), react) + 1e-6f;
					string cause = !busy
						? $"late (track {now - t.SeenAt:0.000} s, {plan.SLeave / speed:0.000} s left in the fragment field's reach, reaction {g.ReactionTime:0.000} + flight {flight:0.000} s)"
						: $"busy (the {(launch == own ? "side's" : "other side's")} last launch {now - lastAny:0.000} s ago)";
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
				t.Axis = plan.Axis;
				t.Shots.Add(
					new Shot
					{
						Block = t.Block,
						FiredAt = launch,
						BurstAt = launch + flight,
						BurstRange = burstRange,
						BurstPoint = burstPoint,
						Axis = plan.Axis
					});
				for (int extra = 1; extra < g.RoundsPerThreat; extra++) FireExtra(g, s, t, p, direction, speed, now, dt, extra);
				result.Act = Act.Fire;
				result.Fired = true;
				result.FirstShot = 0;
				result.ShotCount = t.Shots.Count;
			}
			Shot due = null;
			foreach (var shot in t.Shots)
				if (!shot.Burst && shot.BurstAt <= now + dt + 1e-6f && (due == null || shot.BurstAt < due.BurstAt)) due = shot;
			if (due == null) return result;
			due.Burst = true;
			float move = speed * (due.BurstAt - now);
			if (move < -speed * dt) move = -speed * dt;
			result.Act = Act.Intercept;
			result.Moved = move;
			result.Hit = p + direction * move;
			result.Miss = (result.Hit - due.BurstPoint).Length;
			var coneAxis = g.ClimbBurst ? (result.Hit - due.BurstPoint).Normalized : due.Axis;
			result.Axis = coneAxis;
			result.Shot = due;
			result.InField = InField(g, result.Hit, due.BurstPoint, coneAxis);
			bool pending = false;
			foreach (var shot in t.Shots)
			{
				if (shot.Burst) continue;
				pending = true;
				if (shot.BurstAt <= now + dt + 1e-6f) result.More = true;
			}
			if (!pending) t.Done = true;
			return result;
		}

		static void FireExtra(Geometry g, State s, Track t, V3 p, V3 direction, float speed, float now, float dt, int extra)
		{
			int block = t.Block;
			bool sectorOpen = !g.PickTube || s.SectorLeft(g, t.Sector) > 0;
			if (s.Lost[block] || s.Rounds[block] <= 0 || !sectorOpen)
			{
				if (g.PickTube || !g.OverlapPick) return;
				block = PickCovering(g, s, p, direction, (p - g.Origin).Az);
				if (block < 0 || s.Lost[block] || s.Rounds[block] <= 0) return;
			}
			var spare = g.Blocks[block];
			var first = t.Shots[0];
			float burstAt = first.BurstAt + extra * g.RoundGap;
			V3 point, axis;
			float range;
			if (g.ClimbBurst)
			{
				var threatThen = p + direction * (speed * (burstAt - now));
				point = threatThen + new V3(0f, g.BurstHeight, 0f);
				range = (point - spare.Mouth).Length;
				axis = (threatThen - point).Normalized;
			}
			else
			{
				var plan = PlanBurst(g, spare, p, direction);
				point = plan.Point;
				range = plan.Range;
				axis = plan.Axis;
			}
			float flight = range / g.RoundSpeed;
			float launch = Math.Max(Math.Max(burstAt - flight, now - dt), first.FiredAt);
			s.Rounds[block]--;
			if (g.PickTube) s.Drawn[t.Sector, block]++;
			s.SideFired[spare.Side] = Math.Max(s.SideFired[spare.Side], launch);
			t.Shots.Add(
				new Shot
				{
					Block = block,
					FiredAt = launch,
					BurstAt = launch + flight,
					BurstRange = range,
					BurstPoint = point,
					Axis = axis
				});
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
