using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Crew;
using GHPC.Equipment.Optics;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using MelonLoader;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	using Donor = T80UM2.Common.RemoteMg.Donor;

	// The commander's 12.7 mm НСВТ «Утёс» on its open pedestal mount (тумбовая установка), with the К10-Т collimator on its cradle.
	internal static partial class Commander
	{
		static string Tag => T80UM2Mod.FriendlyName;

		internal const string GunName = "12.7mm Machine Gun NSVT";
		// 450 rounds in the family's 100-round belts: 4 x 100 + 50, the part belt the one loaded at spawn.
		internal const int Rounds = 450, BeltRounds = 100;
		// «680-800» (the Т-64А technical description): the middle, my guess.
		internal const float RoundsPerMinute = 740f;
		// No source: my guess.
		internal const float BoxChangeSeconds = 15f;
		// The donor's 4:1 B-32 / BZT-44 (the НСВТ's own mix not found).
		const int B32PerTracer = 4;
		// The mount does +75, but seated with his hand on the handwheel's low handle he can't reach past +30.
		static readonly Vector2 Elevation = new Vector2(-5f, 30f);
		// By hand, no source: my guess.
		internal const float TraverseSpeed = 120f, ElevationSpeed = 60f;
		// A 1x collimator over the open view: my guess.
		internal const float SightFov = 45f;

		// Prefab space, turret at rest (War Thunder's bones and meshes).
		static readonly Vector3 MuzzleWt = new Vector3(0.986f, 2.537f, 0.747f);
		// The НСВ moves the extracted case right along the bolt face and the next case pushes it out forward through the case
		// chute («выбросом стреляных гильз вперёд, а не в сторону»). With the belt from the right, the chute is at the receiver's
		// front right: War Thunder's receiver ends at z -0.28, the bore is at x 0.98, y about 2.52, and the cradle's right plate
		// (x 1.05) runs up to it. The case leaves just ahead of that corner, mouth first, pushed out slowly, a little right and
		// down, and falls on the turret roof. Place, direction and speed are my guess (the source gives the direction, not the
		// window's size or the speed).
		internal static readonly Vector3 CaseChute = new Vector3(1.015f, 2.525f, -0.26f);
		internal static readonly Vector3 CaseChuteDir = new Vector3(0.25f, -0.1f, 1f);
		const float CaseSpeed = 1.5f, CaseSpread = 4f;
		// War Thunder's box on the pedestal head's right (inside x 1.14..1.40, z -0.68..-0.45) has a closed lid, so the belt
		// leaves through its gun-side wall just under the lid (the mouth, x 1.135, the lid's rim at y 2.64) and runs straight
		// across the 9 cm gap into the receiver's right side (the feed: receiver face x 1.037, under the cover, the tray at
		// z -0.67..-0.44), each round centred on the tray (z -0.56: the round's centre, its base at z -0.63). The rounds are the
		// T-90A's: one round of its War Thunder Kord belt with its link (same 12.7x108 cartridge, same link family), at that
		// belt's link pitch of 2.97 cm. Points and directions are my guess from the model.
		static readonly Vector3 BeltFeed = new Vector3(1.05f, 2.600f, -0.56f), BeltFeedDir = new Vector3(1f, 0f, 0f);
		static readonly Vector3 BeltMouth = new Vector3(1.135f, 2.612f, -0.56f), BeltMouthDir = new Vector3(-1f, 0f, 0f);
		const float BeltPitch = 0.0297f;
		static readonly KordBelt.Settings BeltSettings = new KordBelt.Settings
		{
			Prefix = "T80UM2_NSVT_",
			MeshName = "T80UM2 NSVT",
			SpareRounds = 14,
		};
		// The mount's own controls (no spade grips: «рукоятка горизонтального наведения, маховик вертикального наведения»,
		// «Техническое описание танка Т-80Б»), where War Thunder models them. The traverse handle sits low on the left of the
		// pedestal head (its grip x 0.70..0.81) and turns with the pedestal. The handwheel's D-handle is at the end of its bar
		// behind the magazine (x 1.29..1.31) and goes up and down with the gun.
		static readonly Vector3 LeftGrip = new Vector3(0.755f, 2.325f, -0.795f), RightGrip = new Vector3(1.30f, 2.385f, -0.74f);
		// For a commander outside the hatch, which this tank does not do (switched off: Climbs).
		// Sitting on the roof behind the gun: his hips right of the cupola, in front of the open lid, 10 cm over the roof (2.0
		// there). Fitted against the model at rest so only his hands touch metal: my guess.
		internal static readonly Vector3 SeatPoint = new Vector3(1.22f, 2.10f, -1.25f);

		internal static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");
		static readonly FieldInfo StageDuration = Reflect.Field(typeof(AmmoFeed.ReloadStage), "Duration");
		static readonly FieldInfo FeedMain = Reflect.Field(typeof(AmmoFeed), "_feedClipMain");

		internal sealed partial class Station : RemoteMg.Station
		{
			internal WeaponSystem Weapon;
			internal FireControlSystem Fcs;
			internal UsableOptic Sight;
			internal CrewFigure.Exposure Ex;
			// The rounds of the part belt, which the box loaded at spawn is cut to (Commander.LateSetup).
			internal int Part;
			// Hand-laid from the cupola, so it fires from the commander's head view and not only through the К10-Т.
			internal override bool FromHeadView => true;
			internal override IEnumerable<CameraSlot> Sights => new[] { Sight?.slot }.Where(slot => slot != null);
		}

		static readonly Dictionary<WeaponSystem, Station> ByWeapon = new Dictionary<WeaponSystem, Station>();

		// The belt's round: one round of the T-90A's War Thunder Kord belt (embedded as kord_belt_round.bin) on its own War
		// Thunder maps (the T-90A's mg_6p49_kord_ammo albedo, normal and metal), drawn as the bundle's small arms are (Standard
		// (FLIR): warm in the thermal sights with the tank). Read once. Nulls (and the drawn round) when a resource is missing.
		static BeltRound.Data _round;
		static Material _roundMat;
		static (BeltRound.Data, Material, string) BeltRoundFromResources()
		{
			if (_round != null && _roundMat != null) return (_round, _roundMat, "the T-90A's belt round (read before)");
			var assembly = typeof(Commander).Assembly;
			byte[] Res(string name)
			{
				using (var stream = assembly.GetManifestResourceStream(name))
				{
					if (stream == null) return null;
					using (var memory = new System.IO.MemoryStream())
					{
						stream.CopyTo(memory);
						return memory.ToArray();
					}
				}
			}
			var data = BeltRound.Read(Res("kord_belt_round.bin"));
			if (data == null) return (null, null, "no embedded belt round: the drawn one");
			Texture2D Tex(string name, bool linear)
			{
				var bytes = Res(name);
				if (bytes == null) return null;
				var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = "T80UM2 " + name };
				if (!ImageConversion.LoadImage(texture, bytes, true)) return null;
				texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
				return texture;
			}
			var albedo = Tex("kord_belt_round_albedo.png", false);
			if (albedo == null) return (null, null, "no embedded belt round map: the drawn round");
			// Standard (FLIR) so the rounds warm up in the thermal sights with the rest of the tank.
			var material = new Material(Shader.Find("Standard (FLIR)") ?? Shader.Find("Standard"))
			{
				name = "T80UM2 NSVT belt round (T-90A's mg_6p49_kord_ammo)",
				mainTexture = albedo,
			};
			var normal = Tex("kord_belt_round_normal.png", true);
			if (normal != null && material.HasProperty("_BumpMap"))
			{
				material.SetTexture("_BumpMap", normal);
				material.EnableKeyword("_NORMALMAP");
			}
			var metal = Tex("kord_belt_round_metal.png", true);
			if (metal != null && material.HasProperty("_MetallicGlossMap"))
			{
				material.SetTexture("_MetallicGlossMap", metal);
				material.EnableKeyword("_METALLICGLOSSMAP");
				material.SetFloat("_GlossMapScale", 1f);
			}
			material.hideFlags = HideFlags.DontUnloadUnusedAsset;
			_round = data;
			_roundMat = material;
			return (
				data,
				material,
				$"the T-90A's belt round ({data.Round.Length / 3} triangles; maps: albedo, {(normal != null ? "normal" : "NO normal")}, {(metal != null ? "metal" : "NO metal")})");
		}

		internal static void Apply(Vehicle vehicle)
		{
			DevFitting(vehicle);
			FitStation(vehicle);
		}

		static void FitStation(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var rig = T80UM2Mod.Rig;
			RigMap.Mount MountOf(string group) => rig?.Mounts?.FirstOrDefault(mount => mount.Group == group);
			var horizontalMount = MountOf("NSVT_H");
			var verticalMount = MountOf("NSVT_V");
			if (horizontalMount == null || verticalMount == null)
			{
				MelonLogger.Warning($"{Tag}: the rig map has no НСВТ mounts; the НСВТ stays fixed and unarmed.");
				return;
			}
			var donor = Donor.Get(Tag);
			if (donor == null)
			{
				MelonLogger.Warning($"{Tag}: the T-64B station donor was not found; the НСВТ stays fixed and unarmed.");
				return;
			}
			var turret = Followers.TurretBone(vehicle);
			var weaponsManager = vehicle.GetComponent<WeaponsManager>();
			if (turret == null || weaponsManager?.Weapons == null || weaponsManager.Weapons.Length < 2)
			{
				MelonLogger.Warning($"{Tag}: no turret bone or main gun / coax rows; the НСВТ stays fixed.");
				return;
			}
			// At spawn the turret is at rest: prefab space = root space.
			Vector3 ToWorld(Vector3 prefab) => root.TransformPoint(prefab);
			var problems = new List<string>();

			// The drives' transforms: the pedestal head about its axis, the cradle at the trunnion.
			var axisT = new GameObject("T80UM2 NSVT axis").transform;
			axisT.SetParent(turret, false);
			axisT.SetPositionAndRotation(
				ToWorld(horizontalMount.Pivot),
				root.rotation * Quaternion.FromToRotation(Vector3.up, horizontalMount.Axis.normalized));
			var drumT = new GameObject("T80UM2 NSVT pedestal head").transform;
			drumT.SetParent(axisT, false);
			var frame = new GameObject("T80UM2 NSVT").transform;
			frame.SetParent(drumT, false);
			frame.SetPositionAndRotation(ToWorld(horizontalMount.Pivot), root.rotation);
			var cradleT = new GameObject("T80UM2 NSVT cradle").transform;
			cradleT.SetParent(frame, false);
			cradleT.SetPositionAndRotation(
				ToWorld(verticalMount.Pivot),
				root.rotation * Quaternion.FromToRotation(Vector3.right, verticalMount.Axis.normalized));
			Transform Carrier(string group) => group == horizontalMount.Group ? frame : group == verticalMount.Group ? cradleT : null;
			// The model's cradle and gun carry the turret's 1.5 deg nose-down pitch, but GHPC lays the cradle level.
			float pitch = rig.HasAxes ? rig.PitchDeg : 0f;
			Transform cradleVis = null;
			foreach (var mount in rig.Mounts)
			{
				var vis = turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "T80UM2_" + mount.Group)
					?? turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == mount.Group);
				var carrier = mount.Axis != Vector3.zero ? Carrier(mount.Group) : Carrier(mount.Rides);
				if (vis == null || carrier == null) continue;
				vis.name = "T80UM2_" + mount.Group;
				vis.SetParent(carrier, true);
				if (carrier == cradleT) vis.RotateAround(cradleT.position, cradleT.right, -pitch);
				if (mount.Group == verticalMount.Group) cradleVis = vis;
			}

			// The gun, its fire control, the К10-Т (shared RemoteMg).
			var holder = new GameObject("T80UM2 NSVT (setup)");
			holder.SetActive(false);
			holder.transform.SetParent(root, false);
			var (full, part) = CrewClimb.Belts(Rounds, BeltRounds);
			var gun = RemoteMg.CloneGun(
				donor,
				holder.transform,
				new RemoteMg.GunSettings
				{
					Name = GunName,
					DamageName = "NSVT Utyos",
					BeltRounds = BeltRounds,
					Belts = full + (part > 0 ? 1 : 0),
					RoundsPerMinute = RoundsPerMinute,
					B32PerTracer = B32PerTracer,
					BeltName = "12.7mm B-32 / BZT-44 (100)",
				},
				Tag);
			if (gun == null)
			{
				Object.Destroy(holder);
				return;
			}
			float donorBox = RemoteMg.StagesSum(gun.Feed.ClipReloadStages);
			if (donorBox > 0f)
			{
				foreach (var stage in gun.Feed.ClipReloadStages)
					StageDuration?.SetValue(stage,(float)StageDuration.GetValue(stage) * BoxChangeSeconds / donorBox);
			}
			var (fcsGo, fcs, sightGo, _, _) = RemoteMg.CloneFcs(donor, holder.transform, "T80UM2 NSVT fire control");
			var eye = new GameObject("T80UM2 K10-T eye").transform;
			eye.SetParent(cradleT, false);
			// Levelled as the cradle's visuals.
			var reflector = cradleT.position
				+ Quaternion.AngleAxis(-pitch, cradleT.right) * (ToWorld(K10Window.ReflectorWt) - cradleT.position);
			eye.SetPositionAndRotation(reflector - cradleT.forward * K10Window.EyeRelief, cradleT.rotation);
			var windowNotes = new List<string>();
			// The window and the clean sight body in the sight-axis frame, the plate levelled and the model's holder out of the view.
			K10Window.Build(
				cradleT,
				reflector,
				cradleT.rotation,
				Quaternion.AngleAxis(-pitch, Vector3.right),
				cradleVis,
				root,
				eye,
				windowNotes);
			fcs.ReferenceTransform = eye;
			// No rangefinder on a collimator.
			fcs.LaserOrigin = null;
			fcs.LinkedWeaponSystems = new[] { gun.Weapon };
			var scripts = new GameObject("---T80UM2 NSVT SCRIPTS---");
			scripts.transform.SetParent(root, false);
			var drum = RemoteMg.Platform(
				scripts.transform,
				"NSVT traverse (by hand)",
				donor.CupolaDrive,
				drumT,
				EnumHelpers.Axis3.Y,
				Vector2.zero,
				TraverseSpeed,
				null);
			var cradle = RemoteMg.Platform(
				scripts.transform,
				"NSVT elevation (handwheel)",
				donor.ElevationDrive,
				cradleT,
				EnumHelpers.Axis3.X,
				Elevation,
				ElevationSpeed,
				null);
			foreach (var platform in new[] { drum, cradle })
			{
				// Hand-laid: no power to lose.
				platform.SpeedUnpowered = platform.SpeedPowered;
				// All round in one go.
				platform.ProtectFromBadProjection = false;
				platform.Stabilized = false;
			}
			fcs.Mounts = new[] { drum, cradle };

			var sight = sightGo.GetComponent<UsableOptic>();
			SetupSight(sight, fcs, eye, gun, root, problems);
			var grips = new[] { LeftGrip, RightGrip }
				.Select(
					(grip, index) =>
					{
						var handle = new GameObject(index == 0 ? "T80UM2 NSVT traverse handle" : "T80UM2 NSVT handwheel handle").transform;
						handle.SetParent(index == 0 ? frame : cradleT, false);
						handle.position = ToWorld(grip);
						return handle;
					})
				.ToArray();

			// Place and wake: weapon (feed, rack), fire control, sight.
			gun.Go.transform.SetParent(cradleT, false);
			gun.Go.transform.SetPositionAndRotation(ToWorld(MuzzlePoint(verticalMount, pitch)), cradleT.rotation);
			gun.Go.SetActive(true);
			fcsGo.transform.SetParent(scripts.transform, false);
			fcsGo.SetActive(true);
			sightGo.transform.SetParent(eye, false);
			sightGo.transform.localPosition = Vector3.zero;
			sightGo.transform.localRotation = Quaternion.identity;
			sightGo.SetActive(true);
			Object.Destroy(holder);
			gun.Weapon.SetCycleTime(gun.Cycle);
			// The gun's hit zone on the cradle.
			Conversion.Modules.BuildOn(vehicle, "NSVT", cradleT);
			// Points on the cradle in the model are tilted with its 1.5 deg pitch: levelled as the cradle's visuals.
			Vector3 OnCradle(Vector3 prefab) =>
				cradleT.position + Quaternion.AngleAxis(-pitch, cradleT.right) * (ToWorld(prefab) - cradleT.position);
			// The belt from the box to the feed: War Thunder models no belt here, so the shared belt code lays the T-90A's War
			// Thunder round along it. It flexes as the gun elevates, feeds a link a shot, runs in at a box's end.
			KordBelt.Belt belt = null;
			try
			{
				var feedW = OnCradle(BeltFeed);
				var (round, roundMat, roundLog) = BeltRoundFromResources();
				belt = KordBelt.Build(
					frame,
					cradleT,
					BeltSettings,
					frame.InverseTransformPoint(feedW),
					frame.InverseTransformDirection(root.TransformDirection(BeltFeedDir)),
					frame.InverseTransformPoint(ToWorld(BeltMouth)),
					frame.InverseTransformDirection(root.TransformDirection(BeltMouthDir)),
					frame.InverseTransformDirection(cradleT.forward),
					BeltPitch,
					round,
					roundMat);
				belt.Attach(gun.Weapon, gun.Cycle);
			}
			catch (System.Exception exception) { MelonLogger.Error($"{Tag} NSVT belt: {exception}"); }
			// The spent cases ("NSVT spent cases", on by default): the case cut from the belt round's cartridge (the plain drawn
			// case without a belt), pushed out forward at the receiver's front right (CaseChute above) onto the turret roof.
			if (T80UM2Mod.NsvtCases == null || T80UM2Mod.NsvtCases.Value)
			{
				var cases = belt != null && belt.CaseMesh != null
					? KordBrass.CaseSource.FromBelt(belt)
					: new KordBrass.CaseSource
					{
						Mesh = KordBrass.Case127("T80UM2 NSVT spent case"),
						Material = KordBrass.Brass("T80UM2 NSVT case brass"),
						Layer = cradleT.gameObject.layer,
						MeshName = "T80UM2 NSVT",
					};
				cases.Prefix = "T80UM2_";
				cases.MaxCases = Rounds;
				cases.Speed = CaseSpeed;
				cases.Spread = CaseSpread;
				var chuteW = OnCradle(CaseChute);
				var dirOnCradle = cradleT.InverseTransformDirection(
					Quaternion.AngleAxis(-pitch, cradleT.right) * root.TransformDirection(CaseChuteDir));
				KordBrass.Attach(
					vehicle,
					gun.Weapon,
					gun.Cycle,
					cradleT,
					cradleT.InverseTransformPoint(chuteW),
					dirOnCradle,
					cases,
					new[] { turret, turret.parent });
			}

			var info = new WeaponSystemInfo { Name = GunName, Role = WeaponSystemRole.RoofGun, Weapon = gun.Weapon, FCS = fcs };
			var station = scripts.AddComponent<Station>();
			station.Info = info;
			station.Weapon = gun.Weapon;
			station.Fcs = fcs;
			station.Sight = sight;
			RemoteMg.Register(
				vehicle,
				weaponsManager,
				info,
				new[] { drum, cradle },
				new Component[] { drum, cradle, gun.Weapon, gun.Feed, fcs, sight.slot },
				station.Sights);
			ByWeapon[gun.Weapon] = station;
			UnityKeys.Prune(ByWeapon);

			// His seat on the roof behind the gun (turns with the pedestal) and the climb, both idle while Climbs is off.
			var seat = new GameObject("T80UM2 NSVT commander's seat").transform;
			seat.SetParent(frame, false);
			seat.SetPositionAndRotation(ToWorld(SeatPoint), root.rotation);
			var exposure = scripts.AddComponent<CrewFigure.Exposure>();
			// Switched on by the late setup, once the crew exists.
			exposure.enabled = false;
			exposure.Turret = turret;
			exposure.Seat = seat;
			exposure.SeatRest = Quaternion.Inverse(turret.rotation) * seat.rotation;
			// Climb times: my guess.
			exposure.Climb = new CrewClimb { OutSeconds = 3f, InSeconds = 2.5f, Linger = 3f };
			// Seated on the roof, leaning 20 deg to the gun with his head up to the К10-Т. Thighs forward along the roof (the
			// right one a little up over the raised plate and out), the left shin under the pedestal head and a little in (clear
			// of the magazine), the right one out beside the magazine, heels on the roof. Palms, not wrists, on the handles so the
			// fingers stop short of the magazine, and elbows out and back to keep the left forearm clear of the cupola's sight
			// box. My guess, fitted against the model.
			exposure.P = new CrewFigure.Pose
			{
				LeftGrip = grips[0],
				RightGrip = grips[1],
				Spine = new Vector3(0f, 0.94f, 0.34f),
				Neck = new Vector3(0f, 0.97f, 0.25f),
				LeftThigh = new Vector3(0.05f, 0.15f, 1f),
				RightThigh = new Vector3(0.25f, 0.3f, 1f),
				LeftShin = new Vector3(-0.2f, -0.2f, 0.98f),
				RightShin = new Vector3(0.2f, -0.15f, 1f),
				ElbowPole = new Vector3(0f, -0.3f, -0.5f),
				HandReach = 0.08f,
			};
			exposure.Tag = $"{Tag} NSVT commander";
			exposure.WantOut = () => Climbs && IsPlayers(station) && SelectedBy(station);
			exposure.Busy = () => gun.Feed != null && gun.Feed.Reloading;
			// First person through the К10-Т the camera sits in his head: the drawn figure off then.
			exposure.HideDrawn = () =>
			{
				var camera = Camera.main;
				return camera != null && Vector3.Distance(camera.transform.position, eye.position) < 0.6f;
			};
			station.Ex = exposure;
			scripts.AddComponent<Link>().Init(station);

			station.Part = part;
			foreach (var problem in problems) MelonLogger.Warning($"{Tag} NSVT: {problem}");
		}

		static Vector3 MuzzlePoint(RigMap.Mount mount, float pitch) =>
			mount.Pivot + Quaternion.AngleAxis(-pitch, Vector3.right) * (MuzzleWt - mount.Pivot);

		static void SetupSight(
			UsableOptic optic,
			FireControlSystem fcs,
			Transform eye,
			RemoteMg.Gun gun,
			Transform root,
			List<string> problems)
		{
			optic.name = "K10-T collimator sight";
			var slot = optic.slot;
			if (slot.gameObject != optic.gameObject) slot.gameObject.name = optic.name;
			SlotReparent?.SetValue(slot, eye);
			optic.FCS = fcs;
			optic.RegisterAtStart = true;
			optic.Mounts = new AimablePlatform[0];
			// On the cradle: it looks where the gun points.
			optic.Alignment = OpticAlignment.Fixed;
			optic.RotateElevation = false;
			optic.RotateAzimuth = false;
			optic.RegisteredAmmoTypes = new[] { gun.B32 };
			slot.ExclusiveWeapons = new[] { gun.Weapon };
			slot.SlotToUseOnDestroy = root
				.GetComponentsInChildren<CameraSlot>(true)
				.FirstOrDefault(candidate => candidate.name == "commander head");
			slot.IsExterior = false;
			slot.DefaultFov = SightFov;
			slot.OtherFovs = new float[0];
			slot.AllowFreeZoom = false;
			slot.fovAspect = false;
			slot.VisionType = NightVisionType.None;
			slot.LinkedNightSight = null;
			slot.NightSightAtNightOnly = false;
			// The donor gunsight's optic blur: an open collimator over the naked eye has none.
			slot.BaseBlur = 0f;
			// An open collimator: no scope mask.
			optic.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(slot);
			if (optic.reticleMesh != null && optic.reticleMesh.reticleSO != null)
			{
				var host = optic.reticleMesh.reticleSO;
				optic.reticleMesh.reticleSO = Reticles.Build("k10t_aa.jsonc", host, host, new Dictionary<string, AmmoCodexScriptable>());
				ReticleMeshes.ClearSmr(optic.reticleMesh);
				ReticleBuild.PrepareCopy(optic.reticleMesh);
				// GHPC's reticle (built, so the optic stays whole) is hidden: the window draws the reticle on the plate.
				optic.gameObject.AddComponent<K10Window.HideReticle>().Init(optic.reticleMesh, ReticleMeshes.Smr);
			}
			else problems.Add("the donor gunsight has no reticle mesh");
		}

		// Two frames after spawn (Conversion.LateSteps), after the sights: the crew exists, the feed has loaded its first box.
		internal static void LateSetup(Vehicle vehicle)
		{
			var station = vehicle.GetComponentInChildren<Station>(true);
			if (station == null) return;
			var weaponsManager = vehicle.GetComponent<WeaponsManager>();
			var gunner = weaponsManager.Weapons
				.Where(info => info.Role == WeaponSystemRole.MainGun || info.Role == WeaponSystemRole.Coaxial)
				.Select(info => info.Weapon)
				.Where(weapon => weapon != null)
				.ToArray();
			var fit = vehicle.GetComponentInChildren<Sights.Fitted>(true);
			// The gunner's sights for the gun and coax only: selecting the НСВТ changes to the К10-Т and back, as in the vanilla
			// M60A3 and T-64B.
			var slots = new[] { fit?.Day?.slot, fit?.Night?.slot, fit?.Agava?.slot }
				.Where(candidate => candidate != null)
				.Distinct()
				.ToArray();
			foreach (var slot in slots) slot.ExclusiveWeapons = gunner;
			station.Fcs.SetStabsActive(false);
			RemoteMg.LateSetup(vehicle);
			// The part belt: 450 is 4 x 100 + 50, so the box loaded at spawn is cut to the 50.
			var feed = station.Weapon.Feed;
			if (station.Part > 0 && FeedMain?.GetValue(feed) is Queue<AmmoType> queue)
			{
				while (queue.Count > 0 && queue.Count + (feed.AmmoTypeInBreech != null ? 1 : 0) > station.Part) queue.Dequeue();
			}
			var crew = vehicle.GetComponent<CrewManager>();
			station.Ex.Member = crew?.GetCrewMember(CrewPosition.Commander);
			DevLate(station);
			station.Ex.enabled = station.Ex.Member != null;
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevFitting(Vehicle vehicle);
		static partial void DevLate(Station station);
	}
}
