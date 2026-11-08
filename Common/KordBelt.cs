using System.Collections.Generic;
using System.Linq;
using GHPC.Weapons;
using UnityEngine;

namespace T80UM2.Common
{
	internal static class KordBelt
	{
		internal sealed class Settings
		{
			internal string Prefix = "PORT_";
			internal string MeshName = "Kord";
			internal int SpareRounds = 24;
			internal int MaxCases = 300;
			internal string BeltObject => Prefix + "KORD_BELT";
		}

		internal static Belt Build(Transform carrier, Transform cradle, Settings set, Vector3 feed, Vector3 feedDir, Vector3 mouth, Vector3 mouthDir, Vector3 cartridgeAxis, float pitch,
			BeltRound.Data round = null, Material roundMat = null)
		{
			var go = new GameObject(set.BeltObject);
			go.transform.SetParent(carrier, false);
			go.layer = carrier.gameObject.layer;
			var belt = go.AddComponent<Belt>();
			belt.Set = set;
			belt.Cupola = carrier; belt.Cradle = cradle; belt.Pitch = pitch; belt.FeedPitch = pitch;
			feedDir = feedDir.normalized; mouthDir = mouthDir.normalized;
			belt.FeedOnCradle = cradle.InverseTransformPoint(carrier.TransformPoint(feed));
			belt.FeedDirOnCradle = cradle.InverseTransformDirection(carrier.TransformDirection(feedDir));
			belt.FeedUpOnCradle = cradle.InverseTransformDirection(carrier.up);
			belt.Mouth = mouth; belt.MouthDir = mouthDir;
			var rest = belt.Curve(feed, feedDir, Vector3.up, mouth, mouthDir, Vector3.up);
			var rot = BeltRound.CartridgeRot(rest, cartridgeAxis);
			belt.RestS = new[] { 0f, Mathf.Max(0.01f, rest.Length) };
			belt.CartPos = new[] { Vector3.zero, Vector3.zero };
			belt.CartRot = new[] { rot, rot };
			Mesh mesh; Material[] mats;
			if (round != null && roundMat != null)
			{
				mesh = BeltRound.RoundMesh(round, set.MeshName + " belt round (modelled)");
				var (cv, cn, cuv, ctg, ctris, cstarts) = BeltRound.CartridgeArrays(mesh, round);
				belt.CaseMesh = KordBrass.CaseMesh(cv, cn, cuv, ctg, ctris, cstarts, set.MeshName + " spent case");
				mats = new[] { roundMat };
			}
			else
			{
				mesh = Round127(set.MeshName + " belt round", pitch);
				belt.CaseMesh = KordBrass.Case127(set.MeshName + " spent case");
				mats = new[] { KordBrass.Brass(set.MeshName + " case brass"), Metal(set.MeshName + " bullet jacket", new Color(0.55f, 0.36f, 0.22f), 0.8f, 0.5f), Metal(set.MeshName + " link steel", new Color(0.13f, 0.13f, 0.12f), 0.6f, 0.3f) };
			}
			belt.TypicalMesh = mesh;
			int n = Mathf.CeilToInt(rest.Length / pitch) + 1 + set.SpareRounds;
			var parts = new List<(Transform t, float s)>();
			for (int i = 0; i < n; i++)
			{
				var r = new GameObject($"{set.BeltObject}_round_{i:00}");
				r.layer = go.layer;
				r.transform.SetParent(go.transform, false);
				r.AddComponent<MeshFilter>().sharedMesh = mesh;
				r.AddComponent<MeshRenderer>().sharedMaterials = mats;
				parts.Add((r.transform, i * pitch));
			}
			belt.Rounds = parts.ToArray();
			belt.Place();
			return belt;
		}

		static Material Metal(string name, Color c, float metallic, float gloss)
		{
			var sh = Shader.Find("Standard (FLIR)") ?? Shader.Find("Standard");
			var mat = new Material(sh) { name = name, color = c };
			if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
			if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", gloss);
			return mat;
		}

		internal static Mesh Round127(string name, float pitch)
		{
			const float linkZ = 0.030f;
			var v = new List<Vector3>(); var nn = new List<Vector3>();
			var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
			void Lathe((float z, float r)[] prof, int sub, int sides)
			{
				for (int k = 0; k + 1 < prof.Length; k++)
				{
					var (z0, r0) = prof[k]; var (z1, r1) = prof[k + 1];
					int start = v.Count;
					for (int s = 0; s <= sides; s++)
					{
						float a = s * Mathf.PI * 2f / sides;
						var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
						var nrm = r0 == 0f && r1 > 0f && z0 == z1 ? Vector3.back : (dir * (z1 - z0) + Vector3.forward * (r0 - r1)).normalized;
						v.Add(dir * r0 + Vector3.forward * (z0 - linkZ)); nn.Add(nrm);
						v.Add(dir * r1 + Vector3.forward * (z1 - linkZ)); nn.Add(nrm);
					}
					for (int s = 0; s < sides; s++) { int i = start + s * 2; subs[sub].AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 }); }
				}
			}
			void Box(Vector3 min, Vector3 max, int sub)
			{
				var c = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
				var mid = (min + max) * 0.5f; var half = (max - min) * 0.5f;
				foreach (var f in c)
				{
					var u = f.x != 0f ? Vector3.up : Vector3.right; var w = Vector3.Cross(f, u);
					int i = v.Count;
					foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
					{
						var p = f + u * a + w * b;
						v.Add(mid + Vector3.Scale(p, half) - Vector3.forward * 0f); nn.Add(f);
					}
					subs[sub].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
				}
			}
			Lathe(new[] { (0f, 0f), (0f, 0.010875f), (0.080f, 0.0099f), (0.086f, 0.0065f), (0.108f, 0.0065f) }, 0, 12);
			Lathe(new[] { (0.108f, 0.0065f), (0.118f, 0.0065f), (0.134f, 0.0045f), (0.147f, 0.0008f), (0.147f, 0f) }, 1, 12);
			Lathe(new[] { (0.021f, 0.0118f), (0.039f, 0.0118f), (0.039f, 0.0109f), (0.021f, 0.0109f), (0.021f, 0.0118f) }, 2, 12);
			Box(new Vector3(0f, -0.0125f, 0.024f - linkZ) + new Vector3(-0.002f, 0f, 0f), new Vector3(pitch + 0.002f, -0.0110f, 0.036f - linkZ), 2);
			var m = new Mesh { name = name };
			m.SetVertices(v); m.SetNormals(nn);
			m.subMeshCount = 3;
			for (int s = 0; s < 3; s++) m.SetTriangles(subs[s], s);
			m.RecalculateBounds();
			return m;
		}

		[DefaultExecutionOrder(20)]
		internal class Belt : MonoBehaviour
		{
			// Half is a guess, the source only gives the order of the motions.
			const float FeedPart = 0.5f;

			internal Settings Set;
			internal Transform Cupola, Cradle;
			internal Vector3 FeedOnCradle, FeedDirOnCradle, FeedUpOnCradle;
			internal Vector3 Mouth, MouthDir;
			internal float Pitch;
			internal (Transform t, float s)[] Rounds;
			internal float[] RestS;
			internal Vector3[] CartPos;
			internal Quaternion[] CartRot;

			(Vector3 p, Quaternion r) Cartridge(float s)
			{
				int n = RestS.Length;
				if (s <= RestS[0]) return (CartPos[0], CartRot[0]);
				if (s >= RestS[n - 1]) return (CartPos[n - 1], CartRot[n - 1]);
				int i = 0;
				while (i < n - 2 && RestS[i + 1] < s) i++;
				float u = (s - RestS[i]) / Mathf.Max(1e-6f, RestS[i + 1] - RestS[i]);
				return (Vector3.Lerp(CartPos[i], CartPos[i + 1], u), Quaternion.Slerp(CartRot[i], CartRot[i + 1], u));
			}

			internal Mesh CaseMesh;
			internal Mesh TypicalMesh;
			internal float FeedPitch;
			float Drop => Mathf.Max(0.02f, FeedOnCradle.y);
			Renderer[] _renderers;
			WeaponSystem _weapon;
			AmmoFeed _feed;
			float _cycle, _fed, _target, _fedSince;
			int _head;

			internal BeltCurve Curve(Vector3 feed, Vector3 feedDir, Vector3 feedUp, Vector3 mouth, Vector3 mouthDir, Vector3 mouthUp) =>
				new BeltCurve(feed, feedDir, feedUp, mouth, mouthDir, mouthUp);

			internal void Attach(WeaponSystem weapon, float cycle)
			{
				_weapon = weapon; _feed = weapon.Feed; _cycle = cycle;
				_weapon.Fired += OnFired;
			}

			void OnDestroy() { if (_weapon != null) _weapon.Fired -= OnFired; }

			void OnFired(AmmoType ammo, LiveRound round) { _cycle = Cycle(_weapon, _cycle); _target += FeedPitch; }

			internal static float Cycle(WeaponSystem w, float fallback)
			{
				float c = w != null && RemoteMg.WeaponCycle != null ? (float)RemoteMg.WeaponCycle.GetValue(w) : 0f;
				return c > 0f ? c : fallback;
			}

			void LateUpdate() => Tick();

			void Tick()
			{
				if (_target > _fed)
				{
					float was = _fed;
					_fed = Mathf.MoveTowards(_fed, _target, FeedPitch / Mathf.Max(0.01f, FeedPart * _cycle) * Time.deltaTime);
					_fedSince = Mathf.Min(1f, _fedSince + _fed - was);
					while (_fed - _head * FeedPitch >= FeedPitch - 1e-5f)
					{
						var mf = Rounds[_head % Rounds.Length].t.GetComponent<MeshFilter>();
						if (TypicalMesh != null && mf.sharedMesh != TypicalMesh) mf.sharedMesh = TypicalMesh;
						_head++;
					}
					if (_head >= Rounds.Length) { _head -= Rounds.Length; _fed -= Rounds.Length * FeedPitch; _target -= Rounds.Length * FeedPitch; }
				}
				Place();
			}

			internal void Place()
			{
				if (Cupola == null || Cradle == null || Rounds == null) return;
				var feed = Cupola.InverseTransformPoint(Cradle.TransformPoint(FeedOnCradle));
				var feedDir = Cupola.InverseTransformDirection(Cradle.TransformDirection(FeedDirOnCradle));
				var feedUp = Cupola.InverseTransformDirection(Cradle.TransformDirection(FeedUpOnCradle));
				if (_renderers == null) _renderers = Rounds.Select(r => r.t.GetComponent<Renderer>()).ToArray();
				int left = _feed != null ? _feed.CurrentClipRemainingCount - (_feed.AmmoTypeInBreech != null ? 1 : 0) : int.MaxValue;
				if (_placed && _fed == _lastFed && _head == _lastHead && left == _lastLeft && feed == _lastFeed && feedDir == _lastFeedDir && feedUp == _lastFeedUp) return;
				if (_placed && _target <= _fed && !Seen()) return;
				_placed = true; _lastFed = _fed; _lastHead = _head; _lastLeft = left; _lastFeed = feed; _lastFeedDir = feedDir; _lastFeedUp = feedUp;
				var c = _curve ?? (_curve = new BeltCurve(feed, feedDir, feedUp, Mouth, MouthDir, Vector3.up));
				c.Set(feed, feedDir, feedUp, Mouth, MouthDir, Vector3.up);
				int nAll = Rounds.Length;
				float frac = _fed - _head * FeedPitch;
				float fade = Mathf.Clamp01(1f - _fedSince / (2f * FeedPitch));
				for (int i = 0; i < nAll; i++)
				{
					var (t, s0) = Rounds[i];
					int q = ((i - _head) % nAll + nAll) % nAll;
					float s = q * FeedPitch - frac + (s0 - i * FeedPitch) * fade;
					// Renderer, not the object: the burn effect and heat source collect active renderers once, at Start.
					bool show = s <= c.Length + 0.3f * Pitch && s > -0.95f * FeedPitch && q < left;
					if (_renderers[i].enabled != show) _renderers[i].enabled = show;
					if (!show) continue;
					var f = c.Frame(Mathf.Max(0f, s));
					var (cp, cr) = Cartridge(Mathf.Max(0f, s));
					var p = f.MultiplyPoint3x4(cp);
					if (s < 0f) p -= feedUp.normalized * (Mathf.Clamp01(-s / FeedPitch) * Drop);
					t.localPosition = p;
					t.localRotation = f.rotation * cr;
				}
			}

			BeltCurve _curve;
			bool _placed;
			float _lastFed;
			int _lastHead, _lastLeft;
			Vector3 _lastFeed, _lastFeedDir, _lastFeedUp;

			bool Seen()
			{
				foreach (var r in _renderers) if (r != null && r.enabled && r.isVisible) return true;
				return false;
			}
		}
	}
}
