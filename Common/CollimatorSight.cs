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
				PlateUp = new Vector3(0f, 0.73f, -0.69f).normalized,
				PlateNormal = new Vector3(0f, -0.69f, -0.73f).normalized,
				PlateW = 0.043f,
				PlateH = 0.062f,
				EyeRelief = 0.155f,
				BracketFoot = new Vector3(0.012f, -0.23f, 0.06f),
				Inferred = "the plate's outline, line width / colour / shadow, the filter's tint, the body's 4.6 x 3.0 x 7.5 cm under the plate (the data sheet's 120 x 82 x 90 mm is the whole sight) and the bracket post's run to the mount",
				Reticle = (x, y) =>
				{
					float rad = Mathf.Sqrt(x * x + y * y);
					float distance = Mathf.Min(Mathf.Abs(rad - RingOut), Mathf.Abs(rad - RingIn));
					distance = Mathf.Min(distance, Mathf.Min(Mathf.Abs(x), Mathf.Abs(y)));
					for (int k = 1; k <= 4; k++)
					{
						float tickX = k * Div, half = (k % 2 == 0 ? TickLong : TickShort) / 2f;
						if (Mathf.Abs(y) <= half) distance = Mathf.Min(distance, Mathf.Abs(Mathf.Abs(x) - tickX));
						if (Mathf.Abs(x) <= half) distance = Mathf.Min(distance, Mathf.Abs(Mathf.Abs(y) - tickX));
					}
					if (rad >= RingOut && rad <= RingOut + 6f)
					{
						float ang = Mathf.Abs(Mathf.Atan2(y, x)) * Mathf.Rad2Deg;
						distance = Mathf.Min(distance, Mathf.Min(Mathf.Abs(ang - 45f), Mathf.Abs(ang - 135f)) * Mathf.Deg2Rad * rad);
					}
					return distance;
				},
			};
		}

		public sealed class Built
		{
			public Transform Root;
			public GameObject Body;
			public GameObject Filter;
		}

		public static Built Build(
			Transform parent,
			Vector3 reflector,
			Quaternion axisRot,
			Quaternion plateTilt,
			CollimatorPreset p,
			Material paint,
			Vector2 paintUv,
			Shader transparent,
			List<string> notes)
		{
			var built = new Built();
			var sightObject = new GameObject("K10-T sight (" + p.Name + ")");
			sightObject.transform.SetParent(parent, false);
			sightObject.transform.SetPositionAndRotation(reflector, axisRot);
			built.Root = sightObject.transform;
			var plateRot = plateTilt * Quaternion.LookRotation(p.PlateNormal, p.PlateUp);
			Vector3 up = plateTilt * p.PlateUp, nrm = plateTilt * p.PlateNormal, right = Vector3.Cross(nrm, up).normalized;
			if (Vector3.Dot(right, Vector3.right) < 0f) right = -right;

			var (rtex, rmin, rmax) = ReticleTexture(p, up, nrm, right);
			Quad(sightObject.transform, "reticle", transparent, rtex, 3002, Vector3.zero, Quaternion.identity, rmin, rmax);
			var pmin = new Vector2(-p.PlateW / 2f, -p.PlateH / 2f);
			var pmax = -pmin;
			Quad(sightObject.transform, "glass", transparent, PlateTexture(p, false), 3000, nrm * 0.0003f, plateRot, pmin, pmax);
			built.Filter = Quad(
				sightObject.transform,
				"light filter",
				transparent,
				PlateTexture(p, true),
				3001,
				nrm * 0.0006f,
				plateRot,
				pmin,
				pmax);

			var mesh = BodyMesh(p, up, nrm, right);
			var body = new GameObject("K10-T body");
			body.transform.SetParent(sightObject.transform, false);
			body.AddComponent<MeshFilter>().sharedMesh = mesh;
			var meshRenderer = body.AddComponent<MeshRenderer>();
			if (paint != null)
			{
				var uv = new Vector2[mesh.vertexCount];
				for (int i = 0; i < uv.Length; i++) uv[i] = paintUv;
				mesh.uv = uv;
				mesh.RecalculateTangents();
				meshRenderer.sharedMaterial = paint;
			}
			else
				meshRenderer.sharedMaterial = new Material(Shader.Find("Standard")
					?? transparent) { color = new Color(0.17f, 0.19f, 0.15f) };
			built.Body = body;
			notes?.Add($"{p.Name} sight: reticle square to the axis at {p.EyeRelief * 1000f:0} mm ({rtex.width} x {rtex.height}, mipmapped, lines {p.LineW:0.0} mil with a {p.ShadowW:0.0} mil soft shadow), plate {p.PlateW * 100f:0.0} x {p.PlateH * 100f:0.0} cm, bezel + body {p.BodyW * 100f:0.0} x {p.BodyH * 100f:0.0} x {p.BodyL * 100f:0.0} cm + bracket ({mesh.vertexCount} verts, {(paint != null ? "the mount's paint" : "plain grey")}); INFERRED: {p.Inferred}");
			return built;
		}

		static GameObject Quad(
			Transform parent,
			string name,
			Shader shader,
			Texture2D tex,
			int queue,
			Vector3 pos,
			Quaternion rot,
			Vector2 min,
			Vector2 max)
		{
			var quadObject = new GameObject("K10-T " + name);
			quadObject.transform.SetParent(parent, false);
			quadObject.transform.localPosition = pos;
			quadObject.transform.localRotation = rot;
			var mesh = new Mesh { name = "K10-T " + name };
			mesh.vertices = new[]
			{
				new Vector3(min.x, min.y, 0f),
				new Vector3(max.x, min.y, 0f),
				new Vector3(max.x, max.y, 0f),
				new Vector3(min.x, max.y, 0f)
			};
			mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
			mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
			mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
			mesh.RecalculateBounds();
			quadObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			var meshRenderer = quadObject.AddComponent<MeshRenderer>();
			meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.sharedMaterial = new Material(shader) { mainTexture = tex, renderQueue = queue, name = "K10-T " + name };
			return quadObject;
		}

		static float Inside(CollimatorPreset p, float x, float v)
		{
			float radius = p.PlateW / 2f, arcCenterY = p.PlateH / 2f - radius;
			return v > arcCenterY
				? radius - Mathf.Sqrt(x * x + (v - arcCenterY) * (v - arcCenterY))
				: Mathf.Min(radius - Mathf.Abs(x), v + p.PlateH / 2f);
		}

		static bool OnPlate(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right, float qx, float qy, out float u, out float v)
		{
			var eye = new Vector3(0f, 0f, -p.EyeRelief);
			var dir = new Vector3(qx, qy, p.EyeRelief);
			float den = Vector3.Dot(nrm, dir);
			u = v = 0f;
			if (Mathf.Abs(den) < 1e-6f) return false;
			var hit = eye + dir * (-Vector3.Dot(nrm, eye) / den);
			u = Vector3.Dot(hit, right);
			v = Vector3.Dot(hit, up);
			return true;
		}

		static (Texture2D, Vector2, Vector2) ReticleTexture(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right)
		{
			Vector2 boundsMin = new Vector2(1f, 1f), boundsMax = -boundsMin;
			for (int i = 0; i <= 64; i++)
			{
				float fraction = i / 64f;
				foreach (var (x, vertical) in new[]
				{
					(Mathf.Lerp(-p.PlateW / 2f, p.PlateW / 2f, fraction), p.PlateH / 2f),
					(Mathf.Lerp(-p.PlateW / 2f, p.PlateW / 2f, fraction), -p.PlateH / 2f),
					(-p.PlateW / 2f, Mathf.Lerp(-p.PlateH / 2f, p.PlateH / 2f, fraction)),
					(p.PlateW / 2f, Mathf.Lerp(-p.PlateH / 2f, p.PlateH / 2f, fraction))
				})
				{
					var platePoint = right * x + up * vertical;
					float scale = p.EyeRelief / (p.EyeRelief + platePoint.z);
					var projected = new Vector2(platePoint.x * scale, platePoint.y * scale);
					boundsMin = Vector2.Min(boundsMin, projected);
					boundsMax = Vector2.Max(boundsMax, projected);
				}
			}
			boundsMin -= Vector2.one * 0.001f;
			boundsMax += Vector2.one * 0.001f;
			const int width = 1024;
			int height = Mathf.RoundToInt(width * (boundsMax.y - boundsMin.y) / (boundsMax.x - boundsMin.x));
			float texelSize = (boundsMax.x - boundsMin.x) / width;
			float milPx = texelSize / p.EyeRelief / MilRad;
			var pix = new Color[width * height];
			float halfLine = p.LineW / 2f, shadowHalf = halfLine + p.ShadowW;
			Parallel.For(0, height, j =>
			{
				for (int i = 0; i < width; i++)
				{
					float planeX = boundsMin.x + (i + 0.5f) * texelSize, planeY = boundsMin.y + (j + 0.5f) * texelSize;
					Color color = Color.clear;
					if (OnPlate(p, up, nrm, right, planeX, planeY, out float plateU, out float plateV))
					{
						float inside = Inside(p, plateU, plateV) - p.Bezel * 0.3f;
						float cover = Mathf.Clamp01(inside / (texelSize * 1.4f) + 0.5f);
						if (cover > 0f)
						{
							float distance = p.Reticle(
								Mathf.Atan2(planeX, p.EyeRelief) / MilRad,
								Mathf.Atan2(planeY, p.EyeRelief) / MilRad);
							float coverage = Mathf.Clamp01((halfLine - distance) / milPx + 0.5f);
							float shadowAmount = Mathf.Clamp01((shadowHalf - distance) / p.ShadowW);
							shadowAmount = shadowAmount * shadowAmount * (3f - 2f * shadowAmount);
							var shadowColor = p.Shadow;
							shadowColor.a *= shadowAmount;
							color = Color.Lerp(shadowColor, p.Line, coverage);
							color.a = Mathf.Lerp(shadowColor.a, 1f, coverage) * cover;
						}
					}
					pix[j * width + i] = color;
				}
			});
			var tex = new Texture2D(
				width,
				height,
				TextureFormat.RGBA32,
				true) { name = "K10-T reticle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
			tex.SetPixels(pix);
			tex.Apply(true, true);
			return (tex, boundsMin, boundsMax);
		}

		static readonly Color GlassTop = new Color(0.8f, 0.87f, 1f, 0.06f), GlassBottom = new Color(0.6f, 0.66f, 0.74f, 0.015f);
		static Texture2D PlateTexture(CollimatorPreset p, bool filter)
		{
			const int width = 256;
			int height = Mathf.RoundToInt(width * p.PlateH / p.PlateW);
			float texelSize = p.PlateW / width;
			var pix = new Color[width * height];
			Parallel.For(0, height, j =>
			{
				for (int i = 0; i < width; i++)
				{
					float x = (i + 0.5f) * texelSize - p.PlateW / 2f, plateV = (j + 0.5f) * texelSize - p.PlateH / 2f;
					float cover = Mathf.Clamp01(Inside(p, x, plateV) / texelSize + 0.5f);
					var color = filter ? p.Filter : Color.Lerp(GlassBottom, GlassTop, Mathf.Clamp01((plateV + p.PlateH / 2f) / p.PlateH));
					color.a *= cover;
					pix[j * width + i] = color;
				}
			});
			var tex = new Texture2D(
				width,
				height,
				TextureFormat.RGBA32,
				true)
				{
					name = "K10-T " + (filter ? "filter" : "glass"),
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Trilinear
				};
			tex.SetPixels(pix);
			tex.Apply(true, true);
			return tex;
		}

		sealed class MB
		{
			public readonly List<Vector3> V = new List<Vector3>();
			public readonly List<int> T = new List<int>();
			public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
			{
				int baseIndex = V.Count;
				V.Add(a);
				V.Add(b);
				V.Add(c);
				V.Add(d);
				T.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });
			}

			public void Tri(Vector3 a, Vector3 b, Vector3 c)
			{
				int baseIndex = V.Count;
				V.Add(a);
				V.Add(b);
				V.Add(c);
				T.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2 });
			}

			public void Prism(IList<Vector2> prof, Func<Vector2, float, Vector3> at, float a, float b)
			{
				int count = prof.Count;
				for (int i = 0; i < count; i++)
				{
					var current = prof[i];
					var next = prof[(i + 1) % count];
					Quad(at(current, a), at(current, b), at(next, b), at(next, a));
				}
				for (int i = 1; i < count - 1; i++)
				{
					Tri(at(prof[0], b), at(prof[i], b), at(prof[i + 1], b));
					Tri(at(prof[0], a), at(prof[i + 1], a), at(prof[i], a));
				}
			}

			public void Box(Vector3 c, Vector3 s)
			{
				var halfSize = s / 2f;
				Prism(
					new[]
					{
						new Vector2(-halfSize.y, -halfSize.z),
						new Vector2(halfSize.y, -halfSize.z),
						new Vector2(halfSize.y, halfSize.z),
						new Vector2(-halfSize.y, halfSize.z)
					},
					(p, x) => c + new Vector3(x, p.x, p.y),
					-halfSize.x,
					halfSize.x);
			}
		}

		static Mesh BodyMesh(CollimatorPreset p, Vector3 up, Vector3 nrm, Vector3 right)
		{
			var builder = new MB();
			var inner = new List<Vector2>();
			var outer = new List<Vector2>();
			float radius = p.PlateW / 2f, arcCenterY = p.PlateH / 2f - radius, bot = -p.PlateH / 2f;
			inner.Add(new Vector2(radius, bot));
			outer.Add(new Vector2(radius + p.Bezel, bot - p.ClampH));
			for (int i = 0; i <= 24; i++)
			{
				float angle = Mathf.PI * i / 24f;
				inner.Add(new Vector2(Mathf.Cos(angle) * radius, arcCenterY + Mathf.Sin(angle) * radius));
				outer.Add(new Vector2(Mathf.Cos(angle) * (radius + p.Bezel), arcCenterY + Mathf.Sin(angle) * (radius + p.Bezel)));
			}
			inner.Add(new Vector2(-radius, bot));
			outer.Add(new Vector2(-radius - p.Bezel, bot - p.ClampH));
			Vector3 P(Vector2 point, float z) => right * point.x + up * point.y + nrm * z;
			float frontZ = p.BezelDepth / 2f, backZ = -p.BezelDepth / 2f;
			for (int i = 0; i < inner.Count; i++)
			{
				int k = (i + 1) % inner.Count;
				builder.Quad(P(inner[i], frontZ), P(outer[i], frontZ), P(outer[k], frontZ), P(inner[k], frontZ));
				builder.Quad(P(inner[k], backZ), P(outer[k], backZ), P(outer[i], backZ), P(inner[i], backZ));
				builder.Quad(P(outer[i], frontZ), P(outer[i], backZ), P(outer[k], backZ), P(outer[k], frontZ));
				builder.Quad(P(inner[k], frontZ), P(inner[k], backZ), P(inner[i], backZ), P(inner[i], frontZ));
			}
			var edge = up * bot;
			float top = edge.y
				- p.ClampH * 0.6f, startZ = edge.z - 0.004f, endZ = edge.z + p.BodyL, bottomY = top - p.BodyH, clampDepth = p.BodyH * 0.6f;
			var prof = new[]
			{
				new Vector2(startZ, bottomY),
				new Vector2(endZ, bottomY),
				new Vector2(endZ, top),
				new Vector2(startZ + clampDepth, top),
				new Vector2(startZ, top - clampDepth)
			};
			builder.Prism(prof, (pp, x) => new Vector3(x, pp.y, pp.x), -p.BodyW / 2f, p.BodyW / 2f);
			float px0 = p.BracketFoot.x, pz0 = p.BracketFoot.z;
			builder.Box(
				new Vector3(px0, (bottomY + p.BracketFoot.y) / 2f, pz0),
				new Vector3(0.018f, bottomY - p.BracketFoot.y + 0.004f, 0.02f));
			var mesh = new Mesh { name = "K10-T body" };
			mesh.SetVertices(builder.V);
			mesh.SetTriangles(builder.T, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			mesh.uv = new Vector2[builder.V.Count];
			mesh.RecalculateTangents();
			return mesh;
		}

		public static (List<GameObject> holders, Material paint, Vector2 uv, int faces) SplitHolder(
			Transform visual,
			Transform root,
			Bounds holderBox)
		{
			var holders = new List<GameObject>();
			Material paint = null;
			Vector2 puv = default;
			float best = 0f;
			int moved = 0;
			if (visual == null) return (holders, null, puv, 0);
			foreach (var meshFilter in visual.GetComponentsInChildren<MeshFilter>(true))
			{
				var mesh = meshFilter.sharedMesh;
				var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
				if (mesh == null || !mesh.isReadable || meshRenderer == null) continue;
				var vertices = mesh.vertices;
				var uvs = mesh.uv;
				var toPrefab = root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
				var mats = meshRenderer.sharedMaterials;
				var keep = new List<int>[mesh.subMeshCount];
				var hide = new List<int>[mesh.subMeshCount];
				int here = 0;
				for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
				{
					keep[subMesh] = new List<int>();
					hide[subMesh] = new List<int>();
					var triangles = mesh.GetTriangles(subMesh);
					for (int i = 0; i < triangles.Length; i += 3)
					{
						Vector3 cornerA = toPrefab.MultiplyPoint3x4(vertices[triangles[i]]), cornerB = toPrefab.MultiplyPoint3x4(vertices[triangles[i
								+ 1]]), cornerC = toPrefab.MultiplyPoint3x4(vertices[triangles[i + 2]]);
						bool inHolder = holderBox.Contains((cornerA + cornerB + cornerC) / 3f);
						(inHolder ? hide[subMesh] : keep[subMesh]).AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
						if (!inHolder) continue;
						here++;
						float area = Vector3.Cross(cornerB - cornerA, cornerC - cornerA).magnitude;
						if (area > best && uvs != null && uvs.Length == vertices.Length && subMesh < mats.Length)
						{
							best = area;
							paint = mats[subMesh];
							puv = (uvs[triangles[i]] + uvs[triangles[i + 1]] + uvs[triangles[i + 2]]) / 3f;
						}
					}
				}
				if (here == 0) continue;
				var bodyM = UnityEngine.Object.Instantiate(mesh);
				bodyM.name = mesh.name + " (holder out of the sight view)";
				var holderM = UnityEngine.Object.Instantiate(mesh);
				holderM.name = mesh.name + " (holder)";
				for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
				{
					bodyM.SetTriangles(keep[subMesh], subMesh);
					holderM.SetTriangles(hide[subMesh], subMesh);
				}
				bodyM.RecalculateBounds();
				holderM.RecalculateBounds();
				meshFilter.sharedMesh = bodyM;
				var holderObject = new GameObject("K10-T model holder");
				holderObject.transform.SetParent(meshFilter.transform, false);
				holderObject.layer = meshFilter.gameObject.layer;
				holderObject.AddComponent<MeshFilter>().sharedMesh = holderM;
				var holderRenderer = holderObject.AddComponent<MeshRenderer>();
				holderRenderer.sharedMaterials = mats;
				holderRenderer.shadowCastingMode = meshRenderer.shadowCastingMode;
				holderRenderer.receiveShadows = meshRenderer.receiveShadows;
				holders.Add(holderObject);
				moved += here;
			}
			return (holders, paint, puv, moved);
		}

		public sealed class EyeSwap : MonoBehaviour
		{
			public GameObject[] ModelHolder;
			public GameObject Body;
			public static bool At(Transform eye, Camera cam)
				=> cam != null && (cam.transform.position - eye.position).sqrMagnitude < 0.03f * 0.03f;
			void LateUpdate()
			{
				bool here = At(transform, Camera.main);
				if (ModelHolder != null)
					foreach (var holder in ModelHolder) if (holder != null && holder.activeSelf == here) holder.SetActive(!here);
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
