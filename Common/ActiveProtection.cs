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
	internal static class Aps
	{
		internal sealed class BlockSpec
		{
			public string Name;
			public Vector3[] Mouths;
			public float AxisAz, AxisEl;
			public float AzMin, AzMax;
			public int Side;
			public string Module;
		}

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
			public bool PickTube;
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

		internal sealed class Watched
		{
			public LiveRound Round;
			public int Id;
			public ApsMath.Track T = new ApsMath.Track();
			public Vector3 Prev;
			public int PrevFrame = -10;
			public V3 Vel;
			public bool HasVel;
			public Vector3 Mean;
			public bool HasMean;
			public bool WindowLogged;
			public Vector3 Mouth;
			public bool Launched;
			public int Tube;
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
			internal Bounds Box;
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
				Vector3 Local(Vector3 prefab) => Turret.InverseTransformPoint(root.TransformPoint(prefab));
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
					Blocks = s.Blocks.Select(block =>
					{
						var center = Vector3.zero;
						foreach (var mouth in block.Mouths) center += Local(mouth);
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
				St = new ApsMath.State(G);
				BlockDc = new DC[G.Blocks.Length];
				SensorDc = new DC[2];
				Box = OwnBox(v, s);
				Caps = BindCaps(s, out var capNote);
				if (s.OnAtSpawn)
				{
					On = true;
					ReadyAt = 0f;
				}
				Stations.Add(this);
				EnsurePatched();
				return $"{G.Blocks.Length} blocks ({string.Join("; ", G.Blocks.Select((b, i) => $"{b.Name} {b.AzMin:+0;-0}..{b.AzMax:+0;-0} deg, axis {b.AxisAz:+0;-0}, mouth {b.Mouth} turret frame, burst {b.Burst(G.InterceptRange)}, {St.Rounds[i]} rounds"))}), "
					+ $"radar origin {G.Origin}, protected box {Box.center:F2} size {Box.size:F2} (vehicle frame; {_boxParts}), {(On ? "on at spawn" : "off at spawn")}, {capNote}";
			}

			static Bounds OwnBox(Vehicle v, Settings s)
			{
				var root = v.transform;
				var pts = new List<(Vector3 p, string n)>();
				void Corners(Bounds localBounds, Transform part, string partName)
				{
					for (int i = 0; i < 8; i++)
						pts.Add((root.InverseTransformPoint(part.TransformPoint(localBounds.center
							+ Vector3.Scale(
								localBounds.extents,
								new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))), partName));
				}
				string source = "armour colliders";
				foreach (var collider in v.GetComponentsInChildren<Collider>(true))
				{
					if (collider == null
						|| collider.isTrigger
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
				var root = Vehicle.transform;
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
						var mouthPoint = root.TransformPoint(block.Mouths[k]);
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
					for (int k = 0; k < S.SensorModules.Length && k < 2; k++)
						if (S.SensorModules[k] != null && module.Id == S.SensorModules[k])
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
				if (!_tracks.TryGetValue(r, out var watched) || watched.Id != r.ID)
				{
					watched = new Watched { Round = r, Id = r.ID };
					_tracks[r] = watched;
				}
				int frame = Time.frameCount;
				var forwardVelocity = r.transform.forward * (r.CurrentSpeed > 1f
					? r.CurrentSpeed
					: (r.Info != null ? r.Info.MuzzleVelocity : 0f));
				var movement = pos - watched.Prev;
				var frameVelocity = ApsMath.FrameVelocity(
					watched.PrevFrame == frame - 1,
					new V3(movement.x, movement.y, movement.z),
					dt,
					watched.HasVel,
					watched.Vel,
					new V3(forwardVelocity.x, forwardVelocity.y, forwardVelocity.z));
				Vector3 vel = new Vector3(frameVelocity.x, frameVelocity.y, frameVelocity.z);
				watched.Vel = frameVelocity;
				watched.HasVel = true;
				watched.Prev = pos;
				watched.PrevFrame = frame;
				watched.LastFrame = frame;
				if (watched.T.Done) return;
				var turretPoint = Turret.InverseTransformPoint(pos);
				var turretVelocity = Turret.InverseTransformDirection(vel);
				var localPoint = new V3(turretPoint.x, turretPoint.y, turretPoint.z);
				var localVelocity = new V3(turretVelocity.x, turretVelocity.y, turretVelocity.z);
				var root = Vehicle.transform;
				if (!watched.HasMean)
				{
					watched.Mean = vel;
					watched.HasMean = true;
				}
				else watched.Mean += (vel - watched.Mean) * (1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, S.HeadingSmoothing)));
				var hullPoint = root.InverseTransformPoint(pos);
				var hullDirection = root.InverseTransformDirection(watched.Mean.normalized);
				bool atBox = ApsMath.RayHitsBox(
					new V3(hullPoint.x, hullPoint.y, hullPoint.z),
					new V3(hullDirection.x, hullDirection.y, hullDirection.z),
					new V3(Box.center.x, Box.center.y, Box.center.z),
					new V3(Box.extents.x, Box.extents.y, Box.extents.z));
				float diameter = r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f;
				string window = ApsMath.Window(G, vel.magnitude, diameter, r.IsSpall);
				if (window != null)
				{
					if (!watched.WindowLogged && atBox && (localPoint - G.Origin).Length <= G.TrackRange)
					{
						watched.WindowLogged = true;
						watched.T.Done = true;
						watched.T.Why = $"ignored ({window})";
						Report(r, watched, vel.magnitude, diameter, localPoint);
					}
					return;
				}
				if (!watched.T.Committed && Claimed(r, Key))
				{
					watched.T.Done = true;
					watched.T.Why = "left to another station that engages it";
					Report(r, watched, vel.magnitude, diameter, localPoint);
					return;
				}
				string ready = NotReady();
				if (ready == null && G.Blocks.Length > 0)
				{
					float azimuth = (localPoint - G.Origin).Az;
					int side = azimuth < 0f ? 0 : 1;
					if (SensorDc[side] != null && SensorDc[side].IsDestroyed)
						ready = $"the {(side == 0 ? "left" : "right")} radar head lost";
				}
				var result = ApsMath.Step(G, St, watched.T, localPoint, localVelocity, atBox, now, dt, ready);
				if (result.Fired) Launch(r, watched);
				if (result.Act == ApsMath.Act.Intercept) Burst(r, watched, result, vel);
				else if (result.Act == ApsMath.Act.Drop) Report(r, watched, vel.magnitude, diameter, localPoint);
			}

			void Launch(LiveRound r, Watched w)
			{
				int blockIndex = w.T.Block;
				Claim(r, Key);
				Fired++;
				var spec = S.Blocks[blockIndex];
				int tube = Mathf.Clamp(St.Rounds[blockIndex], 0, spec.Mouths.Length - 1);
				var mouth = Vehicle.transform.TransformPoint(spec.Mouths[tube]);
				w.Mouth = spec.Mouths[tube];
				w.Launched = true;
				w.Tube = tube + 1;
				if (Caps != null && Caps[blockIndex][tube] != null) Caps[blockIndex][tube].enabled = false;
				Blast.Play(mouth, Blast.Size.ApsLaunch, false);
				try { GHPC.Audio.FmodGenericAudioManager.PlayOneShot(S.LaunchEvent, mouth); }
				catch (Exception exception)
				{
					DonorParts.WarnOnce("aps launch sound", $"{S.Tag}: launch sound '{S.LaunchEvent}' failed: {exception.Message}");
				}
				w.T.Why = $"block {G.Blocks[blockIndex].Name} tube {tube + 1} fired";
			}

			void Burst(LiveRound r, Watched w, ApsMath.Result res, Vector3 vel)
			{
				int blockIndex = w.T.Block;
				var blk = G.Blocks[blockIndex];
				var burstPoint = w.T.BurstPoint;
				var burstWorld = Turret.TransformPoint(new Vector3(burstPoint.x, burstPoint.y, burstPoint.z));
				var hitWorld = Turret.TransformPoint(new Vector3(res.Hit.x, res.Hit.y, res.Hit.z));
				var axisWorld = Turret.TransformDirection(new Vector3(blk.Axis.x, blk.Axis.y, blk.Axis.z));
				var from = r.transform.position;
				var seg = hitWorld - from;
				if (seg.sqrMagnitude > 1e-6f
					&& Physics.Raycast(from, seg.normalized, out var block, seg.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore)
					&& !OwnCollider(block.collider))
				{
					var owner = block.collider.GetComponentInParent<Unit>();
					w.T.Why = $"{w.T.Why}; burst, but {block.collider.name}{(owner != null ? $" (of {owner.name})" : "")} at {Vehicle.transform.InverseTransformPoint(block.point):F1} (unit frame) between the threat and the burst point: no effect";
					Blast.Play(burstWorld, Blast.Size.ApsBurst);
					Fragments(burstWorld, axisWorld);
					r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w)}: {block.collider.name} in the way, no effect");
					RecapLines(r, w, burstWorld, axisWorld, null);
					Report(r, w, vel.magnitude, r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f, res.Hit);
					return;
				}
				Blast.Play(burstWorld, Blast.Size.ApsBurst);
				int frags = Fragments(burstWorld, axisWorld);
				if (!res.InField)
				{
					r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w)}: missed by {res.Miss:0.0} m, outside the fragments, not destroyed");
					RecapLines(r, w, burstWorld, axisWorld, null);
					w.T.Why = $"{w.T.Why}, burst {w.T.BurstRange:0.0} m out, the threat {res.Miss:0.0} m from it, outside the fragment field: no effect; frags {frags}";
					Report(r, w, vel.magnitude, r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f, res.Hit);
					return;
				}
				var cls = ClassOf(r);
				float killChance = PKill(cls);
				float draw = ModRandom.Value;
				bool kill = ApsMath.Kills(killChance, draw);
				string fate;
				r.Story?.TryAddStoryEvent($"{Engaged(blk.Name, w)}: fragments hit it {res.Miss:0.0} m from the burst");
				RecapLines(r, w, burstWorld, axisWorld, hitWorld);
				if (kill)
				{
					Intercepts++;
					r.transform.position = hitWorld;
					float draw2 = ModRandom.Value;
					bool broken = ApsMath.BrokenUp(S.PBrokenUp, draw2);
					if (broken)
					{
						FailedToDetonate(r) = true;
						r.Story?.TryAddStoryEvent($"Broken up by {S.Name} ({blk.Name} block), warhead not set off");
						r.Detonate();
						fate = $"destroyed (broken up, warhead not set off; roll {draw2:0.00} < {S.PBrokenUp:0.00})";
					}
					else
					{
						Armed(r) = true;
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
				w.T.Why = $"{w.T.Why}, burst {w.T.BurstRange:0.0} m out, threat hit {res.Miss:0.0} m from it (put {res.Moved:0.00} m ahead) -> {fate} (P {killChance:0.00}); frags {frags} in +-{S.FragConeHalf:0} deg";
				Report(r, w, vel.magnitude, r.Info != null ? ApsMath.Diameter(r.Info.SectionalArea) : 0f, res.Hit);
				if (IsPlayer)
					MelonLogger.Msg($"{S.Name}: {(kill ? "intercepted" : "engaged")} {r.Info?.Name ?? "a round"} ({blk.Name} block){(kill ? "" : ", not destroyed")}; {St.SideRounds(G, blk.Side)} round(s) left on the {(blk.Side == 0 ? "left" : "right")} side.");
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
			void RecapLines(LiveRound r, Watched w, Vector3 burst, Vector3 axis, Vector3? caught)
			{
				try
				{
					var aar = AarController.Instance;
					var shotInfo = r.ShotInfo;
					if (aar == null || shotInfo == null || !w.Launched) return;
					var parent = shotInfo.ParentShot ?? shotInfo;
					Line(aar, parent, Vehicle.transform.TransformPoint(w.Mouth), burst, 20f);
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

			string Engaged(string block, Watched w) => $"{S.Name} {block} block, tube {w.Tube}: burst {w.T.BurstRange:0.0} m out";

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

			void Report(LiveRound r, Watched w, float speed, float diameter, V3 p)
			{
				var rel = p - G.Origin;
				var who = r.Shooter != null
					? $"{r.Shooter.name}{(r.Shooter.Allegiance == Vehicle.Allegiance ? " (friendly)" : "")}"
					: "no shooter";
				string line = $"{S.Tag} round {r.ID} '{r.Info?.Name}' {ApsMath.Name(ClassOf(r))} {speed:0} m/s d={diameter:0.000} m bearing {rel.Az:+0.0;-0.0} el {rel.El:+0.0;-0.0} (turret) at {rel.Length:0.0} m"
					+ (w.T.Seen
						? $": tracked at {w.T.SeenRange:0.0} m, track {Time.time - w.T.SeenAt:0.000} s"
						: "") + $": {w.T.Why}; from {who}; rounds left L {St.SideRounds(G, 0)} R {St.SideRounds(G, 1)}.";
			}

			internal string HudLine()
			{
				string why = NotReady();
				string state = why == null ? "ready"
					: Reloading ? (Stopped
						? $"reloading, {ReloadLeft:0} s ({Empty} to load)"
						: $"reloading: stop the tank ({Empty} to load)")
					: why;
				return $"{S.Name}: L {St.SideRounds(G, 0)} R {St.SideRounds(G, 1)}, {state}";
			}
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

		static Dictionary<long, int> _claims;
		static Dictionary<long, int> ClaimsTable
		{
			get
			{
				if (_claims != null) return _claims;
				const string key = "T80UM2.Common.Aps.Claims.v1";
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

		// Not [HarmonyPatch]: every port compiles this file; only a port with an APS patches anything.
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
