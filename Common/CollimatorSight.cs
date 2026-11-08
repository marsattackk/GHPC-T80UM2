using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T80UM2.Common
{
	public sealed class CollimatorPreset
	{
		public string Name;
		public Vector3 PlateUp, PlateNormal;
		public float PlateW, PlateH, EyeRelief;
		public Func<float, float, float> Reticle;
		public float LineW = 1.2f, ShadowW = 1.4f;
		public Color Line = new Color(1f, 0.97f, 0.9f, 1f), Shadow = new Color(0f, 0f, 0f, 0.42f);
		public Color Filter = new Color(0.12f, 0.1f, 0.06f, 0.12f);
		public float Bezel = 0.0016f, BezelDepth = 0.003f, ClampH = 0.005f;
		public float BodyW = 0.046f, BodyH = 0.03f, BodyL = 0.075f;
		public Vector3 BracketFoot;
		public string Inferred;
	}

	public static class CollimatorSight
	{
		const float MilRad = 2f * Mathf.PI / 6000f;

		public static CollimatorPreset K10T()
		{
			const float Div = 10f, TickShort = 4f, TickLong = 8f, RingIn = 80f, RingOut = 120f;
			return new CollimatorPreset
			{
				Name = "K10-T",
				PlateUp = new Vector3(0f, 0.73f, -0.69f).normalized, PlateNormal = new Vector3(0f, -0.69f, -0.73f).normalized,
				PlateW = 0.043f, PlateH = 0.062f, EyeRelief = 0.155f,
				BracketFoot = new Vector3(0.012f, -0.23f, 0.06f),
				Inferred = "the plate's outline, line width / colour / shadow, the filter's tint, the body's 4.6 x 3.0 x 7.5 cm under the plate (the data sheet's 120 x 82 x 90 mm is the whole sight) and the bracket post's run to the mount",
				Reticle = (x, y) =>
				{
					float rad = Mathf.Sqrt(x * x + y * y);
					float d = Mathf.Min(Mathf.Abs(rad - RingOut), Mathf.Abs(rad - RingIn));
					d = Mathf.Min(d, Mathf.Min(Mathf.Abs(x), Mathf.Abs(y)));
					for (int k = 1; k <= 4; k++)
					{
						float at = k * Div, half = (k % 2 == 0 ? TickLong : TickShort) / 2f;
						if (Mathf.Abs(y) <= half) d = Mathf.Min(d, Mathf.Abs(Mathf.Abs(x) - at));
						if (Mathf.Abs(x) <= half) d = Mathf.Min(d, Mathf.Abs(Mathf.Abs(y) - at));
					}
					if (rad >= RingOut && rad <= RingOut + 6f)
					{
						float ang = Mathf.Abs(Mathf.Atan2(y, x)) * Mathf.Rad2Deg;
						d = Mathf.Min(d, Mathf.Min(Mathf.Abs(ang - 45f), Mathf.Abs(ang - 135f)) * Mathf.Deg2Rad * rad);
					}
					return d;
				},
			};
		}

		public sealed class Built
		{
			public Transform Root;
			public GameObject Body;
			public GameObject Filter;
		}

		public static Built Build(Transform parent, Vector3 reflector, Quaternion axisRot, Quaternion plateTilt, CollimatorPreset p, Material paint, Vector2 paintUv, Shader transparent, List<string> notes)
		{
			var b = new Built();
			var go = new GameObject("K10-T sight (" + p.Name + ")");
			go.transform.SetParent(parent, false);
			go.transform.SetPositionAndRotation(reflector, axisRot);
			b.Root = go.transform;
			var plateRot = plateTilt * Quaternion.LookRotation(p.PlateNormal, p.PlateUp);
			Vector3 up = plateTilt * p.PlateUp, nrm = plateTilt * p.PlateNormal, right = Vector3.Cross(nrm, up).normalized;
			if (Vector3.Dot(right, Vector3.right) < 0f) right = -right;

			var (rtex, rmin, rmax) = ReticleTexture(p, up, nrm, right);
			Quad(go.transform, "reticle", transparent, rtex, 3002, Vector3.zero, Quaternion.identity, rmin, rmax);
			var pmin = new Vector2(-p.PlateW / 2f, -p.PlateH / 2f); var pmax = -pmin;
			Quad(go.transform, "glass", transparent, PlateTexture(p, false), 3000, nrm * 0.0003f, plateRot, pmin, pmax);
			b.Filter = Quad(go.transform, "light filter", transparent, PlateTexture(p, true), 3001, nrm * 0.0006f, plateRot, pmin, pmax);

			var mesh = BodyMesh(p, up, nrm, right);
			var body = new GameObject("K10-T body");
			body.transform.SetParent(go.transform, false);
			body.AddComponent<MeshFilter>().sharedMesh = mesh;
			var mr = body.AddComponent<MeshRenderer>();
			if (paint != null)
			{
				var uv = new Vector2[mesh.vertexCount];
				for (int i = 0; i < uv.Length; i++) uv[i] = paintUv;
				mesh.uv = uv;
				mesh.RecalculateTangents();
				mr.sharedMaterial = paint;
			}
			else mr.sharedMaterial = new Material(Shader.Find("Standard") ?? transparent) { color = new Color(0.17f, 0.19f, 0.15f) };
			b.Body = body;
			notes?.Add($"{p.Name} sight: reticle square to the axis at {p.EyeRelief * 1000f:0} mm ({rtex.width} x {rtex.height}, mipmapped, lines {p.LineW:0.0} mil with a {p.ShadowW:0.0} mil soft shadow), plate {p.PlateW * 100f:0.0} x {p.PlateH * 100f:0.0} cm, bezel + body {p.BodyW * 100f:0.0} x {p.BodyH * 100f:0.0} x {p.BodyL * 100f:0.0} cm + bracket ({mesh.vertexCount} verts, {(paint != null ? "the mount's paint" : "plain grey")}); INFERRED: {p.Inferred}");
			return b;
		}

		static GameObject Quad(Transform parent, string name, Shader shader, Texture2D tex, int queue, Vector3 pos, Quaternion rot, Vector2 min, Vector2 max)
		{
			var go = new GameObject("K10-T " + name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = pos; go.transform.localRotation = rot;
			var m = new Mesh { name = "K10-T " + name };
			m.vertices = new[] { new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f), new Vector3(max.x, max.y, 0f), new Vector3(min.x, max.y, 0f) };
			m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
			m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
			m.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
			m.RecalculateBounds();
			go.AddComponent<MeshFilter>().sharedMesh = m;
			var mr = go.AddComponent<MeshRenderer>();
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
			mr.sharedMaterial = new Material(shader) { mainTexture = tex, renderQueue = queue, name = "K10-T " + name };
			return go;
		}

		static float Inside(CollimatorPreset p, float x, float v)
		{
			float r = p.PlateW / 2f, cy = p.PlateH / 2f - r;
			return v > cy ? r - Mathf.Sqrt(x * x + (v - cy) * (v - cy)) : Mathf.Min(r - Mathf.Abs(x), v + p.PlateH / 2f);
		}

		static bool OnPlate(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right, float qx, float qy, out float u, out float v)
		{
			var e = new Vector3(0f, 0f, -p.EyeRelief); var dir = new Vector3(qx, qy, p.EyeRelief);
			float den = Vector3.Dot(nrm, dir);
			u = v = 0f;
			if (Mathf.Abs(den) < 1e-6f) return false;
			var hit = e + dir * (-Vector3.Dot(nrm, e) / den);
			u = Vector3.Dot(hit, right); v = Vector3.Dot(hit, up);
			return true;
		}

		static (Texture2D, Vector2, Vector2) ReticleTexture(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right)
		{
			Vector2 mn = new Vector2(1f, 1f), mx = -mn;
			for (int i = 0; i <= 64; i++)
			{
				float t = i / 64f;
				foreach (var (x, v) in new[] { (Mathf.Lerp(-p.PlateW / 2f, p.PlateW / 2f, t), p.PlateH / 2f), (Mathf.Lerp(-p.PlateW / 2f, p.PlateW / 2f, t), -p.PlateH / 2f), (-p.PlateW / 2f, Mathf.Lerp(-p.PlateH / 2f, p.PlateH / 2f, t)), (p.PlateW / 2f, Mathf.Lerp(-p.PlateH / 2f, p.PlateH / 2f, t)) })
				{
					var w = right * x + up * v;
					float s = p.EyeRelief / (p.EyeRelief + w.z);
					var q = new Vector2(w.x * s, w.y * s);
					mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q);
				}
			}
			mn -= Vector2.one * 0.001f; mx += Vector2.one * 0.001f;
			const int W = 1024;
			int H = Mathf.RoundToInt(W * (mx.y - mn.y) / (mx.x - mn.x));
			float tx = (mx.x - mn.x) / W;
			float milPx = tx / p.EyeRelief / MilRad;
			var pix = new Color[W * H];
			float lw = p.LineW / 2f, sw = lw + p.ShadowW;
			Parallel.For(0, H, j =>
			{
				for (int i = 0; i < W; i++)
				{
					float qx = mn.x + (i + 0.5f) * tx, qy = mn.y + (j + 0.5f) * tx;
					Color c = Color.clear;
					if (OnPlate(p, up, nrm, right, qx, qy, out float u, out float v))
					{
						float inside = Inside(p, u, v) - p.Bezel * 0.3f;
						float cover = Mathf.Clamp01(inside / (tx * 1.4f) + 0.5f);
						if (cover > 0f)
						{
							float d = p.Reticle(Mathf.Atan2(qx, p.EyeRelief) / MilRad, Mathf.Atan2(qy, p.EyeRelief) / MilRad);
							float a = Mathf.Clamp01((lw - d) / milPx + 0.5f);
							float s = Mathf.Clamp01((sw - d) / p.ShadowW); s = s * s * (3f - 2f * s);
							var sh = p.Shadow; sh.a *= s;
							c = Color.Lerp(sh, p.Line, a);
							c.a = Mathf.Lerp(sh.a, 1f, a) * cover;
						}
					}
					pix[j * W + i] = c;
				}
			});
			var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "K10-T reticle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
			tex.SetPixels(pix);
			tex.Apply(true, true);
			return (tex, mn, mx);
		}

		static readonly Color GlassTop = new Color(0.8f, 0.87f, 1f, 0.06f), GlassBottom = new Color(0.6f, 0.66f, 0.74f, 0.015f);
		static Texture2D PlateTexture(CollimatorPreset p, bool filter)
		{
			const int W = 256;
			int H = Mathf.RoundToInt(W * p.PlateH / p.PlateW);
			float px = p.PlateW / W;
			var pix = new Color[W * H];
			Parallel.For(0, H, j =>
			{
				for (int i = 0; i < W; i++)
				{
					float x = (i + 0.5f) * px - p.PlateW / 2f, v = (j + 0.5f) * px - p.PlateH / 2f;
					float cover = Mathf.Clamp01(Inside(p, x, v) / px + 0.5f);
					var c = filter ? p.Filter : Color.Lerp(GlassBottom, GlassTop, Mathf.Clamp01((v + p.PlateH / 2f) / p.PlateH));
					c.a *= cover;
					pix[j * W + i] = c;
				}
			});
			var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "K10-T " + (filter ? "filter" : "glass"), wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
			tex.SetPixels(pix);
			tex.Apply(true, true);
			return tex;
		}

		sealed class MB
		{
			public readonly List<Vector3> V = new List<Vector3>(); public readonly List<int> T = new List<int>();
			public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
			{
				int o = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
				T.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
			}

			public void Tri(Vector3 a, Vector3 b, Vector3 c) { int o = V.Count; V.Add(a); V.Add(b); V.Add(c); T.AddRange(new[] { o, o + 1, o + 2 }); }

			public void Prism(IList<Vector2> prof, Func<Vector2, float, Vector3> at, float a, float b)
			{
				int n = prof.Count;
				for (int i = 0; i < n; i++)
				{
					var p0 = prof[i]; var p1 = prof[(i + 1) % n];
					Quad(at(p0, a), at(p0, b), at(p1, b), at(p1, a));
				}
				for (int i = 1; i < n - 1; i++) { Tri(at(prof[0], b), at(prof[i], b), at(prof[i + 1], b)); Tri(at(prof[0], a), at(prof[i + 1], a), at(prof[i], a)); }
			}

			public void Box(Vector3 c, Vector3 s)
			{
				var h = s / 2f;
				Prism(new[] { new Vector2(-h.y, -h.z), new Vector2(h.y, -h.z), new Vector2(h.y, h.z), new Vector2(-h.y, h.z) }, (q, x) => c + new Vector3(x, q.x, q.y), -h.x, h.x);
			}
		}

		static Mesh BodyMesh(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right)
		{
			var mb = new MB();
			var inner = new List<Vector2>(); var outer = new List<Vector2>();
			float r = p.PlateW / 2f, cy = p.PlateH / 2f - r, bot = -p.PlateH / 2f;
			inner.Add(new Vector2(r, bot)); outer.Add(new Vector2(r + p.Bezel, bot - p.ClampH));
			for (int i = 0; i <= 24; i++)
			{
				float a = Mathf.PI * i / 24f;
				inner.Add(new Vector2(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
				outer.Add(new Vector2(Mathf.Cos(a) * (r + p.Bezel), cy + Mathf.Sin(a) * (r + p.Bezel)));
			}
			inner.Add(new Vector2(-r, bot)); outer.Add(new Vector2(-r - p.Bezel, bot - p.ClampH));
			Vector3 P(Vector2 q, float z) => right * q.x + up * q.y + nrm * z;
			float zf = p.BezelDepth / 2f, zb = -p.BezelDepth / 2f;
			for (int i = 0; i < inner.Count; i++)
			{
				int k = (i + 1) % inner.Count;
				mb.Quad(P(inner[i], zf), P(outer[i], zf), P(outer[k], zf), P(inner[k], zf));
				mb.Quad(P(inner[k], zb), P(outer[k], zb), P(outer[i], zb), P(inner[i], zb));
				mb.Quad(P(outer[i], zf), P(outer[i], zb), P(outer[k], zb), P(outer[k], zf));
				mb.Quad(P(inner[k], zf), P(inner[k], zb), P(inner[i], zb), P(inner[i], zf));
			}
			var edge = up * bot;
			float top = edge.y - p.ClampH * 0.6f, z0 = edge.z - 0.004f, z1 = edge.z + p.BodyL, y0 = top - p.BodyH, ch = p.BodyH * 0.6f;
			var prof = new[] { new Vector2(z0, y0), new Vector2(z1, y0), new Vector2(z1, top), new Vector2(z0 + ch, top), new Vector2(z0, top - ch) };
			mb.Prism(prof, (q, x) => new Vector3(x, q.y, q.x), -p.BodyW / 2f, p.BodyW / 2f);
			float px0 = p.BracketFoot.x, pz0 = p.BracketFoot.z;
			mb.Box(new Vector3(px0, (y0 + p.BracketFoot.y) / 2f, pz0), new Vector3(0.018f, y0 - p.BracketFoot.y + 0.004f, 0.02f));
			var m = new Mesh { name = "K10-T body" };
			m.SetVertices(mb.V); m.SetTriangles(mb.T, 0);
			m.RecalculateNormals(); m.RecalculateBounds();
			m.uv = new Vector2[mb.V.Count];
			m.RecalculateTangents();
			return m;
		}

		public static (List<GameObject> holders, Material paint, Vector2 uv, int faces) SplitHolder(Transform visual, Transform root, Bounds holderBox)
		{
			var holders = new List<GameObject>(); Material paint = null; Vector2 puv = default; float best = 0f; int moved = 0;
			if (visual == null) return (holders, null, puv, 0);
			foreach (var mf in visual.GetComponentsInChildren<MeshFilter>(true))
			{
				var m = mf.sharedMesh; var mr = mf.GetComponent<MeshRenderer>();
				if (m == null || !m.isReadable || mr == null) continue;
				var v = m.vertices; var uvs = m.uv;
				var toPrefab = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
				var mats = mr.sharedMaterials;
				var keep = new List<int>[m.subMeshCount]; var hide = new List<int>[m.subMeshCount];
				int here = 0;
				for (int s = 0; s < m.subMeshCount; s++)
				{
					keep[s] = new List<int>(); hide[s] = new List<int>();
					var t = m.GetTriangles(s);
					for (int i = 0; i < t.Length; i += 3)
					{
						Vector3 a = toPrefab.MultiplyPoint3x4(v[t[i]]), b = toPrefab.MultiplyPoint3x4(v[t[i + 1]]), c = toPrefab.MultiplyPoint3x4(v[t[i + 2]]);
						bool h = holderBox.Contains((a + b + c) / 3f);
						(h ? hide[s] : keep[s]).AddRange(new[] { t[i], t[i + 1], t[i + 2] });
						if (!h) continue;
						here++;
						float area = Vector3.Cross(b - a, c - a).magnitude;
						if (area > best && uvs != null && uvs.Length == v.Length && s < mats.Length)
						{ best = area; paint = mats[s]; puv = (uvs[t[i]] + uvs[t[i + 1]] + uvs[t[i + 2]]) / 3f; }
					}
				}
				if (here == 0) continue;
				var bodyM = UnityEngine.Object.Instantiate(m); bodyM.name = m.name + " (holder out of the sight view)";
				var holderM = UnityEngine.Object.Instantiate(m); holderM.name = m.name + " (holder)";
				for (int s = 0; s < m.subMeshCount; s++) { bodyM.SetTriangles(keep[s], s); holderM.SetTriangles(hide[s], s); }
				bodyM.RecalculateBounds(); holderM.RecalculateBounds();
				mf.sharedMesh = bodyM;
				var go = new GameObject("K10-T model holder");
				go.transform.SetParent(mf.transform, false);
				go.layer = mf.gameObject.layer;
				go.AddComponent<MeshFilter>().sharedMesh = holderM;
				var hr = go.AddComponent<MeshRenderer>();
				hr.sharedMaterials = mats; hr.shadowCastingMode = mr.shadowCastingMode; hr.receiveShadows = mr.receiveShadows;
				holders.Add(go);
				moved += here;
			}
			return (holders, paint, puv, moved);
		}

		public sealed class EyeSwap : MonoBehaviour
		{
			public GameObject[] ModelHolder; public GameObject Body;
			public static bool At(Transform eye, Camera cam) => cam != null && (cam.transform.position - eye.position).sqrMagnitude < 0.03f * 0.03f;
			void LateUpdate()
			{
				bool here = At(transform, Camera.main);
				if (ModelHolder != null) foreach (var h in ModelHolder) if (h != null && h.activeSelf == here) h.SetActive(!here);
				if (Body != null && Body.activeSelf != here) Body.SetActive(here);
			}
		}

		public sealed class FilterByDay : MonoBehaviour
		{
			public GameObject Filter;
			float _next;
			void Update()
			{
				if (Time.unscaledTime < _next || Filter == null) return;
				_next = Time.unscaledTime + 1f;
				var sun = RenderSettings.sun;
				bool day = sun != null && sun.isActiveAndEnabled && sun.intensity > 0.3f && sun.transform.forward.y < -0.05f;
				if (Filter.activeSelf != day) Filter.SetActive(day);
			}
		}
	}
}
