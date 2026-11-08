using System.Collections.Generic;
using System.Reflection;
using Reticle;
using T80UM2.Common;
using UnityEngine;

namespace T80UM2
{
	internal static class K10Window
	{
		internal static readonly Vector3 ReflectorWt = new Vector3(1.054f, 2.890f, -0.700f);
		static readonly Bounds HolderWt = new Bounds(new Vector3(1.16f, 2.83f, -0.685f), new Vector3(0.34f, 0.34f, 0.37f));
		internal static readonly CollimatorPreset Preset = CollimatorSight.K10T();
		internal static float EyeRelief => Preset.EyeRelief;

		internal static void Build(Transform cradleT, Vector3 reflector, Quaternion axisRot, Quaternion plateTilt, Transform cradleVis, Transform root, Transform eye, List<string> notes)
		{
			var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
			if (shader == null) { notes.Add("К10-Т window: no transparent shader found, not drawn"); return; }
			var (holders, paint, uv, faces) = CollimatorSight.SplitHolder(cradleVis, root, HolderWt);
			var b = CollimatorSight.Build(cradleT, reflector, axisRot, plateTilt, Preset, paint, uv, shader, notes);
			b.Root.gameObject.AddComponent<CollimatorSight.FilterByDay>().Filter = b.Filter;
			var swap = eye.gameObject.AddComponent<CollimatorSight.EyeSwap>();
			swap.ModelHolder = holders.ToArray(); swap.Body = b.Body;
			b.Body.SetActive(false);
			notes.Add($"holder: {faces} faces of the model's holder out of the sight view, the clean body in (paint '{paint?.name ?? "none"}'; shader '{shader.name}')");
		}

		internal sealed class HideReticle : MonoBehaviour
		{
			ReticleMesh _rm; FieldInfo _smr; int _done;
			internal void Init(ReticleMesh rm, FieldInfo smr) { _rm = rm; _smr = smr; }

			// ReticleMesh only sets SMR when it loads, so it's read every frame. The mesh goes too or a faint second reticle still draws.
			void LateUpdate()
			{
				if (_rm == null) return;
				if (_smr?.GetValue(_rm) is Renderer r && r != null) Off(r);
				foreach (var x in _rm.GetComponentsInChildren<Renderer>(true)) Off(x);
			}

			void Off(Renderer r)
			{
				if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) smr.sharedMesh = null;
				if (r.forceRenderingOff && !r.enabled) return;
				r.forceRenderingOff = true; r.enabled = false;
				if (_done++ == 0) MelonLoader.MelonLogger.Msg($"{T80UM2Mod.FriendlyName} NSVT: К10-Т: GHPC's reticle renderer '{r.name}' ({r.GetType().Name}) held off.");
			}
		}
	}
}
