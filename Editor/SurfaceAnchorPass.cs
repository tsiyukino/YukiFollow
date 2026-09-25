using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace TsiYuki.Follow.Editor
{
    /// <summary>
    /// Build step for <see cref="YukiSurfaceAnchor"/>.
    ///
    /// For every blendshape that moves the anchored point and is animated somewhere, one empty
    /// "follow" transform is inserted above the anchored object. Each animation clip that writes the
    /// blendshape gets curves that move that follow transform by the same amount. One transform per
    /// blendshape keeps the effects additive even when different layers drive different blendshapes,
    /// and because the curves live in the very clips that drive the mesh, write defaults, blend trees
    /// and motion time all behave exactly like the blendshape itself.
    /// </summary>
    internal static class SurfaceAnchorPass
    {
        static readonly string[] PositionProps = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        static readonly string[] RotationProps = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };

        public static void Run(BuildContext ctx)
        {
            var anchors = ctx.AvatarRootObject.GetComponentsInChildren<YukiSurfaceAnchor>(true);
            if (anchors.Length == 0) return;

            var animators = ctx.Extension<AnimatorServicesContext>();
            var physBones = ctx.AvatarRootObject.GetComponentsInChildren<VRCPhysBone>(true);

            foreach (var anchor in anchors)
            {
                try { Apply(ctx, animators, physBones, anchor); }
                catch (System.Exception e)
                {
                    Debug.LogException(e, anchor);
                    FollowText.Errors.Report(ErrorSeverity.NonFatal, "error.anchor_failed", anchor, anchor.name, e.Message);
                }
            }
            foreach (var anchor in anchors)
                if (anchor != null) Object.DestroyImmediate(anchor);
        }

        static void Apply(BuildContext ctx, AnimatorServicesContext animators, VRCPhysBone[] physBones, YukiSurfaceAnchor anchor)
        {
            var target = anchor.transform;
            var surface = anchor.surface != null ? anchor.surface : SurfaceSolver.FindNearestSurface(ctx.AvatarRootTransform, target.position);
            if (surface == null)
            {
                FollowText.Errors.Report(ErrorSeverity.NonFatal, "error.no_surface", anchor, anchor.name);
                return;
            }

            var bind = AnchorBinding.EffectiveBind(anchor, ctx.AvatarRootTransform, surface);
            var solution = SurfaceSolver.Solve(surface, target.position, target.rotation, anchor.sampleRadius, anchor.followRotation, bind,
                AnchorBinding.IgnoreFilter(anchor));
            if (solution.Error != null)
            {
                FollowText.Errors.Report(ErrorSeverity.NonFatal, "error.no_surface", anchor, anchor.name);
                return;
            }

            // Scene values may differ from the bind: start from where the surface is now.
            if (solution.Corrected)
            {
                target.position = solution.RestPosition;
                if (anchor.followRotation) target.rotation = solution.RestRotation;
            }

            var paths = animators.ObjectPathRemapper;
            var surfacePath = paths.GetVirtualPathForObject(surface.transform);

            // Only blendshapes something animates need a follow transform.
            var driven = new List<(ShapeMotion motion, EditorCurveBinding binding, List<VirtualClip> clips)>();
            foreach (var motion in solution.Motions)
            {
                var binding = EditorCurveBinding.FloatCurve(surfacePath, typeof(SkinnedMeshRenderer), "blendShape." + motion.Name);
                var clips = animators.AnimationIndex.GetClipsForBinding(binding).ToList();
                if (clips.Count > 0) driven.Add((motion, binding, clips));
            }
            if (driven.Count == 0)
            {
                Debug.Log($"[Yuki Follow] {anchor.name}: no animated blendshape of {surface.name} moves it; nothing to do.", anchor);
                return;
            }

            WarnIfTransformAnimated(animators, paths, anchor);

            // parent -> follow_1 -> ... -> follow_n -> target. Follow transforms sit at the target's
            // rest position with the parent's orientation, so each one rotates about the target.
            var parent = target.parent;
            var sibling = target.GetSiblingIndex();
            var localPosition = target.localPosition;
            var localRotation = target.localRotation;
            var localScale = target.localScale;

            Transform chainTop = null, current = parent;
            var follows = new List<Transform>();
            foreach (var d in driven)
            {
                var go = new GameObject($"{target.name} (Follow {d.motion.Name})");
                var t = go.transform;
                t.SetParent(current, false);
                t.localPosition = current == parent ? localPosition : Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                if (chainTop == null) { chainTop = t; t.SetSiblingIndex(sibling); }
                follows.Add(t);
                current = t;
            }
            target.SetParent(current, false);
            target.localPosition = Vector3.zero;
            target.localRotation = localRotation;
            target.localScale = localScale;

            // Offsets are expressed in the parent's space (every follow transform has its axes).
            var parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            for (int i = 0; i < driven.Count; i++)
            {
                var (motion, binding, clips) = driven[i];
                var follow = follows[i];
                var followPath = paths.GetVirtualPathForObject(follow);
                var restPosition = follow.localPosition;
                var deltaPosition = parent != null ? parent.InverseTransformVector(motion.DeltaPosition) : motion.DeltaPosition;
                var deltaEuler = anchor.followRotation
                    ? SurfaceSolver.SignedEuler(Quaternion.Inverse(parentRotation) * motion.DeltaRotation * parentRotation)
                    : Vector3.zero;

                foreach (var clip in clips)
                {
                    var source = clip.GetFloatCurve(binding);
                    if (source == null || source.length == 0) continue;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        clip.SetFloatCurve(followPath, typeof(Transform), PositionProps[axis],
                            Map(source, restPosition[axis], deltaPosition[axis] / motion.FrameWeight, motion.RestWeight));
                        if (anchor.followRotation)
                            clip.SetFloatCurve(followPath, typeof(Transform), RotationProps[axis],
                                Map(source, 0f, deltaEuler[axis] / motion.FrameWeight, motion.RestWeight));
                    }
                }
            }

            // Follow transforms must not become part of a PhysBone chain the target was kept out of.
            foreach (var pb in physBones)
            {
                if (pb == null) continue;
                var root = pb.GetRootTransform();
                if (root == null || !chainTop.IsChildOf(root)) continue;
                pb.ignoreTransforms.Remove(target);
                if (!pb.ignoreTransforms.Any(ignored => ignored != null && chainTop.IsChildOf(ignored)))
                    pb.ignoreTransforms.Add(chainTop);
            }

            var summary = string.Join(", ", driven.Select(d => $"{d.motion.Name} ({d.clips.Count})"));
            Debug.Log($"[Yuki Follow] {anchor.name} follows {surface.name}: {summary}.", anchor);
        }

        // value = rest + slope * (weight - restWeight), applied to a blendshape curve key by key.
        static AnimationCurve Map(AnimationCurve source, float rest, float slope, float restWeight)
        {
            var keys = source.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                k.value = rest + slope * (k.value - restWeight);
                if (!float.IsInfinity(k.inTangent)) k.inTangent *= slope;
                if (!float.IsInfinity(k.outTangent)) k.outTangent *= slope;
                keys[i] = k;
            }
            return new AnimationCurve(keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }

        static void WarnIfTransformAnimated(AnimatorServicesContext animators, ObjectPathRemapper paths, YukiSurfaceAnchor anchor)
        {
            var path = paths.GetVirtualPathForObject(anchor.transform);
            foreach (var clip in animators.AnimationIndex.GetClipsForObjectPath(path))
                foreach (var b in clip.GetFloatCurveBindings())
                    if (b.path == path && b.type == typeof(Transform))
                    {
                        FollowText.Errors.Report(ErrorSeverity.NonFatal, "error.transform_animated", anchor, anchor.name);
                        return;
                    }
        }
    }

    /// <summary>Bind bookkeeping shared by the inspector and the build.</summary>
    internal static class AnchorBinding
    {
        /// <summary>
        /// Resolving-phase step: VRCFury consumes its socket components before the anchor step runs,
        /// so the blendshapes a socket's depth animations drive are copied onto the anchor first.
        /// </summary>
        public static void CollectSocketShapes(BuildContext ctx)
        {
            foreach (var anchor in ctx.AvatarRootObject.GetComponentsInChildren<YukiSurfaceAnchor>(true))
                foreach (var name in SocketDepthShapes(anchor))
                    if (!anchor.ignoredBlendshapes.Contains(name)) anchor.ignoredBlendshapes.Add(name);
        }

        /// <summary>Blendshapes animated by the depth animations of an SPS socket on the same object.</summary>
        public static List<string> SocketDepthShapes(YukiSurfaceAnchor anchor)
        {
            var names = new List<string>();
            foreach (var component in anchor.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "VRCFuryHapticSocket") continue;
                var it = new SerializedObject(component).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.String && it.name == "blendShape"
                        && it.propertyPath.Contains("depthActions") && !string.IsNullOrEmpty(it.stringValue)
                        && !names.Contains(it.stringValue))
                        names.Add(it.stringValue);
            }
            return names;
        }

        public static System.Func<string, bool> IgnoreFilter(YukiSurfaceAnchor anchor)
        {
            var patterns = anchor.ignoredBlendshapes.Concat(SocketDepthShapes(anchor))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(p.Trim()).Replace("\\*", ".*") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToList();
            if (patterns.Count == 0) return null;
            return name => patterns.Any(p => p.IsMatch(name));
        }

        public const float MoveTolerance = 0.0001f; // 0.1 mm
        public const float TurnTolerance = 0.1f;    // degrees

        public static void Bind(YukiSurfaceAnchor anchor, Transform avatarRoot, SkinnedMeshRenderer surface)
        {
            anchor.bound = surface != null && surface.sharedMesh != null;
            anchor.bindSurface = surface;
            anchor.bindWeights.Clear();
            if (anchor.bound)
            {
                var mesh = surface.sharedMesh;
                for (int b = 0; b < mesh.blendShapeCount; b++)
                {
                    var w = surface.GetBlendShapeWeight(b);
                    if (Mathf.Abs(w) > 1e-4f) anchor.bindWeights.Add(new BindWeight { blendshape = mesh.GetBlendShapeName(b), weight = w });
                }
            }
            anchor.bindPoint = avatarRoot.InverseTransformPoint(anchor.transform.position);
            anchor.bindRotation = Quaternion.Inverse(avatarRoot.rotation) * anchor.transform.rotation;
        }

        public static bool MovedSinceBind(YukiSurfaceAnchor anchor, Transform avatarRoot)
        {
            var point = avatarRoot.InverseTransformPoint(anchor.transform.position);
            var rotation = Quaternion.Inverse(avatarRoot.rotation) * anchor.transform.rotation;
            return (point - anchor.bindPoint).magnitude > MoveTolerance
                   || Quaternion.Angle(rotation, anchor.bindRotation) > TurnTolerance;
        }

        /// <summary>
        /// The bind weights to measure from, or null when the object is taken to be placed at the
        /// surface's current weights (never bound, other surface, or moved since binding).
        /// </summary>
        public static Dictionary<string, float> EffectiveBind(YukiSurfaceAnchor anchor, Transform avatarRoot, SkinnedMeshRenderer surface)
        {
            if (!anchor.bound || anchor.bindSurface != surface || MovedSinceBind(anchor, avatarRoot)) return null;
            var map = new Dictionary<string, float>();
            foreach (var w in anchor.bindWeights)
                if (w != null && !string.IsNullOrEmpty(w.blendshape)) map[w.blendshape] = w.weight;
            return map;
        }
    }
}
