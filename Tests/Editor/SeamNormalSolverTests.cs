using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TsiYuki.Follow.Editor.Tests
{
    public class SeamNormalSolverTests
    {
        const float Step = 0.001f;
        const float Gap = 0.0005f;
        static readonly float Diagonal = Mathf.Sqrt(2f);
        static readonly Vector3 Tilted = new Vector3(1f, 1f, 0f).normalized; // 45° off the body's normal

        // Flat body facing up.
        static SeamGeometry Body() => Grid(-0.05f, 0.05f, -0.05f, 0.05f, 0.005f, (x, z) => 0f);

        // Square bump whose rim lies on the body; it rises 1 mm per mm toward the middle, so the
        // distance walked from the rim to a point is (0.02 - max(|x|, |z|)) * sqrt(2).
        static float BumpHeight(float x, float z) => 0.02f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        static SeamGeometry Bump() => Grid(-0.02f, 0.02f, -0.02f, 0.02f, Step, BumpHeight, Tilted);

        static SeamGeometry Grid(float x0, float x1, float z0, float z1, float step, Func<float, float, float> height, Vector3? normal = null)
        {
            int nx = Mathf.RoundToInt((x1 - x0) / step) + 1, nz = Mathf.RoundToInt((z1 - z0) / step) + 1;
            var positions = new Vector3[nx * nz];
            var normals = new Vector3[nx * nz];
            var triangles = new List<int>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = x0 + i * step, z = z0 + j * step;
                    positions[i * nz + j] = new Vector3(x, height(x, z), z);
                    normals[i * nz + j] = normal ?? Vector3.up;
                }
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    int a = i * nz + j, b = a + nz, c = a + 1, d = b + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            return new SeamGeometry { Positions = positions, Normals = normals, Triangles = triangles.ToArray() };
        }

        static int At(SeamGeometry g, float x, float z)
        {
            for (int i = 0; i < g.Positions.Length; i++)
                if (Mathf.Abs(g.Positions[i].x - x) < 1e-6f && Mathf.Abs(g.Positions[i].z - z) < 1e-6f) return i;
            throw new ArgumentException($"no vertex at {x}, {z}");
        }

        [Test]
        public void RimTakesTheSurfaceNormal()
        {
            var part = Bump();
            var result = SeamNormalSolver.Solve(part, Body(), 0.008f, Gap);
            Assert.IsNull(result.Error);
            Assert.That(result.SeamVertices, Is.EqualTo(160));
            for (int i = 0; i < part.Positions.Length; i++)
                if (BumpHeight(part.Positions[i].x, part.Positions[i].z) < 1e-6f)
                    Assert.That(Vector3.Angle(result.Normals[i], Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void BeyondTheBlendWidthNothingChanges()
        {
            const float width = 0.006f;
            var part = Bump();
            var result = SeamNormalSolver.Solve(part, Body(), width, Gap);
            int untouched = 0;
            for (int i = 0; i < part.Positions.Length; i++)
            {
                var walked = BumpHeight(part.Positions[i].x, part.Positions[i].z) * Diagonal;
                if (walked < width + 1e-4f) continue;
                Assert.That(result.Normals[i], Is.EqualTo(part.Normals[i]));
                Assert.That(result.Weights[i], Is.EqualTo(0f));
                untouched++;
            }
            Assert.That(untouched, Is.GreaterThan(0));
        }

        [Test]
        public void HalfWayBlendsHalfTheAngle()
        {
            var part = Bump();
            var result = SeamNormalSolver.Solve(part, Body(), 6 * Step * Diagonal, Gap);
            int i = At(part, 0.017f, 0f); // three steps in from the rim, half the blend width
            Assert.That(result.Weights[i], Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(Vector3.Angle(part.Normals[i], result.Normals[i]), Is.EqualTo(22.5f).Within(0.5f));
        }

        [Test]
        public void OpenEdgeAwayFromTheSurfaceIsNotASeam()
        {
            // A ramp: its x = 0 edge lies on the body, its x = 0.02 edge is 2 cm above it.
            var part = Grid(0f, 0.02f, -0.003f, 0.003f, Step, (x, z) => x, Tilted);
            var result = SeamNormalSolver.Solve(part, Body(), 0.005f, Gap);
            Assert.That(result.SeamVertices, Is.EqualTo(7));
            for (int j = 0; j < 7; j++)
                Assert.That(result.Weights[At(part, 0.02f, -0.003f + j * Step)], Is.EqualTo(0f));
        }

        [Test]
        public void NoSeamLeavesNormalsAlone()
        {
            var part = Grid(-0.01f, 0.01f, -0.01f, 0.01f, Step, (x, z) => 0.02f, Tilted);
            var result = SeamNormalSolver.Solve(part, Body(), 0.008f, Gap);
            Assert.That(result.Error, Is.EqualTo("no_seam"));
            Assert.IsNull(result.Normals);
        }

        [Test]
        public void SplitVerticesGetTheSameNormal()
        {
            // The bump built as two halves that share the z = 0 row by position only, like a UV seam.
            var a = Grid(-0.02f, 0.02f, -0.02f, 0f, Step, BumpHeight, Tilted);
            var b = Grid(-0.02f, 0.02f, 0f, 0.02f, Step, BumpHeight, Tilted);
            var triangles = new List<int>(a.Triangles);
            foreach (var t in b.Triangles) triangles.Add(t + a.Positions.Length);
            var part = new SeamGeometry
            {
                Positions = Concat(a.Positions, b.Positions),
                Normals = Concat(a.Normals, b.Normals),
                Triangles = triangles.ToArray(),
            };
            var result = SeamNormalSolver.Solve(part, Body(), 0.008f, Gap);
            Assert.That(result.SeamVertices, Is.EqualTo(160));
            for (int k = 0; k <= 40; k++)
            {
                float x = -0.02f + k * Step;
                var ia = At(a, x, 0f);
                var ib = At(b, x, 0f) + a.Positions.Length;
                Assert.That(Vector3.Angle(result.Normals[ia], result.Normals[ib]), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void FalloffEasesFromOneToZero()
        {
            Assert.That(SeamNormalSolver.Falloff(0f), Is.EqualTo(1f));
            Assert.That(SeamNormalSolver.Falloff(0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(SeamNormalSolver.Falloff(1f), Is.EqualTo(0f));
            Assert.That(SeamNormalSolver.Falloff(2f), Is.EqualTo(0f));
        }

        [Test]
        public void MeshNormalUndoesWorldNormal()
        {
            var skin = Matrix4x4.TRS(new Vector3(0.3f, 1f, -0.2f), Quaternion.Euler(30f, 40f, 50f), new Vector3(1f, 2f, 0.5f));
            var normal = new Vector3(0.3f, 0.8f, 0.1f).normalized;
            var tangent = Vector3.Cross(normal, Vector3.right).normalized;

            var world = SurfaceSolver.WorldNormal(skin, normal);
            Assert.That(Mathf.Abs(Vector3.Dot(world, skin.MultiplyVector(tangent).normalized)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Angle(SurfaceSolver.MeshNormal(skin, world), normal), Is.LessThan(0.01f));
        }

        static Vector3[] Concat(Vector3[] a, Vector3[] b)
        {
            var all = new Vector3[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }
}
