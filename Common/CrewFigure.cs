using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC.Crew;
using UnityEngine;
using Object = UnityEngine.Object;
using RCG = GHPC.PhysicsHelpers.RaycastColliderGroup;

namespace T80UM2.Common
{
	internal static class CrewFigure
	{
		internal sealed class Drawn
		{
			internal GameObject Root;
			internal readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
			internal Transform B(string n) => Bones.TryGetValue(n, out var t) ? t : null;
		}

		internal static void SetDir(Transform bone, Transform child, Vector3 want)
		{
			if (bone == null || child == null || want.sqrMagnitude < 1e-10f) return;
			var now = child.position - bone.position;
			if (now.sqrMagnitude < 1e-10f) return;
			bone.rotation = Quaternion.FromToRotation(now, want) * bone.rotation;
		}

		internal static void Reach(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
		{
			if (upper == null || lower == null || end == null) return;
			float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, end.position);
			var s = upper.position;
			var to = target - s;
			float dist = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
			var u = to.normalized;
			var v = Vector3.ProjectOnPlane(pole, u);
			if (v.sqrMagnitude < 1e-8f) v = Vector3.ProjectOnPlane(Vector3.down, u);
			v.Normalize();
			float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f);
			var elbow = s + a * (cosA * u + Mathf.Sqrt(1f - cosA * cosA) * v);
			SetDir(upper, lower, elbow - s);
			SetDir(lower, end, s + u * dist - lower.position);
		}

		static readonly (string swat, string swatTo, string drawn, string drawnTo)[] Chains =
		{
			("swat:Spine", "swat:Neck", "soldierSpine1", "soldierNeck1"),
			("swat:Neck", "swat:Head", "soldierNeck1", "soldierHead"),
			("swat:LeftUpLeg", "swat:LeftLeg", "soldierLLeg1", "soldierLLeg2"),
			("swat:LeftLeg", "swat:LeftFoot", "soldierLLeg2", "soldierLLegAnkle"),
			("swat:RightUpLeg", "swat:RightLeg", "soldierRLeg1", "soldierRLeg2"),
			("swat:RightLeg", "swat:RightFoot", "soldierRLeg2", "soldierRLegAnkle"),
		};
		static readonly (string swat, string swatTo, string drawn, string drawnTo)[] ArmChains =
		{
			("swat:LeftArm", "swat:LeftForeArm", "soldierLArm1", "soldierLArm2"),
			("swat:LeftForeArm", "swat:LeftHand", "soldierLArm2", "soldierLArmPalm"),
			("swat:RightArm", "swat:RightForeArm", "soldierRArm1", "soldierRArm2"),
			("swat:RightForeArm", "swat:RightHand", "soldierRArm2", "soldierRArmPalm"),
		};

		internal sealed class Pose
		{
			internal Vector3 Spine = new Vector3(0f, 0.94f, 0.34f);
			internal Vector3 Neck = new Vector3(0f, 0.97f, 0.25f);
			internal Vector3 LeftThigh = new Vector3(-0.35f, 0.45f, 0.82f);
			internal Vector3 RightThigh = new Vector3(0.35f, 0.45f, 0.82f);
			internal Vector3 LeftShin = new Vector3(-0.1f, -0.85f, 0.5f);
			internal Vector3 RightShin = new Vector3(0.1f, -0.85f, 0.5f);
			internal Vector3 ElbowPole = new Vector3(0f, -1f, -0.3f);
			internal float HandReach;
			internal Transform LeftGrip, RightGrip;
		}

		[DefaultExecutionOrder(990)]
		internal sealed class SeatFacing : MonoBehaviour
		{
			internal Transform Up, A, B;
			void LateUpdate()
			{
				if (Up == null || A == null || B == null) return;
				var dir = Vector3.ProjectOnPlane((A.position + B.position) * 0.5f - transform.position, Up.up);
				if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir, Up.up);
			}
		}

		[DefaultExecutionOrder(1000)]
		internal sealed class Exposure : MonoBehaviour
		{
			internal CrewClimb Climb = new CrewClimb();
			internal CrewManager.CrewMember Member;
			internal Transform Turret;
			internal Transform Seat;
			internal Quaternion SeatRest = Quaternion.identity;
			internal Pose P = new Pose();
			internal Drawn Figure;
			internal Func<bool> WantOut = () => false, Busy = () => false, HideDrawn = () => false;
			internal string Tag = "crew";
			internal Action<string> LogLine = s => { };

			static readonly FieldInfo ChildGroups = Reflect.Field(typeof(RCG), "childGroups");
			static readonly Type StateT = typeof(RCG).GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
			static readonly MethodInfo SetStateM = StateT == null ? null : typeof(RCG).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { StateT }, null);

			Transform _fig, _swatRoot;
			Vector3 _figPos0; Quaternion _figRot0;
			readonly Dictionary<Transform, Quaternion> _bones0 = new Dictionary<Transform, Quaternion>();
			Matrix4x4 _figRelHips;
			bool _hatchOpened, _bodyOut;
			RCG _own, _parentGroup; bool _detached; int _listed;
			internal string LastReport = "";

			internal bool Able => Member != null && Member.Human != null && !Member.Human.IsDead && !Member.Evacuated;

			void LateUpdate()
			{
				if (Member == null || Turret == null || Seat == null) return;
				if (_fig == null && Member.BodyPartRegistry != null) _fig = Member.BodyPartRegistry.transform;
				bool changed = Climb.Step(Time.deltaTime, WantOut(), Busy(), Able);
				if (changed) LogLine($"{Tag}: {Climb.State} (hatch {(Climb.HatchOpen ? "open" : "shut")}, body {(Climb.BodyOut ? "out" : "in")}, fire {(Climb.MayFire ? "allowed" : "held")}).");
				var hatch = Member.ExitHatch;
				if (Climb.HatchOpen && !_hatchOpened) { _hatchOpened = true; if (hatch != null) hatch.Play(); }
				else if (!Climb.HatchOpen && _hatchOpened && !Member.Evacuated) { _hatchOpened = false; if (hatch != null) hatch.PlayReversed(); }
				if (Climb.BodyOut && !_bodyOut) Out();
				else if (!Climb.BodyOut && _bodyOut) In();
				if (_bodyOut) Follow();
			}

			Matrix4x4 ParentInBone()
			{
				var parent = _fig.parent;
				var follow = parent.GetComponentsInParent<GHPC.Utility.LateFollow>(true).FirstOrDefault(f => f.FollowTarget == Turret);
				if (follow == null) return Turret.worldToLocalMatrix * parent.localToWorldMatrix;
				var boneToFollower = Matrix4x4.TRS(Followers.PosShift(follow), Followers.RotShift(follow), Vector3.one);
				return boneToFollower * (follow.transform.worldToLocalMatrix * parent.localToWorldMatrix);
			}

			Matrix4x4 SeatInBone() => Turret.worldToLocalMatrix * Matrix4x4.TRS(Seat.position, Seat.rotation, Vector3.one);
			readonly Dictionary<string, Transform> _swat = new Dictionary<string, Transform>();
			Transform Swat(string n)
			{
				if (_swatRoot == null) return null;
				if (_swat.Count == 0) foreach (var t in _swatRoot.GetComponentsInChildren<Transform>(true)) if (!_swat.ContainsKey(t.name)) _swat[t.name] = t;
				return _swat.TryGetValue(n, out var x) ? x : null;
			}

			void Out()
			{
				_bodyOut = true;
				if (_fig == null) return;
				_figPos0 = _fig.localPosition; _figRot0 = _fig.localRotation;
				_swatRoot = _fig;
				_bones0.Clear();
				foreach (var t in _fig.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("swat:")) _bones0[t] = t.localRotation;
				var pib = ParentInBone();
				var figInBone = pib * Matrix4x4.TRS(_figPos0, _figRot0, Vector3.one);
				var hips = Swat("swat:Hips");
				var hipsInBone = hips != null ? pib.MultiplyPoint3x4(_fig.parent.InverseTransformPoint(hips.position)) : figInBone.MultiplyPoint3x4(Vector3.zero);
				_figRelHips = Matrix4x4.Translate(-hipsInBone) * figInBone;
				Place();
				PoseBody();
				Detach(true);
				if (Figure != null) { LayDrawn(); Figure.Root.SetActive(!HideDrawn()); }
				LogLine($"{Tag}: out at the seat {Report()}.");
			}

			void In()
			{
				_bodyOut = false;
				if (_fig != null)
				{
					_fig.localPosition = _figPos0; _fig.localRotation = _figRot0;
					foreach (var kv in _bones0) if (kv.Key != null) kv.Key.localRotation = kv.Value;
				}
				Detach(false);
				if (Figure != null) Figure.Root.SetActive(false);
				LogLine($"{Tag}: back in his seat.");
			}

			void Place()
			{
				var seat = SeatInBone();
				var turn = seat.rotation * Quaternion.Inverse(SeatRest);
				var want = Matrix4x4.Translate(seat.GetColumn(3)) * Matrix4x4.Rotate(turn) * _figRelHips;
				var local = ParentInBone().inverse * want;
				_fig.localPosition = local.GetColumn(3);
				_fig.localRotation = local.rotation;
			}

			Vector3 SwatDir(Vector3 seatDir)
			{
				var inBone = SeatInBone().MultiplyVector(seatDir);
				return _fig.parent.TransformDirection(ParentInBone().inverse.MultiplyVector(inBone));
			}

			Vector3 SwatPoint(Vector3 world) => _fig.parent.TransformPoint(ParentInBone().inverse.MultiplyPoint3x4(Turret.InverseTransformPoint(world)));
			Vector3 TrueWorld(Vector3 swatWorld) => Turret.TransformPoint(ParentInBone().MultiplyPoint3x4(_fig.parent.InverseTransformPoint(swatWorld)));

			void PoseBody()
			{
				SetDir(Swat("swat:Spine"), Swat("swat:Neck"), SwatDir(P.Spine));
				SetDir(Swat("swat:Neck"), Swat("swat:Head"), SwatDir(P.Neck));
				SetDir(Swat("swat:LeftUpLeg"), Swat("swat:LeftLeg"), SwatDir(P.LeftThigh));
				SetDir(Swat("swat:LeftLeg"), Swat("swat:LeftFoot"), SwatDir(P.LeftShin));
				SetDir(Swat("swat:RightUpLeg"), Swat("swat:RightLeg"), SwatDir(P.RightThigh));
				SetDir(Swat("swat:RightLeg"), Swat("swat:RightFoot"), SwatDir(P.RightShin));
				PoseArms();
			}

			void PoseArms()
			{
				var pole = SwatDir(P.ElbowPole);
				if (P.LeftGrip != null) Reach(Swat("swat:LeftArm"), Swat("swat:LeftForeArm"), Swat("swat:LeftHand"), WristFor(Swat("swat:LeftArm"), P.LeftGrip), pole + SwatDir(Vector3.left) * 0.5f);
				if (P.RightGrip != null) Reach(Swat("swat:RightArm"), Swat("swat:RightForeArm"), Swat("swat:RightHand"), WristFor(Swat("swat:RightArm"), P.RightGrip), pole + SwatDir(Vector3.right) * 0.5f);
			}

			Vector3 WristFor(Transform shoulder, Transform grip)
			{
				var g = SwatPoint(grip.position);
				if (shoulder == null || P.HandReach <= 0f) return g;
				var d = g - shoulder.position;
				return d.sqrMagnitude < 1e-6f ? g : g - d.normalized * P.HandReach;
			}

			void LayDrawn(bool armsOnly = false)
			{
				var d = Figure;
				if (d == null || d.Root == null) return;
				if (!armsOnly)
				{
					d.Root.transform.SetPositionAndRotation(Seat.position, Seat.rotation);
					var hip = d.B("soldierHip"); var sHip = Swat("swat:Hips");
					if (hip != null && sHip != null) d.Root.transform.position += TrueWorld(sHip.position) - hip.position;
					foreach (var c in Chains) Lay(c);
				}
				foreach (var c in ArmChains) Lay(c);
			}

			void Lay((string swat, string swatTo, string drawn, string drawnTo) c)
			{
				var a = Swat(c.swat); var b = Swat(c.swatTo);
				if (a == null || b == null) return;
				SetDir(Figure.B(c.drawn), Figure.B(c.drawnTo), TrueWorld(b.position) - TrueWorld(a.position));
			}

			void Follow()
			{
				if (_fig == null) return;
				if (_detached && _own != null && _parentGroup != null && ((ChildGroups.GetValue(_parentGroup) as List<RCG>)?.Contains(_own) ?? false)) Detach(true);
				Place();
				if (Able) PoseArms();
				if (Figure != null && Figure.Root != null)
				{
					if (Able) LayDrawn(true);
					bool show = !HideDrawn();
					if (Figure.Root.activeSelf != show) Figure.Root.SetActive(show);
					if (show) LayDrawn(false);
				}
			}

			// A nested group is only tested inside its parent's bounds; out on the roof he'd never be hit.
			void Detach(bool detach)
			{
				if (_fig == null) return;
				if (_own == null)
				{
					_own = _fig.GetComponent<RCG>() ?? _fig.GetComponentInChildren<RCG>(true);
					// The turret's collider group is a follower at scene root: its owner isn't among its parents.
					_parentGroup = _own != null && _own.transform.parent != null ? _own.transform.parent.GetComponentsInParent<RCG>(true).FirstOrDefault(g => g != _own) : null;
					if (_own != null && ChildGroups != null && (_parentGroup == null || !((ChildGroups.GetValue(_parentGroup) as List<RCG>)?.Contains(_own) ?? false)))
						_parentGroup = Object.FindObjectsByType<RCG>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(g => g != _own && ((ChildGroups.GetValue(g) as List<RCG>)?.Contains(_own) ?? false)) ?? _parentGroup;
					if (_own == null || _parentGroup == null || ChildGroups == null || SetStateM == null || StateT == null)
					{ LogLine($"{Tag}: collider groups not found (own {(_own != null ? _own.name : "none")}, parent {(_parentGroup != null ? _parentGroup.name : "none")}); his hit zones stay nested in the turret's."); _own = null; return; }
				}
				var list = ChildGroups.GetValue(_parentGroup) as List<RCG>;
				if (list == null) return;
				// Listed twice: Remove takes one, the other keeps him hidden.
				if (detach) { _listed = list.RemoveAll(g => g == _own); SetStateM.Invoke(_own, new[] { Enum.Parse(StateT, "Waiting") }); }
				else { if (!list.Contains(_own)) list.Add(_own); SetStateM.Invoke(_own, new[] { Enum.Parse(StateT, "Hidden") }); }
				_detached = detach;
			}

			internal string Report()
			{
				if (_fig == null) return "(no crew figure)";
				string P3(Vector3 w) { var p = Turret.InverseTransformPoint(w); return $"({p.x:0.00}, {p.y:0.00}, {p.z:0.00})"; }
				var parts = new List<string>();
				foreach (var n in new[] { "swat:Hips", "swat:Head", "swat:LeftHand", "swat:RightHand" })
				{
					var t = Swat(n);
					if (t != null) parts.Add($"{n.Substring(5)} {P3(TrueWorld(t.position))}");
				}
				if (P.LeftGrip != null && Swat("swat:LeftHand") != null) parts.Add($"left hand {Mathf.Abs(Vector3.Distance(TrueWorld(Swat("swat:LeftHand").position), P.LeftGrip.position) - P.HandReach) * 100f:0} cm off its grip{(P.HandReach > 0f ? " (palm)" : "")}");
				if (P.RightGrip != null && Swat("swat:RightHand") != null) parts.Add($"right hand {Mathf.Abs(Vector3.Distance(TrueWorld(Swat("swat:RightHand").position), P.RightGrip.position) - P.HandReach) * 100f:0} cm off its grip{(P.HandReach > 0f ? " (palm)" : "")}");
				if (Figure != null)
				{
					float worst = 0f; string at = "";
					foreach (var (s, dn) in new[] { ("swat:Hips", "soldierHip"), ("swat:Head", "soldierHead"), ("swat:LeftHand", "soldierLArmPalm"), ("swat:RightHand", "soldierRArmPalm"), ("swat:LeftFoot", "soldierLLegAnkle"), ("swat:RightFoot", "soldierRLegAnkle") })
					{
						var a = Swat(s); var b = Figure.B(dn);
						if (a == null || b == null) continue;
						float m = Vector3.Distance(TrueWorld(a.position), b.position);
						if (m > worst) { worst = m; at = dn; }
					}
					parts.Add($"drawn figure within {worst * 100f:0} cm of his hit zones (worst {at})");
				}
				if (_own != null) parts.Add($"his collider group '{_own.name}' {(_detached ? $"on its own (out of '{_parentGroup?.name}', listed there {_listed}x)" : $"in '{_parentGroup?.name}'")}");
				return LastReport = string.Join("; ", parts);
			}
		}
	}
}
