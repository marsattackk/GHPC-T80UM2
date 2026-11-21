using System;
using System.Linq;
using T80UM2.Common;
using Xunit;

namespace T80UM2.Tests
{
	// The hard-kill engagement logic in Common\ActiveProtectionMath.cs, on two stations made up for the test: launcher tubes
	// across the front on two sides, and blocks all round with radars of their own. A threat flies a straight line frame by
	// frame and Step is called the way the game calls it, with the turret at rest.
	public class ApsMathTests
	{
		static readonly V3 BoxCentre = new V3(0f, 1.15f, 0.1f);
		static readonly V3 BoxHalf = new V3(1.8f, 1.15f, 3.55f);
		static readonly V3 Aim = new V3(0f, 1.3f, 0.5f);

		static ApsMath.Block Tube(string name, int side, float min, float max, float axis, V3 mouth) => new ApsMath.Block
		{
			Name = name,
			Side = side,
			AzMin = min,
			AzMax = max,
			AxisAz = axis,
			Mouth = mouth,
			Rounds = 2,
		};

		// Four tubes, two each side of the gun, 20 deg of front each.
		static ApsMath.Geometry Front()
		{
			var g = new ApsMath.Geometry
			{
				Blocks = new[]
				{
					Tube("left outer", 0, -40f, -20f, -20f, new V3(-1.849f, 1.7805f, -0.495f)),
					Tube("left inner", 0, -20f, 0f, 0f, new V3(-1.6025f, 1.6255f, -0.267f)),
					Tube("right inner", 1, 0f, 20f, 0f, new V3(1.6375f, 1.631f, -0.332f)),
					Tube("right outer", 1, 20f, 40f, 20f, new V3(1.874f, 1.7835f, -0.5455f)),
				},
				ConeHalf = 30f,
				ConeReach = 5f,
				PickTube = true,
			};
			g.Origin = new V3(0f, 1.705f, -0.41f);
			return g;
		}

		static ApsMath.Block Launcher(string name, int side, float min, float max, V3 mouth) => new ApsMath.Block
		{
			Name = name,
			Side = side,
			AzMin = min,
			AzMax = max,
			AxisAz = (min + max) / 2f,
			AxisEl = 60f,
			Mouth = mouth,
			Rounds = 3,
		};

		static ApsMath.Sensor Radar(string name, float min, float max) => new ApsMath.Sensor { Name = name, AzMin = min, AzMax = max };

		// Four blocks whose sectors overlap and cross the rear, six radars with an arc each.
		static ApsMath.Geometry AllRound()
		{
			var g = new ApsMath.Geometry
			{
				Blocks = new[]
				{
					Launcher("front left", 0, -115f, 5f, new V3(-0.9f, 2.1f, 0.2f)),
					Launcher("front right", 1, -45f, 75f, new V3(0.9f, 2.1f, 0.2f)),
					Launcher("rear right", 2, 60f, 180f, new V3(1f, 2.1f, -1f)),
					Launcher("rear left", 3, -210f, -90f, new V3(-1f, 2.1f, -1f)),
				},
				Sensors = new[]
				{
					Radar("front", -30f, 30f),
					Radar("front right", 30f, 90f),
					Radar("rear right", 90f, 150f),
					Radar("rear", 150f, 210f),
					Radar("rear left", -150f, -90f),
					Radar("front left", -90f, -30f),
				},
				ClimbBurst = true,
				BurstHeight = 5f,
				InterceptRange = 10f,
				ConeHalf = 45f,
				ConeReach = 7f,
				OverlapPick = true,
			};
			g.Origin = new V3(0f, 2f, 0f);
			return g;
		}

		sealed class Flight
		{
			public ApsMath.Track Track = new ApsMath.Track();
			public bool Fired;
			public bool Intercepted;
			public ApsMath.Result Hit;
		}

		// A threat at `speed` from `from` towards `aim` at 60 frames a second, until it gets there or the track ends.
		static Flight Fly(ApsMath.Geometry g, ApsMath.State s, V3 from, V3 aim, float speed, string ready = null, float start = 0f)
		{
			const float dt = 1f / 60f;
			var flight = new Flight();
			var d = (aim - from).Normalized;
			var p = from;
			float now = start;
			float total = (aim - from).Length / speed + 0.2f;
			for (float flown = 0f; flown < total; flown += dt)
			{
				now += dt;
				p = p + d * (speed * dt);
				bool atBox = ApsMath.RayHitsBox(p, d, BoxCentre, BoxHalf);
				ApsMath.Result r;
				do
				{
					r = ApsMath.Step(g, s, flight.Track, p, d * speed, atBox, now, dt, ready);
					if (r.Fired) flight.Fired = true;
					if (r.Act == ApsMath.Act.Intercept && !flight.Intercepted)
					{
						flight.Intercepted = true;
						flight.Hit = r;
					}
					if (r.Act == ApsMath.Act.Intercept && flight.Track.Done) p = r.Hit;
				}
				while (r.More);
				if (flight.Track.Done) break;
				if (V3.Dot(aim - p, d) < 0f) break;
			}
			return flight;
		}

		static V3 From(ApsMath.Geometry g, float az, float el, float range) => g.Origin + V3.FromAzEl(az, el) * range;

		static string Why(Flight flight) => flight.Track.Why ?? "";

		[Fact]
		public void Front_sector_edges()
		{
			var g = Front();
			Assert.Equal(3, ApsMath.BlockFor(g, 39.9f));
			Assert.Equal(3, ApsMath.BlockFor(g, 40f));
			Assert.Equal(-1, ApsMath.BlockFor(g, 40.1f));
			Assert.Equal(0, ApsMath.BlockFor(g, -40f));
			Assert.Equal(-1, ApsMath.BlockFor(g, -40.1f));
			Assert.Equal(2, ApsMath.BlockFor(g, 0f));
			Assert.Equal(1, ApsMath.BlockFor(g, -0.01f));
			Assert.Equal(3, ApsMath.BlockFor(g, 20f));
		}

		[Theory]
		[InlineData(-32f, 70f)]
		[InlineData(-9f, 208f)]
		[InlineData(6f, 375f)]
		[InlineData(25f, 700f)]
		public void A_threat_from_the_front_bursts_6_to_8_m_out_with_the_threat_in_the_fragments(float az, float speed)
		{
			var g = Front();
			var s = new ApsMath.State(g);
			var flight = Fly(g, s, From(g, az, 1f, 300f), Aim, speed);
			Assert.True(flight.Fired && flight.Intercepted, Why(flight));
			float fromMouth = (flight.Track.BurstPoint - g.Blocks[flight.Track.Block].Mouth).Length;
			Assert.InRange(fromMouth, g.BurstMin - 0.01f, g.BurstMax + 0.01f);
			Assert.True(flight.Hit.InField, $"{flight.Hit.Miss:0.000} m from the burst point");
			Assert.True(flight.Track.FiredAt - flight.Track.SeenAt >= g.ReactionTime - 1e-5f, "launched before the reaction time");
		}

		[Theory]
		[InlineData(90f, 1f)]
		[InlineData(5f, 30f)]
		[InlineData(-60f, 0f)]
		public void Outside_every_sector_nothing_fires(float az, float el)
		{
			var g = Front();
			var s = new ApsMath.State(g);
			var flight = Fly(g, s, From(g, az, el, 200f), Aim, 208f);
			Assert.False(flight.Fired, Why(flight));
			Assert.Contains("outside every sector", Why(flight));
			Assert.Equal(8, s.Total);
		}

		[Fact]
		public void Too_close_and_too_late()
		{
			var g = Front();
			var level = new V3(0f, 1.65f, 0.5f);
			Assert.StartsWith("too close", Why(Fly(g, new ApsMath.State(g), From(g, 5f, 0f, 9f), level, 150f)));
			Assert.StartsWith("late", Why(Fly(g, new ApsMath.State(g), From(g, 5f, 0f, 25f), level, 700f)));
			var grenade = Fly(g, new ApsMath.State(g), From(g, 8f, 0f, 11.2f), new V3(1.6f, 1.65f, 0.5f), 75f);
			Assert.True(grenade.Intercepted, "a grenade at 75 m/s from 11.2 m: " + Why(grenade));
		}

		[Fact]
		public void Two_rounds_per_sector_then_a_reload_gives_one_back()
		{
			var g = Front();
			var s = new ApsMath.State(g);
			for (int i = 0; i < 2; i++)
				Assert.True(Fly(g, s, From(g, 8f, 1f, 200f), Aim, 208f, start: 3f * i).Intercepted, $"threat {i + 1}");
			var third = Fly(g, s, From(g, 8f, 1f, 200f), Aim, 208f, start: 6f);
			Assert.False(third.Intercepted);
			Assert.Contains("already fired from this direction", Why(third));
			Assert.Equal(2, s.SideRounds(g, 1));
			Assert.Equal(0, s.SectorLeft(g, 2));
			int spent = Enumerable.Range(0, s.Rounds.Length).First(b => s.Rounds[b] < g.Blocks[b].Rounds);
			s.Reload(spent);
			Assert.Equal(1, s.SectorLeft(g, 2));
			Assert.True(Fly(g, s, From(g, 8f, 1f, 200f), Aim, 208f, start: 9f).Intercepted, "after the reload");
		}

		[Fact]
		public void Not_ready_fires_nothing_and_says_why()
		{
			var g = Front();
			var s = new ApsMath.State(g);
			var flight = Fly(g, s, From(g, 8f, 1f, 200f), Aim, 208f, "hatch open (commander)");
			Assert.False(flight.Fired);
			Assert.Contains("hatch open", Why(flight));
			Assert.Equal(8, s.Total);
		}

		[Fact]
		public void A_frame_the_threat_does_not_move_keeps_its_last_speed()
		{
			var g = Front();
			var forward = new V3(0f, 0f, -208f);
			var moving = ApsMath.FrameVelocity(true, forward * 0.016f, 0.016f, false, default(V3), forward);
			Assert.InRange(moving.Length, 207.5f, 208.5f);
			var still = ApsMath.FrameVelocity(true, new V3(0f, 0f, 0f), 0.016f, true, moving, forward);
			Assert.Null(ApsMath.Window(g, still.Length, 0.135f, false));
			var first = ApsMath.FrameVelocity(true, new V3(0f, 0f, 0f), 0.016f, false, default(V3), forward);
			Assert.InRange(first.Length, 207.5f, 208.5f);
		}

		[Fact]
		public void The_window_takes_slow_big_rounds_only()
		{
			var g = Front();
			Assert.Null(ApsMath.Window(g, 208f, 0.135f, false));
			Assert.Null(ApsMath.Window(g, 700f, 0.073f, false));
			Assert.NotNull(ApsMath.Window(g, 1690f, 0.03f, false));
			Assert.NotNull(ApsMath.Window(g, 50f, 0.1f, false));
			Assert.NotNull(ApsMath.Window(g, 300f, 0.02f, false));
			Assert.NotNull(ApsMath.Window(g, 200f, 0.1f, true));
		}

		[Fact]
		public void Blocks_and_radars_read_front_first_left_before_right()
		{
			var g = AllRound();
			var s = new ApsMath.State(g);
			Assert.Equal("FL 3 FR 3 RL 3 RR 3", ApsMath.BlockSummary(g, s));
			Assert.Equal("6 of 6", ApsMath.RadarSummary(g, s));
			s.Rounds[1] = 1;
			s.Lost[3] = true;
			s.SensorLost[1] = true;
			s.SensorLost[4] = true;
			Assert.Equal("FL 3 FR 1 RL lost RR 3", ApsMath.BlockSummary(g, s));
			Assert.Equal("4 of 6, front right, rear left lost", ApsMath.RadarSummary(g, s));
			Assert.Equal("L", ApsMath.BlockLabel(new ApsMath.Block { Name = "left" }));
		}

		// The arc arithmetic written out the long way: is az within [min, max) going clockwise round the circle.
		static bool InArc(float az, float min, float max)
		{
			float width = max - min;
			if (width < 0f) width += 360f;
			if (width >= 360f) return true;
			float offset = (((az - min) % 360f) + 360f) % 360f;
			return offset < width;
		}

		[Fact]
		public void Sectors_wrap_round_the_rear()
		{
			var g = AllRound();
			for (int degrees = -180; degrees <= 180; degrees++)
			{
				float az = degrees;
				int first = -1;
				for (int i = 0; i < g.Blocks.Length; i++)
				{
					bool held = ApsMath.Holds(az, g.Blocks[i].AzMin, g.Blocks[i].AzMax, false);
					Assert.True(InArc(az, g.Blocks[i].AzMin, g.Blocks[i].AzMax) == held, $"{az} deg, {g.Blocks[i].Name}");
					if (held && first < 0) first = i;
				}
				Assert.True(first >= 0, $"{az} deg held by no block");
				Assert.True(first == ApsMath.BlockFor(g, az), $"{az} deg: BlockFor is the first block that holds it");
			}
			Assert.Equal(3, ApsMath.BlockFor(g, 180f));
			Assert.Equal(3, ApsMath.BlockFor(g, -180f));
			Assert.Equal(2, ApsMath.BlockFor(g, 179f));
			Assert.True(ApsMath.Holds(180f, 170f, -170f, false) && ApsMath.Holds(-180f, 170f, -170f, false), "170..-170 holds 180");
			Assert.True(ApsMath.Holds(170f, 170f, -170f, false) && !ApsMath.Holds(-170f, 170f, -170f, false), "start in, end out");
			Assert.True(ApsMath.Holds(-170f, 170f, -170f, true), "a closed arc holds its end");
			Assert.True(ApsMath.Holds(-123f, 0f, 360f, false), "0..360 is the whole circle");
			var lone = new ApsMath.Geometry { Blocks = new[] { new ApsMath.Block { Name = "lone", AzMin = -210f, AzMax = -90f } } };
			Assert.Equal(0, ApsMath.BlockFor(lone, -90f));
			Assert.Equal(-1, ApsMath.BlockFor(lone, -89f));
		}

		[Fact]
		public void A_cooldown_for_each_side()
		{
			Assert.Equal(2, new ApsMath.State(Front()).SideFired.Length);
			Assert.Equal(4, new ApsMath.State(AllRound()).SideFired.Length);
			var wide = AllRound();
			wide.Blocks[3].Side = 5;
			Assert.Equal(6, new ApsMath.State(wide).SideFired.Length);
		}

		[Fact]
		public void The_kill_and_break_up_rolls_follow_their_settings()
		{
			var rng = new Random(12345);
			foreach (var p in new[] { 1f, 0.78f, 0.7f })
			{
				int kills = Enumerable.Range(0, 10000).Count(i => ApsMath.Kills(p, (float)rng.NextDouble()));
				Assert.InRange(kills / 10000.0, p - 0.01, p + 0.01);
			}
			int broken = Enumerable.Range(0, 10000).Count(i => ApsMath.BrokenUp(0.25f, (float)rng.NextDouble()));
			Assert.InRange(broken / 10000.0, 0.24, 0.26);
			Assert.DoesNotContain(Enumerable.Range(0, 1000), i => ApsMath.BrokenUp(0f, (float)rng.NextDouble()));
		}
	}
}
