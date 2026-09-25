using System;
using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TsiYuki.Follow.Editor
{
    /// <summary>
    /// Build step for <see cref="YukiSeamNormals"/>. Runs before Avatar Optimizer, so the body still
    /// has the polygons a mask may later remove under the part: those are the surface the part
    /// replaces, and their normals are the ones to match. The part's mesh is replaced by a copy with
    /// the new normals; its blendshapes keep their own normal deltas.
    /// </summary>
    internal static class SeamNormalsPass
    {
        public static void Run(BuildContext ctx)
        {
            var configs = ctx.AvatarRootObject.GetComponentsInChildren<YukiSeamNormals>(true);
            foreach (var config in configs)
            {
                try { Apply(ctx, config); }
                catch (Exception e)
                {
                    Debug.LogException(e, config);
                    FollowText.Report(ErrorSeverity.NonFatal, "error.seam_failed", config, config.name, e.Message);
                }
            }
            foreach (var config in configs)
                if (config != null) Object.DestroyImmediate(config);
        }

        static void Apply(BuildContext ctx, YukiSeamNormals config)
        {
            var renderer = config.GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null) return;

            var part = Geometry(renderer, out var skin);
            var surface = ResolveSurface(ctx.AvatarRootTransform, config, renderer, part);
            if (surface == null)
            {
                FollowText.Report(ErrorSeverity.NonFatal, "error.seam_no_surface", config, config.name);
                return;
            }

            var result = SeamNormalSolver.Solve(part, Geometry(surface, out _), config.blendWidth, config.seamGap);
            if (result.Error != null)
            {
                FollowText.Report(ErrorSeverity.NonFatal, "error.seam_" + result.Error, config, config.name, (config.seamGap * 1000f).ToString("F1"));
                return;
            }

            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
                if (result.Weights[i] > 0f) normals[i] = SurfaceSolver.MeshNormal(skin[i], result.Normals[i]);

            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name + " (Seam Normals)";
            copy.normals = normals;
            ctx.AssetSaver.SaveAsset(copy);
            renderer.sharedMesh = copy;

            Debug.Log($"[Yuki Follow] {config.name} blends into {surface.name}: {result.SeamVertices} seam vertices, {result.BlendedVertices} blended, " +
                      $"normals turned {result.MeanAngle:F1}° on average, {result.MaxAngle:F1}° at most.", config);
        }

        /// <summary>The configured surface, or the mesh nearest to the part's open edges.</summary>
        internal static SkinnedMeshRenderer ResolveSurface(Transform avatarRoot, YukiSeamNormals config, SkinnedMeshRenderer renderer, SeamGeometry part)
        {
            if (config.surface != null) return config.surface.sharedMesh != null ? config.surface : null;
            return SurfaceSolver.FindNearestSurface(avatarRoot, SeamNormalSolver.OpenBoundaryCentroid(part), renderer);
        }

        /// <summary>World-space geometry of a skinned mesh at its current blendshape weights.</summary>
        internal static SeamGeometry Geometry(SkinnedMeshRenderer smr, out Matrix4x4[] skin)
        {
            var mesh = smr.sharedMesh;
            var weights = new float[mesh.blendShapeCount];
            for (int b = 0; b < weights.Length; b++) weights[b] = smr.GetBlendShapeWeight(b);
            var skinned = SurfaceSolver.Skin(smr, mesh, weights);
            skin = skinned.Skin;
            return new SeamGeometry { Positions = skinned.Positions, Normals = skinned.Normals, Triangles = mesh.triangles };
        }
    }
}
