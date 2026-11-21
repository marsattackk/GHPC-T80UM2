using System;
using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Crew;
using GHPC.Player;
using GHPC.Vehicle;
using GHPC.Weapons;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using DC = GHPC.Equipment.DestructibleComponent;
using System.Reflection;

namespace T80UM2.Common
{
	// Hard-kill APS. A postfix on LiveRoundBatchHandler.Update sees every round once GHPC has moved it, and ApsMath
	// makes the calls. Tube mouths and the radar origin are in turret frame, the protected box in the hull's.
	internal static class Aps
	{
		internal const string GroundLaunchEvent = "event:/Weapons/launcher_smoke";

		internal sealed class BlockSpec
		{
			public string Name;
			public Vector3[] Mouths;
			public float AxisAz, AxisEl;
			public float AzMin, AzMax;
			public int Side;
			public string Module;
		}

#pragma warning disable CS0649
		internal sealed class SensorSpec
		{
			public string Name;
			public string Module;
			public float AzMin, AzMax;
		}
#pragma warning restore CS0649

		internal sealed class Settings
		{
			public string Tag = "Port APS";
			public string Name = "APS";
			public bool OnAtSpawn = true;
			public float WarmUp = 300f;
			public bool HatchInterlock = true;
			public BlockSpec[] Blocks = new BlockSpec[0];
			public int RoundsPerBlock = 2;
			public string[] SensorModules = new string[0];
			public SensorSpec[] Sensors = new SensorSpec[0];
#pragma warning disable CS0649   // a port without radar sensors leaves it unset
			public Vector3? RadarOrigin;
#pragma warning restore CS0649
			public string[] SideLabels = { "L", "R" };
			public string[] SideNames = { "left side", "right side" };
			public bool HudPerBlock = false;
			public string ComputerModule;
			public float ElMin = -6f, ElMax = 20f, SearchRange = 330f, TrackRange = 130f, MinRange = 11f;
			public float SpeedMin = 70f, SpeedMax = 700f, SizeMin = 0.036f, ReactionTime = 0.02f;
			public float RoundSpeed = 190f,
				InterceptRange = 6.7f,
				BurstMin = 6f,
				BurstMax = 8f,
				FragReach = 1.5f,
				ConeReach = 3f,
				Cooldown = 0.35f,
				OtherSide = 0.035f;
			public bool PickTube = false;
			public bool PickOverlap = false;
			public bool ClimbBurst = false;
			public float BurstHeight = 0f;
			public int RoundsPerThreat = 1;
			public float RoundGap = 0.02f;
			public string VisualPrefix;
			public float ProtectMargin = 0f;
			public float HeadingSmoothing = 0.15f;
			public string[] BoxLeaveOut = { };
			public float PAtgm = 1f, PGrenade = 0.7f, PHeat = 0.78f, PHe = 0.78f;
			public float PBrokenUp = 0f;
			public float WarheadFragShare = 1f;
			public string[] GrenadePrefixes = { "PG-", "RPG", "M72", "S-5", "S-8", "Hydra" };
			public int FragCount = 60;
			public float FragConeHalf = 15f, FragSpeed = 1600f;
			public float FragMass = 0.003f, FragArea = 4.86e-5f, FragCoeff = 0.6f, FragPen = 6f, FragPenSpeed = 1200f;
			public float DangerRange = 25f;
			public float ReloadPerRound = 60f, ReloadMaxSpeed = 0.5f;
			public string LaunchEvent = "event:/Weapons/launcher_9M14";
			public string CapPrefix = "aps_cap_";
			internal Settings Copy() => (Settings)MemberwiseClone();
		}

		struct Motion
		{
			public int Id;
			public Vector3 Prev;
			public int PrevFrame;
			public V3 Vel;
			public bool HasVel;
			public Vector3 Mean;
			public bool HasMean;
			public bool Ignored;
			public int LastFrame;
		}

		internal sealed class Watched
		{
			public LiveRound Round;
			public int Id;
			public ApsMath.Track T = new ApsMath.Track();
			public Dictionary<ApsMath.Shot, (int tube, Vector3 mouth)> Tubes = new Dictionary<ApsMath.Shot, (int tube, Vector3 mouth)>();
			public bool Launched;
			public bool Killed;
			public V3 KillPoint;
			public int LastFrame;
		}

		internal sealed class Station : MonoBehaviour
		{
			internal Settings S;
			internal Vehicle Vehicle;
			internal ApsMath.Geometry G;
			internal ApsMath.State St;
			internal bool On { get; private set; }

			internal float ReadyAt;
			internal bool Reloading;
			internal float ReloadLeft;
			internal int Fired, Intercepts;
			internal Transform Turret;
			internal DC[] BlockDc, SensorDc;
			internal DC ComputerDc;
			internal Renderer[][] Caps;
			internal Vector3[][] Mouths;
			internal Bounds Box;
			readonly Dictionary<LiveRound, Motion> _seen = new Dictionary<LiveRound, Motion>();
			readonly Dictionary<LiveRound, Watched> _tracks = new Dictionary<LiveRound, Watched>();
			readonly List<LiveRound> _gone = new List<LiveRound>();
			bool _setUp, _modulesBound;
			float _nextModuleLook;
			int Key => GetInstanceID();

			internal string Setup(Vehicle v, Settings s)
			{
				if (_setUp) return "already set up";
				_setUp = true;
				Vehicle = v;
				S = s;
				Turret = Followers.TurretBone(v);
				if (Turret == null) return "no turret bone found: the APS stays off";
				var root = v.transform;
				// The port's points are for the turret at rest. A kit can go on at PlayerReady with the turret turned, so
				// they go through the turret's rest pose, not where it points now.
				var turretRest = TurretRest(v, Turret, out bool restKnown);
				var restToWorld = (Turret.parent != null ? Turret.parent.localToWorldMatrix : Matrix4x4.identity)
					* Matrix4x4.TRS(Turret.localPosition, turretRest, Turret.localScale);
				var worldToRest = restToWorld.inverse;
				Vector3 Local(Vector3 prefab) => worldToRest.MultiplyPoint3x4(root.TransformPoint(prefab));
				Mouths = s.Blocks.Select(b => b.Mouths.Select(Local).ToArray()).ToArray();
				G = new ApsMath.Geometry
				{
					ElMin = s.ElMin,
					ElMax = s.ElMax,
					SearchRange = s.SearchRange,
					TrackRange = s.TrackRange,
					MinRange = s.MinRange,
					SpeedMin = s.SpeedMin,
					SpeedMax = s.SpeedMax,
					SizeMin = s.SizeMin,
					ReactionTime = s.ReactionTime,
					RoundSpeed = s.RoundSpeed,
					InterceptRange = s.InterceptRange,
					BurstMin = s.BurstMin,
					BurstMax = s.BurstMax,
					FragReach = s.FragReach,
					ConeHalf = s.FragConeHalf,
					ConeReach = s.ConeReach,
					Cooldown = s.Cooldown,
					OtherSide = s.OtherSide,
					PickTube = s.PickTube,
					OverlapPick = s.PickOverlap,
					ClimbBurst = s.ClimbBurst,
					BurstHeight = s.BurstHeight,
					RoundsPerThreat = Mathf.Max(1, s.RoundsPerThreat),
					RoundGap = s.RoundGap,
					Sensors = s.Sensors
						.Select(x => new ApsMath.Sensor { Name = x.Name, AzMin = x.AzMin, AzMax = x.AzMax })
						.ToArray(),
					Blocks = s.Blocks.Select((block, blockIndex) =>
					{
						var center = Vector3.zero;
						foreach (var mouth in Mouths[blockIndex]) center += mouth;
						center /= Mathf.Max(1, block.Mouths.Length);
						return new ApsMath.Block
						{
							Name = block.Name,
							Mouth = new V3(center.x, center.y, center.z),
							AxisAz = block.AxisAz,
							AxisEl = block.AxisEl,
							AzMin = block.AzMin,
							AzMax = block.AzMax,
							Side = block.Side,
							Rounds = Mathf.Min(s.RoundsPerBlock, block.Mouths.Length)
						};
					}).ToArray(),
				};
				var origin = Vector3.zero;
				foreach (var block in G.Blocks) origin += new Vector3(block.Mouth.x, block.Mouth.y, block.Mouth.z);
				origin /= Mathf.Max(1, G.Blocks.Length);
				G.Origin = new V3(0f, origin.y, origin.z);
				if (s.RadarOrigin.HasValue)
				{
					var given = Local(s.RadarOrigin.Value);
					G.Origin = new V3(given.x, given.y, given.z);
				}
				St = new ApsMath.State(G);
				BlockDc = new DC[G.Blocks.Length];
				SensorDc = new DC[Mathf.Max(2, s.Sensors.Length)];
				Box = OwnBox(v, s, Turret, restToWorld);
				Caps = BindCaps(s, out var capNote);
				if (s.OnAtSpawn)
				{
					On = true;
					ReadyAt = 0f;
				}
				Stations.Add(this);
				EnsurePatched();
				return $"{G.Blocks.Length} blocks ({string.Join("; ", G.Blocks.Select((b, i) => $"{b.Name} {b.AzMin:+0;-0}..{b.AzMax:+0;-0} deg, axis {b.AxisAz:+0;-0}, mouth {b.Mouth} turret frame, burst {b.Burst(G.InterceptRange)}, {St.Rounds[i]} rounds"))}), "
					+ $"radar origin {G.Origin}, protected box {Box.center:F2} size {Box.size:F2} (vehicle frame; {_boxParts}), {(On ? "on at spawn" : "off at spawn")}, {capNote}"
					+ (restKnown ? "" : ", turret's rest pose unknown: taken as it points now");
			}

			static readonly AccessTools.FieldRef<AimablePlatform, Quaternion> RestRotation =
				Reflect.FieldRefAccess<AimablePlatform, Quaternion>("_originalLocalRotation");

			static Quaternion TurretRest(Vehicle v, Transform turret, out bool known)
			{
				known = RestRotation != null;
				if (known)
				{
					foreach (var link in v.GetComponentsInChildren<TiltedRing.Link>(true))
						if (link.Turret == turret && link.Platform != null)
							return link.Tilt * RestRotation(link.Platform) * Quaternion.Inverse(link.Tilt);
					foreach (var platform in v.GetComponentsInChildren<AimablePlatform>(true))
						if (platform.Transform == turret)
							return RestRotation(platform);
				}
				known = false;
				return turret.localRotation;
			}

			static bool Shown(MeshRenderer renderer)
			{
				if (!renderer.gameObject.activeInHierarchy) return false;
				if (renderer.enabled) return true;
				foreach (string suffix in new[] { EraBricks.OpenSuffix, EraBricks.LidSuffix, EraBricks.SootSuffix })
				{
					if (!renderer.name.EndsWith(suffix, StringComparison.Ordinal) || renderer.transform.parent == null) continue;
					var brick = renderer.transform.parent.Find(renderer.name.Substring(0, renderer.name.Length - suffix.Length));
					return brick != null && brick.gameObject.activeInHierarchy;
				}
				return false;
			}

			static Bounds OwnBox(Vehicle v, Settings s, Transform turret, Matrix4x4 turretRest)
			{
				var root = v.transform;
				var pts = new List<(Vector3 p, string n)>();
				void Corners(Bounds localBounds, Transform part, string partName)
				{
					bool onTurret = part.IsChildOf(turret);
					for (int i = 0; i < 8; i++)
					{
						var world = part.TransformPoint(localBounds.center
							+ Vector3.Scale(
								localBounds.extents,
								new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
						if (onTurret) world = turretRest.MultiplyPoint3x4(turret.InverseTransformPoint(world));
						pts.Add((root.InverseTransformPoint(world), partName));
					}
				}
				string source = "armour colliders";
				foreach (var collider in v.GetComponentsInChildren<Collider>(true))
				{
					if (collider == null
						|| collider.isTrigger
						|| !SwitchedOn(collider, root)
						|| !collider.TryGetComponent<IArmor>(out _)
						|| LeftOut(collider.transform, s.BoxLeaveOut))
						continue;
					if (collider is BoxCollider boxCollider)
						Corners(new Bounds(boxCollider.center, boxCollider.size), collider.transform, collider.name);
					else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
						Corners(meshCollider.sharedMesh.bounds, collider.transform, collider.name);
					else if (collider is SphereCollider sphereCollider)
						Corners(
							new Bounds(sphereCollider.center, Vector3.one * 2f * sphereCollider.radius),
							collider.transform,
							collider.name);
					else if (collider is CapsuleCollider capsuleCollider)
						Corners(
							new Bounds(
								capsuleCollider.center,
								Vector3.one * Mathf.Max(capsuleCollider.height, 2f * capsuleCollider.radius)),
							collider.transform,
							collider.name);
				}
				if (pts.Count == 0)
				{
					source = "visual parts";
					foreach (var renderer in v.GetComponentsInChildren<MeshRenderer>(true))
					{
						if (s.VisualPrefix != null && !Under(renderer.transform, s.VisualPrefix)) continue;
						if (LeftOut(renderer.transform, s.BoxLeaveOut)) continue;
						if (!Shown(renderer)) continue;
						var meshFilter = renderer.GetComponent<MeshFilter>();
						if (meshFilter == null || meshFilter.sharedMesh == null) continue;
						var meshBounds = meshFilter.sharedMesh.bounds;
						var size = Vector3.Scale(meshBounds.size, renderer.transform.lossyScale);
						var abs = new[] { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) };
						Array.Sort(abs);
						if (abs[1] < 0.05f) continue;
						Corners(meshBounds, renderer.transform, renderer.name);
					}
				}
				if (pts.Count == 0)
				{
					_boxParts = "nothing found: a default box";
					return new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(3.6f, 2.4f, 7.2f));
				}
				var bounds = new Bounds(pts[0].p, Vector3.zero);
				foreach (var (corner, _) in pts) bounds.Encapsulate(corner);
				string At(Func<Vector3, float> measure)
				{
					float best = float.MinValue;
					string bestName = null;
					foreach (var (point, pointName) in pts) if (measure(point) > best)
					{
						best = measure(point);
						bestName = pointName;
					}
					return bestName;
				}
				_boxParts = $"from {source}: lowest '{At(p => -p.y)}', highest '{At(x => x.y)}', rearmost '{At(y => -y.z)}', foremost '{At(z => z.z)}', widest '{At(a => Mathf.Abs(a.x))}'";
				bounds.Expand(2f * s.ProtectMargin);
				return bounds;
			}

			static string _boxParts;

			static bool SwitchedOn(Collider collider, Transform root)
			{
				if (!collider.enabled) return false;
				for (var t = collider.transform; t != null && t != root; t = t.parent)
					if (!t.gameObject.activeSelf)
						return false;
				return true;
			}

			static bool Under(Transform t, string prefix)
			{
				for (; t != null; t = t.parent) if (t.name.StartsWith(prefix)) return true;
				return false;
			}

			static bool LeftOut(Transform t, string[] names)
			{
				if (names == null || names.Length == 0) return false;
				for (;
					t != null;
					t = t.parent)
						foreach (var needle in names) if (t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
				return false;
			}

			Renderer[][] BindCaps(Settings s, out string note)
			{
				var free = Vehicle.GetComponentsInChildren<MeshRenderer>(true)
					.Where(r => r.name.StartsWith(s.CapPrefix))
					.ToList();
				int found = free.Count, bound = 0;
				float worst = 0f;
				var all = new Renderer[s.Blocks.Length][];
				for (int i = 0; i < s.Blocks.Length; i++)
				{
					var block = s.Blocks[i];
					all[i] = new Renderer[block.Mouths.Length];
					for (int k = 0; k < block.Mouths.Length; k++)
					{
						var mouthPoint = Turret.TransformPoint(Mouths[i][k]);
						var best = free.OrderBy(r => (r.transform.position - mouthPoint).sqrMagnitude).FirstOrDefault();
						if (best == null || (best.transform.position - mouthPoint).magnitude > 0.15f) continue;
						worst = Mathf.Max(worst, (best.transform.position - mouthPoint).magnitude);
						free.Remove(best);
						all[i][k] = best;
						bound++;
					}
				}
				note = found == 0
					? $"no {s.CapPrefix}* covers in the bundle (tubes shown open)"
					: $"{bound} of {found} covers bound to their tubes (at most {worst * 100f:0.0} cm off a mouth)";
				if (found > 0 && bound < s.Blocks.Sum(b => b.Mouths.Length))
					DonorParts.WarnOnce(
						"aps covers",
						$"{s.Tag}: only {bound} tube covers found for {s.Blocks.Sum(b => b.Mouths.Length)} tubes.");
				return all;
			}

			internal void ShowCaps()
			{
				if (Caps == null) return;
				for (int i = 0; i < Caps.Length; i++)
					for (int k = 0; k < Caps[i].Length; k++)
						if (Caps[i][k] != null) Caps[i][k].enabled = !St.Lost[i] && k < St.Rounds[i];
			}

			void BindModules()
			{
				if (_modulesBound || Time.time < _nextModuleLook) return;
				_nextModuleLook = Time.time + 1f;
				var set = Vehicle.GetComponent<ModuleBuilder.ModuleSet>();
				if (set == null || set.Built.Count == 0) return;
				_modulesBound = true;
				var found = new List<string>();
				foreach (var (module, damageControl) in set.Built)
				{
					for (int i = 0; i < S.Blocks.Length; i++)
						if (S.Blocks[i].Module != null && module.Id == S.Blocks[i].Module)
						{
							BlockDc[i] = damageControl;
							found.Add(module.Id);
						}
					for (int k = 0; k < S.SensorModules.Length && k < 2 && S.Sensors.Length == 0; k++)
						if (S.SensorModules[k] != null && module.Id == S.SensorModules[k])
						{
							SensorDc[k] = damageControl;
							found.Add(module.Id);
						}
					for (int k = 0; k < S.Sensors.Length; k++)
						if (S.Sensors[k].Module != null && module.Id == S.Sensors[k].Module)
						{
							SensorDc[k] = damageControl;
							found.Add(module.Id);
						}
					if (S.ComputerModule != null && module.Id == S.ComputerModule)
					{
						ComputerDc = damageControl;
						found.Add(module.Id);
					}
				}
			}

			internal string NotReady()
			{
				if (Vehicle == null || Vehicle.Neutralized) return "unit knocked out";
				if (!On) return "switched off";
				if (ComputerDc != null && ComputerDc.IsDestroyed) return "computer destroyed";
				if (Time.time < ReadyAt) return $"warming up, {ReadyAt - Time.time:0} s left";
				if (Reloading) return "reloading";
				if (S.HatchInterlock && HatchOpen() is string openHatch
				) return $"hatch open ({openHatch})";
				return null;
			}

			internal string HatchOpen()
			{
				var crew = Vehicle != null ? Vehicle.GetComponent<CrewManager>()?.CrewMembers : null;
				if (crew == null) return null;
				foreach (var member in crew)
					if (member?.ExitHatch != null && member.ExitHatch.Openness > 0.05f)
						return member.Position.ToString().ToLowerInvariant();
				return null;
			}

			internal void Set(bool on, string why)
			{
				if (on == On) return;
				On = on;
				if (on) ReadyAt = Time.time + S.WarmUp;
				if (IsPlayer) MelonLogger.Msg($"{S.Name}: {(on ? $"switched on, ready in {S.WarmUp / 60f:0.#} min" : "switched off")}.");
			}

			internal bool IsPlayer => PlayerInput.Instance != null && PlayerInput.Instance.CurrentPlayerUnit == Vehicle;

			internal int Empty
			{
				get
				{
					int count = 0;
					for (int i = 0; i < St.Rounds.Length; i++) if (!St.Lost[i]) count += G.Blocks[i].Rounds - St.Rounds[i];
					return count;
				}
			}

			internal void StartReload(string by)
			{
				if (Reloading || Empty == 0) return;
				Reloading = true;
				ReloadLeft = S.ReloadPerRound;
				if (IsPlayer) MelonLogger.Msg($"{S.Name}: reloading {Empty} round(s), {S.ReloadPerRound:0} s each with the tank stopped.");
			}

			internal void StopReload(string why)
			{
				if (!Reloading) return;
				Reloading = false;
				if (IsPlayer) MelonLogger.Msg($"{S.Name}: reload {why}; {St.Total} rounds loaded.");
			}

			internal bool Stopped
			{
				get
				{
					var body = Vehicle != null ? Vehicle.GetComponent<Rigidbody>() : null;
					return body == null || body.velocity.magnitude <= S.ReloadMaxSpeed;
				}
			}

			void Update()
			{
				if (Vehicle == null || G == null) return;
				BindModules();
				for (int i = 0; i < BlockDc.Length; i++)
					if (!St.Lost[i] && BlockDc[i] != null && BlockDc[i].IsDestroyed)
					{
						St.Lost[i] = true;
						if (IsPlayer) MelonLogger.Msg($"{S.Name}: {G.Blocks[i].Name} launcher block destroyed.");
						ShowCaps();
					}
				for (int k = 0; k < G.Sensors.Length; k++)
					if (!St.SensorLost[k] && SensorDc[k] != null && SensorDc[k].IsDestroyed)
					{
						St.SensorLost[k] = true;
						if (IsPlayer) MelonLogger.Msg($"{S.Name}: radar sensor {G.Sensors[k].Name} destroyed.");
					}
				BurstPending();
				if (Reloading)
				{
					if (Vehicle.Neutralized)
					{
						StopReload("stopped: unit knocked out");
						return;
					}
					if (!Stopped) return;
					ReloadLeft -= Time.deltaTime;
					if (ReloadLeft > 0f) return;
					int i;
					for (i = 0; i < St.Rounds.Length; i++) if (!St.Lost[i] && St.Rounds[i] < G.Blocks[i].Rounds) break;
					if (i < St.Rounds.Length)
					{
						St.Reload(i);
						ShowCaps();
					}
					if (Empty == 0) StopReload("done");
					else ReloadLeft = S.ReloadPerRound;
				}
			}

			void OnDestroy() => Stations.Remove(this);

			internal void BeginFrame()
			{
				int frame = Time.frameCount;
				_gone.Clear();
				foreach (var pair in _seen)
					if (pair.Key == null || pair.Key.IsDestroyed || pair.Key.ID != pair.Value.Id || pair.Value.LastFrame < frame - 1)
						_gone.Add(pair.Key);
				foreach (var round in _gone) _seen.Remove(round);
				_gone.Clear();
				foreach (var pair in _tracks)
					if (pair.Key == null || pair.Key.IsDestroyed || pair.Key.ID != pair.Value.Id || pair.Value.LastFrame < frame - 1)
						_gone.Add(pair.Key);
				foreach (var round in _gone) _tracks.Remove(round);
			}

			internal void Consider(LiveRound r, Vector3 pos, float now, float dt)
			{
				if (Turret == null || r.Shooter == Vehicle) return;
				var center = Turret.position;
				if ((pos - center).sqrMagnitude > S.SearchRange * S.SearchRange) return;
				if (!_seen.TryGetValue(r, out var motion) || motion.Id != r.ID) motion = new Motion { Id = r.ID, PrevFrame = -10 };
				if (_tracks.TryGetValue(r, out var watched) && watched.Id != r.ID)
				{
					_tracks.Remove(r);
					watched = null;
				}
				int frame = Time.frameCount;
				var forwardVelocity = r.transform.forward * (r.CurrentSpeed > 1f
					? r.CurrentSpeed
					: (r.Info != null ? r.Info.MuzzleVelocity : 0f));
				var movement = pos - motion.Prev;
				var frameVelocity = ApsMath.FrameVelocity(
					motion.PrevFrame == frame - 1,
					new V3(movement.x, movement.y, movement.z),
					dt,
					motion.HasVel,
					motion.Vel,
					new V3(forwardVelocity.x, forwardVelocity.y, forwardVelocity.z));
				Vector3 vel = new Vector3(frameVelocity.x, frameVelocity.y, frameVelocity.z);
				motion.Vel = frameVelocity;
				motion.HasVel = true;
				motion.Prev = pos;
				motion.PrevFrame = frame;
				motion.LastFrame = frame;
				if (watched != null) watched.LastFrame = frame;
				if (motion.Ignored || (watched != null && watched.T.Done))
				{
					_seen[r] = motion;
					return;
				}
				var turretPoint = Turret.InverseTransformPoint(pos);
				var turretVelocity = Turret.InverseTransformDirection(vel);
				var localPoint = new V3(turretPoint.x, turretPoint.y, turretPoint.z);
				var localVelocity = new V3(turretVelocity.x, turretVelocity.y, turretVelocity.z);
				var root = Vehicle.transform;
				if (!motion.HasMean)
				{
					motion.Mean = vel;
					motion.HasMean = true;
				}
				else motion.Mean += (vel - motion.Mean) * (1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, S.HeadingSmoothing)));
				var hullPoint = root.InverseTransformPoint(pos);
				var hullDirection = root.InverseTransformDirection(motion.Mean.normalized);
				bool atBox = ApsMath.RayHitsBox(
					new V3(hullPoint.x, hullPoint.y, hullPoint.z),
					new V3(hullDirection.x, hullDirection.y, hullDirection.z),
					new V3(Box.center.x, Box.center.y, Box.center.z),
					new V3(Box.extents.x, Box.extents.y, Box.extents.z));
				float diameter = r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f;
				if (!ApsMath.InWindow(G, vel.magnitude, diameter, r.IsSpall))
				{
					if (atBox && (localPoint - G.Origin).Length <= G.TrackRange)
					{
						motion.Ignored = true;
						if (watched != null) watched.T.Done = true;
					}
					_seen[r] = motion;
					return;
				}
				_seen[r] = motion;
				if (watched == null)
				{
					watched = new Watched { Round = r, Id = r.ID, LastFrame = frame };
					_tracks[r] = watched;
				}
				if (!watched.T.Committed && Claimed(r, Key))
				{
					watched.T.Done = true;
					watched.T.Why = "left to another station that engages it";
					Report(r, watched.T, vel.magnitude, diameter, localPoint);
					return;
				}
				string ready = NotReady();
				if (ready == null && G.Blocks.Length > 0 && G.Sensors.Length == 0)
				{
					float azimuth = (localPoint - G.Origin).Az;
					int side = azimuth < 0f ? 0 : 1;
					if (SensorDc[side] != null && SensorDc[side].IsDestroyed)
						ready = $"the {(side == 0 ? "left" : "right")} radar head lost";
				}
				ApsMath.Result result;
				// One burst per Step. Several rounds at one threat can all be due in the same frame.
				do
				{
					result = ApsMath.Step(G, St, watched.T, localPoint, localVelocity, atBox, now, dt, ready);
					if (result.Fired) Launch(r, watched, result);
					if (result.Act == ApsMath.Act.Intercept)
					{
						if (watched.Killed) BurstOnNothing(watched, result.Shot, result.Hit);
						else Burst(r, watched, result, vel);
					}
					else if (result.Act == ApsMath.Act.Drop) Report(r, watched.T, vel.magnitude, diameter, localPoint);
				}
				while (result.More);
				if (watched.Killed && !watched.T.Done) HandOver(watched);
			}

			readonly List<(Watched watched, ApsMath.Shot shot)> _pending = new List<(Watched, ApsMath.Shot)>();

			void HandOver(Watched w)
			{
				foreach (var shot in w.T.Shots)
					if (!shot.Burst)
						_pending.Add((w, shot));
				w.T.Done = true;
			}

			void BurstPending()
			{
				for (int i = _pending.Count - 1; i >= 0; i--)
				{
					var (watched, shot) = _pending[i];
					if (Time.time < shot.BurstAt) continue;
					_pending.RemoveAt(i);
					BurstOnNothing(watched, shot, watched.KillPoint);
				}
			}

			void BurstOnNothing(Watched w, ApsMath.Shot shot, V3 threat)
			{
				shot.Burst = true;
				var burstWorld = Turret.TransformPoint(new Vector3(shot.BurstPoint.x, shot.BurstPoint.y, shot.BurstPoint.z));
				var axis = G.ClimbBurst ? (threat - shot.BurstPoint).Normalized : shot.Axis;
				var axisWorld = Turret.TransformDirection(new Vector3(axis.x, axis.y, axis.z));
				Blast.Play(burstWorld, Blast.Size.ApsBurst);
				int frags = Fragments(burstWorld, axisWorld);
			}

			void Launch(LiveRound r, Watched w, ApsMath.Result res)
			{
				Claim(r, Key);
				for (int index = res.FirstShot; index < res.FirstShot + res.ShotCount; index++)
				{
					var shot = w.T.Shots[index];
					int blockIndex = shot.Block;
					Fired++;
					var spec = S.Blocks[blockIndex];
					int laterOnes = 0;
					for (int later = index + 1; later < res.FirstShot + res.ShotCount; later++)
						if (w.T.Shots[later].Block == blockIndex)
							laterOnes++;
					int tube = Mathf.Clamp(St.Rounds[blockIndex] + laterOnes, 0, spec.Mouths.Length - 1);
					var mouth = Turret.TransformPoint(Mouths[blockIndex][tube]);
					w.Tubes[shot] = (tube + 1, Mouths[blockIndex][tube]);
					w.Launched = true;
					if (Caps != null && Caps[blockIndex][tube] != null) Caps[blockIndex][tube].enabled = false;
					Blast.Play(mouth, Blast.Size.ApsLaunch, false);
					try { GHPC.Audio.FmodGenericAudioManager.PlayOneShot(S.LaunchEvent, mouth); }
					catch (Exception exception)
					{
						DonorParts.WarnOnce("aps launch sound", $"{S.Tag}: launch sound '{S.LaunchEvent}' failed: {exception.Message}");
					}
					string fired = $"block {G.Blocks[blockIndex].Name} tube {tube + 1} fired";
					w.T.Why = index == res.FirstShot && res.FirstShot == 0 ? fired : $"{w.T.Why}; {fired}";
				}
			}

			void Burst(LiveRound r, Watched w, ApsMath.Result res, Vector3 vel)
			{
				var shotNow = res.Shot;
				var blk = G.Blocks[shotNow.Block];
				var burstPoint = shotNow.BurstPoint;
				int roundNumber = w.T.Shots.IndexOf(shotNow) + 1;
				string burstText = roundNumber > 1 ? $"; round {roundNumber}: burst" : ", burst";
				var burstWorld = Turret.TransformPoint(new Vector3(burstPoint.x, burstPoint.y, burstPoint.z));
				var hitWorld = Turret.TransformPoint(new Vector3(res.Hit.x, res.Hit.y, res.Hit.z));
				var axisWorld = Turret.TransformDirection(new Vector3(res.Axis.x, res.Axis.y, res.Axis.z));
				var from = r.transform.position;
				var seg = hitWorld - from;
				if (seg.sqrMagnitude > 1e-6f
					&& Physics.Raycast(from, seg.normalized, out var block, seg.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore)
					&& !OwnCollider(block.collider))
				{
					var owner = block.collider.GetComponentInParent<Unit>();
					w.T.Why = $"{w.T.Why}{(roundNumber > 1 ? $"; round {roundNumber}" : "")}; burst, but {block.collider.name}{(owner != null ? $" (of {owner.name})" : "")} at {Vehicle.transform.InverseTransformPoint(block.point):F1} (unit frame) between the threat and the burst point: no effect";
					Blast.Play(burstWorld, Blast.Size.ApsBurst);
					Fragments(burstWorld, axisWorld);
					r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w, shotNow)}: {block.collider.name} in the way, no effect");
					RecapLines(r, w, shotNow, burstWorld, axisWorld, null);
					ReportWhenDone(r, w, vel.magnitude, res.Hit);
					return;
				}
				Blast.Play(burstWorld, Blast.Size.ApsBurst);
				int frags = Fragments(burstWorld, axisWorld);
				if (!res.InField)
				{
					r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w, shotNow)}: missed by {res.Miss:0.0} m, outside the fragments, not destroyed");
					RecapLines(r, w, shotNow, burstWorld, axisWorld, null);
					w.T.Why = $"{w.T.Why}{burstText} {shotNow.BurstRange:0.0} m out, the threat {res.Miss:0.0} m from it, outside the fragment field: no effect; frags {frags}";
					ReportWhenDone(r, w, vel.magnitude, res.Hit);
					return;
				}
				var cls = ClassOf(r);
				float killChance = PKill(cls);
				float draw = ModRandom.Value;
				bool kill = ApsMath.Kills(killChance, draw);
				string fate;
				r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w, shotNow)}: fragments hit it {res.Miss:0.0} m from the burst");
				RecapLines(r, w, shotNow, burstWorld, axisWorld, hitWorld);
				if (kill)
				{
					Intercepts++;
					w.Killed = true;
					w.KillPoint = res.Hit;
					r.transform.position = hitWorld;
					float draw2 = ModRandom.Value;
					// _armed and _failedToDetonate are read by name. If either is gone the round still gets its Detonate.
					bool broken = ApsMath.BrokenUp(S.PBrokenUp, draw2) && FailedToDetonate != null;
					if (broken)
					{
						FailedToDetonate(r) = true;
						r.Story?.TryAddStoryEvent($"Broken up by {S.Name} ({blk.Name} block), warhead not set off");
						r.Detonate();
						fate = $"destroyed (broken up, warhead not set off; roll {draw2:0.00} < {S.PBrokenUp:0.00})";
					}
					else
					{
						if (Armed != null) Armed(r) = true;
						r.Story?.TryAddStoryEvent($"Intercepted by {S.Name} ({blk.Name} block)");
						var info = r.Info;
						if (Explode != null && info != null)
						{
							int orig = info.DetonateSpallCount;
							int fragmentCount = Mathf.Max(1, Mathf.RoundToInt((orig > 0 ? orig : 50) * S.WarheadFragShare));
							try
							{
								info.DetonateSpallCount = fragmentCount;
								Explode.Invoke(r, new object[] { false, 0f, Vector3.zero, 0.03f, fragmentCount });
							}
							catch (Exception exception) { PatchGuard.Failed($"{S.Tag} warhead fragments", exception); }
							finally { info.DetonateSpallCount = orig; }
						}
						r.Detonate();
						fate = "destroyed (warhead set off)";
					}
					InRecap(r, hitWorld);
				}
				else
				{
					r.Story?.TryAddStoryEvent($"Not destroyed by {S.Name}, flies on");
					fate = $"not destroyed (roll {draw:0.00} >= {killChance:0.00}), flies on";
				}
				var shooter = r.Shooter;
				if (shooter != null && shooter.Allegiance != Vehicle.Allegiance)
					try { Vehicle.NotifyStruck(shooter, r.Info, hitWorld); }
					catch (Exception exception) { PatchGuard.Failed($"{S.Tag} AI notice", exception); }
				w.T.Why = $"{w.T.Why}{burstText} {shotNow.BurstRange:0.0} m out, threat hit {res.Miss:0.0} m from it (put {res.Moved:0.00} m ahead) -> {fate} (P {killChance:0.00}); frags {frags} in +-{S.FragConeHalf:0} deg";
				ReportWhenDone(r, w, vel.magnitude, res.Hit);
				if (IsPlayer)
					MelonLogger.Msg($"{S.Name}: {(kill ? "intercepted" : "engaged")} {r.Info?.Name ?? "a round"} ({blk.Name} block){(kill ? "" : ", not destroyed")}; {St.SideRounds(G, blk.Side)} round(s) left on the {SideName(blk.Side)}.");
			}

			void ReportWhenDone(LiveRound r, Watched w, float speed, V3 at)
			{
				if (w.Killed || w.T.Done) Report(r, w.T, speed, r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f, at);
			}

			void InRecap(LiveRound r, Vector3 at)
			{
				try
				{
					var shotInfo = r.ShotInfo;
					if (shotInfo == null) return;
					shotInfo = shotInfo.ParentShot ?? shotInfo;
					shotInfo.IsHit = true;
					shotInfo.DetonationPosition = at;
					if (!shotInfo.HitUnits.Contains(Vehicle))
					{
						shotInfo.HitUnits.Add(Vehicle);
						if (IsPlayer)
						{
							shotInfo.HitPlayerUnit = true;
							if (shotInfo.HitUnits[0] == (IUnit)Vehicle) shotInfo.HitPlayerUnitFirst = true;
						}
					}
					if (shotInfo.Distance < 0f) shotInfo.Distance = Vector3.Distance(shotInfo.StartPosition, at);
					var own = r.ShotInfo;
					var dir = at - own.StartPosition;
					own.AllShotFrames.Add(new GHPC.Weapons.ShotInfo.ShotSubHitInfo
					{
						WorldPosition = at,
						LocalPosition = at,
						ParentTransform = null,
						InstantVector = dir.sqrMagnitude > 1e-6f ? dir.normalized : r.transform.forward,
					});
					if (r.Story != null)
					{
						r.Story.ImpactPosition = at;
						if (r.Story.ImpactTime <= 0f) r.Story.ImpactTime = SceneController.MissionTime;
					}
				}
				catch (Exception exception) { PatchGuard.Failed($"{S.Tag} recap entry", exception); }
			}

			const int RecapFan = 10;
			void RecapLines(LiveRound r, Watched w, ApsMath.Shot shot, Vector3 burst, Vector3 axis, Vector3? caught)
			{
				try
				{
					var aar = AarController.Instance;
					var shotInfo = r.ShotInfo;
					if (aar == null || shotInfo == null || !w.Launched) return;
					var parent = shotInfo.ParentShot ?? shotInfo;
					if (w.Tubes.TryGetValue(shot, out var tube))
						Line(aar, parent, Turret.TransformPoint(tube.mouth), burst, 20f);
					if (caught.HasValue) Line(aar, parent, burst, caught.Value, 12f);
					var right = Vector3.Cross(Vector3.up, axis);
					right = right.sqrMagnitude > 1e-6f ? right.normalized : Turret.right;
					var up = Vector3.Cross(axis, right);
					float tan = Mathf.Tan(S.FragConeHalf * Mathf.Deg2Rad);
					for (int i = 0; i < RecapFan; i++)
					{
						float spread = tan * Mathf.Sqrt((i + 0.5f) / RecapFan), phi = i * 2.39996323f;
						var direction = (axis + spread * (Mathf.Cos(phi) * right + Mathf.Sin(phi) * up)).normalized;
						Line(aar, parent, burst, burst + direction * S.ConeReach, 3f);
					}
				}
				catch (Exception exception) { PatchGuard.Failed($"{S.Tag} recap lines", exception); }
			}

			void Line(AarController aar, GHPC.Weapons.ShotInfo parent, Vector3 from, Vector3 to, float pen)
			{
				var dir = to - from;
				if (dir.sqrMagnitude < 1e-4f) return;
				if (_frag == null)
					_frag = FragmentCone.Type($"{S.Name} fragment", S.FragMass, S.FragArea, S.FragCoeff, 0f, S.FragPen, S.FragPenSpeed);
				var shot = new GHPC.Weapons.ShotInfo(from, dir.normalized, _frag, Vehicle, parent, pen);
				shot.IsHit = true;
				shot.Distance = dir.magnitude;
				shot.AllShotFrames.Add(new GHPC.Weapons.ShotInfo.ShotSubHitInfo
				{
					WorldPosition = to,
					LocalPosition = to,
					ParentTransform = null,
					InstantVector = dir.normalized
				});
				aar.AddShot(shot);
			}

			string Engaged(string block, Watched w, ApsMath.Shot shot)
				=> $"{S.Name} {block} block, tube {(w.Tubes.TryGetValue(shot, out var tube) ? tube.tube : 0)}: burst {shot.BurstRange:0.0} m out";

			bool OwnCollider(Collider c)
				=> c.GetComponentInParent<Unit>() == Vehicle || (c.TryGetComponent<IArmor>(out var armor) && armor.Unit == (IUnit)Vehicle);

			int Fragments(Vector3 at, Vector3 axis)
			{
				if (_frag == null)
					_frag = FragmentCone.Type($"{S.Name} fragment", S.FragMass, S.FragArea, S.FragCoeff, 0f, S.FragPen, S.FragPenSpeed);
				float pen = S.FragPen * (S.FragSpeed / S.FragPenSpeed) * (S.FragSpeed / S.FragPenSpeed);
				return FragmentCone.Spawn(_frag, at, axis, S.FragConeHalf, S.FragCount, S.FragSpeed, pen, Vehicle, !IsPlayer, null, $"{S.Name} fragment",
					() => ModRandom.Value, penAfterInit: true, fallbackRight: Turret.right
					);
			}

			AmmoType _frag;

			ApsMath.Threat ClassOf(LiveRound r) => r.Info == null ? ApsMath.Threat.Other
				: ApsMath.ClassOf(
					r.IsSpall,
					r.Info.Guidance != AmmoType.GuidanceType.Unguided,
					(int)r.Info.Category,
					r.Info.Name,
					S.GrenadePrefixes);

			float PKill(ApsMath.Threat t)
				=> t == ApsMath.Threat.Atgm
					? S.PAtgm
					: t == ApsMath.Threat.Grenade ? S.PGrenade : t == ApsMath.Threat.Heat ? S.PHeat : t == ApsMath.Threat.He ? S.PHe : 0f;

			void Report(LiveRound r, ApsMath.Track t, float speed, float diameter, V3 p)
			{
			}

			internal string HudLine()
			{
				string why = NotReady();
				string state = why == null ? "ready"
					: Reloading ? (Stopped
						? $"reloading, {ReloadLeft:0} s ({Empty} to load)"
						: $"reloading: stop the tank ({Empty} to load)")
					: why;
				string line = $"{S.Name}: {SideSummary()}, {state}";
				if (S.HudPerBlock && G.Sensors.Length > 0) line += $"\n{S.Name} radars: {ApsMath.RadarSummary(G, St)}";
				return line;
			}

			string SideLabel(int side) => side < S.SideLabels.Length ? S.SideLabels[side] : $"S{side + 1}";

			string SideName(int side) => side < S.SideNames.Length ? S.SideNames[side] : $"side {side + 1}";

			string SideSummary()
				=> S.HudPerBlock
					? ApsMath.BlockSummary(G, St)
					: string.Join(" ", Enumerable.Range(0, St.SideFired.Length).Select(s => $"{SideLabel(s)} {St.SideRounds(G, s)}"));
		}

		static readonly AccessTools.FieldRef<LiveRound, bool> Armed = Reflect.FieldRefAccess<LiveRound, bool>("_armed");
		static readonly AccessTools.FieldRef<LiveRound, bool> FailedToDetonate =
			Reflect.FieldRefAccess<LiveRound, bool>("_failedToDetonate");

		internal static readonly List<Station> Stations = new List<Station>();

		internal static Station Of(Unit u)
		{
			if (u == null) return null;
			for (int i = 0; i < Stations.Count; i++)
			{
				var station = Stations[i];
				if (station != null && station.Vehicle == u) return station;
			}
			return null;
		}

		internal static string Apply(Vehicle v, Settings s)
		{
			var station = v.gameObject.GetComponent<Station>() ?? v.gameObject.AddComponent<Station>();
			var line = station.Setup(v, s);
			station.ShowCaps();
			return line;
		}

		internal static string HudLine(Unit u)
		{
			var station = Of(u);
			return station != null && station.G != null ? station.HudLine() : null;
		}

		internal static string HudName(Unit u) => Of(u)?.S.Name;

		// Lives in AppDomain data for every mod with an APS. mscorlib types only, since no mod's own type matches another's,
		// and a new layout means a new key.
		static Dictionary<long, int> _claims;
		static Dictionary<long, int> ClaimsTable
		{
			get
			{
				if (_claims != null) return _claims;
				// GHPCMods, not this mod's namespace: every tank's copy has to find the one table.
				const string key = "GHPCMods.Aps.Claims.v1";
				var had = AppDomain.CurrentDomain.GetData(key);
				if (had is Dictionary<long, int> table) _claims = table;
				else
				{
					if (had != null)
						MelonLogger.Warning("APS: the claim table of another port has another layout; this port keeps its own.");
					_claims = new Dictionary<long, int>();
					if (had == null) AppDomain.CurrentDomain.SetData(key, _claims);
				}
				return _claims;
			}
		}

		static long RoundKey(LiveRound r) => ((long)r.GetInstanceID() << 32) | (uint)r.ID;

		static bool Claimed(LiveRound r, int by) { lock (ClaimsTable) return ClaimsTable.TryGetValue(RoundKey(r), out int k) && k != by; }

		static void Claim(LiveRound r, int by)
		{
			lock (ClaimsTable)
			{
				if (ClaimsTable.Count > 1024) ClaimsTable.Clear();
				ClaimsTable[RoundKey(r)] = by;
			}
		}

		static bool _patched;
		static readonly AccessTools.FieldRef<LiveRoundBatchHandler, List<LiveRound>> LiveRounds =
			Reflect.FieldRefAccess<LiveRoundBatchHandler, List<LiveRound>>("_liveRounds");

		// Not [HarmonyPatch]: every port compiles this file, but only a port with an APS patches anything.
		static void EnsurePatched()
		{
			if (_patched) return;
			_patched = true;
			WeaponHudLines.Any = () => Stations.Count > 0;
			WeaponHudLines.For = u => (HudName(u), HudLine(u));
			var harmony = new HarmonyLib.Harmony(typeof(Aps).Assembly.GetName().Name + ".aps");
			var update = Reflect.Method(typeof(LiveRoundBatchHandler), "Update");
			if (update != null) harmony.Patch(update, postfix: new HarmonyMethod(typeof(Aps), nameof(WatchPostfix)));
			var menu = Reflect.Method(typeof(PlayerInput), "AddSmokeButtons");
			if (menu != null) harmony.Patch(menu, postfix: new HarmonyMethod(typeof(Aps), nameof(MenuPostfix)));
			if (LiveRounds == null) MelonLogger.Warning("APS: GHPC's live-round list not found (game update?); the APS sees nothing.");
		}

		static void WatchPostfix(LiveRoundBatchHandler __instance)
		{
			if (Stations.Count == 0 || Time.timeScale == 0f || LiveRounds == null) return;
			try { Scan(__instance); }
			catch (Exception exception) { PatchGuard.Failed("APS watch", exception); }
		}

		static void Scan(LiveRoundBatchHandler h)
		{
			if (AarController.InAar) return;
			var list = LiveRounds(h);
			if (list == null) return;
			float now = Time.time, deltaTime = Time.deltaTime;
			for (int stationIndex = 0; stationIndex < Stations.Count; stationIndex++) Stations[stationIndex]?.BeginFrame();
			for (int i = 0; i < list.Count; i++)
			{
				var round = list[i];
				if (round == null || round.IsSpall || round.IsDestroyed || round.Info == null) continue;
				var pos = round.transform.position;
				for (int stationIndex = 0; stationIndex < Stations.Count; stationIndex++)
				{
					var station = Stations[stationIndex];
					if (station == null || station.Vehicle == null || station.G == null) continue;
					station.Consider(round, pos, now, deltaTime);
					if (round.IsDestroyed) break;
				}
			}
		}

		static readonly MethodInfo Explode = Reflect.Method(
			typeof(LiveRound),
			"createExplosion",
			new[] { typeof(bool), typeof(float), typeof(Vector3), typeof(float), typeof(int) });
		static readonly MethodInfo AddButton = Reflect.Method(
			typeof(PlayerInput),
			"AddMenuButton",
			new[] { typeof(string), typeof(UnityAction) });

		static void MenuPostfix(PlayerInput __instance)
		{
			try
			{
				var station = Of(__instance.CurrentPlayerUnit);
				if (station == null || AddButton == null || station.G == null) return;
				string state = !station.On
					? "OFF"
					: Time.time < station.ReadyAt ? $"ON, ready in {Mathf.CeilToInt(station.ReadyAt - Time.time)} s" : "ON";
				AddButton.Invoke(
					__instance,
					new object[] { $"{station.S.Name}/Toggle APS ({state})", (UnityAction)(() => station.Set(!station.On, "player")) });
				if (station.Reloading)
					AddButton.Invoke(
						__instance,
						new object[] { $"{station.S.Name}/Cancel Reload", (UnityAction)(() => station.StopReload("cancelled")) });
				else if (station.Empty > 0)
					AddButton.Invoke(
						__instance,
						new object[]
						{
							$"{station.S.Name}/Reload ({station.Empty} empty)",
							(UnityAction)(() => station.StartReload("player"))
						});
			}
			catch (Exception exception) { PatchGuard.Failed("APS vehicle menu", exception); }
		}
	}
}
