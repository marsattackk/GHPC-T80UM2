using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace T80UM2.Common
{
	internal static class LampMove
	{
		internal sealed class Lamp
		{
			public string Light;
			public Vector3 Place;
		}

		internal const float Reach = 0.30f;

		internal static void Apply(Transform root, IEnumerable<Lamp> lamps, IEnumerable<string> glows)
		{
			var lights = root.GetComponentsInChildren<Light>(true);
			var moves = new List<(Vector3 from, Vector3 shift)>();
			foreach (var l in lamps)
			{
				var light = lights.FirstOrDefault(x => x.name == l.Light);
				if (light == null) continue;
				var from = light.transform.position;
				var to = root.TransformPoint(l.Place);
				light.transform.position = to;
				moves.Add((from, to - from));
			}
			var names = new HashSet<string>(glows ?? Enumerable.Empty<string>());
			foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true).Where(m => names.Contains(m.name)))
			{
				if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
				// The instance, not sharedMesh: the T-80B's glow meshes are shared by every T-80B in the scene.
				var mesh = mf.mesh;
				var v = mesh.vertices;
				var clusters = Clusters(mesh);
				foreach (var c in clusters)
				{
					var centre = mf.transform.TransformPoint(c.Aggregate(Vector3.zero, (a, i) => a + v[i]) / c.Count);
					var best = moves.Where(m => (m.from - centre).magnitude <= Reach).OrderBy(m => (m.from - centre).sqrMagnitude).ToList();
					if (best.Count == 0) continue;
					var local = mf.transform.InverseTransformVector(best[0].shift);
					foreach (int i in c) v[i] += local;
				}
				mesh.vertices = v;
				mesh.RecalculateBounds();
			}
		}

		static List<List<int>> Clusters(Mesh mesh)
		{
			var v = mesh.vertices;
			var parent = Enumerable.Range(0, v.Length).ToArray();
			int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
			void Unite(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[a] = b; }
			var t = mesh.triangles;
			for (int k = 0; k + 2 < t.Length; k += 3) { Unite(t[k], t[k + 1]); Unite(t[k], t[k + 2]); }
			var byPos = new Dictionary<Vector3, int>();
			for (int i = 0; i < v.Length; i++) { if (byPos.TryGetValue(v[i], out var j)) Unite(i, j); else byPos[v[i]] = i; }
			return Enumerable.Range(0, v.Length).GroupBy(Root).Select(g => g.ToList()).ToList();
		}
	}
}
