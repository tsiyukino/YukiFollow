using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace TsiYuki.Follow.Editor
{
    /// <summary>One follower blendshape and the source blendshape it copies.</summary>
    internal sealed class SyncLink
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Target;
        public string Name;
    }

    /// <summary>Which meshes follow which blendshapes, resolved the same way in the inspector and the build.</summary>
    internal sealed class SyncPlan
    {
        public List<SkinnedMeshRenderer> Sources = new List<SkinnedMeshRenderer>();
        public List<SyncLink> Links = new List<SyncLink>();

        public static SyncPlan Resolve(Transform avatarRoot, YukiBlendshapeSync config)
        {
            var plan = new SyncPlan();
            plan.Sources = config.sources.Where(s => s != null && s.sharedMesh != null).Distinct().ToList();
            if (plan.Sources.Count == 0) plan.Sources = AutoSources(avatarRoot);

            var excluded = new HashSet<SkinnedMeshRenderer>(config.excludedMeshes.Where(m => m != null));
            var patterns = config.excludedBlendshapes
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => new Regex("^" + Regex.Escape(p.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase))
                .ToList();

            foreach (var target in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (target.sharedMesh == null || plan.Sources.Contains(target) || excluded.Contains(target)) continue;
                var mesh = target.sharedMesh;
                for (int b = 0; b < mesh.blendShapeCount; b++)
                {
                    var name = mesh.GetBlendShapeName(b);
                    if (patterns.Any(p => p.IsMatch(name))) continue;
                    var source = plan.Sources.FirstOrDefault(s => s.sharedMesh.GetBlendShapeIndex(name) >= 0);
                    if (source != null) plan.Links.Add(new SyncLink { Source = source, Target = target, Name = name });
                }
            }
            return plan;
        }

        /// <summary>Meshes named "Body…" directly under the avatar root.</summary>
        public static List<SkinnedMeshRenderer> AutoSources(Transform avatarRoot)
        {
            var list = new List<SkinnedMeshRenderer>();
            foreach (Transform child in avatarRoot)
            {
                if (!child.name.StartsWith("Body", System.StringComparison.OrdinalIgnoreCase)) continue;
                var smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0) list.Add(smr);
            }
            return list;
        }

        public static float SourceWeight(SyncLink link) =>
            link.Source.GetBlendShapeWeight(link.Source.sharedMesh.GetBlendShapeIndex(link.Name));

        public static float TargetWeight(SyncLink link) =>
            link.Target.GetBlendShapeWeight(link.Target.sharedMesh.GetBlendShapeIndex(link.Name));
    }

    /// <summary>
    /// Build step for <see cref="YukiBlendshapeSync"/>: copies the sources' scene weights to the
    /// followers and adds the followers to every clip that animates the source blendshape. A clip
    /// that already sets the follower's blendshape keeps its own curve.
    /// </summary>
    internal static class BlendshapeSyncPass
    {
        public static void Run(BuildContext ctx)
        {
            var configs = ctx.AvatarRootObject.GetComponentsInChildren<YukiBlendshapeSync>(true);
            if (configs.Length == 0) return;
            if (configs.Length > 1)
                FollowText.Errors.Report(ErrorSeverity.Information, "error.sync_duplicate", configs[0]);
            var config = configs[0];
            var plan = SyncPlan.Resolve(ctx.AvatarRootTransform, config);

            int weights = 0, curves = 0;
            if (config.copyDefaultWeights)
                foreach (var link in plan.Links)
                {
                    var value = SyncPlan.SourceWeight(link);
                    if (Mathf.Approximately(SyncPlan.TargetWeight(link), value)) continue;
                    link.Target.SetBlendShapeWeight(link.Target.sharedMesh.GetBlendShapeIndex(link.Name), value);
                    weights++;
                }

            if (config.syncAnimations && plan.Links.Count > 0)
            {
                var animators = ctx.Extension<AnimatorServicesContext>();
                var paths = animators.ObjectPathRemapper;
                foreach (var group in plan.Links.GroupBy(l => (l.Source, l.Name)))
                {
                    var sourceBinding = Binding(paths, group.Key.Source, group.Key.Name);
                    var clips = animators.AnimationIndex.GetClipsForBinding(sourceBinding).ToList();
                    if (clips.Count == 0) continue;
                    var targets = group.Select(l => Binding(paths, l.Target, l.Name)).ToList();
                    foreach (var clip in clips)
                    {
                        var curve = clip.GetFloatCurve(sourceBinding);
                        if (curve == null) continue;
                        foreach (var target in targets)
                        {
                            if (clip.GetFloatCurve(target) != null) continue;
                            clip.SetFloatCurve(target, new AnimationCurve(curve.keys)
                            {
                                preWrapMode = curve.preWrapMode,
                                postWrapMode = curve.postWrapMode,
                            });
                            curves++;
                        }
                    }
                }
            }

            foreach (var c in configs) Object.DestroyImmediate(c);
            Debug.Log($"[Yuki Follow] Blendshape Sync: {plan.Links.Select(l => l.Target).Distinct().Count()} mesh(es), " +
                      $"{plan.Links.Count} blendshape(s); {weights} scene value(s) copied, {curves} curve(s) added.");
        }

        static EditorCurveBinding Binding(ObjectPathRemapper paths, SkinnedMeshRenderer smr, string blendshape) =>
            EditorCurveBinding.FloatCurve(paths.GetVirtualPathForObject(smr.transform), typeof(SkinnedMeshRenderer), "blendShape." + blendshape);
    }
}
