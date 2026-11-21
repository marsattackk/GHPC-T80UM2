using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC.Crew;
using GHPC.Mission;
using UnityEngine;
using Object = UnityEngine.Object;
using RCG = GHPC.PhysicsHelpers.RaycastColliderGroup;
using System.IO;
using System.IO.Compression;

namespace T80UM2.Common
{
	internal static class CrewFigure
	{
		internal sealed class Drawn
		{
			internal GameObject Root;
			internal Transform Skeleton;
			internal readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
			internal SkinnedMeshRenderer[] Renderers = new SkinnedMeshRenderer[0];
			internal Transform B(string n) => Bones.TryGetValue(n, out var bone) ? bone : null;
		}

		internal static Drawn MakeDrawn(Transform parent, string prefabToken, string[] skins, string name, List<string> notes)
		{
			var lookup = UnitSpawner.Instance != null ? UnitSpawner.Instance.AllArmyInfantryLookup : null;
			var prefab = lookup?.Info?.Where(i => i != null && i.Data != null)
				.SelectMany(i => i.Data)
				.Select(u => u.Prefab)
				.FirstOrDefault(c => c != null && c.name.Contains(prefabToken));
			if (prefab == null)
			{
				notes.Add($"no infantry prefab '{prefabToken}': no drawn figure");
				return null;
			}
			var holder = new GameObject(name + " (setup)");
			holder.SetActive(false);   // never woken: the soldier's own scripts (unit, gore, animator) must not run
			holder.transform.SetParent(parent, false);
			var clone = Object.Instantiate(prefab, holder.transform, false);
			var troop = clone.transform.Find("Troop Base");
			var skel = troop != null ? troop.Find("TRP_SKELETON") : null;
			if (skel == null)
			{
				notes.Add($"'{prefab.name}' has no Troop Base/TRP_SKELETON: no drawn figure");
				Object.DestroyImmediate(holder);
				return null;
			}
			var figure = new Drawn { Root = new GameObject(name) };
			figure.Root.SetActive(false);
			figure.Root.transform.SetParent(parent, false);
			figure.Root.layer = parent.gameObject.layer;
			skel.SetParent(figure.Root.transform, false);
			figure.Skeleton = skel;
			foreach (Transform child in skel.Cast<Transform>().ToList())
				if (child.name != "soldierHip" && child.name != "soldier") Object.DestroyImmediate(child.gameObject);
			var renderers = new List<SkinnedMeshRenderer>();
			foreach (var path in skins)
			{
				var skin = troop.Find(path);
				var smr = skin != null ? skin.GetComponent<SkinnedMeshRenderer>() : null;
				if (smr == null)
				{
					notes.Add($"skin '{path}' not found");
					continue;
				}
				skin.SetParent(figure.Root.transform, false);
				skin.gameObject.SetActive(true);
				smr.updateWhenOffscreen = true;
				renderers.Add(smr);
			}
			figure.Renderers = renderers.ToArray();
			foreach (var extra in figure.Root
				.GetComponentsInChildren<Component>(true)
				.Where(c => !(c is Transform) && !(c is SkinnedMeshRenderer))
				.ToList())
				Object.DestroyImmediate(extra);
			foreach (var bone in figure.Root.GetComponentsInChildren<Transform>(true))
			{
				bone.gameObject.layer = figure.Root.layer;
				if (!figure.Bones.ContainsKey(bone.name)) figure.Bones[bone.name] = bone;
			}
			Object.DestroyImmediate(holder);
			notes.Add($"drawn figure from '{prefab.name}': {figure.Renderers.Length} skins ({string.Join(", ", figure.Renderers.Select(r => r.name + " [" + string.Join("/", r.sharedMaterials.Where(m => m != null).Select(x => x.name)) + "]"))}), {figure.Bones.Count} bones");
			return figure;
		}

		internal static Drawn MakeDrawnFromResources(
			Assembly asm,
			string model,
			Transform parent,
			string name,
			float heat,
			List<string> notes)
		{
			byte[] Res(string resourceName)
			{
				using (var stream = asm.GetManifestResourceStream(resourceName))
				{
					if (stream == null) return null;
					using (var memory = new MemoryStream())
					{
						stream.CopyTo(memory);
						return memory.ToArray();
					}
				}
			}
			var data = Res(model + ".bin");
			if (data == null)
			{
				notes.Add($"no embedded crewman '{model}'");
				return null;
			}
			Texture2D Tex(string textureName)
			{
				var bytes = Res(textureName);
				if (bytes == null) return null;
				var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = textureName };
				return ImageConversion.LoadImage(texture, bytes, true) ? texture : null;
			}
			try
			{
				using (var reader = new BinaryReader(new GZipStream(
					new MemoryStream(data),
					CompressionMode.Decompress)))
				{
					string Str() => System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
					if (new string(reader.ReadChars(4)) != "GCRW" || reader.ReadInt32() != 1)
					{
						notes.Add($"crewman '{model}': unknown data format");
						return null;
					}
					var figure = new Drawn { Root = new GameObject(name) };
					figure.Root.SetActive(false);
					figure.Root.transform.SetParent(parent, false);
					figure.Root.layer = parent.gameObject.layer;
					var skel = new GameObject("skeleton").transform;
					skel.SetParent(figure.Root.transform, false);
					figure.Skeleton = skel;
					int boneCount = reader.ReadInt32();
					var bones = new Transform[boneCount];
					var rest = new Vector3[boneCount];
					for (int i = 0; i < boneCount; i++)
					{
						string role = Str();
						int par = reader.ReadInt32();
						rest[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
						var bone = new GameObject(role).transform;
						bone.SetParent(par >= 0 ? bones[par] : skel, false);
						bone.localPosition = par >= 0 ? rest[i] - rest[par] : rest[i];
						bones[i] = bone;
					}
					int vertexCount = reader.ReadInt32();
					Vector3[] V3()
					{
						var vectors = new Vector3[vertexCount];
						for (int i = 0;
							i < vertexCount;
							i++) vectors[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
						return vectors;
					}
					var pos = V3();
					var nrm = V3();
					var uvs = new Vector2[vertexCount];
					for (int i = 0; i < vertexCount; i++) uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
					var boneWeights = new BoneWeight[vertexCount];
					for (int i = 0;
						i < vertexCount;
						i++)
							boneWeights[i] = new BoneWeight
							{
								boneIndex0 = reader.ReadInt32(),
								weight0 = reader.ReadSingle(),
								boneIndex1 = reader.ReadInt32(),
								weight1 = reader.ReadSingle()
							};
					int submeshCount = reader.ReadInt32();
					var keys = new string[submeshCount];
					var tris = new int[submeshCount][];
					for (int submesh = 0; submesh < submeshCount; submesh++)
					{
						keys[submesh] = Str();
						tris[submesh] = new int[reader.ReadInt32()];
						for (int i = 0; i < tris[submesh].Length; i++) tris[submesh][i] = reader.ReadInt32();
					}
					var mesh = new Mesh { name = model + " (crew figure)" };
					mesh.vertices = pos;
					mesh.normals = nrm;
					mesh.uv = uvs;
					mesh.boneWeights = boneWeights;
					mesh.bindposes = rest.Select(p => Matrix4x4.Translate(-p)).ToArray();
					mesh.subMeshCount = submeshCount;
					for (int submesh = 0; submesh < submeshCount; submesh++) mesh.SetTriangles(tris[submesh], submesh);
					mesh.RecalculateBounds();
					mesh.RecalculateTangents();
					var modelObject = new GameObject(model);
					modelObject.transform.SetParent(figure.Root.transform, false);
					var smr = modelObject.AddComponent<SkinnedMeshRenderer>();
					smr.sharedMesh = mesh;
					smr.bones = bones;
					smr.rootBone = bones[0];
					smr.updateWhenOffscreen = true;
					mesh.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), vertexCount).ToArray();
					var shader = Shader.Find("Standard (FLIR)") ?? Shader.Find("Standard");
					smr.sharedMaterials = keys.Select(key =>
					{
						var material = new Material(shader) { name = $"{model} {key}" };
						var texture = Tex($"{model}_{key}.png");
						if (texture != null) material.mainTexture = texture;
						else notes.Add($"crewman '{model}': no {key} texture");
						if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", key == "head" ? 0.3f : 0.12f);
						return material;
					}).ToArray();
					if (heat > 0f) ThermalHeat.AddSource(figure.Root, heat);
					figure.Renderers = new[] { smr };
					foreach (var bone in figure.Root.GetComponentsInChildren<Transform>(true))
					{
						bone.gameObject.layer = figure.Root.layer;
						if (!figure.Bones.ContainsKey(bone.name)) figure.Bones[bone.name] = bone;
					}
					notes.Add($"drawn figure '{model}': {vertexCount} vertices, {boneCount} bones, submeshes {string.Join("/", keys)}, heat {heat:0.#}");
					return figure;
				}
			}
			catch (Exception exception)
			{
				notes.Add($"crewman '{model}' failed ({exception.GetType().Name}: {exception.Message})");
				return null;
			}
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
			float upperLength = Vector3.Distance(
				upper.position,
				lower.position), lowerLength = Vector3.Distance(lower.position, end.position);
			var shoulder = upper.position;
			var toTarget = target - shoulder;
			float dist = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 1e-3f, upperLength + lowerLength - 1e-3f);
			var direction = toTarget.normalized;
			var poleDirection = Vector3.ProjectOnPlane(pole, direction);
			if (poleDirection.sqrMagnitude < 1e-8f) poleDirection = Vector3.ProjectOnPlane(Vector3.down, direction);
			poleDirection.Normalize();
			float cosA = Mathf.Clamp(
				(upperLength * upperLength + dist * dist - lowerLength * lowerLength) / (2f * upperLength * dist),
				-1f,
				1f);
			var elbow = shoulder + upperLength * (cosA * direction + Mathf.Sqrt(1f - cosA * cosA) * poleDirection);
			SetDir(upper, lower, elbow - shoulder);
			SetDir(lower, end, shoulder + direction * dist - lower.position);
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
			// Set when a tank seats a crewman at a pedestal gun. None does in a normal build (CS0649 is off for this declaration only).
#pragma warning disable CS0649
			internal Transform Up, A, B;
#pragma warning restore CS0649
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
			// Set when a tank draws the crewman, which none does in a normal build (CS0649 is off for this declaration only).
#pragma warning disable CS0649
			internal Drawn Figure;
#pragma warning restore CS0649
			internal Func<bool> WantOut = () => false, Busy = () => false, HideDrawn = () => false;
			internal string Tag = "crew";
			internal Action<string> LogLine = m => { };

			static readonly FieldInfo ChildGroups = Reflect.Field(typeof(RCG), "childGroups");
			static readonly Type StateT = typeof(RCG).GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
			static readonly MethodInfo SetStateM = StateT == null
				? null
				: typeof(RCG).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { StateT }, null);

			Transform _fig, _swatRoot;
			Vector3 _figPos0;
			Quaternion _figRot0;
			readonly Dictionary<Transform, Quaternion> _bones0 = new Dictionary<Transform, Quaternion>();
			Matrix4x4 _figRelHips;
			bool _hatchOpened, _bodyOut;
			RCG _own, _parentGroup;
			bool _detached;
			int _listed;
			internal string LastReport = "";
			internal Transform CrewFigureRoot => _fig;

			internal bool Able => Member != null && Member.Human != null && !Member.Human.IsDead && !Member.Evacuated;

			void LateUpdate()
			{
				if (Member == null || Turret == null || Seat == null) return;
				if (_fig == null && Member.BodyPartRegistry != null) _fig = Member.BodyPartRegistry.transform;
				bool changed = Climb.Step(Time.deltaTime, WantOut(), Busy(), Able);
				if (changed)
					LogLine($"{Tag}: {Climb.State} (hatch {(Climb.HatchOpen ? "open" : "shut")}, body {(Climb.BodyOut ? "out" : "in")}, fire {(Climb.MayFire ? "allowed" : "held")}).");
				var hatch = Member.ExitHatch;
				if (Climb.HatchOpen && !_hatchOpened)
				{
					_hatchOpened = true;
					if (hatch != null) hatch.Play();
				}
				else if (!Climb.HatchOpen && _hatchOpened && !Member.Evacuated)
				{
					_hatchOpened = false;
					if (hatch != null) hatch.PlayReversed();
				}
				if (Climb.BodyOut && !_bodyOut) Out();
				else if (!Climb.BodyOut && _bodyOut) In();
				if (_bodyOut) Follow();
			}

			Matrix4x4 ParentInBone()
			{
				var parent = _fig.parent;
				var follow = parent
					.GetComponentsInParent<GHPC.Utility.LateFollow>(true)
					.FirstOrDefault(f => f.FollowTarget == Turret);
				if (follow == null || !Followers.Ready) return Turret.worldToLocalMatrix * parent.localToWorldMatrix;
				var boneToFollower = Matrix4x4.TRS(Followers.PosShift(follow), Followers.RotShift(follow), Vector3.one);
				return boneToFollower * (follow.transform.worldToLocalMatrix * parent.localToWorldMatrix);
			}

			Matrix4x4 SeatInBone() => Turret.worldToLocalMatrix * Matrix4x4.TRS(Seat.position, Seat.rotation, Vector3.one);
			readonly Dictionary<string, Transform> _swat = new Dictionary<string, Transform>();
			Transform Swat(string n)
			{
				if (_swatRoot == null) return null;
				if (_swat.Count == 0)
					foreach (var bone in _swatRoot.GetComponentsInChildren<Transform>(true))
						if (!_swat.ContainsKey(bone.name)) _swat[bone.name] = bone;
				return _swat.TryGetValue(n, out var found) ? found : null;
			}

			void Out()
			{
				_bodyOut = true;
				if (_fig == null) return;
				_figPos0 = _fig.localPosition;
				_figRot0 = _fig.localRotation;
				_swatRoot = _fig;
				_bones0.Clear();
				foreach (var bone in _fig.GetComponentsInChildren<Transform>(true))
					if (bone.name.StartsWith("swat:")) _bones0[bone] = bone.localRotation;
				var pib = ParentInBone();
				var figInBone = pib * Matrix4x4.TRS(_figPos0, _figRot0, Vector3.one);
				var hips = Swat("swat:Hips");
				var hipsInBone = hips != null
					? pib.MultiplyPoint3x4(_fig.parent.InverseTransformPoint(hips.position))
					: figInBone.MultiplyPoint3x4(Vector3.zero);
				_figRelHips = Matrix4x4.Translate(-hipsInBone) * figInBone;
				Place();
				PoseBody();
				Detach(true);
				if (Figure != null)
				{
					LayDrawn();
					Figure.Root.SetActive(!HideDrawn());
				}
				LogLine($"{Tag}: out at the seat {Report()}.");
			}

			void In()
			{
				_bodyOut = false;
				if (_fig != null)
				{
					_fig.localPosition = _figPos0;
					_fig.localRotation = _figRot0;
					foreach (var pair in _bones0) if (pair.Key != null) pair.Key.localRotation = pair.Value;
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

			Vector3 SwatPoint(Vector3 world)
				=> _fig.parent.TransformPoint(ParentInBone().inverse.MultiplyPoint3x4(Turret.InverseTransformPoint(world)));
			Vector3 TrueWorld(Vector3 swatWorld)
				=> Turret.TransformPoint(ParentInBone().MultiplyPoint3x4(_fig.parent.InverseTransformPoint(swatWorld)));

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
				if (P.LeftGrip != null)
					Reach(
						Swat("swat:LeftArm"),
						Swat("swat:LeftForeArm"),
						Swat("swat:LeftHand"),
						WristFor(Swat("swat:LeftArm"), P.LeftGrip),
						pole + SwatDir(Vector3.left) * 0.5f);
				if (P.RightGrip != null)
					Reach(
						Swat("swat:RightArm"),
						Swat("swat:RightForeArm"),
						Swat("swat:RightHand"),
						WristFor(Swat("swat:RightArm"), P.RightGrip),
						pole + SwatDir(Vector3.right) * 0.5f);
			}

			Vector3 WristFor(Transform shoulder, Transform grip)
			{
				var gripPoint = SwatPoint(grip.position);
				if (shoulder == null || P.HandReach <= 0f) return gripPoint;
				var offset = gripPoint - shoulder.position;
				return offset.sqrMagnitude < 1e-6f ? gripPoint : gripPoint - offset.normalized * P.HandReach;
			}

			void LayDrawn(bool armsOnly = false)
			{
				var figure = Figure;
				if (figure == null || figure.Root == null) return;
				if (!armsOnly)
				{
					figure.Root.transform.SetPositionAndRotation(Seat.position, Seat.rotation);
					var hip = figure.B("soldierHip");
					var sHip = Swat("swat:Hips");
					if (hip != null && sHip != null) figure.Root.transform.position += TrueWorld(sHip.position) - hip.position;
					foreach (var chain in Chains) Lay(chain);
				}
				foreach (var chain in ArmChains) Lay(chain);
			}

			void Lay((string swat, string swatTo, string drawn, string drawnTo) c)
			{
				var start = Swat(c.swat);
				var end = Swat(c.swatTo);
				if (start == null || end == null) return;
				SetDir(Figure.B(c.drawn), Figure.B(c.drawnTo), TrueWorld(end.position) - TrueWorld(start.position));
			}

			void Follow()
			{
				if (_fig == null) return;
				if (_detached
					&& _own != null
					&& _parentGroup != null
					&& ((ChildGroups.GetValue(_parentGroup) as List<RCG>)?.Contains(_own) ?? false))
					Detach(true);
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

			// A nested group is only tested inside its parent's bounds, so out on the roof he'd never be hit.
			void Detach(bool detach)
			{
				if (_fig == null) return;
				if (_own == null)
				{
					_own = _fig.GetComponent<RCG>() ?? _fig.GetComponentInChildren<RCG>(true);
					// The turret's collider group is a follower at scene root: its owner isn't among its parents.
					_parentGroup = _own != null && _own.transform.parent != null
						? _own.transform.parent.GetComponentsInParent<RCG>(true).FirstOrDefault(c => c != _own)
						: null;
					if (_own != null
						&& ChildGroups != null
						&& (_parentGroup == null || !((ChildGroups.GetValue(_parentGroup) as List<RCG>)?.Contains(_own) ?? false)))
						_parentGroup = Object.FindObjectsByType<RCG>(FindObjectsInactive.Include, FindObjectsSortMode.None)
							.FirstOrDefault(c => c != _own
								&& ((ChildGroups.GetValue(c) as List<RCG>)?.Contains(_own) ?? false))
							?? _parentGroup;
					if (_own == null || _parentGroup == null || ChildGroups == null || SetStateM == null || StateT == null)
					{
						LogLine($"{Tag}: collider groups not found (own {(_own != null ? _own.name : "none")}, parent {(_parentGroup != null ? _parentGroup.name : "none")}); his hit zones stay nested in the turret's.");
						_own = null;
						return;
					}
				}
				var list = ChildGroups.GetValue(_parentGroup) as List<RCG>;
				if (list == null) return;
				// Listed twice: Remove takes one, the other keeps him hidden.
				if (detach)
				{
					_listed = list.RemoveAll(c => c == _own);
					SetStateM.Invoke(_own, new[] { Enum.Parse(StateT, "Waiting") });
				}
				else
				{
					if (!list.Contains(_own)) list.Add(_own);
					SetStateM.Invoke(_own, new[] { Enum.Parse(StateT, "Hidden") });
				}
				_detached = detach;
			}

			internal string Report()
			{
				if (_fig == null) return "(no crew figure)";
				string P3(Vector3 world)
				{
					var localPoint = Turret.InverseTransformPoint(world);
					return $"({localPoint.x:0.00}, {localPoint.y:0.00}, {localPoint.z:0.00})";
				}
				var parts = new List<string>();
				foreach (var boneName in new[] { "swat:Hips", "swat:Head", "swat:LeftHand", "swat:RightHand" })
				{
					var bone = Swat(boneName);
					if (bone != null) parts.Add($"{boneName.Substring(5)} {P3(TrueWorld(bone.position))}");
				}
				if (P.LeftGrip != null && Swat("swat:LeftHand") != null)
					parts.Add($"left hand {Mathf.Abs(Vector3.Distance(TrueWorld(Swat("swat:LeftHand").position), P.LeftGrip.position) - P.HandReach) * 100f:0} cm off its grip{(P.HandReach > 0f ? " (palm)" : "")}");
				if (P.RightGrip != null && Swat("swat:RightHand") != null)
					parts.Add($"right hand {Mathf.Abs(Vector3.Distance(TrueWorld(Swat("swat:RightHand").position), P.RightGrip.position) - P.HandReach) * 100f:0} cm off its grip{(P.HandReach > 0f ? " (palm)" : "")}");
				if (Figure != null)
				{
					float worst = 0f;
					string worstBone = "";
					foreach (var (swatName, drawnName) in new[]
					{
						("swat:Hips", "soldierHip"),
						("swat:Head", "soldierHead"),
						("swat:LeftHand", "soldierLArmPalm"),
						("swat:RightHand", "soldierRArmPalm"),
						("swat:LeftFoot", "soldierLLegAnkle"),
						("swat:RightFoot", "soldierRLegAnkle")
					})
					{
						var swatBone = Swat(swatName);
						var drawnBone = Figure.B(drawnName);
						if (swatBone == null || drawnBone == null) continue;
						float distance = Vector3.Distance(TrueWorld(swatBone.position), drawnBone.position);
						if (distance > worst)
						{
							worst = distance;
							worstBone = drawnName;
						}
					}
					parts.Add($"drawn figure within {worst * 100f:0} cm of his hit zones (worst {worstBone})");
				}
				if (_own != null)
					parts.Add($"his collider group '{_own.name}' {(_detached ? $"on its own (out of '{_parentGroup?.name}', listed there {_listed}x)" : $"in '{_parentGroup?.name}'")}");
				return LastReport = string.Join("; ", parts);
			}
		}
	}
}
