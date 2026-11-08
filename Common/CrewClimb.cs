namespace T80UM2.Common
{
	internal sealed class CrewClimb
	{
		internal enum Phase { In, ClimbingOut, Out, ClimbingIn }

		internal float OutSeconds = 3f;
		internal float InSeconds = 2.5f;
		internal float Linger = 3f;

		internal Phase State { get; private set; } = Phase.In;
		internal float T { get; private set; }

		float _idle;

		internal bool MayFire => State == Phase.Out;
		internal bool HatchOpen => State != Phase.In;
		internal bool BodyOut => State == Phase.Out || (State == Phase.ClimbingOut && T >= OutSeconds * 0.5f) || (State == Phase.ClimbingIn && T < InSeconds * 0.5f);

		internal bool Step(float dt, bool wantOut, bool busy, bool able)
		{
			var before = State;
			if (!able) return false;
			T += dt;
			switch (State)
			{
				case Phase.In:
					if (wantOut) Go(Phase.ClimbingOut, 0f);
					break;
				case Phase.ClimbingOut:
					if (T >= OutSeconds) Go(Phase.Out, 0f);
					break;
				case Phase.Out:
					_idle = wantOut || busy ? 0f : _idle + dt;
					if (_idle >= Linger) Go(Phase.ClimbingIn, 0f);
					break;
				case Phase.ClimbingIn:
					if (wantOut) Go(Phase.ClimbingOut, OutSeconds * (1f - Clamp01(T / InSeconds)));
					else if (T >= InSeconds) Go(Phase.In, 0f);
					break;
			}
			return State != before;
		}

		void Go(Phase p, float t) { State = p; T = t; _idle = 0f; }

		static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

		internal static (int full, int part) Belts(int total, int belt) => belt <= 0 ? (0, total) : (total / belt, total % belt);
	}
}
