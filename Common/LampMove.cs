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
			public string Note = "";
		}

		internal const float Reach = 0.30f;

		internal static List<string> Apply(Transform root, IEnumerable<Lamp> lamps, IEnumerable<string> glows, string tag)
		{
			var log = new List<string>();
			string P(Vector3 worldPoint)
			{
				var localPoint = root.InverseTransformPoint(worldPoint);
				return $"({localPoint.x:0.000}, {localPoint.y:0.000}, {localPoint.z:0.000})";
			}
			var lights = root.GetComponentsInChildren<Light>(true);
			var moves = new List<(Vector3 from, Vector3 shift)>();
			foreach (var lamp in lamps)
			{
				var light = lights.FirstOrDefault(c => c.name == lamp.Light);
				if (light == null)
				{
					log.Add($"{tag} lamp '{lamp.Light}': NOT FOUND");
					continue;
				}
				var from = light.transform.position;
				var target = root.TransformPoint(lamp.Place);
				light.transform.position = target;
				moves.Add((from, target - from));
				log.Add($"{tag} lamp '{lamp.Light}': {P(from)} -> {P(target)} ({(target - from).magnitude * 100f:0.0} cm){(lamp.Note.Length > 0 ? ", " + lamp.Note : "")}.");
			}
			var names = new HashSet<string>(glows ?? Enumerable.Empty<string>());
			foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true).Where(c => names.Contains(c.name)))
			{
				if (meshFilter.sharedMesh == null || !meshFilter.sharedMesh.isReadable)
				{
					log.Add($"{tag} lamp glow '{meshFilter.name}': mesh {(meshFilter.sharedMesh == null ? "missing" : "not readable")}, left in place.");
					continue;
				}
				// The instance, not sharedMesh: the T-80B's glow meshes are shared by every T-80B in the scene.
				var mesh = meshFilter.mesh;
				var vertices = mesh.vertices;
				var clusters = Clusters(mesh);
				int moved = 0;
				var parts = new List<string>();
				foreach (var cluster in clusters)
				{
					var centre = meshFilter.transform.TransformPoint(cluster.Aggregate(
						Vector3.zero,
						(s, i) => s + vertices[i]) / cluster.Count);
					var best = moves
						.Where(m => (m.from - centre).magnitude <= Reach)
						.OrderBy(m => (m.from - centre).sqrMagnitude)
						.ToList();
					if (best.Count == 0)
					{
						parts.Add($"{P(centre)} stays");
						continue;
					}
					var local = meshFilter.transform.InverseTransformVector(best[0].shift);
					foreach (int i in cluster) vertices[i] += local;
					moved++;
					parts.Add($"{P(centre)} -> {P(centre + best[0].shift)}");
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
				log.Add($"{tag} lamp glow '{meshFilter.name}': {moved} of {clusters.Count} quads moved with their lamps ({string.Join("; ", parts)}).");
			}
			return log;
		}

		static List<List<int>> Clusters(Mesh mesh)
		{
			var vertices = mesh.vertices;
			var parent = Enumerable.Range(0, vertices.Length).ToArray();
			int Root(int i)
			{
				while (parent[i] != i) i = parent[i] = parent[parent[i]];
				return i;
			}
			void Unite(int first, int second)
			{
				first = Root(first);
				second = Root(second);
				if (first != second) parent[first] = second;
			}
			var triangles = mesh.triangles;
			for (int k = 0; k + 2 < triangles.Length; k += 3)
			{
				Unite(triangles[k], triangles[k + 1]);
				Unite(triangles[k], triangles[k + 2]);
			}
			var byPos = new Dictionary<Vector3, int>();
			for (int i = 0; i < vertices.Length; i++)
			{
				if (byPos.TryGetValue(vertices[i], out var j)) Unite(i, j);
				else byPos[vertices[i]] = i;
			}
			return Enumerable.Range(0, vertices.Length).GroupBy(Root).Select(c => c.ToList()).ToList();
		}
	}
}
