using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsiYuki.Follow.Editor
{
    /// <summary>How one blendshape at full weight moves an anchored point, rigidly.</summary>
    internal sealed class ShapeMotion
    {
        public string Name;
        public int Index;
        public float FrameWeight;
        // Weight the surface has now (the build's rest state).
        public float RestWeight;
        // World-space change of the anchor for one full frame weight, measured on the bind geometry.
        public Vector3 DeltaPosition;
        public Quaternion DeltaRotation = Quaternion.identity;

        public float Distance => DeltaPosition.magnitude;
        public float Angle => Quaternion.Angle(Quaternion.identity, DeltaRotation);
    }

    internal sealed class AnchorSolution
    {
        public SkinnedMeshRenderer Surface;
        public int SampleCount;
        public float SampleRadius;
        // Distance from the anchor to the nearest surface vertex (bind geometry).
        public float SurfaceDistance;
        // Moving blendshapes, largest movement first.
        public List<ShapeMotion> Motions = new List<ShapeMotion>();
        // Pose the anchor should have at the surface's current weights (bind correction applied).
        public Vector3 RestPosition;
        public Quaternion RestRotation;
        public bool Corrected;
        public string Error;
    }

    /// <summary>
    /// Measures how the blendshapes of a skinned mesh move a point near its surface. Skinning is done
    /// here from the mesh data and the bones' current transforms, so nothing in the scene is touched
    /// (no temporary blendshape weights, no BakeMesh).
    /// </summary>
    internal static class SurfaceSolver
    {
        public const float MinDistance = 0.00005f; // 0.05 mm
        public const float MinAngle = 0.05f;       // degrees

        /// <summary>World-space vertex positions and normals, and per-vertex skinning matrices.</summary>
        internal sealed class Skinned
        {
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Matrix4x4[] Skin;
        }

        public static AnchorSolution Solve(SkinnedMeshRenderer surface, Vector3 position, Quaternion rotation,
            float radius, bool followRotation, IDictionary<string, float> bindWeights, Func<string, bool> ignore = null)
        {
            var result = new AnchorSolution { Surface = surface, RestPosition = position, RestRotation = rotation };
            var mesh = surface != null ? surface.sharedMesh : null;
            if (mesh == null) { result.Error = "no_mesh"; return result; }

            int shapeCount = mesh.blendShapeCount;
            var current = new float[shapeCount];
            var reference = new float[shapeCount];
            for (int b = 0; b < shapeCount; b++)
            {
                current[b] = surface.GetBlendShapeWeight(b);
                reference[b] = current[b];
                if (bindWeights != null)
                    reference[b] = bindWeights.TryGetValue(mesh.GetBlendShapeName(b), out var w) ? w : 0f;
            }

            var skinned = Skin(surface, mesh, reference);
            var points = skinned.Positions;

            // Surface patch around the anchor; widen it until there is enough to fit a frame.
            var sample = new List<int>();
            var nearest = float.MaxValue;
            for (int i = 0; i < points.Length; i++) nearest = Mathf.Min(nearest, (points[i] - position).sqrMagnitude);
            result.SurfaceDistance = Mathf.Sqrt(nearest);
            float r = Mathf.Max(radius, result.SurfaceDistance * 1.5f);
            for (int attempt = 0; attempt < 4; attempt++, r *= 2f)
            {
                sample.Clear();
                float r2 = r * r;
                for (int i = 0; i < points.Length; i++)
                    if ((points[i] - position).sqrMagnitude <= r2) sample.Add(i);
                if (sample.Count >= 6) break;
            }
            result.SampleRadius = r;
            result.SampleCount = sample.Count;
            if (sample.Count < 3) { result.Error = "no_surface"; return result; }

            var weights = new float[sample.Count];
            double wsum = 0;
            var centroid = Vector3.zero;
            for (int k = 0; k < sample.Count; k++)
            {
                float d = (points[sample[k]] - position).magnitude / r;
                float w = 1f - d * d;
                weights[k] = Mathf.Max(1e-4f, w * w);
                wsum += weights[k];
                centroid += points[sample[k]] * weights[k];
            }
            centroid /= (float)wsum;

            var deltas = new Vector3[mesh.vertexCount];
            for (int b = 0; b < shapeCount; b++)
            {
                int frame = mesh.GetBlendShapeFrameCount(b) - 1;
                if (frame < 0) continue;
                if (ignore != null && ignore(mesh.GetBlendShapeName(b))) continue;
                mesh.GetBlendShapeFrameVertices(b, frame, deltas, null, null);

                var moved = new Vector3[sample.Count];
                bool any = false;
                for (int k = 0; k < sample.Count; k++)
                {
                    int i = sample[k];
                    moved[k] = skinned.Skin[i].MultiplyVector(deltas[i]);
                    if (moved[k].sqrMagnitude > 1e-14f) any = true;
                }
                if (!any) continue;

                var t = Vector3.zero;
                for (int k = 0; k < sample.Count; k++) t += moved[k] * weights[k];
                t /= (float)wsum;

                var rot = Quaternion.identity;
                if (followRotation && sample.Count >= 3)
                    rot = FitRotation(sample, points, moved, weights, centroid, t);

                var motion = new ShapeMotion
                {
                    Name = mesh.GetBlendShapeName(b),
                    Index = b,
                    FrameWeight = mesh.GetBlendShapeFrameWeight(b, frame),
                    RestWeight = current[b],
                    DeltaPosition = centroid + t + rot * (position - centroid) - position,
                    DeltaRotation = rot,
                };
                if (motion.FrameWeight == 0f) motion.FrameWeight = 100f;
                if (motion.Distance >= MinDistance || motion.Angle >= MinAngle) result.Motions.Add(motion);
            }
            result.Motions.Sort((a, c) => c.Distance.CompareTo(a.Distance));

            // Bring the anchor from the bind weights to the weights the mesh has now.
            if (bindWeights != null)
            {
                var pos = position;
                var rotDelta = Quaternion.identity;
                foreach (var m in result.Motions)
                {
                    float s = (current[m.Index] - reference[m.Index]) / m.FrameWeight;
                    if (Mathf.Abs(s) < 1e-5f) continue;
                    pos += m.DeltaPosition * s;
                    rotDelta = Scale(m.DeltaRotation, s) * rotDelta;
                    result.Corrected = true;
                }
                result.RestPosition = pos;
                result.RestRotation = rotDelta * rotation;
            }
            return result;
        }

        /// <summary>Skins every vertex of the mesh at the given blendshape weights.</summary>
        public static Skinned Skin(SkinnedMeshRenderer smr, Mesh mesh, float[] shapeWeights)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var n = vertices.Length;
            bool hasNormals = normals.Length == n;
            if (shapeWeights != null)
            {
                var deltas = new Vector3[n];
                var deltaNormals = new Vector3[n];
                for (int b = 0; b < shapeWeights.Length && b < mesh.blendShapeCount; b++)
                {
                    float w = shapeWeights[b];
                    if (Mathf.Abs(w) < 1e-4f) continue;
                    int frame = mesh.GetBlendShapeFrameCount(b) - 1;
                    if (frame < 0) continue;
                    float fw = mesh.GetBlendShapeFrameWeight(b, frame);
                    if (fw == 0f) fw = 100f;
                    mesh.GetBlendShapeFrameVertices(b, frame, deltas, deltaNormals, null);
                    float s = w / fw;
                    for (int i = 0; i < n; i++) vertices[i] += deltas[i] * s;
                    if (hasNormals)
                        for (int i = 0; i < n; i++) normals[i] += deltaNormals[i] * s;
                }
            }

            var skin = new Matrix4x4[n];
            var bones = smr.bones;
            var bindposes = mesh.bindposes;
            bool rigged = bones != null && bones.Length > 0 && bindposes.Length == bones.Length;
            if (!rigged)
            {
                var m = smr.transform.localToWorldMatrix;
                for (int i = 0; i < n; i++) skin[i] = m;
            }
            else
            {
                var boneMatrices = new Matrix4x4[bones.Length];
                for (int j = 0; j < bones.Length; j++)
                    boneMatrices[j] = (bones[j] != null ? bones[j].localToWorldMatrix : smr.transform.localToWorldMatrix) * bindposes[j];
                var perVertex = mesh.GetBonesPerVertex();
                var all = mesh.GetAllBoneWeights();
                int cursor = 0;
                for (int i = 0; i < n; i++)
                {
                    int count = perVertex.Length > i ? perVertex[i] : 0;
                    var acc = new Matrix4x4();
                    float total = 0f;
                    for (int c = 0; c < count; c++)
                    {
                        var bw = all[cursor + c];
                        Add(ref acc, boneMatrices[bw.boneIndex], bw.weight);
                        total += bw.weight;
                    }
                    cursor += count;
                    if (total <= 0f) acc = smr.transform.localToWorldMatrix;
                    else if (Mathf.Abs(total - 1f) > 1e-4f) Scale(ref acc, 1f / total);
                    skin[i] = acc;
                }
            }

            var positions = new Vector3[n];
            var worldNormals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                positions[i] = skin[i].MultiplyPoint3x4(vertices[i]);
                worldNormals[i] = hasNormals ? WorldNormal(skin[i], normals[i]) : Vector3.up;
            }
            return new Skinned { Positions = positions, Normals = worldNormals, Skin = skin };
        }

        /// <summary>A mesh-space normal after skinning (inverse transpose, so scale does not tilt it).</summary>
        public static Vector3 WorldNormal(Matrix4x4 skin, Vector3 meshNormal) =>
            skin.inverse.transpose.MultiplyVector(meshNormal).normalized;

        /// <summary>The mesh-space normal that skins to the given world normal.</summary>
        public static Vector3 MeshNormal(Matrix4x4 skin, Vector3 worldNormal) =>
            skin.transpose.MultiplyVector(worldNormal).normalized;

        /// <summary>Distance from a point to the nearest vertex of the mesh at its current weights.</summary>
        public static float NearestVertexDistance(SkinnedMeshRenderer smr, Vector3 point)
        {
            var mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0) return float.MaxValue;
            var weights = new float[mesh.blendShapeCount];
            for (int b = 0; b < weights.Length; b++) weights[b] = smr.GetBlendShapeWeight(b);
            var skinned = Skin(smr, mesh, weights);
            float best = float.MaxValue;
            foreach (var p in skinned.Positions) best = Mathf.Min(best, (p - point).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        /// <summary>The avatar mesh whose surface is nearest to a point, or null beyond 5 cm.</summary>
        public static SkinnedMeshRenderer FindNearestSurface(Transform avatarRoot, Vector3 point, SkinnedMeshRenderer exclude = null)
        {
            SkinnedMeshRenderer best = null;
            float bestDistance = 0.05f;
            foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == exclude || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                var bounds = smr.bounds;
                if (bounds.size != Vector3.zero && bounds.SqrDistance(point) > bestDistance * bestDistance * 4f) continue;
                var d = NearestVertexDistance(smr, point);
                if (d < bestDistance) { bestDistance = d; best = smr; }
            }
            return best;
        }

        /// <summary>Rotation scaled by a factor along its own axis (slerp from identity, extrapolating).</summary>
        public static Quaternion Scale(Quaternion q, float factor)
        {
            q.ToAngleAxis(out var angle, out var axis);
            if (float.IsNaN(axis.x) || angle < 1e-6f) return Quaternion.identity;
            if (angle > 180f) angle -= 360f;
            return Quaternion.AngleAxis(angle * factor, axis);
        }

        /// <summary>Signed Euler angles (-180..180] of a rotation, for animation curves.</summary>
        public static Vector3 SignedEuler(Quaternion q)
        {
            var e = q.eulerAngles;
            for (int a = 0; a < 3; a++) if (e[a] > 180f) e[a] -= 360f;
            return e;
        }

        // Weighted best-fit rotation taking the rest patch onto the moved patch (Horn's quaternion
        // method: the eigenvector of the largest eigenvalue of a 4x4 symmetric matrix).
        static Quaternion FitRotation(List<int> sample, Vector3[] points, Vector3[] moved, float[] weights,
            Vector3 centroid, Vector3 translation)
        {
            double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
            for (int k = 0; k < sample.Count; k++)
            {
                var a = points[sample[k]] - centroid;
                var b = points[sample[k]] + moved[k] - centroid - translation;
                double w = weights[k];
                sxx += w * a.x * b.x; sxy += w * a.x * b.y; sxz += w * a.x * b.z;
                syx += w * a.y * b.x; syy += w * a.y * b.y; syz += w * a.y * b.z;
                szx += w * a.z * b.x; szy += w * a.z * b.y; szz += w * a.z * b.z;
            }
            var n = new double[4, 4]
            {
                { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
                { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
                { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
                { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz },
            };
            Jacobi(n, out var values, out var vectors);
            int best = 0;
            for (int i = 1; i < 4; i++) if (values[i] > values[best]) best = i;
            var q = new Quaternion((float)vectors[1, best], (float)vectors[2, best], (float)vectors[3, best], (float)vectors[0, best]);
            float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (len < 1e-6f || float.IsNaN(len)) return Quaternion.identity;
            q = new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len);
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            return q;
        }

        // Cyclic Jacobi eigen decomposition of a symmetric matrix. Columns of vectors are eigenvectors.
        static void Jacobi(double[,] a, out double[] values, out double[,] vectors)
        {
            int n = a.GetLength(0);
            vectors = new double[n, n];
            for (int i = 0; i < n; i++) vectors[i, i] = 1;
            for (int sweep = 0; sweep < 64; sweep++)
            {
                double off = 0;
                for (int p = 0; p < n; p++) for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
                if (off < 1e-30) break;
                for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = (theta >= 0 ? 1 : -1) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = vectors[k, p], vkq = vectors[k, q];
                        vectors[k, p] = c * vkp - s * vkq;
                        vectors[k, q] = s * vkp + c * vkq;
                    }
                }
            }
            values = new double[n];
            for (int i = 0; i < n; i++) values[i] = a[i, i];
        }

        static void Add(ref Matrix4x4 acc, Matrix4x4 m, float w)
        {
            for (int i = 0; i < 16; i++) acc[i] += m[i] * w;
        }

        static void Scale(ref Matrix4x4 acc, float s)
        {
            for (int i = 0; i < 16; i++) acc[i] *= s;
        }
    }
}
