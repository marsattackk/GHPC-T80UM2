using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace T80UM2.Common
{
	public sealed class BeltCurve
	{
		const int N = 48;
		readonly Vector3[] _p = new Vector3[N + 1];
		readonly float[] _s = new float[N + 1];
		Vector3 _upA, _upB;
		public float Length => _s[N];

		public BeltCurve(Vector3 feed, Vector3 feedDir, Vector3 feedUp, Vector3 mouth, Vector3 mouthDir, Vector3 mouthUp) =>
			Set(feed, feedDir, feedUp, mouth, mouthDir, mouthUp);

		public void Set(Vector3 feed, Vector3 feedDir, Vector3 feedUp, Vector3 mouth, Vector3 mouthDir, Vector3 mouthUp)
		{
			float h = (mouth - feed).magnitude * 0.4f;
			Vector3 p1 = feed + feedDir * h, p2 = mouth + mouthDir * h;
			for (int i = 0; i <= N; i++)
			{
				float t = i / (float)N, u = 1f - t;
				_p[i] = u * u * u * feed + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * mouth;
				_s[i] = i == 0 ? 0f : _s[i - 1] + (_p[i] - _p[i - 1]).magnitude;
			}
			_upA = feedUp; _upB = mouthUp;
		}

		int Seg(float s) { int i = 0; while (i < N - 1 && _s[i + 1] < s) i++; return i; }

		public Vector3 Point(float s)
		{
			if (s <= 0f) return _p[0] + (_p[1] - _p[0]).normalized * s;
			if (s >= Length) return _p[N] + (_p[N] - _p[N - 1]).normalized * (s - Length);
			int i = Seg(s);
			return Vector3.Lerp(_p[i], _p[i + 1], (s - _s[i]) / Mathf.Max(1e-6f, _s[i + 1] - _s[i]));
		}

		public Matrix4x4 Frame(float s)
		{
			int i = Mathf.Clamp(Seg(Mathf.Clamp(s, 0f, Length)), 0, N - 1);
			var tan = (_p[i + 1] - _p[i]).normalized;
			var up = Vector3.Slerp(_upA, _upB, Mathf.Clamp01(s / Mathf.Max(1e-6f, Length)));
			if (Mathf.Abs(Vector3.Dot(up, tan)) > 0.995f) up = Vector3.Cross(tan, Vector3.right);
			return Matrix4x4.TRS(Point(s), Quaternion.LookRotation(tan, up), Vector3.one);
		}
	}

	public static class BeltRound
	{
		public sealed class Data
		{
			public Vector3[] V;
			public Vector2[] Uv;
			public int[] Round, Cartridge;
		}

		public static Data Read(byte[] gz)
		{
			if (gz == null) return null;
			using (var r = new BinaryReader(new GZipStream(new MemoryStream(gz), CompressionMode.Decompress)))
			{
				if (new string(r.ReadChars(4)) != "GBLT" || r.ReadInt32() != 1) return null;
				var d = new Data();
				r.ReadSingle();
				int n = r.ReadInt32();
				d.V = new Vector3[n]; d.Uv = new Vector2[n];
				for (int i = 0; i < n; i++) { d.V[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); d.Uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle()); }
				int[] Ints() { var a = new int[r.ReadInt32()]; for (int i = 0; i < a.Length; i++) a[i] = r.ReadInt32(); return a; }
				d.Round = Ints(); d.Cartridge = Ints();
				return d;
			}
		}

		public static Mesh RoundMesh(Data d, string name)
		{
			var m = new Mesh { name = name };
			m.vertices = d.V; m.uv = d.Uv;
			m.SetTriangles(d.Round, 0);
			m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
			return m;
		}

		public static (Vector3[] v, Vector3[] n, Vector2[] uv, Vector4[] tg, int[] tris, List<int> starts) CartridgeArrays(Mesh round, Data d)
		{
			var starts = new List<int>();
			for (int t = 0; t < d.Cartridge.Length; t += 3) starts.Add(t);
			return (round.vertices, round.normals, round.uv, round.tangents, d.Cartridge, starts);
		}

		public static Quaternion CartridgeRot(BeltCurve rest, Vector3 cartridgeAxis)
		{
			var toF = rest.Frame(0f).inverse;
			var ax = toF.MultiplyVector(cartridgeAxis).normalized;
			var along = Vector3.ProjectOnPlane(Vector3.forward, ax).normalized;
			return Quaternion.LookRotation(ax, Vector3.Cross(ax, along));
		}
	}
}
