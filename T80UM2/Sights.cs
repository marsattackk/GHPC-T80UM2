using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using MelonLoader;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static partial class Sights
	{
		static string Tag => T80UM2Mod.FriendlyName;
		static readonly PortReticles Reticles = new PortReticles(typeof(Sights).Assembly, T80UM2Mod.FriendlyName);
		static readonly FieldInfo Smr = Reflect.Field(typeof(ReticleMesh), "SMR");
		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");

		const string DayFile = "1g46_day.jsonc", NightFile = "tpn4_night.jsonc", TknDayFile = "tkn4s_day.jsonc", TknNightFile = "tkn4s_night.jsonc";

		static readonly GunnerFcs.Settings Fcs = new GunnerFcs.Settings
		{
			LaserMin = 400f,
			LaserMax = 5000f,
			LineOfSightLimits = new Vector2(-15f, 20f),
			LineOfSightAzimuth = 8f,
			RangeStep = 100,
			DisplayRangeIncrement = 50,
			FireGateAngle = 0.1f,
			ManualMode = true,
			FireWhenLaid = () => T80UM2Mod.FireWhenLaid == null || T80UM2Mod.FireWhenLaid.Value,
		};

		internal const string Agava2 = "Agava-2", BuranPa = "Buran-PA";
		const float AgavaWide = 2.7f, AgavaNarrow = 1.35f;
		const int AgavaHeight = 288;
		const int AgavaWidth = 512;
		static readonly Vector2 AgavaMirror = new Vector2(-10f, 20f);
		const float AgavaAzimuth = 7.5f;
		const string AgavaFile = "agava2_monitor.jsonc", MonitorEdgesFile = "essa_monitor.jsonc";

		const float NightFov = 5.25f;
		static readonly Vector2 NightRange = new Vector2(0f, 2000f);

		const float TknDayFov = 7.8f;
		const float TknNightFov = 11.5f;
		static readonly Vector2 TknMirror = new Vector2(-10f, 20f);
		static readonly Vector2 TknRange = new Vector2(0f, 4000f);
		const float TknConverge = 1600f;
		internal static readonly Vector3 TknEye = new Vector3(0.591f, 2.240f, -1.030f);

		internal static void Apply(Vehicle vehicle)
		{
			if (vehicle == null) return;
			foreach (var f in vehicle.GetComponentsInChildren<HatchRig.Follower>(true))
				if (f.GetComponentsInChildren<Transform>(true).Any(t => t.name == "T80UM2_HATCH_COMMANDER")) { f.Shut = true; f.Pose(0f); }
			MelonCoroutines.Start(Later(vehicle));
		}

		static IEnumerator Later(Vehicle vehicle)
		{
			yield return null;
			yield return null;
			if (vehicle == null) yield break;
			try { Fit(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{Tag} sights: {e}"); }
		}

		internal sealed class Fitted : RemoteMg.Station
		{
			internal FireControlSystem Fcs;
			internal UsableOptic Day, Night, TknDay, TknNight;
			internal UsableOptic Agava;
			internal bool NightAtNightOnly;
			internal TargetDesignation.Link Cu;
			internal override IEnumerable<CameraSlot> Sights => new[] { TknDay?.slot }.Where(s => s != null);
			internal bool Dubl => (TknDay != null && TknDay.slot.IsActive) || (TknNight != null && TknNight.slot.IsActive);
		}

		static void Fit(Vehicle v)
		{
			var fcs = v.GetComponentsInChildren<FireControlSystem>(true).FirstOrDefault(f => f.name == "1G42 gunner's sight");
			var optics = v.GetComponentsInChildren<UsableOptic>(true);
			var day = fcs == null ? null : GuidanceBeam.DayOptic(fcs) ?? optics.FirstOrDefault(o => o.FCS == fcs && o.slot != null && o.slot.LinkedNightSight != null);
			if (day == null) { MelonLogger.Warning($"{Tag}: no 1G42 gunner's sight found; the T-80B's sights kept."); return; }
			if (v.GetComponentInChildren<Fitted>(true) != null) return;
			var nightSlot = day.slot.LinkedNightSight;
			var night = nightSlot == null ? null : nightSlot.PairedOptic ?? optics.FirstOrDefault(o => o.slot == nightSlot);
			var problems = new List<string>();
			var rounds = Rounds(v, problems);
			var fontHost = night != null ? night.reticleMesh.reticleSO : day.reticleMesh.reticleSO;

			var st = new GameObject("T80UM2 TKN-4S").AddComponent<Fitted>();
			st.transform.SetParent(v.transform, false);
			st.Fcs = fcs; st.Day = day; st.Night = night;
			if (night != null) CloneTkn(v, night, st, problems);
			var buran = night != null ? CopyBuran(v, night) : null;

			ApplyDay(day, rounds, problems, st);
			GunnerFcs.Apply(day, Fcs);
			GunnerFcs.LaserAim.Register(fcs, day, Fcs);
			if (night != null)
			{
				st.NightAtNightOnly = day.slot.NightSightAtNightOnly;
				string agava = ApplyAgava(day, night, problems);
				if (agava != null) st.Agava = night;
				else problems.Add("«Агава-2» not applied (thermal donor missing): the «Буран-ПА» only");
				ApplyNight(buran, fcs, rounds, fontHost, copy: true);
				buran.transform.SetParent(night.transform.parent, false);
				// A copy of an optic is made off (GHPC keeps it off out of view): switch it on or it never starts.
				buran.gameObject.SetActive(true);
				var holder = v.transform.Find("T80UM2 Buran-PA setup");
				if (holder != null) UnityEngine.Object.Destroy(holder.gameObject);
				st.Night = buran;
				st.gameObject.AddComponent<NightChoice>().Init(st, v);
			}
			if (st.TknDay != null) ConfigureTkn(v, fcs, day, st, rounds, fontHost, problems);
			var slots = v.GetComponentsInChildren<CameraSlot>(true);
			st.Cu = TargetDesignation.Register(v, fcs, new[] { slots.FirstOrDefault(s => s.name == "commander head"), slots.FirstOrDefault(s => s.name == "NODs") });
			foreach (var p in problems) MelonLogger.Warning($"{Tag} sights: {p}");
		}

		static Dictionary<string, AmmoCodexScriptable> Rounds(Vehicle v, List<string> problems)
		{
			var clips = v.GetComponent<LoadoutManager>()?.LoadedAmmoList?.AmmoClips;
			var loaded = (clips ?? new AmmoClipCodexScriptable[0]).Where(c => c != null && c.ClipType?.MinimalPattern != null && c.ClipType.MinimalPattern.Length > 0)
				.Select(c => c.ClipType.MinimalPattern[0]).Where(a => a != null && a.AmmoType != null).ToList();
			AmmoCodexScriptable Main(AmmoType.AmmoCategory cat) => loaded.FirstOrDefault(a => a.AmmoType.Guidance == AmmoType.GuidanceType.Unguided && a.AmmoType.Category == cat);
			var all = Resources.FindObjectsOfTypeAll<AmmoCodexScriptable>();
			AmmoCodexScriptable Named(string n) => all.FirstOrDefault(a => a.name == n);
			var wm = v.GetComponent<WeaponsManager>();
			var coax = wm?.Weapons?.FirstOrDefault(w => w.Role == WeaponSystemRole.Coaxial)?.Weapon?.Feed?.ReadyRack?.ClipTypes?.FirstOrDefault()?.MinimalPattern?.FirstOrDefault();
			var r = new Dictionary<string, AmmoCodexScriptable>
			{
				{ "apfsds", Main(AmmoType.AmmoCategory.Penetrator) ?? Named("ammo_3BM32") },
				{ "heat", Main(AmmoType.AmmoCategory.ShapedCharge) ?? Named("ammo_3BK18M") },
				{ "he", Main(AmmoType.AmmoCategory.Explosive) ?? Named("ammo_3OF26") },
				{ "coax", coax ?? Named("ammo_762x54R") },
			};
			r["b32"] = Named("ammo_B32") ?? r["coax"];
			foreach (var kv in r.Where(kv => kv.Value == null)) problems.Add($"no '{kv.Key}' round for the reticles' scales");
			return r;
		}

		static void ApplyDay(UsableOptic day, Dictionary<string, AmmoCodexScriptable> rounds, List<string> problems, Fitted st)
		{
			var spec = Reticles.Spec(DayFile);
			var host = day.reticleMesh.reticleSO;
			day.reticleMesh.reticleSO = Reticles.Build(DayFile, host, host, rounds);
			Smr.SetValue(day.reticleMesh, null);
			day.reticleMesh.Load();
			var fovs = spec.Nums("ZoomFovs").Select(f => (float)f).ToArray();
			day.slot.DefaultFov = fovs[0];
			day.slot.OtherFovs = fovs.Skip(1).ToArray();
			day.slot.AllowFreeZoom = true;
			day.slot.fovAspect = true;       // horizontal fields, as the 1G42's: the eyepiece circle is the real field
			SightMotion.Stabilised(day.slot);
			var oldCanvas = day.transform.Find("1G42 Canvas");
			if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
			var overlay = ReticleBuild.BuildOverlay(spec, day.transform, (float)spec.Fov, problems);
			SightReadout.Attach(day, overlay, new SightReadout.Settings
			{
				Range = "range", RangeQuantize = 5, RangePrefix = "<mspace=0.9em>", RangeSuffix = "</mspace>",
				Ready = "ready",
				Ammo = "ammo", AmmoText = SightReadout.Glyph7,
				Lit = new (string, Func<bool>)[] { ("override", () => st.Dubl || (st.Cu != null && st.Cu.Held) || (st.Fcs != null && st.Fcs.IsOverridingNow)) },
			}, problems);
			day.OverridingObject = null;
		}

		static void ApplyNight(UsableOptic night, FireControlSystem fcs, Dictionary<string, AmmoCodexScriptable> rounds, ReticleSO fontHost, bool copy = false)
		{
			var host = night.reticleMesh.reticleSO;
			night.reticleMesh.reticleSO = Reticles.Build(NightFile, host, fontHost, rounds);
			Smr.SetValue(night.reticleMesh, null);
			if (copy) ReticleBuild.PrepareCopy(night.reticleMesh);
			else night.reticleMesh.Load();
			night.name = "TPN-4 Buran-PA night sight";
			var s = night.slot;
			s.DefaultFov = NightFov;
			s.OtherFovs = new float[0];
			s.AllowFreeZoom = false;
			s.fovAspect = false;
			night.OverrideRangeLimits = NightRange;
			night.OverrideRangeStep = 100;
			SightMotion.Stabilised(s, keepBlur: true);
			ComputerOff.Register(fcs, s, ComputerOff.Mode.Manual, $"{Tag} ТПН-4");
		}

		static string ApplyAgava(UsableOptic day, UsableOptic night, List<string> problems)
		{
			string bound = "";
			bool ok = ThermalSight.Apply(day, night, new ThermalSight.Settings
			{
				Tag = Tag,
				Name = Agava2,
				WideFov = AgavaWide,
				OtherFovs = new[] { AgavaNarrow },
				Width = AgavaWidth,
				Height = AgavaHeight,
				ElevationLimits = AgavaMirror,
				Reticle = o => { if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false); },
				Readout = o => bound = AgavaMonitor(o, problems),
				Monitor = true,
				Crt = true,
			});
			if (!ok) return null;
			GunnerFcs.AimClamp.SightAzimuth(night, AgavaAzimuth);
			night.name = "Agava-2 thermal sight";
			return $"«Агава-2» (inferred) {AgavaWide} / {AgavaNarrow} deg, {AgavaWidth}x{AgavaHeight}, mirror {AgavaMirror.x:0} / +{AgavaMirror.y:0} deg ±{AgavaAzimuth}, on the 1Г46's line; monitor {bound}";
		}

		static string AgavaMonitor(UsableOptic o, List<string> problems)
		{
			var spec = Reticles.Spec(AgavaFile);
			var marks = ReticleBuild.BuildOverlay(spec, o.transform, (float)spec.Fov, problems);
			var edges = Reticles.Spec(MonitorEdgesFile);
			ReticleBuild.BuildOverlay(edges, o.transform, (float)edges.Fov, problems);
			return SightReadout.Attach(o, marks, new SightReadout.Settings
			{
				Range = "range", RangePrefix = "<mspace=0.7em>", RangeSuffix = "</mspace>",
				LaserLamp = "laser",
				Ready = "ready",
			}, problems);
		}

		static UsableOptic CopyBuran(Vehicle v, UsableOptic tpn)
		{
			var holder = new GameObject("T80UM2 Buran-PA setup");
			holder.SetActive(false);
			holder.transform.SetParent(v.transform, false);
			var go = UnityEngine.Object.Instantiate(tpn.gameObject, holder.transform, false);
			foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
			return go.GetComponent<UsableOptic>();
		}

		internal sealed class NightChoice : MonoBehaviour
		{
			Fitted _st;
			Vehicle _v;
			internal string Linked;
			internal static string Want => T80UM2Mod.NightSight != null && T80UM2Mod.NightSight.Value == BuranPa ? BuranPa : Agava2;

			internal void Init(Fitted st, Vehicle v)
			{
				_st = st;
				_v = v;
				Link(Want);
			}

			void Update()
			{
				if (_st != null && Want != Linked) Link(Want);
			}

			void Link(string want)
			{
				var day = _st.Day?.slot;
				var to = want == BuranPa || _st.Agava == null ? _st.Night : _st.Agava;
				if (day == null || to == null) return;
				var active = CameraSlot.ActiveInstance;
				if (active != null && (active == _st.Night?.slot || active == _st.Agava?.slot)) return;
				// CameraSlot.Awake links a day slot's night sight once and never again, so every link is set by hand.
				day.LinkedNightSight = to.slot;
				to.slot.IsLinkedNightSight = true;
				to.slot.LinkedDaySight = day;
				day.NightSightAtNightOnly = to == _st.Agava ? false : _st.NightAtNightOnly;
				// VisibilityManager goes off the unit's night vision type for AI spotting, not the slot's.
				ThermalSight.SetUnitNightVision(_v, to.slot);
				Linked = want;
			}
		}

		static void CloneTkn(Vehicle v, UsableOptic tpn, Fitted st, List<string> problems)
		{
			var turret = v.AimablePlatforms?.FirstOrDefault(p => p != null && p.Name == "Turret traverse")?.Transform;
			if (turret == null) { problems.Add("no turret traverse drive: no ТКН-4С"); return; }
			var holder = new GameObject("T80UM2 TKN-4S setup");
			holder.SetActive(false);
			holder.transform.SetParent(v.transform, false);
			UsableOptic Copy() { var go = UnityEngine.Object.Instantiate(tpn.gameObject, holder.transform, false); foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c); return go.GetComponent<UsableOptic>(); }
			st.TknDay = Copy();
			st.TknNight = Copy();
			// A night sight's reticle mesh draws light strokes in a day picture.
			var dayMesh = st.Day != null ? st.Day.reticleMesh : null;
			if (dayMesh != null && st.TknDay.reticleMesh != null)
			{
				var old = st.TknDay.reticleMesh.gameObject;
				var go = UnityEngine.Object.Instantiate(dayMesh.gameObject, st.TknDay.transform, false);
				go.name = old.name;
				st.TknDay.reticleMesh = go.GetComponent<ReticleMesh>();
				UnityEngine.Object.DestroyImmediate(old);
			}
			else problems.Add("ТКН-4С day channel keeps the night sight's reticle mesh (no 1G42 reticle mesh found)");
			var head = st.transform;
			head.SetParent(turret, false);
			head.position = v.transform.TransformPoint(TknEye);
			head.rotation = turret.rotation;
		}

		static void ConfigureTkn(Vehicle v, FireControlSystem fcs, UsableOptic gunnerDay, Fitted st, Dictionary<string, AmmoCodexScriptable> rounds, ReticleSO fontHost, List<string> problems)
		{
			var head = st.transform;
			var wm = v.GetComponent<WeaponsManager>();
			var weapons = wm.Weapons.Where(w => w.Role == WeaponSystemRole.MainGun || w.Role == WeaponSystemRole.Coaxial).Select(w => w.Weapon).Where(w => w != null).ToArray();
			var day = st.TknDay; var night = st.TknNight;
			// Night channel first: a day slot marks its linked night sight when it wakes.
			foreach (var o in new[] { night, day })
			{
				o.FCS = fcs;
				o.RegisterAtStart = false;
				o.Alignment = OpticAlignment.BoresightStabilized;
				o.RotateElevation = true;
				o.RotateAzimuth = true;
				o.CantCorrect = false;
				o.ForceHorizontalReticleAlign = false;
				o.Mounts = new AimablePlatform[0];
				o.LocalElevationLimits = TknMirror;
				o.LocalAzimuthLimits = Vector2.zero;
				o.OverrideRangeLimits = TknRange;
				o.OverrideRangeStep = 100;
				var s = o.slot;
				SlotReparent?.SetValue(s, head);
				s.AllowFreeZoom = false;
				s.OtherFovs = new float[0];
				s.fovAspect = false;
				s.ExclusiveWeapons = weapons;
				s.ExclusiveItems = new GameObject[0];   // the TPN-3-49's shutter and cover come over with the copy
				s.ZeroScaleItems = new Transform[0];
				s.SlotToUseOnDestroy = gunnerDay.slot;
				s.IsExterior = false;
				s.DirectAttachOnly = false;
				var file = o == night ? TknNightFile : TknDayFile;
				o.reticleMesh.reticleSO = Reticles.Build(file, o.reticleMesh.reticleSO, fontHost, rounds);
				Smr.SetValue(o.reticleMesh, null);
				ReticleBuild.PrepareCopy(o.reticleMesh);
				o.gameObject.AddComponent<ReticleRangeLine>().Init(o, fcs, () => fcs.CurrentAmmoType);
				ComputerOff.Register(fcs, s, ComputerOff.Mode.HandRange, $"{Tag} Дубль");
				GunnerFcs.Converge.Register(fcs, s, TknConverge);
				GunnerFcs.AimClamp.Register(fcs, o);
				o.transform.SetParent(head, false);
				o.transform.localPosition = Vector3.zero;
				o.transform.localRotation = Quaternion.identity;
			}
			day.name = "TKN-4S sight";
			day.slot.DefaultFov = TknDayFov;
			day.slot.VisionType = NightVisionType.None;
			day.slot.LinkedNightSight = night.slot;
			day.slot.NightSightAtNightOnly = false;
			day.slot.BaseBlur = gunnerDay.slot.BaseBlur;
			day.slot.VibrationPreBlur = gunnerDay.slot.VibrationPreBlur;
			if (day.post != null && gunnerDay.post != null) { day.post.sharedProfile = gunnerDay.post.sharedProfile; day.post.priority = gunnerDay.post.priority; day.post.weight = gunnerDay.post.weight; }
			night.name = "TKN-4S night channel";
			night.slot.DefaultFov = TknNightFov;
			night.slot.VisionType = NightVisionType.Intensifier;
			night.slot.LinkedNightSight = null;
			SightMotion.Stabilised(day.slot);
			SightMotion.Stabilised(night.slot, keepBlur: true);
			var tknSpec = Reticles.Spec(TknDayFile);
			var strip = ReticleBuild.BuildOverlay(tknSpec, day.transform, (float)tknSpec.Fov, problems);
			SightReadout.Attach(day, strip, new SightReadout.Settings { Ready = "ready indicator", Ammo = "ammo", AmmoText = SightReadout.Glyph7 }, problems);
			foreach (var o in new[] { night, day }) o.gameObject.SetActive(true);
			var holder = v.transform.Find("T80UM2 TKN-4S setup");
			if (holder != null) UnityEngine.Object.Destroy(holder.gameObject);
			RemoteMg.LateSetup(v);
		}
	}
}
