using UnityEngine;

namespace T80UM2.Common
{
	internal static class VisualParts
	{
		internal static void Attach(Transform root, GameObject prefab, string prefix, (string group, string path)[] groups)
		{
			var inst = Object.Instantiate(prefab);
			foreach (var (group, path) in groups)
			{
				var part = inst.transform.Find(group);
				var bone = string.IsNullOrEmpty(path) ? null : root.Find(path);
				if (part == null || bone == null) continue;
				part.SetParent(bone, false);
				part.localPosition = Vector3.zero; part.localRotation = Quaternion.identity; part.localScale = Vector3.one;
				part.name = prefix + group;
			}
			Object.Destroy(inst);
		}
	}
}
