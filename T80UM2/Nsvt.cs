using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Crew;
using GHPC.Equipment.Optics;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using HarmonyLib;
using MelonLoader;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	using Donor = T80UM2.Common.RemoteMg.Donor;

	internal static class Nsvt
	{
		static string Tag => T80UM2Mod.FriendlyName;

		internal const string GunName = "12.7mm Machine Gun NSVT";
		internal const int Rounds = 450, BeltRounds = 100;
		internal const float RoundsPerMinute = 740f;
		internal const float BoxChangeSeconds = 15f;
		const int B32PerTracer = 4;
		// The mount does +75, but seated with his hand on the handwheel's low handle he can't reach past +30.
		static readonly Vector2 Elevation = new Vector2(-5f, 30f);
		internal const float TraverseSpeed = 120f, ElevationSpeed = 60f;
		internal const float SightFov = 45f;

		static readonly Vector3 MuzzleWt = new Vector3(0.986f, 2.537f, 0.747f);
		// The НСВ throws its cases forward out of the receiver's front right, not to the side.
		internal static readonly Vector3 CaseChute = new Vector3(1.015f, 2.525f, -0.26f);
		internal static readonly Vector3 CaseChuteDir = new Vector3(0.25f, -0.1f, 1f);
		const float CaseSpeed = 1.5f, CaseSpread = 4f;
		// The box's lid is shut, so the belt comes out through its gun-side wall just under the lid and runs straight across.
		static readonly Vector3 BeltFeed = new Vector3(1.05f, 2.600f, -0.56f), BeltFeedDir = new Vector3(1f, 0f, 0f);
		static readonly Vector3 BeltMouth = new Vector3(1.135f, 2.612f, -0.56f), BeltMouthDir = new Vector3(-1f, 0f, 0f);
		const float BeltPitch = 0.0297f;
		static readonly KordBelt.Settings BeltSettings = new KordBelt.Settings { Prefix = "T80UM2_NSVT_", MeshName = "T80UM2 NSVT", SpareRounds = 14 };
		static readonly Vector3 LeftGrip = new Vector3(0.755f, 2.325f, -0.795f), RightGrip = new Vector3(1.30f, 2.385f, -0.74f);
		internal static readonly Vector3 SeatPoint = new Vector3(1.22f, 2.10f, -1.25f);

		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");
		static readonly FieldInfo Smr = Reflect.Field(typeof(ReticleMesh), "SMR");
		static readonly FieldInfo StageDuration = Reflect.Field(typeof(AmmoFeed.ReloadStage), "Duration");
		static readonly FieldInfo FeedMain = Reflect.Field(typeof(AmmoFeed), "_feedClipMain");
		static readonly PortReticles Reticles = new PortReticles(typeof(Nsvt).Assembly, T80UM2Mod.FriendlyName);

		internal sealed class Fitted : RemoteMg.Station
		{
			internal WeaponSystem Weapon;
			internal FireControlSystem Fcs;
			internal UsableOptic Sight;
			internal CrewFigure.Exposure Ex;
			internal CrewFigure.Drawn Figure;
			// Hand-laid from the cupola, so it fires from the commander's head view and not only through the К10-Т.
			internal override bool FromHeadView => true;
			internal override IEnumerable<CameraSlot> Sights => new[] { Sight?.slot }.Where(s => s != null);
		}

		static readonly Dictionary<WeaponSystem, Fitted> ByWeapon = new Dictionary<WeaponSystem, Fitted>();
		static bool _logged;

		static BeltRound.Data _round;
		static Material _roundMat;
		static (BeltRound.Data, Material, string) BeltRoundFromResources()
		{
			if (_round != null && _roundMat != null) return (_round, _roundMat, "the T-90A's belt round (read before)");
			var asm = typeof(Nsvt).Assembly;
			byte[] Res(string n)
			{
				using (var s = asm.GetManifestResourceStream(n))
				{
					if (s == null) return null;
					using (var m = new System.IO.MemoryStream()) { s.CopyTo(m); return m.ToArray(); }
				}
			}
			var data = BeltRound.Read(Res("kord_belt_round.bin"));
			if (data == null) return (null, null, "no embedded belt round: the drawn one");
			Texture2D Tex(string n, bool linear)
			{
				var b = Res(n);
				if (b == null) return null;
				var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = "T80UM2 " + n };
				if (!ImageConversion.LoadImage(t, b, true)) return null;
				t.hideFlags = HideFlags.DontUnloadUnusedAsset;
				return t;
			}
			var albedo = Tex("kord_belt_round_albedo.png", false);
			if (albedo == null) return (null, null, "no embedded belt round map: the drawn round");
			// Standard (FLIR) so the rounds warm up in the thermal sights with the rest of the tank.
			var mat = new Material(Shader.Find("Standard (FLIR)") ?? Shader.Find("Standard")) { name = "T80UM2 NSVT belt round (T-90A's mg_6p49_kord_ammo)", mainTexture = albedo };
			var nrm = Tex("kord_belt_round_normal.png", true);
			if (nrm != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
			var met = Tex("kord_belt_round_metal.png", true);
			if (met != null && mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICGLOSSMAP"); mat.SetFloat("_GlossMapScale", 1f); }
			mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
			_round = data; _roundMat = mat;
			return (data, mat, $"the T-90A's belt round ({data.Round.Length / 3} triangles; maps: albedo, {(nrm != null ? "normal" : "NO normal")}, {(met != null ? "metal" : "NO metal")})");
		}

		internal static void Apply(Vehicle vehicle)
		{
			try { Fit(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{Tag} NSVT: {e}"); }
		}

		static void Fit(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var rig = T80UM2Mod.Rig;
			RigMap.Mount M(string g) => rig?.Mounts?.FirstOrDefault(x => x.Group == g);
			var hM = M("NSVT_H"); var vM = M("NSVT_V");
			if (hM == null || vM == null) { MelonLogger.Warning($"{Tag}: the bundle has no НСВТ mounts (old bundle?); the НСВТ stays fixed and unarmed."); return; }
			RemoteMg.SetLogTag($"{Tag} NSVT: ");
			var d = Donor.Get(Tag);
			if (d == null) { MelonLogger.Warning($"{Tag}: the T-64B station donor was not found; the НСВТ stays fixed and unarmed."); return; }
			var turret = Followers.TurretBone(vehicle);
			var wm = vehicle.GetComponent<WeaponsManager>();
			if (turret == null || wm?.Weapons == null || wm.Weapons.Length < 2) { MelonLogger.Warning($"{Tag}: no turret bone or main gun / coax rows; the НСВТ stays fixed."); return; }
			Vector3 W(Vector3 prefab) => root.TransformPoint(prefab);
			var problems = new List<string>();

			var axisT = new GameObject("T80UM2 NSVT axis").transform;
			axisT.SetParent(turret, false);
			axisT.SetPositionAndRotation(W(hM.Pivot), root.rotation * Quaternion.FromToRotation(Vector3.up, hM.Axis.normalized));
			var drumT = new GameObject("T80UM2 NSVT pedestal head").transform;
			drumT.SetParent(axisT, false);
			var frame = new GameObject("T80UM2 NSVT").transform;
			frame.SetParent(drumT, false);
			frame.SetPositionAndRotation(W(hM.Pivot), root.rotation);
			var cradleT = new GameObject("T80UM2 NSVT cradle").transform;
			cradleT.SetParent(frame, false);
			cradleT.SetPositionAndRotation(W(vM.Pivot), root.rotation * Quaternion.FromToRotation(Vector3.right, vM.Axis.normalized));
			Transform Carrier(string g) => g == hM.Group ? frame : g == vM.Group ? cradleT : null;
			// The model's cradle and gun carry the turret's 1.5 deg nose-down pitch, GHPC lays the cradle level.
			float pitch = rig.HasAxes ? rig.PitchDeg : 0f;
			void Level(Transform t) => t.RotateAround(cradleT.position, cradleT.right, -pitch);
			Transform cradleVis = null;
			foreach (var m in rig.Mounts)
			{
				var vis = turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "T80UM2_" + m.Group)
					?? turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == m.Group);
				var to = m.Axis != Vector3.zero ? Carrier(m.Group) : Carrier(m.Rides);
				if (vis == null || to == null) continue;
				vis.name = "T80UM2_" + m.Group;
				vis.SetParent(to, true);
				if (to == cradleT) Level(vis);
				if (m.Group == vM.Group) cradleVis = vis;
			}

			var holder = new GameObject("T80UM2 NSVT (setup)");
			holder.SetActive(false);
			holder.transform.SetParent(root, false);
			var (full, part) = CrewClimb.Belts(Rounds, BeltRounds);
			var gun = RemoteMg.CloneGun(d, holder.transform, new RemoteMg.GunSettings { Name = GunName, DamageName = "NSVT Utyos", BeltRounds = BeltRounds, Belts = full + (part > 0 ? 1 : 0), RoundsPerMinute = RoundsPerMinute, B32PerTracer = B32PerTracer, BeltName = "12.7mm B-32 / BZT-44 (100)" }, Tag);
			if (gun == null) { Object.Destroy(holder); return; }
			float donorBox = RemoteMg.StagesSum(gun.Feed.ClipReloadStages);
			if (donorBox > 0f) foreach (var s in gun.Feed.ClipReloadStages) StageDuration.SetValue(s, (float)StageDuration.GetValue(s) * BoxChangeSeconds / donorBox);
			var (fcsGo, fcs, sightGo, r2, s2) = RemoteMg.CloneFcs(d, holder.transform, "T80UM2 NSVT fire control");
			var eye = new GameObject("T80UM2 K10-T eye").transform;
			eye.SetParent(cradleT, false);
			var reflector = cradleT.position + Quaternion.AngleAxis(-pitch, cradleT.right) * (W(K10Window.ReflectorWt) - cradleT.position);
			eye.SetPositionAndRotation(reflector - cradleT.forward * K10Window.EyeRelief, cradleT.rotation);
			var windowNotes = new List<string>();
			K10Window.Build(cradleT, reflector, cradleT.rotation, Quaternion.AngleAxis(-pitch, Vector3.right), cradleVis, root, eye, windowNotes);
			fcs.ReferenceTransform = eye;
			fcs.LaserOrigin = null;
			fcs.LinkedWeaponSystems = new[] { gun.Weapon };
			var scripts = new GameObject("---T80UM2 NSVT SCRIPTS---");
			scripts.transform.SetParent(root, false);
			var drum = RemoteMg.Platform(scripts.transform, "NSVT traverse (by hand)", d.CupolaDrive, drumT, EnumHelpers.Axis3.Y, Vector2.zero, TraverseSpeed, null);
			var cradle = RemoteMg.Platform(scripts.transform, "NSVT elevation (handwheel)", d.ElevationDrive, cradleT, EnumHelpers.Axis3.X, Elevation, ElevationSpeed, null);
			foreach (var p in new[] { drum, cradle })
			{
				p.SpeedUnpowered = p.SpeedPowered;
				p.ProtectFromBadProjection = false;
				p.Stabilized = false;
			}
			fcs.Mounts = new[] { drum, cradle };

			var sight = sightGo.GetComponent<UsableOptic>();
			SetupSight(sight, fcs, eye, gun, root, problems);
			var grips = new[] { LeftGrip, RightGrip }.Select((g, i) => { var t = new GameObject(i == 0 ? "T80UM2 NSVT traverse handle" : "T80UM2 NSVT handwheel handle").transform; t.SetParent(i == 0 ? frame : cradleT, false); t.position = W(g); return t; }).ToArray();

			gun.Go.transform.SetParent(cradleT, false);
			gun.Go.transform.SetPositionAndRotation(W(MuzzlePoint(vM, pitch)), cradleT.rotation);
			gun.Go.SetActive(true);
			fcsGo.transform.SetParent(scripts.transform, false);
			fcsGo.SetActive(true);
			sightGo.transform.SetParent(eye, false);
			sightGo.transform.localPosition = Vector3.zero;
			sightGo.transform.localRotation = Quaternion.identity;
			sightGo.SetActive(true);
			Object.Destroy(holder);
			gun.Weapon.SetCycleTime(gun.Cycle);
			Armour.Modules.BuildOn(vehicle, "NSVT", cradleT);
			Vector3 OnCradle(Vector3 prefab) => cradleT.position + Quaternion.AngleAxis(-pitch, cradleT.right) * (W(prefab) - cradleT.position);
			KordBelt.Belt belt = null;
			try
			{
				var feedW = OnCradle(BeltFeed);
				var (round, roundMat, roundLog) = BeltRoundFromResources();
				belt = KordBelt.Build(frame, cradleT, BeltSettings, frame.InverseTransformPoint(feedW), frame.InverseTransformDirection(root.TransformDirection(BeltFeedDir)),
					frame.InverseTransformPoint(W(BeltMouth)), frame.InverseTransformDirection(root.TransformDirection(BeltMouthDir)), frame.InverseTransformDirection(cradleT.forward), BeltPitch,
					round, roundMat);
				belt.Attach(gun.Weapon, gun.Cycle);
			}
			catch (System.Exception e) { MelonLogger.Error($"{Tag} NSVT belt: {e}"); }
			if (T80UM2Mod.NsvtCases == null || T80UM2Mod.NsvtCases.Value)
			{
				var cases = belt != null && belt.CaseMesh != null ? KordBrass.CaseSource.FromBelt(belt)
					: new KordBrass.CaseSource { Mesh = KordBrass.Case127("T80UM2 NSVT spent case"), Material = KordBrass.Brass("T80UM2 NSVT case brass"), Layer = cradleT.gameObject.layer, MeshName = "T80UM2 NSVT" };
				cases.Prefix = "T80UM2_"; cases.MaxCases = Rounds; cases.Speed = CaseSpeed; cases.Spread = CaseSpread;
				var chuteW = OnCradle(CaseChute);
				var dirOnCradle = cradleT.InverseTransformDirection(Quaternion.AngleAxis(-pitch, cradleT.right) * root.TransformDirection(CaseChuteDir));
				KordBrass.Attach(vehicle, gun.Weapon, gun.Cycle, cradleT, cradleT.InverseTransformPoint(chuteW), dirOnCradle, cases, new[] { turret, turret.parent });
			}

			var info = new WeaponSystemInfo { Name = GunName, Role = WeaponSystemRole.RoofGun, Weapon = gun.Weapon, FCS = fcs };
			var st = scripts.AddComponent<Fitted>();
			st.Info = info;
			st.Weapon = gun.Weapon; st.Fcs = fcs; st.Sight = sight;
			RemoteMg.Register(vehicle, wm, info, new[] { drum, cradle }, new Component[] { drum, cradle, gun.Weapon, gun.Feed, fcs, sight.slot }, st.Sights);
			ByWeapon[gun.Weapon] = st;
			foreach (var dead in ByWeapon.Keys.Where(k => k == null).ToList()) ByWeapon.Remove(dead);

			var seat = new GameObject("T80UM2 NSVT commander's seat").transform;
			seat.SetParent(frame, false);
			seat.SetPositionAndRotation(W(SeatPoint), root.rotation);
			var ex = scripts.AddComponent<CrewFigure.Exposure>();
			ex.enabled = false;
			ex.Turret = turret;
			ex.Seat = seat;
			ex.SeatRest = Quaternion.Inverse(turret.rotation) * seat.rotation;
			ex.Climb = new CrewClimb { OutSeconds = 3f, InSeconds = 2.5f, Linger = 3f };
			ex.P = new CrewFigure.Pose
			{
				LeftGrip = grips[0], RightGrip = grips[1],
				Spine = new Vector3(0f, 0.94f, 0.34f), Neck = new Vector3(0f, 0.97f, 0.25f),
				LeftThigh = new Vector3(0.05f, 0.15f, 1f), RightThigh = new Vector3(0.25f, 0.3f, 1f),
				LeftShin = new Vector3(-0.2f, -0.2f, 0.98f), RightShin = new Vector3(0.2f, -0.15f, 1f),
				ElbowPole = new Vector3(0f, -0.3f, -0.5f), HandReach = 0.08f,
			};
			ex.Tag = $"{Tag} NSVT commander";
			ex.WantOut = () => Climbs && IsPlayers(st) && SelectedBy(st);
			ex.Busy = () => gun.Feed != null && gun.Feed.Reloading;
			ex.HideDrawn = () => { var cam = Camera.main; return cam != null && Vector3.Distance(cam.transform.position, eye.position) < 0.6f; };
			st.Ex = ex;
			scripts.AddComponent<Link>().Init(st);

			foreach (var p in problems) MelonLogger.Warning($"{Tag} NSVT: {p}");
			MelonCoroutines.Start(Late(vehicle, st, part));
			if (_logged) return;
			_logged = true;
		}

		static Vector3 MuzzlePoint(RigMap.Mount v, float pitch) => v.Pivot + Quaternion.AngleAxis(-pitch, Vector3.right) * (MuzzleWt - v.Pivot);

		static void SetupSight(UsableOptic o, FireControlSystem fcs, Transform eye, RemoteMg.Gun gun, Transform root, List<string> problems)
		{
			o.name = "K10-T collimator sight";
			var s = o.slot;
			if (s.gameObject != o.gameObject) s.gameObject.name = o.name;
			SlotReparent.SetValue(s, eye);
			o.FCS = fcs;
			o.RegisterAtStart = true;
			o.Mounts = new AimablePlatform[0];
			o.Alignment = OpticAlignment.Fixed;
			o.RotateElevation = false;
			o.RotateAzimuth = false;
			o.RegisteredAmmoTypes = new[] { gun.B32 };
			s.ExclusiveWeapons = new[] { gun.Weapon };
			s.SlotToUseOnDestroy = root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(x => x.name == "commander head");
			s.IsExterior = false;
			s.DefaultFov = SightFov;
			s.OtherFovs = new float[0];
			s.AllowFreeZoom = false;
			s.fovAspect = false;
			s.VisionType = NightVisionType.None;
			s.LinkedNightSight = null;
			s.NightSightAtNightOnly = false;
			s.BaseBlur = 0f;
			o.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(s);
			if (o.reticleMesh != null && o.reticleMesh.reticleSO != null)
			{
				var host = o.reticleMesh.reticleSO;
				o.reticleMesh.reticleSO = Reticles.Build("k10t_aa.jsonc", host, host, new Dictionary<string, AmmoCodexScriptable>());
				Smr.SetValue(o.reticleMesh, null);
				ReticleBuild.PrepareCopy(o.reticleMesh);
				o.gameObject.AddComponent<K10Window.HideReticle>().Init(o.reticleMesh, Smr);
			}
			else problems.Add("the donor gunsight has no reticle mesh");
		}

		static IEnumerator Late(Vehicle vehicle, Fitted st, int part)
		{
			for (int i = 0; i < 4; i++) yield return null;
			if (vehicle == null || st == null) yield break;
			try
			{
				var wm = vehicle.GetComponent<WeaponsManager>();
				var gunner = wm.Weapons.Where(w => w.Role == WeaponSystemRole.MainGun || w.Role == WeaponSystemRole.Coaxial).Select(w => w.Weapon).Where(w => w != null).ToArray();
				var fit = vehicle.GetComponentInChildren<Sights.Fitted>(true);
				// Same trick as the vanilla M60A3 / T-64B: picking the NSVT switches to the К10-Т and back.
				var slots = new[] { fit?.Day?.slot, fit?.Night?.slot }.Where(x => x != null).ToArray();
				foreach (var s in slots) s.ExclusiveWeapons = gunner;
				st.Fcs.SetStabsActive(false);
				RemoteMg.LateSetup(vehicle);
				// 450 is 4 x 100 + 50, so the box loaded at spawn is the 50.
				var feed = st.Weapon.Feed;
				if (part > 0 && FeedMain?.GetValue(feed) is Queue<AmmoType> q)
					while (q.Count > 0 && q.Count + (feed.AmmoTypeInBreech != null ? 1 : 0) > part) q.Dequeue();
				var crew = vehicle.GetComponent<CrewManager>();
				st.Ex.Member = crew?.GetCrewMember(CrewPosition.Commander);
				st.Ex.Figure = st.Figure;
				st.Ex.enabled = st.Ex.Member != null;
			}
			catch (System.Exception e) { MelonLogger.Error($"{Tag} NSVT late setup: {e}"); }
		}

		// He works it from inside with the hatch shut for now. The climb-out code stays for when he gets drawn.
		const bool Climbs = false;
		static bool IsPlayers(Fitted st) => PlayerInput.Instance != null && (object)PlayerInput.Instance.CurrentPlayerUnit == st.GetComponentInParent<Vehicle>();
		static bool SelectedBy(Fitted st)
		{
			var module = PlayerInput.Instance?.CurrentPlayerCrewBrain?.WeaponsModule as CrewBrainWeaponsModule;
			return module?.ActiveWeapon != null && module.ActiveWeapon.Weapon == st.Weapon;
		}

		internal class Link : MonoBehaviour
		{
			Fitted _st;
			internal void Init(Fitted st) => _st = st;
			void Update()
			{
				var feed = _st?.Weapon != null ? _st.Weapon.Feed : null;
				if (feed == null || _st.Ex == null) return;
				bool hold = Climbs && !_st.Ex.Climb.MayFire;
				if (feed.ForcePauseReload != hold) feed.ForcePauseReload = hold;
			}
		}

		[HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.AbleToFire), MethodType.Getter)]
		static class OutToFirePatch
		{
			static void Postfix(WeaponSystem __instance, ref bool __result)
			{
				try
				{
					if (!__result || !ByWeapon.TryGetValue(__instance, out var st) || st == null || st.Ex == null) return;
					if (Climbs ? !st.Ex.Climb.MayFire : !IsPlayers(st)) __result = false;
				}
				catch (System.Exception e) { PatchGuard.Failed("NSVT fires only with the commander out", e); }
			}
		}
	}
}
